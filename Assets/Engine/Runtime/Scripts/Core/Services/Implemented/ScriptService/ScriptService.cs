using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Script;
using Sinkii09.Engine.Services.Performance;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Enhanced Script Service interface with async operations, caching, hot-reload support, and comprehensive event system
    /// </summary>
    public interface IScriptService : IEngineService
    {
        #region Events
        /// <summary>
        /// Fired when a script loading operation starts
        /// </summary>
        event Action<string> ScriptLoadStarted;

        /// <summary>
        /// Fired when a script loading operation completes successfully
        /// </summary>
        event Action<string> ScriptLoadCompleted;

        /// <summary>
        /// Fired when a script loading operation fails
        /// </summary>
        event Action<string> ScriptLoadFailed;

        /// <summary>
        /// Fired when a script is successfully reloaded via hot-reload
        /// </summary>
        event Action<string> ScriptReloaded;
        #endregion

        #region Core Async Operations
        /// <summary>
        /// Load a script asynchronously by name
        /// </summary>
        /// <param name="name">Script name to load</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Loaded script instance</returns>
        UniTask<Script> LoadScriptAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load multiple scripts asynchronously
        /// </summary>
        /// <param name="names">Array of script names to load</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Collection of loaded script instances</returns>
        UniTask<IEnumerable<Script>> LoadScriptsAsync(string[] names, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a script exists without loading it
        /// </summary>
        /// <param name="name">Script name to check</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>True if script exists, false otherwise</returns>
        UniTask<bool> ScriptExistsAsync(string name, CancellationToken cancellationToken = default);
        #endregion

        #region Cache Management
        /// <summary>
        /// Check if a script is already loaded in cache
        /// </summary>
        /// <param name="name">Script name to check</param>
        /// <returns>True if script is loaded, false otherwise</returns>
        bool IsScriptLoaded(string name);

        /// <summary>
        /// Check if a script is currently being loaded
        /// </summary>
        /// <param name="name">Script name to check</param>
        /// <returns>True if script is being loaded, false otherwise</returns>
        bool IsScriptLoading(string name);

        /// <summary>
        /// Get a loaded script from cache or null if not loaded
        /// </summary>
        /// <param name="name">Script name to retrieve</param>
        /// <returns>Loaded script instance or null</returns>
        Script GetLoadedScriptOrNull(string name);

        /// <summary>
        /// Unload a specific script from cache
        /// </summary>
        /// <param name="name">Script name to unload</param>
        void UnloadScript(string name);

        /// <summary>
        /// Unload all scripts from cache
        /// </summary>
        /// <returns>Async task for the unload operation</returns>
        UniTask UnloadAllScriptsAsync();
        #endregion

        #region Hot-Reload Support
        /// <summary>
        /// Validate a script asynchronously
        /// </summary>
        /// <param name="name">Script name to validate</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>True if script is valid, false otherwise</returns>
        UniTask<bool> ValidateScriptAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reload a script asynchronously (hot-reload)
        /// </summary>
        /// <param name="name">Script name to reload</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Async task for the reload operation</returns>
        UniTask ReloadScriptAsync(string name, CancellationToken cancellationToken = default);
        #endregion
    }

    /// <summary>
    /// Enhanced Script Service implementation with async operations, caching, hot-reload support, and comprehensive monitoring
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.Critical, 
        Description = "Manages script loading and hot-reload with advanced caching and performance optimization",
        RequiredServices = new[] { typeof(IResourceService), typeof(IResourcePathResolver) })]
    [ServiceConfiguration(typeof(ScriptServiceConfiguration))]
    public class ScriptService : IScriptService, IMemoryPressureResponder
    {
        #region Events
        public event Action<string> ScriptLoadStarted;
        public event Action<string> ScriptLoadCompleted;
        public event Action<string> ScriptLoadFailed;
        public event Action<string> ScriptReloaded;
        #endregion

        #region Private Fields
        private readonly ScriptServiceConfiguration _config;
        private readonly IResourceService _resourceService;
        private readonly IServiceProvider _serviceProvider;
        private IResourcePathResolver _pathResolver;

        // Core data structures
        private readonly ConcurrentDictionary<string, Script> _scriptCache;
        private readonly ConcurrentDictionary<string, UniTask<Script>> _loadingTasks;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _loadingCancellationTokens;
        private readonly SemaphoreSlim _concurrentLoadSemaphore;

        // Hot-reload support
        private FileSystemWatcher _hotReloadWatcher;

        // Performance monitoring
        private readonly ScriptServiceStatistics _statistics;
        private readonly object _statisticsLock = new object();

        // Service state
        private bool _isInitialized;
        private bool _isDisposed;

        // Memory management
        private MemoryPressureMonitor.MemoryPressureLevel _currentMemoryPressure = MemoryPressureMonitor.MemoryPressureLevel.None;
        private DateTime _lastMemoryCleanup = DateTime.UtcNow;
        private readonly object _memoryManagementLock = new object();
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize ScriptService with dependency injection
        /// </summary>
        /// <param name="config">Service configuration</param>
        /// <param name="resourceService">Resource service dependency</param>
        /// <param name="serviceProvider">Service provider for additional dependencies</param>
        public ScriptService(ScriptServiceConfiguration config, IResourceService resourceService, IServiceProvider serviceProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Initialize data structures
            _scriptCache = new ConcurrentDictionary<string, Script>();
            _loadingTasks = new ConcurrentDictionary<string, UniTask<Script>>();
            _loadingCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
            _concurrentLoadSemaphore = new SemaphoreSlim(_config.MaxConcurrentLoads, _config.MaxConcurrentLoads);

            // Initialize statistics
            _statistics = new ScriptServiceStatistics
            {
                ServiceStartTime = DateTime.UtcNow,
                CacheInfo = new ScriptCacheInfo
                {
                    MaxCacheSize = _config.MaxCacheSize
                }
            };

            _isInitialized = false;
            _isDisposed = false;
        }
        #endregion

        #region IEngineService Implementation - Lifecycle Methods
        /// <summary>
        /// Initialize the ScriptService asynchronously
        /// </summary>
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isInitialized)
                {
                    return ServiceInitializationResult.Success();
                }

                UnityEngine.Debug.Log($"Initializing ScriptService with configuration: {_config.GetConfigurationSummary()}");

                // Validate ResourceService dependency
                if (_resourceService == null)
                {
                    return ServiceInitializationResult.Failed(new InvalidOperationException("ResourceService dependency is required"));
                }

                // Get ResourcePathResolver dependency
                _pathResolver = provider.GetService(typeof(IResourcePathResolver)) as IResourcePathResolver;
                if (_pathResolver == null)
                {
                    return ServiceInitializationResult.Failed(new InvalidOperationException("ResourcePathResolver dependency is required"));
                }

                // Initialize script cache
                InitializeScriptCache();

                // Set up hot-reload file watcher if enabled
                if (_config.EnableHotReload)
                {
                    SetupHotReloadWatcher();
                }

                // Perform script preloading if enabled
                if (_config.EnablePreloading)
                {
                    await PerformScriptPreloading(cancellationToken);
                }

                _isInitialized = true;
                UnityEngine.Debug.Log("ScriptService initialized successfully");

                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"ScriptService initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex);
            }
        }

        /// <summary>
        /// Perform health check on the ScriptService
        /// </summary>
        public UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isInitialized)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Unhealthy("Service not initialized"));
                }

                if (_isDisposed)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Unhealthy("Service disposed"));
                }

                // Check script cache integrity
                var cacheHealth = ValidateScriptCacheHealth();
                if (!cacheHealth.isHealthy)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Degraded($"Cache health issues: {cacheHealth.message}"));
                }

                // Check ResourceService dependency health
                // Note: Would need to call _resourceService.HealthCheckAsync() if available

                // Check hot-reload watcher status
                if (_config.EnableHotReload && _hotReloadWatcher == null)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Degraded("Hot-reload enabled but watcher not active"));
                }

                var healthInfo = $"Cache: {_scriptCache.Count}/{_config.MaxCacheSize}, " +
                                $"Active loads: {_statistics.ActiveLoadingOperations}, " +
                                $"Success rate: {_statistics.SuccessRate:F1}%";

                return UniTask.FromResult(ServiceHealthStatus.Healthy(healthInfo));
            }
            catch (Exception ex)
            {
                return UniTask.FromResult(ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}"));
            }
        }

        /// <summary>
        /// Shutdown the ScriptService gracefully
        /// </summary>
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isDisposed)
                {
                    return ServiceShutdownResult.Success();
                }

                UnityEngine.Debug.Log("Shutting down ScriptService...");

                // Cancel all pending loading operations
                await CancelAllPendingOperations();

                // Dispose file system watcher
                DisposeHotReloadWatcher();

                // Clear script cache and release memory
                ClearScriptCache();

                // Dispose semaphore
                _concurrentLoadSemaphore?.Dispose();

                // Dispose any remaining cancellation tokens
                foreach (var cts in _loadingCancellationTokens.Values)
                {
                    try
                    {
                        cts?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed - ignore
                    }
                }
                _loadingCancellationTokens.Clear();

                _isDisposed = true;
                UnityEngine.Debug.Log("ScriptService shutdown completed");

                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"ScriptService shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex);
            }
        }
        #endregion

        #region Private Helper Methods
        private void InitializeScriptCache()
        {
            _statistics.CacheInfo.MaxCacheSize = _config.MaxCacheSize;
            _statistics.CacheInfo.LastCacheCleanup = DateTime.UtcNow;
        }
        private async UniTask PerformScriptPreloading(CancellationToken cancellationToken)
        {
            try
            {
                if (!_config.EnablePreloading)
                {
                    UnityEngine.Debug.Log("Script preloading is disabled in configuration");
                    return;
                }

                await PreloadScriptsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Script preloading failed: {ex.Message}");
            }
        }

        private (bool isHealthy, string message) ValidateScriptCacheHealth()
        {
            try
            {
                var cacheCount = _scriptCache.Count;
                var maxCache = _config.MaxCacheSize;

                if (cacheCount > maxCache)
                {
                    return (false, $"Cache overflow: {cacheCount}/{maxCache}");
                }

                return (true, "Cache healthy");
            }
            catch (Exception ex)
            {
                return (false, $"Cache validation error: {ex.Message}");
            }
        }

        private async UniTask CancelAllPendingOperations()
        {
            if (_loadingTasks.Count == 0)
                return;

            UnityEngine.Debug.Log($"Cancelling {_loadingTasks.Count} pending script loading operations");

            // Get all cancellation tokens and cancel them
            var cancellationTokens = _loadingCancellationTokens.Values.ToArray();
            foreach (var cts in cancellationTokens)
            {
                try
                {
                    cts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Token was already disposed - ignore
                }
            }

            // Clear the dictionaries
            _loadingTasks.Clear();
            _loadingCancellationTokens.Clear();

            // Reset statistics
            lock (_statisticsLock)
            {
                _statistics.ActiveLoadingOperations = 0;
            }

            // Give tasks a moment to handle cancellation
            await UniTask.Yield();
        }

        private void ClearScriptCache()
        {
            _scriptCache.Clear();
            lock (_statisticsLock)
            {
                _statistics.CacheInfo.LoadedScriptCount = 0;
                _statistics.CacheInfo.LoadedScriptNames.Clear();
            }
        }
        #endregion

        #region IScriptService Implementation - Core Loading Methods
        /// <summary>
        /// Load a script asynchronously with caching, deduplication, and retry policies
        /// </summary>
        public async UniTask<Script> LoadScriptAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Script name cannot be null or empty", nameof(name));

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptService is not initialized");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Fire load started event
                ScriptLoadStarted?.Invoke(name);

                // Check cache first
                if (_config.EnableScriptCaching && _scriptCache.TryGetValue(name, out var cachedScript))
                {
                    UpdateCacheStatistics(true);
                    ScriptLoadCompleted?.Invoke(name);
                    stopwatch.Stop();
                    UpdateLoadTimeStatistics(stopwatch.Elapsed.TotalMilliseconds);
                    return cachedScript;
                }

                UpdateCacheStatistics(false);

                // Check if script is already being loaded (deduplication)
                if (_loadingTasks.TryGetValue(name, out var existingTask))
                {
                    return await existingTask;
                }

                // Create cancellation token source for this loading operation
                var loadingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _loadingCancellationTokens.TryAdd(name, loadingCts);

                // Create new loading task
                var loadingTask = LoadScriptInternalAsync(name, loadingCts.Token);
                _loadingTasks.TryAdd(name, loadingTask);

                try
                {
                    var result = await loadingTask;

                    // Cache the result if caching is enabled
                    if (_config.EnableScriptCaching && result != null)
                    {
                        // Check cache size limit
                        if (_scriptCache.Count >= _config.MaxCacheSize)
                        {
                            EvictOldestCacheEntry();
                        }

                        _scriptCache.TryAdd(name, result);
                        UpdateCacheInfo(name);
                    }

                    ScriptLoadCompleted?.Invoke(name);
                    stopwatch.Stop();
                    UpdateLoadTimeStatistics(stopwatch.Elapsed.TotalMilliseconds);
                    UpdateSuccessStatistics();

                    return result;
                }
                finally
                {
                    _loadingTasks.TryRemove(name, out _);
                    if (_loadingCancellationTokens.TryRemove(name, out var cts))
                    {
                        cts?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ScriptLoadFailed?.Invoke(name);
                stopwatch.Stop();
                UpdateFailureStatistics(ex);
                throw;
            }
        }

        /// <summary>
        /// Load multiple scripts asynchronously with batch optimization
        /// </summary>
        public async UniTask<IEnumerable<Script>> LoadScriptsAsync(string[] names, CancellationToken cancellationToken = default)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptService is not initialized");

            var results = new List<Script>();
            var loadTasks = new List<UniTask<Script>>();

            // Create loading tasks respecting concurrency limits
            foreach (var name in names.Where(n => !string.IsNullOrEmpty(n)))
            {
                loadTasks.Add(LoadScriptAsync(name, cancellationToken));
            }

            // Wait for all tasks to complete
            var loadedScripts = await UniTask.WhenAll(loadTasks);
            results.AddRange(loadedScripts.Where(s => s != null));

            return results;
        }

        /// <summary>
        /// Check if a script exists without loading it
        /// </summary>
        public async UniTask<bool> ScriptExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptService is not initialized");

            try
            {
                // Check cache first
                if (_config.EnableScriptCaching && _scriptCache.ContainsKey(name))
                {
                    return true;
                }

                // Check with ResourceService
                var scriptPath = BuildScriptPath(name);
                return await _resourceService.ResourceExistsAsync<TextAsset>(scriptPath, cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Error checking script existence for '{name}': {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Private Script Loading Implementation
        /// <summary>
        /// Internal script loading implementation with retry policies
        /// </summary>
        private async UniTask<Script> LoadScriptInternalAsync(string name, CancellationToken cancellationToken)
        {
            var retryCount = 0;
            var maxRetries = _config.MaxRetryAttempts;

            while (retryCount <= maxRetries)
            {
                try
                {
                    // Wait for semaphore to limit concurrent operations
                    await _concurrentLoadSemaphore.WaitAsync(cancellationToken);

                    try
                    {
                        lock (_statisticsLock)
                        {
                            _statistics.ActiveLoadingOperations++;
                        }

                        return await LoadScriptFromResourceService(name, cancellationToken);
                    }
                    finally
                    {
                        _concurrentLoadSemaphore.Release();
                        lock (_statisticsLock)
                        {
                            _statistics.ActiveLoadingOperations--;
                        }
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries && IsRetriableException(ex))
                {
                    retryCount++;
                    var delay = CalculateRetryDelay(retryCount);

                    UnityEngine.Debug.LogWarning($"Script loading attempt {retryCount} failed for '{name}': {ex.Message}. Retrying in {delay.TotalSeconds:F1}s...");

                    await UniTask.Delay(delay.Milliseconds, cancellationToken: cancellationToken);
                }
            }

            // If we get here, all retries failed
            throw new InvalidOperationException($"Failed to load script '{name}' after {maxRetries} retry attempts");
        }

        /// <summary>
        /// Load script from ResourceService and parse it
        /// </summary>
        private async UniTask<Script> LoadScriptFromResourceService(string name, CancellationToken cancellationToken)
        {
            var scriptPath = BuildScriptPath(name);

            // Load script text from ResourceService
            var resource = await _resourceService.LoadResourceAsync<TextAsset>(scriptPath, cancellationToken);

            if (resource?.Asset == null)
            {
                throw new FileNotFoundException($"Script resource not found: {scriptPath}");
            }

            // Parse script text using Script.FromScriptText
            var scriptText = resource.Asset.text;
            var script = Script.FromScripText(name, scriptText);

            if (script == null)
            {
                throw new InvalidDataException($"Failed to parse script: {name}");
            }

            return script;
        }

        /// <summary>
        /// Build the full path for a script resource
        /// </summary>
        private string BuildScriptPath(string scriptName)
        {
            if (_pathResolver != null)
            {
                // Use ResourcePathResolver for unified path resolution
                var pathParams = new PathParameter[]
                {
                    new PathParameter(PathParameterNames.SCRIPT_NAME, scriptName),
                };
                
                return _pathResolver.ResolveResourcePath(ResourceType.Script, scriptName, ResourceCategory.Source, pathParams);
            }
            
            // Fallback to legacy path building if PathResolver is not available
            var basePath = string.IsNullOrEmpty(_config.DefaultScriptsPath) ? "Scripts" : _config.DefaultScriptsPath;
            return $"{basePath}/{scriptName}";
        }

        /// <summary>
        /// Check if an exception is retriable
        /// </summary>
        private bool IsRetriableException(Exception ex)
        {
            // Don't retry for these types of exceptions
            if (ex is ArgumentException || ex is ArgumentNullException ||
                ex is InvalidDataException || ex is FileNotFoundException)
            {
                return false;
            }

            // Retry for network/IO related exceptions
            return ex is IOException || ex is TimeoutException || ex is OperationCanceledException;
        }

        /// <summary>
        /// Calculate retry delay with exponential backoff
        /// </summary>
        private TimeSpan CalculateRetryDelay(int retryAttempt)
        {
            var baseDelay = TimeSpan.FromSeconds(_config.RetryDelaySeconds);
            var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1));

            // Add jitter to prevent thundering herd
            var jitter = TimeSpan.FromMilliseconds(UnityEngine.Random.Range(0, 100));

            return exponentialDelay + jitter;
        }

        /// <summary>
        /// Evict oldest cache entry when cache is full
        /// </summary>
        private void EvictOldestCacheEntry()
        {
            // Simple LRU implementation - remove first item
            // In a production system, this would track access times
            var firstKey = _scriptCache.Keys.FirstOrDefault();
            if (firstKey != null)
            {
                _scriptCache.TryRemove(firstKey, out _);
                lock (_statisticsLock)
                {
                    _statistics.CacheInfo.LoadedScriptNames.Remove(firstKey);
                    _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                }
            }
        }
        #endregion

        #region Statistics Update Methods
        private void UpdateCacheStatistics(bool isHit)
        {
            lock (_statisticsLock)
            {
                if (isHit)
                {
                    _statistics.CacheInfo.CacheHits++;
                }
                else
                {
                    _statistics.CacheInfo.CacheMisses++;
                }
            }
        }

        private void UpdateCacheInfo(string scriptName)
        {
            lock (_statisticsLock)
            {
                _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                if (!_statistics.CacheInfo.LoadedScriptNames.Contains(scriptName))
                {
                    _statistics.CacheInfo.LoadedScriptNames.Add(scriptName);
                }
            }
        }

        private void UpdateLoadTimeStatistics(double loadTimeMs)
        {
            lock (_statisticsLock)
            {
                // Calculate running average
                var totalLoads = _statistics.TotalScriptsLoaded + 1;
                _statistics.AverageLoadTimeMs = (_statistics.AverageLoadTimeMs * _statistics.TotalScriptsLoaded + loadTimeMs) / totalLoads;
            }
        }

        private void UpdateSuccessStatistics()
        {
            lock (_statisticsLock)
            {
                _statistics.TotalScriptsLoaded++;
            }
        }

        private void UpdateFailureStatistics(Exception ex)
        {
            lock (_statisticsLock)
            {
                _statistics.TotalLoadFailures++;
                var errorType = ex.GetType().Name;
                if (_statistics.ErrorCountByType.ContainsKey(errorType))
                {
                    _statistics.ErrorCountByType[errorType]++;
                }
                else
                {
                    _statistics.ErrorCountByType[errorType] = 1;
                }
            }
        }
        #endregion

        #region Cache Management Implementation
        /// <summary>
        /// Check if a script is already loaded in cache
        /// </summary>
        public bool IsScriptLoaded(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_isInitialized)
                return false;

            return _config.EnableScriptCaching && _scriptCache.ContainsKey(name);
        }

        /// <summary>
        /// Check if a script is currently being loaded
        /// </summary>
        public bool IsScriptLoading(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_isInitialized)
                return false;

            return _loadingTasks.ContainsKey(name);
        }

        /// <summary>
        /// Get a loaded script from cache or null if not loaded
        /// </summary>
        public Script GetLoadedScriptOrNull(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (!_isInitialized)
                return null;

            if (!_config.EnableScriptCaching)
                return null;

            _scriptCache.TryGetValue(name, out var script);

            // Update cache hit statistics if script was found
            if (script != null)
            {
                UpdateCacheStatistics(true);
            }

            return script;
        }

        /// <summary>
        /// Unload a specific script from cache
        /// </summary>
        public void UnloadScript(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (!_isInitialized)
                return;

            try
            {
                // Remove from cache
                if (_scriptCache.TryRemove(name, out var removedScript))
                {
                    // Update cache statistics
                    lock (_statisticsLock)
                    {
                        _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                        _statistics.CacheInfo.LoadedScriptNames.Remove(name);
                    }

                    UnityEngine.Debug.Log($"Script '{name}' unloaded from cache");
                }

                // Cancel any pending loading task for this script
                if (_loadingTasks.TryRemove(name, out var loadingTask))
                {
                    // Cancel the specific loading operation
                    if (_loadingCancellationTokens.TryRemove(name, out var cts))
                    {
                        try
                        {
                            cts?.Cancel();
                            cts?.Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Token was already disposed - ignore
                        }
                    }
                    UnityEngine.Debug.Log($"Cancelled pending loading task for script '{name}'");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error unloading script '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Unload all scripts from cache
        /// </summary>
        public async UniTask UnloadAllScriptsAsync()
        {
            if (!_isInitialized)
                return;

            try
            {
                UnityEngine.Debug.Log("Starting to unload all scripts...");

                // Get list of all currently cached scripts
                var cachedScriptNames = _scriptCache.Keys.ToArray();
                var loadingTaskNames = _loadingTasks.Keys.ToArray();

                // Cancel all pending loading operations
                await CancelAllPendingOperations();
                // Clear the script cache
                _scriptCache.Clear();

                // Update statistics
                lock (_statisticsLock)
                {
                    _statistics.CacheInfo.LoadedScriptCount = 0;
                    _statistics.CacheInfo.LoadedScriptNames.Clear();
                    _statistics.CacheInfo.LastCacheCleanup = DateTime.UtcNow;
                }

                // Force garbage collection to free memory
                if (cachedScriptNames.Length > 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                UnityEngine.Debug.Log($"Successfully unloaded {cachedScriptNames.Length} cached scripts and cancelled {loadingTaskNames.Length} pending operations");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error during mass script unloading: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Enhanced Cache Management Methods
        /// <summary>
        /// Get comprehensive cache information for monitoring
        /// </summary>
        public ScriptCacheInfo GetCacheInfo()
        {
            if (!_isInitialized)
                return new ScriptCacheInfo();

            lock (_statisticsLock)
            {
                // Create a copy to avoid threading issues
                return new ScriptCacheInfo
                {
                    LoadedScriptCount = _statistics.CacheInfo.LoadedScriptCount,
                    MaxCacheSize = _statistics.CacheInfo.MaxCacheSize,
                    EstimatedMemoryUsage = _statistics.CacheInfo.EstimatedMemoryUsage,
                    CacheHits = _statistics.CacheInfo.CacheHits,
                    CacheMisses = _statistics.CacheInfo.CacheMisses,
                    LastCacheCleanup = _statistics.CacheInfo.LastCacheCleanup,
                    LoadedScriptNames = new List<string>(_statistics.CacheInfo.LoadedScriptNames)
                };
            }
        }

        /// <summary>
        /// Perform cache cleanup based on memory pressure
        /// </summary>
        public void PerformCacheCleanup(float memoryPressureLevel = 0.8f)
        {
            if (!_isInitialized || !_config.EnableScriptCaching)
                return;

            try
            {
                var currentCacheSize = _scriptCache.Count;
                var maxCacheSize = _config.MaxCacheSize;

                // Calculate how many items to remove based on memory pressure
                var targetRemovalCount = (int)(currentCacheSize * memoryPressureLevel * 0.3f); // Remove 30% when under pressure

                if (targetRemovalCount > 0)
                {
                    UnityEngine.Debug.Log($"Performing cache cleanup: removing {targetRemovalCount} scripts due to memory pressure ({memoryPressureLevel:F1})");

                    // Simple LRU-style cleanup - remove oldest entries
                    var keysToRemove = _scriptCache.Keys.Take(targetRemovalCount).ToArray();

                    foreach (var key in keysToRemove)
                    {
                        _scriptCache.TryRemove(key, out _);
                    }

                    // Update statistics
                    lock (_statisticsLock)
                    {
                        _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                        _statistics.CacheInfo.LastCacheCleanup = DateTime.UtcNow;

                        // Remove cleaned up scripts from the names list
                        foreach (var key in keysToRemove)
                        {
                            _statistics.CacheInfo.LoadedScriptNames.Remove(key);
                        }
                    }

                    UnityEngine.Debug.Log($"Cache cleanup completed: {keysToRemove.Length} scripts removed, {_scriptCache.Count} remaining");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error during cache cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Preload scripts based on configuration with optimized batch loading
        /// </summary>
        public async UniTask PreloadScriptsAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || !_config.EnablePreloading)
                return;

            var preloadStopwatch = Stopwatch.StartNew();

            try
            {
                var preloadPaths = ParsePreloadPaths();
                if (preloadPaths.Length == 0)
                {
                    UnityEngine.Debug.Log("No preload paths configured");
                    return;
                }

                UnityEngine.Debug.Log($"Starting optimized preload of {preloadPaths.Length} scripts: {string.Join(", ", preloadPaths)}");

                // Use optimized batch loading with concurrency control
                var results = await LoadScriptsBatchOptimized(preloadPaths, cancellationToken);

                preloadStopwatch.Stop();

                var successCount = results.Count(r => r != null);
                var loadTimeMs = preloadStopwatch.Elapsed.TotalMilliseconds;

                // Update preload statistics
                UpdatePreloadStatistics(successCount, preloadPaths.Length, loadTimeMs);

                UnityEngine.Debug.Log($"Preloading completed in {loadTimeMs:F1}ms: {successCount}/{preloadPaths.Length} scripts loaded successfully");
            }
            catch (Exception ex)
            {
                preloadStopwatch.Stop();
                UnityEngine.Debug.LogError($"Error during script preloading: {ex.Message}");
            }
        }

        /// <summary>
        /// Load scripts with optimized batch processing and concurrency control
        /// </summary>
        private async UniTask<Script[]> LoadScriptsBatchOptimized(string[] scriptNames, CancellationToken cancellationToken)
        {
            var batchSize = Math.Min(_config.MaxConcurrentLoads, scriptNames.Length);
            var results = new Script[scriptNames.Length];
            var semaphore = new SemaphoreSlim(batchSize, batchSize);

            try
            {
                // Create tasks for all scripts with controlled concurrency
                var loadTasks = scriptNames.Select(async (scriptName, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        results[index] = await LoadScriptWithOptimizedParsing(scriptName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to preload script '{scriptName}': {ex.Message}");
                        results[index] = null;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                // Wait for all tasks to complete
                await UniTask.WhenAll(loadTasks);

                return results;
            }
            finally
            {
                semaphore?.Dispose();
            }
        }

        /// <summary>
        /// Load script with optimized parsing and caching
        /// </summary>
        private async UniTask<Script> LoadScriptWithOptimizedParsing(string scriptName, CancellationToken cancellationToken)
        {
            // Check if already in cache first (fastest path)
            if (_config.EnableScriptCaching && _scriptCache.TryGetValue(scriptName, out var cachedScript))
            {
                return cachedScript;
            }

            // Use optimized loading path
            var scriptPath = BuildScriptPath(scriptName);
            var resource = await _resourceService.LoadResourceAsync<TextAsset>(scriptPath, cancellationToken);

            if (resource?.Asset == null)
            {
                return null;
            }

            // Optimized parsing with caching
            var script = ParseScriptWithCaching(scriptName, resource.Asset.text);

            // Add to cache immediately for subsequent requests
            if (_config.EnableScriptCaching && script != null)
            {
                _scriptCache.TryAdd(scriptName, script);
                UpdateCacheInfo(scriptName);
            }

            return script;
        }

        /// <summary>
        /// Optimized script parsing with result caching
        /// </summary>
        private Script ParseScriptWithCaching(string scriptName, string scriptText)
        {
            // In a production system, this could cache parsed results by content hash
            // For now, use the standard parsing but with optimizations
            try
            {
                // Validate content before parsing to avoid expensive parsing failures
                if (string.IsNullOrWhiteSpace(scriptText))
                {
                    return null;
                }

                // Pre-validate common issues to fail fast
                if (scriptText.Length > 1000000) // 1MB limit
                {
                    UnityEngine.Debug.LogWarning($"Script '{scriptName}' is very large ({scriptText.Length} characters) - may impact performance");
                }

                return Script.FromScripText(scriptName, scriptText);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Optimized parsing failed for '{scriptName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse preload paths from configuration with pattern support
        /// </summary>
        private string[] ParsePreloadPaths()
        {
            var preloadPaths = _config.PreloadPaths?.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList() ?? new List<string>();

            // Expand any wildcard patterns (basic implementation)
            var expandedPaths = new List<string>();
            foreach (var path in preloadPaths)
            {
                if (path.Contains("*"))
                {
                    expandedPaths.AddRange(ExpandWildcardPath(path));
                }
                else
                {
                    expandedPaths.Add(path);
                }
            }

            return expandedPaths.Distinct().ToArray();
        }

        /// <summary>
        /// Expand wildcard patterns in preload paths (basic implementation)
        /// </summary>
        private IEnumerable<string> ExpandWildcardPath(string wildcardPath)
        {
            try
            {
                // Basic wildcard expansion - in production this could be more sophisticated
                if (wildcardPath.EndsWith("/*"))
                {
                    var basePath = wildcardPath.Substring(0, wildcardPath.Length - 2);
                    return new[] { $"{basePath}/Core", $"{basePath}/Boot", $"{basePath}/Essential" };
                }

                // For now, just return the original path
                return new[] { wildcardPath.Replace("*", "Core") };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to expand wildcard path '{wildcardPath}': {ex.Message}");
                return new[] { wildcardPath };
            }
        }

        /// <summary>
        /// Warm up the script cache with frequently accessed scripts
        /// </summary>
        public async UniTask WarmupCacheAsync(string[] frequentlyUsedScripts = null, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || !_config.EnableScriptCaching)
                return;

            var warmupStopwatch = Stopwatch.StartNew();

            try
            {
                // Use provided scripts or default warmup set
                var scriptsToWarmup = frequentlyUsedScripts ?? GetDefaultWarmupScripts();

                if (scriptsToWarmup.Length == 0)
                {
                    UnityEngine.Debug.Log("No scripts configured for cache warmup");
                    return;
                }

                UnityEngine.Debug.Log($"Starting cache warmup for {scriptsToWarmup.Length} frequently used scripts");

                // Load scripts in background without blocking
                var warmupTasks = scriptsToWarmup.Select(async scriptName =>
                {
                    try
                    {
                        await LoadScriptWithOptimizedParsing(scriptName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Cache warmup failed for script '{scriptName}': {ex.Message}");
                    }
                }).ToArray();

                await UniTask.WhenAll(warmupTasks);

                warmupStopwatch.Stop();

                var warmedScripts = scriptsToWarmup.Count(s => _scriptCache.ContainsKey(s));
                UnityEngine.Debug.Log($"Cache warmup completed in {warmupStopwatch.Elapsed.TotalMilliseconds:F1}ms: {warmedScripts}/{scriptsToWarmup.Length} scripts cached");
            }
            catch (Exception ex)
            {
                warmupStopwatch.Stop();
                UnityEngine.Debug.LogError($"Cache warmup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get default scripts for cache warmup
        /// </summary>
        private string[] GetDefaultWarmupScripts()
        {
            // Return commonly used scripts - could be configuration-driven
            return new[]
            {
                "Boot",
                "Core",
                "Initialize",
                "Main",
                "Common"
            };
        }

        /// <summary>
        /// Optimize script cache for better performance
        /// </summary>
        public void OptimizeCache()
        {
            if (!_isInitialized || !_config.EnableScriptCaching)
                return;

            try
            {
                var optimizationStopwatch = Stopwatch.StartNew();

                UnityEngine.Debug.Log("Starting cache optimization...");

                // Get current cache statistics
                var initialCount = _scriptCache.Count;
                var initialMemory = GC.GetTotalMemory(false);

                // Perform cache compaction and optimization
                CompactCache();

                // Force garbage collection to clean up unused objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                optimizationStopwatch.Stop();

                var finalMemory = GC.GetTotalMemory(false);
                var memoryFreed = initialMemory - finalMemory;

                UnityEngine.Debug.Log($"Cache optimization completed in {optimizationStopwatch.Elapsed.TotalMilliseconds:F1}ms. " +
                                    $"Scripts: {initialCount} -> {_scriptCache.Count}, " +
                                    $"Memory freed: {memoryFreed / 1024:F1}KB");

                // Update statistics
                lock (_statisticsLock)
                {
                    _statistics.CacheInfo.LastCacheCleanup = DateTime.UtcNow;
                    _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Cache optimization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Compact cache by removing least recently used items
        /// </summary>
        private void CompactCache()
        {
            var currentSize = _scriptCache.Count;
            var targetSize = (int)(_config.MaxCacheSize * 0.8f); // Keep 80% of max size

            if (currentSize <= targetSize)
                return;

            var itemsToRemove = currentSize - targetSize;
            var keysToRemove = _scriptCache.Keys.Take(itemsToRemove).ToArray();

            foreach (var key in keysToRemove)
            {
                _scriptCache.TryRemove(key, out _);

                lock (_statisticsLock)
                {
                    _statistics.CacheInfo.LoadedScriptNames.Remove(key);
                }
            }

            UnityEngine.Debug.Log($"Cache compacted: removed {keysToRemove.Length} scripts");
        }

        /// <summary>
        /// Update preload statistics
        /// </summary>
        private void UpdatePreloadStatistics(int successCount, int totalCount, double loadTimeMs)
        {
            // In a production system, this would track preload-specific metrics
            UnityEngine.Debug.Log($"Preload statistics: {successCount}/{totalCount} successful, {loadTimeMs:F1}ms total time");
        }

        /// <summary>
        /// Get list of all currently loaded script names
        /// </summary>
        public IEnumerable<string> GetLoadedScriptNames()
        {
            if (!_isInitialized || !_config.EnableScriptCaching)
                return Enumerable.Empty<string>();

            return _scriptCache.Keys.ToArray(); // Create a copy to avoid concurrent modification
        }

        /// <summary>
        /// Get list of all currently loading script names
        /// </summary>
        public IEnumerable<string> GetLoadingScriptNames()
        {
            if (!_isInitialized)
                return Enumerable.Empty<string>();

            return _loadingTasks.Keys.ToArray(); // Create a copy to avoid concurrent modification
        }
        #endregion

        #region Hot-Reload System Implementation
        /// <summary>
        /// Validate a script asynchronously with comprehensive syntax checking
        /// </summary>
        public async UniTask<bool> ValidateScriptAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptService is not initialized");

            var validationStopwatch = Stopwatch.StartNew();

            try
            {
                // Load script content for validation
                var scriptPath = BuildScriptPath(name);
                var resource = await _resourceService.LoadResourceAsync<TextAsset>(scriptPath, cancellationToken);

                if (resource?.Asset == null)
                {
                    return false; // Script doesn't exist
                }

                var scriptText = resource.Asset.text;

                // Validate script content
                var validationResult = await ValidateScriptContentAsync(name, scriptText, cancellationToken);

                validationStopwatch.Stop();

                // Update validation statistics
                lock (_statisticsLock)
                {
                    _statistics.TotalValidations++;
                    var totalValidations = _statistics.TotalValidations;
                    _statistics.AverageValidationTimeMs = (_statistics.AverageValidationTimeMs * (totalValidations - 1) + validationStopwatch.Elapsed.TotalMilliseconds) / totalValidations;
                }

                return validationResult.IsValid;
            }
            catch (Exception ex)
            {
                validationStopwatch.Stop();
                UnityEngine.Debug.LogError($"Script validation failed for '{name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reload a script asynchronously with atomic replacement and rollback support
        /// </summary>
        public async UniTask ReloadScriptAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Script name cannot be null or empty", nameof(name));

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptService is not initialized");

            var reloadStopwatch = Stopwatch.StartNew();
            Script originalScript = null;

            try
            {
                UnityEngine.Debug.Log($"Starting hot-reload for script '{name}'");

                // Store original script for rollback if needed
                _scriptCache.TryGetValue(name, out originalScript);

                // Validate script before reloading
                var isValid = await ValidateScriptAsync(name, cancellationToken);
                if (!isValid)
                {
                    throw new InvalidDataException($"Script '{name}' failed validation - hot-reload aborted");
                }

                // Remove from cache temporarily to force reload
                _scriptCache.TryRemove(name, out _);

                // Load the updated script
                var reloadedScript = await LoadScriptAsync(name, cancellationToken);

                if (reloadedScript == null)
                {
                    throw new InvalidOperationException($"Failed to reload script '{name}' - script loading returned null");
                }

                reloadStopwatch.Stop();

                // Update reload statistics
                lock (_statisticsLock)
                {
                    _statistics.TotalReloads++;
                }

                // Fire reload completed event
                ScriptReloaded?.Invoke(name);

                UnityEngine.Debug.Log($"Hot-reload completed for script '{name}' in {reloadStopwatch.Elapsed.TotalMilliseconds:F1}ms");
            }
            catch (Exception ex)
            {
                reloadStopwatch.Stop();

                // Attempt rollback if we had an original script
                if (originalScript != null)
                {
                    try
                    {
                        _scriptCache.TryAdd(name, originalScript);
                        UnityEngine.Debug.Log($"Rolled back script '{name}' to previous version after reload failure");
                    }
                    catch (Exception rollbackEx)
                    {
                        UnityEngine.Debug.LogError($"Failed to rollback script '{name}': {rollbackEx.Message}");
                    }
                }

                UnityEngine.Debug.LogError($"Hot-reload failed for script '{name}': {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Hot-Reload Support Methods
        /// <summary>
        /// Set up FileSystemWatcher for hot-reload file monitoring
        /// </summary>
        private void SetupHotReloadWatcher()
        {
            try
            {
                if (_hotReloadWatcher != null)
                {
                    _hotReloadWatcher.Dispose();
                    _hotReloadWatcher = null;
                }

                // Get the full path for script monitoring
                var scriptsPath = GetScriptsDirectoryPath();

                if (!Directory.Exists(scriptsPath))
                {
                    UnityEngine.Debug.LogWarning($"Scripts directory does not exist: {scriptsPath}. Hot-reload disabled.");
                    return;
                }

                _hotReloadWatcher = new FileSystemWatcher(scriptsPath)
                {
                    Filter = "*.script", // Monitor .script files
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
                };

                // Set up event handlers with debouncing
                var debouncedHandler = CreateDebouncedFileHandler();
                _hotReloadWatcher.Changed += debouncedHandler;
                _hotReloadWatcher.Created += debouncedHandler;
                _hotReloadWatcher.Renamed += (sender, e) => debouncedHandler(sender, e);

                _hotReloadWatcher.Error += OnFileWatcherError;
                _hotReloadWatcher.EnableRaisingEvents = true;

                UnityEngine.Debug.Log($"Hot-reload file watcher enabled for directory: {scriptsPath}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to setup hot-reload watcher: {ex.Message}");
                _hotReloadWatcher = null;
            }
        }

        /// <summary>
        /// Create debounced file change handler to prevent rapid fire events
        /// </summary>
        private FileSystemEventHandler CreateDebouncedFileHandler()
        {
            var lastEventTimes = new ConcurrentDictionary<string, DateTime>();
            var debounceInterval = TimeSpan.FromMilliseconds(_config.HotReloadInterval * 1000);

            return async (sender, e) =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var filePath = e.FullPath;

                    // Debounce rapid events for the same file
                    if (lastEventTimes.TryGetValue(filePath, out var lastTime) &&
                        (now - lastTime) < debounceInterval)
                    {
                        return; // Skip this event - too soon after last one
                    }

                    lastEventTimes[filePath] = now;

                    // Extract script name from file path
                    var scriptName = GetScriptNameFromFilePath(filePath);
                    if (string.IsNullOrEmpty(scriptName))
                        return;

                    UnityEngine.Debug.Log($"Hot-reload triggered for script: {scriptName}");

                    // Perform hot-reload asynchronously
                    await ReloadScriptAsync(scriptName);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Error in hot-reload file handler: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// Handle FileSystemWatcher errors
        /// </summary>
        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            UnityEngine.Debug.LogError($"FileSystemWatcher error: {e.GetException().Message}");

            // Try to restart the watcher
            try
            {
                if (_config.EnableHotReload)
                {
                    SetupHotReloadWatcher();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to restart FileSystemWatcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the full directory path for scripts monitoring
        /// </summary>
        private string GetScriptsDirectoryPath()
        {
            var basePath = string.IsNullOrEmpty(_config.DefaultScriptsPath) ? "Scripts" : _config.DefaultScriptsPath;

            // Convert to full path - for Unity projects, this would be relative to Assets
            if (Path.IsPathRooted(basePath))
            {
                return basePath;
            }
            else
            {
                // Combine with Unity's Application.dataPath or a project-relative path
                return Path.Combine(Application.dataPath, basePath);
            }
        }

        /// <summary>
        /// Extract script name from file path
        /// </summary>
        private string GetScriptNameFromFilePath(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                // Validate the file name
                if (string.IsNullOrEmpty(fileName) || fileName.Contains("~") || fileName.StartsWith("."))
                {
                    return null; // Skip temporary or hidden files
                }

                return fileName;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to extract script name from path '{filePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validate script content with comprehensive syntax checking
        /// </summary>
        private async UniTask<ScriptValidationResult> ValidateScriptContentAsync(string scriptName, string scriptText, CancellationToken cancellationToken)
        {
            var validationStopwatch = Stopwatch.StartNew();
            var errors = new List<string>();
            var warnings = new List<string>();

            try
            {
                // Basic content validation
                if (string.IsNullOrWhiteSpace(scriptText))
                {
                    errors.Add("Script content is empty or whitespace");
                    return ScriptValidationResult.CreateInvalid(scriptName, errors, validationStopwatch.Elapsed, warnings);
                }

                // Validate script parsing
                Script parsedScript = null;
                try
                {
                    parsedScript = Script.FromScripText(scriptName, scriptText);
                }
                catch (Exception parseEx)
                {
                    errors.Add($"Script parsing failed: {parseEx.Message}");
                    return ScriptValidationResult.CreateInvalid(scriptName, errors, validationStopwatch.Elapsed, warnings);
                }

                if (parsedScript == null)
                {
                    errors.Add("Script parsing returned null");
                    return ScriptValidationResult.CreateInvalid(scriptName, errors, validationStopwatch.Elapsed, warnings);
                }

                // Validate script lines
                if (parsedScript.Lines == null || parsedScript.Lines.Count == 0)
                {
                    warnings.Add("Script has no content lines");
                }

                // Additional validation rules
                await ValidateScriptRules(parsedScript, errors, warnings, cancellationToken);

                validationStopwatch.Stop();

                // Return validation result
                bool isValid = errors.Count == 0;
                return isValid
                    ? ScriptValidationResult.CreateValid(scriptName, validationStopwatch.Elapsed, warnings)
                    : ScriptValidationResult.CreateInvalid(scriptName, errors, validationStopwatch.Elapsed, warnings);
            }
            catch (Exception ex)
            {
                validationStopwatch.Stop();
                errors.Add($"Validation process failed: {ex.Message}");
                return ScriptValidationResult.CreateInvalid(scriptName, errors, validationStopwatch.Elapsed, warnings);
            }
        }

        /// <summary>
        /// Validate script against business rules and domain-specific requirements
        /// </summary>
        private async UniTask ValidateScriptRules(Script script, List<string> errors, List<string> warnings, CancellationToken cancellationToken)
        {
            try
            {
                // Add timeout for validation to prevent hanging
                var validationTimeout = TimeSpan.FromSeconds(_config.ScriptValidationTimeout);
                using var timeoutCts = new CancellationTokenSource(validationTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await UniTask.Delay(1, cancellationToken: combinedCts.Token); // Placeholder for async validation

                // Example validation rules (customize based on your script domain)
                foreach (var line in script.Lines)
                {
                    if (line == null)
                    {
                        warnings.Add("Script contains null line");
                        continue;
                    }

                    // Validate line types and content
                    var lineContent = line.ToString();
                    if (string.IsNullOrWhiteSpace(lineContent))
                    {
                        // Empty lines are generally okay
                        continue;
                    }

                    // Add domain-specific validation rules here
                    // For example: validate command syntax, parameter formats, etc.
                }

                // Check for script length limits
                if (script.Lines.Count > 10000) // Example limit
                {
                    warnings.Add($"Script is very long ({script.Lines.Count} lines) - consider breaking into smaller scripts");
                }
            }
            catch (OperationCanceledException)
            {
                errors.Add($"Script validation timed out after {_config.ScriptValidationTimeout} seconds");
            }
            catch (Exception ex)
            {
                errors.Add($"Validation rule check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose hot-reload watcher safely
        /// </summary>
        private void DisposeHotReloadWatcher()
        {
            try
            {
                if (_hotReloadWatcher != null)
                {
                    _hotReloadWatcher.EnableRaisingEvents = false;
                    _hotReloadWatcher.Dispose();
                    _hotReloadWatcher = null;
                    UnityEngine.Debug.Log("Hot-reload file watcher disposed");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Error disposing hot-reload watcher: {ex.Message}");
            }
        }
        #endregion

        #region IMemoryPressureResponder Implementation
        /// <summary>
        /// Respond to memory pressure by performing adaptive cleanup based on pressure level
        /// </summary>
        public async UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            if (!_isInitialized || _isDisposed)
                return;

            var responseStopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                UnityEngine.Debug.Log($"ScriptService responding to memory pressure: Level={pressureLevel}, Strategy={strategy}");

                // Update current memory pressure level
                lock (_memoryManagementLock)
                {
                    _currentMemoryPressure = pressureLevel;
                }

                // Perform cleanup based on pressure level
                await PerformMemoryPressureCleanup(pressureLevel, strategy);

                // Update cleanup timestamp
                lock (_memoryManagementLock)
                {
                    _lastMemoryCleanup = DateTime.UtcNow;
                }

                responseStopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryFreed = Math.Max(0, memoryBefore - memoryAfter);

                UnityEngine.Debug.Log($"ScriptService memory pressure response completed in {responseStopwatch.Elapsed.TotalMilliseconds:F1}ms. " +
                                    $"Memory freed: {memoryFreed / 1024:F1}KB, Pressure level: {pressureLevel}");
            }
            catch (Exception ex)
            {
                responseStopwatch.Stop();
                UnityEngine.Debug.LogError($"Error during ScriptService memory pressure response: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle memory pressure level changes
        /// </summary>
        public void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel)
        {
            if (!_isInitialized || _isDisposed)
                return;

            try
            {
                lock (_memoryManagementLock)
                {
                    _currentMemoryPressure = newLevel;
                }

                UnityEngine.Debug.Log($"ScriptService memory pressure level changed: {previousLevel} -> {newLevel}");

                // Adjust caching behavior based on pressure level
                AdjustCachingBehavior(newLevel);

                // Update statistics
                lock (_statisticsLock)
                {
                    _statistics.CacheInfo.EstimatedMemoryUsage = CalculateEstimatedMemoryUsage();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error handling memory pressure level change: {ex.Message}");
            }
        }
        #endregion

        #region Memory Management Implementation
        /// <summary>
        /// Perform memory pressure cleanup based on pressure level and strategy
        /// </summary>
        private async UniTask PerformMemoryPressureCleanup(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            switch (pressureLevel)
            {
                case MemoryPressureMonitor.MemoryPressureLevel.Low:
                    await PerformLowPressureCleanup(strategy);
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.Medium:
                    await PerformMediumPressureCleanup(strategy);
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.High:
                    await PerformHighPressureCleanup(strategy);
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.Critical:
                    await PerformCriticalPressureCleanup(strategy);
                    break;

                default:
                    // No cleanup needed for None level
                    break;
            }
        }

        /// <summary>
        /// Perform cleanup for low memory pressure
        /// </summary>
        private async UniTask PerformLowPressureCleanup(MemoryPressureMonitor.CleanupStrategy strategy)
        {
            // Conservative cleanup - only remove oldest cached scripts if cache is near limit
            var cacheUsage = (float)_scriptCache.Count / _config.MaxCacheSize;
            if (cacheUsage > 0.9f) // Only if cache is 90% full
            {
                PerformCacheCleanup(0.1f); // Remove 10% of cache
                UnityEngine.Debug.Log("Low pressure cleanup: Removed 10% of script cache");
            }

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Perform cleanup for medium memory pressure
        /// </summary>
        private async UniTask PerformMediumPressureCleanup(MemoryPressureMonitor.CleanupStrategy strategy)
        {
            // Moderate cleanup - reduce cache size and cancel non-essential operations
            var cacheUsage = (float)_scriptCache.Count / _config.MaxCacheSize;
            if (cacheUsage > 0.7f)
            {
                PerformCacheCleanup(0.3f); // Remove 30% of cache
                UnityEngine.Debug.Log("Medium pressure cleanup: Removed 30% of script cache");
            }

            // Cancel non-essential loading operations
            await CancelNonEssentialOperations();
        }

        /// <summary>
        /// Perform cleanup for high memory pressure
        /// </summary>
        private async UniTask PerformHighPressureCleanup(MemoryPressureMonitor.CleanupStrategy strategy)
        {
            // Aggressive cleanup - significantly reduce cache and free memory
            PerformCacheCleanup(0.5f); // Remove 50% of cache

            // Cancel all non-critical loading operations
            await CancelNonEssentialOperations();

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();

            UnityEngine.Debug.Log("High pressure cleanup: Removed 50% of script cache and forced GC");
        }

        /// <summary>
        /// Perform cleanup for critical memory pressure
        /// </summary>
        private async UniTask PerformCriticalPressureCleanup(MemoryPressureMonitor.CleanupStrategy strategy)
        {
            // Emergency cleanup - clear all non-essential cached content
            var criticalScripts = GetCriticalScripts();

            // Keep only critical scripts
            var scriptsToRemove = _scriptCache.Keys
                .Where(key => !criticalScripts.Contains(key))
                .ToArray();

            foreach (var scriptName in scriptsToRemove)
            {
                _scriptCache.TryRemove(scriptName, out _);
            }

            // Cancel all pending operations
            await CancelAllPendingOperations();

            // Aggressive garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Update statistics
            lock (_statisticsLock)
            {
                _statistics.CacheInfo.LoadedScriptCount = _scriptCache.Count;
                _statistics.CacheInfo.LoadedScriptNames.Clear();
                _statistics.CacheInfo.LoadedScriptNames.AddRange(_scriptCache.Keys);
            }

            UnityEngine.Debug.Log($"Critical pressure cleanup: Removed {scriptsToRemove.Length} scripts, kept {_scriptCache.Count} critical scripts");
        }

        /// <summary>
        /// Get list of critical scripts that should not be unloaded during emergency cleanup
        /// </summary>
        private string[] GetCriticalScripts()
        {
            // Define critical scripts that should always remain cached
            var criticalScripts = new List<string>();

            // Add commonly used scripts
            var commonScripts = new[] { "Boot", "Core", "Initialize", "Main" };
            foreach (var script in commonScripts)
            {
                if (_scriptCache.ContainsKey(script))
                {
                    criticalScripts.Add(script);
                }
            }

            // Add recently accessed scripts (basic implementation)
            var recentScripts = _scriptCache.Keys
                .Take(Math.Min(5, _scriptCache.Count / 10)) // Keep 10% or 5 scripts, whichever is smaller
                .ToArray();

            criticalScripts.AddRange(recentScripts);

            return criticalScripts.Distinct().ToArray();
        }

        /// <summary>
        /// Cancel non-essential loading operations to free up resources
        /// </summary>
        private async UniTask CancelNonEssentialOperations()
        {
            // In a production system, this would identify and cancel non-critical loading tasks
            // For now, we'll just log the action
            var pendingCount = _loadingTasks.Count;
            if (pendingCount > 0)
            {
                UnityEngine.Debug.Log($"Memory pressure: {pendingCount} loading operations will complete but new non-essential loads may be deferred");
            }

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Adjust caching behavior based on current memory pressure
        /// </summary>
        private void AdjustCachingBehavior(MemoryPressureMonitor.MemoryPressureLevel pressureLevel)
        {
            // Dynamically adjust cache limits based on memory pressure
            var originalMaxSize = _config.MaxCacheSize;
            var adjustedMaxSize = originalMaxSize;

            switch (pressureLevel)
            {
                case MemoryPressureMonitor.MemoryPressureLevel.None:
                case MemoryPressureMonitor.MemoryPressureLevel.Low:
                    adjustedMaxSize = originalMaxSize; // Full cache size
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.Medium:
                    adjustedMaxSize = (int)(originalMaxSize * 0.7f); // 70% of max size
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.High:
                    adjustedMaxSize = (int)(originalMaxSize * 0.5f); // 50% of max size
                    break;

                case MemoryPressureMonitor.MemoryPressureLevel.Critical:
                    adjustedMaxSize = (int)(originalMaxSize * 0.3f); // 30% of max size
                    break;
            }

            // Apply cache size adjustment if needed
            if (_scriptCache.Count > adjustedMaxSize)
            {
                var excessCount = _scriptCache.Count - adjustedMaxSize;
                var keysToRemove = _scriptCache.Keys.Take(excessCount).ToArray();

                foreach (var key in keysToRemove)
                {
                    _scriptCache.TryRemove(key, out _);
                }

                UnityEngine.Debug.Log($"Adjusted cache size for pressure level {pressureLevel}: removed {excessCount} scripts, new size: {_scriptCache.Count}/{adjustedMaxSize}");
            }
        }

        /// <summary>
        /// Calculate estimated memory usage of cached scripts
        /// </summary>
        private long CalculateEstimatedMemoryUsage()
        {
            try
            {
                // Rough estimation - in production this would be more accurate
                var estimatedBytesPerScript = 1024; // Assume ~1KB per cached script
                var cacheCount = _scriptCache.Count;
                var baseMemoryUsage = cacheCount * estimatedBytesPerScript;

                // Add overhead for data structures
                var overhead = (long)(baseMemoryUsage * 0.3f); // 30% overhead estimate

                return baseMemoryUsage + overhead;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Error calculating memory usage: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get current memory management statistics
        /// </summary>
        public MemoryManagementStats GetMemoryManagementStats()
        {
            lock (_memoryManagementLock)
            {
                return new MemoryManagementStats
                {
                    CurrentPressureLevel = _currentMemoryPressure,
                    LastCleanupTime = _lastMemoryCleanup,
                    EstimatedMemoryUsage = CalculateEstimatedMemoryUsage(),
                    CachedScriptCount = _scriptCache.Count,
                    MaxCacheSize = _config.MaxCacheSize,
                    CacheUsagePercentage = (float)_scriptCache.Count / _config.MaxCacheSize * 100f
                };
            }
        }
        #endregion

        #region Memory Management Supporting Types
        /// <summary>
        /// Statistics for memory management monitoring
        /// </summary>
        public class MemoryManagementStats
        {
            public MemoryPressureMonitor.MemoryPressureLevel CurrentPressureLevel { get; set; }
            public DateTime LastCleanupTime { get; set; }
            public long EstimatedMemoryUsage { get; set; }
            public int CachedScriptCount { get; set; }
            public int MaxCacheSize { get; set; }
            public float CacheUsagePercentage { get; set; }

            public string GetSummary()
            {
                return $"Memory: {EstimatedMemoryUsage / 1024:F1}KB, " +
                       $"Cache: {CachedScriptCount}/{MaxCacheSize} ({CacheUsagePercentage:F1}%), " +
                       $"Pressure: {CurrentPressureLevel}";
            }
        }
        #endregion
    }
}
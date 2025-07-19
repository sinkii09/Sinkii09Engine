using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Sinkii09.Engine.Common.Resources;
using Sinkii09.Engine.Services.Performance;
using System.Threading.Tasks;
using System.Linq;

namespace Sinkii09.Engine.Services
{

    /// <summary>
    /// Memory usage statistics for resource service
    /// </summary>
    public struct ResourceMemoryStats
    {
        public long TotalMemoryUsed;
        public int LoadedResourceCount;
        public int CachedResourceCount;
        public float MemoryPressureLevel;
        public Dictionary<ProviderType, long> MemoryUsageByProvider;
    }

    /// <summary>
    /// Comprehensive statistics for resource service performance
    /// </summary>
    public struct ResourceServiceStatistics
    {
        public long TotalResourcesLoaded;
        public long TotalLoadFailures;
        public long CacheHits;
        public long CacheMisses;
        public double AverageLoadTimeMs;
        public int ActiveProviders;
        public Dictionary<ProviderType, long> LoadCountByProvider;
        public Dictionary<ProviderType, double> AverageLoadTimeByProvider;
        public ResourceMemoryStats MemoryStats;
        public CircuitBreakerStatistics CircuitBreakerStats;
    }

    /// <summary>
    /// Circuit breaker statistics for error handling monitoring
    /// </summary>
    public struct CircuitBreakerStatistics
    {
        public Dictionary<ProviderType, int> FailureCountByProvider;
        public Dictionary<ProviderType, bool> CircuitStateByProvider;
        public Dictionary<ProviderType, DateTime> LastFailureTimeByProvider;
    }

    /// <summary>
    /// Resource cache statistics for monitoring
    /// </summary>
    public struct ResourceCacheStatistics
    {
        public int CachedResourceCount;
        public long TotalMemoryUsed;
        public double AverageResourceAge;
        public float CacheHitRatio;
        public int HighPriorityResources;
        public int MediumPriorityResources;
        public int LowPriorityResources;
    }

    /// <summary>
    /// Memory usage forecast for predictive management
    /// </summary>
    public struct MemoryForecast
    {
        public long CurrentMemoryUsage;
        public long CacheMemoryUsage;
        public long ProjectedMemoryUsage;
        public long ProjectedGrowth;
        public TimeSpan TimeToThreshold;
        public MemoryAction RecommendedAction;
    }

    /// <summary>
    /// Recommended memory management actions
    /// </summary>
    public enum MemoryAction
    {
        NoActionNeeded,
        PreventiveCleanup,
        ModerateCleanup,
        AggressiveCleanup,
        EmergencyCleanup
    }

    /// <summary>
    /// Enhanced ResourceService implementation with modern async patterns, dependency injection, and error handling
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.Critical, 
        Description = "Manages resource loading through multiple providers with enhanced error handling and memory management")]
    [ServiceConfiguration(typeof(ResourceServiceConfiguration))]
    public class ResourceService : IResourceService, IMemoryPressureResponder
    {
        #region Private Fields

        private readonly ResourceServiceConfiguration _config;
        private IServiceProvider _serviceProvider;
        private readonly Dictionary<ProviderType, IResourceProvider> _providers;
        private CircuitBreakerManager _circuitBreakerManager;
        private readonly List<Action<float>> _memoryPressureCallbacks;
        private readonly object _providersLock = new object();
        private readonly object _callbacksLock = new object();

        // Resource loading tracking
        private readonly Dictionary<string, ResourceLoadingOperation> _activeLoadOperations;
        private readonly SemaphoreSlim _loadingSemaphore;
        
        // Resource caching and optimization
        private readonly Dictionary<string, CachedResource> _resourceCache;
        private readonly Dictionary<string, float> _resourcePriorities;
        private readonly Queue<string> _cacheEvictionQueue;
        private readonly object _cacheLock = new object();

        // Statistics and monitoring
        private long _totalResourcesLoaded;
        private long _totalLoadFailures;
        private long _cacheHits;
        private long _cacheMisses;
        private readonly List<double> _loadTimes;
        private readonly Dictionary<ProviderType, List<double>> _loadTimesByProvider;
        private readonly Dictionary<ProviderType, long> _loadCountsByProvider;

        // Memory management
        private float _currentMemoryPressure;
        private DateTime _lastMemoryCleanup;
        private readonly Timer _unloadTimer;

        // Disposal tracking
        private bool _disposed;

        #endregion

        #region Events

        public event Action<string> ResourceLoadStarted;
        public event Action<string, TimeSpan> ResourceLoadCompleted;
        public event Action<string, Exception> ResourceLoadFailed;
        public event Action<float> MemoryPressureChanged;

        #endregion

        #region Constructor

        public ResourceService(ResourceServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serviceProvider = null; // Will be injected during initialization

            _providers = new Dictionary<ProviderType, IResourceProvider>();
            _circuitBreakerManager = new CircuitBreakerManager(_config);
            _memoryPressureCallbacks = new List<Action<float>>();
            _activeLoadOperations = new Dictionary<string, ResourceLoadingOperation>();
            _loadingSemaphore = new SemaphoreSlim(_config.MaxConcurrentLoads, _config.MaxConcurrentLoads);
            
            // Initialize caching and optimization systems
            _resourceCache = new Dictionary<string, CachedResource>();
            _resourcePriorities = new Dictionary<string, float>();
            _cacheEvictionQueue = new Queue<string>();

            // Initialize statistics tracking
            _loadTimes = new List<double>();
            _loadTimesByProvider = new Dictionary<ProviderType, List<double>>();
            _loadCountsByProvider = new Dictionary<ProviderType, long>();

            // Initialize memory management
            _currentMemoryPressure = 0f;
            _lastMemoryCleanup = DateTime.UtcNow;

            // Set up automatic unload timer
            var unloadInterval = TimeSpan.FromSeconds(_config.UnloadUnusedResourcesInterval);
            _unloadTimer = new Timer(async _ => await UnloadUnusedResourcesAsync(), null, unloadInterval, unloadInterval);

            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"ResourceService initialized with configuration: {_config.GetConfigurationSummary()}");
            }
        }

        #endregion

        #region Private Helper Classes

        /// <summary>
        /// Tracks a resource loading operation
        /// </summary>
        private class ResourceLoadingOperation
        {
            public string Path { get; set; }
            public Type ResourceType { get; set; }
            public DateTime StartTime { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public TaskCompletionSource<object> TaskCompletionSource { get; set; }
            public float Priority { get; set; } = 0.5f;
        }

        /// <summary>
        /// Cached resource with metadata
        /// </summary>
        private class CachedResource
        {
            public object Resource { get; set; }
            public Type ResourceType { get; set; }
            public DateTime CachedTime { get; set; }
            public DateTime LastAccessed { get; set; }
            public int AccessCount { get; set; }
            public float Priority { get; set; }
            public long MemorySize { get; set; }

            public bool IsValid => Resource != null && 
                                   DateTime.UtcNow - CachedTime < TimeSpan.FromHours(1); // 1 hour cache validity

            public void UpdateAccess()
            {
                LastAccessed = DateTime.UtcNow;
                AccessCount++;
            }
        }

        /// <summary>
        /// Circuit breaker for provider failure management
        /// </summary>
        private class CircuitBreaker
        {
            public ProviderType ProviderType { get; set; }
            public int FailureCount { get; set; }
            public DateTime LastFailureTime { get; set; }
            public bool IsOpen { get; set; }
            public DateTime OpenedTime { get; set; }

            public bool ShouldAttemptRequest(ResourceServiceConfiguration config)
            {
                if (!IsOpen) return true;

                var timeoutElapsed = DateTime.UtcNow - OpenedTime > TimeSpan.FromSeconds(config.CircuitBreakerTimeoutSeconds);
                if (timeoutElapsed)
                {
                    IsOpen = false;
                    FailureCount = 0;
                    return true;
                }

                return false;
            }

            public void RecordSuccess()
            {
                FailureCount = 0;
                IsOpen = false;
            }

            public void RecordFailure(ResourceServiceConfiguration config)
            {
                FailureCount++;
                LastFailureTime = DateTime.UtcNow;

                if (FailureCount >= config.CircuitBreakerFailureThreshold)
                {
                    IsOpen = true;
                    OpenedTime = DateTime.UtcNow;
                }
            }
        }

        #endregion
        
        #region IEngineService Implementation
        
        /// <summary>
        /// Initialize the ResourceService with provider setup and configuration validation
        /// </summary>
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("ResourceService: Starting initialization...");
                }
                
                // Store service provider reference for dependency injection
                _serviceProvider = provider;
                
                // Validate configuration
                if (!_config.Validate(out var configErrors))
                {
                    var errorMessage = $"ResourceService configuration validation failed: {string.Join(", ", configErrors)}";
                    Debug.LogError(errorMessage);
                    return ServiceInitializationResult.Failed(errorMessage);
                }
                
                // Initialize providers based on configuration
                await InitializeProvidersAsync(cancellationToken);
                
                // Set up circuit breakers for enabled providers
                InitializeCircuitBreakers();
                
                // Initialize memory pressure monitoring if enabled
                if (_config.EnableMemoryPressureResponse)
                {
                    InitializeMemoryPressureMonitoring();
                }
                
                // Preload resources if configured
                if (_config.EnableResourcePreloading && _config.PreloadResourcePaths.Length > 0)
                {
                    await PreloadResourcesAsync(cancellationToken);
                }
                
                // Set up automatic memory cleanup
                Application.lowMemory += OnLowMemory;
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService initialized successfully with {_providers.Count} providers");
                }
                
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex.Message, ex);
            }
        }
        
        /// <summary>
        /// Perform health check on all providers and service state
        /// </summary>
        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var healthIssues = new List<string>();
                var providerHealthResults = new Dictionary<ProviderType, bool>();
                
                // Check each provider's health
                foreach (var kvp in _providers)
                {
                    var providerType = kvp.Key;
                    var provider = kvp.Value;
                    
                    try
                    {
                        // Check if provider is responsive
                        var testPath = "health_check_test";
                        var exists = await provider.ResourceExistsAsync<UnityEngine.Object>(testPath);
                        providerHealthResults[providerType] = true;
                        
                        // Check circuit breaker state
                        var circuitBreakerState = _circuitBreakerManager?.GetState(providerType);
                        if (circuitBreakerState != null && circuitBreakerState.IsOpen)
                        {
                            healthIssues.Add($"Circuit breaker is open for provider {providerType}");
                        }
                    }
                    catch (Exception ex)
                    {
                        providerHealthResults[providerType] = false;
                        healthIssues.Add($"Provider {providerType} health check failed: {ex.Message}");
                    }
                }
                
                // Check memory pressure
                if (_currentMemoryPressure > _config.MemoryPressureThreshold)
                {
                    healthIssues.Add($"High memory pressure detected: {_currentMemoryPressure:P1}");
                }
                
                // Check active loading operations
                var activeOperations = _activeLoadOperations.Count;
                if (activeOperations > _config.MaxConcurrentLoads * 0.8f)
                {
                    healthIssues.Add($"High resource loading activity: {activeOperations}/{_config.MaxConcurrentLoads}");
                }
                
                // Determine overall health status
                if (healthIssues.Count == 0)
                {
                    return ServiceHealthStatus.Healthy("All systems operational");
                }
                else
                {
                    return ServiceHealthStatus.Unhealthy($"Service unhealthy: {string.Join("; ", healthIssues)}");
                }
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unknown($"Health check failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gracefully shutdown the ResourceService with cleanup
        /// </summary>
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("ResourceService: Starting graceful shutdown...");
                }
                
                // Cancel all active loading operations
                var activeTasks = new List<UniTask>();
                lock (_activeLoadOperations)
                {
                    foreach (var operation in _activeLoadOperations.Values)
                    {
                        operation.CancellationTokenSource?.Cancel();
                        if (operation.TaskCompletionSource?.Task != null)
                        {
                            activeTasks.Add(operation.TaskCompletionSource.Task.AsUniTask());
                        }
                    }
                }
                int timeoutMs = _config.GracefulShutdownTimeoutMs;
                try
                {
                    var timeoutToken = new CancellationTokenSource(timeoutMs);
                    var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);
                    await UniTask.WhenAll(activeTasks).SuppressCancellationThrow().AttachExternalCancellation(combinedToken.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning("ResourceService: Some loading operations did not complete within timeout during shutdown");
                }
                
                // Unload all resources
                await UnloadUnusedResourcesAsync();
                
                // Dispose providers with proper cooldown
                foreach (var kvp in _providers)
                {
                    var providerType = kvp.Key;
                    var provider = kvp.Value;
                    
                    try
                    {
                        // Perform provider cooldown before disposal
                        await CooldownProviderAsync(provider, providerType);
                        
                        provider?.UnloadResources();
                        if (provider is IDisposable disposableProvider)
                        {
                            disposableProvider.Dispose();
                        }
                        
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log($"ResourceService: Provider {providerType} disposed successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error disposing provider {providerType}: {ex.Message}");
                    }
                }
                
                // Clean up resources
                _providers.Clear();
                _circuitBreakerManager = null;
                _activeLoadOperations.Clear();
                
                // Dispose timer
                _unloadTimer?.Dispose();
                
                // Dispose semaphore
                _loadingSemaphore?.Dispose();
                
                // Unregister memory event
                Application.lowMemory -= OnLowMemory;
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("ResourceService shutdown completed successfully");
                }
                
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex.Message, ex);
            }
        }
        
        #endregion
        
        #region Private Initialization Methods
        
        /// <summary>
        /// Initialize resource providers based on configuration
        /// </summary>
        private async UniTask InitializeProvidersAsync(CancellationToken cancellationToken)
        {
            var enabledProviders = _config.GetEnabledProviderTypes();
            
            foreach (var providerType in enabledProviders)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                try
                {
                    var provider = await CreateProviderAsync(providerType, cancellationToken);
                    if (provider != null)
                    {
                        lock (_providersLock)
                        {
                            _providers[providerType] = provider;
                        }
                        
                        // Initialize statistics tracking for this provider
                        _loadTimesByProvider[providerType] = new List<double>();
                        _loadCountsByProvider[providerType] = 0;
                        
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log($"ResourceService: Initialized provider {providerType}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize provider {providerType}: {ex.Message}");
                    // Continue with other providers
                }
            }
        }
        
        /// <summary>
        /// Create a provider instance for the specified type with dependency injection
        /// </summary>
        private async UniTask<IResourceProvider> CreateProviderAsync(ProviderType providerType, CancellationToken cancellationToken)
        {
            try
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Creating provider {providerType} with DI integration");
                }

                IResourceProvider provider = null;

                switch (providerType)
                {
                    case ProviderType.Resources:
                        // Create ProjectResourceProvider with dependency injection
                        provider = CreateProjectResourceProvider();
                        break;
                        
                    case ProviderType.AssetBundle:
                        // Create AssetBundle provider with DI (placeholder for future implementation)
                        provider = CreateAssetBundleProvider();
                        break;
                        
                    case ProviderType.Local:
                        // Create Local provider with DI (placeholder for future implementation)
                        provider = CreateLocalProvider();
                        break;
                        
                    default:
                        Debug.LogError($"ResourceService: Unknown provider type: {providerType}");
                        return null;
                }

                if (provider != null)
                {
                    // Initialize provider with warmup
                    await WarmupProviderAsync(provider, providerType, cancellationToken);
                    
                    // Validate provider health
                    if (await ValidateProviderHealthAsync(provider, providerType))
                    {
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log($"ResourceService: Provider {providerType} created and validated successfully");
                        }
                        return provider;
                    }
                    else
                    {
                        Debug.LogError($"ResourceService: Provider {providerType} failed health validation");
                        return null;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService: Failed to create provider {providerType}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create ProjectResourceProvider with dependency injection
        /// </summary>
        private IResourceProvider CreateProjectResourceProvider()
        {
            // For now, create directly - in the future, this could use ServiceContainer
            // to resolve provider dependencies if providers become more complex
            var provider = new ProjectResourceProvider();
            
            // Apply provider-specific configuration if available
            ConfigureProvider(provider, ProviderType.Resources);
            
            return provider;
        }

        /// <summary>
        /// Create AssetBundle provider (placeholder for future implementation)
        /// </summary>
        private IResourceProvider CreateAssetBundleProvider()
        {
            // Future implementation: AssetBundle provider with DI
            Debug.LogWarning("ResourceService: AssetBundle provider not yet implemented");
            return null;
        }

        /// <summary>
        /// Create Local provider (placeholder for future implementation)
        /// </summary>
        private IResourceProvider CreateLocalProvider()
        {
            // Future implementation: Local file system provider with DI
            Debug.LogWarning("ResourceService: Local provider not yet implemented");
            return null;
        }

        /// <summary>
        /// Apply provider-specific configuration
        /// </summary>
        private void ConfigureProvider(IResourceProvider provider, ProviderType providerType)
        {
            try
            {
                // Apply general configuration settings to provider
                // This is where provider-specific configuration would be injected
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Configured provider {providerType} with service settings");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ResourceService: Failed to configure provider {providerType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Warmup provider with initial operations
        /// </summary>
        private async UniTask WarmupProviderAsync(IResourceProvider provider, ProviderType providerType, CancellationToken cancellationToken)
        {
            try
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Warming up provider {providerType}");
                }

                // Perform provider warmup operations
                // For example, test a simple resource existence check
                var warmupStartTime = DateTime.UtcNow;
                
                // Test provider responsiveness
                await provider.ResourceExistsAsync<UnityEngine.Object>("warmup_test");
                
                var warmupTime = (DateTime.UtcNow - warmupStartTime).TotalMilliseconds;
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Provider {providerType} warmup completed in {warmupTime:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ResourceService: Provider {providerType} warmup failed: {ex.Message}");
                // Don't throw - warmup failure shouldn't prevent provider creation
            }
        }

        /// <summary>
        /// Validate provider health after creation
        /// </summary>
        private async UniTask<bool> ValidateProviderHealthAsync(IResourceProvider provider, ProviderType providerType)
        {
            try
            {
                // Basic health validation
                if (provider == null)
                    return false;

                // Test provider basic functionality
                var healthCheckStartTime = DateTime.UtcNow;
                
                // Perform a lightweight operation to validate provider
                var isHealthy = await provider.ResourceExistsAsync<UnityEngine.Object>("health_check");
                
                var healthCheckTime = (DateTime.UtcNow - healthCheckStartTime).TotalMilliseconds;
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Provider {providerType} health check completed in {healthCheckTime:F2}ms, healthy: {isHealthy}");
                }

                return true; // Return true regardless of resource existence - provider is responding
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService: Provider {providerType} health validation failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Initialize circuit breakers for all providers
        /// </summary>
        private void InitializeCircuitBreakers()
        {
            if (!_config.EnableCircuitBreaker)
                return;
            
            foreach (var kvp in _providers)
            {
                _circuitBreakerManager.RegisterProvider(kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// Initialize memory pressure monitoring
        /// </summary>
        private void InitializeMemoryPressureMonitoring()
        {
            // Register for memory pressure callbacks if available from ServiceContainer
            // This will be enhanced when integrated with ServiceContainer's memory management
            _currentMemoryPressure = 0f;
        }
        
        /// <summary>
        /// Handle low memory events from Unity
        /// </summary>
        private async void OnLowMemory()
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("ResourceService: Low memory event received, triggering cleanup");
            }
            
            await ForceMemoryCleanupAsync();
        }
        
        #endregion
        
        #region Placeholder Method Implementations
        
        // These will be implemented in the next tasks - adding minimal implementations to avoid compilation errors
        
        public async UniTask<Resource<T>> LoadResourceAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            return await LoadResourceWithPriorityAsync<T>(path, 0.5f, cancellationToken);
        }

        /// <summary>
        /// Load resource with priority support
        /// </summary>
        public async UniTask<Resource<T>> LoadResourceWithPriorityAsync<T>(string path, float priority, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var startTime = DateTime.UtcNow;

            // Check cache first
            if (_config.EnableResourceCaching)
            {
                var cachedResource = GetCachedResource<T>(path);
                if (cachedResource != null)
                {
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"ResourceService: Cache hit for resource {path}");
                    }
                    Interlocked.Increment(ref _cacheHits);
                    return cachedResource;
                }
                Interlocked.Increment(ref _cacheMisses);
            }
            
            try
            {
                // Check if already loading
                Task<object> existingTask = null;
                lock (_activeLoadOperations)
                {
                    if (_activeLoadOperations.ContainsKey(path))
                    {
                        // Resource is already being loaded, get the task to wait for
                        var existingOp = _activeLoadOperations[path];
                        if (existingOp.ResourceType == typeof(T))
                        {
                            existingTask = existingOp.TaskCompletionSource.Task;
                        }
                    }
                }
                
                // If we found an existing task, wait for it outside the lock
                if (existingTask != null)
                {
                    var result = await existingTask;
                    return result as Resource<T>;
                }
                
                // Acquire semaphore to limit concurrent loads
                await _loadingSemaphore.WaitAsync(cancellationToken);
                
                try
                {
                    // Fire loading started event
                    ResourceLoadStarted?.Invoke(path);
                    
                    // Create loading operation with priority
                    var operation = new ResourceLoadingOperation
                    {
                        Path = path,
                        ResourceType = typeof(T),
                        StartTime = startTime,
                        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
                        TaskCompletionSource = new TaskCompletionSource<object>(),
                        Priority = priority
                    };

                    // Store priority for future reference
                    _resourcePriorities[path] = priority;
                    
                    // Register operation
                    lock (_activeLoadOperations)
                    {
                        _activeLoadOperations[path] = operation;
                    }
                    
                    // Try to find a provider that can load this resource
                    Resource<T> resource = null;
                    Exception lastException = null;
                    
                    foreach (var kvp in _providers)
                    {
                        var providerType = kvp.Key;
                        var provider = kvp.Value;
                        
                        // Check circuit breaker
                        if (!_circuitBreakerManager.ShouldAllowRequest(providerType))
                        {
                            if (_config.EnableDetailedLogging)
                            {
                                Debug.LogWarning($"Circuit breaker is open for provider {providerType}, skipping");
                            }
                            continue;
                        }
                        
                        try
                        {
                            // Check if provider supports this resource type
                            if (!provider.SupportsType<T>())
                                continue;
                            
                            // Attempt to load with retry logic
                            resource = await LoadWithRetryAsync<T>(provider, path, providerType, operation.CancellationTokenSource.Token);
                            
                            if (resource != null && resource.IsValid)
                            {
                                var operationTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                                _circuitBreakerManager.RecordSuccess(providerType, operationTime);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            var operationTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                            _circuitBreakerManager.RecordFailure(providerType, ex, operationTime);
                            
                            if (_config.EnableDetailedLogging)
                            {
                                Debug.LogError($"Provider {providerType} failed to load {path}: {ex.Message}");
                            }
                        }
                    }
                    
                    // Complete the operation
                    lock (_activeLoadOperations)
                    {
                        _activeLoadOperations.Remove(path);
                    }
                    
                    if (resource != null && resource.IsValid)
                    {
                        // Cache the loaded resource
                        if (_config.EnableResourceCaching)
                        {
                            CacheResource(path, resource, priority);
                        }

                        // Record success
                        var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        RecordLoadSuccess(loadTime);
                        
                        operation.TaskCompletionSource.SetResult(resource);
                        ResourceLoadCompleted?.Invoke(path, TimeSpan.FromMilliseconds(loadTime));
                        
                        return resource;
                    }
                    else
                    {
                        // Record failure
                        RecordLoadFailure();
                        
                        var exception = lastException ?? new InvalidOperationException($"Failed to load resource: {path}");
                        operation.TaskCompletionSource.SetException(exception);
                        ResourceLoadFailed?.Invoke(path, exception);
                        
                        throw exception;
                    }
                }
                finally
                {
                    _loadingSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"Resource load cancelled: {path}");
                }
                throw;
            }
        }
        
        public async UniTask<IEnumerable<Resource<T>>> LoadResourcesAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            
            var resourcePaths = await LocateResourcesAsync<T>(path, cancellationToken);
            var loadTasks = new List<UniTask<Resource<T>>>();
            
            foreach (var resourcePath in resourcePaths)
            {
                loadTasks.Add(LoadResourceAsync<T>(resourcePath, cancellationToken));
            }
            
            return await UniTask.WhenAll(loadTasks);
        }
        
        public async UniTask<bool> ResourceExistsAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
                return false;
            
            foreach (var kvp in _providers)
            {
                var providerType = kvp.Key;
                var provider = kvp.Value;
                
                // Check circuit breaker
                if (!_circuitBreakerManager.ShouldAllowRequest(providerType))
                    continue;
                
                try
                {
                    if (!provider.SupportsType<T>())
                        continue;
                    
                    var operationStart = DateTime.UtcNow;
                    var exists = await provider.ResourceExistsAsync<T>(path);
                    var operationTime = (DateTime.UtcNow - operationStart).TotalMilliseconds;
                    
                    if (exists)
                    {
                        _circuitBreakerManager.RecordSuccess(providerType, operationTime);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    var operationTime = (DateTime.UtcNow - DateTime.UtcNow).TotalMilliseconds; // Minimal time for failures
                    _circuitBreakerManager.RecordFailure(providerType, ex, operationTime);
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.LogWarning($"Provider {providerType} failed to check resource existence: {ex.Message}");
                    }
                }
            }
            
            return false;
        }
        
        public async UniTask<IEnumerable<string>> LocateResourcesAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var allPaths = new HashSet<string>();
            
            foreach (var kvp in _providers)
            {
                var providerType = kvp.Key;
                var provider = kvp.Value;
                
                // Check circuit breaker
                if (!_circuitBreakerManager.ShouldAllowRequest(providerType))
                    continue;
                
                try
                {
                    if (!provider.SupportsType<T>())
                        continue;
                    
                    var operationStart = DateTime.UtcNow;
                    var paths = await provider.LocateResourcesAsync<T>(path);
                    var operationTime = (DateTime.UtcNow - operationStart).TotalMilliseconds;
                    
                    if (paths != null)
                    {
                        foreach (var p in paths)
                        {
                            allPaths.Add(p);
                        }
                        _circuitBreakerManager.RecordSuccess(providerType, operationTime);
                    }
                }
                catch (Exception ex)
                {
                    var operationTime = (DateTime.UtcNow - DateTime.UtcNow).TotalMilliseconds; // Minimal time for failures
                    _circuitBreakerManager.RecordFailure(providerType, ex, operationTime);
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.LogWarning($"Provider {providerType} failed to locate resources: {ex.Message}");
                    }
                }
            }
            
            return allPaths;
        }
        
        public async UniTask PreloadResourcesAsync(CancellationToken cancellationToken = default)
        {
            if (!_config.EnableResourcePreloading || _config.PreloadResourcePaths.Length == 0)
                return;
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"Preloading {_config.PreloadResourcePaths.Length} resources...");
            }
            
            var preloadTasks = new List<UniTask>();
            
            foreach (var path in _config.PreloadResourcePaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                // Create a task for each preload
                var task = UniTask.Create(async () =>
                {
                    try
                    {
                        // Try to determine the resource type from the path extension
                        // For now, assume UnityEngine.Object as base type
                        await LoadResourceAsync<UnityEngine.Object>(path, cancellationToken);
                        
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log($"Preloaded resource: {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to preload resource {path}: {ex.Message}");
                    }
                });
                
                preloadTasks.Add(task);
            }
            
            // Wait for all preloads to complete
            await UniTask.WhenAll(preloadTasks);
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("Resource preloading completed");
            }
        }
        
        public bool IsProviderInitialized(ProviderType providerType)
        {
            lock (_providersLock)
            {
                return _providers.ContainsKey(providerType);
            }
        }
        
        public IResourceProvider GetProvider(ProviderType providerType)
        {
            lock (_providersLock)
            {
                return _providers.TryGetValue(providerType, out var provider) ? provider : null;
            }
        }
        
        public List<IResourceProvider> GetProviders(ProviderType providerTypes)
        {
            var providers = new List<IResourceProvider>();
            lock (_providersLock)
            {
                foreach (ProviderType type in Enum.GetValues(typeof(ProviderType)))
                {
                    if (type != ProviderType.None && (providerTypes & type) != 0)
                    {
                        if (_providers.TryGetValue(type, out var provider))
                        {
                            providers.Add(provider);
                        }
                    }
                }
            }
            return providers;
        }
        
        public async UniTask<Dictionary<ProviderType, bool>> GetProviderHealthStatusAsync()
        {
            var healthStatus = new Dictionary<ProviderType, bool>();
            
            foreach (var kvp in _providers)
            {
                var providerType = kvp.Key;
                var provider = kvp.Value;
                
                try
                {
                    if (provider == null)
                    {
                        healthStatus[providerType] = false;
                        continue;
                    }

                    // Enhanced health check with multiple criteria
                    var isHealthy = await PerformDetailedHealthCheckAsync(provider, providerType);
                    healthStatus[providerType] = isHealthy;
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"ResourceService: Provider {providerType} health status: {(isHealthy ? "Healthy" : "Unhealthy")}");
                    }
                }
                catch (Exception ex)
                {
                    healthStatus[providerType] = false;
                    Debug.LogWarning($"ResourceService: Health check failed for provider {providerType}: {ex.Message}");
                }
            }
            
            return healthStatus;
        }

        /// <summary>
        /// Perform detailed health check on a provider
        /// </summary>
        private async UniTask<bool> PerformDetailedHealthCheckAsync(IResourceProvider provider, ProviderType providerType)
        {
            try
            {
                var healthMetrics = new ProviderHealthMetrics();
                var startTime = DateTime.UtcNow;

                // 1. Check provider responsiveness
                var responsivenessTask = UniTask.Create(async () =>
                {
                    try
                    {
                        await provider.ResourceExistsAsync<UnityEngine.Object>("health_test");
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                var timeoutTask = UniTask.Delay(5000); // 5 second timeout
                var completedTask = await UniTask.WhenAny(responsivenessTask, timeoutTask);
                
                if (completedTask.result) // Timeout occurred
                {
                    healthMetrics.ResponsivenessScore = 0f;
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.LogWarning($"ResourceService: Provider {providerType} responsiveness test timed out");
                    }
                    return false;
                }

                var isResponsive = await responsivenessTask;
                healthMetrics.ResponsivenessScore = isResponsive ? 1f : 0f;
                healthMetrics.ResponseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // 2. Check circuit breaker state
                var circuitBreakerState = _circuitBreakerManager?.GetState(providerType);
                healthMetrics.CircuitBreakerOpen = circuitBreakerState?.IsOpen ?? false;

                // 3. Calculate overall health score
                var overallScore = CalculateProviderHealthScore(healthMetrics);
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Provider {providerType} health score: {overallScore:F2}, " +
                             $"response time: {healthMetrics.ResponseTimeMs:F2}ms, " +
                             $"circuit breaker: {(healthMetrics.CircuitBreakerOpen ? "Open" : "Closed")}");
                }

                return overallScore >= 0.5f; // Consider healthy if score >= 50%
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService: Detailed health check failed for provider {providerType}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculate provider health score based on metrics
        /// </summary>
        private float CalculateProviderHealthScore(ProviderHealthMetrics metrics)
        {
            var score = 0f;
            var totalWeight = 0f;

            // Responsiveness weight: 60%
            score += metrics.ResponsivenessScore * 0.6f;
            totalWeight += 0.6f;

            // Response time weight: 20% (faster is better)
            var responseTimeScore = Math.Max(0f, 1f - (float)(metrics.ResponseTimeMs / 1000.0)); // 1 second baseline
            score += responseTimeScore * 0.2f;
            totalWeight += 0.2f;

            // Circuit breaker state weight: 20%
            var circuitBreakerScore = metrics.CircuitBreakerOpen ? 0f : 1f;
            score += circuitBreakerScore * 0.2f;
            totalWeight += 0.2f;

            return totalWeight > 0 ? score / totalWeight : 0f;
        }

        /// <summary>
        /// Health metrics for a provider
        /// </summary>
        private struct ProviderHealthMetrics
        {
            public float ResponsivenessScore;
            public double ResponseTimeMs;
            public bool CircuitBreakerOpen;
        }

        /// <summary>
        /// Perform provider cooldown operations before disposal
        /// </summary>
        private async UniTask CooldownProviderAsync(IResourceProvider provider, ProviderType providerType)
        {
            try
            {
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Cooling down provider {providerType}");
                }

                var cooldownStartTime = DateTime.UtcNow;

                // Allow pending operations to complete (with timeout)
                var timeout = TimeSpan.FromSeconds(5); // 5 second cooldown timeout
                var endTime = DateTime.UtcNow.Add(timeout);

                while (DateTime.UtcNow < endTime)
                {
                    // Check if provider has any pending operations
                    // This is a simplified check - in practice, providers would need
                    // to implement a method to report their operation status
                    
                    await UniTask.Delay(100); // Small delay to allow operations to complete
                    
                    // Break early if provider seems ready for shutdown
                    // (This would be enhanced based on actual provider implementation)
                    break;
                }

                var cooldownTime = (DateTime.UtcNow - cooldownStartTime).TotalMilliseconds;
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Provider {providerType} cooldown completed in {cooldownTime:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ResourceService: Provider {providerType} cooldown failed: {ex.Message}");
                // Don't throw - cooldown failure shouldn't prevent disposal
            }
        }
        
        public async UniTask ForceMemoryCleanupAsync()
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("ResourceService: Performing forced memory cleanup");
            }
            
            // Clear low-priority cached resources first
            if (_config.EnableResourceCaching)
            {
                ClearCacheByPriority(0.3f); // Clear resources with priority <= 30%
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Cache cleanup completed, {_resourceCache.Count} resources remaining");
                }
            }
            
            // Unload unused resources from all providers
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider?.UnloadResources();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during provider cleanup: {ex.Message}");
                }
            }
            
            // Force Unity to unload unused assets
            await UnityEngine.Resources.UnloadUnusedAssets();
            
            // Update memory cleanup timestamp
            _lastMemoryCleanup = DateTime.UtcNow;
        }

        /// <summary>
        /// Get progress information for active resource loading operations
        /// </summary>
        public Dictionary<string, float> GetLoadingProgress()
        {
            var progress = new Dictionary<string, float>();
            
            lock (_activeLoadOperations)
            {
                foreach (var kvp in _activeLoadOperations)
                {
                    var operation = kvp.Value;
                    var elapsed = (DateTime.UtcNow - operation.StartTime).TotalSeconds;
                    
                    // Estimate progress based on elapsed time (simple heuristic)
                    var estimatedProgress = Math.Min(0.9f, (float)(elapsed / 10.0)); // Assume max 10 seconds
                    progress[kvp.Key] = estimatedProgress;
                }
            }
            
            return progress;
        }

        /// <summary>
        /// Get detailed statistics about the resource cache
        /// </summary>
        public ResourceCacheStatistics GetCacheStatistics()
        {
            lock (_cacheLock)
            {
                var totalMemory = _resourceCache.Values.Sum(r => r.MemorySize);
                var averageAge = _resourceCache.Values.Any() 
                    ? _resourceCache.Values.Average(r => (DateTime.UtcNow - r.CachedTime).TotalMinutes)
                    : 0;

                return new ResourceCacheStatistics
                {
                    CachedResourceCount = _resourceCache.Count,
                    TotalMemoryUsed = totalMemory,
                    AverageResourceAge = averageAge,
                    CacheHitRatio = _cacheHits + _cacheMisses > 0 ? (float)_cacheHits / (_cacheHits + _cacheMisses) : 0f,
                    HighPriorityResources = _resourceCache.Values.Count(r => r.Priority > 0.7f),
                    MediumPriorityResources = _resourceCache.Values.Count(r => r.Priority >= 0.3f && r.Priority <= 0.7f),
                    LowPriorityResources = _resourceCache.Values.Count(r => r.Priority < 0.3f)
                };
            }
        }
        
        public void RegisterMemoryPressureCallback(Action<float> callback)
        {
            if (callback == null) return;
            
            lock (_callbacksLock)
            {
                _memoryPressureCallbacks.Add(callback);
            }
        }
        
        public void UnregisterMemoryPressureCallback(Action<float> callback)
        {
            if (callback == null) return;
            
            lock (_callbacksLock)
            {
                _memoryPressureCallbacks.Remove(callback);
            }
        }
        
        public ResourceMemoryStats GetMemoryStatistics()
        {
            var loadedCount = 0;
            var memoryByProvider = new Dictionary<ProviderType, long>();
            var totalCacheMemory = 0L;
            var cachedResourceCount = 0;
            
            // Get cache memory usage
            if (_config.EnableResourceCaching)
            {
                lock (_cacheLock)
                {
                    totalCacheMemory = _resourceCache.Values.Sum(r => r.MemorySize);
                    cachedResourceCount = _resourceCache.Count;
                }
            }
            
            // Count loaded resources from all providers
            foreach (var kvp in _providers)
            {
                try
                {
                    if (_loadCountsByProvider.TryGetValue(kvp.Key, out var count))
                    {
                        loadedCount += (int)count;
                    }
                    
                    // Estimate memory usage per provider
                    var providerMemory = EstimateProviderMemoryUsage(kvp.Key);
                    memoryByProvider[kvp.Key] = providerMemory;
                }
                catch
                {
                    // Ignore errors in statistics collection
                    memoryByProvider[kvp.Key] = 0;
                }
            }
            
            return new ResourceMemoryStats
            {
                TotalMemoryUsed = GC.GetTotalMemory(false) + totalCacheMemory,
                LoadedResourceCount = loadedCount,
                CachedResourceCount = cachedResourceCount,
                MemoryPressureLevel = _currentMemoryPressure,
                MemoryUsageByProvider = memoryByProvider
            };
        }

        /// <summary>
        /// Estimate memory usage for a specific provider
        /// </summary>
        private long EstimateProviderMemoryUsage(ProviderType providerType)
        {
            if (!_config.EnableResourceCaching)
                return 0;

            lock (_cacheLock)
            {
                return _resourceCache.Values
                    .Where(r => r.ResourceType != null)
                    .Sum(r => r.MemorySize);
            }
        }

        /// <summary>
        /// Forecast memory usage based on current trends
        /// </summary>
        public MemoryForecast GetMemoryForecast()
        {
            var currentMemory = GC.GetTotalMemory(false);
            var cacheMemory = 0L;
            var projectedGrowth = 0L;
            
            if (_config.EnableResourceCaching)
            {
                lock (_cacheLock)
                {
                    cacheMemory = _resourceCache.Values.Sum(r => r.MemorySize);
                    
                    // Simple forecasting based on cache growth rate
                    var recentCacheEntries = _resourceCache.Values
                        .Where(r => r.CachedTime > DateTime.UtcNow.AddMinutes(-5))
                        .Count();
                    
                    if (recentCacheEntries > 0)
                    {
                        var averageResourceSize = cacheMemory / Math.Max(1, _resourceCache.Count);
                        var growthRate = recentCacheEntries / 5.0; // resources per minute
                        
                        // Project memory usage 10 minutes into the future
                        projectedGrowth = (long)(growthRate * 10 * averageResourceSize);
                    }
                }
            }

            var projectedMemory = currentMemory + projectedGrowth;
            var memoryThreshold = (long)(_config.MemoryPressureThreshold * 1024 * 1024 * 1024); // Convert to bytes
            
            return new MemoryForecast
            {
                CurrentMemoryUsage = currentMemory,
                CacheMemoryUsage = cacheMemory,
                ProjectedMemoryUsage = projectedMemory,
                ProjectedGrowth = projectedGrowth,
                TimeToThreshold = projectedGrowth > 0 
                    ? TimeSpan.FromMinutes(Math.Max(0, (memoryThreshold - currentMemory) / (projectedGrowth / 10.0)))
                    : TimeSpan.MaxValue,
                RecommendedAction = GetRecommendedMemoryAction(currentMemory, projectedMemory, memoryThreshold)
            };
        }

        /// <summary>
        /// Get recommended memory management action
        /// </summary>
        private MemoryAction GetRecommendedMemoryAction(long currentMemory, long projectedMemory, long threshold)
        {
            var currentPressure = (float)currentMemory / threshold;
            var projectedPressure = (float)projectedMemory / threshold;
            
            if (projectedPressure > 0.9f)
            {
                return MemoryAction.EmergencyCleanup;
            }
            else if (projectedPressure > 0.7f)
            {
                return MemoryAction.AggressiveCleanup;
            }
            else if (projectedPressure > 0.5f)
            {
                return MemoryAction.ModerateCleanup;
            }
            else if (currentPressure > 0.3f)
            {
                return MemoryAction.PreventiveCleanup;
            }
            else
            {
                return MemoryAction.NoActionNeeded;
            }
        }

        /// <summary>
        /// Optimize memory allocation patterns
        /// </summary>
        public async UniTask OptimizeMemoryAllocationAsync()
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("ResourceService: Optimizing memory allocation patterns");
            }

            var optimizationTasks = new List<UniTask>();

            // 1. Optimize cache layout for better memory locality
            optimizationTasks.Add(OptimizeCacheLayoutAsync());

            // 2. Preemptive cleanup of unused resources
            optimizationTasks.Add(PreemptiveResourceCleanupAsync());

            // 3. Defragment resource cache
            optimizationTasks.Add(DefragmentResourceCacheAsync());

            await UniTask.WhenAll(optimizationTasks);

            if (_config.EnableDetailedLogging)
            {
                Debug.Log("ResourceService: Memory allocation optimization completed");
            }
        }

        /// <summary>
        /// Optimize cache layout for better memory locality
        /// </summary>
        private async UniTask OptimizeCacheLayoutAsync()
        {
            if (!_config.EnableResourceCaching)
                return;

            lock (_cacheLock)
            {
                // Rebuild cache with frequently accessed items first
                var sortedCache = _resourceCache
                    .OrderByDescending(kvp => kvp.Value.AccessCount)
                    .ThenByDescending(kvp => kvp.Value.Priority)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                _resourceCache.Clear();
                foreach (var kvp in sortedCache)
                {
                    _resourceCache[kvp.Key] = kvp.Value;
                }
                
                // Rebuild eviction queue in access order
                _cacheEvictionQueue.Clear();
                foreach (var key in sortedCache.Keys)
                {
                    _cacheEvictionQueue.Enqueue(key);
                }
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// Perform preemptive cleanup of unused resources
        /// </summary>
        private async UniTask PreemptiveResourceCleanupAsync()
        {
            if (!_config.EnableResourceCaching)
                return;

            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // 30 minutes unused
            var toRemove = new List<string>();

            lock (_cacheLock)
            {
                toRemove = _resourceCache
                    .Where(kvp => kvp.Value.LastAccessed < cutoffTime && kvp.Value.Priority < 0.5f)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _resourceCache.Remove(key);
                    _resourcePriorities.Remove(key);
                }
            }

            if (_config.EnableDetailedLogging && toRemove.Count > 0)
            {
                Debug.Log($"ResourceService: Preemptively cleaned up {toRemove.Count} unused resources");
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// Defragment resource cache to reduce memory fragmentation
        /// </summary>
        private async UniTask DefragmentResourceCacheAsync()
        {
            // Trigger garbage collection to reduce fragmentation
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await UniTask.Yield();
        }
        
        public void UnloadResource(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            
            // Check if resource is currently loading
            lock (_activeLoadOperations)
            {
                if (_activeLoadOperations.TryGetValue(path, out var operation))
                {
                    // Cancel the loading operation
                    operation.CancellationTokenSource?.Cancel();
                    _activeLoadOperations.Remove(path);
                }
            }
            
            // Unload from all providers
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider.UnloadResource(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error unloading resource {path} from provider: {ex.Message}");
                }
            }
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"Unloaded resource: {path}");
            }
        }
        
        public async UniTask UnloadUnusedResourcesAsync()
        {
            await ForceMemoryCleanupAsync();
        }
        
        public bool IsResourceLoaded(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            
            foreach (var provider in _providers.Values)
            {
                try
                {
                    if (provider.ResourceLoaded(path))
                        return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error checking resource loaded state: {ex.Message}");
                }
            }
            
            return false;
        }
        
        public bool IsResourceLoading(string path)
        {
            lock (_activeLoadOperations)
            {
                return _activeLoadOperations.ContainsKey(path);
            }
        }
        
        public Resource<T> GetLoadedResourceOrNull<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
                return null;
            
            foreach (var provider in _providers.Values)
            {
                try
                {
                    var resource = provider.GetLoadedResourceOrNull<T>(path);
                    if (resource != null && resource.IsValid)
                        return resource;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error getting loaded resource: {ex.Message}");
                }
            }
            
            return null;
        }
        
        public ResourceServiceStatistics GetStatistics()
        {
            return new ResourceServiceStatistics
            {
                TotalResourcesLoaded = _totalResourcesLoaded,
                TotalLoadFailures = _totalLoadFailures,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                AverageLoadTimeMs = _loadTimes.Count > 0 ? _loadTimes.Average() : 0,
                ActiveProviders = _providers.Count,
                LoadCountByProvider = new Dictionary<ProviderType, long>(_loadCountsByProvider),
                AverageLoadTimeByProvider = new Dictionary<ProviderType, double>(),
                MemoryStats = GetMemoryStatistics(),
                CircuitBreakerStats = GetCircuitBreakerStatistics()
            };
        }
        
        private CircuitBreakerStatistics GetCircuitBreakerStatistics()
        {
            return _circuitBreakerManager?.GetStatistics() ?? new CircuitBreakerStatistics
            {
                FailureCountByProvider = new Dictionary<ProviderType, int>(),
                CircuitStateByProvider = new Dictionary<ProviderType, bool>(),
                LastFailureTimeByProvider = new Dictionary<ProviderType, DateTime>()
            };
        }
        
        // IMemoryPressureResponder implementation with enhanced tiered cleanup
        public async UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            _currentMemoryPressure = (float)pressureLevel / 3f; // Convert enum to float
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"ResourceService: Memory pressure level changed to {pressureLevel}, strategy: {strategy}");
            }

            // Perform tiered cleanup based on pressure level
            await PerformTieredMemoryCleanupAsync(pressureLevel, strategy);
            
            // Notify callbacks
            lock (_callbacksLock)
            {
                foreach (var callback in _memoryPressureCallbacks)
                {
                    try
                    {
                        callback(_currentMemoryPressure);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error in memory pressure callback: {ex.Message}");
                    }
                }
            }
            
            // Fire memory pressure changed event
            MemoryPressureChanged?.Invoke(_currentMemoryPressure);
        }

        /// <summary>
        /// Perform tiered memory cleanup based on pressure level and strategy
        /// </summary>
        private async UniTask PerformTieredMemoryCleanupAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            var cleanupStartTime = DateTime.UtcNow;
            var initialMemory = GC.GetTotalMemory(false);
            var resourcesCleared = 0;

            try
            {
                switch (pressureLevel)
                {
                    case MemoryPressureMonitor.MemoryPressureLevel.Low:
                        // Minimal cleanup - only expired cache entries
                        resourcesCleared = await CleanupExpiredCacheEntriesAsync();
                        break;

                    case MemoryPressureMonitor.MemoryPressureLevel.Medium:
                        // Moderate cleanup - clear low priority resources
                        resourcesCleared = await PerformModerateCleanupAsync();
                        break;

                    case MemoryPressureMonitor.MemoryPressureLevel.High:
                        // Aggressive cleanup - clear most cached resources
                        resourcesCleared = await PerformAggressiveCleanupAsync();
                        break;

                    case MemoryPressureMonitor.MemoryPressureLevel.Critical:
                        // Emergency cleanup - clear almost everything
                        resourcesCleared = await PerformEmergencyCleanupAsync();
                        break;
                }

                var cleanupTime = (DateTime.UtcNow - cleanupStartTime).TotalMilliseconds;
                var finalMemory = GC.GetTotalMemory(true); // Force GC
                var memoryFreed = initialMemory - finalMemory;

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Memory cleanup completed in {cleanupTime:F2}ms, " +
                             $"freed {memoryFreed / 1024 / 1024:F2}MB, cleared {resourcesCleared} resources");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResourceService: Memory cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up only expired cache entries
        /// </summary>
        private async UniTask<int> CleanupExpiredCacheEntriesAsync()
        {
            var cleared = 0;
            
            if (!_config.EnableResourceCaching)
                return cleared;

            lock (_cacheLock)
            {
                var expiredKeys = _resourceCache
                    .Where(kvp => !kvp.Value.IsValid)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _resourceCache.Remove(key);
                    _resourcePriorities.Remove(key);
                    cleared++;
                }
            }

            await UniTask.Yield(); // Allow other operations to proceed
            return cleared;
        }

        /// <summary>
        /// Perform moderate memory cleanup
        /// </summary>
        private async UniTask<int> PerformModerateCleanupAsync()
        {
            var cleared = await CleanupExpiredCacheEntriesAsync();
            
            if (_config.EnableResourceCaching)
            {
                // Clear low priority resources (priority <= 30%)
                var initialCount = _resourceCache.Count;
                ClearCacheByPriority(0.3f);
                cleared += initialCount - _resourceCache.Count;
            }

            // Trigger provider cleanup
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider?.UnloadResources();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during moderate provider cleanup: {ex.Message}");
                }
            }

            await UniTask.Yield();
            return cleared;
        }

        /// <summary>
        /// Perform aggressive memory cleanup
        /// </summary>
        private async UniTask<int> PerformAggressiveCleanupAsync()
        {
            var cleared = await PerformModerateCleanupAsync();
            
            if (_config.EnableResourceCaching)
            {
                // Clear medium priority resources (priority <= 70%)
                var initialCount = _resourceCache.Count;
                ClearCacheByPriority(0.7f);
                cleared += initialCount - _resourceCache.Count;
            }

            // Force Unity garbage collection
            await UnityEngine.Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await UniTask.Yield();
            return cleared;
        }

        /// <summary>
        /// Perform emergency memory cleanup
        /// </summary>
        private async UniTask<int> PerformEmergencyCleanupAsync()
        {
            var cleared = 0;
            
            if (_config.EnableResourceCaching)
            {
                // Clear almost all cached resources (keep only highest priority)
                lock (_cacheLock)
                {
                    var toKeep = _resourceCache
                        .Where(kvp => kvp.Value.Priority > 0.9f)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    
                    cleared = _resourceCache.Count - toKeep.Count;
                    
                    _resourceCache.Clear();
                    _resourcePriorities.Clear();
                    _cacheEvictionQueue.Clear();
                    
                    // Restore only highest priority resources
                    foreach (var kvp in toKeep)
                    {
                        _resourceCache[kvp.Key] = kvp.Value;
                        _resourcePriorities[kvp.Key] = kvp.Value.Priority;
                        _cacheEvictionQueue.Enqueue(kvp.Key);
                    }
                }
            }

            // Aggressive provider cleanup
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider?.UnloadResources();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error during emergency provider cleanup: {ex.Message}");
                }
            }

            // Multiple GC cycles
            await UnityEngine.Resources.UnloadUnusedAssets();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await UniTask.Yield();
            }

            return cleared;
        }
        
        public void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel)
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"ResourceService: Memory pressure changed from {previousLevel} to {newLevel}");
            }
        }
        
        #endregion
        
        #region Resource Caching and Optimization Methods

        /// <summary>
        /// Get cached resource if available and valid
        /// </summary>
        private Resource<T> GetCachedResource<T>(string path) where T : UnityEngine.Object
        {
            lock (_cacheLock)
            {
                if (_resourceCache.TryGetValue(path, out var cached) && 
                    cached.IsValid && 
                    cached.ResourceType == typeof(T))
                {
                    cached.UpdateAccess();
                    return cached.Resource as Resource<T>;
                }
                
                // Remove invalid cache entry
                if (cached != null && !cached.IsValid)
                {
                    _resourceCache.Remove(path);
                    _resourcePriorities.Remove(path);
                }
                
                return null;
            }
        }

        /// <summary>
        /// Cache a loaded resource
        /// </summary>
        private void CacheResource<T>(string path, Resource<T> resource, float priority) where T : UnityEngine.Object
        {
            if (!_config.EnableResourceCaching || resource == null)
                return;

            lock (_cacheLock)
            {
                // Check cache size limit
                if (_resourceCache.Count >= _config.MaxCacheSize)
                {
                    EvictLeastUsedResources();
                }

                var cached = new CachedResource
                {
                    Resource = resource,
                    ResourceType = typeof(T),
                    CachedTime = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = 1,
                    Priority = priority,
                    MemorySize = EstimateResourceMemorySize(resource)
                };

                _resourceCache[path] = cached;
                _resourcePriorities[path] = priority;
                _cacheEvictionQueue.Enqueue(path);

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Cached resource {path} (Priority: {priority:F2}, Size: {cached.MemorySize} bytes)");
                }
            }
        }

        /// <summary>
        /// Evict least recently used resources from cache
        /// </summary>
        private void EvictLeastUsedResources()
        {
            var evictionCount = Math.Max(1, _config.MaxCacheSize / 4); // Evict 25% of cache
            var candidates = _resourceCache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ThenBy(kvp => kvp.Value.Priority)
                .Take(evictionCount)
                .ToList();

            foreach (var candidate in candidates)
            {
                _resourceCache.Remove(candidate.Key);
                _resourcePriorities.Remove(candidate.Key);
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"ResourceService: Evicted cached resource {candidate.Key}");
                }
            }

            // Rebuild eviction queue
            _cacheEvictionQueue.Clear();
            foreach (var key in _resourceCache.Keys)
            {
                _cacheEvictionQueue.Enqueue(key);
            }
        }

        /// <summary>
        /// Estimate memory size of a resource
        /// </summary>
        private long EstimateResourceMemorySize<T>(Resource<T> resource) where T : UnityEngine.Object
        {
            try
            {
                // Basic estimation - in practice, this could be more sophisticated
                if (resource?.Asset is Texture2D texture)
                {
                    return texture.width * texture.height * 4; // Assume 4 bytes per pixel
                }
                else if (resource?.Asset is AudioClip audio)
                {
                    return audio.samples * audio.channels * 4; // Assume 4 bytes per sample
                }
                else
                {
                    return 1024; // Default 1KB estimate
                }
            }
            catch
            {
                return 1024; // Fallback estimate
            }
        }

        /// <summary>
        /// Clear resources from cache based on priority
        /// </summary>
        private void ClearCacheByPriority(float maxPriority)
        {
            lock (_cacheLock)
            {
                var toRemove = _resourceCache
                    .Where(kvp => kvp.Value.Priority <= maxPriority)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _resourceCache.Remove(key);
                    _resourcePriorities.Remove(key);
                }

                if (_config.EnableDetailedLogging && toRemove.Count > 0)
                {
                    Debug.Log($"ResourceService: Cleared {toRemove.Count} cached resources with priority <= {maxPriority:F2}");
                }
            }
        }

        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Load resource with retry logic
        /// </summary>
        private async UniTask<Resource<T>> LoadWithRetryAsync<T>(IResourceProvider provider, string path, ProviderType providerType, CancellationToken cancellationToken) 
            where T : UnityEngine.Object
        {
            var retryCount = 0;
            Exception lastException = null;
            
            while (retryCount <= _config.MaxRetryAttempts)
            {
                try
                {
                    var resource = await provider.LoadResourceAsync<T>(path);
                    
                    if (resource != null && resource.IsValid)
                    {
                        // Track provider-specific statistics
                        lock (_loadCountsByProvider)
                        {
                            if (!_loadCountsByProvider.ContainsKey(providerType))
                                _loadCountsByProvider[providerType] = 0;
                            _loadCountsByProvider[providerType]++;
                        }
                        
                        return resource;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (retryCount < _config.MaxRetryAttempts)
                    {
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.LogWarning($"Retry {retryCount + 1}/{_config.MaxRetryAttempts} for {path} after error: {ex.Message}");
                        }
                        
                        // Wait before retry with exponential backoff
                        var delay = (int)(_config.RetryDelaySeconds * 1000 * Math.Pow(2, retryCount));
                        await UniTask.Delay(delay, cancellationToken: cancellationToken);
                    }
                }
                
                retryCount++;
            }
            
            if (lastException != null)
                throw lastException;
                
            return null;
        }
        
        /// <summary>
        /// Record successful load for statistics
        /// </summary>
        private void RecordLoadSuccess(double loadTimeMs)
        {
            Interlocked.Increment(ref _totalResourcesLoaded);
            
            lock (_loadTimes)
            {
                _loadTimes.Add(loadTimeMs);
                
                // Keep only last 100 load times to prevent memory growth
                if (_loadTimes.Count > 100)
                {
                    _loadTimes.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// Record failed load for statistics
        /// </summary>
        private void RecordLoadFailure()
        {
            Interlocked.Increment(ref _totalLoadFailures);
        }
        
        #endregion
    }
}
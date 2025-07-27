using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services.Performance;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Centralized resource path resolution service for the entire engine
    /// Provides unified, configurable, and performant path resolution for all resource types
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.Critical, 
        Description = "Centralized resource path resolution with caching and fallback support")]
    [ServiceConfiguration(typeof(ResourcePathResolverConfiguration))]
    public class ResourcePathResolver : IResourcePathResolver, IMemoryPressureResponder
    {
        #region Private Fields
        
        private readonly ResourcePathResolverConfiguration _config;
        private readonly LRUPathCache _pathCache;
        private readonly Dictionary<(ResourceType, ResourceCategory), PathTemplateData> _templates;
        private readonly Dictionary<(ResourceType, ResourceCategory), List<PathTemplateData>> _fallbackTemplates;
        private readonly ReaderWriterLockSlim _templatesLock;
        
        // Performance tracking and monitoring
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly StringInternPool _stringInternPool;
        private DateTime _lastCacheOptimization;
        
        // Current state
        private ResourceEnvironment _currentEnvironment;
        private bool _disposed;
        
        // Configuration
        private readonly Regex _parameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
        
        #endregion
        
        #region Constructor and Initialization
        
        public ResourcePathResolver(ResourcePathResolverConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            // Initialize high-performance resolution cache
            _pathCache = new LRUPathCache(
                _config.MaxCacheSize,
                _config.CacheEntryLifetime,
                _config.EnableLRUEviction);
            
            // Initialize performance monitoring
            _performanceMonitor = new PerformanceMonitor(_config.MaxResolutionTimeMs);
            
            // Initialize string interning for memory optimization
            _stringInternPool = new StringInternPool();
            
            _templates = new Dictionary<(ResourceType, ResourceCategory), PathTemplateData>();
            _fallbackTemplates = new Dictionary<(ResourceType, ResourceCategory), List<PathTemplateData>>();
            _templatesLock = new ReaderWriterLockSlim();
            
            _currentEnvironment = _config.DefaultEnvironment;
            _lastCacheOptimization = DateTime.UtcNow;
            
            InitializeTemplates();
            
            Debug.Log($"[ResourcePathResolver] Initialized with {_templates.Count} templates, " +
                     $"{_fallbackTemplates.Count} fallback groups, and LRU cache (size: {_config.MaxCacheSize})");
        }
        
        private void InitializeTemplates()
        {
            _templatesLock.EnterWriteLock();
            try
            {
                _templates.Clear();
                _fallbackTemplates.Clear();
                
                // Load primary templates
                foreach (var template in _config.PathTemplates)
                {
                    var key = (template.ResourceType, template.Category);
                    var templateData = new PathTemplateData(template.Template, template.Priority, template.Description);
                    _templates[key] = templateData;
                }
                
                // Load fallback templates
                foreach (var fallback in _config.FallbackPaths)
                {
                    var key = (fallback.ResourceType, fallback.Category);
                    var templateData = new PathTemplateData(fallback.FallbackTemplate, fallback.Priority, "Fallback template");
                    
                    if (!_fallbackTemplates.ContainsKey(key))
                        _fallbackTemplates[key] = new List<PathTemplateData>();
                    
                    _fallbackTemplates[key].Add(templateData);
                }
                
                // Sort fallback templates by priority
                foreach (var fallbackList in _fallbackTemplates.Values)
                {
                    fallbackList.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                }
            }
            finally
            {
                _templatesLock.ExitWriteLock();
            }
        }
        
        #endregion
        
        #region IEngineService Implementation
        
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken)
        {
            try
            {
                // Validate configuration
                if (_config.ValidateTemplatesAtStartup && !ValidateConfiguration(out var errors))
                {
                    var errorMessage = $"ResourcePathResolver configuration validation failed: {string.Join(", ", errors)}";
                    
                    if (_config.StrictValidationMode)
                        return ServiceInitializationResult.Failed(errorMessage);
                    else
                        Debug.LogWarning($"[ResourcePathResolver] {errorMessage}");
                }
                
                // Apply environment-specific overrides
                var effectiveConfig = _config.GetEffectiveConfiguration(_currentEnvironment);
                if (effectiveConfig != _config)
                {
                    Debug.Log($"[ResourcePathResolver] Applied environment overrides for {_currentEnvironment}");
                }
                
                await UniTask.CompletedTask;
                
                Debug.Log($"[ResourcePathResolver] Initialized successfully with {_templates.Count} templates");
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePathResolver] Initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex.Message);
            }
        }
        
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                ClearCache();
                _pathCache?.Dispose();
                _stringInternPool?.Dispose();
                _templatesLock?.Dispose();
                _disposed = true;
                
                await UniTask.CompletedTask;
                
                Debug.Log("[ResourcePathResolver] Shutdown completed successfully");
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePathResolver] Shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex.Message);
            }
        }
        
        #endregion
        
        #region Core Path Resolution
        
        public string ResolveResourcePath(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters)
        {
            var result = ResolveResourcePathDetailed(resourceType, resourceId, category, parameters);
            return result.IsSuccess ? result.ResolvedPath : null;
        }
        
        public PathResolutionResult ResolveResourcePathDetailed(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters)
        {
            if (_disposed)
                return PathResolutionResult.Failure("Service is disposed");
            
            if (string.IsNullOrEmpty(resourceId))
                return PathResolutionResult.Failure("Resource ID cannot be null or empty");
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Generate cache key
                var cacheKey = GenerateCacheKey(resourceType, resourceId, category, parameters);
                
                // Check LRU cache first
                if (_pathCache.TryGet(cacheKey, out var cachedEntry))
                {
                    var templateKey = $"{resourceType}.{category}";
                    _performanceMonitor.RecordResolution(templateKey, stopwatch.Elapsed, true, cachedEntry.ResolvedPath);
                    return PathResolutionResult.Success(cachedEntry.ResolvedPath, cachedEntry.Priority, true, stopwatch.Elapsed);
                }
                
                // Resolve path using templates
                var resolved = ResolvePathInternal(resourceType, resourceId, category, parameters);
                
                if (resolved.IsSuccess)
                {
                    // Add to LRU cache
                    _pathCache.Put(cacheKey, resolved.ResolvedPath, resolved.UsedPriority, stopwatch.Elapsed);
                    
                    var templateKey = $"{resourceType}.{category}";
                    _performanceMonitor.RecordResolution(templateKey, stopwatch.Elapsed, false, resolved.ResolvedPath);
                    return PathResolutionResult.Success(resolved.ResolvedPath, resolved.UsedPriority, false, stopwatch.Elapsed);
                }
                
                // Try fallback paths
                var fallbackResult = TryFallbackPaths(resourceType, resourceId, category, parameters);
                if (fallbackResult.IsSuccess)
                {
                    // Cache fallback result
                    _pathCache.Put(cacheKey, fallbackResult.ResolvedPath, fallbackResult.UsedPriority, stopwatch.Elapsed);
                    
                    var templateKey = $"{resourceType}.{category}.Fallback";
                    _performanceMonitor.RecordResolution(templateKey, stopwatch.Elapsed, false, fallbackResult.ResolvedPath);
                    return PathResolutionResult.Success(fallbackResult.ResolvedPath, fallbackResult.UsedPriority, false, stopwatch.Elapsed);
                }
                
                var failedTemplateKey = $"{resourceType}.{category}";
                _performanceMonitor.RecordResolution(failedTemplateKey, stopwatch.Elapsed, false, "FAILED");
                OnPathResolutionFailed?.Invoke(resourceType, resourceId, category, $"No template found for {resourceType}.{category}");
                return PathResolutionResult.Failure($"Unable to resolve path for {resourceType}.{category} with ID '{resourceId}'", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                var errorTemplateKey = $"{resourceType}.{category}";
                _performanceMonitor.RecordResolution(errorTemplateKey, stopwatch.Elapsed, false, "ERROR");
                Debug.LogError($"[ResourcePathResolver] Error resolving path: {ex.Message}");
                return PathResolutionResult.Failure($"Resolution error: {ex.Message}", stopwatch.Elapsed);
            }
        }
        
        public async UniTask<PathResolutionResult> ResolveResourcePathAsync(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, bool validateExists = false, 
            CancellationToken cancellationToken = default, params PathParameter[] parameters)
        {
            // Resolve path synchronously first
            var result = ResolveResourcePathDetailed(resourceType, resourceId, category, parameters);
            
            if (!result.IsSuccess || !validateExists)
                return result;
            
            // Validate existence asynchronously
            var exists = await ValidateResourceExistsAsync(result.ResolvedPath, cancellationToken);
            
            if (!exists)
            {
                OnPathResolutionFailed?.Invoke(resourceType, resourceId, category, $"Resource not found at path: {result.ResolvedPath}");
                return PathResolutionResult.Failure($"Resource does not exist at resolved path: {result.ResolvedPath}", result.ResolutionTime);
            }
            
            return result;
        }
        
        private PathResolutionResult ResolvePathInternal(ResourceType resourceType, string resourceId, 
            ResourceCategory category, PathParameter[] parameters)
        {
            _templatesLock.EnterReadLock();
            try
            {
                var key = (resourceType, category);
                if (!_templates.TryGetValue(key, out var templateData))
                {
                    return PathResolutionResult.Failure($"No template found for {resourceType}.{category}");
                }
                
                var resolvedPath = SubstituteParameters(templateData.Template, resourceId, parameters);
                var fullPath = CombineWithResourceRoot(resolvedPath);
                
                return PathResolutionResult.Success(fullPath, templateData.Priority);
            }
            finally
            {
                _templatesLock.ExitReadLock();
            }
        }
        
        private PathResolutionResult TryFallbackPaths(ResourceType resourceType, string resourceId, 
            ResourceCategory category, PathParameter[] parameters)
        {
            _templatesLock.EnterReadLock();
            try
            {
                var key = (resourceType, category);
                if (!_fallbackTemplates.TryGetValue(key, out var fallbackList) || fallbackList.Count == 0)
                {
                    return PathResolutionResult.Failure("No fallback templates available");
                }
                
                foreach (var fallbackTemplate in fallbackList)
                {
                    try
                    {
                        var resolvedPath = SubstituteParameters(fallbackTemplate.Template, resourceId, parameters);
                        var fullPath = CombineWithResourceRoot(resolvedPath);
                        
                        return PathResolutionResult.Success(fullPath, fallbackTemplate.Priority);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ResourcePathResolver] Fallback template failed: {ex.Message}");
                        continue;
                    }
                }
                
                return PathResolutionResult.Failure("All fallback templates failed");
            }
            finally
            {
                _templatesLock.ExitReadLock();
            }
        }
        
        #endregion
        
        #region Parameter Substitution
        
        private string SubstituteParameters(string template, string resourceId, PathParameter[] parameters)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;
            
            var result = template;
            var parameterDict = CreateParameterDictionary(resourceId, parameters);
            
            // Replace all parameter placeholders
            result = _parameterRegex.Replace(result, match =>
            {
                var paramName = match.Groups[1].Value;
                var parts = paramName.Split(':');
                var actualParamName = _stringInternPool.Intern(parts[0]);
                var format = parts.Length > 1 ? _stringInternPool.Intern(parts[1]) : null;
                
                if (parameterDict.TryGetValue(actualParamName, out var parameter))
                {
                    var substitutedValue = string.IsNullOrEmpty(format) ? parameter.ToString() : parameter.ToString(format);
                    return _stringInternPool.Intern(substitutedValue);
                }
                
                Debug.LogWarning($"[ResourcePathResolver] Parameter '{actualParamName}' not found in template: {template}");
                return match.Value; // Return original placeholder if parameter not found
            });
            
            return _stringInternPool.Intern(result);
        }
        
        private Dictionary<string, PathParameter> CreateParameterDictionary(string resourceId, PathParameter[] parameters)
        {
            var dict = new Dictionary<string, PathParameter>(StringComparer.OrdinalIgnoreCase)
            {
                // Always include resourceId as a parameter
                [PathParameterNames.RESOURCE_ID] = new PathParameter(PathParameterNames.RESOURCE_ID, resourceId),
                [PathParameterNames.ID] = new PathParameter(PathParameterNames.ID, resourceId),
                
                // Include environment
                [PathParameterNames.ENVIRONMENT] = new PathParameter(PathParameterNames.ENVIRONMENT, _currentEnvironment.ToString()),
                [PathParameterNames.RESOURCE_ROOT] = new PathParameter(PathParameterNames.RESOURCE_ROOT, _config.ResourceRoot)
            };
            
            // Add custom parameters
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    dict[param.Name] = param;
                }
            }
            
            return dict;
        }
        
        private string CombineWithResourceRoot(string relativePath)
        {
            if (string.IsNullOrEmpty(_config.ResourceRoot))
                return relativePath;
            
            return System.IO.Path.Combine(_config.ResourceRoot, relativePath).Replace('\\', '/');
        }
        
        #endregion
        
        #region Fallback Path Management
        
        public string[] GetFallbackPaths(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters)
        {
            var pathsWithPriority = GetFallbackPathsWithPriority(resourceType, resourceId, category, parameters);
            return pathsWithPriority.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
        }
        
        public Dictionary<string, PathPriority> GetFallbackPathsWithPriority(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters)
        {
            var result = new Dictionary<string, PathPriority>();
            
            _templatesLock.EnterReadLock();
            try
            {
                var key = (resourceType, category);
                if (_fallbackTemplates.TryGetValue(key, out var fallbackList))
                {
                    foreach (var fallbackTemplate in fallbackList)
                    {
                        try
                        {
                            var resolvedPath = SubstituteParameters(fallbackTemplate.Template, resourceId, parameters);
                            var fullPath = CombineWithResourceRoot(resolvedPath);
                            result[fullPath] = fallbackTemplate.Priority;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ResourcePathResolver] Failed to resolve fallback path: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                _templatesLock.ExitReadLock();
            }
            
            return result;
        }
        
        #endregion
        
        #region Path Validation
        
        public bool ValidateResourcePath(string path, out string correctedPath)
        {
            correctedPath = path;
            
            if (string.IsNullOrEmpty(path))
                return false;
            
            // Basic path validation
            var isValid = true;
            var corrections = new StringBuilder(path);
            
            // Fix common path issues
            if (path.Contains("\\"))
            {
                corrections.Replace('\\', '/');
                isValid = false;
            }
            
            if (path.Contains("//"))
            {
                corrections.Replace("//", "/");
                isValid = false;
            }
            
            if (path.StartsWith("/"))
            {
                corrections.Remove(0, 1);
                isValid = false;
            }
            
            correctedPath = corrections.ToString();
            return isValid;
        }
        
        public Dictionary<string, bool> ValidateResourcePaths(IEnumerable<string> paths)
        {
            var results = new Dictionary<string, bool>();
            
            foreach (var path in paths)
            {
                results[path] = ValidateResourcePath(path, out _);
            }
            
            return results;
        }
        
        public async UniTask<bool> ValidateResourceExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableExistenceChecking)
                return true; // Assume exists if checking is disabled
            
            try
            {
                // In a real implementation, this would check if the resource exists
                // For now, we'll do a simple null/empty check
                await UniTask.CompletedTask;
                return !string.IsNullOrEmpty(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourcePathResolver] Error validating resource existence: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Template Management
        
        public void RegisterPathTemplate(ResourceType resourceType, ResourceCategory category, 
            string pathTemplate, PathPriority priority = PathPriority.Normal)
        {
            if (string.IsNullOrEmpty(pathTemplate))
                throw new ArgumentException("Path template cannot be null or empty", nameof(pathTemplate));
            
            _templatesLock.EnterWriteLock();
            try
            {
                var key = (resourceType, category);
                var templateData = new PathTemplateData(pathTemplate, priority, "Runtime registered template");
                _templates[key] = templateData;
                
                Debug.Log($"[ResourcePathResolver] Registered template for {resourceType}.{category}: {pathTemplate}");
            }
            finally
            {
                _templatesLock.ExitWriteLock();
            }
        }
        
        public bool UnregisterPathTemplate(ResourceType resourceType, ResourceCategory category)
        {
            _templatesLock.EnterWriteLock();
            try
            {
                var key = (resourceType, category);
                var removed = _templates.Remove(key);
                
                if (removed)
                {
                    Debug.Log($"[ResourcePathResolver] Unregistered template for {resourceType}.{category}");
                }
                
                return removed;
            }
            finally
            {
                _templatesLock.ExitWriteLock();
            }
        }
        
        public string GetPathTemplate(ResourceType resourceType, ResourceCategory category)
        {
            _templatesLock.EnterReadLock();
            try
            {
                var key = (resourceType, category);
                return _templates.TryGetValue(key, out var templateData) ? templateData.Template : null;
            }
            finally
            {
                _templatesLock.ExitReadLock();
            }
        }
        
        public Dictionary<(ResourceType, ResourceCategory), string> GetAllPathTemplates()
        {
            _templatesLock.EnterReadLock();
            try
            {
                return _templates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Template);
            }
            finally
            {
                _templatesLock.ExitReadLock();
            }
        }
        
        #endregion
        
        #region Environment and Context
        
        public void SetResourceEnvironment(ResourceEnvironment environment)
        {
            _currentEnvironment = environment;
            ClearCache(); // Clear cache when environment changes
            Debug.Log($"[ResourcePathResolver] Environment changed to: {environment}");
        }
        
        public ResourceEnvironment GetResourceEnvironment()
        {
            return _currentEnvironment;
        }
        
        public T WithEnvironmentOverride<T>(ResourceEnvironment environment, Func<T> operation)
        {
            var originalEnvironment = _currentEnvironment;
            try
            {
                SetResourceEnvironment(environment);
                return operation();
            }
            finally
            {
                SetResourceEnvironment(originalEnvironment);
            }
        }
        
        #endregion
                
        #region Configuration Management
        
        public async UniTask ReloadConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                InitializeTemplates();
                ClearCache();
                
                await UniTask.CompletedTask;
                
                OnConfigurationReloaded?.Invoke(_config);
                Debug.Log("[ResourcePathResolver] Configuration reloaded successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePathResolver] Failed to reload configuration: {ex.Message}");
                throw;
            }
        }
        
        public bool ValidateConfiguration(out List<string> errors)
        {
            return _config.Validate(out errors);
        }
        
        public ResourcePathResolverConfiguration GetCurrentConfiguration()
        {
            return _config;
        }
        
        #endregion
        
        #region Events
        
        public event Action<ResourcePathResolverConfiguration> OnConfigurationReloaded;
        public event Action<ResourceType, string, ResourceCategory, string> OnPathResolutionFailed;
        public event Action<int, int> OnCacheOptimized;
        
        #endregion
        
        #region Memory Pressure Response
        
        public void OnMemoryPressure(float pressureLevel)
        {
            if (!_config.EnableMemoryPressureResponse)
                return;
            
            if (pressureLevel > 0.8f)
            {
                ClearCache();
                Debug.Log($"[ResourcePathResolver] Cleared cache due to high memory pressure: {pressureLevel:P1}");
            }
            else if (pressureLevel > 0.6f)
            {
                OptimizeCache();
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private string GenerateCacheKey(ResourceType resourceType, string resourceId, ResourceCategory category, PathParameter[] parameters)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(_stringInternPool.Intern(resourceType.ToString())).Append('|');
            keyBuilder.Append(_stringInternPool.Intern(resourceId)).Append('|');
            keyBuilder.Append(_stringInternPool.Intern(category.ToString())).Append('|');
            keyBuilder.Append(_stringInternPool.Intern(_currentEnvironment.ToString())).Append('|');
            
            if (parameters != null && parameters.Length > 0)
            {
                foreach (var param in parameters.OrderBy(p => p.Name))
                {
                    keyBuilder.Append(_stringInternPool.Intern(param.Name)).Append('=')
                              .Append(_stringInternPool.Intern(param.Value?.ToString() ?? "")).Append('|');
                }
            }
            
            return _stringInternPool.Intern(keyBuilder.ToString());
        }
        
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                ClearCache();
                _templatesLock?.Dispose();
                _disposed = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePathResolver] Error during disposal: {ex.Message}");
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var errors = new List<string>();
                var isHealthy = ValidateConfiguration(out errors);
                
                if (!isHealthy)
                {
                    return ServiceHealthStatus.Unhealthy($"Configuration errors: {string.Join(", ", errors)}");
                }
                
                // Check cache health
                var stats = GetStatistics();
                if (stats.CacheHitRate < 0.3 && stats.TotalResolutions > 100)
                {
                    return ServiceHealthStatus.Degraded("Low cache hit rate");
                }
                
                // Check resolution performance
                if (stats.MaxResolutionTime.TotalMilliseconds > _config.MaxResolutionTimeMs * 10)
                {
                    return ServiceHealthStatus.Degraded($"Slow resolution detected: {stats.MaxResolutionTime.TotalMilliseconds:F2}ms");
                }
                
                await UniTask.CompletedTask;
                return ServiceHealthStatus.Healthy();
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }

        public async UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            if (!_config.EnableMemoryPressureResponse)
            {
                await UniTask.CompletedTask;
                return;
            }
            
            float pressureLevelFloat = (float)pressureLevel / 3.0f; // Convert enum to 0.0-1.0 range
            
            // Use the LRU cache's memory pressure response
            _pathCache.RespondToMemoryPressure(pressureLevelFloat);
            
            Debug.Log($"[ResourcePathResolver] Responded to {pressureLevel} memory pressure");
            await UniTask.CompletedTask;
        }

        public void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel)
        {
            if (!_config.EnableMemoryPressureResponse)
                return;
                
            Debug.Log($"[ResourcePathResolver] Memory pressure changed from {previousLevel} to {newLevel}");
            
            // Proactive response to memory pressure changes
            if (newLevel >= MemoryPressureMonitor.MemoryPressureLevel.High && previousLevel < MemoryPressureMonitor.MemoryPressureLevel.High)
            {
                // Entering high pressure - be aggressive about cleanup
                _pathCache.RespondToMemoryPressure(0.9f);
            }
            else if (newLevel == MemoryPressureMonitor.MemoryPressureLevel.Medium && previousLevel < MemoryPressureMonitor.MemoryPressureLevel.Medium)
            {
                // Entering medium pressure - optimize cache
                _pathCache.RespondToMemoryPressure(0.6f);
            }
        }
        
        #endregion
        
        #region Performance and Monitoring
        
        /// <summary>
        /// Gets comprehensive performance statistics
        /// </summary>
        public ResourcePathResolverStatistics GetStatistics()
        {
            var perfStats = _performanceMonitor.GetStatistics();
            var cacheStats = _pathCache.GetStatistics();
            
            return new ResourcePathResolverStatistics(
                totalResolutions: (int)perfStats.TotalResolutions,
                cacheHits: (int)cacheStats.TotalHits,
                cacheMisses: (int)cacheStats.TotalMisses,
                averageResolutionTime: TimeSpan.FromMilliseconds(perfStats.AverageResolutionTimeMs),
                maxResolutionTime: TimeSpan.FromMilliseconds(perfStats.MaxResolutionTimeMs),
                cachedPaths: cacheStats.CacheSize,
                memoryUsageBytes: cacheStats.MemoryUsageBytes,
                lastCacheOptimization: _lastCacheOptimization
            );
        }
        
        /// <summary>
        /// Clears all cached paths
        /// </summary>
        public void ClearCache()
        {
            _pathCache.Clear();
            _lastCacheOptimization = DateTime.UtcNow;
            OnCacheOptimized?.Invoke(0, 0);
        }
        
        /// <summary>
        /// Optimizes cache by removing expired entries
        /// </summary>
        public void OptimizeCache()
        {
            var statsBefore = _pathCache.GetStatistics();
            _pathCache.RespondToMemoryPressure(0.4f); // Light cleanup
            var statsAfter = _pathCache.GetStatistics();
            
            var removedEntries = statsBefore.CacheSize - statsAfter.CacheSize;
            _lastCacheOptimization = DateTime.UtcNow;
            
            OnCacheOptimized?.Invoke(removedEntries, statsAfter.CacheSize);
            Debug.Log($"[ResourcePathResolver] Cache optimized: removed {removedEntries} entries, {statsAfter.CacheSize} remaining");
        }
        
        /// <summary>
        /// Gets performance monitoring health check result
        /// </summary>
        public HealthCheckResult GetPerformanceHealthCheck()
        {
            return _performanceMonitor.PerformHealthCheck();
        }
        
        /// <summary>
        /// Gets string interning statistics
        /// </summary>
        public InternPoolStatistics GetStringInternStatistics()
        {
            return _stringInternPool.GetStatistics();
        }
        
        /// <summary>
        /// Preloads frequently used paths into cache
        /// </summary>
        public async UniTask PreloadPathsAsync(IEnumerable<(ResourceType type, string id, ResourceCategory category)> resourceSpecs, 
            CancellationToken cancellationToken = default)
        {
            if (resourceSpecs == null)
                return;
            
            var preloadTasks = new List<UniTask>();
            
            foreach (var (type, id, category) in resourceSpecs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                // Create preload task for each resource spec
                var preloadTask = PreloadSinglePathAsync(type, id, category, cancellationToken);
                preloadTasks.Add(preloadTask);
            }
            
            if (preloadTasks.Count > 0)
            {
                await UniTask.WhenAll(preloadTasks);
                Debug.Log($"[ResourcePathResolver] Preloaded {preloadTasks.Count} paths into cache");
            }
        }
        
        private async UniTask PreloadSinglePathAsync(ResourceType resourceType, string resourceId, 
            ResourceCategory category, CancellationToken cancellationToken)
        {
            try
            {
                // Resolve the path to warm up the cache
                var result = ResolveResourcePathDetailed(resourceType, resourceId, category);
                
                if (!result.IsSuccess)
                {
                    Debug.LogWarning($"[ResourcePathResolver] Failed to preload path for {resourceType}.{category} with ID '{resourceId}': {result.ErrorMessage}");
                }
                
                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePathResolver] Error preloading {resourceType}.{category} with ID '{resourceId}': {ex.Message}");
            }
        }

        #endregion
        
    }
    
    #region Supporting Data Structures
    
    internal class PathTemplateData
    {
        public string Template { get; }
        public PathPriority Priority { get; }
        public string Description { get; }
        
        public PathTemplateData(string template, PathPriority priority, string description)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Priority = priority;
            Description = description ?? string.Empty;
        }
    }
    
    #endregion
}
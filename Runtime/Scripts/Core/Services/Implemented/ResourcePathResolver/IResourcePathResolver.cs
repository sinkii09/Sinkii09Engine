using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core interface for centralized resource path resolution across the entire engine
    /// Provides unified, configurable, and performant path resolution for all resource types
    /// </summary>
    public interface IResourcePathResolver : IEngineService
    {
        #region Core Path Resolution
        
        /// <summary>
        /// Resolves a resource path using the configured templates and parameters
        /// </summary>
        /// <param name="resourceType">Type of resource to resolve</param>
        /// <param name="resourceId">Unique identifier for the resource</param>
        /// <param name="category">Category of resource (Primary, Sprites, Audio, etc.)</param>
        /// <param name="parameters">Additional parameters for path substitution</param>
        /// <returns>Resolved resource path or null if resolution fails</returns>
        string ResolveResourcePath(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters);
        
        /// <summary>
        /// Resolves a resource path with detailed result information
        /// </summary>
        /// <param name="resourceType">Type of resource to resolve</param>
        /// <param name="resourceId">Unique identifier for the resource</param>
        /// <param name="category">Category of resource</param>
        /// <param name="parameters">Additional parameters for path substitution</param>
        /// <returns>Detailed resolution result with success status and metadata</returns>
        PathResolutionResult ResolveResourcePathDetailed(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters);
        
        /// <summary>
        /// Asynchronously resolves a resource path with optional validation
        /// </summary>
        /// <param name="resourceType">Type of resource to resolve</param>
        /// <param name="resourceId">Unique identifier for the resource</param>
        /// <param name="category">Category of resource</param>
        /// <param name="validateExists">Whether to check if the resolved path exists</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Additional parameters for path substitution</param>
        /// <returns>Resolved resource path with validation result</returns>
        UniTask<PathResolutionResult> ResolveResourcePathAsync(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, bool validateExists = false, 
            CancellationToken cancellationToken = default, params PathParameter[] parameters);
        
        #endregion
        
        #region Fallback Path Management
        
        /// <summary>
        /// Gets all fallback paths for a resource in priority order
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="resourceId">Unique identifier for the resource</param>
        /// <param name="category">Category of resource</param>
        /// <param name="parameters">Additional parameters for path substitution</param>
        /// <returns>Array of fallback paths in priority order</returns>
        string[] GetFallbackPaths(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters);
        
        /// <summary>
        /// Gets fallback paths with detailed priority information
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="resourceId">Unique identifier for the resource</param>
        /// <param name="category">Category of resource</param>
        /// <param name="parameters">Additional parameters for path substitution</param>
        /// <returns>Dictionary of paths with their priority levels</returns>
        Dictionary<string, PathPriority> GetFallbackPathsWithPriority(ResourceType resourceType, string resourceId, 
            ResourceCategory category = ResourceCategory.Primary, params PathParameter[] parameters);
        
        #endregion
        
        #region Path Validation
        
        /// <summary>
        /// Validates a resource path and provides correction suggestions
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <param name="correctedPath">Suggested corrected path if validation fails</param>
        /// <returns>True if path is valid, false otherwise</returns>
        bool ValidateResourcePath(string path, out string correctedPath);
        
        /// <summary>
        /// Validates multiple resource paths in batch
        /// </summary>
        /// <param name="paths">Paths to validate</param>
        /// <returns>Dictionary of paths with their validation results</returns>
        Dictionary<string, bool> ValidateResourcePaths(IEnumerable<string> paths);
        
        /// <summary>
        /// Asynchronously validates if a resource path exists
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if resource exists at the path</returns>
        UniTask<bool> ValidateResourceExistsAsync(string path, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Template Management
        
        /// <summary>
        /// Registers a path template for a specific resource type and category
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="category">Category of resource</param>
        /// <param name="pathTemplate">Template string with parameter placeholders</param>
        /// <param name="priority">Priority level for this template</param>
        void RegisterPathTemplate(ResourceType resourceType, ResourceCategory category, 
            string pathTemplate, PathPriority priority = PathPriority.Normal);
        
        /// <summary>
        /// Unregisters a path template
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="category">Category of resource</param>
        /// <returns>True if template was removed, false if not found</returns>
        bool UnregisterPathTemplate(ResourceType resourceType, ResourceCategory category);
        
        /// <summary>
        /// Gets the registered template for a resource type and category
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="category">Category of resource</param>
        /// <returns>Template string or null if not found</returns>
        string GetPathTemplate(ResourceType resourceType, ResourceCategory category);
        
        /// <summary>
        /// Gets all registered templates for debugging and configuration
        /// </summary>
        /// <returns>Dictionary of all registered templates</returns>
        Dictionary<(ResourceType, ResourceCategory), string> GetAllPathTemplates();
        
        #endregion
        
        #region Environment and Context
        
        /// <summary>
        /// Sets the current resource environment (Development, Production, etc.)
        /// </summary>
        /// <param name="environment">Target environment</param>
        void SetResourceEnvironment(ResourceEnvironment environment);
        
        /// <summary>
        /// Gets the current resource environment
        /// </summary>
        /// <returns>Current environment setting</returns>
        ResourceEnvironment GetResourceEnvironment();
        
        /// <summary>
        /// Temporarily overrides the environment for a specific operation
        /// </summary>
        /// <param name="environment">Temporary environment</param>
        /// <param name="operation">Operation to execute with override</param>
        /// <returns>Result of the operation</returns>
        T WithEnvironmentOverride<T>(ResourceEnvironment environment, Func<T> operation);
        
        #endregion
        
        #region Performance and Monitoring
        
        /// <summary>
        /// Gets performance statistics for the resolver
        /// </summary>
        /// <returns>Performance metrics including cache hit rates and resolution times</returns>
        ResourcePathResolverStatistics GetStatistics();
        
        /// <summary>
        /// Clears all cached paths (use carefully in production)
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// Preloads frequently used paths into cache
        /// </summary>
        /// <param name="resourceSpecs">Resource specifications to preload</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask PreloadPathsAsync(IEnumerable<(ResourceType type, string id, ResourceCategory category)> resourceSpecs, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Optimizes cache by removing least recently used entries
        /// </summary>
        void OptimizeCache();
        
        #endregion
        
        #region Configuration Management
        
        /// <summary>
        /// Reloads configuration from the configured source
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask ReloadConfigurationAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <param name="errors">List of validation errors if any</param>
        /// <returns>True if configuration is valid</returns>
        bool ValidateConfiguration(out List<string> errors);
        
        /// <summary>
        /// Gets the current configuration for debugging
        /// </summary>
        /// <returns>Current configuration snapshot</returns>
        ResourcePathResolverConfiguration GetCurrentConfiguration();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when configuration is reloaded
        /// </summary>
        event Action<ResourcePathResolverConfiguration> OnConfigurationReloaded;
        
        /// <summary>
        /// Fired when a path resolution fails
        /// </summary>
        event Action<ResourceType, string, ResourceCategory, string> OnPathResolutionFailed;
        
        /// <summary>
        /// Fired when cache is cleared or optimized
        /// </summary>
        event Action<int, int> OnCacheOptimized; // (removedEntries, remainingEntries)
        
        #endregion
    }
    
    /// <summary>
    /// Performance statistics for the ResourcePathResolver
    /// </summary>
    public readonly struct ResourcePathResolverStatistics
    {
        public readonly int TotalResolutions;
        public readonly int CacheHits;
        public readonly int CacheMisses;
        public readonly double CacheHitRate;
        public readonly TimeSpan AverageResolutionTime;
        public readonly TimeSpan MaxResolutionTime;
        public readonly int CachedPaths;
        public readonly long MemoryUsageBytes;
        public readonly DateTime LastCacheOptimization;
        
        public ResourcePathResolverStatistics(int totalResolutions, int cacheHits, int cacheMisses, 
            TimeSpan averageResolutionTime, TimeSpan maxResolutionTime, int cachedPaths, 
            long memoryUsageBytes, DateTime lastCacheOptimization)
        {
            TotalResolutions = totalResolutions;
            CacheHits = cacheHits;
            CacheMisses = cacheMisses;
            CacheHitRate = totalResolutions > 0 ? (double)cacheHits / totalResolutions : 0.0;
            AverageResolutionTime = averageResolutionTime;
            MaxResolutionTime = maxResolutionTime;
            CachedPaths = cachedPaths;
            MemoryUsageBytes = memoryUsageBytes;
            LastCacheOptimization = lastCacheOptimization;
        }
    }
}
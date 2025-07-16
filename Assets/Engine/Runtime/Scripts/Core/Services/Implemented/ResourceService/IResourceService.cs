using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using Sinkii09.Engine.Common.Resources;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Enhanced ResourceService interface with modern async patterns, error handling, and memory management
    /// </summary>
    public interface IResourceService : IEngineService
    {
        #region Resource Loading Operations

        /// <summary>
        /// Load a single resource asynchronously with cancellation support
        /// </summary>
        UniTask<Resource<T>> LoadResourceAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        /// <summary>
        /// Load multiple resources asynchronously with cancellation support
        /// </summary>
        UniTask<IEnumerable<Resource<T>>> LoadResourcesAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        /// <summary>
        /// Check if a resource exists without loading it
        /// </summary>
        UniTask<bool> ResourceExistsAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        /// <summary>
        /// Locate available resources at the specified path
        /// </summary>
        UniTask<IEnumerable<string>> LocateResourcesAsync<T>(string path, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        /// <summary>
        /// Preload resources specified in configuration
        /// </summary>
        UniTask PreloadResourcesAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Provider Management

        /// <summary>
        /// Check if a specific provider type is initialized and healthy
        /// </summary>
        bool IsProviderInitialized(ProviderType providerType);

        /// <summary>
        /// Get a provider instance for the specified type
        /// </summary>
        IResourceProvider GetProvider(ProviderType providerType);

        /// <summary>
        /// Get all providers for the specified provider types
        /// </summary>
        List<IResourceProvider> GetProviders(ProviderType providerTypes);

        /// <summary>
        /// Get health status for all providers
        /// </summary>
        UniTask<Dictionary<ProviderType, bool>> GetProviderHealthStatusAsync();

        #endregion

        #region Memory Management

        /// <summary>
        /// Force immediate memory cleanup and resource unloading
        /// </summary>
        UniTask ForceMemoryCleanupAsync();

        /// <summary>
        /// Register a callback for memory pressure events
        /// </summary>
        void RegisterMemoryPressureCallback(Action<float> callback);

        /// <summary>
        /// Unregister a memory pressure callback
        /// </summary>
        void UnregisterMemoryPressureCallback(Action<float> callback);

        /// <summary>
        /// Get current memory usage statistics
        /// </summary>
        ResourceMemoryStats GetMemoryStatistics();

        /// <summary>
        /// Unload a specific resource by path
        /// </summary>
        void UnloadResource(string path);

        /// <summary>
        /// Unload all unused resources
        /// </summary>
        UniTask UnloadUnusedResourcesAsync();

        #endregion

        #region Resource Status

        /// <summary>
        /// Check if a resource is currently loaded
        /// </summary>
        bool IsResourceLoaded(string path);

        /// <summary>
        /// Check if a resource is currently being loaded
        /// </summary>
        bool IsResourceLoading(string path);

        /// <summary>
        /// Get a loaded resource without triggering a load operation
        /// </summary>
        Resource<T> GetLoadedResourceOrNull<T>(string path) where T : UnityEngine.Object;

        /// <summary>
        /// Get comprehensive resource service statistics
        /// </summary>
        ResourceServiceStatistics GetStatistics();

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a resource load starts
        /// </summary>
        event Action<string> ResourceLoadStarted;

        /// <summary>
        /// Event fired when a resource load completes successfully
        /// </summary>
        event Action<string, TimeSpan> ResourceLoadCompleted;

        /// <summary>
        /// Event fired when a resource load fails
        /// </summary>
        event Action<string, Exception> ResourceLoadFailed;

        /// <summary>
        /// Event fired when memory pressure changes
        /// </summary>
        event Action<float> MemoryPressureChanged;

        #endregion
    }
}
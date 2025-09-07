using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Common.Script;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Preloads resources required by script commands to optimize execution performance
    /// </summary>
    public class ResourcePreloader : IDisposable
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly ScriptExecutionContext _executionContext;
        private readonly Dictionary<Type, HashSet<string>> _preloadedResources = new Dictionary<Type, HashSet<string>>();
        private readonly Dictionary<string, UnityEngine.Object> _resourceCache = new Dictionary<string, UnityEngine.Object>();
        private readonly object _preloadLock = new object();
        private bool _isPreloading;
        private bool _disposed;
        #endregion

        #region Events
        public event Action<string, float> ResourcePreloaded;
        public event Action<int, float> BatchPreloadCompleted;
        #endregion

        #region Properties
        public bool IsPreloading => _isPreloading;
        public int CachedResourceCount => _resourceCache.Count;
        public int PreloadedTypeCount => _preloadedResources.Count;
        #endregion

        #region Constructor
        public ResourcePreloader(ScriptPlayerConfiguration config, ScriptExecutionContext executionContext)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Preload all resources required by the script
        /// </summary>
        public async UniTask PreloadScriptResourcesAsync(CancellationToken cancellationToken = default)
        {
            if (_isPreloading || _executionContext.Script?.Lines == null)
                return;

            _isPreloading = true;
            var startTime = DateTime.Now;

            try
            {
                // Analyze script for resource requirements
                var resourceRequirements = await AnalyzeResourceRequirementsAsync(cancellationToken);
                
                if (resourceRequirements.Count == 0)
                {
                    if (_config.LogExecutionFlow)
                        Debug.Log("[ResourcePreloader] No resources to preload");
                    return;
                }

                if (_config.LogExecutionFlow)
                    Debug.Log($"[ResourcePreloader] Starting preload of {resourceRequirements.Sum(kv => kv.Value.Count)} resources across {resourceRequirements.Count} types");

                // Preload resources by type for better organization
                await PreloadResourcesByTypeAsync(resourceRequirements, cancellationToken);

                var totalTime = (float)(DateTime.Now - startTime).TotalSeconds;
                BatchPreloadCompleted?.Invoke(_resourceCache.Count, totalTime);

                if (_config.LogExecutionFlow)
                    Debug.Log($"[ResourcePreloader] Completed preloading {_resourceCache.Count} resources in {totalTime:F2}s");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[ResourcePreloader] Resource preloading was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePreloader] Error during resource preloading: {ex.Message}");
            }
            finally
            {
                _isPreloading = false;
            }
        }

        /// <summary>
        /// Preload resources for a specific command
        /// </summary>
        public async UniTask<bool> PreloadCommandResourcesAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                return false;

            var commandType = command.GetType();
            var resourcePaths = ExtractResourcePaths(command);
            
            if (resourcePaths.Count == 0)
                return true;

            try
            {
                foreach (var resourcePath in resourcePaths)
                {
                    await PreloadSingleResourceAsync(resourcePath, commandType, cancellationToken);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourcePreloader] Failed to preload resources for command {commandType.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get a preloaded resource
        /// </summary>
        public T GetPreloadedResource<T>(string resourcePath) where T : UnityEngine.Object
        {
            lock (_preloadLock)
            {
                if (_resourceCache.TryGetValue(resourcePath, out var resource) && resource is T typedResource)
                {
                    return typedResource;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a resource is preloaded
        /// </summary>
        public bool IsResourcePreloaded(string resourcePath)
        {
            lock (_preloadLock)
            {
                return _resourceCache.ContainsKey(resourcePath);
            }
        }

        /// <summary>
        /// Clear all preloaded resources
        /// </summary>
        public void ClearPreloadedResources()
        {
            lock (_preloadLock)
            {
                // Unload addressable resources
                foreach (var kvp in _resourceCache)
                {
                    if (kvp.Value != null && !IsUnityBuiltinResource(kvp.Value))
                    {
                        // For addressables, we would call Addressables.Release here
                        // For now, just mark for GC
                        Resources.UnloadAsset(kvp.Value);
                    }
                }

                _resourceCache.Clear();
                _preloadedResources.Clear();
            }

            if (_config.LogExecutionFlow)
                Debug.Log("[ResourcePreloader] Cleared all preloaded resources");
        }

        /// <summary>
        /// Get preloading statistics
        /// </summary>
        public ResourcePreloadStats GetPreloadStats()
        {
            lock (_preloadLock)
            {
                return new ResourcePreloadStats
                {
                    TotalResources = _resourceCache.Count,
                    PreloadedTypes = _preloadedResources.Count,
                    MemoryUsageMB = CalculateMemoryUsage(),
                    IsPreloading = _isPreloading
                };
            }
        }
        #endregion

        #region Private Methods
        private async UniTask<Dictionary<Type, HashSet<string>>> AnalyzeResourceRequirementsAsync(CancellationToken cancellationToken)
        {
            var requirements = new Dictionary<Type, HashSet<string>>();
            
            if (_executionContext.Script?.Lines == null)
                return requirements;

            // Parse script lines to find commands and their resource requirements
            for (int i = 0; i < _executionContext.Script.Lines.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Yield periodically for long scripts to allow cancellation
                if (i % 50 == 0)
                    await UniTask.Yield(cancellationToken);

                var scriptLine = _executionContext.Script.Lines[i];
                
                // Handle different line types
                ICommand command = null;
                if (scriptLine is CommandScriptLine commandLine)
                {
                    // Command line - extract command directly
                    command = commandLine.Command;
                }
                else
                {
                    // Other line types - skip for resource analysis
                    continue;
                }

                if (command != null)
                {
                    var commandType = command.GetType();
                    var resourcePaths = ExtractResourcePaths(command);

                    if (resourcePaths.Count > 0)
                    {
                        if (!requirements.ContainsKey(commandType))
                            requirements[commandType] = new HashSet<string>();

                        foreach (var path in resourcePaths)
                            requirements[commandType].Add(path);
                    }
                }
            }

            return requirements;
        }

        private async UniTask PreloadResourcesByTypeAsync(Dictionary<Type, HashSet<string>> requirements, CancellationToken cancellationToken)
        {
            foreach (var typeRequirements in requirements)
            {
                var commandType = typeRequirements.Key;
                var resourcePaths = typeRequirements.Value;

                if (_config.LogExecutionFlow)
                    Debug.Log($"[ResourcePreloader] Preloading {resourcePaths.Count} resources for {commandType.Name}");

                // Preload resources for this command type
                var preloadTasks = resourcePaths.Select(path => PreloadSingleResourceAsync(path, commandType, cancellationToken));
                await UniTask.WhenAll(preloadTasks);

                lock (_preloadLock)
                {
                    _preloadedResources[commandType] = resourcePaths;
                }
            }
        }

        private async UniTask PreloadSingleResourceAsync(string resourcePath, Type commandType, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return;

            lock (_preloadLock)
            {
                if (_resourceCache.ContainsKey(resourcePath))
                    return; // Already preloaded
            }

            try
            {
                var startTime = DateTime.Now;
                UnityEngine.Object resource = null;

                // Determine resource type and load appropriately
                if (IsAddressableResource(resourcePath))
                {
                    resource = await LoadAddressableResourceAsync(resourcePath, cancellationToken);
                }
                else
                {
                    resource = await LoadUnityResourceAsync(resourcePath, cancellationToken);
                }

                if (resource != null)
                {
                    lock (_preloadLock)
                    {
                        _resourceCache[resourcePath] = resource;
                    }

                    var loadTime = (float)(DateTime.Now - startTime).TotalSeconds;
                    ResourcePreloaded?.Invoke(resourcePath, loadTime);

                    if (_config.LogExecutionFlow && loadTime > 0.1f)
                        Debug.Log($"[ResourcePreloader] Loaded {resourcePath} in {loadTime:F3}s");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourcePreloader] Failed to preload resource '{resourcePath}': {ex.Message}");
            }
        }


        private List<string> ExtractResourcePaths(ICommand command)
        {
            var resourcePaths = new List<string>();

            // Use reflection to find properties that might contain resource paths
            var properties = command.GetType().GetProperties();
            
            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(command);
                    
                    // Check for common resource path patterns
                    if (value is string stringValue && IsResourcePath(stringValue))
                    {
                        resourcePaths.Add(stringValue);
                    }
                    else if (value is string[] stringArray)
                    {
                        resourcePaths.AddRange(stringArray.Where(IsResourcePath));
                    }
                }
                catch
                {
                    // Skip properties that can't be accessed
                }
            }

            return resourcePaths;
        }

        private bool IsResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Check for common resource path patterns
            return path.StartsWith("Assets/") ||
                   path.StartsWith("Resources/") ||
                   path.Contains(".prefab") ||
                   path.Contains(".asset") ||
                   path.Contains(".png") ||
                   path.Contains(".jpg") ||
                   path.Contains(".ogg") ||
                   path.Contains(".wav") ||
                   path.Contains(".mp3");
        }

        private bool IsAddressableResource(string resourcePath)
        {
            // Simple heuristic - actual implementation would check addressables catalog
            return resourcePath.StartsWith("addressables/") || 
                   resourcePath.Contains("addressable");
        }

        private async UniTask<UnityEngine.Object> LoadAddressableResourceAsync(string resourcePath, CancellationToken cancellationToken)
        {
            // Placeholder for addressables loading
            // In actual implementation, would use:
            // var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(resourcePath);
            // return await handle.Task;
            
            await UniTask.Delay(10, cancellationToken: cancellationToken); // Simulate async loading
            return null;
        }

        private async UniTask<UnityEngine.Object> LoadUnityResourceAsync(string resourcePath, CancellationToken cancellationToken)
        {
            // Load from Resources folder
            await UniTask.Yield(cancellationToken);
            
            var cleanPath = resourcePath.Replace("Resources/", "").Replace(".asset", "").Replace(".prefab", "");
            return Resources.Load(cleanPath);
        }

        private bool IsUnityBuiltinResource(UnityEngine.Object resource)
        {
            if (resource == null)
                return false;

            // Check if it's a built-in Unity resource
            // In runtime, we can't use AssetDatabase, so use simple heuristics
            var name = resource.name;
            return string.IsNullOrEmpty(name) || name.StartsWith("Default") || name.StartsWith("Unity");
        }

        private float CalculateMemoryUsage()
        {
            // Rough estimation of memory usage
            float totalMemory = 0f;
            
            foreach (var resource in _resourceCache.Values)
            {
                if (resource != null)
                {
                    // Estimate based on resource type
                    totalMemory += EstimateResourceSize(resource);
                }
            }

            return totalMemory / (1024f * 1024f); // Convert to MB
        }

        private float EstimateResourceSize(UnityEngine.Object resource)
        {
            switch (resource)
            {
                case Texture2D texture:
                    return texture.width * texture.height * 4; // Assume RGBA32
                case AudioClip audio:
                    return audio.samples * audio.channels * 4; // Assume 32-bit float
                case Mesh mesh:
                    return mesh.vertices.Length * 12 + mesh.triangles.Length * 4; // Rough estimate
                case GameObject _:
                    return 1024; // Rough estimate for prefabs
                default:
                    return 256; // Default estimate
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the ResourcePreloader and clean up all resources and events
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Clear all preloaded resources
                ClearPreloadedResources();

                // Clear event handlers to prevent memory leaks - simple null assignment
                ResourcePreloaded = null;
                BatchPreloadCompleted = null;

                if (_config.LogExecutionFlow)
                    Debug.Log("[ResourcePreloader] Disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourcePreloader] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
        #endregion
    }

    /// <summary>
    /// Statistics for resource preloading performance
    /// </summary>
    public struct ResourcePreloadStats
    {
        public int TotalResources;
        public int PreloadedTypes;
        public float MemoryUsageMB;
        public bool IsPreloading;

        public override string ToString()
        {
            return $"Resources: {TotalResources}, Types: {PreloadedTypes}, Memory: {MemoryUsageMB:F1}MB, Active: {IsPreloading}";
        }
    }
}
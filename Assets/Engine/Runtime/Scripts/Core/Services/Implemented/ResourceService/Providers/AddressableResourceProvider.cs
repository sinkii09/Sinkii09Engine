using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Resource provider implementation for Unity Addressable Asset System
    /// </summary>
    public class AddressableResourceProvider : ResourceProvider
    {
        private readonly Dictionary<string, AsyncOperationHandle> _activeHandles = new Dictionary<string, AsyncOperationHandle>();
        private readonly object _handlesLock = new object();
        
        public AddressableResourceProvider()
        {
            // Initialize Addressables if needed
            InitializeAddressables().Forget();
        }

        /// <summary>
        /// Initialize the Addressable system
        /// </summary>
        private async UniTaskVoid InitializeAddressables()
        {
            try
            {
                var initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask();
                Debug.Log("AddressableResourceProvider: Addressables system initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddressableResourceProvider: Failed to initialize Addressables: {ex.Message}");
            }
        }

        public override bool SupportsType<T>()
        {
            // Addressables can load any Unity Object type
            return typeof(UnityEngine.Object).IsAssignableFrom(typeof(T));
        }

        protected override LoadResourceRunner<T> CreateLoadResourceRunner<T>(string path)
        {
            return new AddressableLoadResourceRunner<T>(this, path, LogMessage);
        }

        protected override LocateResourceRunner<T> CreateLocateResourceRunner<T>(string path)
        {
            return new AddressableLocateResourceRunner<T>(this, path, LogMessage);
        }

        protected override LocateFolderRunner CreateLocateFolderRunner(string path)
        {
            return new AddressableFolderLocator(this, path, LogMessage);
        }

        protected override void DisposeResource(Resource resource)
        {
            if (resource == null || !resource.IsValid) return;

            Debug.Log($"AddressableResourceProvider: Disposing resource: {resource.Path}");

            // Release the Addressable handle if it exists
            lock (_handlesLock)
            {
                if (_activeHandles.TryGetValue(resource.Path, out var handle))
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                    _activeHandles.Remove(resource.Path);
                }
            }
        }

        /// <summary>
        /// Store an active handle for a loaded resource
        /// </summary>
        public void StoreHandle(string path, AsyncOperationHandle handle)
        {
            lock (_handlesLock)
            {
                if (_activeHandles.ContainsKey(path))
                {
                    // Release old handle if exists
                    if (_activeHandles[path].IsValid())
                    {
                        Addressables.Release(_activeHandles[path]);
                    }
                }
                _activeHandles[path] = handle;
            }
        }

        /// <summary>
        /// Get an active handle for a resource
        /// </summary>
        public AsyncOperationHandle GetHandle(string path)
        {
            lock (_handlesLock)
            {
                return _activeHandles.TryGetValue(path, out var handle) ? handle : default;
            }
        }

        public override void UnloadResources()
        {
            // Release all active Addressable handles
            lock (_handlesLock)
            {
                foreach (var kvp in _activeHandles)
                {
                    if (kvp.Value.IsValid())
                    {
                        try
                        {
                            Addressables.Release(kvp.Value);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"AddressableResourceProvider: Error releasing handle for {kvp.Key}: {ex.Message}");
                        }
                    }
                }
                _activeHandles.Clear();
            }

            // Call base implementation
            base.UnloadResources();
        }

        protected override void CancelResourceLoading(string path)
        {
            Debug.Log($"AddressableResourceProvider: Canceling resource loading for path: {path}");
            
            // Cancel any active Addressable operation
            lock (_handlesLock)
            {
                if (_activeHandles.TryGetValue(path, out var handle))
                {
                    if (handle.IsValid() && !handle.IsDone)
                    {
                        // Note: Addressables doesn't support true cancellation, 
                        // but we can release the handle to prevent memory leaks
                        Addressables.Release(handle);
                    }
                    _activeHandles.Remove(path);
                }
            }

            base.CancelResourceLoading(path);
        }

        /// <summary>
        /// Check if an address exists in the Addressable system
        /// </summary>
        public async UniTask<bool> AddressExistsAsync(string address)
        {
            try
            {
                var locations = await Addressables.LoadResourceLocationsAsync(address).ToUniTask();
                return locations != null && locations.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all resource locations matching a label
        /// </summary>
        public async UniTask<IList<IResourceLocation>> GetLocationsByLabelAsync(string label)
        {
            try
            {
                return await Addressables.LoadResourceLocationsAsync(label, typeof(UnityEngine.Object)).ToUniTask();
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddressableResourceProvider: Failed to load locations for label {label}: {ex.Message}");
                return new List<IResourceLocation>();
            }
        }

        /// <summary>
        /// Load multiple resources by Addressable label
        /// </summary>
        public async UniTask<IEnumerable<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            try
            {
                var handle = Addressables.LoadAssetsAsync<T>(label, null);
                var assets = await handle.ToUniTask();
                
                // Store handle for cleanup
                lock (_handlesLock)
                {
                    _activeHandles[$"label_{label}"] = handle;
                }
                
                return assets;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddressableResourceProvider: Failed to load assets for label {label}: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Load a single resource by Addressable label (first match)
        /// </summary>
        public async UniTask<T> LoadAssetByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            try
            {
                var assets = await LoadAssetsByLabelAsync<T>(label);
                return assets.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogError($"AddressableResourceProvider: Failed to load asset for label {label}: {ex.Message}");
                return null;
            }
        }
    }
}
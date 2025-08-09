using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Resources;
using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Optimized resource runner for loading assets via Unity Addressable system
    /// </summary>
    public class AddressableLoadResourceRunner<T> : LoadResourceRunner<T> where T : UnityEngine.Object
    {
        private readonly Action<string> _logAction;
        private readonly AddressableResourceProvider _addressableProvider;
        private AsyncOperationHandle<T> _loadHandle;
        private bool _isCancelled = false;

        // Static validation cache to avoid repeated address checks
        private static readonly ConcurrentDictionary<string, bool> _addressValidationCache = new();
        private static readonly ConcurrentDictionary<string, DateTime> _validationTimestamps = new();
        private static readonly TimeSpan ValidationCacheExpiry = TimeSpan.FromMinutes(2);

        public AddressableLoadResourceRunner(
            IResourceProvider provider, 
            string path, 
            Action<string> logAction) : base(provider, path)
        {
            _logAction = logAction;
            _addressableProvider = provider as AddressableResourceProvider;
        }

        public override async UniTask RunAsync()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logAction?.Invoke($"[Addressable] Loading resource '{Path}' of type '{typeof(T).Name}'...");

                // Check if already cancelled
                if (_isCancelled)
                {
                    SetResult(new Resource<T>(Path, null, Provider));
                    return;
                }

                // Quick validation check before expensive load operation
                if (!await IsAddressValidAsync(Path))
                {
                    var invalidResult = new Resource<T>(Path, null, Provider);
                    SetResult(invalidResult);
                    _logAction?.Invoke($"[Addressable] Address validation failed for '{Path}'");
                    return;
                }

                // Load the asset using Addressables
                _loadHandle = Addressables.LoadAssetAsync<T>(Path);
                
                // Store the handle in the provider immediately after creation
                _addressableProvider?.StoreHandle(Path, _loadHandle);

                // Wait for the operation to complete with cancellation support
                var asset = await _loadHandle.ToUniTask();

                // Check if cancelled during loading
                if (_isCancelled)
                {
                    await SafeReleaseHandle();
                    SetResult(new Resource<T>(Path, null, Provider));
                    return;
                }

                // Validate the loaded asset
                if (asset == null)
                {
                    _logAction?.Invoke($"[Addressable] Loaded asset is null for '{Path}'");
                    SetResult(new Resource<T>(Path, null, Provider));
                    return;
                }

                // Create the resource result
                var result = new Resource<T>(Path, asset, Provider);
                SetResult(result);

                var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logAction?.Invoke($"[Addressable] Loaded resource '{Path}' of type '{typeof(T).Name}' in {loadTime:0.##}ms.");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Failed to load resource '{Path}': {ex.Message}");
                
                // Clean up handle on failure
                await SafeReleaseHandle();
                
                // Return invalid resource instead of throwing
                SetResult(new Resource<T>(Path, null, Provider));
                
                // Don't re-throw - let the ResourceService handle the error gracefully
            }
        }

        /// <summary>
        /// Fast address validation with caching
        /// </summary>
        private async UniTask<bool> IsAddressValidAsync(string address)
        {
            var cacheKey = $"{address}:{typeof(T).Name}";
            
            // Check cache first
            if (_addressValidationCache.TryGetValue(cacheKey, out var cachedResult) &&
                _validationTimestamps.TryGetValue(cacheKey, out var timestamp) &&
                DateTime.UtcNow - timestamp < ValidationCacheExpiry)
            {
                return cachedResult;
            }

            try
            {
                // Quick validation using LoadResourceLocationsAsync (much faster than LoadAssetAsync)
                var locations = await Addressables.LoadResourceLocationsAsync(address, typeof(T)).ToUniTask();
                var isValid = locations != null && locations.Count > 0;
                
                // Cache the result
                _addressValidationCache[cacheKey] = isValid;
                _validationTimestamps[cacheKey] = DateTime.UtcNow;
                
                return isValid;
            }
            catch
            {
                // Cache negative result to avoid repeated failed validations
                _addressValidationCache[cacheKey] = false;
                _validationTimestamps[cacheKey] = DateTime.UtcNow;
                return false;
            }
        }

        /// <summary>
        /// Safely release the handle with error handling
        /// </summary>
        private async UniTask SafeReleaseHandle()
        {
            if (!_loadHandle.IsValid()) return;

            try
            {
                // Ensure we're on the main thread for Addressables operations
                await UniTask.SwitchToMainThread();
                Addressables.Release(_loadHandle);
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Error releasing handle for '{Path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Clear validation cache (useful for testing or when catalog changes)
        /// </summary>
        public static void ClearValidationCache()
        {
            _addressValidationCache.Clear();
            _validationTimestamps.Clear();
        }

        public override void Cancel()
        {
            _isCancelled = true;
            
            // Async cancellation cleanup
            if (_loadHandle.IsValid())
            {
                _logAction?.Invoke($"[Addressable] Cancelling load operation for '{Path}'");
                SafeReleaseHandle().Forget(); // Fire and forget cleanup
            }
            
            base.Cancel();
        }
    }
}
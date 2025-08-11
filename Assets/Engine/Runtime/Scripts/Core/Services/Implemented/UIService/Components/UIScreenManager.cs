using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component responsible for screen lifecycle management (loading, instantiation, injection)
    /// </summary>
    public class UIScreenManager : IUIComponent
    {
        private IUIServiceContext _context;
        private readonly Dictionary<UIScreenType, UniTask<UIScreen>> _loadingTasks;
        private bool _isInitialized;

        public string ComponentName => "ScreenManager";
        public bool IsInitialized => _isInitialized;

        public UIScreenManager()
        {
            _loadingTasks = new Dictionary<UIScreenType, UniTask<UIScreen>>();
        }

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _loadingTasks.Clear();
            _isInitialized = false;
            await UniTask.CompletedTask;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "ScreenManager not initialized");

            if (_context?.UIRoot == null)
                return new ComponentHealthStatus(false, "UI Root is null");

            return new ComponentHealthStatus(true, $"ScreenManager healthy - {_loadingTasks.Count} active loads");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            // Screen manager doesn't hold cached resources directly
            // Pressure response is handled by cache and pool components
        }

        /// <summary>
        /// Load a screen with concurrency protection and proper caching/pooling strategy
        /// </summary>
        public async UniTask<UIScreen> LoadScreenAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || screenAsset == null)
                throw new InvalidOperationException("ScreenManager not initialized or screenAsset is null");

            // Multi-instance screens: try pool first, then create new
            if (screenAsset.AllowMultipleInstances)
            {
                return await LoadMultiInstanceScreenAsync(screenAsset, cancellationToken);
            }
            
            // Single-instance screens: try cache first, then load with concurrency protection
            return await LoadSingleInstanceScreenAsync(screenAsset, cancellationToken);
        }

        private async UniTask<UIScreen> LoadMultiInstanceScreenAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken)
        {
            // Try instance pool first
            var poolComponent = _context.GetComponent<UIInstancePoolManager>();
            var pooledScreen = poolComponent?.GetPooledInstance(screenAsset.ScreenType);
            
            if (pooledScreen != null)
            {
                Debug.Log($"[ScreenManager] Reused {screenAsset.ScreenType} from instance pool");
                return pooledScreen;
            }

            // Create new instance
            return await CreateNewScreenInstanceAsync(screenAsset, cancellationToken);
        }

        private async UniTask<UIScreen> LoadSingleInstanceScreenAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken)
        {
            // Try cache first
            var cacheComponent = _context.GetComponent<UIScreenCacheManager>();
            if (screenAsset.CacheScreen && cacheComponent != null && cacheComponent.TryGet(screenAsset.ScreenType, out var cachedScreen))
            {
                Debug.Log($"[ScreenManager] Reused {screenAsset.ScreenType} from cache");
                return cachedScreen;
            }

            // Check for concurrent loading
            if (_loadingTasks.TryGetValue(screenAsset.ScreenType, out var existingTask))
            {
                Debug.Log($"[ScreenManager] Waiting for existing {screenAsset.ScreenType} load");
                return await existingTask;
            }

            // Start new loading task with concurrency protection
            var loadingTask = CreateNewScreenInstanceAsync(screenAsset, cancellationToken);
            _loadingTasks[screenAsset.ScreenType] = loadingTask;

            try
            {
                var screen = await loadingTask;
                
                // Cache if configured
                if (screenAsset.CacheScreen && cacheComponent != null)
                {
                    cacheComponent.Put(screenAsset.ScreenType, screen);
                    Debug.Log($"[ScreenManager] Cached new {screenAsset.ScreenType} instance");
                }

                return screen;
            }
            finally
            {
                _loadingTasks.Remove(screenAsset.ScreenType);
            }
        }

        private async UniTask<UIScreen> CreateNewScreenInstanceAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken)
        {
            // Load prefab via ResourceService or Addressables
            GameObject prefab = null;

            if (_context.ResourceService != null)
            {
                try
                {
                    var resourceResult = await _context.ResourceService.LoadResourceAsync<GameObject>(
                        screenAsset.AddressableReference.AssetGUID, cancellationToken);
                    prefab = resourceResult.Asset;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ScreenManager] Failed to load via ResourceService: {ex.Message}. Falling back to Addressables.");
                }
            }

            // Fallback to direct Addressables loading
            if (prefab == null)
            {
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(screenAsset.AddressableReference);
                prefab = await handle.ToUniTask(cancellationToken: cancellationToken);
            }

            if (prefab == null)
                throw new InvalidOperationException($"Failed to load screen prefab for {screenAsset.ScreenType}");

            // Instantiate and configure
            var screenInstance = UnityEngine.Object.Instantiate(prefab, _context.UIRoot.transform);
            var screen = screenInstance.GetComponent<UIScreen>();

            if (screen == null)
                throw new InvalidOperationException($"Screen prefab for {screenAsset.ScreenType} does not have UIScreen component");

            // Ensure screen starts completely hidden (for proper blur backdrop capture)
            screenInstance.SetActive(false);
            
            // Inject dependencies
            screen.ScreenAsset = screenAsset;

            // Register with pool if multi-instance
            if (screenAsset.AllowMultipleInstances)
            {
                var poolComponent = _context.GetComponent<UIInstancePoolManager>();
                poolComponent?.RegisterNewInstance(screenAsset.ScreenType, screen);
                Debug.Log($"[ScreenManager] Registered new {screenAsset.ScreenType} instance with pool");
            }

            return screen;
        }

        public void Dispose()
        {
            _loadingTasks?.Clear();
            _isInitialized = false;
        }
    }
}
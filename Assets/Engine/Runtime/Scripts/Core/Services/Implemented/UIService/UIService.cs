using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using R3;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component-based UIService for better maintainability and efficiency.
    /// This version delegates responsibilities to specialized component managers.
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.High, 
        Description = "Component-based UI service with improved architecture and separation of concerns",
        RequiredServices = new[] { typeof(IResourceService) })]
    [ServiceConfiguration(typeof(UIServiceConfiguration))]
    public class UIService : IUIService, IUIServiceContext
    {
        private readonly UIServiceConfiguration _config;
        private readonly IResourceService _resourceService;
        private readonly UIScreenRegistry _screenRegistry;
        private readonly Dictionary<Type, IUIComponent> _components;
        private readonly DisposableBag _disposables;

        private Canvas _uiRoot;
        private bool _isInitialized;

        #region IUIServiceContext Implementation
        public UIServiceConfiguration Configuration => _config;
        public IResourceService ResourceService => _resourceService;
        public UIScreenRegistry ScreenRegistry => _screenRegistry;
        public Canvas UIRoot => _uiRoot;

        public T GetComponent<T>() where T : class, IUIComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? component as T : null;
        }

        public bool HasComponent<T>() where T : class, IUIComponent
        {
            return _components.TryGetValue(typeof(T), out var component) && component.IsInitialized;
        }
        #endregion

        #region Reactive Events (delegated to NavigationManager)
        public Observable<UIScreenAsset> ScreenShown => GetComponent<UINavigationManager>()?.ScreenShown ?? Observable.Empty<UIScreenAsset>();
        public Observable<UIScreenAsset> ScreenHidden => GetComponent<UINavigationManager>()?.ScreenHidden ?? Observable.Empty<UIScreenAsset>();
        #endregion

        public UIService(UIServiceConfiguration config, IResourceService resourceService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _screenRegistry = _config.ScreenRegistry ?? throw new ArgumentNullException(nameof(_config.ScreenRegistry));
            
            _components = new Dictionary<Type, IUIComponent>();
            _disposables = new DisposableBag();
        }

        #region IEngineService Implementation
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken)
        {
            try
            {
                // Initialize UI Root
                await CreateUIRootAsync(cancellationToken);
                
                // Initialize all components in dependency order
                await InitializeComponentsAsync(cancellationToken);
                
                // Initialize screen registry
                _screenRegistry.Initialize();
                
                // Setup cleanup helpers
                SetupCleanupHandlers();
                
                _isInitialized = true;
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed(ex);
            }
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Shutdown components in reverse order
                await ShutdownComponentsAsync(cancellationToken);
                
                // Cleanup Unity events and DOTween
                CleanupHandlers();
                
                // Destroy UI root
                if (_uiRoot != null)
                {
                    UnityEngine.Object.Destroy(_uiRoot.gameObject);
                }
                
                // Dispose disposables
                _disposables.Dispose();
                
                _isInitialized = false;
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed(ex);
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return ServiceHealthStatus.Unhealthy("UIService not initialized");
            
            if (_uiRoot == null)
                return ServiceHealthStatus.Unhealthy("UI root is null");

            // Check component health
            var unhealthyComponents = new List<string>();
            foreach (var kvp in _components)
            {
                var health = await kvp.Value.HealthCheckAsync(cancellationToken);
                if (!health.IsHealthy)
                {
                    unhealthyComponents.Add($"{kvp.Value.ComponentName}: {health.Message}");
                }
            }

            if (unhealthyComponents.Count > 0)
                return ServiceHealthStatus.Unhealthy($"Unhealthy components: {string.Join(", ", unhealthyComponents)}");

            return ServiceHealthStatus.Healthy($"UIService healthy - {_components.Count} components initialized");
        }
        #endregion

        #region Component Initialization
        private async UniTask InitializeComponentsAsync(CancellationToken cancellationToken)
        {
            // Initialize components in dependency order
            var components = new List<IUIComponent>
            {
                new UIScreenCacheManager(),
                new UIInstancePoolManager(), 
                new UIModalManager(),
                new UITransitionManagerComponent(_config.TransitionConfiguration ?? CreateDefaultTransitionConfiguration()),
                new UIScreenManager(),
                new UINavigationManager(), // Depends on all others
            };

            foreach (var component in components)
            {
                var success = await component.InitializeAsync(this, cancellationToken);
                if (success)
                {
                    _components[component.GetType()] = component;
                    Debug.Log($"[UIService] Initialized component: {component.ComponentName}");
                }
                else
                {
                    throw new InvalidOperationException($"Failed to initialize component: {component.ComponentName}");
                }
            }
        }

        private async UniTask ShutdownComponentsAsync(CancellationToken cancellationToken)
        {
            // Shutdown in reverse order (reverse dependency order)
            var componentList = new List<IUIComponent>(_components.Values);
            componentList.Reverse();

            foreach (var component in componentList)
            {
                try
                {
                    await component.ShutdownAsync(cancellationToken);
                    component.Dispose();
                    Debug.Log($"[UIService] Shutdown component: {component.ComponentName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UIService] Error shutting down component {component.ComponentName}: {ex.Message}");
                }
            }
            
            _components.Clear();
        }
        #endregion

        #region UI Root Creation
        private async UniTask CreateUIRootAsync(CancellationToken cancellationToken)
        {
            GameObject rootGameObject;
            
            if (_config.UIRootPrefab != null)
            {
                rootGameObject = UnityEngine.Object.Instantiate(_config.UIRootPrefab);
                rootGameObject.name = "UIRoot";
                _uiRoot = rootGameObject.GetComponent<Canvas>();
            }
            else
            {
                rootGameObject = new GameObject("UIRoot");
                _uiRoot = rootGameObject.AddComponent<Canvas>();
                _uiRoot.renderMode = RenderMode.ScreenSpaceOverlay;
                _uiRoot.sortingOrder = 0;
                
                var scaler = rootGameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                
                rootGameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            UnityEngine.Object.DontDestroyOnLoad(rootGameObject);
            
            
            await UniTask.CompletedTask;
        }
        
        private UITransitionConfiguration CreateDefaultTransitionConfiguration()
        {
            var defaultConfig = ScriptableObject.CreateInstance<UITransitionConfiguration>();
            defaultConfig.ResetToDefaults();
            Debug.Log("UIService: Created default transition configuration");
            return defaultConfig;
        }
        #endregion

        #region IUIService API Implementation (Delegated to Components)

        // All IUIService methods delegate to the appropriate component managers
        public async UniTask ShowAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
            => await ShowAsync(screenType, null, UIDisplayConfig.Normal, cancellationToken);

        public async UniTask ShowAsync(UIScreenType screenType, UIScreenContext context, CancellationToken cancellationToken = default)
            => await ShowAsync(screenType, context, UIDisplayConfig.Normal, cancellationToken);

        public async UniTask ShowAsync(UIScreenType screenType, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default)
        {
            var screenAsset = _screenRegistry.GetAsset(screenType);
            if (screenAsset == null)
                throw new InvalidOperationException($"No screen asset registered for type: {screenType}");

            var navManager = GetComponent<UINavigationManager>();
            await navManager.ShowScreenAsync(screenAsset, context, displayConfig, cancellationToken);
        }

        public async UniTask HideAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.HideScreenAsync(screenType, cancellationToken);
        }

        public async UniTask HideAllInstancesAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.HideAllInstancesAsync(screenType, cancellationToken);
        }

        public async UniTask HideTopInstanceAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            var screenAsset = _screenRegistry.GetAsset(screenType);
            if (screenAsset == null) return;

            var navManager = GetComponent<UINavigationManager>();
            if (screenAsset.AllowMultipleInstances)
            {
                // For multi-instance screens, hide the topmost one
                // This would need stack modification to get just the top instance
                await navManager.HideScreenAsync(screenType, cancellationToken);
            }
            else
            {
                await navManager.HideScreenAsync(screenType, cancellationToken);
            }
        }

        public UIScreen GetActiveScreen()
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.CurrentScreen;
        }

        public UIScreenType GetActiveScreenType()
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.GetActiveScreenType() ?? UIScreenType.None;
        }

        public bool IsScreenActive(UIScreenType screenType)
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.IsScreenActive(screenType) ?? false;
        }

        public bool IsScreenActive(UIScreenAsset screenAsset)
            => screenAsset != null && IsScreenActive(screenAsset.ScreenType);

        public async UniTask PopAsync(CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.PopAsync(cancellationToken);
        }

        public async UniTask PopToAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.PopToAsync(screenType, cancellationToken);
        }

        public async UniTask ReplaceAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
            => await ReplaceAsync(screenType, null, UIDisplayConfig.Normal, cancellationToken);

        public async UniTask ReplaceAsync(UIScreenType screenType, UIScreenContext context, CancellationToken cancellationToken = default)
            => await ReplaceAsync(screenType, context, UIDisplayConfig.Normal, cancellationToken);

        public async UniTask ReplaceAsync(UIScreenType screenType, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default)
        {
            var screenAsset = _screenRegistry.GetAsset(screenType);
            if (screenAsset == null)
                throw new InvalidOperationException($"No screen asset registered for type: {screenType}");
            
            var navManager = GetComponent<UINavigationManager>();
            await navManager.ReplaceAsync(screenAsset, context, displayConfig, cancellationToken);
        }

        public async UniTask ReplaceAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.ReplaceAsync(screenAsset, null, UIDisplayConfig.Normal, cancellationToken);
        }

        public async UniTask ClearAsync(CancellationToken cancellationToken = default)
        {
            var navManager = GetComponent<UINavigationManager>();
            await navManager.ShutdownAsync(cancellationToken);
            await navManager.InitializeAsync(this, cancellationToken);
        }

        public IUINavigationBuilder Navigate()
        {
            return new UINavigationBuilder(this);
        }

        public System.Collections.Generic.IReadOnlyList<string> GetNavigationBreadcrumbs()
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.GetNavigationBreadcrumbs() ?? new List<string>();
        }

        public int GetStackDepth()
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.StackDepth ?? 0;
        }

        public int GetMaxStackDepth()
        {
            var navManager = GetComponent<UINavigationManager>();
            return navManager?.MaxStackDepth ?? 0;
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            Debug.Log($"[UIService] Responding to memory pressure: {pressureLevel:P1}");
            
            foreach (var component in _components.Values)
            {
                component.RespondToMemoryPressure(pressureLevel);
            }
        }

        public (int cachedScreens, int totalPooledInstances, int totalActiveInstances, double cacheEfficiency, double poolEfficiency) GetMemoryStats()
        {
            var cacheManager = GetComponent<UIScreenCacheManager>();
            var poolManager = GetComponent<UIInstancePoolManager>();
            
            var cacheCount = cacheManager?.Count ?? 0;
            var cacheEfficiency = cacheManager?.HitRatio ?? 0.0;
            
            var (totalAvailable, totalActive, _, poolEfficiency) = poolManager?.GetOverallStats() ?? (0, 0, 0, 0.0);
            
            return (cacheCount, totalAvailable, totalActive, cacheEfficiency, poolEfficiency);
        }

        public (int available, int active, int totalCreated, double efficiency) GetPoolStats(UIScreenType screenType)
        {
            var poolManager = GetComponent<UIInstancePoolManager>();
            return poolManager?.GetStats(screenType) ?? (0, 0, 0, 0.0);
        }

        #endregion

        #region Cleanup Handlers
        private void SetupCleanupHandlers()
        {
            // Let DOTweenCleanupHelper handle global cleanup to avoid conflicts
            DOTweenCleanupHelper.EnsureCleanupHelper();
        }

        private void CleanupHandlers()
        {
            // Cleanup UI-specific DOTween animations only
            CleanupUIDOTween();
        }

        private static void CleanupUIDOTween()
        {
            try
            {
                // Only kill UI-related tweens to avoid conflicts with other systems
                // Let DOTweenCleanupHelper handle global cleanup
                Debug.Log("[UIService] UI DOTween cleanup delegated to DOTweenCleanupHelper");
                DOTweenCleanupHelper.TriggerCleanup("UIService Shutdown");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIService] UI DOTween cleanup error: {ex.Message}");
            }
        }
        #endregion
    }
}
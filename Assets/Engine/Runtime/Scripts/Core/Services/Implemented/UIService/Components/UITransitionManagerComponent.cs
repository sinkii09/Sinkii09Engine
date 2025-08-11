using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component wrapper for UITransitionManager to fit into the component architecture
    /// </summary>
    public class UITransitionManagerComponent : IUIComponent
    {
        private IUIServiceContext _context;
        private UITransitionManager _transitionManager;
        private bool _isInitialized;

        public string ComponentName => "TransitionManagerComponent";
        public bool IsInitialized => _isInitialized;

        private readonly UITransitionConfiguration _config;

        public UITransitionManagerComponent(UITransitionConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _transitionManager = new UITransitionManager(_config);
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _transitionManager?.Dispose();
            _transitionManager = null;
            _isInitialized = false;
            await UniTask.CompletedTask;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "TransitionManager not initialized");

            if (_transitionManager == null)
                return new ComponentHealthStatus(false, "TransitionManager is null");

            var activeTransitions = _transitionManager.ActiveTransitionCount;
            return new ComponentHealthStatus(true, $"TransitionManager healthy - {activeTransitions} active transitions");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            // Transition manager doesn't hold cached resources
            // Memory pressure is handled by individual transitions
        }

        // Delegate all transition methods to the internal manager
        public async UniTask AnimateScreenInAsync(UIScreen screen, TransitionType transitionType, CancellationToken cancellationToken = default)
        {
            if (_transitionManager != null)
            {
                await _transitionManager.AnimateScreenInAsync(screen, transitionType, cancellationToken);
            }
        }

        public async UniTask AnimateScreenOutAsync(UIScreen screen, TransitionType transitionType, CancellationToken cancellationToken = default)
        {
            if (_transitionManager != null)
            {
                await _transitionManager.AnimateScreenOutAsync(screen, transitionType, cancellationToken);
            }
        }

        public async UniTask AnimateScreenReplaceAsync(UIScreen newScreen, UIScreen oldScreen, TransitionType transitionType, CancellationToken cancellationToken = default)
        {
            if (_transitionManager != null)
            {
                await _transitionManager.AnimateScreenReplaceAsync(newScreen, oldScreen, transitionType, cancellationToken);
            }
        }

        public void StopTransition(UIScreen screen)
        {
            _transitionManager?.StopTransition(screen);
        }

        public bool IsTransitioning(UIScreen screen)
        {
            return _transitionManager?.IsTransitioning(screen) ?? false;
        }

        public void Dispose()
        {
            _transitionManager?.Dispose();
            _isInitialized = false;
        }
    }
}
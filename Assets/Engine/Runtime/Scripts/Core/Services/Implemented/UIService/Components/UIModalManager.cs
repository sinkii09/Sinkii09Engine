using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component responsible for managing modal backdrops and overlay behavior
    /// </summary>
    public class UIModalManager : IUIComponent
    {
        private IUIServiceContext _context;
        private readonly Dictionary<UIScreen, UIModalBackdrop> _modalBackdrops;
        private UIModalBackdropPool _backdropPool;
        private bool _isInitialized;

        public string ComponentName => "ModalManager";
        public bool IsInitialized => _isInitialized;

        public int ActiveModalCount => _modalBackdrops?.Count ?? 0;

        public UIModalManager()
        {
            _modalBackdrops = new Dictionary<UIScreen, UIModalBackdrop>();
        }

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _backdropPool = new UIModalBackdropPool(_context.UIRoot.transform, maxPoolSize: 5, enablePooling: true);
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            // Return all backdrops to pool
            foreach (var backdrop in _modalBackdrops.Values)
            {
                if (backdrop != null)
                {
                    _backdropPool.ReturnBackdrop(backdrop);
                }
            }
            _modalBackdrops.Clear();

            _backdropPool?.Dispose();
            _isInitialized = false;
            await UniTask.CompletedTask;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "ModalManager not initialized");

            if (_backdropPool == null)
                return new ComponentHealthStatus(false, "Backdrop pool is null");

            return new ComponentHealthStatus(true, 
                $"ModalManager healthy - {_modalBackdrops.Count} active modals, pool efficiency: {_backdropPool.PoolEfficiency:P1}");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            _backdropPool?.RespondToMemoryPressure(pressureLevel);
        }

        /// <summary>
        /// Create modal backdrop for a screen
        /// </summary>
        public async UniTask CreateModalBackdropAsync(UIScreen screen, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || displayConfig?.IsModal != true || screen == null)
                return;

            // Get backdrop from pool
            var backdrop = _backdropPool.GetBackdrop();
            backdrop.gameObject.name = $"{screen.gameObject.name}_Backdrop";

            // Initialize backdrop with configuration
            backdrop.Initialize(displayConfig, screen, () => OnModalBackdropClicked(screen));

            // Store backdrop reference
            _modalBackdrops[screen] = backdrop;

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Show modal backdrop for a screen
        /// </summary>
        public void ShowModalBackdrop(UIScreen screen)
        {
            if (_modalBackdrops.TryGetValue(screen, out var backdrop))
            {
                backdrop.Show();
            }
        }

        /// <summary>
        /// Hide modal backdrop for a screen
        /// </summary>
        public void HideModalBackdrop(UIScreen screen)
        {
            if (_modalBackdrops.TryGetValue(screen, out var backdrop))
            {
                backdrop.Hide();
                _modalBackdrops.Remove(screen);
                _backdropPool.ReturnBackdrop(backdrop);
            }
        }

        /// <summary>
        /// Cleanup failed screen creation
        /// </summary>
        public void CleanupFailedScreen(UIScreen screen)
        {
            if (_modalBackdrops.TryGetValue(screen, out var backdrop))
            {
                _modalBackdrops.Remove(screen);
                _backdropPool.ReturnBackdrop(backdrop);
            }
        }

        /// <summary>
        /// Get backdrop pool statistics
        /// </summary>
        public (int available, int active, double efficiency) GetBackdropPoolStats()
        {
            if (_backdropPool == null)
                return (0, 0, 0.0);

            return (_backdropPool.AvailableCount, _backdropPool.ActiveCount, _backdropPool.PoolEfficiency);
        }

        private void OnModalBackdropClicked(UIScreen screen)
        {
            if (screen?.IsActive != true)
                return;

            if (screen.DisplayConfig?.CloseOnBackdropClick == true)
            {
                // Request navigation manager to hide this screen
                var navManager = _context.GetComponent<UINavigationManager>();
                _ = navManager?.HideScreenAsync(screen.ScreenAsset.ScreenType);
            }
        }

        public void Dispose()
        {
            foreach (var backdrop in _modalBackdrops.Values)
            {
                if (backdrop != null)
                {
                    _backdropPool?.ReturnBackdrop(backdrop);
                }
            }
            _modalBackdrops?.Clear();
            _backdropPool?.Dispose();
            _isInitialized = false;
        }
    }
}
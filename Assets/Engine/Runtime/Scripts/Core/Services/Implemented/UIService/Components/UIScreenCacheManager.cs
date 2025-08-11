using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component responsible for caching single-instance screens
    /// </summary>
    public class UIScreenCacheManager : IUIComponent
    {
        private IUIServiceContext _context;
        private UIScreenCache _cache;
        private bool _isInitialized;

        public string ComponentName => "ScreenCacheManager";
        public bool IsInitialized => _isInitialized;

        public int Count => _cache?.Count ?? 0;
        public double HitRatio => _cache?.HitRatio ?? 0.0;

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
            _cache = new UIScreenCache(_context.Configuration.MaxCachedScreens, _context.Configuration.EnableScreenCaching);
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _cache?.Dispose();
            _isInitialized = false;
            await UniTask.CompletedTask;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "ScreenCacheManager not initialized");

            if (_cache == null)
                return new ComponentHealthStatus(false, "Screen cache is null");

            return new ComponentHealthStatus(true, $"ScreenCacheManager healthy - {_cache.Count} cached screens, {_cache.HitRatio:P1} hit ratio");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            _cache?.RespondToMemoryPressure(pressureLevel);
        }

        public bool TryGet(UIScreenType screenType, out UIScreen screen)
        {
            screen = null;
            return _cache?.TryGet(screenType, out screen) ?? false;
        }

        public void Put(UIScreenType screenType, UIScreen screen)
        {
            _cache?.Put(screenType, screen);
        }

        public bool Remove(UIScreenType screenType)
        {
            return _cache?.Remove(screenType) ?? false;
        }

        public void Clear()
        {
            _cache?.Clear();
        }

        public void Dispose()
        {
            _cache?.Dispose();
            _isInitialized = false;
        }
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component responsible for pooling multiple-instance screens
    /// </summary>
    public class UIInstancePoolManager : IUIComponent
    {
        private IUIServiceContext _context;
        private UIScreenInstancePool _pool;
        private bool _isInitialized;

        public string ComponentName => "InstancePoolManager";
        public bool IsInitialized => _isInitialized;

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
            _pool = new UIScreenInstancePool(
                _context.UIRoot.transform, 
                _context.Configuration.MaxInstancePoolSizePerType, 
                _context.Configuration.EnableInstancePooling);
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            _pool?.Dispose();
            _isInitialized = false;
            await UniTask.CompletedTask;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "InstancePoolManager not initialized");

            if (_pool == null)
                return new ComponentHealthStatus(false, "Instance pool is null");

            var (totalAvailable, totalActive, totalCreated, efficiency) = _pool.GetOverallStats();
            return new ComponentHealthStatus(true, 
                $"InstancePoolManager healthy - {totalAvailable} available, {totalActive} active, {efficiency:P1} efficiency");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            _pool?.RespondToMemoryPressure(pressureLevel);
        }

        public UIScreen GetPooledInstance(UIScreenType screenType)
        {
            return _pool?.GetPooledInstance(screenType);
        }

        public void RegisterNewInstance(UIScreenType screenType, UIScreen screen)
        {
            _pool?.RegisterNewInstance(screenType, screen);
        }

        public void ReturnInstance(UIScreenType screenType, UIScreen screen)
        {
            _pool?.ReturnInstance(screenType, screen);
        }

        public (int available, int active, int totalCreated, double efficiency) GetStats(UIScreenType screenType)
        {
            return _pool?.GetStats(screenType) ?? (0, 0, 0, 0.0);
        }

        public (int totalAvailable, int totalActive, int totalCreated, double overallEfficiency) GetOverallStats()
        {
            return _pool?.GetOverallStats() ?? (0, 0, 0, 0.0);
        }

        public void Dispose()
        {
            _pool?.Dispose();
            _isInitialized = false;
        }
    }
}
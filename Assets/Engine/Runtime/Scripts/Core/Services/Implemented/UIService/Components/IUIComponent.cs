using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base interface for UIService components that can be initialized, health-checked, and disposed
    /// </summary>
    public interface IUIComponent : IDisposable
    {
        /// <summary>
        /// Component name for identification and debugging
        /// </summary>
        string ComponentName { get; }

        /// <summary>
        /// Whether this component is currently initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initialize the component with the provided context
        /// </summary>
        UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Shutdown the component gracefully
        /// </summary>
        UniTask ShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform health check on the component
        /// </summary>
        UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Respond to memory pressure by cleaning up resources
        /// </summary>
        void RespondToMemoryPressure(float pressureLevel);
    }

    /// <summary>
    /// Health status for UI components
    /// </summary>
    public class ComponentHealthStatus
    {
        public bool IsHealthy { get; }
        public string Message { get; }

        public ComponentHealthStatus(bool isHealthy, string message = "")
        {
            IsHealthy = isHealthy;
            Message = message ?? "";
        }
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    public interface IEngineService
    {
        #region Lifecycle Management

        /// <summary>
        /// Asynchronously initialize the service with dependency injection support
        /// </summary>/// 
        /// <param name="provider">Service provider for dependency resolution</param> 
        /// <param name="cancellationToken">Cancellation token for graceful shutdown</param> 
        /// <returns>Result containing success status, error details, and initialization metrics</returns>
        UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously shutdown the service with graceful cleanup
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for forced shutdown</param>
        /// <returns>Result containing shutdown status and cleanup metrics</returns>
        UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform health check on the service
        /// </summary>
        /// <returns>Health check result with status and performance metrics</returns>
        UniTask<ServiceHealthStatus> HealthCheckAsync();

        #endregion
    }
}
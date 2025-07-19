using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;

namespace Sinkii09.Engine.Tests.TestHelpers
{
    [EngineService(ServiceCategory.Test, ServicePriority.High)]
    public class MockHighPriorityService : IEngineService
    {
        public bool InitializeCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public bool ShouldFailInitialization { get; set; }
        public int InitializationDelay { get; set; }

        /// <summary>
        /// Reset flags for testing purposes
        /// </summary>
        public void ResetFlags()
        {
            InitializeCalled = false;
            ShutdownCalled = false;
        }

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;

            if (InitializationDelay > 0)
                await UniTask.Delay(InitializationDelay, cancellationToken: cancellationToken);

            if (ShouldFailInitialization)
            {
                return ServiceInitializationResult.Failed("Mock initialization failure");
            }

            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalled = true;
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    [EngineService(ServiceCategory.Test, ServicePriority.Medium, RequiredServices = new[] { typeof(MockHighPriorityService) })]
    public class MockDependentService : IEngineService
    {
        public bool InitializeCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public MockHighPriorityService HighPriorityService { get; private set; }

        /// <summary>
        /// Reset flags for testing purposes
        /// </summary>
        public void ResetFlags()
        {
            InitializeCalled = false;
            ShutdownCalled = false;
        }

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;

            HighPriorityService = provider.GetService(typeof(MockHighPriorityService)) as MockHighPriorityService;
            if (HighPriorityService == null)
            {
                return ServiceInitializationResult.Failed("Required dependency not found");
            }

            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalled = true;
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    [EngineService(ServiceCategory.Test, ServicePriority.Low, OptionalServices = new[] { typeof(MockHighPriorityService) })]
    public class MockOptionalDependencyService : IEngineService
    {
        public bool InitializeCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public MockHighPriorityService OptionalService { get; private set; }

        /// <summary>
        /// Reset flags for testing purposes
        /// </summary>
        public void ResetFlags()
        {
            InitializeCalled = false;
            ShutdownCalled = false;
        }

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;

            OptionalService = provider.GetService(typeof(MockHighPriorityService)) as MockHighPriorityService;

            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalled = true;
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    [EngineService(InitializeAtRuntime = false)]
    public class MockManualService : IEngineService
    {
        public bool InitializeCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }

        /// <summary>
        /// Reset flags for testing purposes
        /// </summary>
        public void ResetFlags()
        {
            InitializeCalled = false;
            ShutdownCalled = false;
        }

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;
            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalled = true;
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    public class MockServiceWithoutAttribute : IEngineService
    {

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    [EngineService(ServiceCategory.Test, RequiredServices = new[] { typeof(CircularDependencyServiceB) })]
    public class CircularDependencyServiceA : IEngineService
    {

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }

    [EngineService(ServiceCategory.Test, RequiredServices = new[] { typeof(CircularDependencyServiceA) })]
    public class CircularDependencyServiceB : IEngineService
    {

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return ServiceHealthStatus.Healthy("Mock service is healthy");
        }
    }
}
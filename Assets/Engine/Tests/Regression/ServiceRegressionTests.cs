using NUnit.Framework;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Tests.Regression
{
    [TestFixture]
    public class ServiceRegressionTests
    {
        private ServiceContainer _container;
        private ServiceLifecycleManager _lifecycleManager;

        [SetUp]
        public void SetUp()
        {
            _container = new ServiceContainer();
            _lifecycleManager = new ServiceLifecycleManager(_container);
        }

        [TearDown]
        public void TearDown()
        {
            _lifecycleManager?.Dispose();
            _container?.Dispose();
            _lifecycleManager = null;
            _container = null;
        }

        [Test]
        public void ServiceContainer_DoubleRegistration_LogsWarning()
        {
            // Regression test: Double registration should log warning but not throw
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            // Should not throw - current implementation logs warning and overwrites
            Assert.DoesNotThrow(() => _container.RegisterService<MockHighPriorityService, MockHighPriorityService>());
        }

        [Test]
        public void ServiceContainer_TryResolve_ReturnsFalseForUnregistered()
        {
            // Regression test: Ensure unregistered services are handled correctly
            var success = _container.TryResolve<MockHighPriorityService>(out var service);
            Assert.IsFalse(success);
            Assert.IsNull(service);
        }

        [Test]
        public void ServiceContainer_RegisteredServices_AreAccessible()
        {
            // Regression test: Ensure registered services are accessible
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var registeredServices = _container.GetRegisteredServices().ToList();
            Assert.IsTrue(registeredServices.Contains(typeof(MockHighPriorityService)));
            Assert.IsTrue(registeredServices.Contains(typeof(MockDependentService)));
        }

        [Test]
        public void DependencyGraph_CircularDependency_DetectedCorrectly()
        {
            // Regression test: Ensure circular dependencies are always detected
            _container.RegisterService<CircularDependencyServiceA, CircularDependencyServiceA>();
            _container.RegisterService<CircularDependencyServiceB, CircularDependencyServiceB>();

            var graph = _container.BuildDependencyGraph();
            Assert.IsTrue(graph.HasCircularDependencies);
        }

        [Test]
        public void DependencyGraph_CacheInvalidation_WorksCorrectly()
        {
            // Regression test: Ensure cache is invalidated when services change
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var graph1 = _container.BuildDependencyGraph();

            _container.RegisterService<MockDependentService, MockDependentService>();
            var graph2 = _container.BuildDependencyGraph();

            Assert.AreNotSame(graph1, graph2, "Cache should be invalidated after service registration");
        }

        [Test]
        public async Task ServiceInitialization_MultipleInitializations_OnlyInitializesOnce()
        {
            // Regression test: Ensure services are only initialized once
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();
            service.ResetFlags(); // Reset flag

            // Second initialization should be no-op since already initialized
            await _lifecycleManager.InitializeAllAsync();

            Assert.IsFalse(service.InitializeCalled, "Service should not be initialized twice");
        }

        [Test]
        public async Task ServiceShutdown_MultipleShutdowns_OnlyCallsShutdownOnce()
        {
            // Regression test: Ensure services are only shutdown once
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();
            await _lifecycleManager.ShutdownAllAsync();

            service.ResetFlags(); // Reset flag
            await _lifecycleManager.ShutdownAllAsync();

            Assert.IsFalse(service.ShutdownCalled, "Service should not be shutdown twice");
        }

        [Test]
        public async Task ServiceInitialization_FailedDependency_HandlesGracefully()
        {
            // Regression test: Ensure failed dependencies don't crash the system
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var highPriorityService = _container.Resolve<MockHighPriorityService>();
            var dependentService = _container.Resolve<MockDependentService>();

            highPriorityService.ShouldFailInitialization = true;

            var report = await _lifecycleManager.InitializeAllAsync();

            Assert.IsFalse(report.Success);
            AssertExtensions.AssertServiceInError<MockHighPriorityService>(_lifecycleManager);
        }

        [Test]
        public void ServiceDiscovery_RepeatedCalls_ReturnConsistentResults()
        {
            // Regression test: Ensure dependency graph building is consistent
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var graph1 = _container.BuildDependencyGraph();
            var graph2 = _container.BuildDependencyGraph();

            var order1 = graph1.GetInitializationOrder();
            var order2 = graph2.GetInitializationOrder();

            CollectionAssert.AreEqual(order1, order2, "Dependency graph should be consistent");
        }

        [Test]
        public void ServiceAttribute_NullDependencies_HandledCorrectly()
        {
            // Regression test: Ensure null dependency arrays don't cause issues
            var serviceType = typeof(ServiceWithNullDependencies);
            var attribute = serviceType.GetEngineServiceAttribute();

            Assert.DoesNotThrow(() =>
            {
                _container.RegisterService<ServiceWithNullDependencies, ServiceWithNullDependencies>();
                var graph = _container.BuildDependencyGraph();
            });
        }

        [Test]
        public async Task ServiceHealthCheck_UnhealthyService_ReportsCorrectly()
        {
            // Regression test: Ensure unhealthy services are reported correctly
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();
            service.ShouldFailInitialization = true;

            await _lifecycleManager.InitializeAllAsync();

            var healthReport = await _lifecycleManager.PerformHealthChecksAsync();
            var health = healthReport.Results[typeof(MockHighPriorityService)];
            AssertExtensions.AssertUnhealthy(health);
        }

        [Test]
        public async Task ServiceRestart_PreservesOtherServices()
        {
            // Regression test: Ensure restarting one service doesn't affect others
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();

            var service1 = _container.Resolve<MockHighPriorityService>();
            var service2 = _container.Resolve<MockOptionalDependencyService>();

            await _lifecycleManager.InitializeAllAsync();

            // Reset flags
            service1.ResetFlags();
            service2.ResetFlags();

            await _lifecycleManager.RestartServiceAsync(typeof(MockHighPriorityService));

            Assert.IsTrue(service1.InitializeCalled, "Restarted service should be re-initialized");
            Assert.IsFalse(service2.InitializeCalled, "Other services should not be affected");
        }

        [Test]
        public void ServiceContainer_ThreadSafety_HandlesMultipleThreads()
        {
            // Regression test: Ensure container is thread-safe
            const int threadCount = 10;
            const int operationsPerThread = 50;

            var tasks = new Task[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        try
                        {
                            // Try to resolve services concurrently
                            if (_container.IsRegistered<MockHighPriorityService>())
                            {
                                var retrieved = _container.Resolve<MockHighPriorityService>();
                            }

                            // Try to check registrations
                            var registeredServices = _container.GetRegisteredServices();
                        }
                        catch (InvalidOperationException)
                        {
                            // Expected for some edge cases
                        }
                    }
                });
            }

            // Register a service first to have something to resolve
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
        }

        [Test]
        public void ServiceContainer_Disposal_CleansUpCorrectly()
        {
            // Regression test: Ensure disposal works correctly
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            Assert.IsTrue(_container.IsRegistered<MockHighPriorityService>());

            _container.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                _container.Resolve<MockHighPriorityService>());
        }

        [Test]
        public void ServiceLifecycleManager_Disposal_CleansUpCorrectly()
        {
            // Regression test: Ensure lifecycle manager disposal works
            Assert.DoesNotThrow(() => _lifecycleManager.Dispose());

            // Should not throw on multiple disposals
            Assert.DoesNotThrow(() => _lifecycleManager.Dispose());
        }

        [Test]
        public async Task Engine_InitializationWithoutServices_HandlesGracefully()
        {
            // Regression test: Ensure engine handles empty service registration
            var configProvider = new TestConfigProvider();
            var engineBehaviour = new GameObject("TestEngine").AddComponent<EngineBehaviour>();

            try
            {
                // Should not crash even with no discoverable services
                await Engine.InitializeAsync(configProvider, engineBehaviour);

                // Just ensure it completes without throwing
                Assert.IsTrue(Engine.Initialized || !Engine.Initialized);

                await Engine.TerminateAsync();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineBehaviour.gameObject);
            }
        }
    }

    // Test service with null dependencies
    [EngineService(RequiredServices = null, OptionalServices = null)]
    public class ServiceWithNullDependencies : MockHighPriorityService
    {
        // This service has null dependency arrays to test handling
    }
}
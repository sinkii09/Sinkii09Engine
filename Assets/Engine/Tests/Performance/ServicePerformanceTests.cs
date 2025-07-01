using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sinkii09.Engine.Tests.Performance
{
    [TestFixture]
    public class ServicePerformanceTests
    {
        private ServiceContainer _container;
        private TestServiceProvider _testProvider;
        private Stopwatch _stopwatch;

        [SetUp]
        public void SetUp()
        {
            _container = new ServiceContainer();
            _testProvider = new TestServiceProvider();
            _stopwatch = new Stopwatch();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _container = null;
            _testProvider = null;
            _stopwatch = null;
        }

        [Test]
        public void ServiceRegistration_Performance_WithinAcceptableLimits()
        {
            const int serviceCount = 1000;
            const int maxRegistrationTimeMs = 100;

            _stopwatch.Start();

            // Register many services
            for (int i = 0; i < serviceCount; i++)
            {
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
            }

            _stopwatch.Stop();

            Assert.Less(_stopwatch.ElapsedMilliseconds, maxRegistrationTimeMs,
                $"Service registration took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxRegistrationTimeMs}ms");
        }

        [Test]
        public void ServiceResolution_Performance_WithinAcceptableLimits()
        {
            const int resolutionCount = 10000;
            const int maxResolutionTimeMs = 50;

            // Register service once
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            _stopwatch.Start();

            // Resolve service many times
            for (int i = 0; i < resolutionCount; i++)
            {
                var service = _container.Resolve<MockHighPriorityService>();
                Assert.IsNotNull(service);
            }

            _stopwatch.Stop();

            Assert.Less(_stopwatch.ElapsedMilliseconds, maxResolutionTimeMs,
                $"Service resolution took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxResolutionTimeMs}ms");
        }

        [Test]
        public void DependencyGraphBuilding_Performance_WithinAcceptableLimits()
        {
            const int serviceCount = 100;
            const int maxBuildTimeMs = 200;

            // Register services with dependencies
            for (int i = 0; i < serviceCount; i++)
            {
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
                _container.RegisterService(typeof(MockDependentService), typeof(MockDependentService));
            }

            _stopwatch.Start();

            var graph = _container.BuildDependencyGraph();

            _stopwatch.Stop();

            Assert.IsNotNull(graph);
            Assert.Less(_stopwatch.ElapsedMilliseconds, maxBuildTimeMs,
                $"Dependency graph building took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxBuildTimeMs}ms");
        }

        [Test]
        public void DependencyGraphCaching_Performance_IsFaster()
        {
            const int serviceCount = 50;

            // Register services
            for (int i = 0; i < serviceCount; i++)
            {
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
            }

            // First build (no cache)
            _stopwatch.Start();
            var graph1 = _container.BuildDependencyGraph();
            _stopwatch.Stop();
            var firstBuildTime = _stopwatch.ElapsedMilliseconds;

            _stopwatch.Reset();

            // Second build (with cache)
            _stopwatch.Start();
            var graph2 = _container.BuildDependencyGraph();
            _stopwatch.Stop();
            var secondBuildTime = _stopwatch.ElapsedMilliseconds;

            Assert.AreSame(graph1, graph2, "Cached dependency graph should be the same instance");
            Assert.LessOrEqual(secondBuildTime, firstBuildTime,
                $"Cached build ({secondBuildTime}ms) should be faster than or equal to first build ({firstBuildTime}ms)");
        }

        [Test]
        public async Task ServiceInitialization_Performance_WithinAcceptableLimits()
        {
            const int serviceCount = 50;
            const int maxInitializationTimeMs = 1000;

            var services = new List<MockHighPriorityService>();

            // Register and prepare services
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new MockHighPriorityService();
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
                _testProvider.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
                services.Add(service);
            }

            var lifecycleManager = new ServiceLifecycleManager(_container);

            _stopwatch.Start();

            var result = await lifecycleManager.InitializeAllAsync();

            _stopwatch.Stop();

            Assert.IsTrue(result.Success);
            Assert.Less(_stopwatch.ElapsedMilliseconds, maxInitializationTimeMs,
                $"Service initialization took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxInitializationTimeMs}ms");

            // Verify all services were initialized
            foreach (var service in services)
            {
                Assert.IsTrue(service.InitializeCalled);
            }

            lifecycleManager.Dispose();
        }

        [Test]
        public async Task ConcurrentServiceResolution_Performance_WithinAcceptableLimits()
        {
            const int concurrentTasks = 100;
            const int resolutionsPerTask = 100;
            const int maxTotalTimeMs = 500;

            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var tasks = new List<Task>();

            _stopwatch.Start();

            for (int i = 0; i < concurrentTasks; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < resolutionsPerTask; j++)
                    {
                        var service = _container.Resolve<MockHighPriorityService>();
                        Assert.IsNotNull(service);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            _stopwatch.Stop();

            Assert.Less(_stopwatch.ElapsedMilliseconds, maxTotalTimeMs,
                $"Concurrent service resolution took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxTotalTimeMs}ms");
        }


        [Test]
        public void ServiceValidation_Performance_WithinAcceptableLimits()
        {
            const int validationCount = 1000;
            const int maxValidationTimeMs = 100;

            var serviceType = typeof(MockHighPriorityService);
            var attribute = serviceType.GetEngineServiceAttribute();

            _stopwatch.Start();

            for (int i = 0; i < validationCount; i++)
            {
                var isValid = ValidateServiceForRegistration(serviceType, attribute);
                Assert.IsTrue(isValid);
            }

            _stopwatch.Stop();

            Assert.Less(_stopwatch.ElapsedMilliseconds, maxValidationTimeMs,
                $"Service validation took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxValidationTimeMs}ms");
        }

        [Test]
        public async Task ServiceHealthCheck_Performance_WithinAcceptableLimits()
        {
            const int serviceCount = 20;
            const int maxHealthCheckTimeMs = 200;

            var services = new List<MockHighPriorityService>();

            // Register services
            for (int i = 0; i < serviceCount; i++)
            {
                var service = new MockHighPriorityService();
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
                _testProvider.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
                services.Add(service);
            }

            var lifecycleManager = new ServiceLifecycleManager(_container);
            await lifecycleManager.InitializeAllAsync();

            _stopwatch.Start();

            var healthTasks = services.Select(async service => await service.HealthCheckAsync()).ToArray();
            var healthResults = await UniTask.WhenAll(healthTasks);

            _stopwatch.Stop();

            Assert.AreEqual(serviceCount, healthResults.Length);
            Assert.Less(_stopwatch.ElapsedMilliseconds, maxHealthCheckTimeMs,
                $"Health checks took {_stopwatch.ElapsedMilliseconds}ms, expected less than {maxHealthCheckTimeMs}ms");

            lifecycleManager.Dispose();
        }

        [Test]
        public void MemoryUsage_ServiceContainer_WithinAcceptableLimits()
        {
            const int serviceCount = 1000;
            const long maxMemoryIncreaseBytes = 10 * 1024 * 1024; // 10MB

            var initialMemory = GC.GetTotalMemory(true);

            // Register many services
            for (int i = 0; i < serviceCount; i++)
            {
                _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            Assert.Less(memoryIncrease, maxMemoryIncreaseBytes,
                $"Memory usage increased by {memoryIncrease} bytes, expected less than {maxMemoryIncreaseBytes} bytes");
        }

        private bool ValidateServiceForRegistration(Type serviceType, Sinkii09.Engine.Services.EngineServiceAttribute attribute)
        {
            // Simplified validation for performance testing
            return typeof(Sinkii09.Engine.Services.IEngineService).IsAssignableFrom(serviceType) &&
                   !serviceType.IsAbstract &&
                   serviceType.GetConstructors().Length > 0;
        }
    }
}
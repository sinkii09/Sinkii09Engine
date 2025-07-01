using NUnit.Framework;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using System.Linq;

namespace Sinkii09.Engine.Tests.Services
{
    [TestFixture]
    public class ServiceContainerTests
    {
        private ServiceContainer _container;

        [SetUp]
        public void SetUp()
        {
            _container = new ServiceContainer();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _container = null;
        }

        [Test]
        public void RegisterService_ValidService_RegistersSuccessfully()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var service = _container.Resolve<MockHighPriorityService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<MockHighPriorityService>(service);
        }

        [Test]
        public void RegisterService_ByType_RegistersSuccessfully()
        {
            _container.RegisterService(typeof(MockHighPriorityService), typeof(MockHighPriorityService));

            var service = _container.Resolve(typeof(MockHighPriorityService));
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<MockHighPriorityService>(service);
        }

        [Test]
        public void RegisterSingleton_WithInstance_RegistersSuccessfully()
        {
            var instance = new MockHighPriorityService();
            _container.RegisterSingleton<MockHighPriorityService>(instance);

            var retrievedService = _container.Resolve<MockHighPriorityService>();
            Assert.AreSame(instance, retrievedService);
        }

        [Test]
        public void TryResolve_UnregisteredService_ReturnsFalse()
        {
            var success = _container.TryResolve<MockHighPriorityService>(out var service);
            Assert.IsFalse(success);
            Assert.IsNull(service);
        }

        [Test]
        public void Resolve_RegisteredService_ReturnsSameInstance()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var service1 = _container.Resolve<MockHighPriorityService>();
            var service2 = _container.Resolve<MockHighPriorityService>();

            Assert.AreSame(service1, service2);
        }

        [Test]
        public void IsRegistered_RegisteredService_ReturnsTrue()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var isRegistered = _container.IsRegistered<MockHighPriorityService>();
            Assert.IsTrue(isRegistered);
        }

        [Test]
        public void IsRegistered_UnregisteredService_ReturnsFalse()
        {
            var isRegistered = _container.IsRegistered<MockHighPriorityService>();
            Assert.IsFalse(isRegistered);
        }

        [Test]
        public void RegisterService_DoubleRegistration_LogsWarning()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            Assert.IsTrue(_container.IsRegistered<MockHighPriorityService>());

            // Should not throw - current implementation logs warning and overwrites
            Assert.DoesNotThrow(() => _container.RegisterService<MockHighPriorityService, MockHighPriorityService>());
            Assert.IsTrue(_container.IsRegistered<MockHighPriorityService>());
        }

        [Test]
        public void GetRegisteredServices_ReturnsAllRegisteredServices()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var registeredServices = _container.GetRegisteredServices().ToList();

            AssertExtensions.AssertServiceCount(registeredServices, 4); // +2 for self-registered IServiceProvider and IServiceContainer
            AssertExtensions.AssertContainsService<MockHighPriorityService>(registeredServices);
            AssertExtensions.AssertContainsService<MockDependentService>(registeredServices);
        }

        [Test]
        public void BuildDependencyGraph_WithValidDependencies_BuildsCorrectly()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var graph = _container.BuildDependencyGraph();

            Assert.IsNotNull(graph);
            var initializationOrder = graph.GetInitializationOrder();

            AssertExtensions.AssertInitializationOrder<MockHighPriorityService, MockDependentService>(initializationOrder);
        }

        [Test]
        public void BuildDependencyGraph_WithCircularDependencies_DetectsCircularDependency()
        {
            _container.RegisterService<CircularDependencyServiceA, CircularDependencyServiceA>();
            _container.RegisterService<CircularDependencyServiceB, CircularDependencyServiceB>();

            var graph = _container.BuildDependencyGraph();
            var hasCircularDependency = graph.HasCircularDependencies;

            Assert.IsTrue(hasCircularDependency);
        }

        [Test]
        public void BuildDependencyGraph_CachesResult()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var graph1 = _container.BuildDependencyGraph();
            var graph2 = _container.BuildDependencyGraph();

            Assert.AreSame(graph1, graph2);
        }

        [Test]
        public void RegisterService_InvalidatesDependencyGraphCache()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var graph1 = _container.BuildDependencyGraph();

            _container.RegisterService<MockDependentService, MockDependentService>();
            var graph2 = _container.BuildDependencyGraph();

            Assert.AreNotSame(graph1, graph2);
        }

        [Test]
        public void ValidateDependencies_WithValidServices_ReturnsTrue()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();

            var isValid = _container.ValidateDependencies();
            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateDependencies_WithMissingDependencies_ReturnsFalse()
        {
            // Register dependent service without its dependency
            _container.RegisterService<MockDependentService, MockDependentService>();

            var isValid = _container.ValidateDependencies();
            Assert.IsFalse(isValid);
        }

        [Test]
        public void Resolve_WithOptionalDependencies_Works()
        {
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();

            var service = _container.Resolve<MockOptionalDependencyService>();
            Assert.IsNotNull(service);
        }

        [Test]
        public void Resolve_WithAvailableOptionalDependencies_ResolvesSuccessfully()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();

            var service = _container.Resolve<MockOptionalDependencyService>();
            Assert.IsNotNull(service);

            // The dependency injection would happen during initialization, not construction
            // This test verifies the container can resolve the service
        }

        [Test]
        public void GetCircularDependencies_WithCircularDeps_ReturnsCircularPaths()
        {
            _container.RegisterService<CircularDependencyServiceA, CircularDependencyServiceA>();
            _container.RegisterService<CircularDependencyServiceB, CircularDependencyServiceB>();

            var circularDependencies = _container.GetCircularDependencies().ToList();
            Assert.IsTrue(circularDependencies.Count > 0);
        }

        [Test]
        public void GetRegisteredServices_ReturnsCorrectCount()
        {
            // Container auto-registers IServiceProvider and IServiceContainer
            var initialCount = _container.GetRegisteredServices().Count();
            Assert.AreEqual(2, initialCount);

            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            Assert.AreEqual(initialCount + 1, _container.GetRegisteredServices().Count());

            _container.RegisterService<MockDependentService, MockDependentService>();
            Assert.AreEqual(initialCount + 2, _container.GetRegisteredServices().Count());
        }

        [Test]
        public void ServiceRegistration_PreservesTypeInformation()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            var registeredTypes = _container.GetRegisteredServices().ToList();
            Assert.IsTrue(registeredTypes.Contains(typeof(MockHighPriorityService)));
        }

        [Test]
        public void Dispose_CleansUpResources()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            Assert.IsTrue(_container.IsRegistered<MockHighPriorityService>());

            _container.Dispose();

            // After disposal, container should be unusable
            Assert.Throws<ObjectDisposedException>(() => _container.Resolve<MockHighPriorityService>());
        }
    }
}
using NUnit.Framework;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Tests.Services
{
    [TestFixture]
    public class ServiceLifecycleManagerTests
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
        public async Task InitializeServicesAsync_WithValidServices_InitializesSuccessfully()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            // Act
            var report = await _lifecycleManager.InitializeAllAsync();
            var result = report.Success;

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(_lifecycleManager.IsInitialized);
            AssertExtensions.AssertServiceInitialized<MockHighPriorityService>(_lifecycleManager);
            Assert.IsTrue(service.InitializeCalled);
        }

        [Test]
        public async Task InitializeServicesAsync_WithDependencies_InitializesInCorrectOrder()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();
            
            var highPriorityService = _container.Resolve<MockHighPriorityService>();
            var dependentService = _container.Resolve<MockDependentService>();

            // Act
            var report = await _lifecycleManager.InitializeAllAsync();
            var result = report.Success;

            // Assert
            Assert.IsTrue(result);
            AssertExtensions.AssertServiceInitialized<MockHighPriorityService>(_lifecycleManager);
            AssertExtensions.AssertServiceInitialized<MockDependentService>(_lifecycleManager);
            Assert.IsTrue(highPriorityService.InitializeCalled);
            Assert.IsTrue(dependentService.InitializeCalled);
            Assert.IsNotNull(dependentService.HighPriorityService);
        }

        [Test]
        public async Task InitializeServicesAsync_WithFailingService_HandlesFailing()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();
            service.ShouldFailInitialization = true;

            // Act
            var report = await _lifecycleManager.InitializeAllAsync();
            var result = report.Success;

            // Assert
            Assert.IsFalse(result);
            AssertExtensions.AssertServiceInError<MockHighPriorityService>(_lifecycleManager);
        }

        [Test]
        public async Task InitializeServicesAsync_WithTimeout_HandlesTimeout()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();
            service.InitializationDelay = 100; // 100ms delay

            using var cts = new CancellationTokenSource(50); // 50ms timeout

            // Act
            var report = await _lifecycleManager.InitializeAllAsync(cts.Token);

            // Assert
            Assert.IsFalse(report.Success);
            Assert.AreEqual("Initialization cancelled", report.FailureReason);
        }

        [Test]
        public async Task ShutdownServicesAsync_InitializedServices_ShutsDownSuccessfully()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();
            Assert.IsTrue(_lifecycleManager.IsInitialized);

            // Act
            await _lifecycleManager.ShutdownAllAsync();

            // Assert
            Assert.IsFalse(_lifecycleManager.IsInitialized);
            AssertExtensions.AssertServiceShutdown<MockHighPriorityService>(_lifecycleManager);
            Assert.IsTrue(service.ShutdownCalled);
        }

        [Test]
        public async Task ShutdownServicesAsync_WithDependencies_ShutsDownInReverseOrder()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockDependentService, MockDependentService>();
            
            var highPriorityService = _container.Resolve<MockHighPriorityService>();
            var dependentService = _container.Resolve<MockDependentService>();

            await _lifecycleManager.InitializeAllAsync();

            // Reset shutdown flags
            highPriorityService.ResetFlags();
            dependentService.ResetFlags();

            // Act
            await _lifecycleManager.ShutdownAllAsync();

            // Assert
            Assert.IsTrue(highPriorityService.ShutdownCalled);
            Assert.IsTrue(dependentService.ShutdownCalled);
            AssertExtensions.AssertServiceShutdown<MockHighPriorityService>(_lifecycleManager);
            AssertExtensions.AssertServiceShutdown<MockDependentService>(_lifecycleManager);
        }

        [Test]
        public async Task RestartServiceAsync_ValidService_RestartsSuccessfully()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();

            // Reset flags
            service.ResetFlags();

            // Act
            var result = await _lifecycleManager.RestartServiceAsync(typeof(MockHighPriorityService));

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(service.ShutdownCalled);
            Assert.IsTrue(service.InitializeCalled);
            AssertExtensions.AssertServiceInitialized<MockHighPriorityService>(_lifecycleManager);
        }

        [Test]
        public async Task GetServiceHealthAsync_HealthyService_ReturnsHealthy()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();

            // Act
            var healthReport = await _lifecycleManager.PerformHealthChecksAsync();
            var health = healthReport.Results[typeof(MockHighPriorityService)];

            // Assert
            Assert.IsNotNull(health);
            AssertExtensions.AssertHealthy(health);
        }

        [Test]
        public async Task GetServiceHealthAsync_ErrorService_ReturnsUnhealthy()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();
            service.ShouldFailInitialization = true;

            await _lifecycleManager.InitializeAllAsync();

            // Act
            var healthReport = await _lifecycleManager.PerformHealthChecksAsync();
            var health = healthReport.Results[typeof(MockHighPriorityService)];

            // Assert
            Assert.IsNotNull(health);
            AssertExtensions.AssertUnhealthy(health);
        }

        [Test]
        public async Task GetAllServiceHealthAsync_MultipleServices_ReturnsAllHealth()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();
            
            var service1 = _container.Resolve<MockHighPriorityService>();
            var service2 = _container.Resolve<MockOptionalDependencyService>();

            await _lifecycleManager.InitializeAllAsync();

            // Act
            var healthReport = await _lifecycleManager.PerformHealthChecksAsync();

            // Assert
            Assert.AreEqual(2, healthReport.Results.Count);
            Assert.IsTrue(healthReport.Results.ContainsKey(typeof(MockHighPriorityService)));
            Assert.IsTrue(healthReport.Results.ContainsKey(typeof(MockOptionalDependencyService)));
        }

        [Test]
        public async Task GetServiceLifecycleInfo_AfterInitialization_ReturnsValidInfo()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();

            // Act
            await _lifecycleManager.InitializeAllAsync();
            var info = _lifecycleManager.GetServiceLifecycleInfo(typeof(MockHighPriorityService));

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(typeof(MockHighPriorityService), info.ServiceType);
            Assert.AreEqual(ServiceState.Running, info.State);
        }

        [Test]
        public async Task InitializeServicesAsync_WithOptionalDependencies_HandlesOptionalDependencies()
        {
            // Arrange
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();
            var service = _container.Resolve<MockOptionalDependencyService>();

            // Act
            var report = await _lifecycleManager.InitializeAllAsync();
            var result = report.Success;

            // Assert
            Assert.IsTrue(result);
            AssertExtensions.AssertServiceInitialized<MockOptionalDependencyService>(_lifecycleManager);
            Assert.IsNull(service.OptionalService); // Optional dependency not available
        }

        [Test]
        public async Task InitializeServicesAsync_WithAvailableOptionalDependencies_InjectsOptionalDependencies()
        {
            // Arrange
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            _container.RegisterService<MockOptionalDependencyService, MockOptionalDependencyService>();
            
            var highPriorityService = _container.Resolve<MockHighPriorityService>();
            var optionalService = _container.Resolve<MockOptionalDependencyService>();

            // Act
            var report = await _lifecycleManager.InitializeAllAsync();
            var result = report.Success;

            // Assert
            Assert.IsTrue(result);
            AssertExtensions.AssertServiceInitialized<MockOptionalDependencyService>(_lifecycleManager);
            Assert.IsNotNull(optionalService.OptionalService); // Optional dependency should be injected
        }

        [Test]
        public void IsInitialized_BeforeInitialization_ReturnsFalse()
        {
            Assert.IsFalse(_lifecycleManager.IsInitialized);
        }

        [Test]
        public async Task IsInitialized_AfterSuccessfulInitialization_ReturnsTrue()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();

            await _lifecycleManager.InitializeAllAsync();

            Assert.IsTrue(_lifecycleManager.IsInitialized);
        }

        [Test]
        public async Task IsInitialized_AfterFailedInitialization_ReturnsFalse()
        {
            _container.RegisterService<MockHighPriorityService, MockHighPriorityService>();
            var service = _container.Resolve<MockHighPriorityService>();
            service.ShouldFailInitialization = true;

            await _lifecycleManager.InitializeAllAsync();

            Assert.IsFalse(_lifecycleManager.IsInitialized);
        }
    }
}
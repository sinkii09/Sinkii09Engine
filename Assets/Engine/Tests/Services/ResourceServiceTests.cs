using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Common.Resources;
using System.Reflection;
using Sinkii09.Engine.Services.Performance;
using System.Linq;

namespace Sinkii09.Engine.Tests.Services
{
    /// <summary>
    /// Comprehensive test suite for the enhanced ResourceService implementation
    /// Tests resource loading, circuit breakers, memory management, and configuration
    /// </summary>
    [TestFixture]
    public class ResourceServiceTests
    {
        private ResourceService _resourceService;
        private ResourceServiceConfiguration _testConfig;
        private ServiceContainer _container;
        private ServiceLifecycleManager _lifecycleManager;

        [SetUp]
        public void SetUp()
        {
            // Create test configuration
            _testConfig = ScriptableObject.CreateInstance<ResourceServiceConfiguration>();
            _testConfig.name = "TestResourceServiceConfiguration";
            
            // Set test configuration values
            var enabledProvidersField = typeof(ResourceServiceConfiguration).GetField("_enabledProviders", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxConcurrentLoadsField = typeof(ResourceServiceConfiguration).GetField("_maxConcurrentLoads", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enableCircuitBreakerField = typeof(ResourceServiceConfiguration).GetField("_enableCircuitBreaker", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enableDetailedLoggingField = typeof(ResourceServiceConfiguration).GetField("_enableDetailedLogging", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            enabledProvidersField?.SetValue(_testConfig, ProviderType.Resources);
            maxConcurrentLoadsField?.SetValue(_testConfig, 5);
            enableCircuitBreakerField?.SetValue(_testConfig, true);
            enableDetailedLoggingField?.SetValue(_testConfig, true);
            
            // Create service container and lifecycle manager
            _container = new ServiceContainer();
            _lifecycleManager = new ServiceLifecycleManager(_container);
            
            // Register the ResourceService manually for testing
            _container.RegisterSingleton(_testConfig);
            _container.RegisterService<IResourceService, ResourceService>();
        }

        [TearDown]
        public void TearDown()
        {
            _resourceService?.ShutdownAsync();
            _lifecycleManager?.Dispose();
            _container?.Dispose();
            
            if (_testConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(_testConfig);
            }
        }

        #region Service Registration Tests

        [Test]
        public void ResourceService_ShouldBeRegisteredCorrectly()
        {
            // Arrange & Act
            var isRegistered = _container.IsRegistered<IResourceService>();
            
            // Assert
            Assert.IsTrue(isRegistered, "ResourceService should be registered in the container");
        }

        [Test]
        public void ResourceService_ShouldResolveCorrectly()
        {
            // Arrange & Act
            var service = _container.Resolve<IResourceService>();
            
            // Assert
            Assert.IsNotNull(service, "ResourceService should resolve successfully");
            Assert.IsInstanceOf<ResourceService>(service, "Should resolve to ResourceService implementation");
        }

        [Test]
        public void ResourceService_ShouldHaveCorrectAttributes()
        {
            // Arrange
            var serviceType = typeof(ResourceService);
            
            // Act
            var engineServiceAttr = serviceType.GetEngineServiceAttribute();
            var configAttr = serviceType.GetCustomAttribute<ServiceConfigurationAttribute>();
            
            // Assert
            Assert.IsNotNull(engineServiceAttr, "ResourceService should have EngineService attribute");
            Assert.AreEqual(ServiceCategory.Core, engineServiceAttr.Category, "Should be Core category");
            Assert.AreEqual(ServicePriority.Critical, engineServiceAttr.Priority, "Should be Critical priority");
            Assert.IsTrue(engineServiceAttr.InitializeAtRuntime, "Should initialize at runtime");
            
            Assert.IsNotNull(configAttr, "ResourceService should have ServiceConfiguration attribute");
            Assert.AreEqual(typeof(ResourceServiceConfiguration), configAttr.ConfigurationType, "Should use ResourceServiceConfiguration");
        }

        #endregion

        #region Service Lifecycle Tests

        [Test]
        public async Task ResourceService_ShouldInitializeSuccessfully()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            
            // Act
            var result = await service.InitializeAsync(_container, CancellationToken.None);
            
            // Assert
            Assert.IsTrue(result.IsSuccess, $"ResourceService initialization should succeed: {result.ErrorMessage}");
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage), "Empty error message");
        }

        [Test]
        public async Task ResourceService_ShouldPassHealthCheck()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var healthResult = await service.HealthCheckAsync();
            
            // Assert
            Assert.IsTrue(healthResult.IsHealthy, $"ResourceService should be healthy: {healthResult.StatusMessage}");
        }

        [Test]
        public async Task ResourceService_ShouldShutdownGracefully()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var shutdownResult = await service.ShutdownAsync(CancellationToken.None);
            
            // Assert
            Assert.IsTrue(shutdownResult.IsSuccess, $"ResourceService shutdown should succeed: {shutdownResult.ErrorMessage}");
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void ResourceServiceConfiguration_ShouldValidateCorrectly()
        {
            // Arrange & Act
            var isValid = _testConfig.Validate(out var errors);
            
            // Assert
            Assert.IsTrue(isValid, $"Configuration should be valid. Errors: {string.Join(", ", errors)}");
            Assert.IsEmpty(errors, "Should have no validation errors");
        }

        [Test]
        public void ResourceServiceConfiguration_ShouldProvideCorrectValues()
        {
            // Act & Assert
            Assert.AreEqual(ProviderType.Resources, _testConfig.EnabledProviders);
            Assert.AreEqual(5, _testConfig.MaxConcurrentLoads);
            Assert.IsTrue(_testConfig.EnableCircuitBreaker);
            Assert.IsTrue(_testConfig.EnableDetailedLogging);
        }

        [Test]
        public void ResourceServiceConfiguration_ShouldGenerateConfigSummary()
        {
            // Act
            var summary = _testConfig.GetConfigurationSummary();
            
            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(summary), "Configuration summary should not be empty");
            Assert.IsTrue(summary.Contains("ResourceService Config"), "Should contain service name");
            Assert.IsTrue(summary.Contains("Resources"), "Should contain provider types");
        }

        #endregion

        #region Provider Management Tests

        [Test]
        public async Task ResourceService_ShouldInitializeProviders()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var providersHealth = await service.GetProviderHealthStatusAsync();
            
            // Assert
            Assert.IsNotNull(providersHealth, "Provider health status should not be null");
            Assert.IsTrue(providersHealth.ContainsKey(ProviderType.Resources), "Should contain Resources provider");
        }

        [Test]
        public async Task ResourceService_ShouldReportProviderHealth()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var isInitialized = service.IsProviderInitialized(ProviderType.Resources);
            
            // Assert
            Assert.IsTrue(isInitialized, "Resources provider should be initialized");
        }

        #endregion

        #region Memory Management Tests
        [Test]
        public async Task ResourceService_ShouldProvideMemoryStatistics()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var memoryStats = service.GetMemoryStatistics();
            
            // Assert
            Assert.IsNotNull(memoryStats.MemoryUsageByProvider, "Memory usage by provider should not be null");
            Assert.GreaterOrEqual(memoryStats.MemoryPressureLevel, 0, "Memory pressure should be non-negative");
        }

        #endregion

        #region Statistics Tests

        [Test]
        public async Task ResourceService_ShouldProvideServiceStatistics()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act
            var stats = service.GetStatistics();
            
            // Assert
            Assert.GreaterOrEqual(stats.TotalResourcesLoaded, 0, "Total resources loaded should be non-negative");
            Assert.GreaterOrEqual(stats.TotalLoadFailures, 0, "Total load failures should be non-negative");
            Assert.IsNotNull(stats.LoadCountByProvider, "Load count by provider should not be null");
            Assert.IsNotNull(stats.CircuitBreakerStats.FailureCountByProvider, "Circuit breaker stats should not be null");
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task ResourceService_ShouldHandleConcurrentOperations()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Act - Simulate concurrent operations
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var stats = service.GetStatistics();
                    var memory = service.GetMemoryStatistics();
                    var health = await service.GetProviderHealthStatusAsync();
                }));
            }
            
            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
            Assert.Pass("Concurrent operations completed successfully");
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void ResourceService_ShouldHandleNullConfiguration()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
            {
                new ResourceService(null);
            }, "Should throw ArgumentNullException for null configuration");
        }

        [Test]
        public async Task ResourceService_ShouldHandleInitializationFailure()
        {
            // Arrange - Create service with invalid configuration
            var invalidConfig = ScriptableObject.CreateInstance<ResourceServiceConfiguration>();
            var enabledProvidersField = typeof(ResourceServiceConfiguration).GetField("_enabledProviders", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enabledProvidersField?.SetValue(invalidConfig, ProviderType.None); // Invalid: no providers
            
            var service = new ResourceService(invalidConfig);
            
            // Act
            var result = await service.InitializeAsync(_container, CancellationToken.None);
            
            // Assert
            Assert.IsFalse(result.IsSuccess, "Initialization should fail with invalid configuration");
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage), "Should provide error message");
            
            // Cleanup
            UnityEngine.Object.DestroyImmediate(invalidConfig);
        }

        #endregion

        #region Service Lifecycle Manager Integration Tests

        [Test]
        public async Task ResourceService_ShouldIntegrateWithServiceLifecycleManager()
        {
            // Arrange
            var container = new ServiceContainer();
            var lifecycleManager = new ServiceLifecycleManager(container);
            
            // Register dependencies
            container.RegisterSingleton(_testConfig);
            
            // Register ResourceService with auto-discovery
            container.RegisterRuntimeServices();
            
            // Act - Initialize all services through lifecycle manager
            var initReport = await lifecycleManager.InitializeAllAsync();
            
            // Assert
            Assert.IsTrue(initReport.Success, $"Service initialization should succeed: {initReport.Errors}");
            Assert.IsTrue(container.IsRegistered<IResourceService>(), "ResourceService should be auto-registered");
            
            var service = container.Resolve<IResourceService>();
            Assert.IsNotNull(service, "ResourceService should resolve correctly");
            Assert.IsInstanceOf<ResourceService>(service, "Should resolve to ResourceService implementation");
            
            // Verify service is initialized
            var healthStatus = await service.HealthCheckAsync();
            Assert.IsTrue(healthStatus.IsHealthy, "Service should be healthy after lifecycle initialization");
            
            // Cleanup
            await lifecycleManager.ShutdownAllAsync();
            lifecycleManager.Dispose();
            container.Dispose();
        }

        [Test]
        public async Task ResourceService_ShouldMaintainDependencyInjectionChain()
        {
            // Arrange
            var service = _container.Resolve<IResourceService>();
            
            // Act
            await service.InitializeAsync(_container, CancellationToken.None);
            
            // Assert
            // Verify configuration was injected correctly
            var stats = service.GetStatistics();
            Assert.IsNotNull(stats, "Statistics should be available");
            Assert.AreEqual(0, stats.ActiveProviders, "Should start with 0 active providers initially");
            
            // Verify provider health monitoring works
            var healthStatus = await service.GetProviderHealthStatusAsync();
            Assert.IsNotNull(healthStatus, "Provider health status should be available");
            
            // Verify memory management integration
            var memoryStats = service.GetMemoryStatistics();
            Assert.IsNotNull(memoryStats, "Memory statistics should be available");
        }

        #endregion
    }
}
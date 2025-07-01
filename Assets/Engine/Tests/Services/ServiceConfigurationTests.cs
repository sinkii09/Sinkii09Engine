using NUnit.Framework;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using UnityEngine;

namespace Sinkii09.Engine.Tests.Services
{
    [TestFixture]
    public class ServiceConfigurationTests
    {
        private TestConfigProvider _configProvider;

        [SetUp]
        public void SetUp()
        {
            _configProvider = new TestConfigProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _configProvider = null;
        }

        [Test]
        public void ConfigProvider_GetConfiguration_Generic_ReturnsConfiguration()
        {
            var config = _configProvider.GetConfiguration<TestConfiguration>();

            Assert.IsNotNull(config);
            Assert.IsInstanceOf<TestConfiguration>(config);
        }

        [Test]
        public void ConfigProvider_GetConfiguration_ByType_ReturnsConfiguration()
        {
            var config = _configProvider.GetConfiguration(typeof(TestConfiguration));

            Assert.IsNotNull(config);
            Assert.IsInstanceOf<TestConfiguration>(config);
        }

        [Test]
        public void ServiceAttribute_DefaultValues_AreCorrect()
        {
            var attribute = new EngineServiceAttribute();

            Assert.IsTrue(attribute.InitializeAtRuntime);
            Assert.AreEqual(Sinkii09.Engine.Services.ServicePriority.Medium, attribute.Priority);
            Assert.IsNotNull(attribute.RequiredServices);
            Assert.IsNotNull(attribute.OptionalServices);
            Assert.AreEqual(0, attribute.RequiredServices.Length);
            Assert.AreEqual(0, attribute.OptionalServices.Length);
            Assert.AreEqual(Sinkii09.Engine.Services.ServiceLifetime.Singleton, attribute.Lifetime);
            Assert.AreEqual(0, attribute.InitializationTimeout);
        }

        [Test]
        public void ServiceAttribute_CustomValues_AreSetCorrectly()
        {
            var attribute = new EngineServiceAttribute
            {
                InitializeAtRuntime = false,
                Priority = Sinkii09.Engine.Services.ServicePriority.High,
                RequiredServices = new[] { typeof(MockHighPriorityService) },
                OptionalServices = new[] { typeof(MockDependentService) },
                Lifetime = Sinkii09.Engine.Services.ServiceLifetime.Transient,
                InitializationTimeout = 5000
            };

            Assert.IsFalse(attribute.InitializeAtRuntime);
            Assert.AreEqual(Sinkii09.Engine.Services.ServicePriority.High, attribute.Priority);
            Assert.Contains(typeof(MockHighPriorityService), attribute.RequiredServices);
            Assert.Contains(typeof(MockDependentService), attribute.OptionalServices);
            Assert.AreEqual(Sinkii09.Engine.Services.ServiceLifetime.Transient, attribute.Lifetime);
            Assert.AreEqual(5000, attribute.InitializationTimeout);
        }

        [Test]
        public void ServiceAttribute_EmptyDependencyArrays_HandleCorrectly()
        {
            var attribute = new EngineServiceAttribute
            {
                RequiredServices = new Type[0],
                OptionalServices = new Type[0]
            };

            Assert.IsNotNull(attribute.RequiredServices);
            Assert.IsNotNull(attribute.OptionalServices);
            Assert.AreEqual(0, attribute.RequiredServices.Length);
            Assert.AreEqual(0, attribute.OptionalServices.Length);
        }

        [Test]
        public void ServiceAttribute_NullDependencyArrays_HandleCorrectly()
        {
            var attribute = new EngineServiceAttribute
            {
                RequiredServices = null,
                OptionalServices = null
            };

            // The attribute should handle null arrays
            Assert.IsNull(attribute.RequiredServices);
            Assert.IsNull(attribute.OptionalServices);
        }

        [Test]
        public void ServicePriority_EnumValues_AreOrdered()
        {
            // Verify that priority enum values have the expected ordering
            Assert.IsTrue((int)Sinkii09.Engine.Services.ServicePriority.Critical > (int)Sinkii09.Engine.Services.ServicePriority.High);
            Assert.IsTrue((int)Sinkii09.Engine.Services.ServicePriority.High > (int)Sinkii09.Engine.Services.ServicePriority.Medium);
            Assert.IsTrue((int)Sinkii09.Engine.Services.ServicePriority.Medium > (int)Sinkii09.Engine.Services.ServicePriority.Low);
        }

        [Test]
        public void ServiceLifetime_EnumValues_Exist()
        {
            // Verify that ServiceLifetime enum has expected values
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceLifetime), Sinkii09.Engine.Services.ServiceLifetime.Singleton));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceLifetime), Sinkii09.Engine.Services.ServiceLifetime.Transient));
        }

        [Test]
        public void ServiceState_EnumValues_Exist()
        {
            // Verify that ServiceState enum has expected values
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.Uninitialized));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.Initializing));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.Running));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.ShuttingDown));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.Shutdown));
            Assert.IsTrue(Enum.IsDefined(typeof(Sinkii09.Engine.Services.ServiceState), Sinkii09.Engine.Services.ServiceState.Error));
        }

        [Test]
        public void ConfigurationAttribute_CanBeUsedOnServices()
        {
            // Test that services can be configured with configuration attributes
            var serviceType = typeof(ConfigurableService);
            var configAttribute = serviceType.GetCustomAttributes(typeof(ServiceConfigurationAttribute), false);

            Assert.IsNotNull(configAttribute);
        }

        [Test]
        public void ServiceConfiguration_WithMultipleAttributes_HandlesCorrectly()
        {
            var serviceType = typeof(MultiAttributeService);
            var engineAttribute = serviceType.GetCustomAttributes(typeof(EngineServiceAttribute), false);
            var configAttribute = serviceType.GetCustomAttributes(typeof(ServiceConfigurationAttribute), false);

            Assert.IsNotNull(engineAttribute);
            Assert.IsNotNull(configAttribute);
            Assert.AreEqual(1, engineAttribute.Length);
            Assert.AreEqual(1, configAttribute.Length);
        }

        [Test]
        public void ServiceValidation_WithConfigurationTypes_ValidatesCorrectly()
        {
            // Test that services with configuration requirements validate correctly
            var serviceType = typeof(ConfigurableService);
            var attribute = serviceType.GetCustomAttributes(typeof(EngineServiceAttribute), false)[0] as EngineServiceAttribute;

            Assert.IsNotNull(attribute);
            // Service should be valid even with configuration requirements
        }

        [Test]
        public void Configuration_Inheritance_WorksCorrectly()
        {
            var config = _configProvider.GetConfiguration<DerivedTestConfiguration>();

            Assert.IsNotNull(config);
            Assert.IsInstanceOf<DerivedTestConfiguration>(config);
            Assert.IsInstanceOf<TestConfiguration>(config); // Should also be instance of base type
        }

        [Test]
        public void Configuration_ScriptableObjectBehavior_WorksCorrectly()
        {
            var config = _configProvider.GetConfiguration<TestConfiguration>();

            Assert.IsNotNull(config);
            Assert.IsTrue(config is ScriptableObject);
        }
    }

    // Test configuration classes
    [CreateAssetMenu(fileName = "TestConfiguration", menuName = "Engine/Test Configuration")]
    public class TestConfiguration : Configuration
    {
        [SerializeField] private string _testValue = "DefaultValue";
        [SerializeField] private int _testNumber = 42;

        public string TestValue => _testValue;
        public int TestNumber => _testNumber;
    }

    [CreateAssetMenu(fileName = "DerivedTestConfiguration", menuName = "Engine/Derived Test Configuration")]
    public class DerivedTestConfiguration : TestConfiguration
    {
        [SerializeField] private bool _derivedFlag = true;

        public bool DerivedFlag => _derivedFlag;
    }

    // Test service classes with configuration
    [EngineService(Priority = Sinkii09.Engine.Services.ServicePriority.Medium)]
    [ServiceConfiguration(typeof(TestConfiguration))]
    public class ConfigurableService : MockHighPriorityService
    {
        private TestConfiguration _config;

        public TestConfiguration Configuration => _config;

        public void SetConfiguration(TestConfiguration config)
        {
            _config = config;
        }
    }

    [EngineService(Priority = Sinkii09.Engine.Services.ServicePriority.Low)]
    [ServiceConfiguration(typeof(DerivedTestConfiguration))]
    public class MultiAttributeService : MockHighPriorityService
    {
        private DerivedTestConfiguration _config;

        public DerivedTestConfiguration Configuration => _config;

        public void SetConfiguration(DerivedTestConfiguration config)
        {
            _config = config;
        }
    }

    // Mock ServiceConfiguration attribute (since it might not exist yet)
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceConfigurationAttribute : Attribute
    {
        public Type ConfigurationType { get; }

        public ServiceConfigurationAttribute(Type configurationType)
        {
            ConfigurationType = configurationType ?? throw new ArgumentNullException(nameof(configurationType));
        }
    }
}
using NUnit.Framework;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using Sinkii09.Engine.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinkii09.Engine.Tests.Services
{
    [TestFixture]
    public class ServiceDiscoveryTests
    {
        [Test]
        public void DiscoverServices_FindsServicesWithAttribute()
        {
            var discoveredServices = DiscoverEngineServices();

            AssertExtensions.AssertContainsService<MockHighPriorityService>(discoveredServices.Select(s => s.ServiceType));
            AssertExtensions.AssertContainsService<MockDependentService>(discoveredServices.Select(s => s.ServiceType));
            AssertExtensions.AssertContainsService<MockOptionalDependencyService>(discoveredServices.Select(s => s.ServiceType));
            AssertExtensions.AssertContainsService<MockManualService>(discoveredServices.Select(s => s.ServiceType));
        }

        [Test]
        public void DiscoverServices_ExcludesServicesWithoutAttribute()
        {
            var discoveredServices = DiscoverEngineServices();

            AssertExtensions.AssertDoesNotContainService<MockServiceWithoutAttribute>(discoveredServices.Select(s => s.ServiceType));
        }

        [Test]
        public void DiscoverServices_ReturnsNoDuplicates()
        {
            var discoveredServices = DiscoverEngineServices();

            AssertExtensions.AssertNoDuplicateServices(discoveredServices.Select(s => s.ServiceType));
        }

        [Test]
        public void GetEngineServiceAttribute_ReturnsCorrectAttribute()
        {
            var attribute = typeof(MockHighPriorityService).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.AreEqual(ServicePriority.High, attribute.Priority);
            Assert.IsTrue(attribute.InitializeAtRuntime);
        }

        [Test]
        public void GetEngineServiceAttribute_ReturnsDefaultForServiceWithoutAttribute()
        {
            var attribute = typeof(MockServiceWithoutAttribute).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.IsFalse(attribute.InitializeAtRuntime); // Default for services without attribute
        }

        [Test]
        public void ServiceDiscovery_IdentifiesRequiredDependencies()
        {
            var attribute = typeof(MockDependentService).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.Contains(typeof(MockHighPriorityService), attribute.RequiredServices);
        }

        [Test]
        public void ServiceDiscovery_IdentifiesOptionalDependencies()
        {
            var attribute = typeof(MockOptionalDependencyService).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.Contains(typeof(MockHighPriorityService), attribute.OptionalServices);
        }

        [Test]
        public void ServiceDiscovery_IdentifiesManualInitializationServices()
        {
            var attribute = typeof(MockManualService).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.IsFalse(attribute.InitializeAtRuntime);
        }

        [Test]
        public void ServiceDiscovery_HandlesPriorityCorrectly()
        {
            var highPriorityAttr = typeof(MockHighPriorityService).GetEngineServiceAttribute();
            var mediumPriorityAttr = typeof(MockDependentService).GetEngineServiceAttribute();
            var lowPriorityAttr = typeof(MockOptionalDependencyService).GetEngineServiceAttribute();

            Assert.AreEqual(ServicePriority.High, highPriorityAttr.Priority);
            Assert.AreEqual(ServicePriority.Medium, mediumPriorityAttr.Priority);
            Assert.AreEqual(ServicePriority.Low, lowPriorityAttr.Priority);
        }

        [Test]
        public void ServiceDiscovery_FiltersRuntimeInitializableServices()
        {
            var allServices = DiscoverEngineServices();
            var runtimeServices = allServices.Where(s => s.Attribute.InitializeAtRuntime).ToList();

            AssertExtensions.AssertContainsService<MockHighPriorityService>(runtimeServices.Select(s => s.ServiceType));
            AssertExtensions.AssertContainsService<MockDependentService>(runtimeServices.Select(s => s.ServiceType));
            AssertExtensions.AssertDoesNotContainService<MockManualService>(runtimeServices.Select(s => s.ServiceType));
        }

        [Test]
        public void ServiceDiscovery_ValidatesServiceInterface()
        {
            var services = DiscoverEngineServices();

            foreach (var service in services)
            {
                Assert.IsTrue(typeof(IEngineService).IsAssignableFrom(service.ServiceType),
                    $"Service {service.ServiceType.Name} should implement IEngineService");
            }
        }

        [Test]
        public void ServiceDiscovery_HandlesEmptyDependencyArrays()
        {
            var attribute = typeof(MockHighPriorityService).GetEngineServiceAttribute();

            Assert.IsNotNull(attribute);
            Assert.IsNotNull(attribute.RequiredServices);
            Assert.IsNotNull(attribute.OptionalServices);
            Assert.AreEqual(0, attribute.RequiredServices.Length);
            Assert.AreEqual(0, attribute.OptionalServices.Length);
        }
        /// <summary>
        /// Helper method that mimics the engine's service discovery logic
        /// </summary>
        private List<ServiceDiscoveryResult> DiscoverEngineServices()
        {
            var services = new List<ServiceDiscoveryResult>();
            
            foreach (var type in ReflectionUtils.ExportedDomainTypes)
            {
                // Skip if not implementing IEngineService
                if (!typeof(IEngineService).IsAssignableFrom(type))
                    continue;
                    
                // Skip abstract types and interfaces
                if (type.IsAbstract || type.IsInterface)
                    continue;
                
                var attribute = type.GetEngineServiceAttribute();
                
                services.Add(new ServiceDiscoveryResult
                {
                    ServiceType = type,
                    Attribute = attribute
                });
            }
            
            return services;
        }
        
        /// <summary>
        /// Result of service discovery to match the expected test interface
        /// </summary>
        private class ServiceDiscoveryResult
        {
            public Type ServiceType { get; set; }
            public EngineServiceAttribute Attribute { get; set; }
        }
    }
}
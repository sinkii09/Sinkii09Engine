using NUnit.Framework;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Tests.TestHelpers;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Tests.Services
{
    [TestFixture]
    public class ServiceValidationTests
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
            _container = null;
        }

        [Test]
        public void ValidateService_ValidService_ReturnsTrue()
        {
            var serviceType = typeof(MockHighPriorityService);
            var attribute = serviceType.GetEngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithoutIEngineService_ReturnsFalse()
        {
            var serviceType = typeof(InvalidServiceWithoutInterface);
            var attribute = new EngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateService_AbstractService_ReturnsFalse()
        {
            var serviceType = typeof(AbstractService);
            var attribute = new EngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithoutPublicConstructor_ReturnsFalse()
        {
            var serviceType = typeof(ServiceWithoutPublicConstructor);
            var attribute = new EngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithInvalidDependencies_ReturnsFalse()
        {
            var serviceType = typeof(ServiceWithInvalidDependencies);
            var attribute = new EngineServiceAttribute
            {
                RequiredServices = new[] { typeof(string) } // Invalid dependency type
            };

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithValidDependencies_ReturnsTrue()
        {
            var serviceType = typeof(MockDependentService);
            var attribute = serviceType.GetEngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithCircularDependency_ReturnsFalse()
        {
            // Register both services first
            _container.RegisterService<CircularDependencyServiceA, CircularDependencyServiceA>();
            _container.RegisterService<CircularDependencyServiceB, CircularDependencyServiceB>();

            var dependencyGraph = _container.BuildDependencyGraph();
            var hasCircularDependency = dependencyGraph.HasCircularDependencies;

            Assert.IsTrue(hasCircularDependency);
        }

        [Test]
        public void ValidateService_ServiceWithOptionalDependencies_ReturnsTrue()
        {
            var serviceType = typeof(MockOptionalDependencyService);
            var attribute = serviceType.GetEngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithMultipleDependencies_ReturnsTrue()
        {
            var serviceType = typeof(ServiceWithMultipleDependencies);
            var attribute = serviceType.GetEngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithSelfDependency_ReturnsFalse()
        {
            var serviceType = typeof(ServiceWithSelfDependency);
            var attribute = serviceType.GetEngineServiceAttribute();

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateService_ServiceWithNullDependencyArray_ReturnsTrue()
        {
            var serviceType = typeof(MockHighPriorityService);
            var attribute = new EngineServiceAttribute
            {
                RequiredServices = null,
                OptionalServices = null
            };

            var isValid = ValidateServiceForRegistration(serviceType, attribute);

            Assert.IsTrue(isValid);
        }

        private bool ValidateServiceForRegistration(Type serviceType, EngineServiceAttribute attribute)
        {
            // Validate service implements IEngineService
            if (!typeof(IEngineService).IsAssignableFrom(serviceType))
            {
                Debug.LogError($"Service {serviceType.Name} does not implement IEngineService");
                return false;
            }

            // Validate service is not abstract
            if (serviceType.IsAbstract)
            {
                Debug.LogError($"Service {serviceType.Name} is abstract and cannot be instantiated");
                return false;
            }

            // Validate service has public constructor
            var constructors = serviceType.GetConstructors();
            if (constructors.Length == 0)
            {
                Debug.LogError($"Service {serviceType.Name} has no public constructors");
                return false;
            }

            // Validate dependencies
            if (attribute.RequiredServices != null)
            {
                foreach (var dependency in attribute.RequiredServices)
                {
                    if (dependency == serviceType)
                    {
                        Debug.LogError($"Service {serviceType.Name} has circular dependency on itself");
                        return false;
                    }

                    if (!typeof(IEngineService).IsAssignableFrom(dependency))
                    {
                        Debug.LogError($"Service {serviceType.Name} has invalid required dependency {dependency.Name}");
                        return false;
                    }
                }
            }

            if (attribute.OptionalServices != null)
            {
                foreach (var dependency in attribute.OptionalServices)
                {
                    if (dependency == serviceType)
                    {
                        Debug.LogError($"Service {serviceType.Name} has circular dependency on itself");
                        return false;
                    }

                    if (!typeof(IEngineService).IsAssignableFrom(dependency))
                    {
                        Debug.LogError($"Service {serviceType.Name} has invalid optional dependency {dependency.Name}");
                        return false;
                    }
                }
            }

            return true;
        }
    }

    // Test helper classes
    public class InvalidServiceWithoutInterface
    {
        // This class doesn't implement IEngineService
    }

    public abstract class AbstractService : IEngineService
    {

        public abstract Cysharp.Threading.Tasks.UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, System.Threading.CancellationToken cancellationToken = default);
        public abstract Cysharp.Threading.Tasks.UniTask<ServiceShutdownResult> ShutdownAsync(System.Threading.CancellationToken cancellationToken = default);
        public abstract Cysharp.Threading.Tasks.UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default);
    }

    public class ServiceWithoutPublicConstructor : IEngineService
    {
        private ServiceWithoutPublicConstructor() { }


        public async Cysharp.Threading.Tasks.UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceShutdownResult> ShutdownAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceHealthStatus.Healthy();
        }
    }

    [EngineService]
    public class ServiceWithInvalidDependencies : IEngineService
    {

        public async Cysharp.Threading.Tasks.UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceShutdownResult> ShutdownAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceHealthStatus.Healthy();
        }
    }

    [EngineService(RequiredServices = new[] { typeof(MockHighPriorityService), typeof(MockDependentService) })]
    public class ServiceWithMultipleDependencies : IEngineService
    {

        public async Cysharp.Threading.Tasks.UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceShutdownResult> ShutdownAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceHealthStatus.Healthy();
        }
    }

    [EngineService(RequiredServices = new[] { typeof(ServiceWithSelfDependency) })]
    public class ServiceWithSelfDependency : IEngineService
    {

        public async Cysharp.Threading.Tasks.UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceInitializationResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceShutdownResult> ShutdownAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceShutdownResult.Success();
        }

        public async Cysharp.Threading.Tasks.UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            return ServiceHealthStatus.Healthy();
        }
    }
}
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Tests.TestHelpers
{
    public static class AssertExtensions
    {
        public static void AssertServiceState(ServiceLifecycleManager lifecycleManager, Type serviceType, ServiceState expectedState, string message = null)
        {
            var actualState = lifecycleManager.GetServiceState(serviceType);
            Assert.AreEqual(expectedState, actualState, message ?? $"Service {serviceType.Name} should be in {expectedState} state but was in {actualState} state");
        }

        public static void AssertServiceState<T>(ServiceLifecycleManager lifecycleManager, ServiceState expectedState, string message = null)
            where T : class, IEngineService
        {
            AssertServiceState(lifecycleManager, typeof(T), expectedState, message);
        }

        public static void AssertServiceInitialized<T>(ServiceLifecycleManager lifecycleManager, string message = null)
            where T : class, IEngineService
        {
            AssertServiceState<T>(lifecycleManager, ServiceState.Running, message ?? $"Service {typeof(T).Name} should be initialized and running");
        }

        public static void AssertServiceShutdown<T>(ServiceLifecycleManager lifecycleManager, string message = null)
            where T : class, IEngineService
        {
            AssertServiceState<T>(lifecycleManager, ServiceState.Shutdown, message ?? $"Service {typeof(T).Name} should be shutdown");
        }

        public static void AssertServiceInError<T>(ServiceLifecycleManager lifecycleManager, string message = null)
            where T : class, IEngineService
        {
            AssertServiceState<T>(lifecycleManager, ServiceState.Error, message ?? $"Service {typeof(T).Name} should be in error state");
        }

        // Legacy overloads for backward compatibility (will be removed after tests are updated)
        [Obsolete("Use overload with ServiceLifecycleManager parameter")]
        public static void AssertServiceInitialized(IEngineService service, string message = null)
        {
            throw new NotSupportedException("Service state is now managed by ServiceLifecycleManager. Use AssertServiceInitialized<T>(ServiceLifecycleManager, string) instead.");
        }

        [Obsolete("Use overload with ServiceLifecycleManager parameter")]
        public static void AssertServiceShutdown(IEngineService service, string message = null)
        {
            throw new NotSupportedException("Service state is now managed by ServiceLifecycleManager. Use AssertServiceShutdown<T>(ServiceLifecycleManager, string) instead.");
        }

        [Obsolete("Use overload with ServiceLifecycleManager parameter")]
        public static void AssertServiceInError(IEngineService service, string message = null)
        {
            throw new NotSupportedException("Service state is now managed by ServiceLifecycleManager. Use AssertServiceInError<T>(ServiceLifecycleManager, string) instead.");
        }

        public static void AssertInitializationOrder<T1, T2>(IEnumerable<Type> actualOrder)
            where T1 : IEngineService
            where T2 : IEngineService
        {
            var orderList = actualOrder.ToList();
            var index1 = orderList.IndexOf(typeof(T1));
            var index2 = orderList.IndexOf(typeof(T2));

            Assert.That(index1, Is.Not.EqualTo(-1), $"{typeof(T1).Name} should be in initialization order");
            Assert.That(index2, Is.Not.EqualTo(-1), $"{typeof(T2).Name} should be in initialization order");
            Assert.That(index1, Is.LessThan(index2), $"{typeof(T1).Name} should be initialized before {typeof(T2).Name}");
        }

        public static void AssertContainsService<T>(IEnumerable<Type> services, string message = null)
            where T : IEngineService
        {
            Assert.That(services, Contains.Item(typeof(T)), message ?? $"Should contain service {typeof(T).Name}");
        }

        public static void AssertDoesNotContainService<T>(IEnumerable<Type> services, string message = null)
            where T : IEngineService
        {
            CollectionAssert.DoesNotContain(services.ToList(), typeof(T), message ?? $"Should not contain service {typeof(T).Name}");
        }

        public static void AssertServiceCount(IEnumerable<Type> services, int expectedCount, string message = null)
        {
            Assert.AreEqual(expectedCount, services.Count(), message ?? $"Should contain exactly {expectedCount} services");
        }

        public static void AssertNoDuplicateServices(IEnumerable<Type> services, string message = null)
        {
            var serviceList = services.ToList();
            var uniqueServices = serviceList.Distinct().ToList();
            Assert.AreEqual(serviceList.Count, uniqueServices.Count, message ?? "Should not contain duplicate services");
        }

        public static void AssertHealthy(ServiceHealthStatus health, string message = null)
        {
            Assert.IsTrue(health.IsHealthy, message ?? $"Service should be healthy. Status: {health.StatusMessage}");
        }

        public static void AssertUnhealthy(ServiceHealthStatus health, string message = null)
        {
            Assert.IsFalse(health.IsHealthy, message ?? $"Service should be unhealthy. Status: {health.StatusMessage}");
        }
    }
}
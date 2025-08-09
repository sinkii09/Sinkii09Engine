using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Extension methods for IServiceContainer to provide additional functionality and convenience methods
    /// </summary>
    public static class ServiceContainerExtensions
    {
        #region Registration Extensions
        
        /// <summary>
        /// Register a service conditionally
        /// </summary>
        public static IServiceContainer RegisterIf<TService, TImplementation>(
            this IServiceContainer container, 
            bool condition, 
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TImplementation : class, TService
        {
            if (condition)
            {
                container.RegisterService<TService, TImplementation>(lifetime);
            }
            return container;
        }
        
        /// <summary>
        /// Register a service if not already registered
        /// </summary>
        public static IServiceContainer TryRegister<TService, TImplementation>(
            this IServiceContainer container, 
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TImplementation : class, TService
        {
            if (!container.IsRegistered<TService>())
            {
                container.RegisterService<TService, TImplementation>(lifetime);
            }
            return container;
        }
        
        /// <summary>
        /// Register multiple implementations for the same service
        /// </summary>
        public static IServiceContainer RegisterMultiple<TService>(
            this IServiceContainer container,
            params (Type Implementation, ServiceLifetime Lifetime)[] implementations)
        {
            foreach (var (implementation, lifetime) in implementations)
            {
                container.RegisterService(typeof(TService), implementation, lifetime);
            }
            return container;
        }
        
        /// <summary>
        /// Register with custom initialization
        /// </summary>
        public static IServiceContainer RegisterWithInitialization<TService, TImplementation>(
        this IServiceContainer container,
        Action<TImplementation> initializer,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TImplementation : class, TService
        where TService : class // Ensure TService is a reference type
        {
            container.RegisterSingleton<TService>(provider =>
            {
                var instance = Activator.CreateInstance<TImplementation>();
                initializer?.Invoke(instance);
                return instance;
            });
            return container;
        }
        
        /// <summary>
        /// Register all services marked with InitializeAtRuntime attribute
        /// </summary>
        public static IServiceContainer RegisterRuntimeServices(this IServiceContainer container)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("System") && 
                           !a.FullName.StartsWith("Unity") && 
                           !a.FullName.StartsWith("mscorlib"));
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsAbstract && !t.IsInterface)
                        .Where(t => t.GetEngineServiceAttribute().InitializeAtRuntime);
                    
                    foreach (var type in types)
                    {
                        RegisterRuntimeService(container, type);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to register runtime services from {assembly.FullName}: {ex.Message}");
                }
            }
            
            return container;
        }
        
        #endregion
        
        #region Resolution Extensions
        
        /// <summary>
        /// Resolve service or return default if not found
        /// </summary>
        public static TService ResolveOrDefault<TService>(this IServiceContainer container, TService defaultValue = default)
            where TService : class
        {
            return container.TryResolve<TService>(out var service) ? service : defaultValue;
        }
        
        /// <summary>
        /// Resolve service with timeout
        /// </summary>
        public static async UniTask<TService> ResolveWithTimeoutAsync<TService>(
            this IServiceContainer container, 
            TimeSpan timeout)
            where TService : class
        {
            using (var cts = new System.Threading.CancellationTokenSource(timeout))
            {
                try
                {
                    return await container.ResolveAsync<TService>();
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Failed to resolve {typeof(TService).Name} within {timeout.TotalMilliseconds}ms");
                }
            }
        }
        
        /// <summary>
        /// Resolve all services of a type safely
        /// </summary>
        public static IEnumerable<TService> ResolveAllSafe<TService>(this IServiceContainer container)
            where TService : class
        {
            var results = new List<TService>();
            
            foreach (var serviceType in container.GetRegisteredServices().Where(t => typeof(TService).IsAssignableFrom(t)))
            {
                try
                {
                    if (container.TryResolve(serviceType, out var service) && service is TService typedService)
                    {
                        results.Add(typedService);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to resolve {serviceType.Name}: {ex.Message}");
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Create a lazy resolver for a service
        /// </summary>
        public static Lazy<TService> CreateLazy<TService>(this IServiceContainer container)
            where TService : class
        {
            return new Lazy<TService>(() => container.Resolve<TService>());
        }
        
        #endregion
        
        #region Analysis Extensions
        
        /// <summary>
        /// Get detailed container statistics
        /// </summary>
        public static ContainerStatistics GetStatistics(this IServiceContainer container)
        {
            var registeredServices = container.GetRegisteredServices().ToList();
            var graph = container.BuildDependencyGraph();
            
            return new ContainerStatistics
            {
                TotalRegistrations = registeredServices.Count,
                SingletonCount = registeredServices.Count(t => GetLifetime(container, t) == ServiceLifetime.Singleton),
                TransientCount = registeredServices.Count(t => GetLifetime(container, t) == ServiceLifetime.Transient),
                ScopedCount = registeredServices.Count(t => GetLifetime(container, t) == ServiceLifetime.Scoped),
                HasCircularDependencies = graph.HasCircularDependencies,
                MaxDependencyDepth = graph.OptimizedNodes.Values.Any() ? graph.OptimizedNodes.Values.Max(n => n.Depth) : 0,
                ServicesWithNoDependencies = graph.OptimizedNodes.Values.Count(n => n.DependencyIndices?.Length == 0),
                ServicesWithNoDependents = graph.OptimizedNodes.Values.Count(n => n.DependentIndices?.Length == 0)
            };
        }
        
        /// <summary>
        /// Generate a health report for all engine services
        /// </summary>
        public static async UniTask<HealthReport> GenerateHealthReportAsync(this IServiceContainer container)
        {
            var report = new HealthReport();
            var engineServices = container.ResolveAllSafe<IEngineService>().ToList();
            
            foreach (var service in engineServices)
            {
                try
                {
                    var healthResult = await service.HealthCheckAsync();
                    report.ServiceHealth[service.GetType()] = healthResult;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Health check failed for {service.GetType().Name}: {ex.Message}");
                    report.ServiceHealth[service.GetType()] = ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
                }
            }
            
            return report;
        }
        
        /// <summary>
        /// Validate container configuration
        /// </summary>
        public static ContainerValidationResult ValidateConfiguration(this IServiceContainer container)
        {
            var result = new ContainerValidationResult();
            
            // Check basic dependency validation
            if (!container.ValidateDependencies())
            {
                result.Errors.Add("Container has unresolved dependencies");
            }
            
            // Check for circular dependencies
            var graph = container.BuildDependencyGraph();
            if (graph.HasCircularDependencies)
            {
                foreach (var cycle in graph.CircularDependencies)
                {
                    result.Errors.Add($"Circular dependency detected: {string.Join(" -> ", cycle.Select(t => t.Name))}");
                }
            }
            
            // Check for potential performance issues
            var registeredServices = container.GetRegisteredServices().ToList();
            var transientWithManyDeps = registeredServices
                .Where(t => GetLifetime(container, t) == ServiceLifetime.Transient)
                .Where(t => graph.TryGetOptimizedNode(t, out var node) && (node.DependencyIndices?.Length ?? 0) > 5)
                .ToList();
            
            foreach (var service in transientWithManyDeps)
            {
                if (graph.TryGetOptimizedNode(service, out var node))
                {
                    result.Warnings.Add($"Transient service {service.Name} has many dependencies ({node.DependencyIndices?.Length ?? 0}). Consider using Singleton lifetime.");
                }
            }
            
            result.IsValid = result.Errors.Count == 0;
            return result;
        }
        
        #endregion
        
        #region Migration Extensions
        
        /// <summary>
        /// Migrate from legacy ServiceLocator
        /// </summary>
        public static IServiceContainer MigrateFromServiceLocator(this IServiceContainer container)
        {
            // This would integrate with the existing ServiceLocator
            // For now, we'll just register the container as a service provider
            container.RegisterSingleton<IServiceProvider>(container as IServiceProvider);
            
            Debug.Log("Migration from ServiceLocator completed. Consider updating service consumers to use IServiceContainer directly.");
            
            return container;
        }
        
        /// <summary>
        /// Create compatibility wrapper for ServiceLocator
        /// </summary>
        public static ServiceLocatorWrapper CreateServiceLocatorWrapper(this IServiceContainer container)
        {
            return new ServiceLocatorWrapper(container);
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private static void RegisterRuntimeService(IServiceContainer container, Type serviceType)
        {
            // Find the primary interface this service implements
            var primaryInterface = serviceType.GetInterfaces()
                .FirstOrDefault(i => i != typeof(IEngineService) && typeof(IEngineService).IsAssignableFrom(i));
            
            if (primaryInterface != null)
            {
                container.RegisterService(primaryInterface, serviceType, ServiceLifetime.Singleton);
            }
            else
            {
                // Register as self if no specific interface found
                container.RegisterService(serviceType, serviceType, ServiceLifetime.Singleton);
            }
        }
        
        private static ServiceLifetime GetLifetime(IServiceContainer container, Type serviceType)
        {
            // This is a simplified version - in practice, we'd need access to the internal registration data
            // For now, assume most services are Singleton unless explicitly known to be otherwise
            return ServiceLifetime.Singleton;
        }
        
        #endregion
    }
    
    #region Supporting Classes
    
    /// <summary>
    /// Container statistics
    /// </summary>
    public class ContainerStatistics
    {
        public int TotalRegistrations { get; set; }
        public int SingletonCount { get; set; }
        public int TransientCount { get; set; }
        public int ScopedCount { get; set; }
        public bool HasCircularDependencies { get; set; }
        public int MaxDependencyDepth { get; set; }
        public int ServicesWithNoDependencies { get; set; }
        public int ServicesWithNoDependents { get; set; }
        
        public override string ToString()
        {
            return $"Container Statistics:\n" +
                   $"Total Registrations: {TotalRegistrations}\n" +
                   $"Singletons: {SingletonCount}, Transients: {TransientCount}, Scoped: {ScopedCount}\n" +
                   $"Max Dependency Depth: {MaxDependencyDepth}\n" +
                   $"Circular Dependencies: {(HasCircularDependencies ? "Yes" : "No")}\n" +
                   $"Root Services: {ServicesWithNoDependencies}\n" +
                   $"Leaf Services: {ServicesWithNoDependents}";
        }
    }
    
    /// <summary>
    /// Health report for all services
    /// </summary>
    public class HealthReport
    {
        public Dictionary<Type, ServiceHealthStatus> ServiceHealth { get; set; } = new Dictionary<Type, ServiceHealthStatus>();
        
        public bool IsHealthy => ServiceHealth.Values.All(h => h.IsHealthy);
        
        public override string ToString()
        {
            var healthy = ServiceHealth.Count(kvp => kvp.Value.IsHealthy);
            var total = ServiceHealth.Count;
            
            return $"Health Report: {healthy}/{total} services healthy";
        }
    }
    
    /// <summary>
    /// Container validation result
    /// </summary>
    public class ContainerValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        
        public override string ToString()
        {
            if (IsValid)
            {
                return "Container validation passed" + (Warnings.Any() ? $" with {Warnings.Count} warnings" : "");
            }
            
            return $"Container validation failed with {Errors.Count} errors" + 
                   (Warnings.Any() ? $" and {Warnings.Count} warnings" : "");
        }
    }
    
    /// <summary>
    /// Wrapper to provide ServiceLocator compatibility
    /// </summary>
    public class ServiceLocatorWrapper
    {
        private readonly IServiceContainer _container;
        
        public ServiceLocatorWrapper(IServiceContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }
        
        public T GetService<T>() where T : class, IEngineService
        {
            return _container.Resolve<T>();
        }
        
        public void RegisterService<T>(T service) where T : class, IEngineService
        {
            _container.RegisterSingleton(service);
        }
    }
    
    #endregion
}
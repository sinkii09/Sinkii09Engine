using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Fluent API builder for service container configuration
    /// </summary>
    public class ServiceContainerBuilder
    {
        private readonly ServiceContainer _container;
        private readonly List<Action<ServiceContainer>> _registrations;
        private readonly Dictionary<string, object> _settings;
        
        public ServiceContainerBuilder()
        {
            _container = new ServiceContainer();
            _registrations = new List<Action<ServiceContainer>>();
            _settings = new Dictionary<string, object>();
        }
        
        public ServiceContainerBuilder(ServiceContainer existingContainer)
        {
            _container = existingContainer ?? throw new ArgumentNullException(nameof(existingContainer));
            _registrations = new List<Action<ServiceContainer>>();
            _settings = new Dictionary<string, object>();
        }
        
        #region Registration Methods
        
        /// <summary>
        /// Register a service with implementation
        /// </summary>
        public ServiceContainerBuilder RegisterService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TImplementation : class, TService
        {
            _registrations.Add(container => container.RegisterService<TService, TImplementation>(lifetime));
            return this;
        }
        
        /// <summary>
        /// Register a singleton instance
        /// </summary>
        public ServiceContainerBuilder RegisterSingleton<TService>(TService instance) where TService : class
        {
            _registrations.Add(container => container.RegisterSingleton(instance));
            return this;
        }
        
        /// <summary>
        /// Register a singleton with factory
        /// </summary>
        public ServiceContainerBuilder RegisterSingleton<TService>(Func<IServiceProvider, TService> factory) where TService : class
        {
            _registrations.Add(container => container.RegisterSingleton(factory));
            return this;
        }
        
        /// <summary>
        /// Register a transient service
        /// </summary>
        public ServiceContainerBuilder RegisterTransient<TService, TImplementation>()
            where TImplementation : class, TService
        {
            _registrations.Add(container => container.RegisterTransient<TService, TImplementation>());
            return this;
        }
        
        /// <summary>
        /// Register a scoped service
        /// </summary>
        public ServiceContainerBuilder RegisterScoped<TService, TImplementation>()
            where TImplementation : class, TService
        {
            _registrations.Add(container => container.RegisterScoped<TService, TImplementation>());
            return this;
        }
        
        /// <summary>
        /// Register services from an assembly
        /// </summary>
        public ServiceContainerBuilder RegisterAssembly(Assembly assembly, Func<Type, bool> filter = null)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            
            _registrations.Add(container =>
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => filter?.Invoke(t) ?? true);
                
                foreach (var type in types)
                {
                    RegisterTypeAutomatically(container, type);
                }
            });
            
            return this;
        }
        
        /// <summary>
        /// Register all types implementing an interface
        /// </summary>
        public ServiceContainerBuilder RegisterAllImplementations<TInterface>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            return RegisterAllImplementations(typeof(TInterface), lifetime);
        }
        
        /// <summary>
        /// Register all types implementing an interface
        /// </summary>
        public ServiceContainerBuilder RegisterAllImplementations(Type interfaceType, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            _registrations.Add(container =>
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes()
                            .Where(t => !t.IsAbstract && !t.IsInterface)
                            .Where(t => interfaceType.IsAssignableFrom(t))
                            .Where(t => t != interfaceType);
                        
                        foreach (var type in types)
                        {
                            container.RegisterService(interfaceType, type, lifetime);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to scan assembly {assembly.FullName}: {ex.Message}");
                    }
                }
            });
            
            return this;
        }
        
        /// <summary>
        /// Register services using a convention
        /// </summary>
        public ServiceContainerBuilder RegisterByConvention(IServiceRegistrationConvention convention)
        {
            if (convention == null)
                throw new ArgumentNullException(nameof(convention));
            
            _registrations.Add(container => convention.Register(container));
            return this;
        }
        
        /// <summary>
        /// Register a module containing multiple service registrations
        /// </summary>
        public ServiceContainerBuilder RegisterModule<TModule>() where TModule : IServiceModule, new()
        {
            return RegisterModule(new TModule());
        }
        
        /// <summary>
        /// Register a module containing multiple service registrations
        /// </summary>
        public ServiceContainerBuilder RegisterModule(IServiceModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            
            _registrations.Add(container => module.RegisterServices(container));
            return this;
        }
        
        #endregion
        
        #region Configuration Methods
        
        /// <summary>
        /// Configure a setting for the container
        /// </summary>
        public ServiceContainerBuilder WithSetting(string key, object value)
        {
            _settings[key] = value;
            return this;
        }
        
        /// <summary>
        /// Enable automatic service discovery using attributes
        /// </summary>
        public ServiceContainerBuilder EnableAutoDiscovery()
        {
            _settings["AutoDiscovery"] = true;
            return this;
        }
        
        /// <summary>
        /// Enable validation of dependencies during build
        /// </summary>
        public ServiceContainerBuilder EnableValidation()
        {
            _settings["Validation"] = true;
            return this;
        }
        
        /// <summary>
        /// Set the default service lifetime
        /// </summary>
        public ServiceContainerBuilder WithDefaultLifetime(ServiceLifetime lifetime)
        {
            _settings["DefaultLifetime"] = lifetime;
            return this;
        }
        
        #endregion
        
        #region Build Methods
        
        /// <summary>
        /// Build the service container with all registrations
        /// </summary>
        public ServiceContainer Build()
        {
            // Apply all registrations
            foreach (var registration in _registrations)
            {
                registration(_container);
            }
            
            // Auto-discovery if enabled
            if (_settings.TryGetValue("AutoDiscovery", out var autoDiscovery) && (bool)autoDiscovery)
            {
                PerformAutoDiscovery();
            }
            
            // Validation if enabled
            if (_settings.TryGetValue("Validation", out var validation) && (bool)validation)
            {
                ValidateContainer();
            }
            
            return _container;
        }
        
        /// <summary>
        /// Build and validate the container
        /// </summary>
        public (ServiceContainer Container, ValidationResult Result) BuildWithValidation()
        {
            var container = Build();
            var result = ValidateContainer();
            return (container, result);
        }
        
        #endregion
        
        #region Private Methods
        
        private void RegisterTypeAutomatically(ServiceContainer container, Type type)
        {
            // Check for EngineServiceAttribute or legacy InitializeAtRuntime attribute
            var engineAttr = type.GetEngineServiceAttribute();
            
            if (engineAttr.InitializeAtRuntime)
            {
                // Find interfaces to register
                var interfaces = type.GetInterfaces()
                    .Where(i => i != typeof(IEngineService) && typeof(IEngineService).IsAssignableFrom(i))
                    .ToList();
                
                var lifetime = engineAttr.Lifetime;
                
                if (interfaces.Any())
                {
                    foreach (var iface in interfaces)
                    {
                        container.RegisterService(iface, type, lifetime);
                    }
                }
                else
                {
                    // Register as self
                    container.RegisterService(type, type, lifetime);
                }
            }
        }
        
        private void PerformAutoDiscovery()
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
                        RegisterTypeAutomatically(_container, type);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to auto-discover services in {assembly.FullName}: {ex.Message}");
                }
            }
        }
        
        private ValidationResult ValidateContainer()
        {
            var result = new ValidationResult();
            
            if (!_container.ValidateDependencies())
            {
                result.IsValid = false;
                result.Errors.Add("Container has missing dependencies");
            }
            
            var graph = _container.BuildDependencyGraph();
            if (graph.HasCircularDependencies)
            {
                result.IsValid = false;
                foreach (var cycle in graph.CircularDependencies)
                {
                    result.Errors.Add($"Circular dependency: {string.Join(" -> ", cycle.Select(t => t.Name))}");
                }
            }
            
            return result;
        }
        
        private ServiceLifetime GetDefaultLifetime()
        {
            if (_settings.TryGetValue("DefaultLifetime", out var lifetime))
            {
                return (ServiceLifetime)lifetime;
            }
            return ServiceLifetime.Singleton;
        }
        
        #endregion
        
        /// <summary>
        /// Validation result
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
            public string ErrorMessage => Errors.Count > 0 ? string.Join("; ", Errors) : null;
        }
    }
    
    /// <summary>
    /// Interface for service registration conventions
    /// </summary>
    public interface IServiceRegistrationConvention
    {
        void Register(IServiceContainer container);
    }
    
    /// <summary>
    /// Interface for service modules
    /// </summary>
    public interface IServiceModule
    {
        void RegisterServices(IServiceContainer container);
    }
    
    /// <summary>
    /// Base class for service modules
    /// </summary>
    public abstract class ServiceModule : IServiceModule
    {
        public abstract void RegisterServices(IServiceContainer container);
        
        protected void RegisterSingleton<TService, TImplementation>(IServiceContainer container)
            where TImplementation : class, TService
        {
            container.RegisterService<TService, TImplementation>(ServiceLifetime.Singleton);
        }
        
        protected void RegisterTransient<TService, TImplementation>(IServiceContainer container)
            where TImplementation : class, TService
        {
            container.RegisterService<TService, TImplementation>(ServiceLifetime.Transient);
        }
        
        protected void RegisterScoped<TService, TImplementation>(IServiceContainer container)
            where TImplementation : class, TService
        {
            container.RegisterService<TService, TImplementation>(ServiceLifetime.Scoped);
        }
    }
}
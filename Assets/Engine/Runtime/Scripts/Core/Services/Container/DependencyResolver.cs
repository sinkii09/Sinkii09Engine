using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Advanced dependency resolver with support for complex scenarios
    /// </summary>
    public class DependencyResolver
    {
        private readonly IServiceContainer _container;
        private readonly Dictionary<Type, ResolutionStrategy> _strategies;
        
        public DependencyResolver(IServiceContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _strategies = new Dictionary<Type, ResolutionStrategy>();
            InitializeDefaultStrategies();
        }
        
        /// <summary>
        /// Resolution strategy for custom type resolution
        /// </summary>
        public delegate object ResolutionStrategy(Type serviceType, IServiceProvider provider);
        
        /// <summary>
        /// Register a custom resolution strategy for a type
        /// </summary>
        public void RegisterStrategy(Type serviceType, ResolutionStrategy strategy)
        {
            _strategies[serviceType] = strategy;
        }
        
        /// <summary>
        /// Resolve a service with advanced dependency injection
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T));
        }
        
        /// <summary>
        /// Resolve a service with advanced dependency injection
        /// </summary>
        public object Resolve(Type serviceType)
        {
            // Check for custom strategy
            if (_strategies.TryGetValue(serviceType, out var strategy))
            {
                return strategy(serviceType, _container as IServiceProvider);
            }
            
            // Try container resolution
            if (_container.IsRegistered(serviceType))
            {
                return _container.Resolve(serviceType);
            }
            
            // Try auto-resolution for concrete types
            if (!serviceType.IsAbstract && !serviceType.IsInterface)
            {
                return AutoResolve(serviceType);
            }
            
            throw new InvalidOperationException($"Unable to resolve service of type {serviceType.Name}");
        }
        
        /// <summary>
        /// Resolve all implementations of a service type
        /// </summary>
        public IEnumerable<T> ResolveAll<T>() where T : class
        {
            return ResolveAll(typeof(T)).Cast<T>();
        }
        
        /// <summary>
        /// Resolve all implementations of a service type
        /// </summary>
        public IEnumerable<object> ResolveAll(Type serviceType)
        {
            var results = new List<object>();
            
            // Find all registered types that implement the service type
            foreach (var registeredType in _container.GetRegisteredServices())
            {
                if (serviceType.IsAssignableFrom(registeredType))
                {
                    try
                    {
                        var instance = _container.Resolve(registeredType);
                        if (instance != null)
                        {
                            results.Add(instance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to resolve {registeredType.Name}: {ex.Message}");
                    }
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Resolve with optional dependencies handled gracefully
        /// </summary>
        public T ResolveWithOptionals<T>() where T : class
        {
            return (T)ResolveWithOptionals(typeof(T));
        }
        
        /// <summary>
        /// Resolve with optional dependencies handled gracefully
        /// </summary>
        public object ResolveWithOptionals(Type serviceType)
        {
            try
            {
                return Resolve(serviceType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to resolve {serviceType.Name} with all dependencies: {ex.Message}");
                
                // Try to create with optional dependencies as null
                return AutoResolveWithOptionals(serviceType);
            }
        }
        
        /// <summary>
        /// Create a factory function for lazy resolution
        /// </summary>
        public Func<T> CreateFactory<T>() where T : class
        {
            return () => Resolve<T>();
        }
        
        /// <summary>
        /// Create a factory function for lazy resolution with parameters
        /// </summary>
        public Func<object[], T> CreateParameterizedFactory<T>() where T : class
        {
            var type = typeof(T);
            return (parameters) =>
            {
                var constructors = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .ToArray();
                
                foreach (var constructor in constructors)
                {
                    var ctorParams = constructor.GetParameters();
                    if (ctorParams.Length == parameters.Length)
                    {
                        try
                        {
                            return (T)constructor.Invoke(parameters);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                
                throw new InvalidOperationException($"No suitable constructor found for {type.Name} with {parameters.Length} parameters");
            };
        }
        
        /// <summary>
        /// Validate that all dependencies can be resolved
        /// </summary>
        public DependencyValidationResult ValidateDependencies(Type serviceType)
        {
            var result = new DependencyValidationResult
            {
                ServiceType = serviceType,
                IsValid = true,
                MissingDependencies = new List<Type>(),
                OptionalDependencies = new List<Type>()
            };
            
            // Check constructor dependencies
            var constructors = serviceType.GetConstructors();
            if (constructors.Length > 0)
            {
                var validConstructor = false;
                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();
                    var canConstruct = true;
                    
                    foreach (var param in parameters)
                    {
                        if (!_container.IsRegistered(param.ParameterType) && !param.HasDefaultValue)
                        {
                            canConstruct = false;
                            result.MissingDependencies.Add(param.ParameterType);
                        }
                    }
                    
                    if (canConstruct)
                    {
                        validConstructor = true;
                        break;
                    }
                }
                
                if (!validConstructor)
                {
                    result.IsValid = false;
                }
            }
            
            return result;
        }
        
        private object AutoResolve(Type serviceType)
        {
            var constructors = serviceType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();
            
            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = ResolveConstructorParameters(constructor);
                    return Activator.CreateInstance(serviceType, parameters);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to auto-resolve {serviceType.Name} using constructor: {ex.Message}");
                }
            }
            
            throw new InvalidOperationException($"Unable to auto-resolve {serviceType.Name}. No suitable constructor found.");
        }
        
        private object AutoResolveWithOptionals(Type serviceType)
        {
            var constructors = serviceType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();
            
            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = ResolveConstructorParametersWithOptionals(constructor);
                    return Activator.CreateInstance(serviceType, parameters);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to auto-resolve {serviceType.Name} with optionals: {ex.Message}");
                }
            }
            
            // Try parameterless constructor
            try
            {
                return Activator.CreateInstance(serviceType);
            }
            catch
            {
                throw new InvalidOperationException($"Unable to create instance of {serviceType.Name}");
            }
        }
        
        private object[] ResolveConstructorParameters(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];
            
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                
                if (_container.TryResolve(param.ParameterType, out var instance))
                {
                    parameterInstances[i] = instance;
                }
                else if (param.HasDefaultValue)
                {
                    parameterInstances[i] = param.DefaultValue;
                }
                else
                {
                    throw new InvalidOperationException($"Unable to resolve parameter {param.Name} of type {param.ParameterType.Name}");
                }
            }
            
            return parameterInstances;
        }
        
        private object[] ResolveConstructorParametersWithOptionals(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];
            
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                
                if (_container.TryResolve(param.ParameterType, out var instance))
                {
                    parameterInstances[i] = instance;
                }
                else if (param.HasDefaultValue)
                {
                    parameterInstances[i] = param.DefaultValue;
                }
                else if (!param.ParameterType.IsValueType)
                {
                    // For reference types, we can use null for optional dependencies
                    parameterInstances[i] = null;
                }
                else
                {
                    // For value types, use default value
                    parameterInstances[i] = Activator.CreateInstance(param.ParameterType);
                }
            }
            
            return parameterInstances;
        }
        
        private void InitializeDefaultStrategies()
        {
            // Strategy for IEnumerable<T>
            RegisterStrategy(typeof(IEnumerable<>), (type, provider) =>
            {
                var elementType = type.GetGenericArguments()[0];
                var resolveAllMethod = GetType().GetMethod(nameof(ResolveAll), new[] { typeof(Type) });
                return resolveAllMethod.Invoke(this, new object[] { elementType });
            });
            
            // Strategy for Lazy<T>
            RegisterStrategy(typeof(Lazy<>), (type, provider) =>
            {
                var serviceType = type.GetGenericArguments()[0];
                var lazyType = typeof(Lazy<>).MakeGenericType(serviceType);
                var factoryType = typeof(Func<>).MakeGenericType(serviceType);
                
                var factory = Delegate.CreateDelegate(factoryType, this, 
                    GetType().GetMethod(nameof(Resolve), Type.EmptyTypes).MakeGenericMethod(serviceType));
                
                return Activator.CreateInstance(lazyType, factory);
            });
            
            // Strategy for Func<T>
            RegisterStrategy(typeof(Func<>), (type, provider) =>
            {
                var serviceType = type.GetGenericArguments()[0];
                var method = GetType().GetMethod(nameof(CreateFactory)).MakeGenericMethod(serviceType);
                return method.Invoke(this, null);
            });
        }
    }
    
    /// <summary>
    /// Result of dependency validation
    /// </summary>
    public class DependencyValidationResult
    {
        public Type ServiceType { get; set; }
        public bool IsValid { get; set; }
        public List<Type> MissingDependencies { get; set; }
        public List<Type> OptionalDependencies { get; set; }
        
        public override string ToString()
        {
            if (IsValid)
            {
                return $"{ServiceType.Name}: All dependencies can be resolved";
            }
            
            return $"{ServiceType.Name}: Missing dependencies - {string.Join(", ", MissingDependencies.Select(t => t.Name))}";
        }
    }
}
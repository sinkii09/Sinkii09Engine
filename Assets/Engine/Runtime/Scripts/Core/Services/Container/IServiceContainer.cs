using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Service container interface providing dependency injection and service lifecycle management
    /// </summary>
    public interface IServiceContainer : IDisposable
    {
        #region Registration Methods
        
        /// <summary>
        /// Register a service with its implementation type
        /// </summary>
        void RegisterService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton) 
            where TImplementation : class, TService;
            
        /// <summary>
        /// Register a service with its implementation type (non-generic)
        /// </summary>
        void RegisterService(Type serviceType, Type implementationType, ServiceLifetime lifetime = ServiceLifetime.Singleton);
        
        /// <summary>
        /// Register a singleton instance
        /// </summary>
        void RegisterSingleton<TService>(TService instance) where TService : class;
        
        /// <summary>
        /// Register a singleton with factory
        /// </summary>
        void RegisterSingleton<TService>(Func<IServiceProvider, TService> factory) where TService : class;
        
        /// <summary>
        /// Register a transient service (new instance each time)
        /// </summary>
        void RegisterTransient<TService, TImplementation>() where TImplementation : class, TService;
        
        /// <summary>
        /// Register a scoped service (single instance per scope)
        /// </summary>
        void RegisterScoped<TService, TImplementation>() where TImplementation : class, TService;
        
        #endregion
        
        #region Resolution Methods
        
        /// <summary>
        /// Resolve a service instance
        /// </summary>
        TService Resolve<TService>() where TService : class;
        
        /// <summary>
        /// Resolve a service instance (non-generic)
        /// </summary>
        object Resolve(Type serviceType);
        
        /// <summary>
        /// Asynchronously resolve a service instance
        /// </summary>
        UniTask<TService> ResolveAsync<TService>() where TService : class;
        
        /// <summary>
        /// Try to resolve a service instance
        /// </summary>
        bool TryResolve<TService>(out TService service) where TService : class;
        
        /// <summary>
        /// Try to resolve a service instance (non-generic)
        /// </summary>
        bool TryResolve(Type serviceType, out object service);
        
        #endregion
        
        #region Query Methods
        
        /// <summary>
        /// Check if a service is registered
        /// </summary>
        bool IsRegistered<TService>();
        
        /// <summary>
        /// Check if a service is registered (non-generic)
        /// </summary>
        bool IsRegistered(Type serviceType);
        
        /// <summary>
        /// Get all registered service types
        /// </summary>
        IEnumerable<Type> GetRegisteredServices();
        
        #endregion
        
        #region Scope Management
        
        /// <summary>
        /// Create a new service scope for scoped lifetime services
        /// </summary>
        IServiceScope CreateScope();
        
        #endregion
        
        #region Dependency Graph
        
        /// <summary>
        /// Build a dependency graph of all registered services
        /// </summary>
        ServiceDependencyGraph BuildDependencyGraph();
        
        /// <summary>
        /// Validate all service dependencies
        /// </summary>
        bool ValidateDependencies();
        
        /// <summary>
        /// Get circular dependencies if any exist
        /// </summary>
        IEnumerable<string> GetCircularDependencies();
        
        #endregion
        
    }
    
    /// <summary>
    /// Service scope for scoped lifetime services
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        /// <summary>
        /// Service provider for this scope
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}
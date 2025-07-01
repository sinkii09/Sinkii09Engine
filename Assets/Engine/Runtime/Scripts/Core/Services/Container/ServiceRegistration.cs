using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Internal class representing a service registration in the container
    /// </summary>
    public class ServiceRegistration
    {
        /// <summary>
        /// The service type being registered
        /// </summary>
        public Type ServiceType { get; set; }
        
        /// <summary>
        /// The implementation type for the service
        /// </summary>
        public Type ImplementationType { get; set; }
        
        /// <summary>
        /// The lifetime of the service
        /// </summary>
        public ServiceLifetime Lifetime { get; set; }
        
        /// <summary>
        /// Factory method for creating the service instance
        /// </summary>
        public Func<IServiceProvider, object> Factory { get; set; }
        
        /// <summary>
        /// Singleton instance (if lifetime is Singleton and instance is created)
        /// </summary>
        public object SingletonInstance { get; set; }
        
        /// <summary>
        /// Lock object for thread-safe singleton creation
        /// </summary>
        public readonly object SingletonLock = new object();
        
        /// <summary>
        /// Dependencies required by this service
        /// </summary>
        public Type[] RequiredDependencies { get; set; }
        
        /// <summary>
        /// Optional dependencies for this service
        /// </summary>
        public Type[] OptionalDependencies { get; set; }
        
        /// <summary>
        /// Service initialization priority
        /// </summary>
        public ServicePriority Priority { get; set; }
        
        /// <summary>
        /// Whether this service implements IEngineService
        /// </summary>
        public bool IsEngineService { get; set; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ServiceRegistration()
        {
            RequiredDependencies = Array.Empty<Type>();
            OptionalDependencies = Array.Empty<Type>();
            Priority = ServicePriority.Medium;
        }
    }
}
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Comprehensive attribute for configuring engine services with all metadata in one place
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class EngineServiceAttribute : Attribute
    {
        /// <summary>
        /// Whether this service should be initialized at runtime
        /// </summary>
        public bool InitializeAtRuntime { get; set; } = true;
        
        /// <summary>
        /// Service initialization priority
        /// </summary>
        public ServicePriority Priority { get; set; } = ServicePriority.Medium;
        
        /// <summary>
        /// Required service dependencies
        /// </summary>
        public Type[] RequiredServices { get; set; } = Array.Empty<Type>();
        
        /// <summary>
        /// Optional service dependencies
        /// </summary>
        public Type[] OptionalServices { get; set; } = Array.Empty<Type>();
        
        /// <summary>
        /// Service lifetime
        /// </summary>
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
        
        /// <summary>
        /// Service description for documentation
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Whether this service supports health checks
        /// </summary>
        public bool SupportsHealthCheck { get; set; } = true;
        
        /// <summary>
        /// Whether this service can be restarted
        /// </summary>
        public bool SupportsRestart { get; set; } = true;
        
        /// <summary>
        /// Maximum initialization timeout in milliseconds (0 = no timeout)
        /// </summary>
        public int InitializationTimeout { get; set; } = 30000; // 30 seconds default
        
        /// <summary>
        /// Service category for organization and filtering
        /// </summary>
        public ServiceCategory Category { get; set; } = ServiceCategory.Core;
        
        /// <summary>
        /// Constructor with minimal required information
        /// </summary>
        public EngineServiceAttribute()
        {
        }
        
        /// <summary>
        /// Constructor with common parameters
        /// </summary>
        public EngineServiceAttribute(ServicePriority priority, params Type[] requiredServices)
        {
            Priority = priority;
            RequiredServices = requiredServices ?? Array.Empty<Type>();
        }
        
        /// <summary>
        /// Constructor with category and priority
        /// </summary>
        public EngineServiceAttribute(ServiceCategory category, ServicePriority priority = ServicePriority.Medium)
        {
            Category = category;
            Priority = priority;
        }
        
        /// <summary>
        /// Validate the attribute configuration
        /// </summary>
        public void Validate(Type serviceType)
        {
            if (RequiredServices != null)
            {
                foreach (var requiredService in RequiredServices)
                {
                    if (requiredService == null)
                        throw new ArgumentException($"Required service type cannot be null in {serviceType.Name}");
                    
                    if (!typeof(IEngineService).IsAssignableFrom(requiredService))
                        throw new ArgumentException($"Required service {requiredService.Name} must implement IEngineService in {serviceType.Name}");
                }
            }
            
            if (OptionalServices != null)
            {
                foreach (var optionalService in OptionalServices)
                {
                    if (optionalService == null)
                        throw new ArgumentException($"Optional service type cannot be null in {serviceType.Name}");
                    
                    if (!typeof(IEngineService).IsAssignableFrom(optionalService))
                        throw new ArgumentException($"Optional service {optionalService.Name} must implement IEngineService in {serviceType.Name}");
                }
            }
            
            if (InitializationTimeout < 0)
                throw new ArgumentException($"Initialization timeout cannot be negative in {serviceType.Name}");
        }
    }
    
    /// <summary>
    /// Static helper class for EngineServiceAttribute usage
    /// </summary>
    public static class EngineServiceAttributeExtensions
    {
        // Thread-local storage to prevent recursive attribute resolution
        [ThreadStatic]
        private static HashSet<Type> _typesBeingProcessed;
        
        /// <summary>
        /// Get the EngineServiceAttribute from a type, creating a default if none exists
        /// </summary>
        public static EngineServiceAttribute GetEngineServiceAttribute(this Type serviceType)
        {
            // Initialize thread-local storage if needed
            if (_typesBeingProcessed == null)
                _typesBeingProcessed = new HashSet<Type>();
            
            // Check for recursion
            if (_typesBeingProcessed.Contains(serviceType))
            {
                // Return a safe default to break recursion
                return new EngineServiceAttribute { InitializeAtRuntime = false };
            }
            
            _typesBeingProcessed.Add(serviceType);
            try
            {
                var attr = serviceType.GetCustomAttribute<EngineServiceAttribute>(false);
                if (attr != null)
                    return attr;
                
                // Check for legacy attributes and create a compatible EngineServiceAttribute
                var legacyAttr = new EngineServiceAttribute();
                
                // Note: InitializeAtRuntimeAttribute check removed as it's deprecated
                // Services without any attributes are assumed to not initialize at runtime
                legacyAttr.InitializeAtRuntime = false;
               
                return legacyAttr;
            }
            finally
            {
                _typesBeingProcessed.Remove(serviceType);
            }
        }
    }
}
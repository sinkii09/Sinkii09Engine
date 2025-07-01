using System;
using System.Reflection;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Attribute to link a service to its configuration type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceConfigurationAttribute : Attribute
    {
        /// <summary>
        /// The type of configuration this service requires
        /// </summary>
        public Type ConfigurationType { get; }
        
        /// <summary>
        /// Optional path to the configuration asset (relative to Resources folder)
        /// </summary>
        public string ConfigPath { get; set; } = "Configs/Services";
        
        /// <summary>
        /// Whether the configuration is required for the service to function
        /// </summary>
        public bool Required { get; set; } = true;
        
        /// <summary>
        /// Default configuration values as JSON (used if no asset exists and Required = false)
        /// </summary>
        public string DefaultConfigJson { get; set; }
        
        /// <summary>
        /// Create a service configuration attribute
        /// </summary>
        /// <param name="configurationType">Type of configuration (must implement IServiceConfiguration)</param>
        public ServiceConfigurationAttribute(Type configurationType)
        {
            if (configurationType == null)
                throw new ArgumentNullException(nameof(configurationType));
            
            if (!typeof(IServiceConfiguration).IsAssignableFrom(configurationType))
                throw new ArgumentException($"Configuration type {configurationType.Name} must implement IServiceConfiguration", nameof(configurationType));
            
            ConfigurationType = configurationType;
        }
    }
    
    /// <summary>
    /// Helper extensions for working with service configuration attributes
    /// </summary>
    public static class ServiceConfigurationAttributeExtensions
    {
        /// <summary>
        /// Get the configuration type for a service type
        /// </summary>
        public static Type GetConfigurationType(this Type serviceType)
        {
            var attr = serviceType.GetCustomAttribute<ServiceConfigurationAttribute>(false);
            if (attr != null)
                return attr.ConfigurationType;
            
            // Try naming convention: ServiceName + "Configuration"
            var conventionName = serviceType.Name + "Configuration";
            var conventionType = serviceType.Assembly.GetType(serviceType.Namespace + "." + conventionName);
            
            if (conventionType != null && typeof(IServiceConfiguration).IsAssignableFrom(conventionType))
                return conventionType;
            
            return null;
        }
        
        /// <summary>
        /// Get the configuration attribute for a service type
        /// </summary>
        public static ServiceConfigurationAttribute GetConfigurationAttribute(this Type serviceType)
        {
            return serviceType.GetCustomAttribute<ServiceConfigurationAttribute>(false);
        }
        
        /// <summary>
        /// Check if a service type has a configuration
        /// </summary>
        public static bool HasConfiguration(this Type serviceType)
        {
            return GetConfigurationType(serviceType) != null;
        }
        
        /// <summary>
        /// Get the configuration path for a service type
        /// </summary>
        public static string GetConfigurationPath(this Type serviceType)
        {
            var attr = GetConfigurationAttribute(serviceType);
            if (attr != null)
            {
                var configTypeName = attr.ConfigurationType.Name;
                return $"{attr.ConfigPath}/{configTypeName}";
            }
            
            // Default path using naming convention
            var conventionName = serviceType.Name + "Configuration";
            return $"Configs/Services/{conventionName}";
        }
        
        /// <summary>
        /// Check if configuration is required for a service
        /// </summary>
        public static bool IsConfigurationRequired(this Type serviceType)
        {
            var attr = GetConfigurationAttribute(serviceType);
            return attr?.Required ?? true; // Default to required
        }
    }
}
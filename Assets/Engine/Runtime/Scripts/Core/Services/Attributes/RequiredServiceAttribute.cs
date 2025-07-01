using System;
using System.Linq;

namespace Sinkii09.Engine.Services
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [Obsolete("Use EngineServiceAttribute with RequiredServices property instead. This attribute will be removed in a future version.")]
    public class RequiredServiceAttribute : Attribute
    {
        public readonly Type[] ServiceTypes;

        public RequiredServiceAttribute(params Type[] serviceTypes)
        {
            if (serviceTypes == null || serviceTypes.Length == 0)
                throw new ArgumentException("At least one service type must be specified", nameof(serviceTypes));
            
            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == null)
                    throw new ArgumentNullException(nameof(serviceTypes), "Service type cannot be null");
                
                if (!typeof(IEngineService).IsAssignableFrom(serviceType))
                    throw new ArgumentException($"Service type {serviceType.Name} must implement {nameof(IEngineService)}", nameof(serviceTypes));
            }
            
            ServiceTypes = serviceTypes;
        }
        
        // Backward compatibility property
        [Obsolete("Use ServiceTypes property instead")]
        public Type ServiceType => ServiceTypes?.FirstOrDefault();
    }
}
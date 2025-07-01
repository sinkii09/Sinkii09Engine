namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Specifies the lifetime of a service in the service container
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// Single instance for the entire application lifetime
        /// </summary>
        Singleton = 0,
        
        /// <summary>
        /// New instance created for each service resolution
        /// </summary>
        Transient = 1,
        
        /// <summary>
        /// Single instance per scope/context
        /// </summary>
        Scoped = 2
    }
}
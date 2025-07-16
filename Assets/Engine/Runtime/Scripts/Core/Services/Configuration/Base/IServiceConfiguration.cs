using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base interface for all service configurations
    /// </summary>
    public interface IServiceConfiguration
    {
        /// <summary>
        /// Validate the configuration and return any errors
        /// </summary>
        /// <param name="errors">List of validation errors</param>
        /// <returns>True if configuration is valid</returns>
        bool Validate(out List<string> errors);
        
        /// <summary>
        /// Event fired when configuration changes (for hot-reload support)
        /// </summary>
        event Action<IServiceConfiguration> ConfigurationChanged;
        
        /// <summary>
        /// Get the display name for this configuration
        /// </summary>
        string GetDisplayName();
        
        /// <summary>
        /// Get the version of this configuration for tracking changes
        /// </summary>
        int GetVersion();
    }
}
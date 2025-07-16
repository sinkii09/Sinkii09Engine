using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Script Service
    /// </summary>
    [CreateAssetMenu(fileName = "ScriptServiceConfiguration", menuName = "Engine/Services/ScriptServiceConfiguration", order = 2)]
    public class ScriptServiceConfiguration : ServiceConfigurationBase
    {
        [Header("Loading Settings")]
        [SerializeField]
        [Tooltip("Default path for loading scripts (e.g., 'Scripts' or 'Assets/Scripts')")]
        private string _defaultScriptsPath = "Scripts";

        [SerializeField]
        [Range(1, 50)]
        [Tooltip("Maximum number of scripts that can be loaded concurrently")]
        private int _maxConcurrentLoads = 10;

        [SerializeField]
        [Tooltip("Enable caching of loaded scripts for better performance")]
        private bool _enableScriptCaching = true;

        [SerializeField]
        [Range(10, 10000)]
        [Tooltip("Maximum number of scripts to keep in cache")]
        private int _maxCacheSize = 1000;


        [Header("Performance Settings")]
        [SerializeField]
        [Tooltip("Enable preloading of scripts during initialization")]
        private bool _enablePreloading = true;
        
        [SerializeField]
        [Tooltip("Comma-separated paths for scripts to preload (e.g., 'Core,Boot,Essential')")]
        private string _preloadPaths = "Core,Boot";
        
        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("Maximum time in seconds to wait for script validation")]
        private float _scriptValidationTimeout = 2f;

        [Header("Hot-Reload Settings")]
        [SerializeField]
        [Tooltip("Enable automatic hot-reload when script files change")]
        private bool _enableHotReload = true;

        [SerializeField]
        [Range(0.1f, 60f)]
        [Tooltip("Time interval in seconds to check for script file changes")]
        private float _hotReloadInterval = 1f;
        
        [Header("Error Handling Settings")]
        [SerializeField]
        [Range(0, 10)]
        [Tooltip("Maximum number of retry attempts for failed script loads")]
        private int _maxRetryAttempts = 3;
        
        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("Delay in seconds between retry attempts")]
        private float _retryDelaySeconds = 1f;
        
        [SerializeField]
        [Tooltip("Enable script validation during loading")]
        private bool _enableScriptValidation = true;

        // Public properties for accessing configuration values
        public string DefaultScriptsPath => _defaultScriptsPath;
        public int MaxConcurrentLoads => _maxConcurrentLoads;
        public bool EnableScriptCaching => _enableScriptCaching;
        public int MaxCacheSize => _maxCacheSize;
        public bool EnablePreloading => _enablePreloading;
        public string PreloadPaths => _preloadPaths;
        public float ScriptValidationTimeout => _scriptValidationTimeout;
        public bool EnableHotReload => _enableHotReload;
        public float HotReloadInterval => _hotReloadInterval;
        public int MaxRetryAttempts => _maxRetryAttempts;
        public float RetryDelaySeconds => _retryDelaySeconds;
        public bool EnableScriptValidation => _enableScriptValidation;
        
        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;
            
            // Validate default scripts path
            if (string.IsNullOrWhiteSpace(_defaultScriptsPath))
            {
                errors.Add("DefaultScriptsPath cannot be empty");
                isValid = false;
            }
            
            // Validate max concurrent loads
            if (_maxConcurrentLoads <= 0)
            {
                errors.Add("MaxConcurrentLoads must be greater than 0");
                isValid = false;
            }
            
            // Validate max cache size
            if (_enableScriptCaching && _maxCacheSize <= 0)
            {
                errors.Add("MaxCacheSize must be greater than 0 when caching is enabled");
                isValid = false;
            }
            
            // Validate preload paths
            if (_enablePreloading && string.IsNullOrWhiteSpace(_preloadPaths))
            {
                errors.Add("PreloadPaths cannot be empty when preloading is enabled");
                isValid = false;
            }
            
            // Validate script validation timeout
            if (_scriptValidationTimeout <= 0)
            {
                errors.Add("ScriptValidationTimeout must be greater than 0");
                isValid = false;
            }
            
            // Validate hot-reload interval
            if (_enableHotReload && _hotReloadInterval <= 0)
            {
                errors.Add("HotReloadInterval must be greater than 0 when hot-reload is enabled");
                isValid = false;
            }
            
            // Validate retry settings
            if (_maxRetryAttempts < 0)
            {
                errors.Add("MaxRetryAttempts cannot be negative");
                isValid = false;
            }
            
            if (_retryDelaySeconds < 0)
            {
                errors.Add("RetryDelaySeconds cannot be negative");
                isValid = false;
            }
            
            return isValid;
        }
        
        protected override void OnResetToDefaults()
        {
            _defaultScriptsPath = "Scripts";
            _maxConcurrentLoads = 10;
            _enableScriptCaching = true;
            _maxCacheSize = 1000;
            _enablePreloading = true;
            _preloadPaths = "Core,Boot";
            _scriptValidationTimeout = 2f;
            _enableHotReload = true;
            _hotReloadInterval = 1f;
            _maxRetryAttempts = 3;
            _retryDelaySeconds = 1f;
            _enableScriptValidation = true;
        }
        
        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"ScriptService Config: {_maxConcurrentLoads} concurrent loads, " +
                   $"cache {(_enableScriptCaching ? $"enabled ({_maxCacheSize})" : "disabled")}, " +
                   $"preload {(_enablePreloading ? "enabled" : "disabled")}, " +
                   $"hot-reload {(_enableHotReload ? $"every {_hotReloadInterval}s" : "disabled")}";
        }
    }
}
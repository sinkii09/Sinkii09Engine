using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Resource Service
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceServiceConfiguration", menuName = "Engine/Services/ResourceServiceConfiguration", order = 2)]
    public class ResourceServiceConfiguration : ServiceConfigurationBase
    {
        [Header("Provider Settings")]
        [SerializeField]
        [Tooltip("Types of resource providers to enable")]
        private ProviderType _enabledProviders = ProviderType.Addressable | ProviderType.Resources;
        
        [SerializeField]
        [Range(1, 50)]
        [Tooltip("Maximum number of concurrent resource loading operations")]
        private int _maxConcurrentLoads = 10;
        
        [SerializeField]
        [Range(0.1f, 1.0f)]
        [Tooltip("Memory pressure threshold that triggers automatic resource cleanup")]
        private float _memoryPressureThreshold = 0.8f;

        [Header("Performance Settings")]
        [SerializeField]
        [Tooltip("Enable resource caching for improved performance")]
        private bool _enableResourceCaching = true;
        
        [SerializeField]
        [Range(100, 10000)]
        [Tooltip("Maximum number of resources to keep in cache")]
        private int _maxCacheSize = 1000;
        
        [SerializeField]
        [Range(5f, 300f)]
        [Tooltip("Interval in seconds for automatically unloading unused resources")]
        private float _unloadUnusedResourcesInterval = 30f;
        
        [SerializeField]
        [Tooltip("Enable preloading of commonly used resources")]
        private bool _enableResourcePreloading = false;
        
        [SerializeField]
        [Tooltip("Resources to preload on service initialization")]
        private string[] _preloadResourcePaths = new string[0];

        [Header("Error Handling")]
        [SerializeField]
        [Range(0, 10)]
        [Tooltip("Maximum number of retry attempts for failed resource loads")]
        private int _maxRetryAttempts = 3;
        
        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("Delay in seconds between retry attempts")]
        private float _retryDelaySeconds = 1f;
        
        [SerializeField]
        [Tooltip("Enable circuit breaker pattern for failing providers")]
        private bool _enableCircuitBreaker = true;
        
        [SerializeField]
        [Range(3, 20)]
        [Tooltip("Number of consecutive failures before circuit breaker opens")]
        private int _circuitBreakerFailureThreshold = 5;
        
        [SerializeField]
        [Range(5f, 300f)]
        [Tooltip("Time in seconds before circuit breaker attempts to close")]
        private float _circuitBreakerTimeoutSeconds = 30f;

        [Header("Memory Management")]
        [SerializeField]
        [Tooltip("Enable automatic memory cleanup based on system memory pressure")]
        private bool _enableMemoryPressureResponse = true;
        
        [SerializeField]
        [Range(1, 100)]
        [Tooltip("Percentage of cache to clear during moderate memory pressure")]
        private int _moderateCleanupPercentage = 25;
        
        [SerializeField]
        [Range(1, 100)]
        [Tooltip("Percentage of cache to clear during high memory pressure")]
        private int _aggressiveCleanupPercentage = 75;
        
        [SerializeField]
        [Tooltip("Enable detailed logging of resource operations")]
        private bool _enableDetailedLogging = false;

        [SerializeField]
        private int _gracefulShutdownTimeoutMs = 5000;

        // Public properties for accessing configuration values
        public ProviderType EnabledProviders => _enabledProviders;
        public int MaxConcurrentLoads => _maxConcurrentLoads;
        public float MemoryPressureThreshold => _memoryPressureThreshold;
        public bool EnableResourceCaching => _enableResourceCaching;
        public int MaxCacheSize => _maxCacheSize;
        public float UnloadUnusedResourcesInterval => _unloadUnusedResourcesInterval;
        public bool EnableResourcePreloading => _enableResourcePreloading;
        public string[] PreloadResourcePaths => _preloadResourcePaths;
        public int MaxRetryAttempts => _maxRetryAttempts;
        public float RetryDelaySeconds => _retryDelaySeconds;
        public bool EnableCircuitBreaker => _enableCircuitBreaker;
        public int CircuitBreakerFailureThreshold => _circuitBreakerFailureThreshold;
        public float CircuitBreakerTimeoutSeconds => _circuitBreakerTimeoutSeconds;
        public bool EnableMemoryPressureResponse => _enableMemoryPressureResponse;
        public int ModerateCleanupPercentage => _moderateCleanupPercentage;
        public int AggressiveCleanupPercentage => _aggressiveCleanupPercentage;
        public bool EnableDetailedLogging => _enableDetailedLogging;
        public int GracefulShutdownTimeoutMs => _gracefulShutdownTimeoutMs;

        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;

            // Validate enabled providers
            if (_enabledProviders == ProviderType.None)
            {
                errors.Add("At least one provider type must be enabled");
                isValid = false;
            }

            // Validate concurrent loads
            if (_maxConcurrentLoads <= 0)
            {
                errors.Add("MaxConcurrentLoads must be greater than 0");
                isValid = false;
            }

            // Validate memory pressure threshold
            if (_memoryPressureThreshold <= 0 || _memoryPressureThreshold > 1.0f)
            {
                errors.Add("MemoryPressureThreshold must be between 0.1 and 1.0");
                isValid = false;
            }

            // Validate cache size
            if (_enableResourceCaching && _maxCacheSize <= 0)
            {
                errors.Add("MaxCacheSize must be greater than 0 when caching is enabled");
                isValid = false;
            }

            // Validate unload interval
            if (_unloadUnusedResourcesInterval <= 0)
            {
                errors.Add("UnloadUnusedResourcesInterval must be greater than 0");
                isValid = false;
            }

            // Validate retry settings
            if (_maxRetryAttempts < 0)
            {
                errors.Add("MaxRetryAttempts cannot be negative");
                isValid = false;
            }

            if (_retryDelaySeconds <= 0)
            {
                errors.Add("RetryDelaySeconds must be greater than 0");
                isValid = false;
            }

            // Validate circuit breaker settings
            if (_enableCircuitBreaker)
            {
                if (_circuitBreakerFailureThreshold <= 0)
                {
                    errors.Add("CircuitBreakerFailureThreshold must be greater than 0 when circuit breaker is enabled");
                    isValid = false;
                }

                if (_circuitBreakerTimeoutSeconds <= 0)
                {
                    errors.Add("CircuitBreakerTimeoutSeconds must be greater than 0 when circuit breaker is enabled");
                    isValid = false;
                }
            }

            // Validate memory cleanup percentages
            if (_moderateCleanupPercentage <= 0 || _moderateCleanupPercentage > 100)
            {
                errors.Add("ModerateCleanupPercentage must be between 1 and 100");
                isValid = false;
            }

            if (_aggressiveCleanupPercentage <= 0 || _aggressiveCleanupPercentage > 100)
            {
                errors.Add("AggressiveCleanupPercentage must be between 1 and 100");
                isValid = false;
            }

            if (_moderateCleanupPercentage >= _aggressiveCleanupPercentage)
            {
                errors.Add("AggressiveCleanupPercentage must be greater than ModerateCleanupPercentage");
                isValid = false;
            }

            // Validate preload paths
            if (_enableResourcePreloading && (_preloadResourcePaths == null || _preloadResourcePaths.Length == 0))
            {
                errors.Add("PreloadResourcePaths cannot be empty when resource preloading is enabled");
                isValid = false;
            }

            return isValid;
        }

        protected override void OnResetToDefaults()
        {
            _enabledProviders = ProviderType.Addressable | ProviderType.Resources;
            _maxConcurrentLoads = 10;
            _memoryPressureThreshold = 0.8f;
            _enableResourceCaching = true;
            _maxCacheSize = 1000;
            _unloadUnusedResourcesInterval = 30f;
            _enableResourcePreloading = false;
            _preloadResourcePaths = new string[0];
            _maxRetryAttempts = 3;
            _retryDelaySeconds = 1f;
            _enableCircuitBreaker = true;
            _circuitBreakerFailureThreshold = 5;
            _circuitBreakerTimeoutSeconds = 30f;
            _enableMemoryPressureResponse = true;
            _moderateCleanupPercentage = 25;
            _aggressiveCleanupPercentage = 75;
            _enableDetailedLogging = false;
        }

        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"ResourceService Config: {_enabledProviders} providers, " +
                   $"{_maxConcurrentLoads} concurrent loads, " +
                   $"caching {(_enableResourceCaching ? "enabled" : "disabled")}, " +
                   $"circuit breaker {(_enableCircuitBreaker ? "enabled" : "disabled")}";
        }

        /// <summary>
        /// Check if a specific provider type is enabled
        /// </summary>
        public bool IsProviderEnabled(ProviderType providerType)
        {
            return (_enabledProviders & providerType) != 0;
        }

        /// <summary>
        /// Get all enabled provider types as an array
        /// </summary>
        public ProviderType[] GetEnabledProviderTypes()
        {
            var enabledTypes = new List<ProviderType>();
            
            foreach (ProviderType type in System.Enum.GetValues(typeof(ProviderType)))
            {
                if (type != ProviderType.None && IsProviderEnabled(type))
                {
                    enabledTypes.Add(type);
                }
            }
            
            return enabledTypes.ToArray();
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the UI Service with comprehensive settings for performance and behavior
    /// </summary>
    [CreateAssetMenu(fileName = "UIServiceConfiguration", menuName = "Engine/Services/UIServiceConfiguration", order = 1)]
    public class UIServiceConfiguration : ServiceConfigurationBase
    {
        [Header("Core Settings")]
        [SerializeField, Tooltip("Canvas prefab for UI root")]
        private GameObject _uiRootPrefab;
        
        [SerializeField, Tooltip("Screen registry for enum-to-asset mapping")]
        private UIScreenRegistry _screenRegistry;
        
        [SerializeField, Tooltip("Screen to show on service initialization")]
        private UIScreenAsset _initialScreen;

        [Header("Stack Management")]
        [SerializeField, Tooltip("Allow screen stacking for overlay behavior")]
        private bool _allowStacking = true;
        
        [SerializeField, Range(1, 20), Tooltip("Maximum screens in navigation stack")]
        private int _maxStackDepth = 10;

        [Header("Performance Settings")]
        [SerializeField, Range(1, 10), Tooltip("Maximum concurrent screen loads")]
        private int _maxConcurrentLoads = 3;
        
        [SerializeField, Tooltip("Enable screen caching for better performance")]
        private bool _enableScreenCaching = true;
        
        [SerializeField, Range(1, 100), Tooltip("Maximum cached screens")]
        private int _maxCachedScreens = 20;

        [Header("Instance Pool Settings")]
        [SerializeField, Tooltip("Enable instance pooling for multiple-instance screens")]
        private bool _enableInstancePooling = true;
        
        [SerializeField, Range(1, 20), Tooltip("Maximum pooled instances per screen type")]
        private int _maxInstancePoolSizePerType = 5;
        
        [SerializeField, Range(0.1f, 0.95f), Tooltip("Memory pressure threshold for pool cleanup")]
        private float _instancePoolMemoryThreshold = 0.7f;

        [Header("Memory Management")]
        [SerializeField, Range(0.1f, 60f), Tooltip("Delay before unloading unused screens")]
        private float _screenUnloadDelay = 5f;
        
        [SerializeField, Range(0.5f, 0.95f), Tooltip("Memory pressure threshold for cleanup")]
        private float _memoryPressureThreshold = 0.8f;
        
        [SerializeField, Tooltip("Enable automatic memory cleanup")]
        private bool _enableMemoryCleanup = true;

        [Header("Animation Settings")]
        [SerializeField, Range(0.1f, 2f), Tooltip("Default transition duration")]
        private float _defaultTransitionDuration = 0.3f;
        
        [SerializeField, Tooltip("Enable screen transitions")]
        private bool _enableTransitions = true;

        [Header("Error Handling")]
        [SerializeField, Tooltip("Enable error recovery mechanisms")]
        private bool _enableErrorRecovery = true;
        
        [SerializeField, Range(1, 5), Tooltip("Maximum retry attempts for failed loads")]
        private int _maxRetryAttempts = 3;
        
        [SerializeField, Range(0.1f, 5f), Tooltip("Delay between retry attempts")]
        private float _retryDelay = 1f;

        [Header("Transition Settings")]
        [SerializeField, Tooltip("Configuration for UI transitions")]
        private UITransitionConfiguration _transitionConfiguration;

        #region Public Properties

        /// <summary>
        /// Canvas prefab for UI root
        /// </summary>
        public GameObject UIRootPrefab => _uiRootPrefab;

        /// <summary>
        /// Screen registry for enum-to-asset mapping
        /// </summary>
        public UIScreenRegistry ScreenRegistry => _screenRegistry;

        /// <summary>
        /// Screen to show on service initialization
        /// </summary>
        public UIScreenAsset InitialScreen => _initialScreen;

        /// <summary>
        /// Allow screen stacking for overlay behavior
        /// </summary>
        public bool AllowStacking => _allowStacking;

        /// <summary>
        /// Maximum screens in navigation stack
        /// </summary>
        public int MaxStackDepth => _maxStackDepth;

        /// <summary>
        /// Maximum concurrent screen loads
        /// </summary>
        public int MaxConcurrentLoads => _maxConcurrentLoads;

        /// <summary>
        /// Enable screen caching for better performance
        /// </summary>
        public bool EnableScreenCaching => _enableScreenCaching;

        /// <summary>
        /// Maximum cached screens
        /// </summary>
        public int MaxCachedScreens => _maxCachedScreens;

        /// <summary>
        /// Enable instance pooling for multiple-instance screens
        /// </summary>
        public bool EnableInstancePooling => _enableInstancePooling;

        /// <summary>
        /// Maximum pooled instances per screen type
        /// </summary>
        public int MaxInstancePoolSizePerType => _maxInstancePoolSizePerType;

        /// <summary>
        /// Memory pressure threshold for pool cleanup
        /// </summary>
        public float InstancePoolMemoryThreshold => _instancePoolMemoryThreshold;

        /// <summary>
        /// Delay before unloading unused screens
        /// </summary>
        public float ScreenUnloadDelay => _screenUnloadDelay;

        /// <summary>
        /// Memory pressure threshold for cleanup
        /// </summary>
        public float MemoryPressureThreshold => _memoryPressureThreshold;

        /// <summary>
        /// Enable automatic memory cleanup
        /// </summary>
        public bool EnableMemoryCleanup => _enableMemoryCleanup;

        /// <summary>
        /// Default transition duration
        /// </summary>
        public float DefaultTransitionDuration => _defaultTransitionDuration;

        /// <summary>
        /// Enable screen transitions
        /// </summary>
        public bool EnableTransitions => _enableTransitions;

        /// <summary>
        /// Enable error recovery mechanisms
        /// </summary>
        public bool EnableErrorRecovery => _enableErrorRecovery;

        /// <summary>
        /// Maximum retry attempts for failed loads
        /// </summary>
        public int MaxRetryAttempts => _maxRetryAttempts;

        /// <summary>
        /// Delay between retry attempts
        /// </summary>
        public float RetryDelay => _retryDelay;

        /// <summary>
        /// Configuration for UI transitions
        /// </summary>
        public UITransitionConfiguration TransitionConfiguration => _transitionConfiguration;

        #endregion

        #region Validation

        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;

            // UI Root validation
            if (_uiRootPrefab == null)
            {
                errors.Add("UI Root Prefab is required");
                isValid = false;
            }
            else if (_uiRootPrefab.GetComponent<Canvas>() == null)
            {
                errors.Add("UI Root Prefab must have a Canvas component");
                isValid = false;
            }

            // Screen Registry validation
            if (_screenRegistry == null)
            {
                errors.Add("Screen Registry is required");
                isValid = false;
            }

            // Stack depth validation
            if (_maxStackDepth <= 0)
            {
                errors.Add("Max Stack Depth must be greater than 0");
                isValid = false;
            }

            // Performance validation
            if (_maxConcurrentLoads <= 0)
            {
                errors.Add("Max Concurrent Loads must be greater than 0");
                isValid = false;
            }

            if (_enableScreenCaching && _maxCachedScreens <= 0)
            {
                errors.Add("Max Cached Screens must be greater than 0 when caching is enabled");
                isValid = false;
            }

            // Instance pool validation
            if (_enableInstancePooling && _maxInstancePoolSizePerType <= 0)
            {
                errors.Add("Max Instance Pool Size Per Type must be greater than 0 when instance pooling is enabled");
                isValid = false;
            }

            if (_instancePoolMemoryThreshold <= 0.1f || _instancePoolMemoryThreshold >= 1f)
            {
                errors.Add("Instance Pool Memory Threshold must be between 0.1 and 1.0");
                isValid = false;
            }

            // Memory management validation
            if (_screenUnloadDelay < 0)
            {
                errors.Add("Screen Unload Delay cannot be negative");
                isValid = false;
            }

            if (_memoryPressureThreshold <= 0.5f || _memoryPressureThreshold >= 1f)
            {
                errors.Add("Memory Pressure Threshold must be between 0.5 and 1.0");
                isValid = false;
            }

            // Animation validation
            if (_defaultTransitionDuration <= 0)
            {
                errors.Add("Default Transition Duration must be greater than 0");
                isValid = false;
            }

            // Error handling validation
            if (_maxRetryAttempts < 0)
            {
                errors.Add("Max Retry Attempts cannot be negative");
                isValid = false;
            }

            if (_retryDelay < 0)
            {
                errors.Add("Retry Delay cannot be negative");
                isValid = false;
            }

            // Transition configuration validation
            if (_transitionConfiguration == null)
            {
                errors.Add("Transition Configuration is required");
                isValid = false;
            }

            return isValid;
        }

        protected override void OnResetToDefaults()
        {
            _uiRootPrefab = null;
            _screenRegistry = null;
            _initialScreen = null;
            _allowStacking = true;
            _maxStackDepth = 10;
            _maxConcurrentLoads = 3;
            _enableScreenCaching = true;
            _maxCachedScreens = 20;
            _enableInstancePooling = true;
            _maxInstancePoolSizePerType = 5;
            _instancePoolMemoryThreshold = 0.7f;
            _screenUnloadDelay = 5f;
            _memoryPressureThreshold = 0.8f;
            _enableMemoryCleanup = true;
            _defaultTransitionDuration = 0.3f;
            _enableTransitions = true;
            _enableErrorRecovery = true;
            _maxRetryAttempts = 3;
            _retryDelay = 1f;
            _transitionConfiguration = null;
        }

        #endregion

        #region Editor Support

        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"UIService Config: Stack depth {_maxStackDepth}, " +
                   $"Caching {(_enableScreenCaching ? "enabled" : "disabled")}, " +
                   $"Transitions {(_enableTransitions ? "enabled" : "disabled")}";
        }

        #endregion
    }
}
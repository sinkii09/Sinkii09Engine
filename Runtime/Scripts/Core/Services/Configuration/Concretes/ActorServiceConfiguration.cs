using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Actor Service with comprehensive settings for performance, resources, and behavior
    /// </summary>
    [CreateAssetMenu(fileName = "ActorServiceConfiguration", menuName = "Engine/Services/ActorServiceConfiguration", order = 1)]
    public class ActorServiceConfiguration : ServiceConfigurationBase
    {
        [Header("Actor Pool Settings")]
        [SerializeField]
        [Range(1, 10000)]
        [Tooltip("Maximum number of actors that can exist simultaneously")]
        private int _maxActors = 1000;
        
        [SerializeField]
        [Range(1, 100)]
        [Tooltip("Initial pool size for actor objects")]
        private int _initialPoolSize = 50;
        
        [SerializeField]
        [Tooltip("Name of the actor pool for identification")]
        private string _actorPoolName = "MainActorPool";
        
        [Header("Performance Settings")]
        [SerializeField]
        [Range(0.001f, 1f)]
        [Tooltip("Time delay between actor spawns to prevent performance spikes")]
        private float _actorSpawnDelay = 0.1f;
        
        [SerializeField]
        [Tooltip("Enable object pooling for better performance")]
        private bool _enableObjectPooling = true;
        
        [SerializeField]
        [Range(1, 60)]
        [Tooltip("Maximum actors to update per frame")]
        private int _maxActorsPerFrame = 30;
        
        [Header("Cleanup Settings")]
        [SerializeField]
        [Range(1f, 300f)]
        [Tooltip("Time in seconds before inactive actors are cleaned up")]
        private float _cleanupInterval = 30f;
        
        [SerializeField]
        [Tooltip("Automatically clean up actors that are too far from active areas")]
        private bool _enableDistanceBasedCleanup = true;
        
        [SerializeField]
        [Range(10f, 1000f)]
        [Tooltip("Distance threshold for automatic cleanup")]
        private float _cleanupDistance = 200f;
        
        [Header("Resource Management")]
        [SerializeField, Range(1, 20)] 
        [Tooltip("Maximum number of actors loading resources simultaneously")]
        private int _maxConcurrentLoads = 5;
        
        [SerializeField, Range(10, 500)] 
        [Tooltip("Maximum number of cached resources")]
        private int _maxCachedResources = 100;
        
        [SerializeField] 
        [Tooltip("Preload common resources at startup")]
        private bool _preloadCommonResources = true;
        
        [SerializeField] 
        [Tooltip("Base path for actor resources")]
        private string _resourceBasePath = "Actors";
        
        [SerializeField, Range(0.1f, 60f)] 
        [Tooltip("Delay before unloading unused resources")]
        private float _resourceUnloadDelay = 5f;
        
        [Header("Animation Settings")]
        [SerializeField, Range(0.1f, 5f)] 
        [Tooltip("Default animation duration")]
        private float _defaultAnimationDuration = 1f;
        
        [SerializeField] 
        [Tooltip("Default easing for animations")]
        private Ease _defaultEase = Ease.OutQuad;
        
        [SerializeField] 
        [Tooltip("Enable complex animation sequences")]
        private bool _enableAnimationSequences = true;
        
        [SerializeField, Range(0.1f, 3f)] 
        [Tooltip("Global animation speed multiplier")]
        private float _globalAnimationSpeed = 1f;
        
        [Header("Performance Settings")]
        [SerializeField] 
        [Tooltip("Enable performance monitoring")]
        private bool _enablePerformanceMonitoring = true;
        
        [SerializeField] 
        [Tooltip("Enable memory optimization")]
        private bool _enableMemoryOptimization = true;
        
        [SerializeField, Range(0.5f, 0.95f)] 
        [Tooltip("Memory pressure threshold for cleanup")]
        private float _memoryPressureThreshold = 0.8f;
        
        [Header("Scene Management")]
        [SerializeField] 
        [Tooltip("Automatically set main background")]
        private bool _autoSetMainBackground = true;
        
        [SerializeField] 
        [Tooltip("Preload actors for scene transitions")]
        private bool _preloadSceneActors = true;
        
        [SerializeField, Range(1f, 10f)] 
        [Tooltip("Default scene transition duration")]
        private float _sceneTransitionDuration = 2f;
        
        [Header("Error Handling")]
        [SerializeField] 
        [Tooltip("Enable error recovery mechanisms")]
        private bool _enableErrorRecovery = true;
        
        [SerializeField, Range(1, 10)] 
        [Tooltip("Maximum retry attempts for failed operations")]
        private int _maxRetryAttempts = 3;
        
        [SerializeField, Range(0.1f, 5f)] 
        [Tooltip("Delay between retry attempts")]
        private float _retryDelay = 1f;
        
        [SerializeField] 
        [Tooltip("Use default assets as fallback")]
        private bool _fallbackToDefaultAssets = true;
        
        // === Public Properties ===
        
        // Actor Pool Settings
        public int MaxActors => _maxActors;
        public int InitialPoolSize => _initialPoolSize;
        public string ActorPoolName => _actorPoolName;
        public float ActorSpawnDelay => _actorSpawnDelay;
        public bool EnableObjectPooling => _enableObjectPooling;
        public int MaxActorsPerFrame => _maxActorsPerFrame;
        
        // Cleanup Settings
        public float CleanupInterval => _cleanupInterval;
        public bool EnableDistanceBasedCleanup => _enableDistanceBasedCleanup;
        public float CleanupDistance => _cleanupDistance;
        
        // Resource Management
        public int MaxConcurrentLoads => _maxConcurrentLoads;
        public int MaxCachedResources => _maxCachedResources;
        public bool PreloadCommonResources => _preloadCommonResources;
        public string ResourceBasePath => _resourceBasePath;
        public float ResourceUnloadDelay => _resourceUnloadDelay;
        
        // Animation Settings
        public float DefaultAnimationDuration => _defaultAnimationDuration;
        public Ease DefaultEase => _defaultEase;
        public bool EnableAnimationSequences => _enableAnimationSequences;
        public float GlobalAnimationSpeed => _globalAnimationSpeed;
        
        // Performance Settings
        public bool EnablePerformanceMonitoring => _enablePerformanceMonitoring;
        public bool EnableMemoryOptimization => _enableMemoryOptimization;
        public float MemoryPressureThreshold => _memoryPressureThreshold;
        
        // Scene Management
        public bool AutoSetMainBackground => _autoSetMainBackground;
        public bool PreloadSceneActors => _preloadSceneActors;
        public float SceneTransitionDuration => _sceneTransitionDuration;
        
        // Error Handling
        public bool EnableErrorRecovery => _enableErrorRecovery;
        public int MaxRetryAttempts => _maxRetryAttempts;
        public float RetryDelay => _retryDelay;
        public bool FallbackToDefaultAssets => _fallbackToDefaultAssets;
        
        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;
            
            // Validate max actors
            if (_maxActors <= 0)
            {
                errors.Add("MaxActors must be greater than 0");
                isValid = false;
            }
            
            // Validate initial pool size
            if (_initialPoolSize > _maxActors)
            {
                errors.Add("InitialPoolSize cannot be greater than MaxActors");
                isValid = false;
            }
            
            if (_initialPoolSize <= 0)
            {
                errors.Add("InitialPoolSize must be greater than 0");
                isValid = false;
            }
            
            // Validate actor pool name
            if (string.IsNullOrEmpty(_actorPoolName))
            {
                errors.Add("ActorPoolName cannot be empty");
                isValid = false;
            }
            
            // Validate spawn delay
            if (_actorSpawnDelay < 0)
            {
                errors.Add("ActorSpawnDelay cannot be negative");
                isValid = false;
            }
            
            // Validate max actors per frame
            if (_maxActorsPerFrame <= 0)
            {
                errors.Add("MaxActorsPerFrame must be greater than 0");
                isValid = false;
            }
            
            // Validate cleanup settings
            if (_cleanupInterval <= 0)
            {
                errors.Add("CleanupInterval must be greater than 0");
                isValid = false;
            }
            
            if (_enableDistanceBasedCleanup && _cleanupDistance <= 0)
            {
                errors.Add("CleanupDistance must be greater than 0 when distance-based cleanup is enabled");
                isValid = false;
            }
            
            return isValid;
        }
        
        protected override void OnResetToDefaults()
        {
            // Actor Pool Settings
            _maxActors = 1000;
            _initialPoolSize = 50;
            _actorPoolName = "MainActorPool";
            _actorSpawnDelay = 0.1f;
            _enableObjectPooling = true;
            _maxActorsPerFrame = 30;
            
            // Cleanup Settings
            _cleanupInterval = 30f;
            _enableDistanceBasedCleanup = true;
            _cleanupDistance = 200f;
            
            // Resource Management
            _maxConcurrentLoads = 5;
            _maxCachedResources = 100;
            _preloadCommonResources = true;
            _resourceBasePath = "Actors";
            _resourceUnloadDelay = 5f;
            
            // Animation Settings
            _defaultAnimationDuration = 1f;
            _defaultEase = Ease.OutQuad;
            _enableAnimationSequences = true;
            _globalAnimationSpeed = 1f;
            
            // Performance Settings
            _enablePerformanceMonitoring = true;
            _enableMemoryOptimization = true;
            _memoryPressureThreshold = 0.8f;
            
            // Scene Management
            _autoSetMainBackground = true;
            _preloadSceneActors = true;
            _sceneTransitionDuration = 2f;
            
            // Error Handling
            _enableErrorRecovery = true;
            _maxRetryAttempts = 3;
            _retryDelay = 1f;
            _fallbackToDefaultAssets = true;
        }
        
        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"ActorService Config: {_maxActors} max actors, {_initialPoolSize} initial pool, " +
                   $"pooling {(_enableObjectPooling ? "enabled" : "disabled")}, " +
                   $"cleanup every {_cleanupInterval}s";
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Actor Service
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
        
        // Public properties for accessing configuration values
        public int MaxActors => _maxActors;
        public int InitialPoolSize => _initialPoolSize;
        public string ActorPoolName => _actorPoolName;
        public float ActorSpawnDelay => _actorSpawnDelay;
        public bool EnableObjectPooling => _enableObjectPooling;
        public int MaxActorsPerFrame => _maxActorsPerFrame;
        public float CleanupInterval => _cleanupInterval;
        public bool EnableDistanceBasedCleanup => _enableDistanceBasedCleanup;
        public float CleanupDistance => _cleanupDistance;
        
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
            _maxActors = 1000;
            _initialPoolSize = 50;
            _actorPoolName = "MainActorPool";
            _actorSpawnDelay = 0.1f;
            _enableObjectPooling = true;
            _maxActorsPerFrame = 30;
            _cleanupInterval = 30f;
            _enableDistanceBasedCleanup = true;
            _cleanupDistance = 200f;
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
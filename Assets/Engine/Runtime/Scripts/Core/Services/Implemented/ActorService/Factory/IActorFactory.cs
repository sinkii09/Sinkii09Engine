using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for actor creation operations including pooling and validation
    /// Responsible for managing actor instantiation with performance optimization
    /// </summary>
    public interface IActorFactory
    {
        #region Actor Creation Operations
        
        /// <summary>
        /// Creates a new character actor with the specified appearance
        /// </summary>
        /// <param name="id">Unique actor ID</param>
        /// <param name="appearance">Character appearance configuration</param>
        /// <param name="position">Initial world position</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created character actor</returns>
        UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new background actor with the specified appearance
        /// </summary>
        /// <param name="id">Unique actor ID</param>
        /// <param name="appearance">Background appearance configuration</param>
        /// <param name="position">Initial world position</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created background actor</returns>
        UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a custom actor from a prefab with type checking
        /// </summary>
        /// <typeparam name="T">Actor type that must implement IActor</typeparam>
        /// <param name="id">Unique actor ID</param>
        /// <param name="prefab">Actor prefab to instantiate</param>
        /// <param name="position">Initial world position</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created custom actor</returns>
        UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor;
        
        #endregion
        
        #region Pool Management
        
        /// <summary>
        /// Configures object pooling settings for actor types
        /// </summary>
        /// <param name="enabled">Whether pooling is enabled</param>
        /// <param name="defaultPoolSize">Default pool size for each actor type</param>
        void ConfigurePooling(bool enabled, int defaultPoolSize);
        
        /// <summary>
        /// Gets the current pool size for a specific actor type
        /// </summary>
        /// <param name="actorType">Actor type to check</param>
        /// <returns>Current pool size</returns>
        int GetPoolSize(ActorType actorType);
        
        /// <summary>
        /// Warms up the pool for a specific actor type by pre-creating instances
        /// </summary>
        /// <param name="actorType">Actor type to warm up</param>
        /// <param name="count">Number of instances to pre-create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Warmup completion task</returns>
        UniTask WarmupPoolAsync(ActorType actorType, int count, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Returns an actor to the pool for reuse
        /// </summary>
        /// <param name="actor">Actor to return to pool</param>
        /// <returns>True if actor was returned to pool, false if not pooled</returns>
        bool ReturnToPool(IActor actor);
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Validates that an actor can be created with the given parameters
        /// </summary>
        /// <param name="id">Actor ID to validate</param>
        /// <param name="actorType">Actor type to validate</param>
        /// <returns>True if creation is valid, false otherwise</returns>
        bool ValidateActorCreation(string id, ActorType actorType);
        
        /// <summary>
        /// Checks if actors of the specified type can be created
        /// </summary>
        /// <param name="actorType">Actor type to check</param>
        /// <returns>True if type can be created, false if disabled/unavailable</returns>
        bool CanCreateActorType(ActorType actorType);
        
        /// <summary>
        /// Validates a prefab for custom actor creation
        /// </summary>
        /// <param name="prefab">Prefab to validate</param>
        /// <param name="expectedType">Expected actor interface type</param>
        /// <returns>True if prefab is valid for the type</returns>
        bool ValidatePrefab(GameObject prefab, System.Type expectedType);
        
        #endregion
        
        #region Statistics
        
        /// <summary>
        /// Gets creation statistics for monitoring
        /// </summary>
        /// <returns>Factory statistics</returns>
        ActorFactoryStatistics GetStatistics();
        
        /// <summary>
        /// Resets all creation statistics
        /// </summary>
        void ResetStatistics();
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Updates factory configuration at runtime
        /// </summary>
        /// <param name="maxConcurrentCreations">Maximum concurrent creation operations</param>
        /// <param name="enableValidation">Whether to enable validation checks</param>
        /// <param name="enablePooling">Whether to enable object pooling</param>
        void Configure(int maxConcurrentCreations, bool enableValidation, bool enablePooling);
        
        #endregion
    }
    
    /// <summary>
    /// Statistics for actor factory operations
    /// </summary>
    public class ActorFactoryStatistics
    {
        public int TotalCreated { get; set; }
        public int CharactersCreated { get; set; }
        public int BackgroundsCreated { get; set; }
        public int CustomActorsCreated { get; set; }
        public int PoolHits { get; set; }
        public int PoolMisses { get; set; }
        public int ValidationFailures { get; set; }
        public float AverageCreationTime { get; set; }
        public int CurrentPoolSize { get; set; }
        public int PendingCreations { get; set; }
        
        public float PoolHitRate => TotalCreated > 0 ? (float)PoolHits / TotalCreated : 0f;
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core service interface for actor management with complete lifecycle support and registry operations
    /// </summary>
    public interface IActorService : IEngineService
    {
        // === Actor Registry and Management ===
        
        /// <summary>
        /// Gets all registered actors of all types
        /// </summary>
        IReadOnlyCollection<IActor> AllActors { get; }
        
        /// <summary>
        /// Gets all registered character actors
        /// </summary>
        IReadOnlyCollection<ICharacterActor> CharacterActors { get; }
        
        /// <summary>
        /// Gets all registered background actors
        /// </summary>
        IReadOnlyCollection<IBackgroundActor> BackgroundActors { get; }
        
        /// <summary>
        /// Total number of registered actors
        /// </summary>
        int ActorCount { get; }
        
        /// <summary>
        /// Number of currently loading actors
        /// </summary>
        int LoadingActorsCount { get; }
        
        // === Actor Creation and Registration ===
        
        /// <summary>
        /// Creates a new character actor with the specified configuration
        /// </summary>
        UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a new background actor with the specified configuration
        /// </summary>
        UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a custom actor using a prefab or GameObject
        /// </summary>
        UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor;
        
        /// <summary>
        /// Registers an existing actor with the service
        /// </summary>
        bool RegisterActor(IActor actor);
        
        /// <summary>
        /// Unregisters an actor from the service
        /// </summary>
        bool UnregisterActor(string actorId);
        
        // === Actor Lookup and Retrieval ===
        
        /// <summary>
        /// Gets an actor by ID (any type)
        /// </summary>
        IActor GetActor(string id);
        
        /// <summary>
        /// Gets a character actor by ID
        /// </summary>
        ICharacterActor GetCharacterActor(string id);
        
        /// <summary>
        /// Gets a background actor by ID
        /// </summary>
        IBackgroundActor GetBackgroundActor(string id);
        
        /// <summary>
        /// Gets an actor by ID with type checking
        /// </summary>
        T GetActor<T>(string id) where T : class, IActor;
        
        /// <summary>
        /// Tries to get an actor by ID (thread-safe)
        /// </summary>
        bool TryGetActor(string id, out IActor actor);
        
        /// <summary>
        /// Tries to get a character actor by ID (thread-safe)
        /// </summary>
        bool TryGetCharacterActor(string id, out ICharacterActor actor);
        
        /// <summary>
        /// Tries to get a background actor by ID (thread-safe)
        /// </summary>
        bool TryGetBackgroundActor(string id, out IBackgroundActor actor);
        
        /// <summary>
        /// Gets all actors of a specific type
        /// </summary>
        IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor;
        
        /// <summary>
        /// Checks if an actor with the given ID exists
        /// </summary>
        bool HasActor(string id);
        
        /// <summary>
        /// Gets all actor IDs currently registered
        /// </summary>
        string[] GetActorIds();
        
        // === Actor Operations ===
        
        /// <summary>
        /// Loads resources for a specific actor
        /// </summary>
        UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Loads resources for all actors
        /// </summary>
        UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads resources for a specific actor
        /// </summary>
        UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads resources for all actors
        /// </summary>
        UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Destroys a specific actor and removes it from registry
        /// </summary>
        UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Destroys all actors and clears registry
        /// </summary>
        UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Refreshes all actors (reloads resources and updates visuals)
        /// </summary>
        UniTask RefreshAllActorsAsync(CancellationToken cancellationToken = default);
        
        // === Scene Management ===
        
        /// <summary>
        /// Sets the main background actor for the current scene
        /// </summary>
        UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current main background actor
        /// </summary>
        IBackgroundActor GetMainBackground();
        
        /// <summary>
        /// Clears the current scene (destroys all actors)
        /// </summary>
        UniTask ClearSceneAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Preloads actors for a scene transition
        /// </summary>
        UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default);
        
        // === Visibility and State Management ===
        
        /// <summary>
        /// Shows an actor with animation
        /// </summary>
        UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Hides an actor with animation
        /// </summary>
        UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Shows all actors with animation
        /// </summary>
        UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Hides all actors with animation
        /// </summary>
        UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current state of all actors for save/load
        /// </summary>
        Dictionary<string, ActorState> GetAllActorStates();
        
        /// <summary>
        /// Applies saved states to all actors
        /// </summary>
        UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default);
        
        // === Animation and Effects ===
        
        /// <summary>
        /// Stops all animations on all actors
        /// </summary>
        void StopAllAnimations();
        
        /// <summary>
        /// Pauses all animations on all actors
        /// </summary>
        void PauseAllAnimations();
        
        /// <summary>
        /// Resumes all animations on all actors
        /// </summary>
        void ResumeAllAnimations();
        
        /// <summary>
        /// Sets animation speed multiplier for all actors
        /// </summary>
        void SetGlobalAnimationSpeed(float speedMultiplier);
        
        // === Configuration and Utility ===
        
        /// <summary>
        /// Validates all actor configurations
        /// </summary>
        Dictionary<string, string[]> ValidateAllActors();
        
        /// <summary>
        /// Gets performance statistics for the actor system
        /// </summary>
        ActorServiceStatistics GetStatistics();
        
        /// <summary>
        /// Gets debug information for all actors
        /// </summary>
        string GetDebugInfo();
        
        /// <summary>
        /// Configures the actor service at runtime
        /// </summary>
        void Configure(int maxConcurrentLoads, bool enableActorPooling, bool enablePerformanceMonitoring);
        
        // === Events ===
        
        /// <summary>
        /// Fired when any actor is created
        /// </summary>
        event Action<IActor> OnActorCreated;
        
        /// <summary>
        /// Fired when any actor is destroyed
        /// </summary>
        event Action<string> OnActorDestroyed;
        
        /// <summary>
        /// Fired when any actor's visibility changes
        /// </summary>
        event Action<IActor, bool> OnActorVisibilityChanged;
        
        /// <summary>
        /// Fired when any actor encounters an error
        /// </summary>
        event Action<IActor, string> OnActorError;
        
        /// <summary>
        /// Fired when the main background changes
        /// </summary>
        event Action<IBackgroundActor> OnMainBackgroundChanged;
        
        /// <summary>
        /// Fired when actor loading progress updates
        /// </summary>
        event Action<string, float> OnActorLoadProgressChanged;
    }
    
    /// <summary>
    /// Statistics and performance information for the actor service
    /// </summary>
    public class ActorServiceStatistics
    {
        public int TotalActors { get; set; }
        public int LoadedActors { get; set; }
        public int LoadingActors { get; set; }
        public int ErrorActors { get; set; }
        public int CharacterActors { get; set; }
        public int BackgroundActors { get; set; }
        public int CustomActors { get; set; }
        
        public TimeSpan TotalLoadTime { get; set; }
        public TimeSpan AverageLoadTime { get; set; }
        public long MemoryUsageBytes { get; set; }
        
        public int ActiveAnimations { get; set; }
        public int CachedResources { get; set; }
        public float GlobalAnimationSpeed { get; set; }
        
        public DateTime LastUpdateTime { get; set; }
        public bool PerformanceMonitoringEnabled { get; set; }
    }
}
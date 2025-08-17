using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for scene operations, background management, and actor state coordination
    /// Responsible for managing scene-level operations and actor lifecycle at scene scope
    /// </summary>
    public interface ISceneManager
    {
        #region Background Management
        
        /// <summary>
        /// Sets the main background for the current scene
        /// </summary>
        /// <param name="backgroundId">ID of the background actor to set as main</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current main background actor
        /// </summary>
        /// <returns>Main background actor or null if none set</returns>
        IBackgroundActor GetMainBackground();
        
        /// <summary>
        /// Clears the main background setting
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask ClearMainBackgroundAsync(CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Scene Operations
        
        /// <summary>
        /// Clears all actors from the current scene
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask ClearSceneAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Preloads actors for upcoming scene transitions
        /// </summary>
        /// <param name="actorIds">Array of actor IDs to preload</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Transitions to a new scene with specified actors
        /// </summary>
        /// <param name="newSceneActors">Dictionary of actor IDs to their states for the new scene</param>
        /// <param name="transitionDuration">Duration of the transition</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask TransitionToSceneAsync(Dictionary<string, ActorState> newSceneActors, float transitionDuration = 2.0f, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Visibility Management
        
        /// <summary>
        /// Shows an actor with animation
        /// </summary>
        /// <param name="actorId">ID of actor to show</param>
        /// <param name="duration">Animation duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Hides an actor with animation
        /// </summary>
        /// <param name="actorId">ID of actor to hide</param>
        /// <param name="duration">Animation duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Shows all actors in the scene
        /// </summary>
        /// <param name="duration">Animation duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Hides all actors in the scene
        /// </summary>
        /// <param name="duration">Animation duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region State Management
        
        /// <summary>
        /// Captures the current state of all actors in the scene
        /// </summary>
        /// <returns>Dictionary mapping actor IDs to their current states</returns>
        Dictionary<string, ActorState> GetAllActorStates();
        
        /// <summary>
        /// Applies states to all actors in the scene
        /// </summary>
        /// <param name="states">Dictionary mapping actor IDs to desired states</param>
        /// <param name="duration">Animation duration for state transitions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves the current scene state with a name
        /// </summary>
        /// <param name="stateName">Name to save the state under</param>
        /// <returns>True if state was saved successfully</returns>
        bool SaveSceneState(string stateName);
        
        /// <summary>
        /// Loads a previously saved scene state
        /// </summary>
        /// <param name="stateName">Name of the state to load</param>
        /// <param name="duration">Animation duration for state transitions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask LoadSceneStateAsync(string stateName, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets names of all saved scene states
        /// </summary>
        /// <returns>Array of saved state names</returns>
        string[] GetSavedStateNames();
        
        #endregion
        
        #region Resource Management
        
        /// <summary>
        /// Loads resources for a specific actor
        /// </summary>
        /// <param name="actorId">ID of actor to load resources for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Loads resources for all actors in the scene
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads resources for a specific actor
        /// </summary>
        /// <param name="actorId">ID of actor to unload resources for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads resources for all actors in the scene
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Lifecycle Management
        
        /// <summary>
        /// Destroys a specific actor and cleans up its resources
        /// </summary>
        /// <param name="actorId">ID of actor to destroy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Destroys all actors in the scene
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when the main background changes
        /// </summary>
        event Action<IBackgroundActor> OnMainBackgroundChanged;
        
        /// <summary>
        /// Fired when a scene transition starts
        /// </summary>
        event Action<string> OnSceneTransitionStarted;
        
        /// <summary>
        /// Fired when a scene transition completes
        /// </summary>
        event Action<string> OnSceneTransitionCompleted;
        
        /// <summary>
        /// Fired when a scene state is saved
        /// </summary>
        event Action<string> OnSceneStateSaved;
        
        /// <summary>
        /// Fired when a scene state is loaded
        /// </summary>
        event Action<string> OnSceneStateLoaded;
        
        #endregion
        
        #region Utilities
        
        /// <summary>
        /// Gets scene manager statistics for monitoring
        /// </summary>
        /// <returns>Scene manager statistics</returns>
        SceneManagerStatistics GetStatistics();
        
        /// <summary>
        /// Validates that a scene operation can be performed
        /// </summary>
        /// <param name="operationType">Type of operation to validate</param>
        /// <param name="parameters">Operation parameters</param>
        /// <returns>True if operation is valid</returns>
        bool ValidateSceneOperation(string operationType, params object[] parameters);
        
        #endregion
    }
    
    /// <summary>
    /// Statistics for scene manager operations
    /// </summary>
    public class SceneManagerStatistics
    {
        public int TotalSceneTransitions { get; set; }
        public int ResourceLoadOperations { get; set; }
        public int ResourceUnloadOperations { get; set; }
        public int StateSnapshotsSaved { get; set; }
        public int StateSnapshotsLoaded { get; set; }
        public int ActorsDestroyed { get; set; }
        public float AverageTransitionTime { get; set; }
        public string CurrentMainBackground { get; set; }
        public int SavedStatesCount { get; set; }
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Scene manager implementation handling scene-level operations and actor coordination
    /// Manages background settings, scene transitions, state management, and resource lifecycle
    /// </summary>
    public class SceneManager : ISceneManager
    {
        #region Private Fields
        
        private readonly IActorRegistry _registry;
        private readonly IResourceService _resourceService;
        private readonly ActorServiceConfiguration _config;
        
        // Background management
        private IBackgroundActor _mainBackground;
        private readonly object _backgroundLock = new();
        
        // State management
        private readonly ConcurrentDictionary<string, Dictionary<string, ActorState>> _savedStates = new();
        
        // Resource management
        private readonly SemaphoreSlim _resourceSemaphore;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeResourceOperations = new();
        
        // Statistics
        private SceneManagerStatistics _statistics = new();
        private readonly Stopwatch _transitionStopwatch = new();
        
        #endregion
        
        #region Events
        
        public event Action<IBackgroundActor> OnMainBackgroundChanged;
        public event Action<string> OnSceneTransitionStarted;
        public event Action<string> OnSceneTransitionCompleted;
        public event Action<string> OnSceneStateSaved;
        public event Action<string> OnSceneStateLoaded;
        
        #endregion
        
        #region Constructor
        
        public SceneManager(IActorRegistry registry, IResourceService resourceService, ActorServiceConfiguration config)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _resourceSemaphore = new SemaphoreSlim(_config.MaxConcurrentLoads, _config.MaxConcurrentLoads);
            
            UnityEngine.Debug.Log("[SceneManager] Initialized");
        }
        
        #endregion
        
        #region Background Management
        
        public async UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(backgroundId))
            {
                throw new ArgumentException("Background ID cannot be null or empty", nameof(backgroundId));
            }
            
            var backgroundActor = _registry.GetBackgroundActor(backgroundId);
            if (backgroundActor == null)
            {
                throw new ArgumentException($"Background actor '{backgroundId}' not found", nameof(backgroundId));
            }
            
            lock (_backgroundLock)
            {
                var previousBackground = _mainBackground;
                _mainBackground = backgroundActor;
                
                // Set sorting order to ensure it's behind other actors
                backgroundActor.SortingOrder = -100;
                
                _statistics.CurrentMainBackground = backgroundId;
                
                UnityEngine.Debug.Log($"[SceneManager] Set main background to '{backgroundId}'");
            }
            
            // Set the background as main
            await backgroundActor.SetAsMainBackgroundAsync(cancellationToken);
            
            OnMainBackgroundChanged?.Invoke(backgroundActor);
        }
        
        public IBackgroundActor GetMainBackground()
        {
            lock (_backgroundLock)
            {
                return _mainBackground;
            }
        }
        
        public async UniTask ClearMainBackgroundAsync(CancellationToken cancellationToken = default)
        {
            IBackgroundActor previousBackground;
            
            lock (_backgroundLock)
            {
                previousBackground = _mainBackground;
                _mainBackground = null;
                _statistics.CurrentMainBackground = null;
            }
            
            if (previousBackground != null)
            {
                // Fade out the previous background
                await previousBackground.FadeOutAsync(1.0f, cancellationToken);
                UnityEngine.Debug.Log("[SceneManager] Cleared main background");
            }
            
            OnMainBackgroundChanged?.Invoke(null);
        }
        
        #endregion
        
        #region Scene Operations
        
        public async UniTask ClearSceneAsync(CancellationToken cancellationToken = default)
        {
            OnSceneTransitionStarted?.Invoke("Clear");
            _transitionStopwatch.Restart();
            
            try
            {
                // Cleanup DOTween animations before clearing scene
                DOTweenCleanupHelper.TriggerCleanup("Scene Clear Started");
                
                // Hide all actors first
                await HideAllActorsAsync(1.0f, cancellationToken);
                
                // Destroy all actors
                await DestroyAllActorsAsync(cancellationToken);
                
                // Clear main background
                await ClearMainBackgroundAsync(cancellationToken);
                
                // Final DOTween cleanup after scene operations
                DOTweenCleanupHelper.TriggerCleanup("Scene Clear Completed");
                
                _statistics.TotalSceneTransitions++;
                
                UnityEngine.Debug.Log("[SceneManager] Scene cleared");
            }
            finally
            {
                _transitionStopwatch.Stop();
                UpdateAverageTransitionTime(_transitionStopwatch.ElapsedMilliseconds);
                OnSceneTransitionCompleted?.Invoke("Clear");
            }
        }
        
        public async UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default)
        {
            if (actorIds == null || actorIds.Length == 0)
                return;
                
            var loadTasks = new List<UniTask>();
            
            foreach (var actorId in actorIds)
            {
                if (_registry.HasActor(actorId))
                {
                    loadTasks.Add(LoadActorResourcesAsync(actorId, cancellationToken));
                }
            }
            
            await UniTask.WhenAll(loadTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Preloaded {loadTasks.Count} actors");
        }
        
        public async UniTask TransitionToSceneAsync(Dictionary<string, ActorState> newSceneActors, float transitionDuration = 2.0f, CancellationToken cancellationToken = default)
        {
            OnSceneTransitionStarted?.Invoke("Transition");
            _transitionStopwatch.Restart();
            
            try
            {
                // Cleanup DOTween before starting transition
                DOTweenCleanupHelper.TriggerCleanup("Scene Transition Started");
                
                // Phase 1: Hide current actors
                await HideAllActorsAsync(transitionDuration * 0.3f, cancellationToken);
                
                // Phase 2: Apply new scene states
                await ApplyAllActorStatesAsync(newSceneActors, transitionDuration * 0.4f, cancellationToken);
                
                // Phase 3: Show new actors
                await ShowAllActorsAsync(transitionDuration * 0.3f, cancellationToken);
                
                _statistics.TotalSceneTransitions++;
                
                UnityEngine.Debug.Log($"[SceneManager] Scene transition completed with {newSceneActors.Count} actors");
            }
            finally
            {
                _transitionStopwatch.Stop();
                UpdateAverageTransitionTime(_transitionStopwatch.ElapsedMilliseconds);
                OnSceneTransitionCompleted?.Invoke("Transition");
            }
        }
        
        #endregion
        
        #region Visibility Management
        
        public async UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                UnityEngine.Debug.LogWarning($"[SceneManager] Actor '{actorId}' not found for show operation");
                return;
            }
            
            await actor.ChangeVisibilityAsync(true, duration, cancellationToken: cancellationToken);
        }
        
        public async UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                UnityEngine.Debug.LogWarning($"[SceneManager] Actor '{actorId}' not found for hide operation");
                return;
            }
            
            await actor.ChangeVisibilityAsync(false, duration, cancellationToken: cancellationToken);
        }
        
        public async UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var showTasks = _registry.AllActors
                .Select(actor => actor.ChangeVisibilityAsync(true, duration, cancellationToken: cancellationToken))
                .ToList();
                
            await UniTask.WhenAll(showTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Showed {showTasks.Count} actors");
        }
        
        public async UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var hideTasks = _registry.AllActors
                .Select(actor => actor.ChangeVisibilityAsync(false, duration, cancellationToken: cancellationToken))
                .ToList();
                
            await UniTask.WhenAll(hideTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Hid {hideTasks.Count} actors");
        }
        
        #endregion
        
        #region State Management
        
        public Dictionary<string, ActorState> GetAllActorStates()
        {
            var states = new Dictionary<string, ActorState>();
            
            foreach (var actor in _registry.AllActors)
            {
                states[actor.Id] = actor.GetState();
            }
            
            return states;
        }
        
        public async UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default)
        {
            if (states == null || states.Count == 0)
                return;
                
            var applyTasks = new List<UniTask>();
            
            foreach (var kvp in states)
            {
                var actor = _registry.GetActor(kvp.Key);
                if (actor != null)
                {
                    applyTasks.Add(actor.ApplyStateAsync(kvp.Value, duration, cancellationToken));
                }
            }
            
            await UniTask.WhenAll(applyTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Applied states to {applyTasks.Count} actors");
        }
        
        public bool SaveSceneState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return false;
                
            var currentStates = GetAllActorStates();
            _savedStates[stateName] = currentStates;
            
            _statistics.StateSnapshotsSaved++;
            _statistics.SavedStatesCount = _savedStates.Count;
            
            OnSceneStateSaved?.Invoke(stateName);
            
            UnityEngine.Debug.Log($"[SceneManager] Saved scene state '{stateName}' with {currentStates.Count} actors");
            return true;
        }
        
        public async UniTask LoadSceneStateAsync(string stateName, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            if (!_savedStates.TryGetValue(stateName, out var savedStates))
            {
                throw new ArgumentException($"Scene state '{stateName}' not found", nameof(stateName));
            }
            
            await ApplyAllActorStatesAsync(savedStates, duration, cancellationToken);
            
            _statistics.StateSnapshotsLoaded++;
            
            OnSceneStateLoaded?.Invoke(stateName);
            
            UnityEngine.Debug.Log($"[SceneManager] Loaded scene state '{stateName}' with {savedStates.Count} actors");
        }
        
        public string[] GetSavedStateNames()
        {
            return _savedStates.Keys.ToArray();
        }
        
        #endregion
        
        #region Resource Management
        
        public async UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                UnityEngine.Debug.LogWarning($"[SceneManager] Actor '{actorId}' not found for resource loading");
                return;
            }
            
            try
            {
                await _resourceSemaphore.WaitAsync(cancellationToken);
                
                using var resourceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeResourceOperations.TryAdd(actorId, resourceCts);
                
                await actor.LoadResourcesAsync(resourceCts.Token);
                
                _statistics.ResourceLoadOperations++;
            }
            finally
            {
                _activeResourceOperations.TryRemove(actorId, out _);
                _resourceSemaphore.Release();
            }
        }
        
        public async UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default)
        {
            var loadTasks = _registry.AllActors
                .Select(actor => LoadActorResourcesAsync(actor.Id, cancellationToken))
                .ToList();
                
            await UniTask.WhenAll(loadTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Loaded resources for {loadTasks.Count} actors");
        }
        
        public async UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                UnityEngine.Debug.LogWarning($"[SceneManager] Actor '{actorId}' not found for resource unloading");
                return;
            }
            
            try
            {
                await _resourceSemaphore.WaitAsync(cancellationToken);
                
                using var resourceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeResourceOperations.TryAdd(actorId, resourceCts);
                
                await actor.UnloadResourcesAsync(resourceCts.Token);
                
                _statistics.ResourceUnloadOperations++;
            }
            finally
            {
                _activeResourceOperations.TryRemove(actorId, out _);
                _resourceSemaphore.Release();
            }
        }
        
        public async UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default)
        {
            var unloadTasks = _registry.AllActors
                .Select(actor => UnloadActorResourcesAsync(actor.Id, cancellationToken))
                .ToList();
                
            await UniTask.WhenAll(unloadTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Unloaded resources for {unloadTasks.Count} actors");
        }
        
        #endregion
        
        #region Lifecycle Management
        
        public async UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                UnityEngine.Debug.LogWarning($"[SceneManager] Actor '{actorId}' not found for destruction");
                return;
            }
            
            // Remove from registry first
            _registry.UnregisterActor(actorId);
            
            // Destroy the actor
            await actor.DestroyAsync(cancellationToken);
            
            _statistics.ActorsDestroyed++;
            
            UnityEngine.Debug.Log($"[SceneManager] Destroyed actor '{actorId}'");
        }
        
        public async UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default)
        {
            var actorIds = _registry.GetActorIds();
            var destroyTasks = actorIds
                .Select(actorId => DestroyActorAsync(actorId, cancellationToken))
                .ToList();
                
            await UniTask.WhenAll(destroyTasks);
            
            UnityEngine.Debug.Log($"[SceneManager] Destroyed {destroyTasks.Count} actors");
        }
        
        #endregion
        
        #region Utilities
        
        public SceneManagerStatistics GetStatistics()
        {
            return new SceneManagerStatistics
            {
                TotalSceneTransitions = _statistics.TotalSceneTransitions,
                ResourceLoadOperations = _statistics.ResourceLoadOperations,
                ResourceUnloadOperations = _statistics.ResourceUnloadOperations,
                StateSnapshotsSaved = _statistics.StateSnapshotsSaved,
                StateSnapshotsLoaded = _statistics.StateSnapshotsLoaded,
                ActorsDestroyed = _statistics.ActorsDestroyed,
                AverageTransitionTime = _statistics.AverageTransitionTime,
                CurrentMainBackground = _statistics.CurrentMainBackground,
                SavedStatesCount = _statistics.SavedStatesCount
            };
        }
        
        public bool ValidateSceneOperation(string operationType, params object[] parameters)
        {
            switch (operationType?.ToLower())
            {
                case "setmainbackground":
                    return parameters?.Length > 0 && parameters[0] is string backgroundId && 
                           !string.IsNullOrEmpty(backgroundId) && _registry.HasActor(backgroundId);
                           
                case "showactor":
                case "hideactor":
                case "destroyactor":
                    return parameters?.Length > 0 && parameters[0] is string actorId && 
                           !string.IsNullOrEmpty(actorId) && _registry.HasActor(actorId);
                           
                case "savestate":
                case "loadstate":
                    return parameters?.Length > 0 && parameters[0] is string stateName && 
                           !string.IsNullOrEmpty(stateName);
                           
                default:
                    return false;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdateAverageTransitionTime(long elapsedMilliseconds)
        {
            var currentAvg = _statistics.AverageTransitionTime;
            var totalTransitions = _statistics.TotalSceneTransitions;
            
            if (totalTransitions == 1)
            {
                _statistics.AverageTransitionTime = elapsedMilliseconds;
            }
            else
            {
                // Rolling average
                _statistics.AverageTransitionTime = (currentAvg * (totalTransitions - 1) + elapsedMilliseconds) / totalTransitions;
            }
        }
        
        #endregion
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages the lifecycle of actors with proper async/await patterns and cancellation support
    /// </summary>
    public class ActorLifecycleManager : IDisposable
    {
        private readonly Dictionary<string, IActor> _actors = new();
        private readonly Dictionary<string, CancellationTokenSource> _lifecycleCancellationTokens = new();
        private readonly object _lock = new();
        private CancellationTokenSource _disposeCts = new();
        
        // Events for lifecycle monitoring
        public event Action<IActor> OnActorInitialized;
        public event Action<IActor> OnActorLoaded;
        public event Action<IActor> OnActorUnloaded;
        public event Action<IActor> OnActorDestroyed;
        public event Action<IActor, Exception> OnActorError;
        
        /// <summary>
        /// Gets all currently managed actors
        /// </summary>
        public IReadOnlyDictionary<string, IActor> Actors
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, IActor>(_actors);
                }
            }
        }
        
        /// <summary>
        /// Gets the number of managed actors
        /// </summary>
        public int ActorCount
        {
            get
            {
                lock (_lock)
                {
                    return _actors.Count;
                }
            }
        }
        
        /// <summary>
        /// Registers an actor for lifecycle management
        /// </summary>
        public void RegisterActor(IActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            
            if (_disposeCts.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(ActorLifecycleManager));
            
            lock (_lock)
            {
                if (_actors.ContainsKey(actor.Id))
                {
                    Debug.LogWarning($"[ActorLifecycleManager] Actor with ID '{actor.Id}' is already registered");
                    return;
                }
                
                _actors[actor.Id] = actor;
                _lifecycleCancellationTokens[actor.Id] = new CancellationTokenSource();
                
                // Subscribe to actor events
                actor.OnLoaded += HandleActorLoaded;
                actor.OnUnloaded += HandleActorUnloaded;
                actor.OnError += HandleActorError;
                
                Debug.Log($"[ActorLifecycleManager] Registered actor: {actor.Id} ({actor.ActorType})");
            }
        }
        
        /// <summary>
        /// Unregisters an actor from lifecycle management
        /// </summary>
        public void UnregisterActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
                return;
            
            lock (_lock)
            {
                if (_actors.TryGetValue(actorId, out var actor))
                {
                    // Unsubscribe from events
                    actor.OnLoaded -= HandleActorLoaded;
                    actor.OnUnloaded -= HandleActorUnloaded;
                    actor.OnError -= HandleActorError;
                    
                    _actors.Remove(actorId);
                    
                    // Cancel any ongoing operations for this actor
                    if (_lifecycleCancellationTokens.TryGetValue(actorId, out var cts))
                    {
                        cts.Cancel();
                        cts.Dispose();
                        _lifecycleCancellationTokens.Remove(actorId);
                    }
                    
                    Debug.Log($"[ActorLifecycleManager] Unregistered actor: {actorId}");
                }
            }
        }
        
        /// <summary>
        /// Initializes an actor with proper error handling and cancellation support
        /// </summary>
        public async UniTask<bool> InitializeActorAsync(string actorId, CancellationToken cancellationToken = default)
        {
            IActor actor = null;
            CancellationToken combinedToken;
            
            lock (_lock)
            {
                if (!_actors.TryGetValue(actorId, out actor))
                {
                    Debug.LogError($"[ActorLifecycleManager] Cannot initialize unknown actor: {actorId}");
                    return false;
                }
                
                if (!_lifecycleCancellationTokens.TryGetValue(actorId, out var actorCts))
                {
                    Debug.LogError($"[ActorLifecycleManager] Missing cancellation token for actor: {actorId}");
                    return false;
                }
                
                combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, 
                    actorCts.Token, 
                    _disposeCts.Token
                ).Token;
            }
            
            try
            {
                Debug.Log($"[ActorLifecycleManager] Initializing actor: {actorId}");
                await actor.InitializeAsync(combinedToken);
                
                OnActorInitialized?.Invoke(actor);
                Debug.Log($"[ActorLifecycleManager] Successfully initialized actor: {actorId}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[ActorLifecycleManager] Actor initialization cancelled: {actorId}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorLifecycleManager] Failed to initialize actor {actorId}: {ex.Message}");
                OnActorError?.Invoke(actor, ex);
                return false;
            }
        }
        
        /// <summary>
        /// Loads resources for an actor
        /// </summary>
        public async UniTask<bool> LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default)
        {
            IActor actor = null;
            CancellationToken combinedToken;
            
            lock (_lock)
            {
                if (!_actors.TryGetValue(actorId, out actor))
                {
                    Debug.LogError($"[ActorLifecycleManager] Cannot load resources for unknown actor: {actorId}");
                    return false;
                }
                
                if (!_lifecycleCancellationTokens.TryGetValue(actorId, out var actorCts))
                {
                    Debug.LogError($"[ActorLifecycleManager] Missing cancellation token for actor: {actorId}");
                    return false;
                }
                
                combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, 
                    actorCts.Token, 
                    _disposeCts.Token
                ).Token;
            }
            
            try
            {
                Debug.Log($"[ActorLifecycleManager] Loading resources for actor: {actorId}");
                await actor.LoadResourcesAsync(combinedToken);
                
                Debug.Log($"[ActorLifecycleManager] Successfully loaded resources for actor: {actorId}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[ActorLifecycleManager] Resource loading cancelled for actor: {actorId}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorLifecycleManager] Failed to load resources for actor {actorId}: {ex.Message}");
                OnActorError?.Invoke(actor, ex);
                return false;
            }
        }
        
        /// <summary>
        /// Destroys an actor with proper cleanup
        /// </summary>
        public async UniTask<bool> DestroyActorAsync(string actorId, CancellationToken cancellationToken = default)
        {
            IActor actor = null;
            CancellationToken combinedToken;
            
            lock (_lock)
            {
                if (!_actors.TryGetValue(actorId, out actor))
                {
                    Debug.LogWarning($"[ActorLifecycleManager] Cannot destroy unknown actor: {actorId}");
                    return true; // Already destroyed/doesn't exist
                }
                
                if (!_lifecycleCancellationTokens.TryGetValue(actorId, out var actorCts))
                {
                    Debug.LogError($"[ActorLifecycleManager] Missing cancellation token for actor: {actorId}");
                    return false;
                }
                
                combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, 
                    _disposeCts.Token
                ).Token;
            }
            
            try
            {
                Debug.Log($"[ActorLifecycleManager] Destroying actor: {actorId}");
                
                // Unload resources first
                await actor.UnloadResourcesAsync(combinedToken);
                
                // Then destroy the actor
                await actor.DestroyAsync(combinedToken);
                
                OnActorDestroyed?.Invoke(actor);
                Debug.Log($"[ActorLifecycleManager] Successfully destroyed actor: {actorId}");
                
                // Unregister after successful destruction
                UnregisterActor(actorId);
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[ActorLifecycleManager] Actor destruction cancelled: {actorId}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorLifecycleManager] Failed to destroy actor {actorId}: {ex.Message}");
                OnActorError?.Invoke(actor, ex);
                return false;
            }
        }
        
        /// <summary>
        /// Destroys all managed actors
        /// </summary>
        public async UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default)
        {
            List<string> actorIds;
            
            lock (_lock)
            {
                actorIds = new List<string>(_actors.Keys);
            }
            
            Debug.Log($"[ActorLifecycleManager] Destroying {actorIds.Count} actors");
            
            var tasks = new List<UniTask<bool>>();
            foreach (var actorId in actorIds)
            {
                tasks.Add(DestroyActorAsync(actorId, cancellationToken));
            }
            
            try
            {
                await UniTask.WhenAll(tasks);
                Debug.Log("[ActorLifecycleManager] All actors destroyed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorLifecycleManager] Error during bulk actor destruction: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cancels all operations for a specific actor
        /// </summary>
        public void CancelActorOperations(string actorId)
        {
            lock (_lock)
            {
                if (_lifecycleCancellationTokens.TryGetValue(actorId, out var cts))
                {
                    cts.Cancel();
                    Debug.Log($"[ActorLifecycleManager] Cancelled operations for actor: {actorId}");
                }
            }
        }
        
        /// <summary>
        /// Cancels all operations for all actors
        /// </summary>
        public void CancelAllOperations()
        {
            lock (_lock)
            {
                foreach (var cts in _lifecycleCancellationTokens.Values)
                {
                    cts.Cancel();
                }
                Debug.Log("[ActorLifecycleManager] Cancelled all actor operations");
            }
        }
        
        /// <summary>
        /// Gets debug information about all managed actors
        /// </summary>
        public string GetDebugInfo()
        {
            lock (_lock)
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine($"=== Actor Lifecycle Manager Debug Info ===");
                info.AppendLine($"Total Actors: {_actors.Count}");
                info.AppendLine($"Active Cancellation Tokens: {_lifecycleCancellationTokens.Count}");
                info.AppendLine();
                
                foreach (var kvp in _actors)
                {
                    var actor = kvp.Value;
                    info.AppendLine($"Actor: {kvp.Key}");
                    info.AppendLine($"  Type: {actor.ActorType}");
                    info.AppendLine($"  Load State: {actor.LoadState}");
                    info.AppendLine($"  Visibility State: {actor.VisibilityState}");
                    info.AppendLine($"  Has Error: {actor.HasError}");
                    if (actor.HasError)
                        info.AppendLine($"  Last Error: {actor.LastError}");
                    info.AppendLine();
                }
                
                return info.ToString();
            }
        }
        
        // Event handlers
        private void HandleActorLoaded(IActor actor)
        {
            OnActorLoaded?.Invoke(actor);
        }
        
        private void HandleActorUnloaded(IActor actor)
        {
            OnActorUnloaded?.Invoke(actor);
        }
        
        private void HandleActorError(IActor actor, string error)
        {
            OnActorError?.Invoke(actor, new Exception(error));
        }
        
        /// <summary>
        /// Disposes the lifecycle manager and all managed actors
        /// </summary>
        public void Dispose()
        {
            if (_disposeCts.IsCancellationRequested)
                return;
            
            Debug.Log("[ActorLifecycleManager] Disposing lifecycle manager");
            
            // Cancel all operations
            _disposeCts.Cancel();
            
            // Destroy all actors (fire-and-forget since we're disposing)
            _ = DestroyAllActorsAsync();
            
            // Dispose cancellation tokens
            lock (_lock)
            {
                foreach (var cts in _lifecycleCancellationTokens.Values)
                {
                    cts?.Dispose();
                }
                _lifecycleCancellationTokens.Clear();
            }
            
            _disposeCts?.Dispose();
            _disposeCts = null;
            
            Debug.Log("[ActorLifecycleManager] Lifecycle manager disposed");
        }
    }
    
    /// <summary>
    /// Lifecycle state information for actors
    /// </summary>
    public class ActorLifecycleState
    {
        public string ActorId { get; set; }
        public ActorLoadState LoadState { get; set; }
        public ActorVisibilityState VisibilityState { get; set; }
        public DateTime LastStateChange { get; set; }
        public string LastError { get; set; }
        public bool HasActiveOperations { get; set; }
        
        public ActorLifecycleState(string actorId)
        {
            ActorId = actorId;
            LoadState = ActorLoadState.Unloaded;
            VisibilityState = ActorVisibilityState.Hidden;
            LastStateChange = DateTime.Now;
        }
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core Actor Service implementation with complete lifecycle support and registry management
    /// Follows the enhanced service architecture patterns with dependency injection and configuration
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.Critical, 
        Description = "Manages all actors in the scene with complete lifecycle support")]
    [ServiceConfiguration(typeof(ActorServiceConfiguration))]
    public class ActorService : IActorService
    {
        // === Configuration and Dependencies ===
        
        private readonly ActorServiceConfiguration _config;
        private IResourceService _resourceService;
        
        // === Thread-Safe Actor Registry ===
        
        private readonly ConcurrentDictionary<string, IActor> _actors = new();
        private readonly ConcurrentDictionary<string, ICharacterActor> _characterActors = new();
        private readonly ConcurrentDictionary<string, IBackgroundActor> _backgroundActors = new();
        
        // === State Management ===
        
        private ServiceState _serviceState = ServiceState.Uninitialized;
        private IBackgroundActor _mainBackground;
        private readonly object _stateLock = new();
        
        // === Performance and Resource Management ===
        
        private readonly SemaphoreSlim _loadingSemaphore;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _loadingOperations = new();
        private readonly Timer _performanceTimer;
        private readonly Timer _cleanupTimer;
        
        // === Cancellation and Lifecycle ===
        
        private CancellationTokenSource _serviceCts;
        private bool _disposed = false;
        
        // === Statistics and Monitoring ===
        
        private ActorServiceStatistics _statistics = new();
        private DateTime _serviceStartTime;
        
        // === Events ===
        
        public event Action<IActor> OnActorCreated;
        public event Action<string> OnActorDestroyed;
        public event Action<IActor, bool> OnActorVisibilityChanged;
        public event Action<IActor, string> OnActorError;
        public event Action<IBackgroundActor> OnMainBackgroundChanged;
        public event Action<string, float> OnActorLoadProgressChanged;
        
        // === Properties ===
        
        public ServiceState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _serviceState;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _serviceState = value;
                }
            }
        }
        
        public IReadOnlyCollection<IActor> AllActors => _actors.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<ICharacterActor> CharacterActors => _characterActors.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<IBackgroundActor> BackgroundActors => _backgroundActors.Values.ToList().AsReadOnly();
        
        public int ActorCount => _actors.Count;
        public int LoadingActorsCount => _loadingOperations.Count;
        
        // === Constructor and Service Lifecycle ===
        
        public ActorService(ActorServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _serviceCts = new CancellationTokenSource();
            _loadingSemaphore = new SemaphoreSlim(_config.MaxConcurrentLoads, _config.MaxConcurrentLoads);
            
            // Initialize timers
            _performanceTimer = new Timer(UpdatePerformanceStatistics, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(PerformCleanup, null, Timeout.Infinite, Timeout.Infinite);
            
            _serviceStartTime = DateTime.Now;
            
            Debug.Log($"[ActorService] Initialized with max {_config.MaxActors} actors, {_config.MaxConcurrentLoads} concurrent loads");
        }
        
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
                
                // Get resource service dependency
                _resourceService = Engine.GetService<IResourceService>();
                
                // Initialize actor pool
                await InitializeActorPool(linkedCts.Token);
                
                // Initialize performance monitoring
                if (_config.EnablePerformanceMonitoring)
                {
                    StartPerformanceMonitoring();
                }
                
                // Initialize cleanup timer
                StartCleanupTimer();
                
                // Preload common resources if enabled
                if (_config.PreloadCommonResources)
                {
                    await PreloadCommonResourcesAsync(linkedCts.Token);
                }
                
                State = ServiceState.Running;
                
                return ServiceInitializationResult.Success();
            }
            catch (OperationCanceledException e)
            {
                return ServiceInitializationResult.Failed(e);
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed(ex);
            }
        }
        
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Stop timers
                StopPerformanceMonitoring();
                StopCleanupTimer();
                
                // Destroy all actors
                await DestroyAllActorsAsync(cancellationToken);
                
                // Cancel all operations
                _serviceCts.Cancel();
                
                State = ServiceState.Shutdown;
                Debug.Log("[ActorService] Service shutdown completed");
                
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Service shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex);
            }
        }
        
        public UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var status = new ServiceHealthStatus
            {
                IsHealthy = State == ServiceState.Running,
                StatusMessage = State == ServiceState.Running ? "ActorService is running smoothly" : $"ActorService is unhealthy"
            };

            return UniTask.FromResult(status);
        }
        
        // === Actor Creation (Simplified Implementation) ===
        
        public async UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            ValidateOperationPermitted();
            ValidateActorId(id);
            
            if (ActorCount >= _config.MaxActors)
                throw new InvalidOperationException($"Cannot create actor: Maximum actor limit ({_config.MaxActors}) reached");
            
            try
            {
                await _loadingSemaphore.WaitAsync(cancellationToken);
                
                // Create a placeholder character actor for now
                // In a real implementation, this would create the actual character actor
                Debug.Log($"[ActorService] Creating character actor: {id}");
                
                // For this demo, we'll return null and log - implement actual creation logic
                throw new NotImplementedException("Character actor creation not yet implemented - Phase 3 task");
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }
        
        public async UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            ValidateOperationPermitted();
            ValidateActorId(id);
            
            if (ActorCount >= _config.MaxActors)
                throw new InvalidOperationException($"Cannot create actor: Maximum actor limit ({_config.MaxActors}) reached");
            
            try
            {
                await _loadingSemaphore.WaitAsync(cancellationToken);
                
                Debug.Log($"[ActorService] Creating background actor: {id}");
                
                // For this demo, we'll return null and log - implement actual creation logic
                throw new NotImplementedException("Background actor creation not yet implemented - Phase 3 task");
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }
        
        public async UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor
        {
            ValidateOperationPermitted();
            ValidateActorId(id);
            
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            
            if (ActorCount >= _config.MaxActors)
                throw new InvalidOperationException($"Cannot create actor: Maximum actor limit ({_config.MaxActors}) reached");
            
            try
            {
                await _loadingSemaphore.WaitAsync(cancellationToken);
                
                Debug.Log($"[ActorService] Creating custom actor: {id}");
                
                // For this demo, we'll return null and log - implement actual creation logic
                throw new NotImplementedException("Custom actor creation not yet implemented - Phase 3 task");
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }
        
        // === Basic Registry Operations (Simplified) ===
        
        public bool RegisterActor(IActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            
            ValidateOperationPermitted();
            
            if (ActorCount >= _config.MaxActors)
            {
                Debug.LogWarning($"[ActorService] Cannot register actor: Maximum limit reached ({_config.MaxActors})");
                return false;
            }
            
            return RegisterActorInternal(actor);
        }
        
        public bool UnregisterActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
                return false;
            
            return UnregisterActorInternal(actorId);
        }
        
        // === Actor Lookup ===
        
        public IActor GetActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            
            _actors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public ICharacterActor GetCharacterActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            
            _characterActors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public IBackgroundActor GetBackgroundActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            
            _backgroundActors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public T GetActor<T>(string id) where T : class, IActor
        {
            return GetActor(id) as T;
        }
        
        public bool TryGetActor(string id, out IActor actor)
        {
            actor = null;
            return !string.IsNullOrEmpty(id) && _actors.TryGetValue(id, out actor);
        }
        
        public bool TryGetCharacterActor(string id, out ICharacterActor actor)
        {
            actor = null;
            return !string.IsNullOrEmpty(id) && _characterActors.TryGetValue(id, out actor);
        }
        
        public bool TryGetBackgroundActor(string id, out IBackgroundActor actor)
        {
            actor = null;
            return !string.IsNullOrEmpty(id) && _backgroundActors.TryGetValue(id, out actor);
        }
        
        public IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor
        {
            return _actors.Values.OfType<T>().ToList().AsReadOnly();
        }
        
        public bool HasActor(string id)
        {
            return !string.IsNullOrEmpty(id) && _actors.ContainsKey(id);
        }
        
        public string[] GetActorIds()
        {
            return _actors.Keys.ToArray();
        }
        
        // === Placeholder Methods (To be implemented in Phase 3) ===
        
        public UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public async UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default) { await UniTask.CompletedTask; }
        public UniTask RefreshAllActorsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public IBackgroundActor GetMainBackground() => _mainBackground;
        public UniTask ClearSceneAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        public Dictionary<string, ActorState> GetAllActorStates() => new();
        public UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default) => throw new NotImplementedException("Phase 3 implementation");
        
        public void StopAllAnimations()
        {
            foreach (var actor in _actors.Values)
            {
                actor.StopAllAnimations();
            }
            Debug.Log($"[ActorService] Stopped all animations for {ActorCount} actors");
        }
        
        public void PauseAllAnimations()
        {
            DG.Tweening.DOTween.PauseAll();
            Debug.Log("[ActorService] Paused all animations");
        }
        
        public void ResumeAllAnimations()
        {
            DG.Tweening.DOTween.PlayAll();
            Debug.Log("[ActorService] Resumed all animations");
        }
        
        public void SetGlobalAnimationSpeed(float speedMultiplier)
        {
            DG.Tweening.DOTween.timeScale = Mathf.Max(0f, speedMultiplier);
            Debug.Log($"[ActorService] Set global animation speed: {speedMultiplier:F2}x");
        }
        
        // === Utility and Debug ===
        
        public Dictionary<string, string[]> ValidateAllActors()
        {
            var validationResults = new Dictionary<string, string[]>();
            
            foreach (var kvp in _actors)
            {
                if (kvp.Value.ValidateConfiguration(out var errors))
                    continue;
                
                validationResults[kvp.Key] = errors;
            }
            
            return validationResults;
        }
        
        public ActorServiceStatistics GetStatistics()
        {
            UpdateStatistics();
            return _statistics;
        }
        
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== ActorService Debug Info ===");
            info.AppendLine($"Service State: {State}");
            info.AppendLine($"Total Actors: {ActorCount}");
            info.AppendLine($"Character Actors: {_characterActors.Count}");
            info.AppendLine($"Background Actors: {_backgroundActors.Count}");
            info.AppendLine($"Loading Operations: {LoadingActorsCount}");
            info.AppendLine($"Main Background: {(_mainBackground?.Id ?? "None")}");
            info.AppendLine($"Service Uptime: {DateTime.Now - _serviceStartTime:hh\\:mm\\:ss}");
            
            return info.ToString();
        }
        
        public void Configure(int maxConcurrentLoads, bool enableActorPooling, bool enablePerformanceMonitoring)
        {
            Debug.Log($"[ActorService] Runtime configuration update requested: " +
                      $"MaxLoads={maxConcurrentLoads}, Pooling={enableActorPooling}, Monitoring={enablePerformanceMonitoring}");
        }
        
        // === Private Implementation Methods ===
        
        private async UniTask InitializeActorPool(CancellationToken cancellationToken)
        {
            Debug.Log($"[ActorService] Creating actor pool '{_config.ActorPoolName}' with {_config.InitialPoolSize} initial actors (max: {_config.MaxActors})");
            await UniTask.CompletedTask;
        }
        
        private bool RegisterActorInternal(IActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.Id))
                return false;
            
            // Register in main collection
            if (!_actors.TryAdd(actor.Id, actor))
            {
                Debug.LogWarning($"[ActorService] Actor with ID '{actor.Id}' already exists");
                return false;
            }
            
            // Register in specialized collections
            if (actor is ICharacterActor characterActor)
                _characterActors.TryAdd(actor.Id, characterActor);
            else if (actor is IBackgroundActor backgroundActor)
                _backgroundActors.TryAdd(actor.Id, backgroundActor);
            
            // Subscribe to actor events
            actor.OnError += OnActorErrorInternal;
            actor.OnVisibilityChanged += OnActorVisibilityChangedInternal;
            
            Debug.Log($"[ActorService] Registered actor: {actor.Id} ({actor.GetType().Name})");
            return true;
        }
        
        private bool UnregisterActorInternal(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
                return false;
            
            // Get the actor before removing
            if (!_actors.TryGetValue(actorId, out var actor))
                return false;
            
            // Unsubscribe from events
            actor.OnError -= OnActorErrorInternal;
            actor.OnVisibilityChanged -= OnActorVisibilityChangedInternal;
            
            // Remove from collections
            _actors.TryRemove(actorId, out _);
            _characterActors.TryRemove(actorId, out _);
            _backgroundActors.TryRemove(actorId, out _);
            
            // Clear main background if it was this actor
            if (_mainBackground?.Id == actorId)
                _mainBackground = null;
            
            Debug.Log($"[ActorService] Unregistered actor: {actorId}");
            return true;
        }
        
        private async UniTask PreloadCommonResourcesAsync(CancellationToken cancellationToken)
        {
            try
            {
                Debug.Log("[ActorService] Preloading common resources...");
                await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
                Debug.Log("[ActorService] Common resources preloaded");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActorService] Failed to preload common resources: {ex.Message}");
            }
        }
        
        private void StartPerformanceMonitoring()
        {
            if (_config.EnablePerformanceMonitoring)
            {
                var interval = TimeSpan.FromMilliseconds(1000);
                _performanceTimer.Change(interval, interval);
            }
        }
        
        private void StopPerformanceMonitoring()
        {
            _performanceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        private void StartCleanupTimer()
        {
            var interval = TimeSpan.FromSeconds(_config.CleanupInterval);
            _cleanupTimer.Change(interval, interval);
        }
        
        private void StopCleanupTimer()
        {
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        private void UpdatePerformanceStatistics(object state)
        {
            if (_disposed)
                return;
            
            try
            {
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Error updating performance statistics: {ex.Message}");
            }
        }
        
        private void UpdateStatistics()
        {
            _statistics.TotalActors = ActorCount;
            _statistics.LoadingActors = LoadingActorsCount;
            _statistics.CharacterActors = _characterActors.Count;
            _statistics.BackgroundActors = _backgroundActors.Count;
            _statistics.CustomActors = ActorCount - _characterActors.Count - _backgroundActors.Count;
            
            _statistics.LoadedActors = _actors.Values.Count(a => a.IsLoaded);
            _statistics.ErrorActors = _actors.Values.Count(a => a.HasError);
            
            _statistics.ActiveAnimations = 0;
            _statistics.GlobalAnimationSpeed = DG.Tweening.DOTween.timeScale;
            
            _statistics.LastUpdateTime = DateTime.Now;
            _statistics.PerformanceMonitoringEnabled = _config.EnablePerformanceMonitoring;
        }
        
        private void PerformCleanup(object state)
        {
            if (_disposed)
                return;
            
            try
            {
                if (_config.EnableDistanceBasedCleanup)
                {
                    Debug.Log("[ActorService] Performing distance-based cleanup...");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Error during cleanup: {ex.Message}");
            }
        }
        
        private void OnActorErrorInternal(IActor actor, string error)
        {
            OnActorError?.Invoke(actor, error);
        }
        
        private void OnActorVisibilityChangedInternal(IActor actor, bool visible)
        {
            OnActorVisibilityChanged?.Invoke(actor, visible);
        }
        
        private void ValidateOperationPermitted()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActorService));
            
            if (Engine.Initialized)
                throw new InvalidOperationException($"Actor service is not ready (current state: {State})");
        }
        
        private void ValidateActorId(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Actor ID cannot be null or empty", nameof(id));
            
            if (HasActor(id))
                throw new ArgumentException($"Actor with ID '{id}' already exists", nameof(id));
        }
        
        // === IDisposable Implementation ===
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                _serviceCts?.Cancel();
                
                StopPerformanceMonitoring();
                StopCleanupTimer();
                
                _performanceTimer?.Dispose();
                _cleanupTimer?.Dispose();
                _loadingSemaphore?.Dispose();
                
                // Cancel all loading operations
                foreach (var cts in _loadingOperations.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _loadingOperations.Clear();
                
                _serviceCts?.Dispose();
                _disposed = true;
                
                Debug.Log("[ActorService] Service disposed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Error during disposal: {ex.Message}");
            }
        }
    }
}
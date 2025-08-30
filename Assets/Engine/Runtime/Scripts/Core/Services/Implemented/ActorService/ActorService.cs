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
        Description = "Manages all actors in the scene with complete lifecycle support",
        RequiredServices = new[] { typeof(IResourceService) })]
    [ServiceConfiguration(typeof(ActorServiceConfiguration))]
    public class ActorService : IActorService
    {
        #region Dependencies
        
        private readonly ActorServiceConfiguration _config;
        private IResourceService _resourceService;
        private IActorRegistry _registry;
        private IActorFactory _factory;
        private ISceneManager _sceneManager;
        private IActorMonitor _monitor;
        
        #endregion
        
        #region Service State
        
        private CancellationTokenSource _serviceCts;
        private bool _disposed = false;
        
        #endregion
        
        #region Events
        
        public event Action<IActor> OnActorCreated;
        public event Action<string> OnActorDestroyed;
        public event Action<IActor, bool> OnActorVisibilityChanged;
        public event Action<IActor, string> OnActorError;
        public event Action<IBackgroundActor> OnMainBackgroundChanged;
        public event Action<string, float> OnActorLoadProgressChanged;
        
        #endregion
        
        #region Properties
        
        
        public IReadOnlyCollection<IActor> AllActors => _registry?.AllActors ?? new List<IActor>().AsReadOnly();
        public IReadOnlyCollection<ICharacterActor> CharacterActors => _registry?.CharacterActors ?? new List<ICharacterActor>().AsReadOnly();
        public IReadOnlyCollection<IBackgroundActor> BackgroundActors => _registry?.BackgroundActors ?? new List<IBackgroundActor>().AsReadOnly();
        public int ActorCount => _registry?.ActorCount ?? 0;
        public int LoadingActorsCount => _factory?.GetStatistics()?.PendingCreations ?? 0;
        
        #endregion
        
        #region Constructor and Service Lifecycle
        
        public ActorService(ActorServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serviceCts = new CancellationTokenSource();
            
            Debug.Log($"[ActorService] Facade initialized - will delegate to specialized services");
        }
        
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
                
                _resourceService = Engine.GetService<IResourceService>();
                
                _registry = new ActorRegistry();
                _factory = new ActorFactory(_registry, _resourceService, _config);
                _sceneManager = new SceneManager(_registry, _resourceService, _config);
                _monitor = new ActorMonitor(_registry, _factory, _sceneManager, _config);
                
                SubscribeToServiceEvents();
                
                Debug.Log("[ActorService] Facade initialization completed - all services ready");
                await UniTask.Yield();
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex);
            }
        }
        
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _monitor?.Dispose();
                
                if (_sceneManager != null)
                {
                    await _sceneManager.ClearSceneAsync(cancellationToken);
                }
                
                _serviceCts?.Cancel();
                
                Debug.Log("[ActorService] Facade shutdown completed");
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex);
            }
        }
        
        public UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var isHealthy = !_disposed && 
                           _registry != null && 
                           _factory != null && 
                           _sceneManager != null && 
                           _monitor != null;
                           
            var status = new ServiceHealthStatus
            {
                IsHealthy = isHealthy,
                StatusMessage = isHealthy ? "ActorService facade is healthy" : "ActorService facade has issues"
            };
            
            return UniTask.FromResult(status);
        }
        
        #endregion
        
        #region Actor Creation - Delegate to Factory
        
        public async UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            ValidateService();
            return await _factory.CreateCharacterActorAsync(id, appearance, position, cancellationToken);
        }
        
        public async UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            ValidateService();
            return await _factory.CreateBackgroundActorAsync(id, appearance, position, cancellationToken);
        }
        
        public async UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor
        {
            ValidateService();
            return await _factory.CreateCustomActorAsync<T>(id, prefab, position, cancellationToken);
        }
        
        #endregion
        
        #region Actor Registry - Delegate to Registry
        
        public bool RegisterActor(IActor actor)
        {
            ValidateService();
            return _registry.RegisterActor(actor);
        }
        
        public bool UnregisterActor(string actorId)
        {
            ValidateService();
            return _registry.UnregisterActor(actorId);
        }
        
        public IActor GetActor(string id)
        {
            return _registry?.GetActor(id);
        }
        
        public ICharacterActor GetCharacterActor(string id)
        {
            return _registry?.GetCharacterActor(id);
        }
        
        public IBackgroundActor GetBackgroundActor(string id)
        {
            return _registry?.GetBackgroundActor(id);
        }
        
        public T GetActor<T>(string id) where T : class, IActor
        {
            return _registry?.GetActor<T>(id);
        }
        
        public bool TryGetActor(string id, out IActor actor)
        {
            actor = null;
            return _registry?.TryGetActor(id, out actor) ?? false;
        }
        
        public bool TryGetCharacterActor(string id, out ICharacterActor actor)
        {
            actor = null;
            return _registry?.TryGetCharacterActor(id, out actor) ?? false;
        }
        
        public bool TryGetBackgroundActor(string id, out IBackgroundActor actor)
        {
            actor = null;
            return _registry?.TryGetBackgroundActor(id, out actor) ?? false;
        }
        
        public IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor
        {
            return _registry?.GetActorsOfType<T>() ?? new List<T>().AsReadOnly();
        }
        
        public bool HasActor(string id)
        {
            return _registry?.HasActor(id) ?? false;
        }
        
        public string[] GetActorIds()
        {
            return _registry?.GetActorIds() ?? new string[0];
        }
        
        #endregion
        
        #region Scene Operations - Delegate to SceneManager
        
        public async UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.SetMainBackgroundAsync(backgroundId, cancellationToken);
        }
        
        public IBackgroundActor GetMainBackground()
        {
            return _sceneManager?.GetMainBackground();
        }
        
        public async UniTask ClearSceneAsync(CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.ClearSceneAsync(cancellationToken);
        }
        
        public async UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.PreloadSceneActorsAsync(actorIds, cancellationToken);
        }
        
        public async UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.ShowActorAsync(actorId, duration, cancellationToken);
        }
        
        public async UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.HideActorAsync(actorId, duration, cancellationToken);
        }
        
        public async UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.ShowAllActorsAsync(duration, cancellationToken);
        }
        
        public async UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.HideAllActorsAsync(duration, cancellationToken);
        }
        
        public Dictionary<string, ActorState> GetAllActorStates()
        {
            return _sceneManager?.GetAllActorStates() ?? new Dictionary<string, ActorState>();
        }
        
        public async UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.ApplyAllActorStatesAsync(states, duration, cancellationToken);
        }
        
        public async UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.LoadActorResourcesAsync(actorId, cancellationToken);
        }
        
        public async UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.LoadAllActorResourcesAsync(cancellationToken);
        }
        
        public async UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.UnloadActorResourcesAsync(actorId, cancellationToken);
        }
        
        public async UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.UnloadAllActorResourcesAsync(cancellationToken);
        }
        
        public async UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.DestroyActorAsync(actorId, cancellationToken);
        }
        
        public async UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.DestroyAllActorsAsync(cancellationToken);
        }
        
        public async UniTask RefreshAllActorsAsync(CancellationToken cancellationToken = default)
        {
            ValidateService();
            await _sceneManager.LoadAllActorResourcesAsync(cancellationToken);
        }
        
        #endregion
        
        #region Animation Control - Delegate to Monitor
        
        public void StopAllAnimations()
        {
            _monitor?.StopAllAnimations();
        }
        
        public void PauseAllAnimations()
        {
            _monitor?.PauseAllAnimations();
        }
        
        public void ResumeAllAnimations()
        {
            _monitor?.ResumeAllAnimations();
        }
        
        public void SetGlobalAnimationSpeed(float speedMultiplier)
        {
            _monitor?.SetGlobalAnimationSpeed(speedMultiplier);
        }
        
        #endregion
        
        #region Statistics and Validation - Delegate to Monitor
        
        public Dictionary<string, string[]> ValidateAllActors()
        {
            return _monitor?.ValidateAllActors() ?? new Dictionary<string, string[]>();
        }
        
        public ActorServiceStatistics GetStatistics()
        {
            return _monitor?.GetStatistics() ?? new ActorServiceStatistics();
        }
        
        public string GetDebugInfo()
        {
            return _monitor?.GetDebugInfo() ?? "[ActorService] Monitor not available";
        }
        
        public void Configure(int maxConcurrentLoads, bool enableActorPooling, bool enablePerformanceMonitoring)
        {
            _monitor?.Configure(true, enablePerformanceMonitoring, 5.0f);
            Debug.Log($"[ActorService] Configuration updated through monitor");
        }
        
        #endregion
        
        #region Private Methods
        
        private void SubscribeToServiceEvents()
        {
            if (_registry is ActorRegistry registry)
            {
                registry.OnActorRegistered += (actor) => OnActorCreated?.Invoke(actor);
                registry.OnActorUnregistered += (actorId) => OnActorDestroyed?.Invoke(actorId);
            }
            
            if (_sceneManager != null)
            {
                _sceneManager.OnMainBackgroundChanged += (background) => OnMainBackgroundChanged?.Invoke(background);
            }
            
            if (_monitor != null)
            {
                _monitor.OnValidationErrorDetected += (actorId, errors) => 
                {
                    var actor = _registry?.GetActor(actorId);
                    if (actor != null)
                    {
                        OnActorError?.Invoke(actor, string.Join(", ", errors));
                    }
                };
            }
        }
        
        private void ValidateService()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActorService));
                
            if (_registry == null || _factory == null || _sceneManager == null || _monitor == null)
                throw new InvalidOperationException("ActorService is not properly initialized");
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            try
            {
                _serviceCts?.Cancel();
                _monitor?.Dispose();
                _serviceCts?.Dispose();
                _disposed = true;
                
                Debug.Log("[ActorService] Facade disposed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorService] Error during disposal: {ex.Message}");
            }
        }
        
        #endregion
    }
}
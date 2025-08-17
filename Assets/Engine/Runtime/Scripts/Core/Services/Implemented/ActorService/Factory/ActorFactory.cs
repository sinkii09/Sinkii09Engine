using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

using System.Threading;
using UnityEngine;
using ZLinq;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Thread-safe actor factory implementation with object pooling and validation
    /// Manages actor creation with performance optimization and resource management
    /// </summary>
    public class ActorFactory : IActorFactory
    {
        #region Private Fields
        
        private readonly ActorServiceConfiguration _config;
        private readonly IResourceService _resourceService;
        private readonly IActorRegistry _registry;
        
        // Concurrency control
        private readonly SemaphoreSlim _creationSemaphore;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCreations = new();
        
        // Object pooling
        private readonly ConcurrentDictionary<ActorType, Queue<IActor>> _actorPools = new();
        private readonly object _poolLock = new();
        private bool _poolingEnabled;
        private int _defaultPoolSize;
        
        // Statistics
        private ActorFactoryStatistics _statistics = new();
        private readonly Stopwatch _creationStopwatch = new();
        
        // Configuration
        private bool _validationEnabled = true;
        
        #endregion
        
        #region Constructor
        
        public ActorFactory(IActorRegistry registry, IResourceService resourceService, ActorServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            
            _creationSemaphore = new SemaphoreSlim(_config.MaxConcurrentLoads, _config.MaxConcurrentLoads);
            _poolingEnabled = _config.EnableObjectPooling;
            _defaultPoolSize = _config.InitialPoolSize;
            
            InitializePools();
            
            UnityEngine.Debug.Log($"[ActorFactory] Initialized with pooling {(_poolingEnabled ? "enabled" : "disabled")}");
        }
        
        #endregion
        
        #region Actor Creation Operations
        
        public async UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            if (_validationEnabled && !ValidateActorCreation(id, ActorType.Character))
            {
                _statistics.ValidationFailures++;
                throw new ArgumentException($"Invalid character actor creation parameters for ID: {id}");
            }
            
            _creationStopwatch.Restart();
            
            try
            {
                await _creationSemaphore.WaitAsync(cancellationToken);
                
                using var creationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCreations.TryAdd(id, creationCts);
                
                ICharacterActor character;
                
                // Try to get from pool first
                if (_poolingEnabled && TryGetFromPool<ICharacterActor>(ActorType.Character, out var pooledActor))
                {
                    character = pooledActor;
                    _statistics.PoolHits++;
                    
                    // Reconfigure pooled actor
                    await ReconfigurePooledCharacterAsync(character, id, appearance, position, creationCts.Token);
                }
                else
                {
                    // Create new actor
                    character = await CreateNewCharacterActorAsync(id, appearance, position, creationCts.Token);
                    _statistics.PoolMisses++;
                }
                
                // Register with the registry
                if (!_registry.RegisterActor(character))
                {
                    throw new InvalidOperationException($"Failed to register character actor with ID: {id}");
                }
                
                _statistics.CharactersCreated++;
                _statistics.TotalCreated++;
                
                return character;
            }
            finally
            {
                _activeCreations.TryRemove(id, out _);
                _creationSemaphore.Release();
                
                _creationStopwatch.Stop();
                UpdateAverageCreationTime(_creationStopwatch.ElapsedMilliseconds);
            }
        }
        
        public async UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            if (_validationEnabled && !ValidateActorCreation(id, ActorType.Background))
            {
                _statistics.ValidationFailures++;
                throw new ArgumentException($"Invalid background actor creation parameters for ID: {id}");
            }
            
            _creationStopwatch.Restart();
            
            try
            {
                await _creationSemaphore.WaitAsync(cancellationToken);
                
                using var creationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCreations.TryAdd(id, creationCts);
                
                IBackgroundActor background;
                
                // Try to get from pool first
                if (_poolingEnabled && TryGetFromPool<IBackgroundActor>(ActorType.Background, out var pooledActor))
                {
                    background = pooledActor;
                    _statistics.PoolHits++;
                    
                    // Reconfigure pooled actor
                    await ReconfigurePooledBackgroundAsync(background, id, appearance, position, creationCts.Token);
                }
                else
                {
                    // Create new actor
                    background = await CreateNewBackgroundActorAsync(id, appearance, position, creationCts.Token);
                    _statistics.PoolMisses++;
                }
                
                // Register with the registry
                if (!_registry.RegisterActor(background))
                {
                    throw new InvalidOperationException($"Failed to register background actor with ID: {id}");
                }
                
                _statistics.BackgroundsCreated++;
                _statistics.TotalCreated++;
                
                return background;
            }
            finally
            {
                _activeCreations.TryRemove(id, out _);
                _creationSemaphore.Release();
                
                _creationStopwatch.Stop();
                UpdateAverageCreationTime(_creationStopwatch.ElapsedMilliseconds);
            }
        }
        
        public async UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor
        {
            if (_validationEnabled && !ValidatePrefab(prefab, typeof(T)))
            {
                _statistics.ValidationFailures++;
                throw new ArgumentException($"Invalid prefab for custom actor creation: {prefab?.name ?? "null"}");
            }
            
            if (_validationEnabled && !ValidateActorCreation(id, ActorType.Character)) // Default to Character for custom
            {
                _statistics.ValidationFailures++;
                throw new ArgumentException($"Invalid custom actor creation parameters for ID: {id}");
            }
            
            _creationStopwatch.Restart();
            
            try
            {
                await _creationSemaphore.WaitAsync(cancellationToken);
                
                using var creationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCreations.TryAdd(id, creationCts);
                
                // Create custom actor from prefab
                var gameObject = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                gameObject.name = id;
                
                var customActor = gameObject.GetComponent<T>();
                if (customActor == null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                    throw new InvalidOperationException($"Prefab does not contain component of type {typeof(T).Name}");
                }
                
                // Initialize if it's an IActor
                if (customActor is IActor actor)
                {
                    await actor.InitializeAsync(creationCts.Token);
                    
                    // Register with the registry
                    if (!_registry.RegisterActor(actor))
                    {
                        await actor.DestroyAsync(creationCts.Token);
                        throw new InvalidOperationException($"Failed to register custom actor with ID: {id}");
                    }
                }
                
                _statistics.CustomActorsCreated++;
                _statistics.TotalCreated++;
                
                return customActor;
            }
            finally
            {
                _activeCreations.TryRemove(id, out _);
                _creationSemaphore.Release();
                
                _creationStopwatch.Stop();
                UpdateAverageCreationTime(_creationStopwatch.ElapsedMilliseconds);
            }
        }
        
        #endregion
        
        #region Pool Management
        
        public void ConfigurePooling(bool enabled, int defaultPoolSize)
        {
            _poolingEnabled = enabled;
            _defaultPoolSize = defaultPoolSize;
            
            if (!enabled)
            {
                ClearAllPools();
            }
            
            UnityEngine.Debug.Log($"[ActorFactory] Pooling {(enabled ? "enabled" : "disabled")} with default size {defaultPoolSize}");
        }
        
        public int GetPoolSize(ActorType actorType)
        {
            if (!_poolingEnabled || !_actorPools.TryGetValue(actorType, out var pool))
                return 0;
                
            lock (_poolLock)
            {
                return pool.Count;
            }
        }
        
        public async UniTask WarmupPoolAsync(ActorType actorType, int count, CancellationToken cancellationToken = default)
        {
            if (!_poolingEnabled)
                return;
                
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                IActor actor = actorType switch
                {
                    var t when t == ActorType.Character => await CreateNewCharacterActorAsync($"pool_char_{i}", CharacterAppearance.Default, Vector3.zero, cancellationToken),
                    var t when t == ActorType.Background => await CreateNewBackgroundActorAsync($"pool_bg_{i}", BackgroundAppearance.Default, Vector3.zero, cancellationToken),
                    _ => null
                };
                
                if (actor != null)
                {
                    ReturnToPool(actor);
                }
            }
            
            UnityEngine.Debug.Log($"[ActorFactory] Warmed up pool for {actorType} with {count} actors");
        }
        
        public bool ReturnToPool(IActor actor)
        {
            if (!_poolingEnabled || actor == null)
                return false;
                
            if (!_actorPools.TryGetValue(actor.ActorType, out var pool))
                return false;
                
            lock (_poolLock)
            {
                if (pool.Count >= _defaultPoolSize)
                    return false; // Pool is full
                    
                // Reset actor state for pooling
                actor.GameObject.SetActive(false);
                pool.Enqueue(actor);
                _statistics.CurrentPoolSize++;
                
                return true;
            }
        }
        
        #endregion
        
        #region Validation
        
        public bool ValidateActorCreation(string id, ActorType actorType)
        {
            if (string.IsNullOrEmpty(id))
                return false;
                
            if (_registry.HasActor(id))
                return false;
                
            return CanCreateActorType(actorType);
        }
        
        public bool CanCreateActorType(ActorType actorType)
        {
            // Check if we're at the limit
            if (_registry.ActorCount >= _config.MaxActors)
                return false;
                
            // Actor type specific checks could go here
            return actorType == ActorType.Character || actorType == ActorType.Background;
        }
        
        public bool ValidatePrefab(GameObject prefab, Type expectedType)
        {
            if (prefab == null || expectedType == null)
                return false;
                
            return prefab.GetComponent(expectedType) != null;
        }
        
        #endregion
        
        #region Statistics
        
        public ActorFactoryStatistics GetStatistics()
        {
            lock (_poolLock)
            {
                _statistics.CurrentPoolSize = _actorPools.Values.AsValueEnumerable().Sum(pool => pool.Count);
            }
            
            return new ActorFactoryStatistics
            {
                TotalCreated = _statistics.TotalCreated,
                CharactersCreated = _statistics.CharactersCreated,
                BackgroundsCreated = _statistics.BackgroundsCreated,
                CustomActorsCreated = _statistics.CustomActorsCreated,
                PoolHits = _statistics.PoolHits,
                PoolMisses = _statistics.PoolMisses,
                ValidationFailures = _statistics.ValidationFailures,
                AverageCreationTime = _statistics.AverageCreationTime,
                CurrentPoolSize = _statistics.CurrentPoolSize,
                PendingCreations = _activeCreations.Count
            };
        }
        
        public void ResetStatistics()
        {
            _statistics = new ActorFactoryStatistics();
            UnityEngine.Debug.Log("[ActorFactory] Statistics reset");
        }
        
        #endregion
        
        #region Configuration
        
        public void Configure(int maxConcurrentCreations, bool enableValidation, bool enablePooling)
        {
            // Update semaphore if needed (this is complex, so we'll log a warning)
            if (maxConcurrentCreations != _config.MaxConcurrentLoads)
            {
                UnityEngine.Debug.LogWarning("[ActorFactory] Runtime semaphore changes not supported - restart service");
            }
            
            _validationEnabled = enableValidation;
            
            if (_poolingEnabled != enablePooling)
            {
                ConfigurePooling(enablePooling, _defaultPoolSize);
            }
            
            UnityEngine.Debug.Log($"[ActorFactory] Configuration updated - Validation: {enableValidation}, Pooling: {enablePooling}");
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializePools()
        {
            if (!_poolingEnabled)
                return;
                
            _actorPools.TryAdd(ActorType.Character, new Queue<IActor>());
            _actorPools.TryAdd(ActorType.Background, new Queue<IActor>());
        }
        
        private bool TryGetFromPool<T>(ActorType actorType, out T actor) where T : class, IActor
        {
            actor = null;
            
            if (!_poolingEnabled || !_actorPools.TryGetValue(actorType, out var pool))
                return false;
                
            lock (_poolLock)
            {
                if (pool.Count == 0)
                    return false;
                    
                var pooledActor = pool.Dequeue();
                actor = pooledActor as T;
                _statistics.CurrentPoolSize--;
                
                return actor != null;
            }
        }
        
        private async UniTask<ICharacterActor> CreateNewCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position, CancellationToken cancellationToken)
        {
            var gameObject = new GameObject(id);
            gameObject.transform.position = position;
            
            var character = gameObject.AddComponent<CharacterActor>();
            character.Initialize(id, appearance);
            
            await character.InitializeAsync(cancellationToken);
            
            return character;
        }
        
        private async UniTask<IBackgroundActor> CreateNewBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position, CancellationToken cancellationToken)
        {
            var gameObject = new GameObject(id);
            gameObject.transform.position = position;
            
            var background = gameObject.AddComponent<BackgroundActor>();
            background.Initialize(id, appearance);
            
            await background.InitializeAsync(cancellationToken);
            
            return background;
        }
        
        private async UniTask ReconfigurePooledCharacterAsync(ICharacterActor character, string id, CharacterAppearance appearance, Vector3 position, CancellationToken cancellationToken)
        {
            character.GameObject.name = id;
            character.GameObject.transform.position = position;
            character.GameObject.SetActive(true);
            
            // Reset the character's appearance
            await character.ChangeAppearanceAsync(appearance, 0f, cancellationToken);
        }
        
        private async UniTask ReconfigurePooledBackgroundAsync(IBackgroundActor background, string id, BackgroundAppearance appearance, Vector3 position, CancellationToken cancellationToken)
        {
            background.GameObject.name = id;
            background.GameObject.transform.position = position;
            background.GameObject.SetActive(true);
            
            // Reset the background's appearance
            await background.ChangeAppearanceAsync(appearance, 0f, cancellationToken);
        }
        
        private void UpdateAverageCreationTime(long elapsedMilliseconds)
        {
            var currentAvg = _statistics.AverageCreationTime;
            var totalCreated = _statistics.TotalCreated;
            
            if (totalCreated == 1)
            {
                _statistics.AverageCreationTime = elapsedMilliseconds;
            }
            else
            {
                // Rolling average
                _statistics.AverageCreationTime = (currentAvg * (totalCreated - 1) + elapsedMilliseconds) / totalCreated;
            }
        }
        
        private void ClearAllPools()
        {
            lock (_poolLock)
            {
                foreach (var pool in _actorPools.Values)
                {
                    while (pool.Count > 0)
                    {
                        var actor = pool.Dequeue();
                        if (actor?.GameObject != null)
                        {
                            UnityEngine.Object.DestroyImmediate(actor.GameObject);
                        }
                    }
                }
                
                _actorPools.Clear();
                _statistics.CurrentPoolSize = 0;
            }
        }
        
        #endregion
    }
}
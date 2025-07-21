using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages resource loading and caching for actors with enum-based resource path generation
    /// </summary>
    public class ActorResourceManager : IDisposable
    {
        private readonly IActor _actor;
        private readonly Dictionary<string, Sprite> _spriteCache = new();
        private readonly Dictionary<string, Texture2D> _textureCache = new();
        private readonly Dictionary<string, ResourceLoadingTask> _loadingTasks = new();
        private readonly object _cacheLock = new();
        
        // Configuration
        private int _maxCacheSize = 50;
        private bool _enablePreloading = true;
        private bool _enableFallbackLoading = true;
        private string _resourceBasePath = "Actors";
        
        // Resource service integration
        private IResourceService _resourceService;
        
        // State tracking
        private CancellationTokenSource _disposeCts;
        private bool _disposed = false;
        private float _loadProgress = 0f;
        
        // Events
        public event Action<string, Sprite> OnSpriteLoaded;
        public event Action<string, Exception> OnLoadError;
        
        public float LoadProgress => _loadProgress;
        public int CachedResourceCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _spriteCache.Count + _textureCache.Count;
                }
            }
        }
        
        public ActorResourceManager(IActor actor)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _disposeCts = new CancellationTokenSource();
            
            // Get resource service from engine
            _resourceService = Engine.GetService<IResourceService>();
            
            Debug.Log($"[ActorResourceManager] Initialized for actor: {_actor.Id}");
        }
        
        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                // Initialize cache
                ConfigureCache();
                
                // Preload default resources if enabled
                if (_enablePreloading)
                {
                    await PreloadDefaultResourcesAsync(linkedCts.Token);
                }
                
                Debug.Log($"[ActorResourceManager] Resource manager initialized for {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorResourceManager] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        // === Resource Loading Methods ===
        
        public async UniTask<Sprite> LoadSpriteAsync(string resourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(resourcePath))
                throw new ArgumentException("Resource path cannot be null or empty", nameof(resourcePath));
            
            ValidateNotDisposed();
            
            // Check cache first
            lock (_cacheLock)
            {
                if (_spriteCache.TryGetValue(resourcePath, out var cachedSprite))
                {
                    return cachedSprite;
                }
            }
            
            // Check if already loading
            if (_loadingTasks.TryGetValue(resourcePath, out var existingTask))
            {
                return await existingTask.SpriteTask;
            }
            
            // Start new loading task
            var loadingTask = new ResourceLoadingTask
            {
                ResourcePath = resourcePath,
                StartTime = DateTime.Now
            };
            
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _disposeCts.Token);
                
            try
            {
                _loadingTasks[resourcePath] = loadingTask;
                
                var sprite = await LoadSpriteInternalAsync(resourcePath, linkedCts.Token);
                
                // Cache the loaded sprite
                if (sprite != null)
                {
                    CacheSprite(resourcePath, sprite);
                    OnSpriteLoaded?.Invoke(resourcePath, sprite);
                }
                
                loadingTask.SpriteTask = UniTask.FromResult(sprite);
                return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorResourceManager] Failed to load sprite '{resourcePath}': {ex.Message}");
                OnLoadError?.Invoke(resourcePath, ex);
                
                // Try fallback loading if enabled
                if (_enableFallbackLoading)
                {
                    return await TryLoadFallbackSpriteAsync(resourcePath, linkedCts.Token);
                }
                
                throw;
            }
            finally
            {
                _loadingTasks.Remove(resourcePath);
            }
        }
        
        public async UniTask<Sprite> LoadCharacterSpriteAsync(CharacterAppearance appearance, string characterName, CancellationToken cancellationToken = default)
        {
            var resourcePath = appearance.GetResourcePath(characterName, _resourceBasePath);
            var fallbackPaths = appearance.GetFallbackPaths(characterName, _resourceBasePath);
            
            return await LoadSpriteWithFallbacksAsync(resourcePath, fallbackPaths, cancellationToken);
        }
        
        public async UniTask<Sprite> LoadBackgroundSpriteAsync(BackgroundAppearance appearance, CancellationToken cancellationToken = default)
        {
            var resourcePath = appearance.GetResourcePath(_resourceBasePath);
            var fallbackPaths = appearance.GetFallbackPaths(_resourceBasePath);
            
            return await LoadSpriteWithFallbacksAsync(resourcePath, fallbackPaths, cancellationToken);
        }
        
        public async UniTask<Sprite> LoadSpriteWithFallbacksAsync(string primaryPath, string[] fallbackPaths, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            // Try primary path first
            try
            {
                var sprite = await LoadSpriteAsync(primaryPath, cancellationToken);
                if (sprite != null)
                    return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActorResourceManager] Primary path failed '{primaryPath}': {ex.Message}");
            }
            
            // Try fallback paths
            if (fallbackPaths != null)
            {
                foreach (var fallbackPath in fallbackPaths)
                {
                    if (string.IsNullOrEmpty(fallbackPath))
                        continue;
                    
                    try
                    {
                        var sprite = await LoadSpriteAsync(fallbackPath, cancellationToken);
                        if (sprite != null)
                        {
                            Debug.Log($"[ActorResourceManager] Loaded fallback sprite: {fallbackPath}");
                            return sprite;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ActorResourceManager] Fallback path failed '{fallbackPath}': {ex.Message}");
                    }
                }
            }
            
            Debug.LogError($"[ActorResourceManager] All resource paths failed for: {primaryPath}");
            return null;
        }
        
        // === Resource Management ===
        
        public async UniTask LoadResourcesAsync(CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                _loadProgress = 0f;
                
                // Load actor-specific resources based on type
                if (_actor is ICharacterActor character)
                {
                    await LoadCharacterResourcesAsync(character, linkedCts.Token);
                }
                else if (_actor is IBackgroundActor background)
                {
                    await LoadBackgroundResourcesAsync(background, linkedCts.Token);
                }
                else
                {
                    await LoadGenericActorResourcesAsync(linkedCts.Token);
                }
                
                _loadProgress = 1f;
                Debug.Log($"[ActorResourceManager] Loaded resources for {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorResourceManager] Failed to load resources: {ex.Message}");
                throw;
            }
        }
        
        public async UniTask UnloadResourcesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Cancel any ongoing loads
                foreach (var task in _loadingTasks.Values)
                {
                    task.CancellationTokenSource?.Cancel();
                }
                _loadingTasks.Clear();
                
                // Clear caches
                lock (_cacheLock)
                {
                    // Unload sprites (but don't destroy - they might be shared)
                    _spriteCache.Clear();
                    _textureCache.Clear();
                }
                
                _loadProgress = 0f;
                Debug.Log($"[ActorResourceManager] Unloaded resources for {_actor.Id}");
                
                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorResourceManager] Error during resource unloading: {ex.Message}");
                throw;
            }
        }
        
        public async UniTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            await UnloadResourcesAsync(cancellationToken);
            await LoadResourcesAsync(cancellationToken);
        }
        
        // === Cache Management ===
        
        public void CacheSprite(string resourcePath, Sprite sprite)
        {
            if (string.IsNullOrEmpty(resourcePath) || sprite == null)
                return;
            
            lock (_cacheLock)
            {
                // Check cache size and evict if necessary
                if (_spriteCache.Count >= _maxCacheSize)
                {
                    EvictOldestCacheEntry();
                }
                
                _spriteCache[resourcePath] = sprite;
            }
        }
        
        public Sprite GetCachedSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;
            
            lock (_cacheLock)
            {
                return _spriteCache.TryGetValue(resourcePath, out var sprite) ? sprite : null;
            }
        }
        
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _spriteCache.Clear();
                _textureCache.Clear();
            }
            
            Debug.Log($"[ActorResourceManager] Cleared resource cache for {_actor.Id}");
        }
        
        public void PrewarmCache(string[] resourcePaths)
        {
            if (resourcePaths == null)
                return;
            
            _ = PrewarmCacheAsync(resourcePaths, _disposeCts.Token);
        }
        
        // === Configuration ===
        
        public void Configure(int maxCacheSize, bool enablePreloading, bool enableFallbackLoading, string resourceBasePath)
        {
            _maxCacheSize = Mathf.Max(1, maxCacheSize);
            _enablePreloading = enablePreloading;
            _enableFallbackLoading = enableFallbackLoading;
            _resourceBasePath = resourceBasePath ?? "Actors";
            
            Debug.Log($"[ActorResourceManager] Configured: Cache={_maxCacheSize}, Preload={_enablePreloading}, Fallback={_enableFallbackLoading}");
        }
        
        // === Debug and Utility ===
        
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"=== ActorResourceManager Debug Info: {_actor.Id} ===");
            
            lock (_cacheLock)
            {
                info.AppendLine($"Cached Sprites: {_spriteCache.Count}/{_maxCacheSize}");
                info.AppendLine($"Cached Textures: {_textureCache.Count}");
            }
            
            info.AppendLine($"Active Loading Tasks: {_loadingTasks.Count}");
            info.AppendLine($"Load Progress: {_loadProgress:P}");
            info.AppendLine($"Resource Base Path: {_resourceBasePath}");
            info.AppendLine($"Preloading Enabled: {_enablePreloading}");
            info.AppendLine($"Fallback Loading Enabled: {_enableFallbackLoading}");
            
            if (_spriteCache.Count > 0)
            {
                info.AppendLine("\nCached Resources:");
                lock (_cacheLock)
                {
                    foreach (var kvp in _spriteCache)
                    {
                        info.AppendLine($"  {kvp.Key}: {kvp.Value?.name ?? "null"}");
                    }
                }
            }
            
            return info.ToString();
        }
        
        // === Private Methods ===
        
        private void ConfigureCache()
        {
            // Configure cache based on actor metadata if available
            if (Engine.GetService<IActorService>() is ActorService actorService)
            {
                // Configuration would come from ActorMetadata
                // For now, use defaults
            }
        }
        
        private async UniTask<Sprite> LoadSpriteInternalAsync(string resourcePath, CancellationToken cancellationToken)
        {
            if (_resourceService != null)
            {
                // Use engine's resource service
                var resource = await _resourceService.LoadResourceAsync<Sprite>(resourcePath, cancellationToken);
                return resource.IsValid ? resource.Asset : null;
            }
            else
            {
                // Fallback to Unity Resources
                return await LoadFromUnityResourcesAsync<Sprite>(resourcePath, cancellationToken);
            }
        }
        
        private async UniTask<T> LoadFromUnityResourcesAsync<T>(string resourcePath, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            // Simulate async loading using Unity Resources (for demo purposes)
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
            
            var resource = Resources.Load<T>(resourcePath);
            if (resource == null)
            {
                throw new ResourceNotFoundException($"Resource not found: {resourcePath}");
            }
            
            return resource;
        }
        
        private async UniTask<Sprite> TryLoadFallbackSpriteAsync(string originalPath, CancellationToken cancellationToken)
        {
            // Try to load a generic fallback sprite
            var fallbackPaths = new[]
            {
                $"{_resourceBasePath}/Default/Default_Sprite",
                "Default/Default_Actor",
                "Fallback/Missing_Sprite"
            };
            
            foreach (var fallbackPath in fallbackPaths)
            {
                try
                {
                    var sprite = await LoadSpriteInternalAsync(fallbackPath, cancellationToken);
                    if (sprite != null)
                    {
                        Debug.Log($"[ActorResourceManager] Loaded fallback for '{originalPath}': {fallbackPath}");
                        return sprite;
                    }
                }
                catch
                {
                    // Continue to next fallback
                }
            }
            
            Debug.LogWarning($"[ActorResourceManager] No fallback available for: {originalPath}");
            return null;
        }
        
        private async UniTask PreloadDefaultResourcesAsync(CancellationToken cancellationToken)
        {
            // Preload common resources based on actor type
            var preloadPaths = new List<string>();
            
            if (_actor is ICharacterActor)
            {
                preloadPaths.AddRange(new[]
                {
                    $"{_resourceBasePath}/Default/Character_Neutral_Standing_00",
                    $"{_resourceBasePath}/Default/Character_Happy_Standing_00"
                });
            }
            else if (_actor is IBackgroundActor)
            {
                preloadPaths.AddRange(new[]
                {
                    $"{_resourceBasePath}/Default/Scene_Classroom_00",
                    $"{_resourceBasePath}/Default/Scene_Black_00"
                });
            }
            
            await PrewarmCacheAsync(preloadPaths.ToArray(), cancellationToken);
        }
        
        private async UniTask PrewarmCacheAsync(string[] resourcePaths, CancellationToken cancellationToken)
        {
            var tasks = new List<UniTask>();
            
            foreach (var path in resourcePaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    tasks.Add(LoadSpriteAsync(path, cancellationToken).AsUniTask());
                }
            }
            
            try
            {
                await UniTask.WhenAll(tasks);
                Debug.Log($"[ActorResourceManager] Prewarmed cache with {resourcePaths.Length} resources");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActorResourceManager] Some resources failed to preload: {ex.Message}");
            }
        }
        
        private async UniTask LoadCharacterResourcesAsync(ICharacterActor character, CancellationToken cancellationToken)
        {
            _loadProgress = 0.2f;
            
            // Load current appearance
            await LoadCharacterSpriteAsync(character.Appearance, character.Id, cancellationToken);
            _loadProgress = 0.8f;
            
            // TODO: Load additional character resources (voice, animations, etc.)
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
        }
        
        private async UniTask LoadBackgroundResourcesAsync(IBackgroundActor background, CancellationToken cancellationToken)
        {
            _loadProgress = 0.2f;
            
            // Load current background
            await LoadBackgroundSpriteAsync(background.Appearance, cancellationToken);
            _loadProgress = 0.8f;
            
            // TODO: Load additional background resources (music, effects, etc.)
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
        }
        
        private async UniTask LoadGenericActorResourcesAsync(CancellationToken cancellationToken)
        {
            _loadProgress = 0.5f;
            
            // Load generic actor resources
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
            
            _loadProgress = 1f;
        }
        
        private void EvictOldestCacheEntry()
        {
            // Simple LRU eviction - in a real implementation, you'd track access times
            lock (_cacheLock)
            {
                if (_spriteCache.Count > 0)
                {
                    var firstKey = _spriteCache.Keys.First();
                    _spriteCache.Remove(firstKey);
                }
            }
        }
        
        private void ValidateNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActorResourceManager));
        }
        
        // === IDisposable Implementation ===
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                _disposeCts.Cancel();
                
                // Cancel all loading tasks
                foreach (var task in _loadingTasks.Values)
                {
                    task.CancellationTokenSource?.Cancel();
                    task.CancellationTokenSource?.Dispose();
                }
                _loadingTasks.Clear();
                
                // Clear caches
                ClearCache();
                
                _disposeCts?.Dispose();
                _disposed = true;
                
                Debug.Log($"[ActorResourceManager] Disposed for actor: {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorResourceManager] Error during disposal: {ex.Message}");
            }
        }
    }
    
    // === Helper Classes ===
    
    internal class ResourceLoadingTask
    {
        public string ResourcePath { get; set; }
        public UniTask<Sprite> SpriteTask { get; set; }
        public DateTime StartTime { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
    }
    
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException(string message) : base(message) { }
        public ResourceNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
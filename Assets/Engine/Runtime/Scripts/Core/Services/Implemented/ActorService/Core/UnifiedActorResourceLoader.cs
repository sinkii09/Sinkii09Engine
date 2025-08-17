using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Unified actor resource loader that supports both single-sprite and layered approaches
    /// Provides backward compatibility while enabling new layered features
    /// </summary>
    public class UnifiedActorResourceLoader
    {
        private readonly IResourceService _resourceService;
        private readonly Dictionary<string, Sprite> _spriteCache;
        private readonly Dictionary<string, bool> _layeredModeCache;
        
        public UnifiedActorResourceLoader(IResourceService resourceService)
        {
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _spriteCache = new Dictionary<string, Sprite>();
            _layeredModeCache = new Dictionary<string, bool>();
        }
        
        #region Public Loading Methods
        
        /// <summary>
        /// Loads actor sprite automatically detecting whether to use single or layered approach
        /// </summary>
        public async UniTask<Sprite> LoadActorSprite(string actorId, IAppearance appearance, CancellationToken cancellationToken = default)
        {
            // Check if this is explicitly a layered appearance
            if (appearance is LayeredCharacterAppearance layeredAppearance)
            {
                return await LoadLayeredSprite(actorId, layeredAppearance, cancellationToken);
            }
            
            // For standard appearances, auto-detect the best approach
            var isLayered = await DetectLayeredMode(actorId, cancellationToken);
            
            if (isLayered)
            {
                // Convert standard appearance to layered and load
                var convertedLayered = ConvertToLayered(appearance);
                return await LoadLayeredSprite(actorId, convertedLayered, cancellationToken);
            }
            else
            {
                // Use traditional single-sprite loading
                return await LoadSingleSprite(actorId, appearance, cancellationToken);
            }
        }
        
        /// <summary>
        /// Explicitly loads as single sprite (old system)
        /// </summary>
        public async UniTask<Sprite> LoadSingleSprite(string actorId, IAppearance appearance, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"single_{actorId}_{appearance.GetAddressableKey(actorId)}";
            
            // Check cache
            if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
            {
                Debug.Log($"UnifiedLoader: Using cached single sprite for {cacheKey}");
                return cachedSprite;
            }
            
            try
            {
                Debug.Log($"UnifiedLoader: Loading single sprite for {actorId} with appearance {appearance}");
                
                // Try multiple loading approaches in order
                Sprite sprite = null;
                
                // Approach 1: Try addressable key
                sprite = await TryLoadByAddressableKey(actorId, appearance, cancellationToken);
                
                // Approach 2: Try path resolution
                if (sprite == null)
                {
                    sprite = await TryLoadByPathResolver(actorId, appearance, cancellationToken);
                }
                
                if (sprite != null)
                {
                    _spriteCache[cacheKey] = sprite;
                    Debug.Log($"UnifiedLoader: Successfully loaded single sprite for {cacheKey}");
                    return sprite;
                }
                
                Debug.LogWarning($"UnifiedLoader: Failed to load single sprite for {cacheKey}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UnifiedLoader: Error loading single sprite for {cacheKey}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Explicitly loads as layered sprite (new system)
        /// </summary>
        public async UniTask<Sprite> LoadLayeredSprite(string actorId, LayeredCharacterAppearance appearance, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"layered_{actorId}_{appearance.GetCompositionExpression()}";
            
            // Check cache
            if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
            {
                Debug.Log($"UnifiedLoader: Using cached layered sprite for {cacheKey}");
                return cachedSprite;
            }
            
            try
            {
                Debug.Log($"UnifiedLoader: Loading layered sprite for {actorId} with {appearance.GetLayerPaths(actorId).Length} layers");
                
                // Load all layers with fallbacks
                var layers = await LoadLayersWithFallbacks(actorId, appearance, cancellationToken);
                
                if (layers.Count == 0)
                {
                    Debug.LogWarning($"UnifiedLoader: No layers loaded for {cacheKey}, falling back to single sprite");
                    // Fallback to single sprite approach
                    return await LoadSingleSprite(actorId, appearance, cancellationToken);
                }
                
                // Compose layers into single sprite
                var compositeSprite = ComposeLayers(layers, appearance);
                
                if (compositeSprite != null)
                {
                    _spriteCache[cacheKey] = compositeSprite;
                    Debug.Log($"UnifiedLoader: Successfully composed {layers.Count} layers for {cacheKey}");
                }
                
                return compositeSprite;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UnifiedLoader: Error loading layered sprite for {cacheKey}: {ex.Message}");
                // Fallback to single sprite on error
                return await LoadSingleSprite(actorId, appearance, cancellationToken);
            }
        }
        
        #endregion
        
        #region Detection and Conversion
        
        /// <summary>
        /// Auto-detects whether actor uses layered or single sprite system
        /// </summary>
        private async UniTask<bool> DetectLayeredMode(string actorId, CancellationToken cancellationToken)
        {
            // Check cache
            if (_layeredModeCache.TryGetValue(actorId, out var isLayered))
            {
                return isLayered;
            }
            
            try
            {
                // Check if base layer exists (indicator of layered system)
                var basePath = $"Actors/Characters/{actorId}/base/standing";
                var baseExists = await _resourceService.ResourceExistsAsync<Sprite>(basePath, cancellationToken);
                
                if (baseExists)
                {
                    _layeredModeCache[actorId] = true;
                    Debug.Log($"UnifiedLoader: Detected layered mode for {actorId}");
                    return true;
                }
                
                // Check for old-style sprites
                var oldPath = $"Actors/Characters/{actorId}/Sprites/char_{actorId}_happy_standing_00";
                var oldExists = await _resourceService.ResourceExistsAsync<Sprite>(oldPath, cancellationToken);
                
                _layeredModeCache[actorId] = !oldExists;
                Debug.Log($"UnifiedLoader: Detected {(oldExists ? "single" : "unknown")} mode for {actorId}");
                
                return false;
            }
            catch
            {
                // Default to single mode on error
                _layeredModeCache[actorId] = false;
                return false;
            }
        }
        
        /// <summary>
        /// Converts standard appearance to layered appearance
        /// </summary>
        private LayeredCharacterAppearance ConvertToLayered(IAppearance appearance)
        {
            if (appearance is CharacterAppearance charAppearance)
            {
                return new LayeredCharacterAppearance(
                    charAppearance.Pose,
                    new[] { charAppearance.Expression },
                    new[] { charAppearance.OutfitId }
                );
            }
            
            // Default conversion
            return new LayeredCharacterAppearance(
                CharacterPose.Standing,
                new[] { CharacterEmotion.Neutral },
                new[] { 0 }
            );
        }
        
        #endregion
        
        #region Layer Loading
        
        /// <summary>
        /// Loads layers with intelligent fallback system
        /// </summary>
        private async UniTask<List<Sprite>> LoadLayersWithFallbacks(string actorId, LayeredCharacterAppearance appearance, CancellationToken cancellationToken)
        {
            var loadedLayers = new List<Sprite>();
            var pathGroups = appearance.GetLayerPathsWithFallbacks(actorId);
            
            foreach (var pathGroup in pathGroups)
            {
                Sprite layerSprite = null;
                
                // Try each fallback path in order
                foreach (var path in pathGroup)
                {
                    try
                    {
                        var fullPath = $"Actors/Characters/{actorId}/{path}";
                        var resource = await _resourceService.LoadResourceAsync<Sprite>(fullPath, cancellationToken);
                        
                        if (resource?.Asset != null)
                        {
                            layerSprite = resource.Asset;
                            Debug.Log($"UnifiedLoader: Loaded layer from {fullPath}");
                            break;
                        }
                    }
                    catch
                    {
                        // Try next fallback
                    }
                }
                
                if (layerSprite != null)
                {
                    loadedLayers.Add(layerSprite);
                }
                else
                {
                    Debug.LogWarning($"UnifiedLoader: Could not load layer for paths: {string.Join(", ", pathGroup)}");
                }
            }
            
            return loadedLayers;
        }
        
        #endregion
        
        #region Loading Approaches
        
        private async UniTask<Sprite> TryLoadByAddressableKey(string actorId, IAppearance appearance, CancellationToken cancellationToken)
        {
            try
            {
                var addressableKey = appearance.GetAddressableKey(actorId);
                var resource = await _resourceService.LoadResourceAsync<Sprite>(addressableKey, cancellationToken);
                return resource?.Asset;
            }
            catch
            {
                return null;
            }
        }
        
        private async UniTask<Sprite> TryLoadByPathResolver(string actorId, IAppearance appearance, CancellationToken cancellationToken)
        {
            try
            {
                var parameters = appearance.GetPathParameters(actorId);
                var resource = await _resourceService.LoadResourceByIdAsync<Sprite>(
                    ResourceType.Actor,
                    actorId,
                    ResourceCategory.Sprites,
                    parameters: parameters
                );
                return resource?.Asset;
            }
            catch
            {
                return null;
            }
        }
        
        #endregion
        
        #region Layer Composition
        
        /// <summary>
        /// Composes multiple sprites into single sprite
        /// </summary>
        private Sprite ComposeLayers(List<Sprite> layers, LayeredCharacterAppearance appearance)
        {
            if (layers.Count == 0)
                return null;
            
            // For simplicity, if only one layer, return it directly
            if (layers.Count == 1)
                return layers[0];
            
            // Determine composite size
            var maxWidth = layers.Max(s => (int)s.rect.width);
            var maxHeight = layers.Max(s => (int)s.rect.height);
            
            // Create composite texture
            var compositeTexture = new Texture2D(maxWidth, maxHeight, TextureFormat.RGBA32, false);
            
            // Clear with transparent
            var pixels = new Color[maxWidth * maxHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            compositeTexture.SetPixels(pixels);
            
            // Composite each layer
            foreach (var layer in layers)
            {
                var layerPixels = layer.texture.GetPixels(
                    (int)layer.textureRect.x,
                    (int)layer.textureRect.y,
                    (int)layer.textureRect.width,
                    (int)layer.textureRect.height
                );
                
                // Apply to composite (centered)
                var offsetX = (maxWidth - (int)layer.textureRect.width) / 2;
                var offsetY = (maxHeight - (int)layer.textureRect.height) / 2;
                
                compositeTexture.SetPixels(offsetX, offsetY, 
                    (int)layer.textureRect.width, 
                    (int)layer.textureRect.height, 
                    layerPixels);
            }
            
            compositeTexture.Apply();
            
            // Create sprite from composite
            return Sprite.Create(
                compositeTexture,
                new Rect(0, 0, maxWidth, maxHeight),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
        
        #endregion
        
        #region Cache Management
        
        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearCache()
        {
            foreach (var sprite in _spriteCache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(sprite.texture);
                    UnityEngine.Object.DestroyImmediate(sprite);
                }
            }
            
            _spriteCache.Clear();
            _layeredModeCache.Clear();
            
            Debug.Log("UnifiedLoader: Cache cleared");
        }
        
        /// <summary>
        /// Preloads actor resources
        /// </summary>
        public async UniTask PreloadActor(string actorId, IAppearance[] appearances, CancellationToken cancellationToken = default)
        {
            var tasks = new List<UniTask>();
            
            foreach (var appearance in appearances)
            {
                tasks.Add(LoadActorSprite(actorId, appearance, cancellationToken));
            }
            
            await UniTask.WhenAll(tasks);
            Debug.Log($"UnifiedLoader: Preloaded {appearances.Length} appearances for {actorId}");
        }
        
        #endregion
    }
}
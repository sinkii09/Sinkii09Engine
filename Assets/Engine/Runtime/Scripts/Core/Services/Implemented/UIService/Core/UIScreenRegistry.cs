using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Auto-generated registry that maps UIScreenType enums to UIScreenAssets
    /// Uses the ScreenType property directly from assets - no redundant mapping needed
    /// </summary>
    [CreateAssetMenu(fileName = "UIScreenRegistry", menuName = "Engine/UI/Screen Registry", order = 0)]
    public class UIScreenRegistry : ScriptableObject
    {
        [SerializeField, Tooltip("Auto-discovered screen assets with their screen types")]
        private UIScreenAsset[] _screenAssets = Array.Empty<UIScreenAsset>();
        
        private Dictionary<UIScreenType, UIScreenAsset> _mappingCache;
        private bool _isInitialized;
        
        /// <summary>
        /// Initialize the registry and build lookup caches
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _mappingCache = new Dictionary<UIScreenType, UIScreenAsset>();
            
            foreach (var asset in _screenAssets)
            {
                if (asset != null && asset.ScreenType != UIScreenType.None)
                {
                    _mappingCache[asset.ScreenType] = asset;
                }
            }
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Get the screen asset for the specified screen type
        /// </summary>
        public UIScreenAsset GetAsset(UIScreenType screenType)
        {
            Initialize();
            return _mappingCache.TryGetValue(screenType, out var asset) ? asset : null;
        }
        
        /// <summary>
        /// Check if a screen type is registered
        /// </summary>
        public bool HasScreen(UIScreenType screenType)
        {
            Initialize();
            return _mappingCache.ContainsKey(screenType);
        }
        
        /// <summary>
        /// Get all registered screen types
        /// </summary>
        public UIScreenType[] GetAllScreenTypes()
        {
            Initialize();
            return _mappingCache.Keys.ToArray();
        }
        
        /// <summary>
        /// Get all registered screen assets
        /// </summary>
        public UIScreenAsset[] GetAllScreenAssets()
        {
            return _screenAssets ?? Array.Empty<UIScreenAsset>();
        }
        
        /// <summary>
        /// Set the screen assets (used by auto-generation system)
        /// </summary>
        public void SetScreenAssets(UIScreenAsset[] assets)
        {
            _screenAssets = assets ?? Array.Empty<UIScreenAsset>();
            _isInitialized = false;
            
            // Auto-sort by screen type for cleaner inspector display
            Array.Sort(_screenAssets, (a, b) => 
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.ScreenType.CompareTo(b.ScreenType);
            });
        }
        
        /// <summary>
        /// Validate the registry for missing or invalid assets
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            
            // Check for null assets
            for (int i = 0; i < _screenAssets.Length; i++)
            {
                if (_screenAssets[i] == null)
                {
                    errors.Add($"Asset at index {i} is null");
                }
            }
            
            // Check for duplicate screen types
            var duplicateTypes = _screenAssets
                .Where(a => a != null && a.ScreenType != UIScreenType.None)
                .GroupBy(a => a.ScreenType)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
                
            foreach (var duplicateType in duplicateTypes)
            {
                errors.Add($"Duplicate screen type: {duplicateType}");
            }
            
            // Check for assets with UIScreenType.None
            var noneTypeAssets = _screenAssets
                .Where(a => a != null && a.ScreenType == UIScreenType.None)
                .Select(a => a.name);
                
            foreach (var assetName in noneTypeAssets)
            {
                errors.Add($"Asset '{assetName}' has screen type set to None");
            }
            
            return errors;
        }
        
        private void OnEnable()
        {
            _isInitialized = false;
        }
        
        private void OnValidate()
        {
            // Validate registry in editor
            var errors = Validate();
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogWarning($"UIScreenRegistry validation error: {error}", this);
                }
            }
        }
    }
}
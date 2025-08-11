using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Type-safe screen reference asset for UI system - uses Addressables for all loading
    /// </summary>
    [CreateAssetMenu(fileName = "UIScreenAsset", menuName = "Engine/UI/Screen Asset", order = 1)]
    public class UIScreenAsset : ScriptableObject
    {
        [Header("Screen Type")]
        [SerializeField, Tooltip("Type-safe screen identifier")]
        private UIScreenType _screenType = UIScreenType.None;

        [Header("Screen Identification")]
        [SerializeField, Tooltip("Display name for this screen")]
        private string _displayName;

        [SerializeField, Tooltip("Category for organizing screens")]
        private UICategory _category = UICategory.Core;

        [Header("Addressable Reference")]
        [SerializeField, Tooltip("Addressable reference for the screen prefab")]
        private AssetReference _addressableReference;

        [Header("Display Settings")]
        [SerializeField, Range(0, 100), Tooltip("Sorting order for screen layering")]
        private int _sortingOrder = 0;

        [SerializeField, Tooltip("Screen should be cached after first load")]
        private bool _cacheScreen = true;

        [SerializeField, Tooltip("Screen can be shown multiple times")]
        private bool _allowMultipleInstances = false;

        #region Public Properties

        /// <summary>
        /// Type-safe screen identifier
        /// </summary>
        public UIScreenType ScreenType => _screenType;

        /// <summary>
        /// Unique identifier for this screen (deprecated - use ScreenType instead)
        /// </summary>
        [System.Obsolete("Use ScreenType property instead. This property will be removed in a future version.")]
        public string ScreenId => _screenType.ToString();

        /// <summary>
        /// Display name for this screen
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(_displayName) ? _displayName : _screenType.ToString();

        /// <summary>
        /// Category for organizing screens
        /// </summary>
        public UICategory Category => _category;

        /// <summary>
        /// Addressable reference for the screen
        /// </summary>
        public AssetReference AddressableReference => _addressableReference;

        /// <summary>
        /// Sorting order for screen layering
        /// </summary>
        public int SortingOrder => _sortingOrder;

        /// <summary>
        /// Whether screen should be cached after first load
        /// </summary>
        public bool CacheScreen => _cacheScreen;

        /// <summary>
        /// Whether screen allows multiple instances
        /// </summary>
        public bool AllowMultipleInstances => _allowMultipleInstances;

        #endregion

        #region Validation

        /// <summary>
        /// Validate this screen asset configuration
        /// </summary>
        /// <param name="errors">List to collect validation errors</param>
        /// <returns>True if configuration is valid</returns>
        public bool Validate(out System.Collections.Generic.List<string> errors)
        {
            errors = new System.Collections.Generic.List<string>();

            // Screen type validation
            if (_screenType == UIScreenType.None)
            {
                errors.Add("Screen type must be set (cannot be None)");
            }

            // Addressable reference validation
            if (_addressableReference == null || !_addressableReference.RuntimeKeyIsValid())
            {
                errors.Add("Valid Addressable reference is required");
            }

            return errors.Count == 0;
        }

        #endregion

        #region Unity Callbacks

        private void OnValidate()
        {
            // Auto-generate display name from screen type if empty
            if (string.IsNullOrEmpty(_displayName) && _screenType != UIScreenType.None)
            {
                _displayName = _screenType.ToString().Replace("Screen", "").Replace("UI", "");
            }

            // Validate configuration
            if (!Validate(out var errors))
            {
                foreach (var error in errors)
                {
                    Debug.LogWarning($"UIScreenAsset validation error in {name}: {error}", this);
                }
            }
        }

        /// <summary>
        /// Determines the UI category based on the screen type enum value
        /// </summary>
        private static UICategory GetCategoryFromScreenType(UIScreenType screenType)
        {
            var enumValue = (int)screenType;
            
            return enumValue switch
            {
                >= 1 and <= 9 => UICategory.Core,
                >= 10 and <= 19 => UICategory.Gameplay,
                >= 20 and <= 29 => UICategory.Dialog,
                >= 30 and <= 39 => UICategory.Menu,
                >= 40 and <= 49 => UICategory.Popup,
                >= 50 and <= 59 => UICategory.Debug,
                >= 1000 => UICategory.Custom,
                _ => UICategory.Core
            };
        }

        #endregion

        #region Editor Support

        /// <summary>
        /// Get a summary of this screen asset for debugging
        /// </summary>
        public override string ToString()
        {
            return $"UIScreenAsset: {DisplayName} (Type: {ScreenType}, Category: {Category})";
        }

        #endregion
    }

    /// <summary>
    /// Categories for organizing UI screens
    /// </summary>
    public enum UICategory
    {
        /// <summary>
        /// Core system screens (main menu, settings, etc.)
        /// </summary>
        Core = 0,

        /// <summary>
        /// Gameplay-related screens (HUD, inventory, etc.)
        /// </summary>
        Gameplay = 1,

        /// <summary>
        /// Dialog and conversation screens
        /// </summary>
        Dialog = 2,

        /// <summary>
        /// Menu and navigation screens
        /// </summary>
        Menu = 3,

        /// <summary>
        /// Popup and modal screens
        /// </summary>
        Popup = 4,

        /// <summary>
        /// Debug and development screens
        /// </summary>
        Debug = 5,

        /// <summary>
        /// Custom category for project-specific screens
        /// </summary>
        Custom = 999
    }
}
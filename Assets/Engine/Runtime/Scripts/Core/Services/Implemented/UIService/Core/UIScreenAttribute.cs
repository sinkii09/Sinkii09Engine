using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Attribute for marking UI screens for auto-discovery.
    /// Enables zero-hardcoded-string screen registration through reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UIScreenAttribute : Attribute
    {
        /// <summary>
        /// Unique identifier for the screen
        /// </summary>
        public string ScreenId { get; }
        
        /// <summary>
        /// Category for grouping screens (e.g., "Menu", "Gameplay", "Settings")
        /// </summary>
        public string Category { get; }
        
        /// <summary>
        /// Priority for screen loading order (higher values load first)
        /// </summary>
        public int Priority { get; }
        
        /// <summary>
        /// Whether this screen should be cached after hiding
        /// </summary>
        public bool CacheScreen { get; }
        
        /// <summary>
        /// Custom sorting order for canvas layering
        /// </summary>
        public int SortingOrder { get; }
        
        /// <summary>
        /// Whether this screen can be shown on top of other screens
        /// </summary>
        public bool AllowStacking { get; }
        
        /// <summary>
        /// Addressable key for the screen prefab (optional - can use auto-generated)
        /// </summary>
        public string AddressableKey { get; }

        /// <summary>
        /// Create a UI screen attribute with minimal configuration
        /// </summary>
        /// <param name="screenId">Unique identifier for the screen</param>
        /// <param name="category">Category for grouping screens</param>
        /// <param name="priority">Priority for screen loading order</param>
        public UIScreenAttribute(string screenId, string category = "Default", int priority = 0)
        {
            ScreenId = screenId ?? throw new ArgumentNullException(nameof(screenId));
            Category = category ?? "Default";
            Priority = priority;
            CacheScreen = true; // Default to caching for performance
            SortingOrder = 0;
            AllowStacking = false;
            AddressableKey = null; // Auto-generate from class name
        }
        
        /// <summary>
        /// Create a UI screen attribute with full configuration
        /// </summary>
        /// <param name="screenId">Unique identifier for the screen</param>
        /// <param name="category">Category for grouping screens</param>
        /// <param name="priority">Priority for screen loading order</param>
        /// <param name="cacheScreen">Whether this screen should be cached</param>
        /// <param name="sortingOrder">Custom sorting order for canvas layering</param>
        /// <param name="allowStacking">Whether this screen can be shown on top of others</param>
        /// <param name="addressableKey">Addressable key for the screen prefab</param>
        public UIScreenAttribute(
            string screenId, 
            string category = "Default", 
            int priority = 0,
            bool cacheScreen = true,
            int sortingOrder = 0,
            bool allowStacking = false,
            string addressableKey = null)
        {
            ScreenId = screenId ?? throw new ArgumentNullException(nameof(screenId));
            Category = category ?? "Default";
            Priority = priority;
            CacheScreen = cacheScreen;
            SortingOrder = sortingOrder;
            AllowStacking = allowStacking;
            AddressableKey = addressableKey;
        }
    }
}
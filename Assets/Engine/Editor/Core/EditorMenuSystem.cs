using System;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core
{
    /// <summary>
    /// Centralized editor menu system using enums for type safety and consistency
    /// </summary>
    public static class EditorMenuSystem
    {
        /// <summary>
        /// Main menu categories following engine architecture patterns
        /// </summary>
        public enum MenuCategory
        {
            Setup = 0,          // 0-99: Essential setup and configuration
            Tools = 100,        // 100-199: Development tools
            Analysis = 200,     // 200-299: Code analysis and debugging
            Generators = 300,   // 300-399: Code and asset generation
            Configuration = 400, // 400-499: Configuration management
            Development = 500,   // 500-599: Development utilities
            Export = 600,       // 600-699: Export and packaging
            Help = 900          // 900-999: Help and documentation
        }

        /// <summary>
        /// Subcategories for organized menu structure
        /// </summary>
        public enum MenuSubcategory
        {
            // Setup subcategories (0-99)
            Dependencies = 0,
            Services = 10,
            Configurations = 20,

            // Tools subcategories (100-199)
            Scripts = 100,
            UI = 110,
            Assets = 120,

            // Analysis subcategories (200-299)
            ServiceAnalysis = 200,
            DependencyAnalysis = 210,
            Performance = 220,

            // Generator subcategories (300-399)
            CodeGeneration = 300,
            AssetGeneration = 310,
            UIGeneration = 320,

            // Config subcategories (400-499)
            Creation = 400,
            Validation = 410,
            Management = 420,

            // Development subcategories (500-599)
            Testing = 500,
            Debugging = 510,
            Utilities = 520,

            // Export subcategories (600-699)
            Package = 600,
            Project = 610,

            // Help subcategories (900-999)
            Documentation = 900,
            About = 910
        }

        /// <summary>
        /// Asset creation menu locations
        /// </summary>
        public enum AssetMenuCategory
        {
            Scripts = 1000,
            Configurations = 1100,
            UI = 1200,
            Resources = 1300
        }

        /// <summary>
        /// Get the full menu path for a menu item
        /// </summary>
        public static string GetMenuPath(MenuCategory category, MenuSubcategory subcategory, string itemName)
        {
            var categoryName = GetCategoryDisplayName(category);
            var subcategoryName = GetSubcategoryDisplayName(subcategory);
            
            return $"Engine/{categoryName}/{subcategoryName}/{itemName}";
        }

        /// <summary>
        /// Get simplified menu path for single-level menus
        /// </summary>
        public static string GetMenuPath(MenuCategory category, string itemName)
        {
            var categoryName = GetCategoryDisplayName(category);
            return $"Engine/{categoryName}/{itemName}";
        }

        /// <summary>
        /// Get asset creation menu path
        /// </summary>
        public static string GetAssetMenuPath(AssetMenuCategory category, string itemName)
        {
            var categoryName = GetAssetCategoryDisplayName(category);
            return $"Assets/Create/Engine/{categoryName}/{itemName}";
        }

        /// <summary>
        /// Get priority value for menu ordering
        /// </summary>
        public static int GetPriority(MenuCategory category, int offset = 0)
        {
            return (int)category + offset;
        }

        /// <summary>
        /// Get priority value with subcategory
        /// </summary>
        public static int GetPriority(MenuCategory category, MenuSubcategory subcategory, int offset = 0)
        {
            return (int)category + (int)subcategory + offset;
        }

        /// <summary>
        /// Validate menu path follows engine conventions
        /// </summary>
        public static bool IsValidMenuPath(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath))
                return false;

            var parts = menuPath.Split('/');
            return parts.Length >= 3 && parts[0] == "Engine";
        }

        private static string GetCategoryDisplayName(MenuCategory category)
        {
            return category switch
            {
                MenuCategory.Setup => "Setup",
                MenuCategory.Tools => "Tools",
                MenuCategory.Analysis => "Analysis",
                MenuCategory.Generators => "Generators",
                MenuCategory.Configuration => "Configuration",
                MenuCategory.Development => "Development",
                MenuCategory.Export => "Export",
                MenuCategory.Help => "Help",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        private static string GetSubcategoryDisplayName(MenuSubcategory subcategory)
        {
            return subcategory switch
            {
                // Setup
                MenuSubcategory.Dependencies => "Dependencies",
                MenuSubcategory.Services => "Services",
                MenuSubcategory.Configurations => "Configurations",
                
                // Tools
                MenuSubcategory.Scripts => "Scripts",
                MenuSubcategory.UI => "UI",
                MenuSubcategory.Assets => "Assets",
                
                // Analysis
                MenuSubcategory.ServiceAnalysis => "Services",
                MenuSubcategory.DependencyAnalysis => "Dependencies",
                MenuSubcategory.Performance => "Performance",
                
                // Generators
                MenuSubcategory.CodeGeneration => "Code",
                MenuSubcategory.AssetGeneration => "Assets",
                MenuSubcategory.UIGeneration => "UI",
                
                // Configuration
                MenuSubcategory.Creation => "Creation",
                MenuSubcategory.Validation => "Validation",
                MenuSubcategory.Management => "Management",
                
                // Development
                MenuSubcategory.Testing => "Testing",
                MenuSubcategory.Debugging => "Debugging",
                MenuSubcategory.Utilities => "Utilities",
                
                // Export
                MenuSubcategory.Package => "Package",
                MenuSubcategory.Project => "Project",
                
                // Help
                MenuSubcategory.Documentation => "Documentation",
                MenuSubcategory.About => "About",
                
                _ => throw new ArgumentOutOfRangeException(nameof(subcategory))
            };
        }

        private static string GetAssetCategoryDisplayName(AssetMenuCategory category)
        {
            return category switch
            {
                AssetMenuCategory.Scripts => "Scripts",
                AssetMenuCategory.Configurations => "Configurations",
                AssetMenuCategory.UI => "UI",
                AssetMenuCategory.Resources => "Resources",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }
    }

    /// <summary>
    /// Menu item metadata for documentation and validation
    /// </summary>
    [Serializable]
    public class MenuItemInfo
    {
        [SerializeField] private string _displayName;
        [SerializeField] private string _description;
        [SerializeField] private EditorMenuSystem.MenuCategory _category;
        [SerializeField] private EditorMenuSystem.MenuSubcategory _subcategory;
        [SerializeField] private bool _requiresPlayMode;
        [SerializeField] private bool _requiresProject;

        public string DisplayName => _displayName;
        public string Description => _description;
        public EditorMenuSystem.MenuCategory Category => _category;
        public EditorMenuSystem.MenuSubcategory Subcategory => _subcategory;
        public bool RequiresPlayMode => _requiresPlayMode;
        public bool RequiresProject => _requiresProject;

        public MenuItemInfo(string displayName, string description, 
            EditorMenuSystem.MenuCategory category,
            EditorMenuSystem.MenuSubcategory subcategory = default,
            bool requiresPlayMode = false, bool requiresProject = true)
        {
            _displayName = displayName;
            _description = description;
            _category = category;
            _subcategory = subcategory;
            _requiresPlayMode = requiresPlayMode;
            _requiresProject = requiresProject;
        }

        public string GetMenuPath(string itemName)
        {
            return _subcategory == default 
                ? EditorMenuSystem.GetMenuPath(_category, itemName)
                : EditorMenuSystem.GetMenuPath(_category, _subcategory, itemName);
        }

        public int GetPriority(int offset = 0)
        {
            return _subcategory == default
                ? EditorMenuSystem.GetPriority(_category, offset)
                : EditorMenuSystem.GetPriority(_category, _subcategory, offset);
        }
    }
}
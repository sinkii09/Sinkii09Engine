using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Advanced menu registration system with automatic discovery and metadata management
    /// </summary>
    [InitializeOnLoad]
    public static class MenuRegistrationSystem
    {
        private static readonly Dictionary<string, RegisteredMenuItem> _registeredMenus = new Dictionary<string, RegisteredMenuItem>();
        private static readonly Dictionary<WorkflowMenuSystem.WorkflowCategory, MenuCategoryManager> _categoryManagers = new Dictionary<WorkflowMenuSystem.WorkflowCategory, MenuCategoryManager>();
        private static MenuUsageTracker _usageTracker;

        static MenuRegistrationSystem()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the menu registration system
        /// </summary>
        private static void Initialize()
        {
            _usageTracker = new MenuUsageTracker();
            RegisterCategoryManagers();
            DiscoverAndRegisterMenuItems();
            
            Debug.Log($"[MenuRegistrationSystem] Registered {_registeredMenus.Count} menu items across {_categoryManagers.Count} categories");
        }

        /// <summary>
        /// Register all category managers
        /// </summary>
        private static void RegisterCategoryManagers()
        {
            _categoryManagers[WorkflowMenuSystem.WorkflowCategory.ProjectSetup] = new ProjectSetupMenuManager();
            _categoryManagers[WorkflowMenuSystem.WorkflowCategory.DailyDevelopment] = new DailyDevelopmentMenuManager();
            _categoryManagers[WorkflowMenuSystem.WorkflowCategory.QualityAssurance] = new QualityAssuranceMenuManager();
            // Add more managers as needed

            foreach (var manager in _categoryManagers.Values)
            {
                manager.RegisterMenuItems();
            }
        }

        /// <summary>
        /// Discover and register menu items using reflection
        /// </summary>
        private static void DiscoverAndRegisterMenuItems()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var method in methods)
                        {
                            var workflowMenuAttr = method.GetCustomAttribute<WorkflowMenuAttribute>();
                            if (workflowMenuAttr != null)
                            {
                                RegisterMenuItem(method, workflowMenuAttr);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MenuRegistrationSystem] Failed to process assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Register a single menu item
        /// </summary>
        private static void RegisterMenuItem(MethodInfo method, WorkflowMenuAttribute workflowAttr)
        {
            try
            {
                var menuItem = CreateRegisteredMenuItem(method, workflowAttr);
                var menuPath = menuItem.GenerateMenuPath();
                
                _registeredMenus[menuPath] = menuItem;
                
                // Track usage for analytics
                _usageTracker.RegisterMenuItem(menuPath, menuItem);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MenuRegistrationSystem] Failed to register menu item for {method.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a registered menu item from method and attributes
        /// </summary>
        private static RegisteredMenuItem CreateRegisteredMenuItem(MethodInfo method, WorkflowMenuAttribute workflowAttr)
        {
            var metadata = method.GetCustomAttribute<MenuMetadataAttribute>();
            var context = method.GetCustomAttribute<MenuContextAttribute>();
            var performance = method.GetCustomAttribute<MenuPerformanceAttribute>();
            var visual = method.GetCustomAttribute<MenuVisualAttribute>();

            return new RegisteredMenuItem
            {
                Method = method,
                WorkflowAttribute = workflowAttr,
                Metadata = metadata ?? new MenuMetadataAttribute(),
                Context = context ?? new MenuContextAttribute(),
                Performance = performance ?? new MenuPerformanceAttribute(),
                Visual = visual ?? new MenuVisualAttribute(),
                RegistrationTime = DateTime.Now
            };
        }

        /// <summary>
        /// Get all registered menu items for a category
        /// </summary>
        public static RegisteredMenuItem[] GetMenuItemsForCategory(WorkflowMenuSystem.WorkflowCategory category)
        {
            return _registeredMenus.Values
                .Where(item => item.WorkflowAttribute.Category == category)
                .OrderBy(item => item.WorkflowAttribute.Priority)
                .ToArray();
        }

        /// <summary>
        /// Get menu items based on current project context
        /// </summary>
        public static RegisteredMenuItem[] GetContextualMenuItems(WorkflowMenuSystem.ProjectContext context, WorkflowMenuSystem.UserRole role)
        {
            return _registeredMenus.Values
                .Where(item => ShouldShowInContext(item, context, role))
                .OrderBy(item => item.WorkflowAttribute.Priority)
                .ToArray();
        }

        /// <summary>
        /// Get frequently used menu items
        /// </summary>
        public static RegisteredMenuItem[] GetFrequentlyUsedItems(int maxCount = 10)
        {
            return _usageTracker.GetFrequentlyUsedItems(maxCount);
        }

        /// <summary>
        /// Get recommended menu items based on context
        /// </summary>
        public static RegisteredMenuItem[] GetRecommendedItems(WorkflowMenuSystem.ProjectContext context, int maxCount = 5)
        {
            var contextItems = GetContextualMenuItems(context, WorkflowMenuSystem.UserRole.Developer);
            
            return contextItems
                .Where(item => item.Metadata.IsHighPriority || item.Performance.RecommendationWeight > 1.0f)
                .OrderByDescending(item => item.Performance.RecommendationWeight)
                .Take(maxCount)
                .ToArray();
        }

        /// <summary>
        /// Search menu items by keyword
        /// </summary>
        public static RegisteredMenuItem[] SearchMenuItems(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return new RegisteredMenuItem[0];

            var lowerSearch = searchTerm.ToLowerInvariant();
            
            return _registeredMenus.Values
                .Where(item => MatchesSearch(item, lowerSearch))
                .OrderByDescending(item => CalculateSearchRelevance(item, lowerSearch))
                .ToArray();
        }

        /// <summary>
        /// Validate all registered menu items
        /// </summary>
        public static MenuValidationReport ValidateAllMenus()
        {
            var report = new MenuValidationReport();
            
            // Check for priority conflicts
            var priorityGroups = _registeredMenus.Values
                .GroupBy(item => new { item.WorkflowAttribute.Category, item.WorkflowAttribute.Priority })
                .Where(g => g.Count() > 1);

            foreach (var group in priorityGroups)
            {
                var items = group.Select(i => i.Metadata.DisplayName ?? i.Method.Name).ToArray();
                report.AddWarning($"Priority conflict in {group.Key.Category} category, priority {group.Key.Priority}: {string.Join(", ", items)}");
            }

            // Check for missing metadata
            foreach (var item in _registeredMenus.Values)
            {
                if (string.IsNullOrEmpty(item.Metadata.DisplayName))
                {
                    report.AddWarning($"Missing display name for {item.Method.Name}");
                }
                
                if (string.IsNullOrEmpty(item.Metadata.Description))
                {
                    report.AddWarning($"Missing description for {item.Method.Name}");
                }
            }

            // Validate category managers
            foreach (var manager in _categoryManagers.Values)
            {
                var categoryResult = manager.ValidateMenuStructure();
                report.Merge(categoryResult);
            }

            return report;
        }

        /// <summary>
        /// Generate comprehensive menu documentation
        /// </summary>
        public static string GenerateMenuDocumentation()
        {
            var doc = new System.Text.StringBuilder();
            
            doc.AppendLine("# Workflow-Based Menu System Documentation");
            doc.AppendLine("Generated automatically from registered menu items.");
            doc.AppendLine();

            foreach (var category in Enum.GetValues(typeof(WorkflowMenuSystem.WorkflowCategory)).Cast<WorkflowMenuSystem.WorkflowCategory>())
            {
                var categoryItems = GetMenuItemsForCategory(category);
                if (categoryItems.Length == 0) continue;

                var categoryInfo = WorkflowMenuSystem.GetCategoryInfo(category);
                doc.AppendLine($"## {categoryInfo.Icon} {categoryInfo.DisplayName}");
                doc.AppendLine($"{categoryInfo.Description}");
                doc.AppendLine();

                var subcategories = categoryItems
                    .GroupBy(item => item.WorkflowAttribute.Subcategory)
                    .OrderBy(g => (int)g.Key);

                foreach (var subcategoryGroup in subcategories)
                {
                    doc.AppendLine($"### {subcategoryGroup.Key}");
                    doc.AppendLine();

                    foreach (var item in subcategoryGroup.OrderBy(i => i.WorkflowAttribute.Priority))
                    {
                        doc.AppendLine($"- **{item.Metadata.DisplayName ?? item.Method.Name}**");
                        doc.AppendLine($"  - *Description*: {item.Metadata.Description ?? "No description available"}");
                        doc.AppendLine($"  - *Menu Path*: {item.GenerateMenuPath()}");
                        if (!string.IsNullOrEmpty(item.Metadata.Shortcut))
                        {
                            doc.AppendLine($"  - *Shortcut*: {item.Metadata.Shortcut}");
                        }
                        if (item.Metadata.Tags.Length > 0)
                        {
                            doc.AppendLine($"  - *Tags*: {string.Join(", ", item.Metadata.Tags)}");
                        }
                        doc.AppendLine();
                    }
                }
            }

            return doc.ToString();
        }

        private static bool ShouldShowInContext(RegisteredMenuItem item, WorkflowMenuSystem.ProjectContext context, WorkflowMenuSystem.UserRole role)
        {
            // Check required context
            if (item.Context.RequiredContext != WorkflowMenuSystem.ProjectContext.None)
            {
                if (!context.HasFlag(item.Context.RequiredContext))
                    return false;
            }

            // Check prohibited context
            if (item.Context.ProhibitedContext != WorkflowMenuSystem.ProjectContext.None)
            {
                if (context.HasFlag(item.Context.ProhibitedContext))
                    return false;
            }

            // Check allowed roles
            if (item.Context.AllowedRoles.Length > 0)
            {
                if (!item.Context.AllowedRoles.Contains(role))
                    return false;
            }

            return true;
        }

        private static bool MatchesSearch(RegisteredMenuItem item, string searchTerm)
        {
            var displayName = item.Metadata.DisplayName ?? item.Method.Name;
            var description = item.Metadata.Description ?? "";
            
            return displayName.ToLowerInvariant().Contains(searchTerm) ||
                   description.ToLowerInvariant().Contains(searchTerm) ||
                   item.Metadata.Keywords.Any(k => k.ToLowerInvariant().Contains(searchTerm)) ||
                   item.Metadata.Tags.Any(t => t.ToLowerInvariant().Contains(searchTerm));
        }

        private static float CalculateSearchRelevance(RegisteredMenuItem item, string searchTerm)
        {
            var relevance = 0f;
            var displayName = (item.Metadata.DisplayName ?? item.Method.Name).ToLowerInvariant();
            var description = (item.Metadata.Description ?? "").ToLowerInvariant();

            // Exact match in display name gets highest score
            if (displayName == searchTerm) relevance += 100f;
            else if (displayName.StartsWith(searchTerm)) relevance += 50f;
            else if (displayName.Contains(searchTerm)) relevance += 25f;

            // Description matches
            if (description.Contains(searchTerm)) relevance += 10f;

            // Keyword and tag matches
            if (item.Metadata.Keywords.Any(k => k.ToLowerInvariant() == searchTerm)) relevance += 30f;
            if (item.Metadata.Tags.Any(t => t.ToLowerInvariant() == searchTerm)) relevance += 20f;

            // High priority items get bonus
            if (item.Metadata.IsHighPriority) relevance += 15f;

            // Usage frequency bonus
            relevance += _usageTracker.GetUsageFrequency(item.GenerateMenuPath()) * 10f;

            return relevance;
        }
    }

    /// <summary>
    /// Registered menu item with full metadata
    /// </summary>
    public class RegisteredMenuItem
    {
        public MethodInfo Method { get; set; }
        public WorkflowMenuAttribute WorkflowAttribute { get; set; }
        public MenuMetadataAttribute Metadata { get; set; }
        public MenuContextAttribute Context { get; set; }
        public MenuPerformanceAttribute Performance { get; set; }
        public MenuVisualAttribute Visual { get; set; }
        public DateTime RegistrationTime { get; set; }

        public string GenerateMenuPath()
        {
            return WorkflowMenuSystem.GetWorkflowMenuPath(
                WorkflowAttribute.Category,
                WorkflowAttribute.Subcategory,
                Metadata.DisplayName ?? Method.Name
            );
        }

        public int CalculatePriority()
        {
            return WorkflowMenuSystem.GetWorkflowPriority(
                WorkflowAttribute.Category,
                WorkflowAttribute.Subcategory,
                WorkflowAttribute.Priority
            );
        }
    }

    /// <summary>
    /// Menu validation report
    /// </summary>
    public class MenuValidationReport : ValidationResult
    {
        public void Merge(ValidationResult other)
        {
            Errors.AddRange(other.Errors);
            Warnings.AddRange(other.Warnings);
            Info.AddRange(other.Info);
        }
    }
}
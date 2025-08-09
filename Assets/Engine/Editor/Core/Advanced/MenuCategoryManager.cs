using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Base class for workflow-specific menu category managers
    /// </summary>
    public abstract class MenuCategoryManager
    {
        public abstract WorkflowMenuSystem.WorkflowCategory Category { get; }
        public abstract string CategoryDisplayName { get; }
        public abstract WorkflowCategoryInfo CategoryInfo { get; }

        /// <summary>
        /// Register all menu items for this category
        /// </summary>
        public abstract void RegisterMenuItems();

        /// <summary>
        /// Check if this category should be shown in current context
        /// </summary>
        public abstract bool ShouldShowInContext(WorkflowMenuSystem.ProjectContext context);

        /// <summary>
        /// Get all menu items managed by this category
        /// </summary>
        public abstract MenuItemDescriptor[] GetMenuItems();

        /// <summary>
        /// Get recommended tools based on project context
        /// </summary>
        public virtual MenuItemDescriptor[] GetRecommendedTools(WorkflowMenuSystem.ProjectContext context)
        {
            return new MenuItemDescriptor[0];
        }

        /// <summary>
        /// Get frequently used tools for quick access
        /// </summary>
        public virtual MenuItemDescriptor[] GetFrequentlyUsedTools()
        {
            var allItems = GetMenuItems();
            var frequentItems = new List<MenuItemDescriptor>();

            foreach (var item in allItems)
            {
                if (item.UsageFrequency > 0.5f) // Used frequently
                {
                    frequentItems.Add(item);
                }
            }

            return frequentItems.ToArray();
        }

        /// <summary>
        /// Validate menu structure for this category
        /// </summary>
        public virtual ValidationResult ValidateMenuStructure()
        {
            var result = new ValidationResult();
            var items = GetMenuItems();

            // Check for priority conflicts
            var priorities = new Dictionary<int, MenuItemDescriptor>();
            foreach (var item in items)
            {
                if (priorities.ContainsKey(item.Priority))
                {
                    result.AddWarning($"Priority conflict: {item.DisplayName} and {priorities[item.Priority].DisplayName} both use priority {item.Priority}");
                }
                else
                {
                    priorities[item.Priority] = item;
                }
            }

            // Check menu path consistency
            foreach (var item in items)
            {
                if (!item.MenuPath.StartsWith($"Engine/{CategoryDisplayName}/"))
                {
                    result.AddError($"Invalid menu path for {item.DisplayName}: {item.MenuPath}");
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Project Setup category manager - handles initial project configuration
    /// </summary>
    public class ProjectSetupMenuManager : MenuCategoryManager
    {
        public override WorkflowMenuSystem.WorkflowCategory Category => WorkflowMenuSystem.WorkflowCategory.ProjectSetup;
        public override string CategoryDisplayName => "Project Setup";

        public override WorkflowCategoryInfo CategoryInfo => new WorkflowCategoryInfo
        {
            DisplayName = "Project Setup",
            Description = "Initial project setup and configuration tools",
            Icon = "🚀",
            Color = new Color(0.2f, 0.8f, 0.2f),
            Keywords = new[] { "setup", "init", "configure", "dependencies", "quickstart" },
            IsHighPriority = true
        };

        public override void RegisterMenuItems()
        {
            // Implementation for registering project setup menu items
            // This would be called during editor initialization
        }

        public override bool ShouldShowInContext(WorkflowMenuSystem.ProjectContext context)
        {
            return context.HasFlag(WorkflowMenuSystem.ProjectContext.NewProject) ||
                   context.HasFlag(WorkflowMenuSystem.ProjectContext.MissingDependencies);
        }

        public override MenuItemDescriptor[] GetMenuItems()
        {
            return new[]
            {
                new MenuItemDescriptor
                {
                    DisplayName = "Quick Start Wizard",
                    Description = "One-click project initialization",
                    MenuPath = "Engine/Project Setup/Quick Start/Quick Start Wizard",
                    Priority = 0,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.QuickStart,
                    Icon = "wizard_icon",
                    RequiresProject = false,
                    IsHighPriority = true
                },
                new MenuItemDescriptor
                {
                    DisplayName = "Check Dependencies",
                    Description = "Validate and install required packages",
                    MenuPath = "Engine/Project Setup/Dependencies/Check Dependencies",
                    Priority = 10,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Dependencies,
                    Icon = "dependency_icon",
                    RequiresProject = true
                },
                new MenuItemDescriptor
                {
                    DisplayName = "Generate Configurations",
                    Description = "Create default service configurations",
                    MenuPath = "Engine/Project Setup/Configuration/Generate Configurations",
                    Priority = 20,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Configuration,
                    Icon = "config_icon",
                    RequiresProject = true
                }
            };
        }

        public override MenuItemDescriptor[] GetRecommendedTools(WorkflowMenuSystem.ProjectContext context)
        {
            var recommendations = new List<MenuItemDescriptor>();
            
            if (context.HasFlag(WorkflowMenuSystem.ProjectContext.NewProject))
            {
                recommendations.Add(GetMenuItems()[0]); // Quick Start Wizard
            }
            
            if (context.HasFlag(WorkflowMenuSystem.ProjectContext.MissingDependencies))
            {
                recommendations.Add(GetMenuItems()[1]); // Check Dependencies
            }
            
            return recommendations.ToArray();
        }
    }

    /// <summary>
    /// Daily Development category manager - handles day-to-day development tools
    /// </summary>
    public class DailyDevelopmentMenuManager : MenuCategoryManager
    {
        public override WorkflowMenuSystem.WorkflowCategory Category => WorkflowMenuSystem.WorkflowCategory.DailyDevelopment;
        public override string CategoryDisplayName => "Daily Development";

        public override WorkflowCategoryInfo CategoryInfo => new WorkflowCategoryInfo
        {
            DisplayName = "Daily Development",
            Description = "Essential tools for day-to-day development",
            Icon = "💻",
            Color = new Color(0.2f, 0.6f, 1.0f),
            Keywords = new[] { "code", "script", "asset", "ui", "debug", "daily" },
            IsHighPriority = true
        };

        public override void RegisterMenuItems()
        {
            // Implementation for daily development tools
        }

        public override bool ShouldShowInContext(WorkflowMenuSystem.ProjectContext context)
        {
            return true; // Always show daily development tools
        }

        public override MenuItemDescriptor[] GetMenuItems()
        {
            return new[]
            {
                new MenuItemDescriptor
                {
                    DisplayName = "Smart Script Creator",
                    Description = "AI-assisted script generation",
                    MenuPath = "Engine/Daily Development/Code Tools/Smart Script Creator",
                    Priority = 100,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.CodeTools,
                    Icon = "script_icon",
                    Shortcut = "Ctrl+Shift+N",
                    RequiresProject = true,
                    UsageFrequency = 0.8f
                },
                new MenuItemDescriptor
                {
                    DisplayName = "Asset Pipeline Manager",
                    Description = "Manage asset processing pipeline",
                    MenuPath = "Engine/Daily Development/Asset Pipeline/Pipeline Manager",
                    Priority = 110,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.AssetPipeline,
                    Icon = "pipeline_icon",
                    RequiresProject = true
                },
                new MenuItemDescriptor
                {
                    DisplayName = "UI Component Generator",
                    Description = "Generate UI components and screens",
                    MenuPath = "Engine/Daily Development/UI Development/Component Generator",
                    Priority = 120,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.UIDevelopment,
                    Icon = "ui_icon",
                    RequiresProject = true
                }
            };
        }
    }

    /// <summary>
    /// Quality Assurance category manager - handles testing and validation tools
    /// </summary>
    public class QualityAssuranceMenuManager : MenuCategoryManager
    {
        public override WorkflowMenuSystem.WorkflowCategory Category => WorkflowMenuSystem.WorkflowCategory.QualityAssurance;
        public override string CategoryDisplayName => "Quality Assurance";

        public override WorkflowCategoryInfo CategoryInfo => new WorkflowCategoryInfo
        {
            DisplayName = "Quality Assurance",
            Description = "Testing, validation, and quality tools",
            Icon = "🔍",
            Color = new Color(1.0f, 0.6f, 0.2f),
            Keywords = new[] { "test", "validate", "analyze", "quality", "performance" }
        };

        public override void RegisterMenuItems()
        {
            // Implementation for QA tools
        }

        public override bool ShouldShowInContext(WorkflowMenuSystem.ProjectContext context)
        {
            return context.HasFlag(WorkflowMenuSystem.ProjectContext.HasTests) ||
                   context.HasFlag(WorkflowMenuSystem.ProjectContext.HasPerformanceIssues);
        }

        public override MenuItemDescriptor[] GetMenuItems()
        {
            return new[]
            {
                new MenuItemDescriptor
                {
                    DisplayName = "Service Dependency Analyzer",
                    Description = "Analyze service dependencies and relationships",
                    MenuPath = "Engine/Quality Assurance/Code Analysis/Service Dependency Analyzer",
                    Priority = 200,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.CodeAnalysis,
                    Icon = "analyze_icon",
                    RequiresProject = true
                },
                new MenuItemDescriptor
                {
                    DisplayName = "Performance Profiler",
                    Description = "Profile engine and game performance",
                    MenuPath = "Engine/Quality Assurance/Performance/Performance Profiler",
                    Priority = 210,
                    Category = Category,
                    Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Performance,
                    Icon = "profiler_icon",
                    RequiresProject = true
                }
            };
        }
    }

    /// <summary>
    /// Menu item descriptor with rich metadata
    /// </summary>
    [Serializable]
    public class MenuItemDescriptor
    {
        public string DisplayName;
        public string Description;
        public string MenuPath;
        public int Priority;
        public WorkflowMenuSystem.WorkflowCategory Category;
        public WorkflowMenuSystem.WorkflowSubcategory Subcategory;
        public string Icon;
        public string Shortcut;
        public bool RequiresProject = true;
        public bool RequiresPlayMode = false;
        public bool IsHighPriority = false;
        public float UsageFrequency = 0.0f;
        public string[] Tags = new string[0];
        public MenuItemDescriptor[] RelatedItems = new MenuItemDescriptor[0];
    }

    /// <summary>
    /// Validation result for menu structure analysis
    /// </summary>
    public class ValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Info { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0;
        
        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
        public void AddInfo(string message) => Info.Add(message);
        
        public override string ToString()
        {
            var result = "";
            if (Errors.Count > 0) result += $"Errors: {string.Join(", ", Errors)}\n";
            if (Warnings.Count > 0) result += $"Warnings: {string.Join(", ", Warnings)}\n";
            if (Info.Count > 0) result += $"Info: {string.Join(", ", Info)}\n";
            return result;
        }
    }
}
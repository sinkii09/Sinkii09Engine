using System;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Attribute for registering menu items in the workflow-based menu system
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WorkflowMenuAttribute : Attribute
    {
        public WorkflowMenuSystem.WorkflowCategory Category { get; }
        public WorkflowMenuSystem.WorkflowSubcategory Subcategory { get; }
        public int Priority { get; set; } = 0;
        public bool ValidateMethod { get; set; } = false;

        public WorkflowMenuAttribute(WorkflowMenuSystem.WorkflowCategory category, 
                                   WorkflowMenuSystem.WorkflowSubcategory subcategory)
        {
            Category = category;
            Subcategory = subcategory;
        }
    }

    /// <summary>
    /// Rich metadata for menu items
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MenuMetadataAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Shortcut { get; set; }
        public bool RequiresProject { get; set; } = true;
        public bool RequiresPlayMode { get; set; } = false;
        public bool IsHighPriority { get; set; } = false;
        public ProjectType[] ProjectTypes { get; set; } = new ProjectType[0];
        public string[] Tags { get; set; } = new string[0];
        public string[] Keywords { get; set; } = new string[0];
        public string ToolTip { get; set; }
        public string HelpUrl { get; set; }
    }

    /// <summary>
    /// Context requirements for conditional menu display
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MenuContextAttribute : Attribute
    {
        public WorkflowMenuSystem.ProjectContext RequiredContext { get; set; }
        public WorkflowMenuSystem.ProjectContext ProhibitedContext { get; set; }
        public WorkflowMenuSystem.UserRole[] AllowedRoles { get; set; } = new WorkflowMenuSystem.UserRole[0];
        public bool ShowInContextMenu { get; set; } = true;
        public bool ShowInMainMenu { get; set; } = true;
        public bool ShowInCommandPalette { get; set; } = true;
    }

    /// <summary>
    /// Performance and usage tracking metadata
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MenuPerformanceAttribute : Attribute
    {
        public int ExpectedExecutionTimeMs { get; set; } = 0;
        public bool TrackUsage { get; set; } = true;
        public bool ShowInFrequentlyUsed { get; set; } = true;
        public float RecommendationWeight { get; set; } = 1.0f;
        public string[] RelatedCommands { get; set; } = new string[0];
    }

    /// <summary>
    /// Visual presentation attributes
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MenuVisualAttribute : Attribute
    {
        public string IconPath { get; set; }
        public float Red { get; set; } = 1.0f;
        public float Green { get; set; } = 1.0f;
        public float Blue { get; set; } = 1.0f;
        public float Alpha { get; set; } = 1.0f;
        public MenuStyle Style { get; set; } = MenuStyle.Default;
        public bool ShowAsButton { get; set; } = false;
        public bool ShowInToolbar { get; set; } = false;
    }

    /// <summary>
    /// Support for different project types
    /// </summary>
    public enum ProjectType
    {
        Any,
        Game,
        Tool,
        Library,
        Plugin,
        Template
    }

    /// <summary>
    /// Menu visual styles
    /// </summary>
    public enum MenuStyle
    {
        Default,
        Bold,
        Italic,
        Warning,
        Success,
        Danger,
        Info
    }

    /// <summary>
    /// Example usage combining multiple attributes
    /// </summary>
    public class ExampleMenuUsage
    {
        [WorkflowMenu(WorkflowMenuSystem.WorkflowCategory.DailyDevelopment, 
                     WorkflowMenuSystem.WorkflowSubcategory.CodeTools, Priority = 5)]
        [MenuMetadata(
            DisplayName = "Smart Script Creator",
            Description = "AI-assisted script generation with templates",
            Icon = "script_wand",
            Shortcut = "Ctrl+Shift+N",
            IsHighPriority = true,
            Tags = new[] { "script", "ai", "generation" },
            Keywords = new[] { "create", "script", "generate", "template" },
            ToolTip = "Create scripts using AI assistance and predefined templates",
            HelpUrl = "https://docs.engine.com/script-creator"
        )]
        [MenuContext(
            RequiredContext = WorkflowMenuSystem.ProjectContext.InDevelopment,
            ShowInCommandPalette = true
        )]
        [MenuPerformance(
            ExpectedExecutionTimeMs = 500,
            TrackUsage = true,
            RecommendationWeight = 1.5f,
            RelatedCommands = new[] { "Engine/Templates/Script Templates", "Engine/Code/Refactor Tools" }
        )]
        [MenuVisual(
            IconPath = "icons/script_creator.png",
            Red = 0.2f, Green = 0.8f, Blue = 1.0f,
            Style = MenuStyle.Bold,
            ShowInToolbar = true
        )]
        public static void SmartScriptCreator()
        {
            // Implementation
        }

        // Validation method for context-sensitive enabling/disabling
        [WorkflowMenu(WorkflowMenuSystem.WorkflowCategory.DailyDevelopment, 
                     WorkflowMenuSystem.WorkflowSubcategory.CodeTools, ValidateMethod = true)]
        public static bool ValidateSmartScriptCreator()
        {
            // Return true if menu should be enabled
            return UnityEditor.EditorApplication.isCompiling == false;
        }
    }
}
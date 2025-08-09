using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Contextual menu system that adapts menus based on project state and user workflow
    /// </summary>
    [InitializeOnLoad]
    public static class ContextualMenuSystem
    {
        private static WorkflowMenuSystem.ProjectContext _currentContext;
        private static WorkflowMenuSystem.UserRole _currentUserRole = WorkflowMenuSystem.UserRole.Developer;
        private static Dictionary<string, float> _contextualRelevance = new Dictionary<string, float>();
        private static bool _autoDetectContext = true;

        static ContextualMenuSystem()
        {
            EditorApplication.update += UpdateProjectContext;
            
            // Initialize context detection
            UpdateProjectContext();
        }

        /// <summary>
        /// Update project context based on current project state
        /// </summary>
        private static void UpdateProjectContext()
        {
            if (_autoDetectContext)
            {
                var newContext = ProjectContextDetector.DetectCurrentContext();
                if (newContext != _currentContext)
                {
                    _currentContext = newContext;
                    OnContextChanged(_currentContext);
                }
            }

            // Update less frequently to avoid performance impact
            EditorApplication.update -= UpdateProjectContext;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.update += UpdateProjectContext;
            };
        }

        /// <summary>
        /// Handle context changes and update menu relevance
        /// </summary>
        private static void OnContextChanged(WorkflowMenuSystem.ProjectContext newContext)
        {
            Debug.Log($"[ContextualMenuSystem] Context changed to: {newContext}");
            UpdateMenuRelevance();
            
            // Show contextual recommendations
            ShowContextualRecommendations(newContext);
        }

        /// <summary>
        /// Get contextually relevant menu items for current state
        /// </summary>
        public static RegisteredMenuItem[] GetContextualMenuItems(int maxItems = 20)
        {
            return MenuRegistrationSystem.GetContextualMenuItems(_currentContext, _currentUserRole)
                .Take(maxItems)
                .ToArray();
        }

        /// <summary>
        /// Get smart recommendations based on current context
        /// </summary>
        public static ContextualRecommendation[] GetSmartRecommendations(int maxRecommendations = 5)
        {
            var recommendations = new List<ContextualRecommendation>();

            // Context-based recommendations
            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.NewProject))
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Title = "Set Up Your Project",
                    Description = "Get started quickly with the project setup wizard",
                    MenuPath = "Engine/Project Setup/Quick Start/Quick Start Wizard",
                    Priority = 100,
                    ReasonCode = RecommendationReason.NewProject,
                    Icon = "🚀"
                });
            }

            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.MissingDependencies))
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Title = "Check Dependencies",
                    Description = "Some dependencies appear to be missing. Check and install them.",
                    MenuPath = "Engine/Project Setup/Dependencies/Check Dependencies",
                    Priority = 90,
                    ReasonCode = RecommendationReason.MissingDependencies,
                    Icon = "📦"
                });
            }

            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.HasErrors))
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Title = "Analyze Code Issues",
                    Description = "Compilation errors detected. Use analysis tools to identify issues.",
                    MenuPath = "Engine/Quality Assurance/Code Analysis/Service Dependency Analyzer",
                    Priority = 80,
                    ReasonCode = RecommendationReason.CompilationErrors,
                    Icon = "🔍"
                });
            }

            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.HasPerformanceIssues))
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Title = "Performance Optimization",
                    Description = "Performance issues detected. Run the profiler to identify bottlenecks.",
                    MenuPath = "Engine/Quality Assurance/Performance/Performance Profiler",
                    Priority = 70,
                    ReasonCode = RecommendationReason.PerformanceIssues,
                    Icon = "⚡"
                });
            }

            // Role-based recommendations
            if (_currentUserRole == WorkflowMenuSystem.UserRole.Beginner)
            {
                recommendations.Add(new ContextualRecommendation
                {
                    Title = "Getting Started Guide",
                    Description = "New to the engine? Check out our getting started resources.",
                    MenuPath = "Engine/Help/Getting Started",
                    Priority = 60,
                    ReasonCode = RecommendationReason.UserRole,
                    Icon = "📚"
                });
            }

            return recommendations.OrderByDescending(r => r.Priority).Take(maxRecommendations).ToArray();
        }

        /// <summary>
        /// Get workflow-based quick actions for current context
        /// </summary>
        public static QuickAction[] GetQuickActions()
        {
            var actions = new List<QuickAction>();

            // Always available quick actions
            actions.Add(new QuickAction
            {
                Name = "Create Script",
                Description = "Create a new script with templates",
                MenuPath = "Engine/Daily Development/Code Tools/Smart Script Creator",
                Shortcut = "Ctrl+Shift+N",
                Icon = "📝",
                Category = QuickActionCategory.Creation
            });

            // Context-specific actions
            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.InDevelopment))
            {
                actions.Add(new QuickAction
                {
                    Name = "Asset Pipeline",
                    Description = "Manage asset processing",
                    MenuPath = "Engine/Daily Development/Asset Pipeline/Pipeline Manager",
                    Icon = "🔧",
                    Category = QuickActionCategory.Development
                });
            }

            if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.ReadyForRelease))
            {
                actions.Add(new QuickAction
                {
                    Name = "Build Package",
                    Description = "Build release package",
                    MenuPath = "Engine/Release Pipeline/Package Building/Build Package",
                    Icon = "📦",
                    Category = QuickActionCategory.Release
                });
            }

            return actions.ToArray();
        }

        /// <summary>
        /// Show contextual menu overlay (similar to VS Code command palette)
        /// </summary>
        [MenuItem("Engine/Navigation/Command Palette _%#P", priority = 0)]
        public static void ShowCommandPalette()
        {
            CommandPaletteWindow.OpenWindow(_currentContext, _currentUserRole);
        }

        /// <summary>
        /// Show contextual recommendations notification
        /// </summary>
        private static void ShowContextualRecommendations(WorkflowMenuSystem.ProjectContext context)
        {
            var recommendations = GetSmartRecommendations(3);
            if (recommendations.Length > 0)
            {
                var message = $"Based on your project state, you might want to:\n" +
                            string.Join("\n", recommendations.Select(r => $"• {r.Title}"));

                if (EditorUtility.DisplayDialog("Contextual Recommendations", message, "Open Command Palette", "Dismiss"))
                {
                    ShowCommandPalette();
                }
            }
        }

        /// <summary>
        /// Update menu relevance scores based on current context
        /// </summary>
        private static void UpdateMenuRelevance()
        {
            _contextualRelevance.Clear();

            var allMenuItems = MenuRegistrationSystem.GetContextualMenuItems(_currentContext, _currentUserRole);
            
            foreach (var item in allMenuItems)
            {
                var relevance = CalculateContextualRelevance(item);
                _contextualRelevance[item.GenerateMenuPath()] = relevance;
            }
        }

        /// <summary>
        /// Calculate relevance score for a menu item in current context
        /// </summary>
        private static float CalculateContextualRelevance(RegisteredMenuItem item)
        {
            float score = 1.0f;

            // Context matching bonus
            if (_currentContext != WorkflowMenuSystem.ProjectContext.None)
            {
                if (item.Context.RequiredContext != WorkflowMenuSystem.ProjectContext.None)
                {
                    if (_currentContext.HasFlag(item.Context.RequiredContext))
                        score += 2.0f;
                }
            }

            // Category priority based on context
            switch (item.WorkflowAttribute.Category)
            {
                case WorkflowMenuSystem.WorkflowCategory.ProjectSetup:
                    if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.NewProject))
                        score += 3.0f;
                    break;
                
                case WorkflowMenuSystem.WorkflowCategory.QualityAssurance:
                    if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.HasErrors) ||
                        _currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.HasPerformanceIssues))
                        score += 2.5f;
                    break;

                case WorkflowMenuSystem.WorkflowCategory.DailyDevelopment:
                    if (_currentContext.HasFlag(WorkflowMenuSystem.ProjectContext.InDevelopment))
                        score += 1.5f;
                    break;
            }

            // High priority items get bonus
            if (item.Metadata.IsHighPriority)
                score += 1.0f;

            // Performance-based weighting
            score *= item.Performance.RecommendationWeight;

            return score;
        }

        /// <summary>
        /// Set user role for personalized menu experience
        /// </summary>
        public static void SetUserRole(WorkflowMenuSystem.UserRole role)
        {
            _currentUserRole = role;
            UpdateMenuRelevance();
        }

        /// <summary>
        /// Enable or disable automatic context detection
        /// </summary>
        public static void SetAutoDetectContext(bool enabled)
        {
            _autoDetectContext = enabled;
            if (enabled)
            {
                UpdateProjectContext();
            }
        }

        /// <summary>
        /// Manually set project context (overrides auto-detection)
        /// </summary>
        public static void SetProjectContext(WorkflowMenuSystem.ProjectContext context)
        {
            _autoDetectContext = false;
            _currentContext = context;
            OnContextChanged(context);
        }
    }

    /// <summary>
    /// Contextual recommendation data
    /// </summary>
    [Serializable]
    public class ContextualRecommendation
    {
        public string Title;
        public string Description;
        public string MenuPath;
        public int Priority;
        public RecommendationReason ReasonCode;
        public string Icon;
        public DateTime CreatedTime = DateTime.Now;
    }

    /// <summary>
    /// Quick action for immediate access
    /// </summary>
    [Serializable]
    public class QuickAction
    {
        public string Name;
        public string Description;
        public string MenuPath;
        public string Shortcut;
        public string Icon;
        public QuickActionCategory Category;
    }

    /// <summary>
    /// Reason codes for recommendations
    /// </summary>
    public enum RecommendationReason
    {
        NewProject,
        MissingDependencies,
        CompilationErrors,
        PerformanceIssues,
        UserRole,
        UsagePattern,
        TimeOfDay,
        ProjectPhase
    }

    /// <summary>
    /// Quick action categories
    /// </summary>
    public enum QuickActionCategory
    {
        Creation,
        Development,
        Testing,
        Release,
        Maintenance
    }
}
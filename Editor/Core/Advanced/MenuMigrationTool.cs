using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Tool to migrate existing MenuItem attributes to the new workflow-based system
    /// </summary>
    public class MenuMigrationTool : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<MenuItemMigrationData> _discoveredMenuItems = new List<MenuItemMigrationData>();
        private bool _showPreview = true;
        private bool _createBackup = true;
        private string _migrationReport = "";

        [MenuItem("Engine/Maintenance/Migration/Menu Migration Tool", priority = 450)]
        public static void OpenWindow()
        {
            var window = GetWindow<MenuMigrationTool>("Menu Migration Tool");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            DiscoverExistingMenuItems();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Menu Migration Tool", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool will migrate existing MenuItem attributes to the new workflow-based menu system. " +
                "It will analyze your codebase and suggest appropriate workflow categories and metadata.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Options
            _createBackup = EditorGUILayout.Toggle("Create Backup Files", _createBackup);
            _showPreview = EditorGUILayout.Toggle("Show Migration Preview", _showPreview);

            EditorGUILayout.Space();

            // Discovery section
            EditorGUILayout.LabelField($"Discovered Menu Items: {_discoveredMenuItems.Count}", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh Discovery", GUILayout.Height(30)))
            {
                DiscoverExistingMenuItems();
            }

            EditorGUILayout.Space();

            // Menu items list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
            foreach (var item in _discoveredMenuItems)
            {
                DrawMenuItemMigrationData(item);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Migration actions
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Migration Preview", GUILayout.Height(30)))
                {
                    GenerateMigrationPreview();
                }

                GUI.enabled = _discoveredMenuItems.Count > 0;
                if (GUILayout.Button("Migrate All Items", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Migration", 
                        "This will modify your source files. Are you sure you want to proceed?", 
                        "Yes, Migrate", "Cancel"))
                    {
                        PerformMigration();
                    }
                }
                GUI.enabled = true;
            }

            // Migration report
            if (!string.IsNullOrEmpty(_migrationReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Migration Report:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_migrationReport, GUILayout.Height(100));
            }
        }

        private void DrawMenuItemMigrationData(MenuItemMigrationData data)
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.LabelField(data.MethodName, GUILayout.Width(150));
                EditorGUILayout.LabelField(data.CurrentMenuPath, GUILayout.Width(200));
                EditorGUILayout.LabelField($"→ {data.SuggestedCategory}", GUILayout.Width(150));
                
                data.IsSelected = EditorGUILayout.Toggle(data.IsSelected, GUILayout.Width(20));
            }
        }

        private void DiscoverExistingMenuItems()
        {
            _discoveredMenuItems.Clear();

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
                            var menuItemAttr = method.GetCustomAttribute<MenuItem>();
                            if (menuItemAttr != null && !HasWorkflowMenuAttribute(method))
                            {
                                var migrationData = CreateMigrationData(method, menuItemAttr);
                                _discoveredMenuItems.Add(migrationData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MenuMigrationTool] Failed to process assembly {assembly.FullName}: {ex.Message}");
                }
            }

            Debug.Log($"[MenuMigrationTool] Discovered {_discoveredMenuItems.Count} menu items for migration");
        }

        private bool HasWorkflowMenuAttribute(MethodInfo method)
        {
            return method.GetCustomAttribute<WorkflowMenuAttribute>() != null;
        }

        private MenuItemMigrationData CreateMigrationData(MethodInfo method, MenuItem menuItemAttr)
        {
            var data = new MenuItemMigrationData
            {
                Method = method,
                MethodName = method.Name,
                CurrentMenuPath = menuItemAttr.menuItem,
                Priority = menuItemAttr.priority,
                IsSelected = true
            };

            // Analyze menu path to suggest category and subcategory
            var pathAnalysis = AnalyzeMenuPath(menuItemAttr.menuItem);
            data.SuggestedCategory = pathAnalysis.Category;
            data.SuggestedSubcategory = pathAnalysis.Subcategory;
            data.SuggestedDisplayName = pathAnalysis.DisplayName;

            // Find source file
            data.SourceFilePath = FindSourceFile(method);

            return data;
        }

        private MenuPathAnalysis AnalyzeMenuPath(string menuPath)
        {
            var analysis = new MenuPathAnalysis();

            // Extract the last part as display name
            var parts = menuPath.Split('/');
            analysis.DisplayName = parts[parts.Length - 1];

            // Use keyword matching to suggest category
            var lowerPath = menuPath.ToLowerInvariant();

            if (ContainsKeywords(lowerPath, "setup", "init", "config", "dependency", "install"))
            {
                analysis.Category = WorkflowMenuSystem.WorkflowCategory.ProjectSetup;
                if (ContainsKeywords(lowerPath, "dependency", "package")) 
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Dependencies;
                else if (ContainsKeywords(lowerPath, "config", "setting"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Configuration;
                else
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.QuickStart;
            }
            else if (ContainsKeywords(lowerPath, "test", "validate", "analyze", "profile", "performance"))
            {
                analysis.Category = WorkflowMenuSystem.WorkflowCategory.QualityAssurance;
                if (ContainsKeywords(lowerPath, "test"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Testing;
                else if (ContainsKeywords(lowerPath, "performance", "profile"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Performance;
                else if (ContainsKeywords(lowerPath, "analyze", "analysis"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.CodeAnalysis;
                else
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Validation;
            }
            else if (ContainsKeywords(lowerPath, "build", "export", "package", "release", "deploy"))
            {
                analysis.Category = WorkflowMenuSystem.WorkflowCategory.ReleasePipeline;
                if (ContainsKeywords(lowerPath, "build", "package"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.PackageBuilding;
                else if (ContainsKeywords(lowerPath, "export", "distribute"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Distribution;
                else
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.ReleaseValidation;
            }
            else if (ContainsKeywords(lowerPath, "cleanup", "clean", "migrate", "diagnostic", "backup"))
            {
                analysis.Category = WorkflowMenuSystem.WorkflowCategory.Maintenance;
                if (ContainsKeywords(lowerPath, "cleanup", "clean"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.ProjectCleanup;
                else if (ContainsKeywords(lowerPath, "migrate", "migration"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Migration;
                else if (ContainsKeywords(lowerPath, "diagnostic", "diagnose"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Diagnostics;
                else
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Backup;
            }
            else
            {
                // Default to daily development
                analysis.Category = WorkflowMenuSystem.WorkflowCategory.DailyDevelopment;
                
                if (ContainsKeywords(lowerPath, "script", "code", "create", "generate"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.CodeTools;
                else if (ContainsKeywords(lowerPath, "asset", "import", "pipeline"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.AssetPipeline;
                else if (ContainsKeywords(lowerPath, "ui", "interface", "screen"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.UIDevelopment;
                else if (ContainsKeywords(lowerPath, "debug", "log"))
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.Debugging;
                else
                    analysis.Subcategory = WorkflowMenuSystem.WorkflowSubcategory.CodeTools;
            }

            return analysis;
        }

        private bool ContainsKeywords(string text, params string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword));
        }

        private string FindSourceFile(MethodInfo method)
        {
            var typeName = method.DeclaringType?.Name;
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Search for .cs files containing the type
            var searchPattern = $"{typeName}.cs";
            var files = Directory.GetFiles(Application.dataPath, searchPattern, SearchOption.AllDirectories);
            
            return files.FirstOrDefault();
        }

        private void GenerateMigrationPreview()
        {
            var preview = new StringBuilder();
            preview.AppendLine("Migration Preview:");
            preview.AppendLine("================");

            foreach (var item in _discoveredMenuItems.Where(i => i.IsSelected))
            {
                preview.AppendLine($"Method: {item.Method.DeclaringType?.Name}.{item.MethodName}");
                preview.AppendLine($"Current: {item.CurrentMenuPath}");
                preview.AppendLine($"New Category: {item.SuggestedCategory}");
                preview.AppendLine($"New Subcategory: {item.SuggestedSubcategory}");
                preview.AppendLine($"Display Name: {item.SuggestedDisplayName}");
                preview.AppendLine();
            }

            _migrationReport = preview.ToString();
        }

        private void PerformMigration()
        {
            var report = new StringBuilder();
            var migratedCount = 0;
            var errorCount = 0;

            foreach (var item in _discoveredMenuItems.Where(i => i.IsSelected))
            {
                try
                {
                    if (MigrateMenuItem(item))
                    {
                        migratedCount++;
                        report.AppendLine($"✓ Migrated: {item.MethodName}");
                    }
                    else
                    {
                        errorCount++;
                        report.AppendLine($"✗ Failed: {item.MethodName}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    report.AppendLine($"✗ Error migrating {item.MethodName}: {ex.Message}");
                }
            }

            report.Insert(0, $"Migration Complete: {migratedCount} migrated, {errorCount} errors\n\n");
            _migrationReport = report.ToString();

            AssetDatabase.Refresh();
        }

        private bool MigrateMenuItem(MenuItemMigrationData data)
        {
            if (string.IsNullOrEmpty(data.SourceFilePath) || !File.Exists(data.SourceFilePath))
                return false;

            var content = File.ReadAllText(data.SourceFilePath);
            
            // Create backup if requested
            if (_createBackup)
            {
                File.Copy(data.SourceFilePath, data.SourceFilePath + ".backup", true);
            }

            // Find and replace the MenuItem attribute
            var methodPattern = $@"\[MenuItem\([""'].*?[""']\s*(?:,\s*.*?)?\)\]\s*public\s+static\s+.*?\s+{Regex.Escape(data.MethodName)}\s*\(";
            var match = Regex.Match(content, methodPattern, RegexOptions.Multiline);

            if (!match.Success)
                return false;

            // Generate new attributes
            var newAttributes = GenerateWorkflowAttributes(data);
            var replacement = newAttributes + match.Value.Substring(match.Value.IndexOf(']') + 1);

            content = content.Replace(match.Value, replacement);

            File.WriteAllText(data.SourceFilePath, content);
            return true;
        }

        private string GenerateWorkflowAttributes(MenuItemMigrationData data)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"[WorkflowMenu(WorkflowMenuSystem.WorkflowCategory.{data.SuggestedCategory}, ");
            sb.AppendLine($"             WorkflowMenuSystem.WorkflowSubcategory.{data.SuggestedSubcategory}, Priority = {data.Priority})]");
            
            sb.AppendLine("[MenuMetadata(");
            sb.AppendLine($"    DisplayName = \"{data.SuggestedDisplayName}\",");
            sb.AppendLine($"    Description = \"Migrated from {data.CurrentMenuPath}\"");
            sb.Append(")]");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Data structure for menu item migration
    /// </summary>
    [Serializable]
    public class MenuItemMigrationData
    {
        public MethodInfo Method;
        public string MethodName;
        public string CurrentMenuPath;
        public int Priority;
        public bool IsSelected = true;
        public WorkflowMenuSystem.WorkflowCategory SuggestedCategory;
        public WorkflowMenuSystem.WorkflowSubcategory SuggestedSubcategory;
        public string SuggestedDisplayName;
        public string SourceFilePath;
    }

    /// <summary>
    /// Analysis result for menu path categorization
    /// </summary>
    public class MenuPathAnalysis
    {
        public WorkflowMenuSystem.WorkflowCategory Category;
        public WorkflowMenuSystem.WorkflowSubcategory Subcategory;
        public string DisplayName;
    }
}
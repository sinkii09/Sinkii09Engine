using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Advanced menu documentation and validation system
    /// </summary>
    public class MenuDocumentationGenerator : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _includeInternalMenus = false;
        private bool _includeUsageStatistics = true;
        private bool _includeValidationReport = true;
        private bool _generateMarkdown = true;
        private bool _generateHTML = false;
        private string _outputPath = "Documentation/MenuSystem/";
        private MenuValidationReport _lastValidationReport;
        
        [MenuItem("Engine/Maintenance/Documentation/Menu Documentation Generator", priority = 460)]
        public static void OpenWindow()
        {
            var window = GetWindow<MenuDocumentationGenerator>("Menu Documentation");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Menu Documentation Generator", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            DrawOptions();
            EditorGUILayout.Space();
            
            DrawGenerationButtons();
            EditorGUILayout.Space();
            
            DrawValidationSection();
            EditorGUILayout.Space();
            
            DrawPreview();
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Generation Options", EditorStyles.boldLabel);
            
            _includeInternalMenus = EditorGUILayout.Toggle("Include Internal Menus", _includeInternalMenus);
            _includeUsageStatistics = EditorGUILayout.Toggle("Include Usage Statistics", _includeUsageStatistics);
            _includeValidationReport = EditorGUILayout.Toggle("Include Validation Report", _includeValidationReport);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Output Formats", EditorStyles.boldLabel);
            _generateMarkdown = EditorGUILayout.Toggle("Generate Markdown", _generateMarkdown);
            _generateHTML = EditorGUILayout.Toggle("Generate HTML", _generateHTML);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Output Directory", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputPath = EditorGUILayout.TextField(_outputPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Output Directory", _outputPath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _outputPath = path;
                    }
                }
            }
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.LabelField("Documentation Generation", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Full Documentation", GUILayout.Height(30)))
                {
                    GenerateDocumentation();
                }

                if (GUILayout.Button("Quick Reference", GUILayout.Height(30)))
                {
                    GenerateQuickReference();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Usage Analytics Report", GUILayout.Height(25)))
                {
                    GenerateUsageReport();
                }

                if (GUILayout.Button("Category Overview", GUILayout.Height(25)))
                {
                    GenerateCategoryOverview();
                }
            }
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Menu System Validation", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Menu System", GUILayout.Height(30)))
                {
                    _lastValidationReport = MenuRegistrationSystem.ValidateAllMenus();
                }

                if (GUILayout.Button("Fix Common Issues", GUILayout.Height(30)))
                {
                    FixCommonIssues();
                }
            }

            if (_lastValidationReport != null)
            {
                EditorGUILayout.Space();
                DrawValidationReport(_lastValidationReport);
            }
        }

        private void DrawValidationReport(MenuValidationReport report)
        {
            EditorGUILayout.LabelField("Validation Results:", EditorStyles.boldLabel);
            
            if (report.Errors.Count > 0)
            {
                EditorGUILayout.HelpBox($"Errors: {report.Errors.Count}", MessageType.Error);
                foreach (var error in report.Errors.Take(5))
                {
                    EditorGUILayout.LabelField($"• {error}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (report.Warnings.Count > 0)
            {
                EditorGUILayout.HelpBox($"Warnings: {report.Warnings.Count}", MessageType.Warning);
                foreach (var warning in report.Warnings.Take(3))
                {
                    EditorGUILayout.LabelField($"• {warning}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox("Menu system validation passed!", MessageType.Info);
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Documentation Preview", EditorStyles.boldLabel);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
            
            var preview = GenerateDocumentationPreview();
            EditorGUILayout.TextArea(preview, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndScrollView();
        }

        private void GenerateDocumentation()
        {
            try
            {
                EnsureOutputDirectory();

                if (_generateMarkdown)
                {
                    var markdownContent = GenerateMarkdownDocumentation();
                    var markdownPath = Path.Combine(_outputPath, "MenuSystem.md");
                    File.WriteAllText(markdownPath, markdownContent);
                    Debug.Log($"Generated Markdown documentation: {markdownPath}");
                }

                if (_generateHTML)
                {
                    var htmlContent = GenerateHTMLDocumentation();
                    var htmlPath = Path.Combine(_outputPath, "MenuSystem.html");
                    File.WriteAllText(htmlPath, htmlContent);
                    Debug.Log($"Generated HTML documentation: {htmlPath}");
                }

                // Generate additional files
                if (_includeUsageStatistics)
                {
                    GenerateUsageReport();
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Documentation Generated", 
                    $"Menu documentation has been generated in:\n{_outputPath}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Failed to generate documentation:\n{ex.Message}", "OK");
            }
        }

        private string GenerateMarkdownDocumentation()
        {
            var doc = new StringBuilder();
            
            // Header
            doc.AppendLine("# Engine Menu System Documentation");
            doc.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            doc.AppendLine();
            
            // Table of Contents
            doc.AppendLine("## Table of Contents");
            doc.AppendLine("- [Overview](#overview)");
            doc.AppendLine("- [Menu Categories](#menu-categories)");
            doc.AppendLine("- [Quick Actions](#quick-actions)");
            doc.AppendLine("- [Keyboard Shortcuts](#keyboard-shortcuts)");
            if (_includeUsageStatistics) doc.AppendLine("- [Usage Statistics](#usage-statistics)");
            if (_includeValidationReport) doc.AppendLine("- [System Validation](#system-validation)");
            doc.AppendLine();

            // Overview
            doc.AppendLine("## Overview");
            doc.AppendLine("The Engine menu system provides a workflow-based approach to organizing development tools.");
            doc.AppendLine("Menus are dynamically organized based on project context and user roles for improved discoverability.");
            doc.AppendLine();

            // Menu Categories
            doc.AppendLine("## Menu Categories");
            doc.AppendLine();

            foreach (var category in Enum.GetValues(typeof(WorkflowMenuSystem.WorkflowCategory)).Cast<WorkflowMenuSystem.WorkflowCategory>())
            {
                var categoryInfo = WorkflowMenuSystem.GetCategoryInfo(category);
                var menuItems = MenuRegistrationSystem.GetMenuItemsForCategory(category);
                
                if (!_includeInternalMenus && menuItems.Length == 0) continue;

                doc.AppendLine($"### {categoryInfo.Icon} {categoryInfo.DisplayName}");
                doc.AppendLine($"{categoryInfo.Description}");
                doc.AppendLine();

                if (menuItems.Length > 0)
                {
                    var subcategories = menuItems.GroupBy(item => item.WorkflowAttribute.Subcategory)
                        .OrderBy(g => (int)g.Key);

                    foreach (var subcategoryGroup in subcategories)
                    {
                        doc.AppendLine($"#### {subcategoryGroup.Key}");
                        doc.AppendLine();

                        foreach (var item in subcategoryGroup.OrderBy(i => i.WorkflowAttribute.Priority))
                        {
                            var displayName = item.Metadata.DisplayName ?? item.Method.Name;
                            doc.AppendLine($"- **{displayName}**");
                            
                            if (!string.IsNullOrEmpty(item.Metadata.Description))
                            {
                                doc.AppendLine($"  - {item.Metadata.Description}");
                            }
                            
                            doc.AppendLine($"  - Path: `{item.GenerateMenuPath()}`");
                            
                            if (!string.IsNullOrEmpty(item.Metadata.Shortcut))
                            {
                                doc.AppendLine($"  - Shortcut: `{item.Metadata.Shortcut}`");
                            }
                            
                            doc.AppendLine();
                        }
                    }
                }
            }

            // Quick Actions
            doc.AppendLine("## Quick Actions");
            doc.AppendLine("Quick actions provide immediate access to frequently used tools:");
            doc.AppendLine();
            
            var quickActions = ContextualMenuSystem.GetQuickActions();
            foreach (var action in quickActions)
            {
                doc.AppendLine($"- **{action.Name}** - {action.Description}");
                if (!string.IsNullOrEmpty(action.Shortcut))
                {
                    doc.AppendLine($"  - Shortcut: `{action.Shortcut}`");
                }
                doc.AppendLine();
            }

            // Keyboard Shortcuts
            doc.AppendLine("## Keyboard Shortcuts");
            doc.AppendLine();
            doc.AppendLine("| Shortcut | Action |");
            doc.AppendLine("|----------|--------|");
            doc.AppendLine("| `Ctrl+Shift+P` | Open Command Palette |");
            
            var allMenus = MenuRegistrationSystem.GetContextualMenuItems(
                WorkflowMenuSystem.ProjectContext.InDevelopment, 
                WorkflowMenuSystem.UserRole.Developer);
            
            foreach (var menu in allMenus.Where(m => !string.IsNullOrEmpty(m.Metadata.Shortcut)))
            {
                doc.AppendLine($"| `{menu.Metadata.Shortcut}` | {menu.Metadata.DisplayName ?? menu.Method.Name} |");
            }
            doc.AppendLine();

            // Usage Statistics
            if (_includeUsageStatistics)
            {
                doc.AppendLine("## Usage Statistics");
                doc.AppendLine("*Statistics would be included here based on actual usage data*");
                doc.AppendLine();
            }

            // Validation Report
            if (_includeValidationReport && _lastValidationReport != null)
            {
                doc.AppendLine("## System Validation");
                doc.AppendLine();
                
                if (_lastValidationReport.IsValid)
                {
                    doc.AppendLine("✅ Menu system validation passed - no issues found.");
                }
                else
                {
                    if (_lastValidationReport.Errors.Count > 0)
                    {
                        doc.AppendLine("### Errors");
                        foreach (var error in _lastValidationReport.Errors)
                        {
                            doc.AppendLine($"- ❌ {error}");
                        }
                        doc.AppendLine();
                    }

                    if (_lastValidationReport.Warnings.Count > 0)
                    {
                        doc.AppendLine("### Warnings");
                        foreach (var warning in _lastValidationReport.Warnings)
                        {
                            doc.AppendLine($"- ⚠️ {warning}");
                        }
                        doc.AppendLine();
                    }
                }
            }

            return doc.ToString();
        }

        private string GenerateHTMLDocumentation()
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='utf-8'>");
            html.AppendLine("    <title>Engine Menu System Documentation</title>");
            html.AppendLine("    <style>");
            html.AppendLine(GetHTMLStyles());
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            html.AppendLine("    <div class='container'>");
            html.AppendLine("        <header>");
            html.AppendLine("            <h1>🎛️ Engine Menu System Documentation</h1>");
            html.AppendLine($"           <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine("        </header>");
            
            // Convert markdown to HTML (simplified)
            var markdownContent = GenerateMarkdownDocumentation();
            var htmlContent = ConvertMarkdownToHTML(markdownContent);
            html.AppendLine(htmlContent);
            
            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private string GetHTMLStyles()
        {
            return @"
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }
                .container { max-width: 1200px; margin: 0 auto; background: white; padding: 40px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
                header { text-align: center; margin-bottom: 40px; padding-bottom: 20px; border-bottom: 2px solid #eee; }
                h1 { color: #333; margin: 0; }
                h2 { color: #2c5aa0; margin-top: 40px; margin-bottom: 20px; }
                h3 { color: #555; margin-top: 30px; }
                h4 { color: #666; margin-top: 20px; }
                .category-icon { font-size: 1.2em; margin-right: 8px; }
                table { width: 100%; border-collapse: collapse; margin: 20px 0; }
                th, td { text-align: left; padding: 8px 12px; border-bottom: 1px solid #ddd; }
                th { background-color: #f8f9fa; font-weight: bold; }
                code { background: #f4f4f4; padding: 2px 6px; border-radius: 3px; font-family: 'Monaco', 'Consolas', monospace; }
                ul { line-height: 1.6; }
                .validation-pass { color: #28a745; }
                .validation-error { color: #dc3545; }
                .validation-warning { color: #ffc107; }
            ";
        }

        private string ConvertMarkdownToHTML(string markdown)
        {
            // Simplified markdown to HTML conversion
            var html = markdown;
            
            // Headers
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.*?)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.*?)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.*?)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Code blocks
            html = System.Text.RegularExpressions.Regex.Replace(html, @"`([^`]+)`", "<code>$1</code>");
            
            // Bold
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.*?)\*\*", "<strong>$1</strong>");
            
            // Lists
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^- (.*?)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"(<li>.*?</li>)", "<ul>$1</ul>", System.Text.RegularExpressions.RegexOptions.Singleline);
            
            // Line breaks
            html = html.Replace("\n\n", "<br><br>");
            
            return html;
        }

        private void GenerateQuickReference()
        {
            var quickRef = GenerateQuickReferenceContent();
            var quickRefPath = Path.Combine(_outputPath, "QuickReference.md");
            
            EnsureOutputDirectory();
            File.WriteAllText(quickRefPath, quickRef);
            
            Debug.Log($"Generated quick reference: {quickRefPath}");
            EditorUtility.DisplayDialog("Quick Reference Generated", 
                $"Quick reference generated:\n{quickRefPath}", "OK");
        }

        private string GenerateQuickReferenceContent()
        {
            var doc = new StringBuilder();
            
            doc.AppendLine("# Menu System Quick Reference");
            doc.AppendLine();
            
            // Most important shortcuts
            doc.AppendLine("## Essential Shortcuts");
            doc.AppendLine("- `Ctrl+Shift+P` - Command Palette");
            doc.AppendLine("- `Ctrl+Shift+N` - Create Script");
            doc.AppendLine();
            
            // Categories at a glance
            doc.AppendLine("## Categories");
            foreach (var category in Enum.GetValues(typeof(WorkflowMenuSystem.WorkflowCategory)).Cast<WorkflowMenuSystem.WorkflowCategory>())
            {
                var info = WorkflowMenuSystem.GetCategoryInfo(category);
                doc.AppendLine($"- {info.Icon} **{info.DisplayName}** - {info.Description}");
            }
            
            return doc.ToString();
        }

        private void GenerateUsageReport()
        {
            // This would integrate with the MenuUsageTracker for real statistics
            var report = "# Menu Usage Report\n\nUsage statistics would be generated here based on actual tracking data.";
            
            var reportPath = Path.Combine(_outputPath, "UsageReport.md");
            EnsureOutputDirectory();
            File.WriteAllText(reportPath, report);
            
            Debug.Log($"Generated usage report: {reportPath}");
        }

        private void GenerateCategoryOverview()
        {
            var overview = GenerateCategoryOverviewContent();
            var overviewPath = Path.Combine(_outputPath, "CategoryOverview.md");
            
            EnsureOutputDirectory();
            File.WriteAllText(overviewPath, overview);
            
            Debug.Log($"Generated category overview: {overviewPath}");
        }

        private string GenerateCategoryOverviewContent()
        {
            var doc = new StringBuilder();
            
            doc.AppendLine("# Menu Category Overview");
            doc.AppendLine();
            
            foreach (var category in Enum.GetValues(typeof(WorkflowMenuSystem.WorkflowCategory)).Cast<WorkflowMenuSystem.WorkflowCategory>())
            {
                var info = WorkflowMenuSystem.GetCategoryInfo(category);
                var items = MenuRegistrationSystem.GetMenuItemsForCategory(category);
                
                doc.AppendLine($"## {info.Icon} {info.DisplayName}");
                doc.AppendLine($"{info.Description}");
                doc.AppendLine($"**Menu Items:** {items.Length}");
                doc.AppendLine();
            }
            
            return doc.ToString();
        }

        private void FixCommonIssues()
        {
            var report = MenuRegistrationSystem.ValidateAllMenus();
            var fixedCount = 0;

            // This would contain automated fixes for common issues
            Debug.Log($"Fixed {fixedCount} common menu system issues");
            
            // Re-validate after fixes
            _lastValidationReport = MenuRegistrationSystem.ValidateAllMenus();
        }

        private void EnsureOutputDirectory()
        {
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        private string GenerateDocumentationPreview()
        {
            var preview = new StringBuilder();
            
            preview.AppendLine("# Engine Menu System Documentation");
            preview.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            preview.AppendLine();
            
            var categoriesCount = Enum.GetValues(typeof(WorkflowMenuSystem.WorkflowCategory)).Length;
            var totalMenus = MenuRegistrationSystem.GetContextualMenuItems(
                WorkflowMenuSystem.ProjectContext.InDevelopment, 
                WorkflowMenuSystem.UserRole.Developer).Length;
            
            preview.AppendLine($"- Categories: {categoriesCount}");
            preview.AppendLine($"- Total Menu Items: {totalMenus}");
            preview.AppendLine($"- Quick Actions: {ContextualMenuSystem.GetQuickActions().Length}");
            
            if (_lastValidationReport != null)
            {
                preview.AppendLine($"- Validation: {(_lastValidationReport.IsValid ? "PASS" : "ISSUES")}");
            }
            
            preview.AppendLine();
            preview.AppendLine("Sample categories would be listed here...");
            
            return preview.ToString();
        }
    }
}
using UnityEngine;
using UnityEditor;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Editor.Core;
using System.Text;
using System.Linq;

namespace Sinkii09.Engine.Editor.Analysis
{
    /// <summary>
    /// Editor tools for analyzing service dependencies and generating reports
    /// </summary>
    public class ServiceDependencyAnalyzer : EditorWindow
    {
        private Vector2 scrollPosition;
        private ServiceDependencyReport lastReport;
        private ServiceDependencyGraph lastGraph;
        private bool showDetailedView = false;
        private bool showVisualization = false;
        
        [MenuItem("Engine/Analysis/Services/Dependency Analyzer", false, 210)]
        public static void ShowWindow()
        {
            var window = GetWindow<ServiceDependencyAnalyzer>("Dependency Analyzer");
            window.Show();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Service Dependency Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Control buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Analyze Current Services"))
            {
                AnalyzeCurrentServices();
            }
            
            if (GUILayout.Button("📋 Generate Report"))
            {
                GenerateAndShowReport();
            }
            
            if (GUILayout.Button("📊 Export Report"))
            {
                ExportReportToFile();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Display options
            EditorGUILayout.BeginHorizontal();
            showDetailedView = EditorGUILayout.Toggle("Detailed View", showDetailedView);
            showVisualization = EditorGUILayout.Toggle("Show Graph Visualization", showVisualization);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Report display
            if (lastReport != null)
            {
                DisplayReport();
            }
            else
            {
                EditorGUILayout.LabelField("Click 'Analyze Current Services' to generate dependency report", 
                    EditorStyles.helpBox);
            }
        }
        
        private void AnalyzeCurrentServices()
        {
            try
            {
                // Create a temporary service container to analyze dependencies
                var tempContainer = new ServiceContainer();
                
                // Register runtime services (same as Engine does)
                tempContainer.RegisterRuntimeServices();
                
                // Build dependency graph
                lastGraph = tempContainer.BuildDependencyGraph();
                lastReport = lastGraph.GenerateReport();
                
                Debug.Log("✅ Service dependency analysis completed");
                Debug.Log($"📊 Found {lastReport.TotalServices} services with max depth {lastReport.MaxDepth}");
                
                if (lastReport.CircularDependencies.Count > 0)
                {
                    Debug.LogWarning($"⚠️ Found {lastReport.CircularDependencies.Count} circular dependencies!");
                }
                
                Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Failed to analyze services: {ex.Message}");
                EditorUtility.DisplayDialog("Analysis Error", 
                    $"Failed to analyze services:\n{ex.Message}", "OK");
            }
        }
        
        private void GenerateAndShowReport()
        {
            if (lastReport == null)
            {
                AnalyzeCurrentServices();
                return;
            }
            
            var reportText = GenerateDetailedReport();
            
            // Show in console
            Debug.Log("📋 Service Dependency Report:\n" + reportText);
            
            // Show in dialog
            var shortReport = $"Services: {lastReport.TotalServices}\n" +
                             $"Max Depth: {lastReport.MaxDepth}\n" +
                             $"Circular Dependencies: {lastReport.CircularDependencies.Count}\n" +
                             $"Root Services: {lastReport.ServicesWithNoDependencies}\n" +
                             $"Leaf Services: {lastReport.ServicesWithNoDependents}";
            
            EditorUtility.DisplayDialog("Dependency Report", shortReport, "OK");
        }
        
        private void ExportReportToFile()
        {
            if (lastReport == null)
            {
                EditorUtility.DisplayDialog("No Report", "Generate a report first", "OK");
                return;
            }
            
            var path = EditorUtility.SaveFilePanel("Export Dependency Report", 
                "", "service-dependency-report.txt", "txt");
                
            if (!string.IsNullOrEmpty(path))
            {
                var reportContent = GenerateDetailedReport();
                System.IO.File.WriteAllText(path, reportContent);
                Debug.Log($"📄 Report exported to: {path}");
                EditorUtility.DisplayDialog("Export Complete", $"Report saved to:\n{path}", "OK");
            }
        }
        
        private void DisplayReport()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // Summary statistics
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.LabelField($"Total Services: {lastReport.TotalServices}");
            EditorGUILayout.LabelField($"Maximum Dependency Depth: {lastReport.MaxDepth}");
            EditorGUILayout.LabelField($"Average Dependencies: {lastReport.AverageDependencies:F1}");
            EditorGUILayout.LabelField($"Root Services (no dependencies): {lastReport.ServicesWithNoDependencies}");
            EditorGUILayout.LabelField($"Leaf Services (no dependents): {lastReport.ServicesWithNoDependents}");
            
            // Circular dependencies warning
            if (lastReport.CircularDependencies.Count > 0)
            {
                EditorGUILayout.Space();
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = Color.red;
                EditorGUILayout.LabelField($"⚠️ Circular Dependencies: {lastReport.CircularDependencies.Count}", style);
                
                foreach (var cycle in lastReport.CircularDependencies)
                {
                    var cycleText = string.Join(" → ", cycle.Select(t => t.Name));
                    EditorGUILayout.LabelField($"  {cycleText}", style);
                }
            }
            
            EditorGUILayout.EndVertical();
            
            // Detailed view
            if (showDetailedView && lastGraph != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Detailed Analysis", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                
                // Use optimized nodes for better performance
                var depthGroups = lastGraph.OptimizedNodes.Values
                    .GroupBy(n => n.Depth)
                    .OrderBy(g => g.Key);
                
                foreach (var group in depthGroups)
                {
                    EditorGUILayout.LabelField($"Depth {group.Key}:", EditorStyles.boldLabel);
                    foreach (var node in group.OrderBy(n => n.ServiceType.Name))
                    {
                        EditorGUILayout.LabelField($"  {node.ServiceType.Name}");
                    }
                    EditorGUILayout.Space();
                }
                
                EditorGUILayout.EndVertical();
            }
            
            // Visualization
            if (showVisualization && lastGraph != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Graph Visualization", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                
                var visualization = lastGraph.GenerateVisualization();
                EditorGUILayout.LabelField(visualization, EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private string GenerateDetailedReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SERVICE DEPENDENCY ANALYSIS REPORT");
            sb.AppendLine("==================================");
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Summary
            sb.AppendLine("SUMMARY");
            sb.AppendLine("-------");
            sb.AppendLine(lastReport.ToString());
            sb.AppendLine();
            
            // Detailed graph visualization
            if (lastGraph != null)
            {
                sb.AppendLine("DEPENDENCY GRAPH");
                sb.AppendLine("---------------");
                sb.AppendLine(lastGraph.GenerateVisualization());
                sb.AppendLine();
            }
            
            // Recommendations
            sb.AppendLine("RECOMMENDATIONS");
            sb.AppendLine("--------------");
            
            if (lastReport.CircularDependencies.Count > 0)
            {
                sb.AppendLine("🔴 CRITICAL: Circular dependencies detected!");
                sb.AppendLine("  - Review service design to eliminate circular references");
                sb.AppendLine("  - Consider using interfaces or lazy initialization");
                sb.AppendLine();
            }
            
            if (lastReport.MaxDepth > 5)
            {
                sb.AppendLine("⚠️ WARNING: Deep dependency chains detected");
                sb.AppendLine("  - Consider flattening dependency hierarchy");
                sb.AppendLine("  - Review if all dependencies are necessary");
                sb.AppendLine();
            }
            
            if (lastReport.AverageDependencies > 3)
            {
                sb.AppendLine("💡 INFO: High average dependencies per service");
                sb.AppendLine("  - Consider breaking down complex services");
                sb.AppendLine("  - Review single responsibility principle");
                sb.AppendLine();
            }
            
            sb.AppendLine("✅ Analysis completed successfully");
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Menu items for quick dependency analysis
    /// </summary>
    public static class ServiceDependencyMenuItems
    {
        [MenuItem("Engine/Analysis/Services/Quick Dependency Check", false, 211)]
        public static void QuickDependencyCheck()
        {
            try
            {
                var container = new ServiceContainer();
                container.RegisterRuntimeServices();
                
                var graph = container.BuildDependencyGraph();
                var report = graph.GenerateReport();
                
                Debug.Log("🔍 Quick Dependency Check Results:");
                Debug.Log($"   Services: {report.TotalServices}");
                Debug.Log($"   Max Depth: {report.MaxDepth}");
                Debug.Log($"   Circular Dependencies: {report.CircularDependencies.Count}");
                
                if (report.CircularDependencies.Count > 0)
                {
                    Debug.LogError("❌ Circular dependencies found! Open Dependency Analyzer for details.");
                }
                else
                {
                    Debug.Log("✅ No circular dependencies detected");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Quick check failed: {ex.Message}");
            }
        }
        
        [MenuItem("Engine/Analysis/Services/Log Full Dependency Graph", false, 212)]
        public static void LogFullDependencyGraph()
        {
            try
            {
                var container = new ServiceContainer();
                container.RegisterRuntimeServices();
                
                var graph = container.BuildDependencyGraph();
                var visualization = graph.GenerateVisualization();
                
                Debug.Log("📊 Full Service Dependency Graph:\n" + visualization);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Failed to generate graph: {ex.Message}");
            }
        }
        
        [MenuItem("Engine/Analysis/Services/Export Dependency Report", false, 213)]
        public static void ExportDependencyReport()
        {
            try
            {
                var container = new ServiceContainer();
                container.RegisterRuntimeServices();
                
                var graph = container.BuildDependencyGraph();
                var report = graph.GenerateReport();
                
                var path = EditorUtility.SaveFilePanel("Export Dependency Report", 
                    "", $"dependency-report-{System.DateTime.Now:yyyyMMdd-HHmmss}.txt", "txt");
                    
                if (!string.IsNullOrEmpty(path))
                {
                    var content = $"{report}\n\n{graph.GenerateVisualization()}";
                    System.IO.File.WriteAllText(path, content);
                    Debug.Log($"📄 Report exported to: {path}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Export failed: {ex.Message}");
            }
        }
    }
}
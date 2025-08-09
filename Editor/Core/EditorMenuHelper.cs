using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core
{
    /// <summary>
    /// Helper utilities for unified menu system
    /// </summary>
    public static class EditorMenuHelper
    {
        /// <summary>
        /// Create a menu item with standard engine formatting
        /// </summary>
        public static void CreateMenuItem(EditorMenuSystem.MenuCategory category, 
            EditorMenuSystem.MenuSubcategory subcategory, 
            string itemName, int offset = 0)
        {
            var menuPath = EditorMenuSystem.GetMenuPath(category, subcategory, itemName);
            var priority = EditorMenuSystem.GetPriority(category, subcategory, offset);
            
            Debug.Log($"Menu Path: {menuPath}, Priority: {priority}");
        }

        /// <summary>
        /// Validate current menu structure
        /// </summary>
        [MenuItem("Engine/Help/Documentation/Validate Menu Structure", false, 910)]
        public static void ValidateMenuStructure()
        {
            var menuReport = GenerateMenuReport();
            Debug.Log("=== Engine Menu Structure Report ===\n" + menuReport);
            
            EditorUtility.DisplayDialog("Menu Validation", 
                "Menu structure report generated. Check Console for details.", "OK");
        }

        /// <summary>
        /// Show menu guidelines
        /// </summary>
        [MenuItem("Engine/Help/Documentation/Menu Guidelines", false, 911)]
        public static void ShowMenuGuidelines()
        {
            var guidelines = GetMenuGuidelines();
            EditorUtility.DisplayDialog("Menu Guidelines", guidelines, "OK");
        }

        /// <summary>
        /// Generate comprehensive menu report
        /// </summary>
        private static string GenerateMenuReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("Engine Menu Structure:");
            report.AppendLine("====================");
            report.AppendLine();
            
            // Setup Category
            report.AppendLine("📋 SETUP (Priority 0-99)");
            report.AppendLine("├── Dependencies/");
            report.AppendLine("│   ├── Check Dependencies (10)");
            report.AppendLine("│   ├── Quick Install Missing (11)"); 
            report.AppendLine("│   └── Reset Startup Check (12)");
            report.AppendLine("├── Services/ (20-29)");
            report.AppendLine("└── Configurations/ (30-39)");
            report.AppendLine();
            
            // Tools Category  
            report.AppendLine("🔧 TOOLS (Priority 100-199)");
            report.AppendLine("├── Scripts/ (110-119)");
            report.AppendLine("├── UI/ (120-129)");
            report.AppendLine("└── Assets/ (130-139)");
            report.AppendLine();
            
            // Analysis Category
            report.AppendLine("📊 ANALYSIS (Priority 200-299)");
            report.AppendLine("├── Services/");
            report.AppendLine("│   ├── Dependency Analyzer (210)");
            report.AppendLine("│   ├── Quick Dependency Check (211)");
            report.AppendLine("│   ├── Log Full Dependency Graph (212)");
            report.AppendLine("│   └── Export Dependency Report (213)");
            report.AppendLine("├── Dependencies/ (220-229)");
            report.AppendLine("└── Performance/ (230-239)");
            report.AppendLine();
            
            // Generators Category
            report.AppendLine("⚡ GENERATORS (Priority 300-399)");
            report.AppendLine("├── Code/ (310-319)");
            report.AppendLine("├── Assets/ (320-329)");
            report.AppendLine("└── UI/ (330-339)");
            report.AppendLine();
            
            // Configuration Category
            report.AppendLine("⚙️ CONFIGURATION (Priority 400-499)");
            report.AppendLine("├── Creation/ (410-419)");
            report.AppendLine("├── Validation/ (420-429)");
            report.AppendLine("└── Management/ (430-439)");
            report.AppendLine();
            
            // Development Category
            report.AppendLine("🔬 DEVELOPMENT (Priority 500-599)");
            report.AppendLine("├── Testing/ (510-519)");
            report.AppendLine("├── Debugging/ (520-529)");
            report.AppendLine("└── Utilities/ (530-539)");
            report.AppendLine();
            
            // Export Category
            report.AppendLine("📦 EXPORT (Priority 600-699)");
            report.AppendLine("├── Package/");
            report.AppendLine("│   ├── Export as UnityPackage (610)");
            report.AppendLine("│   └── Validate Package Structure (611)");
            report.AppendLine("└── Project/");
            report.AppendLine("    └── Create Release Folder (620)");
            report.AppendLine();
            
            // Help Category
            report.AppendLine("❓ HELP (Priority 900-999)");
            report.AppendLine("└── Documentation/");
            report.AppendLine("    ├── Validate Menu Structure (910)");
            report.AppendLine("    └── Menu Guidelines (911)");
            
            return report.ToString();
        }

        private static string GetMenuGuidelines()
        {
            return @"Engine Menu Guidelines:

📋 CATEGORIES:
• Setup (0-99): First-time setup, dependencies
• Tools (100-199): Daily development tools  
• Analysis (200-299): Code analysis & debugging
• Generators (300-399): Code & asset generation
• Configuration (400-499): Config management
• Development (500-599): Dev utilities & testing
• Export (600-699): Package & project export
• Help (900-999): Documentation & help

🔢 PRIORITIES:
• Use category base + subcategory + offset
• Keep 10-point gaps between subcategories
• Sequential numbering within subcategories

📝 NAMING:
• Use descriptive, consistent names
• Follow: Category/Subcategory/Item Name
• Keep menu depths <= 3 levels

💡 EXAMPLES:
[MenuItem(""Engine/Setup/Dependencies/Check"", false, 10)]
[MenuItem(""Engine/Analysis/Services/Report"", false, 210)]";
        }

        /// <summary>
        /// Quick menu path generator for common patterns
        /// </summary>
        public static class QuickPaths
        {
            // Setup paths
            public static string SetupDependencies(string item, int offset = 0) => 
                EditorMenuSystem.GetMenuPath(EditorMenuSystem.MenuCategory.Setup, EditorMenuSystem.MenuSubcategory.Dependencies, item);
                
            public static string SetupServices(string item, int offset = 0) =>
                EditorMenuSystem.GetMenuPath(EditorMenuSystem.MenuCategory.Setup, EditorMenuSystem.MenuSubcategory.Services, item);
                
            // Analysis paths  
            public static string AnalysisServices(string item, int offset = 0) =>
                EditorMenuSystem.GetMenuPath(EditorMenuSystem.MenuCategory.Analysis, EditorMenuSystem.MenuSubcategory.ServiceAnalysis, item);
                
            // Tools paths
            public static string ToolsScripts(string item, int offset = 0) =>
                EditorMenuSystem.GetMenuPath(EditorMenuSystem.MenuCategory.Tools, EditorMenuSystem.MenuSubcategory.Scripts, item);
                
            // Export paths
            public static string ExportPackage(string item, int offset = 0) =>
                EditorMenuSystem.GetMenuPath(EditorMenuSystem.MenuCategory.Export, EditorMenuSystem.MenuSubcategory.Package, item);
        }
    }
}
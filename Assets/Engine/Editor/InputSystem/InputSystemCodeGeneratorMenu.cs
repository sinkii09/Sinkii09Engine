using System;
using UnityEngine;
using UnityEditor;
using Sinkii09.Engine.Editor.Core;

namespace Sinkii09.Engine.Editor.InputSystem
{
    /// <summary>
    /// Provides Unity menu integration for the Input System code generation workflow
    /// </summary>
    public static class InputSystemCodeGeneratorMenu
    {
        private const string AutoGeneratePreferenceKey = "InputSystem.AutoGenerate";

        // Menu paths - must be const for MenuItem attributes (compile-time constants)
        private const string GenerateMenuPath = "Engine/Generators/Code/Generate Input Action Cache";
        private const string AutoGenerateMenuPath = "Engine/Generators/Code/Auto-Generate Input Actions";
        private const string ClearCacheMenuPath = "Engine/Generators/Code/Clear Input Action Cache";
        private const string ValidationMenuPath = "Engine/Configuration/Validation/Validate Input Actions";
        private const string HelpMenuPath = "Engine/Help/Documentation/Input System Code Generator";

        // Menu priorities - calculated from EditorMenuSystem enum values (compile-time constants)
        private const int GeneratePriority = (int)EditorMenuSystem.MenuCategory.Generators + (int)EditorMenuSystem.MenuSubcategory.CodeGeneration + 1; // 301
        private const int AutoGeneratePriority = (int)EditorMenuSystem.MenuCategory.Generators + (int)EditorMenuSystem.MenuSubcategory.CodeGeneration + 2; // 302
        private const int ClearCachePriority = (int)EditorMenuSystem.MenuCategory.Generators + (int)EditorMenuSystem.MenuSubcategory.CodeGeneration + 3; // 303
        private const int ValidationPriority = (int)EditorMenuSystem.MenuCategory.Configuration + (int)EditorMenuSystem.MenuSubcategory.Validation + 1; // 411
        private const int HelpPriority = (int)EditorMenuSystem.MenuCategory.Help + (int)EditorMenuSystem.MenuSubcategory.Documentation + 1; // 901

        [MenuItem(GenerateMenuPath, false, GeneratePriority)]
        public static void GenerateActionCache()
        {
            Debug.Log("[InputSystemCodeGenerator] Starting manual input action cache generation...");
            
            try
            {
                EditorUtility.DisplayProgressBar("Input System Code Generator", "Analyzing input actions...", 0.3f);
                
                // Step 1: Validate and analyze
                var analyzer = new InputActionsAnalyzer();
                var actionsInfo = analyzer.AnalyzeInputActions();
                
                if (actionsInfo == null || actionsInfo.ActionMaps.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Generation Failed", 
                        "No input action maps found. Make sure your InputSystem_Actions asset has generated the C# wrapper.", 
                        "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Input System Code Generator", "Generating delegate cache...", 0.7f);
                
                // Step 2: Generate code
                var codeGenerator = new InputActionCodeGenerator();
                codeGenerator.GenerateDelegateCache(actionsInfo);

                EditorUtility.ClearProgressBar();

                // Success notification
                int totalActions = 0;
                foreach (var actionMap in actionsInfo.ActionMaps)
                    totalActions += actionMap.Actions.Count;

                string message = $"Successfully generated input action cache!\n\n" +
                               $"Action Maps: {actionsInfo.ActionMaps.Count}\n" +
                               $"Total Actions: {totalActions}\n\n" +
                               $"Generated file: InputActionMappings.generated.cs";

                EditorUtility.DisplayDialog("Generation Complete", message, "OK");
                
                Debug.Log($"[InputSystemCodeGenerator] Manual generation completed successfully! Generated cache for {actionsInfo.ActionMaps.Count} action maps with {totalActions} total actions.");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                
                Debug.LogError($"[InputSystemCodeGenerator] Generation failed: {ex.Message}");
                Debug.LogError($"[InputSystemCodeGenerator] Stack trace: {ex.StackTrace}");
                
                EditorUtility.DisplayDialog("Generation Failed", 
                    $"Failed to generate input action cache:\n\n{ex.Message}\n\nCheck the Console for details.", 
                    "OK");
            }
        }

        [MenuItem(AutoGenerateMenuPath, false, AutoGeneratePriority)]
        public static void ToggleAutoGenerate()
        {
            bool currentValue = EditorPrefs.GetBool(AutoGeneratePreferenceKey, true);
            bool newValue = !currentValue;
            
            EditorPrefs.SetBool(AutoGeneratePreferenceKey, newValue);
            
            string status = newValue ? "enabled" : "disabled";
            Debug.Log($"[InputSystemCodeGenerator] Auto-generation {status}");
            
            EditorUtility.DisplayDialog("Auto-Generation Setting", 
                $"Auto-generation is now {status}.\n\n" +
                $"When enabled, the input action cache will be automatically regenerated when InputSystem_Actions files change.",
                "OK");
        }

        [MenuItem(AutoGenerateMenuPath, true)]
        public static bool ToggleAutoGenerateValidate()
        {
            Menu.SetChecked(AutoGenerateMenuPath, 
                EditorPrefs.GetBool(AutoGeneratePreferenceKey, true));
            return true;
        }

        [MenuItem(ValidationMenuPath, false, ValidationPriority)]
        public static void ValidateInputActions()
        {
            Debug.Log("[InputSystemCodeGenerator] Validating input actions setup...");
            
            try
            {
                // Check if InputSystem_Actions type exists
                var inputActionsType = typeof(Services.InputSystem_Actions);
                if (inputActionsType == null)
                {
                    EditorUtility.DisplayDialog("Validation Failed",
                        "InputSystem_Actions class not found.\n\n" +
                        "Make sure you have:\n" +
                        "1. Created an Input Actions asset\n" +
                        "2. Generated C# Class in the asset inspector\n" +
                        "3. Compiled the project",
                        "OK");
                    return;
                }

                // Try to analyze
                var analyzer = new InputActionsAnalyzer();
                var actionsInfo = analyzer.AnalyzeInputActions();

                // Display validation results
                int totalActions = 0;
                string actionMapDetails = "";
                
                foreach (var actionMap in actionsInfo.ActionMaps)
                {
                    totalActions += actionMap.Actions.Count;
                    actionMapDetails += $"• {actionMap.Name}: {actionMap.Actions.Count} actions\n";
                }

                string message = $"Input Actions validation successful! ✓\n\n" +
                               $"Found {actionsInfo.ActionMaps.Count} action maps with {totalActions} total actions:\n\n" +
                               actionMapDetails;

                EditorUtility.DisplayDialog("Validation Successful", message, "OK");
                
                Debug.Log($"[InputSystemCodeGenerator] Validation completed successfully! Found {actionsInfo.ActionMaps.Count} action maps.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputSystemCodeGenerator] Validation failed: {ex.Message}");
                
                EditorUtility.DisplayDialog("Validation Failed",
                    $"Input Actions validation failed:\n\n{ex.Message}\n\n" +
                    $"Common solutions:\n" +
                    $"• Ensure Input Actions asset exists\n" +
                    $"• Generate C# Class in asset inspector\n" +
                    $"• Check for compilation errors\n" +
                    $"• Restart Unity if needed",
                    "OK");
            }
        }

        [MenuItem(ClearCacheMenuPath, false, ClearCachePriority)]
        public static void ClearGeneratedCache()
        {
            const string generatedFilePath = "Assets/Engine/Runtime/Scripts/Core/Services/Implemented/InputService/Generated/InputActionMappings.generated.cs";
            
            if (System.IO.File.Exists(generatedFilePath))
            {
                if (EditorUtility.DisplayDialog("Clear Generated Cache",
                    "Are you sure you want to delete the generated input action cache?\n\n" +
                    "This will remove InputActionMappings.generated.cs and you'll need to regenerate it.",
                    "Delete", "Cancel"))
                {
                    try
                    {
                        System.IO.File.Delete(generatedFilePath);
                        System.IO.File.Delete(generatedFilePath + ".meta"); // Delete meta file too
                        
                        AssetDatabase.Refresh();
                        
                        Debug.Log("[InputSystemCodeGenerator] Generated cache cleared successfully.");
                        EditorUtility.DisplayDialog("Cache Cleared", "Generated input action cache has been cleared.", "OK");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[InputSystemCodeGenerator] Failed to clear cache: {ex.Message}");
                        EditorUtility.DisplayDialog("Clear Failed", $"Failed to clear cache:\n{ex.Message}", "OK");
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Cache Found", "No generated cache file found to clear.", "OK");
            }
        }

        [MenuItem(HelpMenuPath, false, HelpPriority)]
        public static void ShowHelp()
        {
            string helpMessage = 
                "Input System Code Generator Help\n\n" +
                
                "OVERVIEW:\n" +
                "Generates high-performance delegate mappings from Unity's InputSystem_Actions to eliminate enum-to-string lookup overhead.\n\n" +
                
                "MENU OPTIONS:\n" +
                "• Generate Action Cache - Manually generate optimized input mappings\n" +
                "• Auto-Generate on Asset Changes - Toggle automatic regeneration\n" +
                "• Validate Input Actions - Check if InputSystem_Actions is properly set up\n" +
                "• Clear Generated Cache - Delete generated files\n\n" +
                
                "REQUIREMENTS:\n" +
                "1. Input Actions asset (.inputactions)\n" +
                "2. Generated C# Class enabled in asset inspector\n" +
                "3. Project compiled without errors\n\n" +
                
                "GENERATED FILES:\n" +
                "• InputActionMappings.generated.cs - High-performance delegate cache\n" +
                "• Auto-generated enums (PlayerAction, UIAction, etc.)\n\n" +
                
                "PERFORMANCE:\n" +
                "• 10-50x faster than Dictionary<enum,string> lookups\n" +
                "• Zero runtime reflection or string operations\n" +
                "• IL2CPP compatible\n\n" +
                
                "For more details, see the auto-generated delegate mapping documentation.";

            EditorUtility.DisplayDialog("Input System Code Generator - Help", helpMessage, "OK");
        }

        /// <summary>
        /// Public method for programmatic generation (used by AssetPostprocessor)
        /// </summary>
        public static bool TryGenerateActionCache(bool showProgressBar = false)
        {
            try
            {
                if (showProgressBar)
                {
                    EditorUtility.DisplayProgressBar("Auto-Generate", "Generating input action cache...", 0.5f);
                }

                var analyzer = new InputActionsAnalyzer();
                var actionsInfo = analyzer.AnalyzeInputActions();

                var codeGenerator = new InputActionCodeGenerator();
                codeGenerator.GenerateDelegateCache(actionsInfo);

                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }

                return true;
            }
            catch (Exception ex)
            {
                if (showProgressBar)
                {
                    EditorUtility.ClearProgressBar();
                }

                Debug.LogError($"[InputSystemCodeGenerator] Auto-generation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if auto-generation is enabled
        /// </summary>
        public static bool IsAutoGenerateEnabled()
        {
            return EditorPrefs.GetBool(AutoGeneratePreferenceKey, true);
        }
    }
}
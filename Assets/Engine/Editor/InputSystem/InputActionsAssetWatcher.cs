using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Sinkii09.Engine.Editor.InputSystem
{
    /// <summary>
    /// AssetPostprocessor that watches for changes to InputSystem_Actions files
    /// and automatically regenerates the delegate cache when needed
    /// </summary>
    public class InputActionsAssetWatcher : AssetPostprocessor
    {
        private const string InputActionsExtension = ".inputactions";
        private const string InputActionsScriptName = "InputSystem_Actions.cs";
        private const string DebouncePreferenceKey = "InputSystem.LastDebounceTime";
        private const double DebounceDelaySeconds = 1.0; // Wait 1 second for multiple changes to settle

        /// <summary>
        /// Called after assets are imported, deleted, or moved
        /// </summary>
        /// <param name="importedAssets">Paths of imported assets</param>
        /// <param name="deletedAssets">Paths of deleted assets</param>
        /// <param name="movedAssets">Paths of moved assets</param>
        /// <param name="movedFromAssetPaths">Previous paths of moved assets</param>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!InputSystemCodeGeneratorMenu.IsAutoGenerateEnabled())
            {
                return; // Auto-generation is disabled
            }

            bool shouldRegenerate = false;
            bool hasInputActionsChanges = false;
            bool hasScriptChanges = false;

            // Check for InputActions asset changes
            foreach (string assetPath in importedAssets)
            {
                if (IsInputActionsAsset(assetPath))
                {
                    hasInputActionsChanges = true;
                    shouldRegenerate = true;
                    Debug.Log($"[InputActionsAssetWatcher] Detected InputActions asset change: {assetPath}");
                }
                else if (IsInputActionsScript(assetPath))
                {
                    hasScriptChanges = true;
                    shouldRegenerate = true;
                    Debug.Log($"[InputActionsAssetWatcher] Detected InputActions script change: {assetPath}");
                }
            }

            // Check moved assets
            foreach (string assetPath in movedAssets)
            {
                if (IsInputActionsAsset(assetPath) || IsInputActionsScript(assetPath))
                {
                    shouldRegenerate = true;
                    Debug.Log($"[InputActionsAssetWatcher] Detected InputActions asset move: {assetPath}");
                }
            }

            // Check deleted assets (might need to clear generated code)
            foreach (string assetPath in deletedAssets)
            {
                if (IsInputActionsAsset(assetPath) || IsInputActionsScript(assetPath))
                {
                    Debug.LogWarning($"[InputActionsAssetWatcher] InputActions asset deleted: {assetPath}. Generated cache may be outdated.");
                    // Note: We don't auto-regenerate on deletion as there's nothing to regenerate from
                }
            }

            if (shouldRegenerate)
            {
                // Use debouncing to avoid multiple rapid regenerations
                ScheduleDebouncedRegeneration(hasInputActionsChanges, hasScriptChanges);
            }
        }

        /// <summary>
        /// Schedules a debounced regeneration to avoid multiple rapid calls
        /// </summary>
        private static void ScheduleDebouncedRegeneration(bool hasInputActionsChanges, bool hasScriptChanges)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            double lastDebounceTime = EditorPrefs.GetFloat(DebouncePreferenceKey, 0);

            // Update debounce time
            EditorPrefs.SetFloat(DebouncePreferenceKey, (float)currentTime);

            // If we're within the debounce window, schedule for later
            if (currentTime - lastDebounceTime < DebounceDelaySeconds)
            {
                Debug.Log($"[InputActionsAssetWatcher] Debouncing regeneration (waiting for changes to settle)...");
                
                // Schedule delayed execution
                EditorApplication.delayCall += () => {
                    DelayedRegeneration(hasInputActionsChanges, hasScriptChanges);
                };
            }
            else
            {
                // Execute immediately
                PerformRegeneration(hasInputActionsChanges, hasScriptChanges);
            }
        }

        /// <summary>
        /// Delayed regeneration with additional debounce check
        /// </summary>
        private static void DelayedRegeneration(bool hasInputActionsChanges, bool hasScriptChanges)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            double lastDebounceTime = EditorPrefs.GetFloat(DebouncePreferenceKey, 0);

            // Check if we're still within debounce window (more changes might have occurred)
            if (currentTime - lastDebounceTime < DebounceDelaySeconds)
            {
                // Schedule another delay
                EditorApplication.delayCall += () => {
                    DelayedRegeneration(hasInputActionsChanges, hasScriptChanges);
                };
                return;
            }

            // Execute regeneration
            PerformRegeneration(hasInputActionsChanges, hasScriptChanges);
        }

        /// <summary>
        /// Performs the actual regeneration
        /// </summary>
        private static void PerformRegeneration(bool hasInputActionsChanges, bool hasScriptChanges)
        {
            // Skip if Unity is compiling (can cause issues)
            if (EditorApplication.isCompiling)
            {
                Debug.Log("[InputActionsAssetWatcher] Skipping auto-generation during compilation...");
                
                // Try again after compilation
                EditorApplication.delayCall += () => {
                    if (!EditorApplication.isCompiling)
                    {
                        PerformRegeneration(hasInputActionsChanges, hasScriptChanges);
                    }
                };
                return;
            }

            Debug.Log("[InputActionsAssetWatcher] Starting auto-generation of input action cache...");

            try
            {
                bool success = InputSystemCodeGeneratorMenu.TryGenerateActionCache(false);
                
                if (success)
                {
                    string changeType = hasInputActionsChanges ? "InputActions asset" : "InputActions script";
                    Debug.Log($"[InputActionsAssetWatcher] Auto-generation completed successfully after {changeType} changes. ✓");
                    
                    // Show a brief notification in the console (not a dialog to avoid interrupting workflow)
                    Debug.Log("<color=green>[InputSystem] Action cache auto-regenerated</color>");
                }
                else
                {
                    Debug.LogWarning("[InputActionsAssetWatcher] Auto-generation failed. Try manual generation from the menu.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputActionsAssetWatcher] Auto-generation error: {ex.Message}");
                
                // For critical errors, show a dialog
                if (ex is System.IO.FileNotFoundException || ex is System.UnauthorizedAccessException)
                {
                    EditorUtility.DisplayDialog("Auto-Generation Error", 
                        $"Failed to auto-generate input action cache:\n\n{ex.Message}\n\n" +
                        $"You may need to regenerate manually from the Engine menu.",
                        "OK");
                }
            }
        }

        /// <summary>
        /// Checks if the asset path is an InputActions asset
        /// </summary>
        private static bool IsInputActionsAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && 
                   assetPath.EndsWith(InputActionsExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the asset path is the generated InputActions script
        /// </summary>
        private static bool IsInputActionsScript(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && 
                   Path.GetFileName(assetPath).Equals(InputActionsScriptName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Additional utility for manual control of the asset watcher
    /// </summary>
    public static class InputActionsAssetWatcherUtility
    {
        /// <summary>
        /// Forces immediate regeneration (bypasses debouncing)
        /// </summary>
        public static void ForceRegeneration()
        {
            Debug.Log("[InputActionsAssetWatcher] Force regeneration requested...");
            
            try
            {
                bool success = InputSystemCodeGeneratorMenu.TryGenerateActionCache(true);
                
                if (success)
                {
                    Debug.Log("[InputActionsAssetWatcher] Force regeneration completed successfully. ✓");
                }
                else
                {
                    Debug.LogError("[InputActionsAssetWatcher] Force regeneration failed.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputActionsAssetWatcher] Force regeneration error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the debounce timer (useful for testing)
        /// </summary>
        public static void ClearDebounceTimer()
        {
            EditorPrefs.DeleteKey("InputSystem.LastDebounceTime");
            Debug.Log("[InputActionsAssetWatcher] Debounce timer cleared.");
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Editor.Generators
{
    /// <summary>
    /// Auto-generates UIScreenType enum and UIScreenRegistry from UIScreenAssets
    /// </summary>
    [InitializeOnLoad]
    public static class UIEnumGenerator
    {
        private const string ENUM_FILE_PATH = "Assets/Engine/Runtime/Scripts/Core/Services/Implemented/UIService/Core/UIScreenType.cs";
        private const string REGISTRY_ASSET_PATH = "Assets/Engine/Runtime/Resources/Configs/UIScreenRegistry.asset";
        
        static UIEnumGenerator()
        {
            // Auto-regenerate when assets change
            EditorApplication.projectChanged += OnProjectChanged;
        }
        
        private static void OnProjectChanged()
        {
            // Debounce rapid changes
            EditorApplication.delayCall += () =>
            {
                if (HasUIScreenAssetsChanged())
                {
                    RegenerateAll();
                }
            };
        }
        
        [MenuItem("Engine/UI/Generate Screen Enums and Registry")]
        public static void RegenerateAll()
        {
            try
            {
                var screenAssets = FindAllScreenAssets();
                
                // Generate enum file
                GenerateEnumFile(screenAssets);
                
                // Generate or update registry asset
                GenerateRegistryAsset(screenAssets);
                
                Debug.Log($"Successfully generated UIScreenType enum and registry for {screenAssets.Length} screen assets");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to generate UI screen enum/registry: {ex.Message}");
            }
        }
        
        private static UIScreenAsset[] FindAllScreenAssets()
        {
            return AssetDatabase.FindAssets("t:UIScreenAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UIScreenAsset>)
                .Where(asset => asset != null)
                .OrderBy(asset => asset.Category)
                .ThenBy(asset => asset.name)
                .ToArray();
        }
        
        private static void GenerateEnumFile(UIScreenAsset[] assets)
        {
            var sb = new StringBuilder();
            
            // File header
            sb.AppendLine("// AUTO-GENERATED - DO NOT EDIT");
            sb.AppendLine("// Generated from UIScreenAssets");
            sb.AppendLine("// Use Engine/UI/Generate Screen Enums to regenerate");
            sb.AppendLine();
            sb.AppendLine("namespace Sinkii09.Engine.Services");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated enum representing all available UI screens");
            sb.AppendLine("    /// Values are automatically assigned based on screen categories");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public enum UIScreenType");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// No screen selected");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        None = 0,");
            sb.AppendLine();
            
            // Group by category and generate enum values
            var categoryGroups = assets.GroupBy(a => a.Category).OrderBy(g => (int)g.Key);
            
            foreach (var categoryGroup in categoryGroups)
            {
                var category = categoryGroup.Key;
                var categoryAssets = categoryGroup.OrderBy(a => a.name).ToArray();
                
                // Category comment
                sb.AppendLine($"        // {category} screens ({GetCategoryBaseValue(category)}-{GetCategoryMaxValue(category)})");
                
                var enumValue = GetCategoryBaseValue(category);
                foreach (var asset in categoryAssets)
                {
                    var enumName = SanitizeEnumName(asset.name);
                    sb.AppendLine($"        /// <summary>");
                    sb.AppendLine($"        /// {asset.DisplayName}");
                    sb.AppendLine($"        /// </summary>");
                    sb.AppendLine($"        {enumName} = {enumValue},");
                    sb.AppendLine();
                    enumValue++;
                }
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // Write file
            Directory.CreateDirectory(Path.GetDirectoryName(ENUM_FILE_PATH));
            File.WriteAllText(ENUM_FILE_PATH, sb.ToString());
            AssetDatabase.ImportAsset(ENUM_FILE_PATH);
        }
        
        private static void GenerateRegistryAsset(UIScreenAsset[] assets)
        {
            // Load or create registry asset
            var registry = AssetDatabase.LoadAssetAtPath<UIScreenRegistry>(REGISTRY_ASSET_PATH);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<UIScreenRegistry>();
                
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(REGISTRY_ASSET_PATH));
                AssetDatabase.CreateAsset(registry, REGISTRY_ASSET_PATH);
            }
            
            // Filter out assets with valid screen types (not None)
            var validAssets = assets
                .Where(asset => asset != null && asset.ScreenType != UIScreenType.None)
                .ToArray();
            
            // Update registry with assets directly - no mapping needed since assets have ScreenType
            registry.SetScreenAssets(validAssets);
            
            // Mark dirty and save
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Updated UIScreenRegistry with {validAssets.Length} screen assets");
        }
        
        private static string SanitizeEnumName(string assetName)
        {
            return assetName
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .Replace("Screen", "")
                .Replace("UI", "");
        }
        
        private static int GetCategoryBaseValue(UICategory category)
        {
            return category switch
            {
                UICategory.Core => 1,
                UICategory.Gameplay => 10,
                UICategory.Dialog => 20,
                UICategory.Menu => 30,
                UICategory.Popup => 40,
                UICategory.Debug => 50,
                UICategory.Custom => 1000,
                _ => 1
            };
        }
        
        private static int GetCategoryMaxValue(UICategory category)
        {
            return category switch
            {
                UICategory.Core => 9,
                UICategory.Gameplay => 19,
                UICategory.Dialog => 29,
                UICategory.Menu => 39,
                UICategory.Popup => 49,
                UICategory.Debug => 59,
                UICategory.Custom => 9999,
                _ => 9
            };
        }
        
        private static bool HasUIScreenAssetsChanged()
        {
            // Simple check - could be more sophisticated
            var currentAssets = FindAllScreenAssets();
            return currentAssets.Length > 0; // For now, always regenerate if assets exist
        }
    }
}
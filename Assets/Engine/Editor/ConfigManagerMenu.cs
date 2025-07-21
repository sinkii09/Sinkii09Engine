using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Unity menu items for easy engine configuration management with automatic service discovery
    /// </summary>
    public static class ConfigManagerMenu
    {
        private const string MENU_ROOT = "Engine/Configs/";
        private const string CONFIG_PATH = "Assets/Engine/Runtime/Resources/Configs/Services/";

        // Cache for discovered service configurations
        private static Dictionary<Type, Type> _serviceConfigCache;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(5);

        [MenuItem(MENU_ROOT + "Setup All Configs", priority = 1)]
        public static void SetupAllConfigs()
        {
            ConfigInstaller.EnsureDefaultConfigs();
            EditorUtility.DisplayDialog("Config Setup",
                "All engine configurations have been created or verified.", "OK");
        }

        [MenuItem(MENU_ROOT + "Recreate All Configs", priority = 2)]
        public static void RecreateAllConfigs()
        {
            if (EditorUtility.DisplayDialog("Recreate Configs",
                "This will delete all existing engine configurations and recreate them with latest defaults.\n\nAny custom settings will be lost!",
                "Recreate", "Cancel"))
            {
                ConfigInstaller.RecreateAllConfigs();
                EditorUtility.DisplayDialog("Config Recreation",
                    "All engine configurations have been recreated with latest defaults.", "OK");
            }
        }

        [MenuItem(MENU_ROOT + "Validate All Configs", priority = 3)]
        public static void ValidateAllConfigs()
        {
            bool isValid = ConfigInstaller.ValidateConfigurations();

            if (isValid)
            {
                EditorUtility.DisplayDialog("Config Validation",
                    "All engine configurations are valid!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Config Validation",
                    "Some configurations have validation errors. Check the console for details.", "OK");
            }
        }

        [MenuItem(MENU_ROOT + "Open Config Folder", priority = 20)]
        public static void OpenConfigFolder()
        {
            // Create folder if it doesn't exist
            if (!System.IO.Directory.Exists(CONFIG_PATH))
            {
                System.IO.Directory.CreateDirectory(CONFIG_PATH);
                AssetDatabase.Refresh();
            }

            // Select and reveal in Project window
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CONFIG_PATH);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        // Dynamic menu generation for all discovered service configurations
        [MenuItem(MENU_ROOT + "Create/Refresh Config Menu", priority = 99)]
        public static void RefreshConfigMenu()
        {
            RefreshServiceConfigCache();
            EditorUtility.DisplayDialog("Config Menu Refresh",
                $"Found {_serviceConfigCache.Count} service configurations. Unity will refresh the menu items.", "OK");
        }

        [MenuItem(MENU_ROOT + "Tools/Clear Config Cache", priority = 200)]
        public static void ClearConfigCache()
        {
            // This would work with a static config provider instance
            // For now, just show info dialog
            EditorUtility.DisplayDialog("Clear Config Cache",
                "Config cache clearing is handled automatically by the ConfigProvider during runtime.", "OK");
        }

        [MenuItem(MENU_ROOT + "Tools/Show Config Info", priority = 201)]
        public static void ShowConfigInfo()
        {
            var info = GetConfigurationInfo();
            EditorUtility.DisplayDialog("Configuration Info", info, "OK");
        }

        /// <summary>
        /// Creates a configuration asset of the specified type
        /// </summary>
        /// <typeparam name="T">Type of configuration to create</typeparam>
        private static void CreateConfigAsset<T>() where T : ServiceConfigurationBase
        {
            var fileName = typeof(T).Name;
            var fullPath = GetConfigPath<T>();

            // Check if already exists
            if (System.IO.File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Config Exists",
                    $"Configuration {fileName} already exists. Overwrite?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // Ensure directory exists
            if (!System.IO.Directory.Exists(CONFIG_PATH))
            {
                System.IO.Directory.CreateDirectory(CONFIG_PATH);
            }

            // Create the asset
            var config = ScriptableObject.CreateInstance<T>();
            if (config is ServiceConfigurationBase baseConfig)
            {
                baseConfig.ResetToDefaults();
            }

            AssetDatabase.CreateAsset(config, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select and ping the new asset
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"Created configuration: {fileName}", config);
        }

        /// <summary>
        /// Gets information about current engine configurations
        /// </summary>
        /// <returns>Information string</returns>
        private static string GetConfigurationInfo()
        {
            UpdateConfigurationInfo();
            return _lastConfigInfo;
        }

        /// <summary>
        /// Checks the status of a specific configuration
        /// </summary>
        /// <typeparam name="T">Type of configuration</typeparam>
        /// <param name="fileName">Configuration file name</param>
        /// <param name="info">StringBuilder to append status to</param>
        private static void CheckConfigStatus<T>(System.Text.StringBuilder info) where T : ServiceConfigurationBase
        {
            var fileName = typeof(T).Name;
            var fullPath = Path.Combine(CONFIG_PATH, fileName + ".asset");
            var exists = System.IO.File.Exists(fullPath);

            info.AppendLine($"• {fileName}: {(exists ? "✓ EXISTS" : "✗ MISSING")}");

            if (exists)
            {
                var config = AssetDatabase.LoadAssetAtPath<T>(fullPath);
                if (config != null)
                {
                    var isValid = config.Validate(out var errors);
                    info.AppendLine($"  Validation: {(isValid ? "✓ VALID" : "✗ INVALID")}");
                }
            }
        }

        private static string GetConfigPath<T>() where T : ServiceConfigurationBase
        {
            var fileName = typeof(T).Name;
            return Path.Combine(CONFIG_PATH, fileName + ".asset");
        }

        /// <summary>
        /// Discovers all service configurations using reflection
        /// </summary>
        private static void RefreshServiceConfigCache()
        {
            _serviceConfigCache = new Dictionary<Type, Type>();

            // Use ReflectionUtils to get all types like the engine does
            var allTypes = ReflectionUtils.ExportedDomainTypes;

            // Find all services with ServiceConfiguration attribute
            foreach (var serviceType in allTypes)
            {
                if (!typeof(IEngineService).IsAssignableFrom(serviceType) || serviceType.IsAbstract)
                    continue;

                var configAttr = serviceType.GetCustomAttribute<ServiceConfigurationAttribute>();
                if (configAttr?.ConfigurationType != null)
                {
                    _serviceConfigCache[configAttr.ConfigurationType] = serviceType;
                    Debug.Log($"[ConfigManagerMenu] Discovered: {serviceType.Name} -> {configAttr.ConfigurationType.Name}");
                }
            }

            _lastCacheUpdate = DateTime.Now;

            // Now generate the menu items dynamically
            GenerateDynamicMenuItems();
        }

        /// <summary>
        /// Gets all discovered service configurations
        /// </summary>
        private static Dictionary<Type, Type> GetServiceConfigurations()
        {
            if (_serviceConfigCache == null || DateTime.Now - _lastCacheUpdate > CacheTimeout)
            {
                RefreshServiceConfigCache();
            }
            return _serviceConfigCache;
        }

        /// <summary>
        /// Generates menu items dynamically based on discovered configurations
        /// </summary>
        private static void GenerateDynamicMenuItems()
        {
            // Note: Unity doesn't support truly dynamic MenuItem attributes,
            // so we'll use the InitializeOnLoad approach instead
            UpdateConfigurationInfo();
        }

        /// <summary>
        /// Updates configuration info to include all discovered configs
        /// </summary>
        private static void UpdateConfigurationInfo()
        {
            var configs = GetServiceConfigurations();
            var info = new System.Text.StringBuilder();

            info.AppendLine("=== Discovered Service Configurations ===");
            info.AppendLine($"Total Found: {configs.Count}");
            info.AppendLine();

            foreach (var kvp in configs.OrderBy(x => x.Key.Name))
            {
                var configType = kvp.Key;
                var serviceType = kvp.Value;
                var configPath = Path.Combine(CONFIG_PATH, configType.Name + ".asset");
                var exists = File.Exists(configPath);

                info.AppendLine($"• {configType.Name}");
                info.AppendLine($"  Service: {serviceType.Name}");
                info.AppendLine($"  Status: {(exists ? "✓ EXISTS" : "✗ MISSING")}");

                if (exists)
                {
                    var config = AssetDatabase.LoadAssetAtPath(configPath, configType) as ServiceConfigurationBase;
                    if (config != null)
                    {
                        var isValid = config.Validate(out var errors);
                        info.AppendLine($"  Valid: {(isValid ? "✓ YES" : "✗ NO")}");
                        if (!isValid && errors != null)
                        {
                            foreach (var error in errors)
                            {
                                info.AppendLine($"    - {error}");
                            }
                        }
                    }
                }
                info.AppendLine();
            }

            // Store for display
            _lastConfigInfo = info.ToString();
        }

        private static string _lastConfigInfo = "";

        /// <summary>
        /// Creates all missing configurations automatically
        /// </summary>
        [MenuItem(MENU_ROOT + "Create/All Missing Configs", priority = 98)]
        public static void CreateAllMissingConfigs()
        {
            var configs = GetServiceConfigurations();
            int created = 0;

            foreach (var configType in configs.Keys)
            {
                var configPath = Path.Combine(CONFIG_PATH, configType.Name + ".asset");
                if (!File.Exists(configPath))
                {
                    CreateConfigAssetByType(configType);
                    created++;
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Create Missing Configs",
                $"Created {created} missing configuration(s).", "OK");
        }

        /// <summary>
        /// Creates a configuration asset by type
        /// </summary>
        private static void CreateConfigAssetByType(Type configType)
        {
            if (!typeof(ServiceConfigurationBase).IsAssignableFrom(configType))
            {
                Debug.LogError($"Type {configType} is not a ServiceConfigurationBase");
                return;
            }

            var fileName = configType.Name;
            var fullPath = Path.Combine(CONFIG_PATH, fileName + ".asset");

            // Ensure directory exists
            if (!Directory.Exists(CONFIG_PATH))
            {
                Directory.CreateDirectory(CONFIG_PATH);
            }

            // Create the asset
            var config = ScriptableObject.CreateInstance(configType);
            if (config is ServiceConfigurationBase baseConfig)
            {
                baseConfig.ResetToDefaults();
            }

            AssetDatabase.CreateAsset(config, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created configuration: {fileName}", config);
        }

        /// <summary>
        /// Shows enhanced configuration info
        /// </summary>
        [MenuItem(MENU_ROOT + "Tools/Show Enhanced Config Info", priority = 202)]
        public static void ShowEnhancedConfigInfo()
        {
            UpdateConfigurationInfo();

            // Show in a scrollable window since it might be long
            var window = EditorWindow.GetWindow<ConfigInfoWindow>("Config Info");
            window.SetContent(_lastConfigInfo);
            window.Show();
        }
    }

    /// <summary>
    /// Window to display configuration information
    /// </summary>
    public class ConfigInfoWindow : EditorWindow
    {
        private string _content = "";
        private Vector2 _scrollPosition;

        public void SetContent(string content)
        {
            _content = content;
        }

        void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.TextArea(_content, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Refresh"))
            {
                ConfigManagerMenu.ShowEnhancedConfigInfo();
            }
        }
    }
}
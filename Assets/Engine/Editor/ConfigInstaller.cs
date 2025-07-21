using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Automatically creates default engine configurations when missing
    /// Ensures new projects have all required configs without manual setup
    /// Uses reflection to automatically discover all service configurations
    /// </summary>
    public static class ConfigInstaller
    {
        private const string CONFIG_PATH = "Assets/Engine/Runtime/Resources/Configs/Services/";
        private const string CONFIG_RESOURCES_PATH = "Configs/Services/";
        /// <summary>
        /// Automatically called when Unity loads - ensures all default configs exist
        /// </summary>
        [InitializeOnLoadMethod]
        public static void EnsureDefaultConfigs()
        {
            // Only run in editor, not during builds
            if (Application.isBatchMode) return;

            // Ensure the config directory exists
            EnsureDirectoryExists(CONFIG_PATH);

            // Discover and create all required service configurations
            var configTypes = DiscoverServiceConfigurations();

            foreach (var configType in configTypes)
            {
                EnsureConfigExistsByType(configType);
            }

            // Refresh the AssetDatabase to show new configs
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Discovers all service configuration types through reflection
        /// </summary>
        /// <returns>Array of configuration types</returns>
        private static Type[] DiscoverServiceConfigurations()
        {
            try
            {
                var allTypes = ReflectionUtils.ExportedDomainTypes;
                var configTypes = new System.Collections.Generic.List<Type>();

                // Find all services with ServiceConfiguration attribute
                foreach (var serviceType in allTypes)
                {
                    if (!typeof(IEngineService).IsAssignableFrom(serviceType) || serviceType.IsAbstract)
                        continue;

                    var configAttr = serviceType.GetCustomAttribute<ServiceConfigurationAttribute>();
                    if (configAttr?.ConfigurationType != null &&
                        typeof(ServiceConfigurationBase).IsAssignableFrom(configAttr.ConfigurationType))
                    {
                        configTypes.Add(configAttr.ConfigurationType);
                        Debug.Log($"[ConfigInstaller] Discovered config: {configAttr.ConfigurationType.Name} for service {serviceType.Name}");
                    }
                }

                return configTypes.Distinct().ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigInstaller] Failed to discover service configurations: {ex.Message}");
                // Fallback to known configurations
                return new Type[]
                {
                    typeof(ActorServiceConfiguration),
                    typeof(ScriptServiceConfiguration),
                    typeof(SaveLoadServiceConfiguration),
                    typeof(AutoSaveServiceConfiguration)
                };
            }
        }

        /// <summary>
        /// Creates a specific config if it doesn't exist (generic version)
        /// </summary>
        /// <typeparam name="T">Type of configuration to create</typeparam>
        private static void EnsureConfigExists<T>() where T : ServiceConfigurationBase
        {
            EnsureConfigExistsByType(typeof(T));
        }

        /// <summary>
        /// Creates a specific config if it doesn't exist (type-based version)
        /// </summary>
        /// <param name="configType">Type of configuration to create</param>
        private static void EnsureConfigExistsByType(Type configType)
        {
            if (!typeof(ServiceConfigurationBase).IsAssignableFrom(configType))
            {
                Debug.LogError($"[ConfigInstaller] Type {configType} is not a ServiceConfigurationBase");
                return;
            }

            var fileName = configType.Name;
            var fullPath = CONFIG_PATH + fileName + ".asset";

            // Check if config already exists
            if (File.Exists(fullPath))
                return;

            try
            {
                // Create the config instance
                var config = ScriptableObject.CreateInstance(configType);

                // Set default values (calls OnResetToDefaults internally)
                if (config is ServiceConfigurationBase baseConfig)
                {
                    baseConfig.ResetToDefaults();
                }

                // Create the asset
                AssetDatabase.CreateAsset(config, fullPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[ConfigInstaller] Created default configuration: {fileName}", config);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigInstaller] Failed to create config {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the config directory exists
        /// </summary>
        /// <param name="path">Directory path to create</param>
        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[ConfigInstaller] Created config directory: {path}");
            }
        }

        /// <summary>
        /// Recreates all engine configurations (useful for updates)
        /// </summary>
        public static void RecreateAllConfigs()
        {
            // Remove existing configs
            if (Directory.Exists(CONFIG_PATH))
            {
                var configFiles = Directory.GetFiles(CONFIG_PATH, "*.asset");
                foreach (var file in configFiles)
                {
                    AssetDatabase.DeleteAsset(file);
                }
            }

            // Recreate with latest defaults
            EnsureDefaultConfigs();

            var configTypes = DiscoverServiceConfigurations();
            Debug.Log($"[ConfigInstaller] Recreated {configTypes.Length} engine configurations with latest defaults");
        }

        /// <summary>
        /// Validates all existing configurations
        /// </summary>
        /// <returns>True if all configurations are valid</returns>
        public static bool ValidateConfigurations()
        {
            bool allValid = true;
            var configTypes = DiscoverServiceConfigurations();

            foreach (var configType in configTypes)
            {
                allValid &= ValidateConfigByType(configType);
            }

            if (allValid)
            {
                Debug.Log($"[ConfigInstaller] All {configTypes.Length} configurations are valid");
            }
            else
            {
                Debug.LogWarning("[ConfigInstaller] Some configurations have validation errors");
            }

            return allValid;
        }

        /// <summary>
        /// Validates a specific configuration (generic version)
        /// </summary>
        /// <typeparam name="T">Type of configuration to validate</typeparam>
        /// <returns>True if configuration is valid</returns>
        private static bool ValidateConfig<T>() where T : ServiceConfigurationBase
        {
            return ValidateConfigByType(typeof(T));
        }

        /// <summary>
        /// Validates a specific configuration (type-based version)
        /// </summary>
        /// <param name="configType">Type of configuration to validate</param>
        /// <returns>True if configuration is valid</returns>
        private static bool ValidateConfigByType(Type configType)
        {
            var fileName = configType.Name;
            var filePath = Path.Combine(CONFIG_RESOURCES_PATH, fileName);
            var config = Resources.Load(filePath, configType) as ServiceConfigurationBase;
            if (config == null)
            {
                Debug.LogError($"[ConfigInstaller] Configuration not found: {fileName}");
                return false;
            }

            var isValid = config.Validate(out var errors);
            if (!isValid)
            {
                Debug.LogError($"[ConfigInstaller] Configuration validation failed: {fileName} with errors: \n {string.Join('\n', errors)}", config);
            }

            return isValid;
        }
    }
}
using Sinkii09.Engine.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZLinq;
using Sinkii09.Engine.Services;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sinkii09.Engine.Configs
{
    public interface IConfigProvider
    {
        Configuration GetConfiguration(Type type);
        T GetConfiguration<T>() where T : Configuration;
        void ClearCache();
        T ReloadConfiguration<T>() where T : Configuration;
    }
    
    public class ConfigProvider : IConfigProvider
    {
        private const string DEFAULT_CONFIG_PATH = "Configs";

        private readonly Dictionary<Type, Configuration> _configs = new Dictionary<Type, Configuration>();
        private readonly string _resourcePath;

        public ConfigProvider(string resourcePath = DEFAULT_CONFIG_PATH)
        {
            _resourcePath = resourcePath;
            //LoadAllConfigurations();
        }

        private void LoadAllConfigurations()
        {
            var baseConfigType = typeof(Configuration);

            var configTypes = ReflectionUtils.ExportedDomainTypes.AsValueEnumerable()
                .Where(type => type.IsSubclassOf(baseConfigType) && !type.IsAbstract);

            foreach (var configType in configTypes)
            {
                var config = LoadWithFallback(configType);
                if (config != null)
                {
                    var instance = UnityEngine.Object.Instantiate(config);
                    _configs.Add(configType, instance);
                }
            }
        }

        public Configuration GetConfiguration(Type type)
        {
            if (_configs.TryGetValue(type, out var config))
            {
                return config;
            }

            // Try to load on-demand with fallback
            var loadedConfig = LoadWithFallback(type);
            if (loadedConfig != null)
            {
                var instance = UnityEngine.Object.Instantiate(loadedConfig);
                _configs.Add(type, instance);
                return instance;
            }

            Debug.LogError($"Configuration for type {type.Name} not found anywhere.");
            return null;
        }

        public T GetConfiguration<T>() where T : Configuration
        {
            return GetConfiguration(typeof(T)) as T;
        }

        public void ClearCache()
        {
            _configs.Clear();
            Debug.Log("[ConfigProvider] Configuration cache cleared");
        }

        public T ReloadConfiguration<T>() where T : Configuration
        {
            var type = typeof(T);
            _configs.Remove(type);
            return GetConfiguration<T>();
        }

        /// <summary>
        /// Enhanced loading with fallback to package defaults
        /// </summary>
        private Configuration LoadWithFallback(Type type)
        {
            // Step 1: Try project Resources first
            var config = LoadFromResources(type, DEFAULT_CONFIG_PATH);
            if (IsValid(config))
            {
                Debug.Log($"[ConfigProvider] Loaded project service config: {type.Name}");
                return config;
            }

            // Step 2: Try general config path
            config = LoadFromResources(type, _resourcePath);
            if (IsValid(config))
            {
                Debug.Log($"[ConfigProvider] Loaded project config: {type.Name}");
                return config;
            }

            // Step 3: Try loading from package
            config = LoadFromPackage(type);
            if (IsValid(config))
            {
                Debug.LogWarning($"[ConfigProvider] Using package default for: {type.Name} (consider creating project-specific config)");
                return config;
            }

            // Step 4: Create runtime default
            config = CreateRuntimeDefault(type);
            Debug.LogError($"[ConfigProvider] Created runtime default for: {type.Name} (no config found anywhere!)");
            
            return config;
        }

        private Configuration LoadFromResources(Type type, string path)
        {
            var resourcePath = $"{path}/{type.Name}";
            return Resources.Load(resourcePath, type) as Configuration;
        }

        private Configuration LoadFromPackage(Type type)
        {
#if UNITY_EDITOR
            var typeName = type.Name;
            var guids = AssetDatabase.FindAssets($"t:{typeName}");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Check if this asset is in our package
                if (path.StartsWith("Packages/com.sinkii09.engine"))
                {
                    var config = AssetDatabase.LoadAssetAtPath(path, type) as Configuration;
                    if (IsValid(config))
                    {
                        return config;
                    }
                }
            }
#endif
            return null;
        }

        private Configuration CreateRuntimeDefault(Type type)
        {
            var config = ScriptableObject.CreateInstance(type) as Configuration;
            
            return config;
        }

        public static Configuration Load(Type type, string path = DEFAULT_CONFIG_PATH)
        {
            var resourcePath = $"{path}/{type.Name}";
            var config = UnityEngine.Resources.Load(resourcePath, type) as Configuration;

            if (!IsValid(config))
            {
                Debug.LogWarning($"Configuration {type.Name} not found at path {resourcePath}. Creating a new instance instead.");
                config = ScriptableObject.CreateInstance(type) as Configuration;
            }

            return config;
        }

        public static bool IsValid(object obj)
        {
            if (obj is UnityEngine.Object unityObject)
                return unityObject != null && unityObject;
            else return false;
        }
    }
}


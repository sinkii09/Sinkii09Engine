using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Configs
{
    public interface IConfigProvider
    {
        Configuration GetConfiguration(Type type);
    }
    public class ConfigProvider : IConfigProvider
    {
        private const string DEFAULT_CONFIG_PATH = "Configs";

        private readonly Dictionary<Type, Configuration> _configs = new Dictionary<Type, Configuration>();

        public ConfigProvider(string resourcePath = DEFAULT_CONFIG_PATH)
        {
            var baseConfigType = typeof(Configuration);
            //TODO: Use reflection to find all subclasses of Configuration in the current domain
            var configTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(baseConfigType) && !type.IsAbstract);

            foreach (var configType in configTypes)
            {
                var config = Load(configType, resourcePath);
                var instance = UnityEngine.Object.Instantiate(config);
                _configs.Add(configType,instance);
            }
        }
        public Configuration GetConfiguration(Type type)
        {
            if (_configs.TryGetValue(type, out var config))
            {
                return config;
            }

            Debug.LogError($"Configuration for type {type.Name} not found.");
            return null;
        }

        public static Configuration Load(Type type, string path = DEFAULT_CONFIG_PATH)
        {
            var resourcePath = $"{path}/{type.Name}";
            var config = UnityEngine.Resources.Load(resourcePath, type) as Configuration;

            if (!IsValid(config))
            {
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


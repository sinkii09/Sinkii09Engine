using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Registry for automatic discovery and management of storage providers
    /// </summary>
    public class StorageProviderRegistry
    {
        private readonly Dictionary<StorageProviderType, Type> _providerTypes;
        private readonly Dictionary<StorageProviderType, StorageProviderAttribute> _providerAttributes;
        private static StorageProviderRegistry _instance;

        public static StorageProviderRegistry Instance => _instance ??= new StorageProviderRegistry();

        private StorageProviderRegistry()
        {
            _providerTypes = new Dictionary<StorageProviderType, Type>();
            _providerAttributes = new Dictionary<StorageProviderType, StorageProviderAttribute>();
            DiscoverProviders();
        }

        /// <summary>
        /// Discovers all storage providers marked with StorageProviderAttribute
        /// </summary>
        private void DiscoverProviders()
        {
            var providerInterface = typeof(IStorageProvider);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && providerInterface.IsAssignableFrom(t));

                    foreach (var type in types)
                    {
                        var attribute = type.GetCustomAttribute<StorageProviderAttribute>();
                        if (attribute != null)
                        {
                            _providerTypes[attribute.ProviderType] = type;
                            _providerAttributes[attribute.ProviderType] = attribute;
                            Debug.Log($"Discovered storage provider: {type.Name} for {attribute.ProviderType}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to scan assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a storage provider instance for the specified type
        /// </summary>
        public IStorageProvider CreateProvider(StorageProviderType providerType)
        {
            if (!_providerTypes.TryGetValue(providerType, out var type))
            {
                throw new NotSupportedException($"No storage provider registered for type: {providerType}");
            }

            try
            {
                return (IStorageProvider)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create storage provider of type {providerType}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a provider type is supported on the current platform
        /// </summary>
        public bool IsProviderSupported(StorageProviderType providerType)
        {
            if (!_providerAttributes.TryGetValue(providerType, out var attribute))
                return false;

            var supportedPlatforms = attribute.SupportedPlatforms;
            var currentPlatform = GetCurrentPlatform();
            
            return supportedPlatforms.HasFlag(currentPlatform);
        }
        
        /// <summary>
        /// Gets the current platform as a SupportedPlatform enum value
        /// </summary>
        private SupportedPlatform GetCurrentPlatform()
        {
#if UNITY_EDITOR
            return SupportedPlatform.Editor;
#elif UNITY_STANDALONE_WIN
            return SupportedPlatform.Windows;
#elif UNITY_STANDALONE_OSX
            return SupportedPlatform.Mac;
#elif UNITY_STANDALONE_LINUX
            return SupportedPlatform.Linux;
#elif UNITY_IOS
            return SupportedPlatform.iOS;
#elif UNITY_ANDROID
            return SupportedPlatform.Android;
#elif UNITY_WEBGL
            return SupportedPlatform.WebGL;
#elif UNITY_PS4 || UNITY_PS5
            return SupportedPlatform.PlayStation;
#elif UNITY_XBOXONE || UNITY_GAMECORE_XBOXSERIES || UNITY_GAMECORE_XBOXONE
            return SupportedPlatform.Xbox;
#elif UNITY_SWITCH
            return SupportedPlatform.Switch;
#else
            return SupportedPlatform.None;
#endif
        }

        /// <summary>
        /// Gets all available provider types for the current platform
        /// </summary>
        public IEnumerable<StorageProviderType> GetAvailableProviders()
        {
            return _providerTypes.Keys.Where(IsProviderSupported);
        }

        /// <summary>
        /// Gets provider metadata
        /// </summary>
        public StorageProviderAttribute GetProviderAttribute(StorageProviderType providerType)
        {
            return _providerAttributes.TryGetValue(providerType, out var attribute) ? attribute : null;
        }
    }
}
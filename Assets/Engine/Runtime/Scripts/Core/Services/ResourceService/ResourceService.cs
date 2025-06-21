using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    public interface IResourceService : IService
    {
        bool IsInitialized(ProviderType providerType);
        IResourceProvider GetProvider(ProviderType providerType);
        List<IResourceProvider> GetProviders(ProviderType providerTypes);
    }
    public class ResourceService : IResourceService
    {
        private readonly Dictionary<ProviderType, IResourceProvider> _providers = new Dictionary<ProviderType, IResourceProvider>();
        public UniTask<bool> Initialize()
        {
            Application.lowMemory += UnloadAllResourcesAsync;
            return UniTask.FromResult(true);
        }

        public bool IsInitialized(ProviderType providerType)
        {
            return _providers.ContainsKey(providerType) && _providers[providerType] != null;
        }
        public IResourceProvider GetProvider(ProviderType providerType)
        {
            if (!_providers.ContainsKey(providerType))
            {
                _providers[providerType] = InitializeProvider(providerType);
            }
            Debug.Log($"Getting provider of type {providerType}.");
            return _providers[providerType];
        }

        public List<IResourceProvider> GetProviders(ProviderType providerTypes)
        {
            List<IResourceProvider> providers = new List<IResourceProvider>();

            foreach (ProviderType type in Enum.GetValues(typeof(ProviderType)))
            {
                if (type == ProviderType.None) continue;
                if ((providerTypes & type) != 0)
                {
                    var provider = GetProvider(type);
                    if (provider != null)
                        providers.Add(provider);
                }
            }
            Debug.Log($"Retrieved {providers.Count} providers for types: {string.Join(", ", providerTypes)}.");
            return providers;
        }

        private IResourceProvider InitializeProvider(ProviderType providerType)
        {
            IResourceProvider provider = null;
            switch (providerType)
            {
                case ProviderType.AssetBundle:
                    // Initialize AssetBundle provider
                    break;
                case ProviderType.Resources:
                    provider = new ProjectResourceProvider();
                    break;
                case ProviderType.Local:
                    // Initialize Local provider
                    break;
                default:
                    Debug.LogError($"Unknown provider type: {providerType}");
                    break;
            }

            return provider;
        }
        public void Reset()
        {

        }

        public void Terminate()
        {
            Application.lowMemory -= UnloadAllResourcesAsync;
            foreach (var provider in _providers.Values)
            {
                provider?.UnloadResources();
            }
            _providers.Clear();
        }
        private async void UnloadAllResourcesAsync()
        {
            await UnityEngine.Resources.UnloadUnusedAssets();
        }
    }
}
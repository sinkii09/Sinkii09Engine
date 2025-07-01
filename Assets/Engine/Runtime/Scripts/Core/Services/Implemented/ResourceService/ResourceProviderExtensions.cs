using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Sinkii09.Engine.Common.Resources;

namespace Sinkii09.Engine.Services
{
    public static class ResourceProviderExtensions
    {
        public static async UniTask<Resource<T>> LoadResourceAsync<T>(this List<IResourceProvider> providers, string path) where T : UnityEngine.Object
        {
            if (providers.Count == 1)
                return await providers[0].LoadResourceAsync<T>(path);
            else
            {
                foreach (var provider in providers)
                {
                    if (!await provider.ResourceExistsAsync<T>(path)) continue;
                    return await provider.LoadResourceAsync<T>(path);
                }
            }
            return new Resource<T>(path, null, null);
        }

        public static bool ResourceLoaded(this List<IResourceProvider> providers, string path)
        {
            foreach (var provider in providers)
                if (provider.ResourceLoaded(path)) return true;
            return false;
        }

        public static Resource<T> GetLoadedResourceOrNull<T>(this List<IResourceProvider> providers, string path) where T : UnityEngine.Object
        {
            foreach (var provider in providers)
                if (provider.ResourceLoaded(path))
                    return provider.GetLoadedResourceOrNull<T>(path);
            return null;
        }
    }
}
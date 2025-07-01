using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Sinkii09.Engine.Common.Resources;

namespace Sinkii09.Engine.Services
{
    public abstract class ResourceLoader
    {
        public string PathPrefix { get; }

        protected List<IResourceProvider> Providers { get; }

        protected ResourceLoader(IList<IResourceProvider> providers, string pathPrefix = null)
        {
            Providers = new();
            Providers.AddRange(providers);
            PathPrefix = pathPrefix;
        }

        public string BuildFullPath(string localPath)
        {
            if (!string.IsNullOrWhiteSpace(PathPrefix))
            {
                if (!string.IsNullOrWhiteSpace(localPath)) return $"{PathPrefix}/{localPath}";
                else return PathPrefix;
            }
            else return localPath;
        }

        public abstract bool IsLoaded(string path, bool isFullPath = false);
        public abstract void Unload(string path, bool isFullPath = false);
        public abstract void UnloadAll();
    }
    public class ResourceLoader<T> : ResourceLoader where T : Object
    {
        protected readonly List<Resource<T>> _loadedResources = new List<Resource<T>>();

        public ResourceLoader(IList<IResourceProvider> providers, string pathPrefix = null) : base(providers, pathPrefix)
        {
        }
        public override bool IsLoaded(string path, bool isFullPath = false)
        {
            if (!isFullPath) path = BuildFullPath(path);
            return Providers.ResourceLoaded(path);
        }

        public async UniTask<Resource<T>> LoadAsync(string path, bool isFullPath = false)
        {
            if (!isFullPath)
            {
                path = BuildFullPath(path);
            }

            Resource<T> resource = await Providers.LoadResourceAsync<T>(path);

            if (resource != null && resource.IsValid)
            {
                _loadedResources.Add(resource);
            }

            return resource;
        }
        public virtual Resource<T> GetLoadedOrNull(string path, bool isFullPath = false)
        {
            if (!isFullPath) path = BuildFullPath(path);
            return Providers.GetLoadedResourceOrNull<T>(path);
        }
        public override void Unload(string path, bool isFullPath = false)
        {

        }

        public override void UnloadAll()
        {
        }
    }
}
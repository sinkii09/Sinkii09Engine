using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZLinq;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Common.Resources;
using Sinkii09.Engine.Common;

namespace Sinkii09.Engine.Services
{
    public interface IResourceProvider
    {
        UniTask<Resource<T>> LoadResourceAsync<T>(string path) where T : UnityEngine.Object;
        UniTask<IEnumerable<Resource<T>>> LoadResourcesAsync<T>(string path) where T : UnityEngine.Object;
        UniTask<IEnumerable<string>> LocateResourcesAsync<T> (string path) where T : UnityEngine.Object;
        //UniTask<IEnumerable<Folder>> LocateFoldersAsync (string path);
        UniTask<bool> ResourceExistsAsync<T>(string path) where T : UnityEngine.Object;
        void UnloadResource(string path);
        void UnloadResources();
        bool ResourceLoaded(string path);
        bool ResourceLoading(string path);
        Resource<T> GetLoadedResourceOrNull<T>(string path) where T : UnityEngine.Object;

    }
    public abstract class ResourceProvider : IResourceProvider
    {
        private event Action<string> OnMessage;

        protected Dictionary<string, Resource> _loadedResources = new Dictionary<string, Resource>();
        protected Dictionary<string, List<Folder>> _loadedFolders = new Dictionary<string, List<Folder>>();
        protected Dictionary<string, ResourceRunner> _loadRunners = new Dictionary<string, ResourceRunner>();
        protected Dictionary<Tuple<string, Type>, ResourceRunner> _locators = new Dictionary<Tuple<string, Type>, ResourceRunner>();

        public abstract bool SupportsType<T>() where T : UnityEngine.Object;

        public Resource<T> GetLoadedResourceOrNull<T>(string path) where T : UnityEngine.Object
        {
            if (!SupportsType<T>() || !ResourceLoaded(path)) return null;

            Type loadedType = _loadedResources[path].Asset?.GetType();
            if (loadedType != typeof(T))
            {
                Debug.LogError($"Failed to get a loaded resource with path `{path}`: the loaded resource is of type `{loadedType.FullName}`, while the requested type is `{typeof(T).FullName}`.");
                return null;
            }

            return _loadedResources[path] as Resource<T>;
        }

        public virtual async UniTask<Resource<T>> LoadResourceAsync<T>(string path) where T : UnityEngine.Object
        {
            if (!SupportsType<T>()) return null;

            if(ResourceLoading(path))
            {
                if(_loadRunners[path].ResourceType != typeof(T))
                {
                    UnloadResource(path);
                }
                else
                {
                    return await (_loadRunners[path] as LoadResourceRunner<T>);
                }
            }
            if (ResourceLoaded(path))
            {
                if (_loadedResources[path].Asset?.GetType() != typeof(T)) UnloadResource(path);
                else return _loadedResources[path] as Resource<T>;
            }

            var runner = CreateLoadResourceRunner<T>(path);
            _loadRunners[path] = runner;
            UpdateLoadProgress();
            RunResourceLoader(runner);
            var resource = await runner;
            HandleResourceLoaded(path, resource);
            return resource;
        }

        public virtual async UniTask<IEnumerable<Resource<T>>> LoadResourcesAsync<T>(string path) where T : UnityEngine.Object
        {
            if (!SupportsType<T>()) return null;
            var locatedResourcePaths = await LocateResourcesAsync<T>(path);
            return await UniTask.WhenAll(locatedResourcePaths.AsEnumerable().Select(p => LoadResourceAsync<T>(p)));
        }

        public virtual async UniTask<IEnumerable<string>> LocateResourcesAsync<T>(string path) where T : UnityEngine.Object
        {
            if (!SupportsType<T>()) return null;

            if (path is null) path = string.Empty;

            var locateKey = new Tuple<string, Type>(path, typeof(T));

            if (ResourceLocating<T>(path))
                return await(_locators[locateKey] as LocateResourceRunner<T>);

            var locateRunner = CreateLocateResourceRunner<T>(path);
            _locators.Add(locateKey, locateRunner);
            UpdateLoadProgress();

            RunResourcesLocator(locateRunner);

            var locatedResourcePaths = await locateRunner;
            HandleResourcesLocated<T>(locatedResourcePaths, path);
            return locatedResourcePaths;
        }
        public virtual async UniTask<IEnumerable<Folder>> LocateFoldersAsync(string path)
        {
            if (path is null) path = string.Empty;

            if (_loadedFolders.ContainsKey(path)) return _loadedFolders[path];

            var locateKey = new Tuple<string, Type>(path, typeof(Folder));

            if (ResourceLocating<Folder>(path))
                return await (_locators[locateKey] as LocateFolderRunner);

            var locateRunner = CreateLocateFolderRunner(path);
            _locators.Add(locateKey, locateRunner);
            UpdateLoadProgress();

            RunFoldersLocator(locateRunner);

            var locatedFolders = await locateRunner;
            HandleFoldersLocated(locatedFolders, path);
            return locatedFolders;
        }
        public virtual async UniTask<bool> ResourceExistsAsync<T>(string path) where T : UnityEngine.Object
        {
            if (!SupportsType<T>()) return false;
            if (ResourceLoaded<T>(path)) return true;
            var folderPath = path.Contains("/") ? path.GetBeforeLast("/") : string.Empty;
            var locatedResourcePaths = await LocateResourcesAsync<T>(folderPath);
            return locatedResourcePaths.AsEnumerable().Any(p => p.EqualsFast(path));
        }

        public bool ResourceLoaded(string path)
        {
            return _loadedResources.ContainsKey(path);
        }

        public bool ResourceLoading(string path)
        {
            return _loadRunners.ContainsKey(path);
        }
        public virtual bool ResourceLocating<T>(string path)
        {
            return _locators.ContainsKey(new Tuple<string, Type>(path, typeof(T)));
        }

        public void UnloadResource(string path)
        {
            if (ResourceLoading(path))
            {
                CancelResourceLoading(path);
            }
            if (!ResourceLoaded(path)) return;

            if (_loadedResources.TryGetValue(path, out var resource))
            {
                _loadedResources.Remove(path);
                DisposeResource(resource);
            }
        }
        protected abstract LoadResourceRunner<T> CreateLoadResourceRunner<T>(string path) where T : UnityEngine.Object;
        protected abstract LocateResourceRunner<T> CreateLocateResourceRunner<T>(string path) where T : UnityEngine.Object;
        protected abstract LocateFolderRunner CreateLocateFolderRunner(string path);
        protected abstract void DisposeResource(Resource resource);


        protected virtual void RunResourceLoader<T>(LoadResourceRunner<T> loader) where T : UnityEngine.Object => loader.RunAsync().Forget();
        protected virtual void RunResourcesLocator<T>(LocateResourceRunner<T> locator) where T : UnityEngine.Object => locator.RunAsync().Forget();
        protected virtual void RunFoldersLocator(LocateFolderRunner locator) => locator.RunAsync().Forget();

        protected virtual void CancelResourceLoading(string path)
        {
            Debug.Log("Canceling resource loading for path: " + path);
            if (!ResourceLoading(path)) return;


            _loadRunners[path].Cancel();
            _loadRunners.Remove(path);

            UpdateLoadProgress();
        }
        protected virtual void CancelResourceLocating<T>(string path)
        {
            if (!ResourceLocating<T>(path)) return;

            var locateKey = new Tuple<string, Type>(path, typeof(T));

            _locators[locateKey].Cancel();
            _locators.Remove(locateKey);

            UpdateLoadProgress();
        }
        public virtual void UnloadResources()
        {
            List<string> paths = _loadedResources.Values.AsValueEnumerable().Select(r => r.Path).ToList();
            foreach (var path in paths)
            {
                UnloadResource(path);
            }
        }
        protected virtual bool ResourceLoaded<T>(string path) where T : UnityEngine.Object
        {
            return ResourceLoaded(path) && _loadedResources[path].Asset.GetType() == typeof(T);
        }

        protected virtual void HandleResourceLoaded<T>(string path, Resource<T> resource) where T : UnityEngine.Object
        {
            if (!resource.IsValid) Debug.LogError($"Resource '{resource.Path}' failed to load.");
            else
                _loadedResources[path] = resource;
            if (_loadRunners.ContainsKey(path))
            {
                _loadRunners.Remove(path);
            }
            UpdateLoadProgress();
        }

        protected virtual void HandleResourcesLocated<T>(IEnumerable<string> locatedResourcePaths, string path) where T : UnityEngine.Object
        {
            var locateKey = new Tuple<string, Type>(path, typeof(T));
            _locators.Remove(locateKey);

            UpdateLoadProgress();
        }
        protected virtual void HandleFoldersLocated(IEnumerable<Folder> locatedFolders, string path)
        {
            var locateKey = new Tuple<string, Type>(path, typeof(Folder));
            _locators.Remove(locateKey);

            _loadedFolders[path] = locatedFolders.ToList();

            UpdateLoadProgress();
        }
        protected virtual void UpdateLoadProgress()
        {
            
        }
        public void LogMessage(string message) => OnMessage?.Invoke(message);
    }
}
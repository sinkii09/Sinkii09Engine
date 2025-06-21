using Sinkii09.Engine.Common.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    public class ProjectResourceProvider : ResourceProvider
    {
        public readonly string RootPath;
        private ProjectResources _projectResources;
        private Dictionary<Type, TypeRedirector> _redirectors;

        public ProjectResourceProvider(string rootPath = null)
        {
            RootPath = rootPath;
            _projectResources = ProjectResources.Get();
            _redirectors = new();
        }

        public override bool SupportsType<T>() => true;

        protected override LoadResourceRunner<T> CreateLoadResourceRunner<T>(string path)
        {
            return new ProjectLoadResourceRunner<T>(this, path, RootPath, _redirectors.ContainsKey(typeof(T)) ? _redirectors[typeof(T)] : null, LogMessage);
        }

        protected override LocateFolderRunner CreateLocateFolderRunner(string path)
        {
            return new ProjectFolderLocator(this, path, RootPath, _projectResources);
        }

        protected override LocateResourceRunner<T> CreateLocateResourceRunner<T>(string path)
        {
            return new ProjectResourceLocator<T>(this, path, RootPath, _projectResources);
        }

        protected override void DisposeResource(Resource resource)
        {
            Debug.Log("Disposing resource: " + resource.Path);
            if (!resource.IsValid) return;

            if (_redirectors.Count > 0 && _redirectors.ContainsKey(resource.Asset.GetType()))
            {
                UnityEngine.Object.Destroy(resource.Asset);
                return;
            }

            UnityEngine.Resources.UnloadAsset(resource.Asset);
        }
    }
}
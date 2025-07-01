using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Resources;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    public class TypeRedirector
    {
        public Type SourceType { get; private set; }
        public Type RedirectType { get; private set; }

        internal Task<T> ToSourceAsync<T>(UnityEngine.Object asset, string assetName)
        {
            throw new NotImplementedException();
        }
    }
    public class ProjectLoadResourceRunner<T> : LoadResourceRunner<T> where T : UnityEngine.Object
    {
        public readonly string RootPath;

        private readonly Action<string> _logAction;
        private readonly TypeRedirector _redirector;
        public ProjectLoadResourceRunner(IResourceProvider provider, string path, string rootPath, TypeRedirector redirector, Action<string> logAction) : base(provider, path)
        {
            RootPath = rootPath;
            _redirector = redirector;
            _logAction = logAction;
        }

        public override async UniTask RunAsync()
        {
            var startTime = Time.time;

            var resourcePath = string.IsNullOrEmpty(RootPath) ? Path : string.Concat(RootPath, "/", Path);
            var resourceType = _redirector != null ? _redirector.RedirectType : typeof(T);
            var asset = await UnityEngine.Resources.LoadAsync(resourcePath, resourceType);
            var assetName = System.IO.Path.GetFileNameWithoutExtension(Path);

            var obj = _redirector is null ? asset as T : await _redirector.ToSourceAsync<T>(asset, assetName);

            var result = new Resource<T>(Path, obj, Provider);

            SetResult(result);

            _logAction?.Invoke($"Loaded resource '{Path}' of type '{typeof(T).Name}' in {Time.time - startTime:0.###} seconds.");
        }
    }
}
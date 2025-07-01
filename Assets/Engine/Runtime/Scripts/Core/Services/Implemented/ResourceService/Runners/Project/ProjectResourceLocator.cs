using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ZLinq;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Common.Resources;

namespace Sinkii09.Engine.Services
{
    public class ProjectResourceLocator<T> : LocateResourceRunner<T> where T : UnityEngine.Object
    {
        public readonly string RootPath;

        private ProjectResources _projectResources;
        public ProjectResourceLocator(IResourceProvider provider, string path, string rootPath, ProjectResources projectResources) : base(provider, path ?? string.Empty)
        {
            RootPath = rootPath;
            _projectResources = projectResources;
        }

        public override UniTask RunAsync()
        {
            var locatedPaths = LocateProjectResources(RootPath, Path, _projectResources);
            SetResult(locatedPaths);
            return UniTask.CompletedTask;
        }

        private IEnumerable<string> LocateProjectResources(string rootPath, string resourcesPath, ProjectResources projectResources)
        {
            var path = string.IsNullOrEmpty(rootPath) ? resourcesPath : string.IsNullOrEmpty(resourcesPath) ? rootPath : $"{rootPath}/{resourcesPath}";
            var result = projectResources.ResourcePaths.LocateResourcePathsAtFolder(path);
            if (!string.IsNullOrEmpty(rootPath))
                return result.AsValueEnumerable().Select(p => p.GetAfterFirst(rootPath + "/")).ToList();
            return result;
        }
    }
}
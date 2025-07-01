using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ZLinq;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Common.Resources;
using Sinkii09.Engine.Common;

namespace Sinkii09.Engine.Services
{
    public class ProjectFolderLocator : LocateFolderRunner
    {
        public readonly string RootPath;
        private ProjectResources _projectResources;
        public ProjectFolderLocator(IResourceProvider provider, string path, string rootPath, ProjectResources projectResources) : base(provider, path ?? string.Empty)
        {
            RootPath = rootPath;
            _projectResources = projectResources;
        }

        public override UniTask RunAsync()
        {
            var locatedFolders = LocateProjectFolders(RootPath, Path, _projectResources);
            SetResult(locatedFolders);
            return UniTask.CompletedTask;
        }
        public static List<Folder> LocateProjectFolders(string rootPath, string resourcesPath, ProjectResources projectResources)
        {
            var path = string.IsNullOrEmpty(rootPath) ? resourcesPath : string.IsNullOrEmpty(resourcesPath) ? rootPath : $"{rootPath}/{resourcesPath}";
            return projectResources.ResourcePaths.LocateFolderPathsAtFolder(path)
                .AsValueEnumerable().Select(p => new Folder(string.IsNullOrEmpty(rootPath) ? p : p.GetAfterFirst(rootPath + "/"))).ToList();
        }
    }
}
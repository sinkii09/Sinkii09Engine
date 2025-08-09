using Sinkii09.Engine.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Common.Resources
{
    [Serializable]
    [CreateAssetMenu(fileName = nameof(ProjectResources), menuName = "Engine/Resources/Project Resources", order = 1)]
    public class ProjectResources : ScriptableObject
    {
        public List<string> ResourcePaths => resourcePaths;

        [SerializeField] List<string> resourcePaths = new List<string>();

        private void Awake()
        {
            LocateAllResources();
        }

        public static ProjectResources Get()
        {
            return Application.isEditor ? CreateInstance<ProjectResources>() : UnityEngine.Resources.Load<ProjectResources>(nameof(ProjectResources));
        }
        public void LocateAllResources()
        {
#if UNITY_EDITOR
            resourcePaths.Clear();
            var dataDir = new System.IO.DirectoryInfo(Application.dataPath);
            var resourcesDirs = dataDir.GetDirectories("*Resources", System.IO.SearchOption.AllDirectories)
                .Where(d => d.FullName.EndsWithFast($"{System.IO.Path.DirectorySeparatorChar}Resources")).ToList();
            foreach (var dir in resourcesDirs)
                WalkResourcesDirectory(dir, resourcePaths);
#endif
        }

#if UNITY_EDITOR
        private static void WalkResourcesDirectory(System.IO.DirectoryInfo directory, List<string> outPaths)
        {
            var paths = directory.GetFiles().Where(p => !p.FullName.EndsWithFast(".meta"))
                .Select(p => p.FullName.Replace("\\", "/").GetAfterFirst("/Resources/").GetBeforeLast("."));
            outPaths.AddRange(paths);

            var subDirs = directory.GetDirectories();
            foreach (var dirInfo in subDirs)
                WalkResourcesDirectory(dirInfo, outPaths);
        }
#endif
    }
}
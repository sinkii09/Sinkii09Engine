using Sinkii09.Engine.Services;
using UnityEngine;

namespace Sinkii09.Engine.Configs
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "ScriptsConfig", menuName = "Engine/Configs/ScriptsConfig", order = 1)]
    public class ScriptsConfig : Configuration
    {
        public const string DefaultScriptsPathPrefix = "Scripts";

        public ResouceLoaderConfig ResouceLoaderConfig = new ResouceLoaderConfig()
        {
            PathPrefix = DefaultScriptsPathPrefix
        };
    }
}
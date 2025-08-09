using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using UnityEngine;

namespace Sinkii09.Engine.Common.Resources
{
    public class Resource
    {
        // TODO: Implement resource loading logic
        public bool IsValid => ObjUtils.IsValid(Asset);

        public string Path { get; }
        public Object Asset { get; }
        public IResourceProvider Provider { get; }

        public Resource(string path, Object asset, IResourceProvider provider)
        {
            Path = path;
            Asset = asset;
            Provider = provider;
        }
    }
    public class Resource<T> : Resource where T : Object
    {
        // TODO: Rework casting logic if necessary
        public new T Asset => (T)base.Asset;

        public static implicit operator T(Resource<T> resource) => resource?.Asset;

        public Resource(string path, Object asset, IResourceProvider provider) : base(path, asset, provider)
        {
        }
    }
}
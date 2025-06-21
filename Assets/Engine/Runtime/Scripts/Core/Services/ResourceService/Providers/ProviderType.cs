using System;

namespace Sinkii09.Engine.Services
{
    [Flags]
    public enum ProviderType
    {
        None = 0,
        AssetBundle = 1 << 0,
        Resources = 1 << 1,
        Local = 1 << 2,
    }
}
using System;

namespace Sinkii09.Engine.Services
{
    [Flags]
    public enum ProviderType
    {
        None = 0,
        Addressable = 1 << 0,
        AssetBundle = 1 << 1,
        Resources = 1 << 2,
        Local = 1 << 3,
    }
}
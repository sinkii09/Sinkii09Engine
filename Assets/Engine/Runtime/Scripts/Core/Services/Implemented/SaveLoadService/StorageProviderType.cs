using System;

namespace Sinkii09.Engine.Services
{
    [Flags]
    public enum StorageProviderType
    {
        None = 0,
        LocalFile = 1 << 0,
        CloudStorage = 1 << 1,
        PlayerPrefs = 1 << 2,
        Steam = 1 << 3,
        GooglePlay = 1 << 4,
        
        // Common combinations
        All = LocalFile | CloudStorage | PlayerPrefs | Steam | GooglePlay,
        MobilePlatforms = PlayerPrefs | GooglePlay,
        DesktopPlatforms = LocalFile | Steam,
    }
}
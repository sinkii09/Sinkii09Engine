using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Attribute to mark storage provider implementations for automatic discovery
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StorageProviderAttribute : Attribute
    {
        public StorageProviderType ProviderType { get; }
        public bool RequiresAuthentication { get; set; }
        public SupportedPlatform SupportedPlatforms { get; set; }
        public string Description { get; set; }

        public StorageProviderAttribute(StorageProviderType providerType)
        {
            ProviderType = providerType;
            SupportedPlatforms = SupportedPlatform.All;
        }
    }
}
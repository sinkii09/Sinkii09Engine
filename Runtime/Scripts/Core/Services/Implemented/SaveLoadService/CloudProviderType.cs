namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Types of cloud storage providers
    /// </summary>
    public enum CloudProviderType
    {
        None = 0,
        GoogleDrive,
        OneDrive,
        Dropbox,
        iCloud,
        AmazonS3,
        AzureBlob,
        CustomCloud
    }
}
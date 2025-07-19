using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for cloud storage providers that extends IStorageProvider with cloud-specific functionality
    /// </summary>
    public interface ICloudStorageProvider : IStorageProvider
    {
        
        /// <summary>
        /// Gets the current authentication status
        /// </summary>
        CloudAuthenticationStatus AuthenticationStatus { get; }
        
        /// <summary>
        /// Gets the current sync status
        /// </summary>
        CloudSyncStatus SyncStatus { get; }
        
        /// <summary>
        /// Gets the available cloud storage quota information
        /// </summary>
        CloudStorageQuota StorageQuota { get; }
        
        /// <summary>
        /// Authenticates with the cloud provider
        /// </summary>
        /// <param name="credentials">Cloud provider credentials</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Authentication result</returns>
        UniTask<CloudAuthenticationResult> AuthenticateAsync(CloudCredentials credentials, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Refreshes the authentication token
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Authentication result</returns>
        UniTask<CloudAuthenticationResult> RefreshAuthenticationAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Logs out from the cloud provider
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Logout result</returns>
        UniTask<CloudOperationResult> LogoutAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Synchronizes local saves with cloud storage
        /// </summary>
        /// <param name="syncOptions">Synchronization options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Synchronization result</returns>
        UniTask<CloudSyncResult> SyncAsync(CloudSyncOptions syncOptions = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the cloud storage quota information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Storage quota result</returns>
        UniTask<CloudStorageQuotaResult> GetStorageQuotaAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Uploads a save file to cloud storage
        /// </summary>
        /// <param name="saveId">Save identifier</param>
        /// <param name="data">Save data</param>
        /// <param name="metadata">Save metadata</param>
        /// <param name="overwrite">Whether to overwrite existing files</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Upload result</returns>
        UniTask<CloudUploadResult> UploadAsync(string saveId, byte[] data, SaveMetadata metadata = null, bool overwrite = true, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Downloads a save file from cloud storage
        /// </summary>
        /// <param name="saveId">Save identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Download result</returns>
        UniTask<CloudDownloadResult> DownloadAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the list of saves available in cloud storage
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cloud save list result</returns>
        UniTask<CloudSaveListResult> GetCloudSaveListAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a save file from cloud storage
        /// </summary>
        /// <param name="saveId">Save identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Delete result</returns>
        UniTask<CloudOperationResult> DeleteFromCloudAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Resolves conflicts when the same save exists both locally and in cloud with different content
        /// </summary>
        /// <param name="saveId">Save identifier</param>
        /// <param name="conflictResolution">Conflict resolution strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Conflict resolution result</returns>
        UniTask<CloudConflictResolutionResult> ResolveConflictAsync(string saveId, CloudConflictResolution conflictResolution, CancellationToken cancellationToken = default);
    }
    
    #region Cloud Storage Enums
    
    /// <summary>
    /// Cloud authentication status
    /// </summary>
    public enum CloudAuthenticationStatus
    {
        NotAuthenticated,
        Authenticated,
        TokenExpired,
        AuthenticationFailed,
        RefreshRequired
    }
    
    /// <summary>
    /// Cloud synchronization status
    /// </summary>
    public enum CloudSyncStatus
    {
        NotSynced,
        Syncing,
        Synced,
        SyncFailed,
        ConflictDetected
    }
    
    /// <summary>
    /// Cloud conflict resolution strategies
    /// </summary>
    public enum CloudConflictResolution
    {
        KeepLocal,
        KeepCloud,
        KeepBoth,
        MergeData,
        UserChoice
    }
    
    #endregion
    
    #region Cloud Storage Data Classes
    
    /// <summary>
    /// Cloud provider credentials
    /// </summary>
    public class CloudCredentials
    {
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Cloud storage quota information
    /// </summary>
    public class CloudStorageQuota
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes { get; set; }
        public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
        public bool IsQuotaExceeded => UsedBytes >= TotalBytes;
    }
    
    /// <summary>
    /// Cloud synchronization options
    /// </summary>
    public class CloudSyncOptions
    {
        public bool UploadLocalChanges { get; set; } = true;
        public bool DownloadCloudChanges { get; set; } = true;
        public CloudConflictResolution DefaultConflictResolution { get; set; } = CloudConflictResolution.UserChoice;
        public bool DeleteCloudOrphans { get; set; } = false;
        public bool DeleteLocalOrphans { get; set; } = false;
        public TimeSpan SyncTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
    
    #endregion
    
    #region Cloud Storage Result Classes
    
    /// <summary>
    /// Cloud authentication result
    /// </summary>
    public class CloudAuthenticationResult : StorageOperationResult
    {
        public CloudCredentials Credentials { get; set; }
        public CloudAuthenticationStatus Status { get; set; }
        
        public static CloudAuthenticationResult CreateSuccess(CloudCredentials credentials, TimeSpan duration)
        {
            return new CloudAuthenticationResult
            {
                Success = true,
                Credentials = credentials,
                Status = CloudAuthenticationStatus.Authenticated,
                Duration = duration
            };
        }
        
        public static new CloudAuthenticationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudAuthenticationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Status = CloudAuthenticationStatus.AuthenticationFailed,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud operation result
    /// </summary>
    public class CloudOperationResult : StorageOperationResult
    {
        public string CloudFileId { get; set; }
        public Dictionary<string, object> CloudMetadata { get; set; } = new Dictionary<string, object>();
        
        public static new CloudOperationResult CreateSuccess(TimeSpan duration, long bytesProcessed = 0)
        {
            return new CloudOperationResult
            {
                Success = true,
                Duration = duration,
                BytesProcessed = bytesProcessed
            };
        }
        
        public static new CloudOperationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudOperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud upload result
    /// </summary>
    public class CloudUploadResult : CloudOperationResult
    {
        public string UploadUrl { get; set; }
        public DateTime UploadTimestamp { get; set; }
        
        public static CloudUploadResult CreateSuccess(string cloudFileId, TimeSpan duration, long bytesProcessed)
        {
            return new CloudUploadResult
            {
                Success = true,
                CloudFileId = cloudFileId,
                Duration = duration,
                BytesProcessed = bytesProcessed,
                UploadTimestamp = DateTime.UtcNow
            };
        }
        
        public static new CloudUploadResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudUploadResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud download result
    /// </summary>
    public class CloudDownloadResult : StorageLoadResult
    {
        public string CloudFileId { get; set; }
        public DateTime LastModified { get; set; }
        public string ETag { get; set; }
        
        public static CloudDownloadResult CreateSuccess(byte[] data, SaveMetadata metadata, string cloudFileId, TimeSpan duration)
        {
            return new CloudDownloadResult
            {
                Success = true,
                Data = data,
                Metadata = metadata,
                CloudFileId = cloudFileId,
                Duration = duration,
                BytesProcessed = data?.Length ?? 0,
                LastModified = DateTime.UtcNow
            };
        }
        
        public static new CloudDownloadResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudDownloadResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud save list result
    /// </summary>
    public class CloudSaveListResult : StorageListResult
    {
        public Dictionary<string, string> CloudFileIds { get; set; } = new Dictionary<string, string>();
        
        public static CloudSaveListResult CreateSuccess(SaveMetadata[] saveList, Dictionary<string, string> cloudFileIds, TimeSpan duration)
        {
            return new CloudSaveListResult
            {
                Success = true,
                SaveList = saveList ?? new SaveMetadata[0],
                CloudFileIds = cloudFileIds ?? new Dictionary<string, string>(),
                Duration = duration
            };
        }
        
        public static new CloudSaveListResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudSaveListResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud synchronization result
    /// </summary>
    public class CloudSyncResult : StorageOperationResult
    {
        public int FilesUploaded { get; set; }
        public int FilesDownloaded { get; set; }
        public int ConflictsResolved { get; set; }
        public int ConflictsRemaining { get; set; }
        public List<string> SyncedFiles { get; set; } = new List<string>();
        public List<CloudSyncConflict> Conflicts { get; set; } = new List<CloudSyncConflict>();
        
        public static CloudSyncResult CreateSuccess(int uploaded, int downloaded, int conflictsResolved, TimeSpan duration)
        {
            return new CloudSyncResult
            {
                Success = true,
                FilesUploaded = uploaded,
                FilesDownloaded = downloaded,
                ConflictsResolved = conflictsResolved,
                Duration = duration
            };
        }
        
        public static new CloudSyncResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudSyncResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud sync conflict information
    /// </summary>
    public class CloudSyncConflict
    {
        public string SaveId { get; set; }
        public DateTime LocalTimestamp { get; set; }
        public DateTime CloudTimestamp { get; set; }
        public long LocalSize { get; set; }
        public long CloudSize { get; set; }
        public CloudConflictResolution RecommendedResolution { get; set; }
        public string ConflictReason { get; set; }
    }
    
    /// <summary>
    /// Cloud conflict resolution result
    /// </summary>
    public class CloudConflictResolutionResult : StorageOperationResult
    {
        public CloudConflictResolution Resolution { get; set; }
        public string ResolvedSaveId { get; set; }
        public string BackupSaveId { get; set; }
        
        public static CloudConflictResolutionResult CreateSuccess(CloudConflictResolution resolution, string resolvedSaveId, TimeSpan duration)
        {
            return new CloudConflictResolutionResult
            {
                Success = true,
                Resolution = resolution,
                ResolvedSaveId = resolvedSaveId,
                Duration = duration
            };
        }
        
        public static new CloudConflictResolutionResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudConflictResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Cloud storage quota result
    /// </summary>
    public class CloudStorageQuotaResult : StorageOperationResult
    {
        public CloudStorageQuota Quota { get; set; }
        
        public static CloudStorageQuotaResult CreateSuccess(CloudStorageQuota quota, TimeSpan duration)
        {
            return new CloudStorageQuotaResult
            {
                Success = true,
                Quota = quota,
                Duration = duration
            };
        }
        
        public static new CloudStorageQuotaResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new CloudStorageQuotaResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    #endregion
}
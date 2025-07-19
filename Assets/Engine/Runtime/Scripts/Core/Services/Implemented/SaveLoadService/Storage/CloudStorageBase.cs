using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base implementation for cloud storage providers with common functionality
    /// </summary>
    public abstract class CloudStorageBase : ICloudStorageProvider
    {
        #region Protected Fields
        protected StorageProviderConfiguration _configuration;
        protected StorageProviderStatistics _statistics;
        protected StorageHealthStatus _healthStatus;
        protected CloudCredentials _credentials;
        protected CloudAuthenticationStatus _authStatus;
        protected CloudSyncStatus _syncStatus;
        protected CloudStorageQuota _storageQuota;
        protected bool _isInitialized;
        protected readonly object _lockObject = new object();
        #endregion
        
        #region Abstract Properties
        public abstract StorageProviderType ProviderType { get; }
        public abstract CloudProviderType CloudProvider { get; }
        #endregion
        
        #region Properties
        public StorageHealthStatus HealthStatus => _healthStatus;
        public CloudAuthenticationStatus AuthenticationStatus => _authStatus;
        public CloudSyncStatus SyncStatus => _syncStatus;
        public CloudStorageQuota StorageQuota => _storageQuota;
        
        /// <summary>
        /// Gets the display name for the cloud provider
        /// </summary>
        public string CloudProviderName
        {
            get
            {
                return CloudProvider switch
                {
                    CloudProviderType.GoogleDrive => "Google Drive",
                    CloudProviderType.OneDrive => "OneDrive",
                    CloudProviderType.Dropbox => "Dropbox",
                    CloudProviderType.iCloud => "iCloud",
                    CloudProviderType.AmazonS3 => "Amazon S3",
                    CloudProviderType.AzureBlob => "Azure Blob Storage",
                    CloudProviderType.CustomCloud => "Custom Cloud Storage",
                    _ => "Unknown Cloud Provider"
                };
            }
        }
        #endregion
        
        #region Constructor
        protected CloudStorageBase()
        {
            _statistics = new StorageProviderStatistics();
            _healthStatus = StorageHealthStatus.Unknown;
            _authStatus = CloudAuthenticationStatus.NotAuthenticated;
            _syncStatus = CloudSyncStatus.NotSynced;
            _storageQuota = new CloudStorageQuota();
            _isInitialized = false;
        }
        #endregion
        
        #region IStorageProvider Implementation
        public async UniTask<StorageInitializationResult> InitializeAsync(StorageProviderConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                
                // Perform cloud-specific initialization
                await InitializeCloudProviderAsync(cancellationToken);
                
                _isInitialized = true;
                _healthStatus = StorageHealthStatus.Healthy;
                
                stopwatch.Stop();
                return StorageInitializationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _healthStatus = StorageHealthStatus.Unhealthy;
                return StorageInitializationResult.CreateFailure($"Failed to initialize {CloudProviderName}: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> SaveAsync(string saveId, byte[] data, SaveMetadata metadata = null, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var uploadResult = await UploadAsync(saveId, data, metadata, true, cancellationToken);
            
            if (uploadResult.Success)
            {
                _statistics.TotalSaves++;
                _statistics.RecordOperation(true, uploadResult.Duration, uploadResult.BytesProcessed, true);
                
                return StorageOperationResult.CreateSuccess(uploadResult.Duration, uploadResult.BytesProcessed);
            }
            else
            {
                _statistics.RecordOperation(false, uploadResult.Duration);
                return StorageOperationResult.CreateFailure(uploadResult.ErrorMessage, uploadResult.Exception, uploadResult.Duration);
            }
        }
        
        public async UniTask<StorageLoadResult> LoadAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var downloadResult = await DownloadAsync(saveId, cancellationToken);
            
            if (downloadResult.Success)
            {
                _statistics.TotalLoads++;
                _statistics.RecordOperation(true, downloadResult.Duration, downloadResult.BytesProcessed, false);
                
                return StorageLoadResult.CreateSuccess(downloadResult.Data, downloadResult.Metadata, downloadResult.Duration);
            }
            else
            {
                _statistics.RecordOperation(false, downloadResult.Duration);
                return StorageLoadResult.CreateFailure(downloadResult.ErrorMessage, downloadResult.Exception, downloadResult.Duration);
            }
        }
        
        public async UniTask<StorageOperationResult> DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var deleteResult = await DeleteFromCloudAsync(saveId, cancellationToken);
            
            if (deleteResult.Success)
            {
                _statistics.TotalDeletes++;
                _statistics.RecordOperation(true, deleteResult.Duration);
                
                return StorageOperationResult.CreateSuccess(deleteResult.Duration);
            }
            else
            {
                _statistics.RecordOperation(false, deleteResult.Duration);
                return StorageOperationResult.CreateFailure(deleteResult.ErrorMessage, deleteResult.Exception, deleteResult.Duration);
            }
        }
        
        public async UniTask<StorageExistsResult> ExistsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var saveListResult = await GetCloudSaveListAsync(cancellationToken);
                
                if (saveListResult.Success)
                {
                    bool exists = saveListResult.CloudFileIds.ContainsKey(saveId);
                    stopwatch.Stop();
                    
                    return StorageExistsResult.CreateSuccess(exists, stopwatch.Elapsed);
                }
                else
                {
                    stopwatch.Stop();
                    return StorageExistsResult.CreateFailure($"Failed to check existence: {saveListResult.ErrorMessage}", 
                        saveListResult.Exception, stopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageExistsResult.CreateFailure($"Failed to check existence of '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageListResult> GetSaveListAsync(CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var cloudListResult = await GetCloudSaveListAsync(cancellationToken);
            
            if (cloudListResult.Success)
            {
                return StorageListResult.CreateSuccess(cloudListResult.SaveList, cloudListResult.Duration);
            }
            else
            {
                return StorageListResult.CreateFailure(cloudListResult.ErrorMessage, cloudListResult.Exception, cloudListResult.Duration);
            }
        }
        
        public async UniTask<StorageListResult> GetBackupListAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var cloudBackupResult = await GetCloudBackupListAsync(saveId, cancellationToken);
            
            if (cloudBackupResult.Success)
            {
                return StorageListResult.CreateSuccess(cloudBackupResult.SaveList, cloudBackupResult.Duration);
            }
            else
            {
                return StorageListResult.CreateFailure(cloudBackupResult.ErrorMessage, cloudBackupResult.Exception, cloudBackupResult.Duration);
            }
        }
        
        public async UniTask<StorageMetadataResult> GetMetadataAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var saveListResult = await GetCloudSaveListAsync(cancellationToken);
                
                if (saveListResult.Success)
                {
                    var metadata = saveListResult.SaveList.FirstOrDefault(s => s.SaveId == saveId);
                    
                    if (metadata != null)
                    {
                        stopwatch.Stop();
                        return StorageMetadataResult.CreateSuccess(metadata, stopwatch.Elapsed);
                    }
                    else
                    {
                        stopwatch.Stop();
                        return StorageMetadataResult.CreateFailure($"Save '{saveId}' not found", duration: stopwatch.Elapsed);
                    }
                }
                else
                {
                    stopwatch.Stop();
                    return StorageMetadataResult.CreateFailure($"Failed to get metadata: {saveListResult.ErrorMessage}", 
                        saveListResult.Exception, stopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageMetadataResult.CreateFailure($"Failed to get metadata for '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> CreateBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Download the original save
                var downloadResult = await DownloadAsync(saveId, cancellationToken);
                
                if (!downloadResult.Success)
                {
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Failed to download original save: {downloadResult.ErrorMessage}", 
                        downloadResult.Exception, stopwatch.Elapsed);
                }
                
                // Upload as backup
                var uploadResult = await UploadAsync(backupId, downloadResult.Data, downloadResult.Metadata, true, cancellationToken);
                
                if (uploadResult.Success)
                {
                    _statistics.TotalBackups++;
                    _statistics.RecordOperation(true, uploadResult.Duration);
                    
                    stopwatch.Stop();
                    return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
                }
                else
                {
                    _statistics.RecordOperation(false, uploadResult.Duration);
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Failed to create backup: {uploadResult.ErrorMessage}", 
                        uploadResult.Exception, stopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageOperationResult.CreateFailure($"Failed to create backup '{backupId}' for '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> RestoreFromBackupAsync(string backupId, string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Download the backup
                var downloadResult = await DownloadAsync(backupId, cancellationToken);
                
                if (!downloadResult.Success)
                {
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Failed to download backup: {downloadResult.ErrorMessage}", 
                        downloadResult.Exception, stopwatch.Elapsed);
                }
                
                // Upload as the restored save
                var uploadResult = await UploadAsync(saveId, downloadResult.Data, downloadResult.Metadata, true, cancellationToken);
                
                if (uploadResult.Success)
                {
                    _statistics.TotalRestores++;
                    _statistics.RecordOperation(true, uploadResult.Duration);
                    
                    stopwatch.Stop();
                    return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
                }
                else
                {
                    _statistics.RecordOperation(false, uploadResult.Duration);
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Failed to restore from backup: {uploadResult.ErrorMessage}", 
                        uploadResult.Exception, stopwatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageOperationResult.CreateFailure($"Failed to restore from backup '{backupId}' to '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageHealthResult> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var healthMetrics = new Dictionary<string, object>();
                var status = StorageHealthStatus.Healthy;
                var statusMessage = $"{CloudProviderName} storage is healthy";
                
                // Check authentication status
                if (_authStatus != CloudAuthenticationStatus.Authenticated)
                {
                    status = StorageHealthStatus.Degraded;
                    statusMessage = $"{CloudProviderName} authentication required";
                }
                
                // Check cloud connectivity
                try
                {
                    var quotaResult = await GetStorageQuotaAsync(cancellationToken);
                    if (quotaResult.Success)
                    {
                        healthMetrics["quota_used_gb"] = quotaResult.Quota.UsedBytes / (1024.0 * 1024.0 * 1024.0);
                        healthMetrics["quota_available_gb"] = quotaResult.Quota.AvailableBytes / (1024.0 * 1024.0 * 1024.0);
                        healthMetrics["quota_percentage"] = quotaResult.Quota.UsagePercentage;
                        
                        if (quotaResult.Quota.IsQuotaExceeded)
                        {
                            status = StorageHealthStatus.Unhealthy;
                            statusMessage = $"{CloudProviderName} storage quota exceeded";
                        }
                        else if (quotaResult.Quota.UsagePercentage > 90)
                        {
                            status = StorageHealthStatus.Degraded;
                            statusMessage = $"{CloudProviderName} storage quota nearly full";
                        }
                    }
                    else
                    {
                        status = StorageHealthStatus.Degraded;
                        statusMessage = $"{CloudProviderName} connectivity issues";
                    }
                }
                catch (Exception ex)
                {
                    status = StorageHealthStatus.Unhealthy;
                    statusMessage = $"{CloudProviderName} health check failed: {ex.Message}";
                }
                
                // Add statistics
                healthMetrics["total_saves"] = _statistics.TotalSaves;
                healthMetrics["total_loads"] = _statistics.TotalLoads;
                healthMetrics["success_rate"] = _statistics.SuccessRate;
                healthMetrics["auth_status"] = _authStatus.ToString();
                healthMetrics["sync_status"] = _syncStatus.ToString();
                
                stopwatch.Stop();
                _healthStatus = status;
                
                return StorageHealthResult.CreateSuccess(status, statusMessage, healthMetrics, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _healthStatus = StorageHealthStatus.Unhealthy;
                return StorageHealthResult.CreateFailure($"Health check failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Perform cloud-specific cleanup
                await CleanupCloudResourcesAsync(cancellationToken);
                
                stopwatch.Stop();
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageOperationResult.CreateFailure($"Cleanup failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public StorageProviderStatistics GetStatistics()
        {
            return _statistics;
        }
        
        public async UniTask<StorageOperationResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Perform cloud-specific shutdown
                await ShutdownCloudProviderAsync(cancellationToken);
                
                _isInitialized = false;
                _healthStatus = StorageHealthStatus.Offline;
                
                stopwatch.Stop();
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageOperationResult.CreateFailure($"Shutdown failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region ICloudStorageProvider Implementation
        public abstract UniTask<CloudAuthenticationResult> AuthenticateAsync(CloudCredentials credentials, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudAuthenticationResult> RefreshAuthenticationAsync(CancellationToken cancellationToken = default);
        public abstract UniTask<CloudOperationResult> LogoutAsync(CancellationToken cancellationToken = default);
        public abstract UniTask<CloudSyncResult> SyncAsync(CloudSyncOptions syncOptions = null, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudStorageQuotaResult> GetStorageQuotaAsync(CancellationToken cancellationToken = default);
        public abstract UniTask<CloudUploadResult> UploadAsync(string saveId, byte[] data, SaveMetadata metadata = null, bool overwrite = true, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudDownloadResult> DownloadAsync(string saveId, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudSaveListResult> GetCloudSaveListAsync(CancellationToken cancellationToken = default);
        public abstract UniTask<CloudSaveListResult> GetCloudBackupListAsync(string saveId, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudOperationResult> DeleteFromCloudAsync(string saveId, CancellationToken cancellationToken = default);
        public abstract UniTask<CloudConflictResolutionResult> ResolveConflictAsync(string saveId, CloudConflictResolution conflictResolution, CancellationToken cancellationToken = default);
        #endregion
        
        #region Protected Abstract Methods
        protected abstract UniTask InitializeCloudProviderAsync(CancellationToken cancellationToken);
        protected abstract UniTask CleanupCloudResourcesAsync(CancellationToken cancellationToken);
        protected abstract UniTask ShutdownCloudProviderAsync(CancellationToken cancellationToken);
        #endregion
        
        #region Protected Helper Methods
        protected void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException($"{CloudProviderName} storage is not initialized");
        }
        
        protected void ValidateAuthentication()
        {
            if (_authStatus != CloudAuthenticationStatus.Authenticated)
                throw new InvalidOperationException($"{CloudProviderName} authentication required");
        }
        
        protected void UpdateAuthenticationStatus(CloudAuthenticationStatus status, CloudCredentials credentials = null)
        {
            _authStatus = status;
            
            if (credentials != null)
            {
                _credentials = credentials;
            }
            
            if (status == CloudAuthenticationStatus.NotAuthenticated)
            {
                _credentials = null;
            }
        }
        
        protected void UpdateSyncStatus(CloudSyncStatus status)
        {
            _syncStatus = status;
        }
        
        protected void UpdateStorageQuota(CloudStorageQuota quota)
        {
            _storageQuota = quota;
        }
        #endregion
    }
}
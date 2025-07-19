using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for storage providers that handle save/load operations across different storage backends
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>
        /// Gets the provider type identifier
        /// </summary>
        StorageProviderType ProviderType { get; }
        
        /// <summary>
        /// Gets the current health status of the storage provider
        /// </summary>
        StorageHealthStatus HealthStatus { get; }
        
        /// <summary>
        /// Initializes the storage provider with configuration
        /// </summary>
        /// <param name="configuration">Provider-specific configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Initialization result</returns>
        UniTask<StorageInitializationResult> InitializeAsync(StorageProviderConfiguration configuration, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves data to the storage backend
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="data">Serialized data to save</param>
        /// <param name="metadata">Optional metadata for the save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Save operation result</returns>
        UniTask<StorageOperationResult> SaveAsync(string saveId, byte[] data, SaveMetadata metadata = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Loads data from the storage backend
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Load operation result with data</returns>
        UniTask<StorageLoadResult> LoadAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes data from the storage backend
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Delete operation result</returns>
        UniTask<StorageOperationResult> DeleteAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if data exists in the storage backend
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Existence check result</returns>
        UniTask<StorageExistsResult> ExistsAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a list of all available save files
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of save metadata</returns>
        UniTask<StorageListResult> GetSaveListAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a list of backup files for a specific save ID
        /// </summary>
        /// <param name="saveId">Save ID to get backups for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of backup metadata</returns>
        UniTask<StorageListResult> GetBackupListAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets metadata for a specific save file
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Metadata result</returns>
        UniTask<StorageMetadataResult> GetMetadataAsync(string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a backup of the specified save file
        /// </summary>
        /// <param name="saveId">Unique identifier for the save data</param>
        /// <param name="backupId">Unique identifier for the backup</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Backup operation result</returns>
        UniTask<StorageOperationResult> CreateBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Restores a save file from backup
        /// </summary>
        /// <param name="backupId">Unique identifier for the backup</param>
        /// <param name="saveId">Unique identifier for the save data to restore</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Restore operation result</returns>
        UniTask<StorageOperationResult> RestoreFromBackupAsync(string backupId, string saveId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs a health check on the storage provider
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        UniTask<StorageHealthResult> HealthCheckAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Cleans up temporary files and performs maintenance
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cleanup operation result</returns>
        UniTask<StorageOperationResult> CleanupAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets storage provider statistics
        /// </summary>
        /// <returns>Storage statistics</returns>
        StorageProviderStatistics GetStatistics();
        
        /// <summary>
        /// Shuts down the storage provider and releases resources
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Shutdown result</returns>
        UniTask<StorageOperationResult> ShutdownAsync(CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Health status of a storage provider
    /// </summary>
    public enum StorageHealthStatus
    {
        Unknown,
        Healthy,
        Degraded,
        Unhealthy,
        Offline
    }
    
    /// <summary>
    /// Result of storage provider initialization
    /// </summary>
    public class StorageInitializationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static StorageInitializationResult CreateSuccess(TimeSpan duration)
        {
            return new StorageInitializationResult
            {
                Success = true,
                Duration = duration
            };
        }
        
        public static StorageInitializationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageInitializationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage operations
    /// </summary>
    public class StorageOperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public TimeSpan Duration { get; set; }
        public long BytesProcessed { get; set; }
        
        public static StorageOperationResult CreateSuccess(TimeSpan duration, long bytesProcessed = 0)
        {
            return new StorageOperationResult
            {
                Success = true,
                Duration = duration,
                BytesProcessed = bytesProcessed
            };
        }
        
        public static StorageOperationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageOperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage load operations
    /// </summary>
    public class StorageLoadResult : StorageOperationResult
    {
        public byte[] Data { get; set; }
        public SaveMetadata Metadata { get; set; }
        
        public static StorageLoadResult CreateSuccess(byte[] data, SaveMetadata metadata, TimeSpan duration)
        {
            return new StorageLoadResult
            {
                Success = true,
                Data = data,
                Metadata = metadata,
                Duration = duration,
                BytesProcessed = data?.Length ?? 0
            };
        }
        
        public static new StorageLoadResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageLoadResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage existence check
    /// </summary>
    public class StorageExistsResult : StorageOperationResult
    {
        public bool Exists { get; set; }
        
        public static StorageExistsResult CreateSuccess(bool exists, TimeSpan duration)
        {
            return new StorageExistsResult
            {
                Success = true,
                Exists = exists,
                Duration = duration
            };
        }
        
        public static new StorageExistsResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageExistsResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage list operations
    /// </summary>
    public class StorageListResult : StorageOperationResult
    {
        public SaveMetadata[] SaveList { get; set; }
        
        public static StorageListResult CreateSuccess(SaveMetadata[] saveList, TimeSpan duration)
        {
            return new StorageListResult
            {
                Success = true,
                SaveList = saveList ?? new SaveMetadata[0],
                Duration = duration
            };
        }
        
        public static new StorageListResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageListResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage metadata operations
    /// </summary>
    public class StorageMetadataResult : StorageOperationResult
    {
        public SaveMetadata Metadata { get; set; }
        
        public static StorageMetadataResult CreateSuccess(SaveMetadata metadata, TimeSpan duration)
        {
            return new StorageMetadataResult
            {
                Success = true,
                Metadata = metadata,
                Duration = duration
            };
        }
        
        public static new StorageMetadataResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageMetadataResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Result of storage health check
    /// </summary>
    public class StorageHealthResult : StorageOperationResult
    {
        public StorageHealthStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public Dictionary<string, object> HealthMetrics { get; set; }
        
        public static StorageHealthResult CreateSuccess(StorageHealthStatus status, string statusMessage, Dictionary<string, object> healthMetrics, TimeSpan duration)
        {
            return new StorageHealthResult
            {
                Success = true,
                Status = status,
                StatusMessage = statusMessage,
                HealthMetrics = healthMetrics ?? new Dictionary<string, object>(),
                Duration = duration
            };
        }
        
        public static new StorageHealthResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new StorageHealthResult
            {
                Success = false,
                Status = StorageHealthStatus.Unhealthy,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }
    
    /// <summary>
    /// Configuration for storage providers
    /// </summary>
    public class StorageProviderConfiguration
    {
        public StorageProviderType ProviderType { get; set; }
        public string BasePath { get; set; }
        public Dictionary<string, object> Settings { get; set; }
        
        public StorageProviderConfiguration()
        {
            Settings = new Dictionary<string, object>();
        }
    }
    
    /// <summary>
    /// Statistics for storage provider operations
    /// </summary>
    public class StorageProviderStatistics
    {
        public long TotalSaves { get; set; }
        public long TotalLoads { get; set; }
        public long TotalDeletes { get; set; }
        public long TotalBackups { get; set; }
        public long TotalRestores { get; set; }
        public long SuccessfulOperations { get; set; }
        public long FailedOperations { get; set; }
        public long TotalBytesRead { get; set; }
        public long TotalBytesWritten { get; set; }
        public TimeSpan TotalOperationTime { get; set; }
        public TimeSpan AverageOperationTime { get; set; }
        public DateTime LastOperationTime { get; set; }
        public StorageHealthStatus CurrentHealth { get; set; }
        
        public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 0;
        public long TotalOperations => TotalSaves + TotalLoads + TotalDeletes + TotalBackups + TotalRestores;
        
        public void RecordOperation(bool success, TimeSpan duration, long bytesProcessed = 0, bool isWrite = false)
        {
            if (success)
            {
                SuccessfulOperations++;
                if (isWrite)
                    TotalBytesWritten += bytesProcessed;
                else
                    TotalBytesRead += bytesProcessed;
            }
            else
            {
                FailedOperations++;
            }
            
            TotalOperationTime = TotalOperationTime.Add(duration);
            var totalOps = TotalOperations;
            if (totalOps > 0)
                AverageOperationTime = new TimeSpan(TotalOperationTime.Ticks / totalOps);
            
            LastOperationTime = DateTime.UtcNow;
        }
    }
}
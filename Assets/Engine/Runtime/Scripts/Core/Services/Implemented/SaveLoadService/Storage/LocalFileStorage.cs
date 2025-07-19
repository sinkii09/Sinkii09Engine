using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Diagnostics;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Local file system storage provider for save/load operations
    /// </summary>
    [StorageProvider(StorageProviderType.LocalFile,
        Description = "Local file system storage provider",
        SupportedPlatforms = SupportedPlatform.All)]
    public class LocalFileStorage : IStorageProvider
    {
        #region Private Fields
        private const string SAVE_FILE_EXTENSION = ".sav";
        private const string BACKUP_FILE_EXTENSION = ".bak";
        private const string TEMP_FILE_EXTENSION = ".tmp";
        private const string METADATA_FILE_EXTENSION = ".meta";
        
        private StorageProviderConfiguration _configuration;
        private StorageProviderStatistics _statistics;
        private StorageHealthStatus _healthStatus;
        private string _saveDirectory;
        private string _backupDirectory;
        private bool _isInitialized;
        private readonly object _lockObject = new object();
        #endregion
        
        #region Properties
        public StorageProviderType ProviderType => StorageProviderType.LocalFile;
        public StorageHealthStatus HealthStatus => _healthStatus;
        #endregion
        
        #region Initialization
        public async UniTask<StorageInitializationResult> InitializeAsync(StorageProviderConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                _statistics = new StorageProviderStatistics();
                _healthStatus = StorageHealthStatus.Unknown;
                
                // Set up directories
                _saveDirectory = Path.Combine(Application.persistentDataPath, configuration.BasePath ?? "SaveData");
                _backupDirectory = Path.Combine(_saveDirectory, "Backups");
                
                // Create directories if they don't exist
                await CreateDirectoriesAsync(cancellationToken);
                
                // Validate directory permissions
                await ValidateDirectoryPermissionsAsync(cancellationToken);
                
                // Perform initial health check
                var healthResult = await HealthCheckAsync(cancellationToken);
                _healthStatus = healthResult.Success ? StorageHealthStatus.Healthy : StorageHealthStatus.Degraded;
                
                _isInitialized = true;
                stopwatch.Stop();
                
                return StorageInitializationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _healthStatus = StorageHealthStatus.Unhealthy;
                return StorageInitializationResult.CreateFailure($"Failed to initialize LocalFileStorage: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        private async UniTask ValidateDirectoryPermissionsAsync(CancellationToken cancellationToken)
        {
            // Test write permissions
            var testFile = Path.Combine(_saveDirectory, "test.tmp");
            await TestWritePermissionsAsync(testFile, cancellationToken);
        }
        #endregion
        
        #region Save Operations
        public async UniTask<StorageOperationResult> SaveAsync(string saveId, byte[] data, SaveMetadata metadata = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                
                if (data == null || data.Length == 0)
                    throw new ArgumentException("Data cannot be null or empty", nameof(data));
                
                var saveFilePath = GetSaveFilePath(saveId);
                var tempFilePath = GetTempFilePath(saveId);
                var metadataFilePath = GetMetadataFilePath(saveId);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(saveFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                
                // Write to temporary file first (atomic operation)
                await WriteFileAtomically(tempFilePath, saveFilePath, data, cancellationToken);
                
                // Save metadata if provided
                if (metadata != null)
                {
                    await SaveMetadataAsync(metadataFilePath, metadata, cancellationToken);
                }
                
                stopwatch.Stop();
                _statistics.TotalSaves++;
                _statistics.RecordOperation(true, stopwatch.Elapsed, data.Length, true);
                
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed, data.Length);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordOperation(false, stopwatch.Elapsed);
                return StorageOperationResult.CreateFailure($"Failed to save '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        private async UniTask WriteFileAtomically(string tempFilePath, string finalFilePath, byte[] data, CancellationToken cancellationToken)
        {
            // Write to temporary file
            await WriteToTempFileAsync(tempFilePath, data, cancellationToken);
            
            // Atomic move to final location
            await MoveFileAtomicallyAsync(tempFilePath, finalFilePath, cancellationToken);
        }
        
        private async UniTask SaveMetadataAsync(string metadataFilePath, SaveMetadata metadata, CancellationToken cancellationToken)
        {
            var metadataJson = JsonUtility.ToJson(metadata, true);
            var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataJson);
            
            await WriteMetadataFileAsync(metadataFilePath, metadataBytes, cancellationToken);
        }
        #endregion
        
        #region Load Operations
        public async UniTask<StorageLoadResult> LoadAsync(string saveId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                
                var saveFilePath = GetSaveFilePath(saveId);
                var metadataFilePath = GetMetadataFilePath(saveId);
                
                if (!File.Exists(saveFilePath))
                {
                    stopwatch.Stop();
                    return StorageLoadResult.CreateFailure($"Save file '{saveId}' not found", duration: stopwatch.Elapsed);
                }
                
                // Load data
                byte[] data = await ReadFileAsync(saveFilePath, cancellationToken);
                
                // Load metadata if available
                SaveMetadata metadata = null;
                if (File.Exists(metadataFilePath))
                {
                    try
                    {
                        metadata = await LoadMetadataAsync(metadataFilePath, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Metadata load failure shouldn't fail the entire operation
                        UnityEngine.Debug.LogWarning($"Failed to load metadata for '{saveId}': {ex.Message}");
                    }
                }
                
                stopwatch.Stop();
                _statistics.TotalLoads++;
                _statistics.RecordOperation(true, stopwatch.Elapsed, data.Length, false);
                
                return StorageLoadResult.CreateSuccess(data, metadata, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordOperation(false, stopwatch.Elapsed);
                return StorageLoadResult.CreateFailure($"Failed to load '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        private async UniTask<SaveMetadata> LoadMetadataAsync(string metadataFilePath, CancellationToken cancellationToken)
        {
            var metadataBytes = await ReadFileAsync(metadataFilePath, cancellationToken);
            var metadataJson = System.Text.Encoding.UTF8.GetString(metadataBytes);
            return JsonUtility.FromJson<SaveMetadata>(metadataJson);
        }
        #endregion
        
        #region Delete Operations
        public async UniTask<StorageOperationResult> DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                
                var saveFilePath = GetSaveFilePath(saveId);
                var metadataFilePath = GetMetadataFilePath(saveId);
                
                bool deleted = await DeleteFilesAsync(saveFilePath, metadataFilePath, cancellationToken);
                
                stopwatch.Stop();
                _statistics.TotalDeletes++;
                _statistics.RecordOperation(true, stopwatch.Elapsed);
                
                if (!deleted)
                {
                    return StorageOperationResult.CreateFailure($"Save file '{saveId}' not found", duration: stopwatch.Elapsed);
                }
                
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordOperation(false, stopwatch.Elapsed);
                return StorageOperationResult.CreateFailure($"Failed to delete '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region Existence Check
        public async UniTask<StorageExistsResult> ExistsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                
                var saveFilePath = GetSaveFilePath(saveId);
                
                bool exists = await CheckFileExistsAsync(saveFilePath, cancellationToken);
                
                stopwatch.Stop();
                return StorageExistsResult.CreateSuccess(exists, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageExistsResult.CreateFailure($"Failed to check existence of '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region List Operations
        public async UniTask<StorageListResult> GetSaveListAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                
                var saveFiles = await GetSaveFilesAsync(cancellationToken);
                
                var saveList = new List<SaveMetadata>();
                
                foreach (var filePath in saveFiles)
                {
                    try
                    {
                        var saveId = Path.GetFileNameWithoutExtension(filePath);
                        var fileInfo = new FileInfo(filePath);
                        var metadataFilePath = GetMetadataFilePath(saveId);
                        
                        SaveMetadata metadata = null;
                        if (File.Exists(metadataFilePath))
                        {
                            try
                            {
                                metadata = await LoadMetadataAsync(metadataFilePath, cancellationToken);
                            }
                            catch
                            {
                                // If metadata loading fails, create basic metadata
                                metadata = CreateBasicMetadata(saveId, fileInfo);
                            }
                        }
                        else
                        {
                            metadata = CreateBasicMetadata(saveId, fileInfo);
                        }
                        
                        saveList.Add(metadata);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other files
                        UnityEngine.Debug.LogWarning($"Failed to process save file '{filePath}': {ex.Message}");
                    }
                }
                
                stopwatch.Stop();
                return StorageListResult.CreateSuccess(saveList.ToArray(), stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageListResult.CreateFailure($"Failed to get save list: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        private SaveMetadata CreateBasicMetadata(string saveId, FileInfo fileInfo)
        {
            return new SaveMetadata
            {
                SaveId = saveId,
                CreatedAt = fileInfo.CreationTime,
                ModifiedAt = fileInfo.LastWriteTime,
                FileSize = fileInfo.Length,
                SaveVersion = 1
            };
        }
        #endregion
        
        #region Metadata Operations
        public async UniTask<StorageMetadataResult> GetMetadataAsync(string saveId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                
                var saveFilePath = GetSaveFilePath(saveId);
                var metadataFilePath = GetMetadataFilePath(saveId);
                
                if (!File.Exists(saveFilePath))
                {
                    stopwatch.Stop();
                    return StorageMetadataResult.CreateFailure($"Save file '{saveId}' not found", duration: stopwatch.Elapsed);
                }
                
                SaveMetadata metadata;
                
                if (File.Exists(metadataFilePath))
                {
                    metadata = await LoadMetadataAsync(metadataFilePath, cancellationToken);
                }
                else
                {
                    var fileInfo = new FileInfo(saveFilePath);
                    metadata = CreateBasicMetadata(saveId, fileInfo);
                }
                
                stopwatch.Stop();
                return StorageMetadataResult.CreateSuccess(metadata, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageMetadataResult.CreateFailure($"Failed to get metadata for '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region Backup Operations
        public async UniTask<StorageOperationResult> CreateBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                ValidateSaveId(backupId);
                
                var saveFilePath = GetSaveFilePath(saveId);
                var backupFilePath = GetBackupFilePath(backupId);
                var saveMetadataPath = GetMetadataFilePath(saveId);
                var backupMetadataPath = GetBackupMetadataFilePath(backupId);
                
                if (!File.Exists(saveFilePath))
                {
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Save file '{saveId}' not found", duration: stopwatch.Elapsed);
                }
                
                // Ensure backup directory exists
                var backupDirectory = Path.GetDirectoryName(backupFilePath);
                if (!Directory.Exists(backupDirectory))
                    Directory.CreateDirectory(backupDirectory);
                
                await CopyFilesForBackupAsync(saveFilePath, backupFilePath, saveMetadataPath, backupMetadataPath, cancellationToken);
                
                stopwatch.Stop();
                _statistics.TotalBackups++;
                _statistics.RecordOperation(true, stopwatch.Elapsed);
                
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordOperation(false, stopwatch.Elapsed);
                return StorageOperationResult.CreateFailure($"Failed to create backup '{backupId}' for '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> RestoreFromBackupAsync(string backupId, string saveId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                ValidateSaveId(saveId);
                ValidateSaveId(backupId);
                
                var backupFilePath = GetBackupFilePath(backupId);
                var saveFilePath = GetSaveFilePath(saveId);
                var backupMetadataPath = GetBackupMetadataFilePath(backupId);
                var saveMetadataPath = GetMetadataFilePath(saveId);
                
                if (!File.Exists(backupFilePath))
                {
                    stopwatch.Stop();
                    return StorageOperationResult.CreateFailure($"Backup file '{backupId}' not found", duration: stopwatch.Elapsed);
                }
                
                // Ensure save directory exists
                var saveDirectory = Path.GetDirectoryName(saveFilePath);
                if (!Directory.Exists(saveDirectory))
                    Directory.CreateDirectory(saveDirectory);
                
                await RestoreFilesFromBackupAsync(backupFilePath, saveFilePath, backupMetadataPath, saveMetadataPath, cancellationToken);
                
                stopwatch.Stop();
                _statistics.TotalRestores++;
                _statistics.RecordOperation(true, stopwatch.Elapsed);
                
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordOperation(false, stopwatch.Elapsed);
                return StorageOperationResult.CreateFailure($"Failed to restore from backup '{backupId}' to '{saveId}': {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region Health Check
        public async UniTask<StorageHealthResult> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var healthMetrics = new Dictionary<string, object>();
                var status = StorageHealthStatus.Healthy;
                var statusMessage = "Storage is healthy";
                
                // Check directory existence
                if (!Directory.Exists(_saveDirectory))
                {
                    status = StorageHealthStatus.Unhealthy;
                    statusMessage = "Save directory does not exist";
                }
                else
                {
                    // Check write permissions
                    var testFile = Path.Combine(_saveDirectory, $"health_test_{Guid.NewGuid():N}.tmp");
                    try
                    {
                        await TestWritePermissionsAsync(testFile, cancellationToken);
                        
                        healthMetrics["write_test"] = "passed";
                    }
                    catch (Exception ex)
                    {
                        status = StorageHealthStatus.Unhealthy;
                        statusMessage = $"Write permission test failed: {ex.Message}";
                        healthMetrics["write_test"] = "failed";
                    }
                    
                    // Check available space
                    try
                    {
                        var driveInfo = new DriveInfo(Path.GetPathRoot(_saveDirectory));
                        var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        healthMetrics["free_space_gb"] = freeSpaceGB;
                        
                        if (freeSpaceGB < 0.1) // Less than 100MB
                        {
                            status = StorageHealthStatus.Degraded;
                            statusMessage = "Low disk space";
                        }
                    }
                    catch (Exception ex)
                    {
                        healthMetrics["disk_space_check"] = $"failed: {ex.Message}";
                    }
                }
                
                // Add statistics
                healthMetrics["total_saves"] = _statistics.TotalSaves;
                healthMetrics["total_loads"] = _statistics.TotalLoads;
                healthMetrics["success_rate"] = _statistics.SuccessRate;
                healthMetrics["average_operation_time_ms"] = _statistics.AverageOperationTime.TotalMilliseconds;
                
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
        #endregion
        
        #region Cleanup
        public async UniTask<StorageOperationResult> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ValidateInitialization();
                
                int filesDeleted = await CleanupTempFilesAsync(cancellationToken);
                
                stopwatch.Stop();
                return StorageOperationResult.CreateSuccess(stopwatch.Elapsed, filesDeleted);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return StorageOperationResult.CreateFailure($"Cleanup failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        #endregion
        
        #region Statistics
        public StorageProviderStatistics GetStatistics()
        {
            return _statistics;
        }
        #endregion
        
        #region Shutdown
        public async UniTask<StorageOperationResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Perform cleanup before shutdown
                await CleanupAsync(cancellationToken);
                
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
        
        #region Helper Methods
        private void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("LocalFileStorage is not initialized");
        }
        
        private void ValidateSaveId(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId))
                throw new ArgumentException("Save ID cannot be null or empty", nameof(saveId));
            
            if (saveId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Save ID contains invalid characters", nameof(saveId));
        }
        
        private string GetSaveFilePath(string saveId)
        {
            return Path.Combine(_saveDirectory, $"{saveId}{SAVE_FILE_EXTENSION}");
        }
        
        private string GetTempFilePath(string saveId)
        {
            return Path.Combine(_saveDirectory, $"temp_{saveId}_{Guid.NewGuid():N}{TEMP_FILE_EXTENSION}");
        }
        
        private string GetMetadataFilePath(string saveId)
        {
            return Path.Combine(_saveDirectory, $"{saveId}{METADATA_FILE_EXTENSION}");
        }
        
        private string GetBackupFilePath(string backupId)
        {
            return Path.Combine(_backupDirectory, $"{backupId}{BACKUP_FILE_EXTENSION}");
        }
        
        private string GetBackupMetadataFilePath(string backupId)
        {
            return Path.Combine(_backupDirectory, $"{backupId}{METADATA_FILE_EXTENSION}");
        }
        
        private async UniTask<bool> DeleteFilesAsync(string saveFilePath, string metadataFilePath, CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                bool fileDeleted = false;

                if (File.Exists(saveFilePath))
                {
                    File.Delete(saveFilePath);
                    fileDeleted = true;
                }

                if (File.Exists(metadataFilePath))
                {
                    File.Delete(metadataFilePath);
                }

                return fileDeleted;
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask<int> CleanupTempFilesAsync(CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                int deletedCount = 0;

                if (!Directory.Exists(_saveDirectory))
                    return deletedCount;

                var tempFiles = Directory.GetFiles(_saveDirectory, $"*{TEMP_FILE_EXTENSION}");
                var cutoffTime = DateTime.Now - TimeSpan.FromHours(1);

                foreach (var tempFile in tempFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(tempFile);
                        if (fileInfo.CreationTime < cutoffTime)
                        {
                            File.Delete(tempFile);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to delete temp file '{tempFile}': {ex.Message}");
                    }
                }

                return deletedCount;
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask CreateDirectoriesAsync(CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                Directory.CreateDirectory(_saveDirectory);
                Directory.CreateDirectory(_backupDirectory);
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask TestWritePermissionsAsync(string testFile, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });
                File.Delete(testFile);
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask WriteToTempFileAsync(string tempFilePath, byte[] data, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fileStream.Write(data, 0, data.Length);
                    fileStream.Flush();
                }
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask MoveFileAtomicallyAsync(string tempFilePath, string finalFilePath, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                File.Move(tempFilePath, finalFilePath);
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask WriteMetadataFileAsync(string metadataFilePath, byte[] metadataBytes, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllBytes(metadataFilePath, metadataBytes);
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask<byte[]> ReadFileAsync(string filePath, CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() => File.ReadAllBytes(filePath), cancellationToken: cancellationToken);
        }
        
        private async UniTask<bool> CheckFileExistsAsync(string filePath, CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() => File.Exists(filePath), cancellationToken: cancellationToken);
        }
        
        private async UniTask<string[]> GetSaveFilesAsync(CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                if (!Directory.Exists(_saveDirectory))
                    return new string[0];
                
                return Directory.GetFiles(_saveDirectory, $"*{SAVE_FILE_EXTENSION}")
                    .Where(f => !Path.GetFileName(f).StartsWith("temp_"))
                    .ToArray();
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask CopyFilesForBackupAsync(string saveFilePath, string backupFilePath, string saveMetadataPath, string backupMetadataPath, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                // Copy save file
                File.Copy(saveFilePath, backupFilePath, true);
                
                // Copy metadata if exists
                if (File.Exists(saveMetadataPath))
                {
                    File.Copy(saveMetadataPath, backupMetadataPath, true);
                }
            }, cancellationToken: cancellationToken);
        }
        
        private async UniTask RestoreFilesFromBackupAsync(string backupFilePath, string saveFilePath, string backupMetadataPath, string saveMetadataPath, CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                // Copy backup to save location
                File.Copy(backupFilePath, saveFilePath, true);
                
                // Copy metadata if exists
                if (File.Exists(backupMetadataPath))
                {
                    File.Copy(backupMetadataPath, saveMetadataPath, true);
                }
            }, cancellationToken: cancellationToken);
        }
        #endregion
    }
}
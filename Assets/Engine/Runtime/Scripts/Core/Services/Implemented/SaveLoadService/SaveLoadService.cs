using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using ZLinq;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Sinkii09.Engine.Services
{
    [EngineService(ServiceCategory.Core, ServicePriority.Critical,
        Description = "Manages save/load operations with compression, encryption, and multi-platform storage support")]
    [ServiceConfiguration(typeof(SaveLoadServiceConfiguration))]
    public class SaveLoadService : ISaveLoadService
    {
        #region Private Fields
        private readonly SaveLoadServiceConfiguration _config;
        private readonly SaveLoadServiceStatistics _statistics;
        private IServiceProvider _serviceProvider;

        // Serialization system components
        private IBinarySerializer _serializer;
        private SaveDataValidator _validator;
        private SaveDataProviderManager _providerManager;
        private SerializationPerformanceMonitor _performanceMonitor;

        // Storage system components
        private StorageManager _storageManager;
        private StorageHealthMonitor _healthMonitor;

        // Service state tracking
        private bool _isInitialized;
        private bool _isDisposed;
        #endregion

        #region Events
        public event Action<SaveEventArgs> OnSaveStarted;
        public event Action<SaveEventArgs> OnSaveCompleted;
        public event Action<SaveErrorEventArgs> OnSaveFailed;
        public event Action<LoadEventArgs> OnLoadStarted;
        public event Action<LoadEventArgs> OnLoadCompleted;
        public event Action<LoadErrorEventArgs> OnLoadFailed;
        #endregion

        #region Constructor
        public SaveLoadService(SaveLoadServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _statistics = new SaveLoadServiceStatistics();
            _isInitialized = false;
            _isDisposed = false;
        }
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isInitialized)
                {
                    return ServiceInitializationResult.Success();
                }

                if (_config == null)
                {
                    return ServiceInitializationResult.Failed("Configuration is required");
                }

                // Store service provider reference
                _serviceProvider = provider;

                // Validate configuration
                if (!_config.Validate(out var validationErrors))
                {
                    var errorMessage = string.Join("; ", validationErrors);
                    return ServiceInitializationResult.Failed($"Configuration validation failed: {errorMessage}");
                }

                // Initialize statistics
                _statistics.ServiceStartTime = DateTime.UtcNow;
                _statistics.Reset();

                // Initialize serialization system
                _serializer = new GameDataSerializer();
                _validator = new SaveDataValidator();
                _providerManager = new SaveDataProviderManager();
                _performanceMonitor = new SerializationPerformanceMonitor();

                // Initialize storage system
                _storageManager = new StorageManager();
                _healthMonitor = new StorageHealthMonitor();

                // Configure storage providers based on configuration
                await InitializeStorageProvidersAsync(_config.EnabledStorageProviders, cancellationToken);

                // Initialize storage manager
                var storageInitResult = await _storageManager.InitializeAsync(new StorageProviderConfiguration
                {
                    BasePath = _config.SaveDirectoryPath ?? "SaveData"
                }, cancellationToken);

                if (!storageInitResult.Success)
                {
                    return ServiceInitializationResult.Failed($"Storage initialization failed: {storageInitResult.ErrorMessage}");
                }

                // Start health monitoring
                await _healthMonitor.StartMonitoringAsync(cancellationToken);

                _isInitialized = true;
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed(ex.Message, ex);
            }
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isDisposed)
                {
                    return ServiceShutdownResult.Success();
                }

                // Shutdown storage system
                if (_storageManager != null)
                {
                    await _storageManager.ShutdownAsync(cancellationToken);
                }

                // Stop health monitoring
                _healthMonitor?.StopMonitoring();
                _healthMonitor?.Dispose();

                _isDisposed = true;

                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed(ex.Message, ex);
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isInitialized)
                {
                    return ServiceHealthStatus.Unhealthy("Service not initialized");
                }

                if (_isDisposed)
                {
                    return ServiceHealthStatus.Unhealthy("Service disposed");
                }

                // Check storage system health
                var storageHealthResult = await _storageManager.HealthCheckAsync(cancellationToken);
                if (!storageHealthResult.Success)
                {
                    return ServiceHealthStatus.Unhealthy($"Storage system unhealthy: {storageHealthResult.ErrorMessage}");
                }
                
                if (storageHealthResult.Status == StorageHealthStatus.Unhealthy)
                {
                    return ServiceHealthStatus.Unhealthy($"Storage system unhealthy: {storageHealthResult.StatusMessage}");
                }

                // Check health monitoring status
                var healthSummary = _healthMonitor.GetHealthSummary();
                if (healthSummary.UnhealthyProviders > 0)
                {
                    return ServiceHealthStatus.Degraded($"Storage health degraded: {healthSummary.UnhealthyProviders} unhealthy providers");
                }

                var healthInfo = $"Saves: {_statistics.TotalSaves}, " +
                                $"Loads: {_statistics.TotalLoads}, " +
                                $"Success rate: {(_statistics.TotalSaves + _statistics.TotalLoads > 0 ? (double)(_statistics.SuccessfulSaves + _statistics.SuccessfulLoads) / (_statistics.TotalSaves + _statistics.TotalLoads) * 100 : 0):F1}%";

                return ServiceHealthStatus.Healthy(healthInfo);
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }
        #endregion

        #region ISaveLoadService Implementation
        public async UniTask<SaveResult> SaveAsync(string saveId, SaveData data, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                OnSaveStarted?.Invoke(new SaveEventArgs(saveId));
                _statistics.TotalSaves++;

                // Get save data provider
                var provider = _providerManager.GetProvider(data);
                if (provider == null)
                {
                    throw new InvalidOperationException($"No provider found for save data type {data.GetType().Name}");
                }

                // Validate save data
                if (_config.EnableValidation && _config.ValidateOnSave)
                {
                    var validationResult = await provider.ValidateAsync(data, cancellationToken);
                    if (!validationResult.IsValid)
                    {
                        throw new InvalidOperationException($"Save data validation failed: {string.Join(", ", validationResult.Errors)}");
                    }
                }

                // Pre-process save data
                data = await provider.PreProcessAsync(data, cancellationToken);

                // Create serialization context
                var context = SerializationContext.ForSave(saveId, data.GetType(),
                    _config.EnableCompression, _config.EnableValidation);

                // Serialize data
                var serializationResult = await _serializer.SerializeAsync(data, context, cancellationToken);
                if (!serializationResult.Success)
                {
                    throw new InvalidOperationException($"Serialization failed: {serializationResult.ErrorMessage}");
                }

                // Create save metadata
                var metadata = new SaveMetadata
                {
                    SaveId = saveId,
                    SaveType = data.GetType().Name,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    SaveVersion = 1,
                    FileSize = serializationResult.Data.Length,
                    IsCompressed = _config.EnableCompression
                };

                // Save to storage
                var saveResult = await _storageManager.SaveAsync(saveId, serializationResult.Data, metadata, cancellationToken);
                if (!saveResult.Success)
                {
                    throw new InvalidOperationException($"Storage save failed: {saveResult.ErrorMessage}");
                }

                // Record performance metrics
                _performanceMonitor.RecordMetrics(serializationResult.Metrics, data.GetType().Name);
                _statistics.TotalBytesWritten += serializationResult.Data.Length;
                _statistics.TotalBytesCompressed += serializationResult.SerializedSize;
                _statistics.AverageCompressionRatio = _config.EnableCompression ?
                    (double)serializationResult.SerializedSize / serializationResult.OriginalSize : 1.0;

                stopwatch.Stop();
                _statistics.SuccessfulSaves++;
                _statistics.TotalSaveTime = _statistics.TotalSaveTime.Add(stopwatch.Elapsed);
                _statistics.LastSaveTime = DateTime.UtcNow;

                var result = SaveResult.CreateSuccess(saveId, stopwatch.Elapsed,
                    serializationResult.OriginalSize, serializationResult.SerializedSize);
                OnSaveCompleted?.Invoke(new SaveEventArgs(saveId, result.UncompressedSize));

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.FailedSaves++;

                var result = SaveResult.CreateFailure(saveId, ex, stopwatch.Elapsed);
                OnSaveFailed?.Invoke(new SaveErrorEventArgs(saveId, ex));

                if (_config.EnableDebugLogging)
                {
                    Debug.LogError($"Save failed for '{saveId}': {ex.Message}");
                }

                return result;
            }
        }

        public async UniTask<LoadResult<T>> LoadAsync<T>(string saveId, CancellationToken cancellationToken = default) where T : SaveData
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                OnLoadStarted?.Invoke(new LoadEventArgs(saveId));
                _statistics.TotalLoads++;

                // Load data from storage
                var loadResult = await _storageManager.LoadAsync(saveId, cancellationToken);
                if (!loadResult.Success)
                {
                    throw new InvalidOperationException($"Storage load failed: {loadResult.ErrorMessage}");
                }

                // Create serialization context for loading
                var context = SerializationContext.ForLoad(saveId, typeof(T), _config.EnableValidation);

                // Deserialize data
                var deserializationResult = await _serializer.DeserializeAsync<T>(loadResult.Data, context, cancellationToken);
                if (!deserializationResult.Success)
                {
                    throw new InvalidOperationException($"Deserialization failed: {deserializationResult.ErrorMessage}");
                }

                // Update statistics
                _statistics.TotalBytesRead += loadResult.Data.Length;

                var loadedData = deserializationResult.Data;

                // Get provider for post-processing
                var provider = _providerManager.GetProvider(loadedData);
                if (provider != null)
                {
                    var value = await provider.PostProcessAsync(loadedData, cancellationToken);
                    loadedData = (T)value;
                }

                // Record performance metrics
                _performanceMonitor.RecordMetrics(deserializationResult.Metrics, typeof(T).Name);

                stopwatch.Stop();
                _statistics.SuccessfulLoads++;
                _statistics.TotalLoadTime = _statistics.TotalLoadTime.Add(stopwatch.Elapsed);
                _statistics.LastLoadTime = DateTime.UtcNow;

                var result = LoadResult<T>.CreateSuccess(loadedData, saveId, stopwatch.Elapsed, deserializationResult.DeserializedSize);
                OnLoadCompleted?.Invoke(new LoadEventArgs(saveId));

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.FailedLoads++;

                var result = LoadResult<T>.CreateFailure(saveId, ex, stopwatch.Elapsed);
                OnLoadFailed?.Invoke(new LoadErrorEventArgs(saveId, ex));

                if (_config.EnableDebugLogging)
                {
                    Debug.LogError($"Load failed for '{saveId}': {ex.Message}");
                }

                return result;
            }
        }

        public async UniTask<bool> ExistsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            var existsResult = await _storageManager.ExistsAsync(saveId, cancellationToken);
            return existsResult.Success && existsResult.Exists;
        }

        public async UniTask<bool> DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            var deleteResult = await _storageManager.DeleteAsync(saveId, cancellationToken);
            if (deleteResult.Success)
            {
                _statistics.TotalDeletes++;
                return true;
            }

            if (_config.EnableDebugLogging)
            {
                Debug.LogError($"Delete failed for '{saveId}': {deleteResult.ErrorMessage}");
            }

            return false;
        }

        public async UniTask<IReadOnlyList<SaveMetadata>> GetAllSavesAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            var listResult = await _storageManager.GetSaveListAsync(cancellationToken);
            if (listResult.Success)
            {
                return listResult.SaveList;
            }

            if (_config.EnableDebugLogging)
            {
                Debug.LogError($"Failed to get save list: {listResult.ErrorMessage}");
            }

            return new List<SaveMetadata>();
        }

        public async UniTask<SaveMetadata> GetSaveMetadataAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            var metadataResult = await _storageManager.GetMetadataAsync(saveId, cancellationToken);
            if (metadataResult.Success)
            {
                return metadataResult.Metadata;
            }

            if (_config.EnableDebugLogging)
            {
                Debug.LogError($"Failed to get metadata for '{saveId}': {metadataResult.ErrorMessage}");
            }

            return null;
        }

        public async UniTask<bool> ValidateSaveAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            try
            {
                // Check if save exists
                var existsResult = await _storageManager.ExistsAsync(saveId, cancellationToken);
                if (!existsResult.Success || !existsResult.Exists)
                {
                    return false;
                }

                // Try to load and validate the save
                var loadResult = await _storageManager.LoadAsync(saveId, cancellationToken);
                if (!loadResult.Success)
                {
                    return false;
                }

                // Validate the loaded data structure
                if (loadResult.Data == null || loadResult.Data.Length == 0)
                {
                    return false;
                }

                // Additional validation could be added here
                // For example, checking data integrity, version compatibility, etc.

                return true;
            }
            catch (Exception ex)
            {
                if (_config.EnableDebugLogging)
                {
                    Debug.LogError($"Validation failed for '{saveId}': {ex.Message}");
                }
                return false;
            }
        }

        public async UniTask<SaveResult> AutoSaveAsync(SaveData data, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableAutoSave)
                throw new InvalidOperationException("Auto-save is disabled in configuration");

            var autoSaveId = $"autosave_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            return await SaveAsync(autoSaveId, data, cancellationToken);
        }

        public async UniTask<LoadResult<T>> LoadLatestAsync<T>(CancellationToken cancellationToken = default) where T : SaveData
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            var saves = await GetAllSavesAsync(cancellationToken);
            if (saves.Count == 0)
                return LoadResult<T>.CreateNotFound("latest", TimeSpan.Zero);

            // Find the latest save based on modification time
            var latestSave = saves
                .AsValueEnumerable()
                .Where(s => !s.SaveId.Contains("_backup_")) // Exclude backups
                .OrderByDescending(s => s.ModifiedAt)
                .FirstOrDefault();

            if (latestSave == null)
                return LoadResult<T>.CreateNotFound("latest", TimeSpan.Zero);

            return await LoadAsync<T>(latestSave.SaveId, cancellationToken);
        }

        public async UniTask<bool> CreateBackupAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            var backupId = $"{saveId}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var backupResult = await _storageManager.CreateBackupAsync(saveId, backupId, cancellationToken);

            if (backupResult.Success)
            {
                _statistics.TotalBackups++;
                return true;
            }

            if (_config.EnableDebugLogging)
            {
                Debug.LogError($"Backup creation failed for '{saveId}': {backupResult.ErrorMessage}");
            }

            return false;
        }

        public async UniTask<bool> RestoreBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            if (string.IsNullOrEmpty(backupId))
                throw new ArgumentException("BackupId cannot be null or empty", nameof(backupId));

            var restoreResult = await _storageManager.RestoreFromBackupAsync(backupId, saveId, cancellationToken);

            if (restoreResult.Success)
            {
                _statistics.TotalRestores++;
                return true;
            }

            if (_config.EnableDebugLogging)
            {
                Debug.LogError($"Backup restore failed for '{saveId}' from '{backupId}': {restoreResult.ErrorMessage}");
            }

            return false;
        }

        public async UniTask<IReadOnlyList<string>> GetBackupsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // Get all saves and filter for backups of the specified saveId
            var allSaves = await GetAllSavesAsync(cancellationToken);
            var backupPrefix = $"{saveId}_backup_";

            var backups = allSaves
                .AsValueEnumerable()
                .Where(metadata => metadata.SaveId.StartsWith(backupPrefix))
                .Select(metadata => metadata.SaveId)
                .ToList();

            return backups;
        }

        public SaveLoadServiceStatistics GetStatistics()
        {
            return _statistics;
        }

        public void ResetStatistics()
        {
            _statistics.Reset();
        }

        /// <summary>
        /// Get serialization performance summary
        /// </summary>
        public PerformanceSummary GetPerformanceSummary()
        {
            return _performanceMonitor?.GetOverallSummary() ?? new PerformanceSummary();
        }

        /// <summary>
        /// Generate performance report
        /// </summary>
        public string GeneratePerformanceReport()
        {
            return _performanceMonitor?.GenerateReport() ?? "Performance monitoring not available";
        }

        private byte[] CreateTestData<T>()
        {
            // Create test data for demonstrating serialization pipeline
            // In real implementation, this would load from storage
            try
            {
                if (typeof(T) == typeof(GameSaveData))
                {
                    var gameData = new GameSaveData
                    {
                        CurrentSceneName = "TestScene",
                        CurrentLevel = 1,
                        GameMode = "Story",
                        Difficulty = GameDifficulty.Normal
                    };
                    gameData.UnlockLevel("Level1");
                    gameData.SetFlag("tutorial_completed", true);

                    var context = SerializationContext.ForSave("test", typeof(GameSaveData));
                    var result = _serializer.SerializeAsync(gameData, context).GetAwaiter().GetResult();
                    return result.Success ? result.Data : new byte[0];
                }
                else if (typeof(T) == typeof(PlayerSaveData))
                {
                    var playerData = new PlayerSaveData("TestPlayer")
                    {
                        Level = 5,
                        Experience = 1000,
                        Currency = 500
                    };
                    playerData.AddItem(new InventoryItem("sword1", "Iron Sword", "Weapon"));
                    playerData.UnlockAchievement("first_kill");

                    var context = SerializationContext.ForSave("test", typeof(PlayerSaveData));
                    var result = _serializer.SerializeAsync(playerData, context).GetAwaiter().GetResult();
                    return result.Success ? result.Data : new byte[0];
                }

                return new byte[0];
            }
            catch
            {
                return new byte[0];
            }
        }
        #endregion

        #region Private Helper Methods
        private async UniTask InitializeStorageProvidersAsync(StorageProviderType enabledProviders, CancellationToken cancellationToken)
        {
            var registry = StorageProviderRegistry.Instance;
            var availableProviders = registry.GetAvailableProviders();
            
            foreach (var providerType in Enum.GetValues(typeof(StorageProviderType)).AsValueEnumerable().Cast<StorageProviderType>())
            {
                // Skip None and check if provider is enabled
                if (providerType == StorageProviderType.None || !enabledProviders.HasFlag(providerType))
                    continue;
                
                // Check if provider is available on this platform
                if (!availableProviders.AsValueEnumerable().Contains(providerType))
                {
                    Debug.LogWarning($"Storage provider '{providerType}' is not available on this platform");
                    continue;
                }
                
                try
                {
                    // Create provider instance
                    var provider = registry.CreateProvider(providerType);
                    
                    // Create configuration for provider
                    var storageConfig = new StorageProviderConfiguration
                    {
                        ProviderType = providerType,
                        BasePath = _config.SaveDirectoryPath ?? "SaveData"
                    };
                    
                    // Add provider-specific settings
                    ConfigureProviderSettings(storageConfig, providerType);
                    
                    // Register provider
                    _storageManager.RegisterProvider(provider, storageConfig);
                    _healthMonitor.RegisterProvider(provider);
                    
                    Debug.Log($"Registered storage provider: {providerType}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize storage provider '{providerType}': {ex.Message}");
                }
            }
            await UniTask.CompletedTask;
        }
        
        private void ConfigureProviderSettings(StorageProviderConfiguration config, StorageProviderType providerType)
        {
            // Add provider-specific settings based on type
            switch (providerType)
            {
                case StorageProviderType.LocalFile:
                    config.Settings["MaxFileSize"] = 10 * 1024 * 1024; // 10MB
                    config.Settings["EnableCompression"] = _config.EnableCompression;
                    break;
                    
                case StorageProviderType.CloudStorage:
                    config.Settings["SyncInterval"] = 300; // 5 minutes
                    config.Settings["EnableOfflineCache"] = true;
                    break;
                    
                case StorageProviderType.PlayerPrefs:
                    config.Settings["KeyPrefix"] = "SaveData_";
                    config.Settings["MaxKeyLength"] = 255;
                    break;
                    
                case StorageProviderType.Steam:
                    config.Settings["CloudEnabled"] = true;
                    config.Settings["AutoSync"] = true;
                    break;
                    
                // Add more provider-specific configurations as needed
            }
        }
        #endregion
    }
}
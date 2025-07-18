using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
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
                // Add a minimal await to avoid CS1998 warning
                await UniTask.Yield();

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

                Debug.Log("SaveLoadService serialization system initialized");

                _isInitialized = true;
                Debug.Log($"SaveLoadService initialized successfully with {_config.EnabledStorageProviders.Count} storage providers");
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveLoadService initialization failed: {ex.Message}");
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

                await UniTask.Yield();
                // TODO: Shutdown storage providers
                // TODO: Complete any pending operations
                // TODO: Cleanup resources

                _isDisposed = true;
                
                Debug.Log("SaveLoadService shutdown completed");
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveLoadService shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex.Message, ex);
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync()
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

                await UniTask.Yield(); // Simulate async operation
                // TODO: Add more comprehensive health checks
                // - Check storage provider health
                // - Check available disk space
                // - Check configuration validity

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

                // Record performance metrics
                _performanceMonitor.RecordMetrics(serializationResult.Metrics, data.GetType().Name);

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

                // Create serialization context for loading
                var context = SerializationContext.ForLoad(saveId, typeof(T), _config.EnableValidation);

                // TODO: Load actual data from storage provider
                // For now, create placeholder data to test serialization pipeline
                byte[] savedData = CreateTestData<T>();

                // Deserialize data
                var deserializationResult = await _serializer.DeserializeAsync<T>(savedData, context, cancellationToken);
                if (!deserializationResult.Success)
                {
                    throw new InvalidOperationException($"Deserialization failed: {deserializationResult.ErrorMessage}");
                }

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

            // TODO: Implement actual exists check
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            return false;
        }

        public async UniTask<bool> DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // TODO: Implement actual delete logic
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            _statistics.TotalDeletes++;
            return true;
        }

        public async UniTask<IReadOnlyList<SaveMetadata>> GetAllSavesAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            // TODO: Implement actual metadata retrieval
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            return new List<SaveMetadata>();
        }

        public async UniTask<SaveMetadata> GetSaveMetadataAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // TODO: Implement actual metadata retrieval
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            return null;
        }

        public async UniTask<bool> ValidateSaveAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // TODO: Implement actual validation
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            return true;
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

            // TODO: Find actual latest save
            return LoadResult<T>.CreateNotFound("latest", TimeSpan.Zero);
        }

        public async UniTask<bool> CreateBackupAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // TODO: Implement actual backup creation
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            _statistics.TotalBackups++;
            return true;
        }

        public async UniTask<bool> RestoreBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            if (string.IsNullOrEmpty(backupId))
                throw new ArgumentException("BackupId cannot be null or empty", nameof(backupId));

            // TODO: Implement actual backup restore
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            _statistics.TotalRestores++;
            return true;
        }

        public async UniTask<IReadOnlyList<string>> GetBackupsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("SaveLoadService is not initialized");

            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("SaveId cannot be null or empty", nameof(saveId));

            // TODO: Implement actual backup listing
            await UniTask.Delay(10, cancellationToken: cancellationToken);
            return new List<string>();
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
    }
}
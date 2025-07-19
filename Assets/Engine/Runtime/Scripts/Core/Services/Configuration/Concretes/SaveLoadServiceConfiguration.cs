using System;
using System.Collections.Generic;
using UnityEngine;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Services
{
    [CreateAssetMenu(fileName = "SaveLoadServiceConfiguration", menuName = "Engine/Services/SaveLoadService Configuration")]
    public class SaveLoadServiceConfiguration : ServiceConfigurationBase
    {
        [Header("Storage Settings")]
        [SerializeField] private string _saveDirectoryPath = "SaveData";
        [SerializeField] private string _backupDirectoryPath = "SaveData/Backups";
        [SerializeField] private StorageProviderType _enabledStorageProviders = StorageProviderType.LocalFile;
        [SerializeField] private int _maxSaveSlots = 10;
        [SerializeField] private int _maxBackupsPerSave = 5;
        
        [Header("Performance Settings")]
        [SerializeField] private bool _enableCaching = true;
        [SerializeField] private int _maxCacheSize = 50;
        [SerializeField] private bool _enableCompression = true;
        [SerializeField] private int _compressionLevel = 6;
        [SerializeField] private bool _enableBackgroundSaving = true;
        [SerializeField] private int _maxConcurrentOperations = 4;
        
        [Header("Security Settings")]
        [SerializeField] private SecurityConfiguration _securityConfiguration;
        [SerializeField] private bool _enableEncryption = false;
        [SerializeField] private bool _enableIntegrityChecks = true;
        [SerializeField] private bool _enableMagicBytes = true;
        
        [Header("Auto-Save Settings")]
        [SerializeField] private bool _enableAutoSave = true;
        [SerializeField] private float _autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private int _maxAutoSaves = 3;
        [SerializeField] private bool _autoSaveOnSceneChange = true;
        [SerializeField] private bool _autoSaveOnApplicationPause = true;
        
        [Header("Validation Settings")]
        [SerializeField] private bool _enableValidation = true;
        [SerializeField] private bool _validateOnLoad = true;
        [SerializeField] private bool _validateOnSave = true;
        [SerializeField] private bool _strictValidation = false;
        
        [Header("Error Handling")]
        [SerializeField] private int _maxRetryAttempts = 3;
        [SerializeField] private float _retryDelay = 1f;
        [SerializeField] private bool _enableCircuitBreaker = true;
        [SerializeField] private int _circuitBreakerThreshold = 5;
        [SerializeField] private float _circuitBreakerTimeout = 60f;
        
        [Header("Debug Settings")]
        [SerializeField] private bool _enableDebugLogging = false;
        [SerializeField] private bool _enablePerformanceMetrics = true;
        [SerializeField] private bool _enableStatistics = true;

        public string SaveDirectoryPath => _saveDirectoryPath;
        public string BackupDirectoryPath => _backupDirectoryPath;
        public StorageProviderType EnabledStorageProviders => _enabledStorageProviders;
        public int MaxSaveSlots => _maxSaveSlots;
        public int MaxBackupsPerSave => _maxBackupsPerSave;
        
        public bool EnableCaching => _enableCaching;
        public int MaxCacheSize => _maxCacheSize;
        public bool EnableCompression => _enableCompression;
        public int CompressionLevel => _compressionLevel;
        public bool EnableBackgroundSaving => _enableBackgroundSaving;
        public int MaxConcurrentOperations => _maxConcurrentOperations;
        
        public SecurityConfiguration SecurityConfiguration => _securityConfiguration;
        public bool EnableEncryption => _enableEncryption || (_securityConfiguration?.EnableEncryption ?? false);
        public bool EnableIntegrityChecks => _enableIntegrityChecks;
        public bool EnableMagicBytes => _enableMagicBytes;
        
        public bool EnableAutoSave => _enableAutoSave;
        public float AutoSaveInterval => _autoSaveInterval;
        public int MaxAutoSaves => _maxAutoSaves;
        public bool AutoSaveOnSceneChange => _autoSaveOnSceneChange;
        public bool AutoSaveOnApplicationPause => _autoSaveOnApplicationPause;
        
        public bool EnableValidation => _enableValidation;
        public bool ValidateOnLoad => _validateOnLoad;
        public bool ValidateOnSave => _validateOnSave;
        public bool StrictValidation => _strictValidation;
        
        public int MaxRetryAttempts => _maxRetryAttempts;
        public float RetryDelay => _retryDelay;
        public bool EnableCircuitBreaker => _enableCircuitBreaker;
        public int CircuitBreakerThreshold => _circuitBreakerThreshold;
        public float CircuitBreakerTimeout => _circuitBreakerTimeout;
        
        public bool EnableDebugLogging => _enableDebugLogging;
        public bool EnablePerformanceMetrics => _enablePerformanceMetrics;
        public bool EnableStatistics => _enableStatistics;
        
        protected override bool OnCustomValidate(List<string> errors)
        {
            if (string.IsNullOrEmpty(_saveDirectoryPath))
                errors.Add("Save directory path cannot be empty");
            
            if (string.IsNullOrEmpty(_backupDirectoryPath))
                errors.Add("Backup directory path cannot be empty");
            
            if (_enabledStorageProviders == StorageProviderType.None)
                errors.Add("At least one storage provider must be enabled");
            
            if (_maxSaveSlots <= 0)
                errors.Add("Max save slots must be greater than 0");
            
            if (_maxBackupsPerSave < 0)
                errors.Add("Max backups per save cannot be negative");
            
            if (_maxCacheSize <= 0)
                errors.Add("Max cache size must be greater than 0");
            
            if (_compressionLevel < 0 || _compressionLevel > 9)
                errors.Add("Compression level must be between 0 and 9");
            
            if (_maxConcurrentOperations <= 0)
                errors.Add("Max concurrent operations must be greater than 0");
            
            if (_enableEncryption && _securityConfiguration == null)
                errors.Add("Security configuration is required when encryption is enabled");

            if (_securityConfiguration != null)
            {
                if (!_securityConfiguration.Validate(out var securityErrors))
                {
                    errors.AddRange(securityErrors);
                }
            }
            
            if (_autoSaveInterval <= 0)
                errors.Add("Auto save interval must be greater than 0");
            
            if (_maxAutoSaves < 0)
                errors.Add("Max auto saves cannot be negative");
            
            if (_maxRetryAttempts < 0)
                errors.Add("Max retry attempts cannot be negative");
            
            if (_retryDelay < 0)
                errors.Add("Retry delay cannot be negative");
            
            if (_circuitBreakerThreshold <= 0)
                errors.Add("Circuit breaker threshold must be greater than 0");
            
            if (_circuitBreakerTimeout <= 0)
                errors.Add("Circuit breaker timeout must be greater than 0");
            
            return errors.Count == 0;
        }
        
        protected override void OnResetToDefaults()
        {
            _saveDirectoryPath = "SaveData";
            _backupDirectoryPath = "SaveData/Backups";
            _enabledStorageProviders = StorageProviderType.LocalFile;
            _maxSaveSlots = 10;
            _maxBackupsPerSave = 5;
            _enableCaching = true;
            _maxCacheSize = 50;
            _enableCompression = true;
            _compressionLevel = 6;
            _enableBackgroundSaving = true;
            _maxConcurrentOperations = 4;
            _enableEncryption = false;
            _enableIntegrityChecks = true;
            _enableMagicBytes = true;
            _enableAutoSave = true;
            _autoSaveInterval = 300f;
            _maxAutoSaves = 3;
            _autoSaveOnSceneChange = true;
            _autoSaveOnApplicationPause = true;
            _enableValidation = true;
            _validateOnLoad = true;
            _validateOnSave = true;
            _strictValidation = false;
            _maxRetryAttempts = 3;
            _retryDelay = 1f;
            _enableCircuitBreaker = true;
            _circuitBreakerThreshold = 5;
            _circuitBreakerTimeout = 60f;
            _enableDebugLogging = false;
            _enablePerformanceMetrics = true;
            _enableStatistics = true;
        }
        
    }
}
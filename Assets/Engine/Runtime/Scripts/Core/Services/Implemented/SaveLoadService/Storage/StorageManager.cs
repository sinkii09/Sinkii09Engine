using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Diagnostics;
using System.IO;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages multiple storage providers with routing, failover, and health monitoring
    /// </summary>
    public class StorageManager : IStorageProvider
    {
        #region Private Fields
        private readonly Dictionary<StorageProviderType, IStorageProvider> _providers;
        private readonly Dictionary<StorageProviderType, StorageProviderConfiguration> _configurations;
        private readonly StorageProviderStatistics _aggregatedStatistics;
        private readonly object _lockObject = new object();
        private StorageProviderType _primaryProviderType;
        private StorageProviderType _fallbackProviderType;
        private StorageHealthStatus _healthStatus;
        private bool _isInitialized;
        private CancellationTokenSource _healthCheckCancellation;
        #endregion
        
        #region Properties
        public StorageProviderType ProviderType => StorageProviderType.None;
        public StorageHealthStatus HealthStatus => _healthStatus;
        public IReadOnlyDictionary<StorageProviderType, IStorageProvider> Providers => _providers;
        #endregion
        
        #region Constructor
        public StorageManager()
        {
            _providers = new Dictionary<StorageProviderType, IStorageProvider>();
            _configurations = new Dictionary<StorageProviderType, StorageProviderConfiguration>();
            _aggregatedStatistics = new StorageProviderStatistics();
            _healthStatus = StorageHealthStatus.Unknown;
            _isInitialized = false;
        }
        #endregion
        
        #region Provider Management
        public void RegisterProvider(IStorageProvider provider, StorageProviderConfiguration configuration)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            lock (_lockObject)
            {
                var providerType = provider.ProviderType;
                
                if (_providers.ContainsKey(providerType))
                {
                    UnityEngine.Debug.LogWarning($"Provider '{providerType}' is already registered. Replacing existing provider.");
                }
                
                _providers[providerType] = provider;
                _configurations[providerType] = configuration;
                
                // Set primary provider if not set
                if (_primaryProviderType == StorageProviderType.None)
                    _primaryProviderType = providerType;
                
                UnityEngine.Debug.Log($"Registered storage provider: {providerType}");
            }
        }
        
        public void UnregisterProvider(StorageProviderType providerType)
        {
            if (providerType == StorageProviderType.None)
                throw new ArgumentException("Provider type cannot be None", nameof(providerType));
            
            lock (_lockObject)
            {
                if (_providers.Remove(providerType))
                {
                    _configurations.Remove(providerType);
                    
                    // Update primary provider if necessary
                    if (_primaryProviderType == providerType)
                    {
                        _primaryProviderType = _providers.Keys.FirstOrDefault();
                    }
                    
                    UnityEngine.Debug.Log($"Unregistered storage provider: {providerType}");
                }
            }
        }
        
        public void SetPrimaryProvider(StorageProviderType providerType)
        {
            if (providerType == StorageProviderType.None)
                throw new ArgumentException("Provider type cannot be None", nameof(providerType));
            
            lock (_lockObject)
            {
                if (!_providers.ContainsKey(providerType))
                    throw new ArgumentException($"Provider '{providerType}' is not registered");
                
                _primaryProviderType = providerType;
                UnityEngine.Debug.Log($"Primary storage provider set to: {providerType}");
            }
        }
        
        public void SetFallbackProvider(StorageProviderType providerType)
        {
            if (providerType == StorageProviderType.None)
                throw new ArgumentException("Provider type cannot be None", nameof(providerType));
            
            lock (_lockObject)
            {
                if (!_providers.ContainsKey(providerType))
                    throw new ArgumentException($"Provider '{providerType}' is not registered");
                
                _fallbackProviderType = providerType;
                UnityEngine.Debug.Log($"Fallback storage provider set to: {providerType}");
            }
        }
        #endregion
        
        #region IStorageProvider Implementation
        public async UniTask<StorageInitializationResult> InitializeAsync(StorageProviderConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Auto-register LocalFileStorage if no providers are registered
                if (_providers.Count == 0)
                {
                    var localFileStorage = new LocalFileStorage();
                    var localConfig = new StorageProviderConfiguration
                    {
                        ProviderType = StorageProviderType.LocalFile,
                        BasePath = configuration?.BasePath ?? "SaveData"
                    };
                    RegisterProvider(localFileStorage, localConfig);
                }
                
                // Initialize all registered providers
                var initializationTasks = new List<UniTask<StorageInitializationResult>>();
                var providerTypes = new List<StorageProviderType>();
                
                lock (_lockObject)
                {
                    foreach (var kvp in _providers)
                    {
                        var providerConfig = _configurations[kvp.Key];
                        initializationTasks.Add(kvp.Value.InitializeAsync(providerConfig, cancellationToken));
                        providerTypes.Add(kvp.Key);
                    }
                }
                
                // Wait for all providers to initialize
                var results = await UniTask.WhenAll(initializationTasks);
                
                // Check initialization results
                var successfulProviders = 0;
                var failedProviders = new List<StorageProviderType>();
                
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].Success)
                    {
                        successfulProviders++;
                        UnityEngine.Debug.Log($"Storage provider '{providerTypes[i]}' initialized successfully");
                    }
                    else
                    {
                        failedProviders.Add(providerTypes[i]);
                        UnityEngine.Debug.LogError($"Storage provider '{providerTypes[i]}' failed to initialize: {results[i].ErrorMessage}");
                    }
                }
                
                if (successfulProviders == 0)
                {
                    stopwatch.Stop();
                    _healthStatus = StorageHealthStatus.Unhealthy;
                    return StorageInitializationResult.CreateFailure("All storage providers failed to initialize", duration: stopwatch.Elapsed);
                }
                
                // Start health monitoring
                _healthCheckCancellation = new CancellationTokenSource();
                _ = StartHealthMonitoring(_healthCheckCancellation.Token);
                
                _isInitialized = true;
                _healthStatus = failedProviders.Count == 0 ? StorageHealthStatus.Healthy : StorageHealthStatus.Degraded;
                
                stopwatch.Stop();
                return StorageInitializationResult.CreateSuccess(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _healthStatus = StorageHealthStatus.Unhealthy;
                return StorageInitializationResult.CreateFailure($"Failed to initialize StorageManager: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public async UniTask<StorageOperationResult> SaveAsync(string saveId, byte[] data, SaveMetadata metadata = null, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageOperationResult.CreateFailure("No healthy storage providers available");
            
            var result = await provider.SaveAsync(saveId, data, metadata, cancellationToken);
            
            // Try fallback provider if primary fails
            if (!result.Success && provider.ProviderType == _primaryProviderType && _fallbackProviderType != StorageProviderType.None)
            {
                var fallbackProvider = GetProvider(_fallbackProviderType);
                if (fallbackProvider != null && fallbackProvider.HealthStatus != StorageHealthStatus.Unhealthy)
                {
                    UnityEngine.Debug.LogWarning($"Primary storage provider failed, trying fallback: {_fallbackProviderType}");
                    result = await fallbackProvider.SaveAsync(saveId, data, metadata, cancellationToken);
                }
            }
            
            // Update aggregated statistics
            _aggregatedStatistics.TotalSaves++;
            _aggregatedStatistics.RecordOperation(result.Success, result.Duration, result.BytesProcessed, true);
            
            return result;
        }
        
        public async UniTask<StorageLoadResult> LoadAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageLoadResult.CreateFailure("No healthy storage providers available");
            
            var result = await provider.LoadAsync(saveId, cancellationToken);
            
            // Try fallback provider if primary fails
            if (!result.Success && provider.ProviderType == _primaryProviderType && _fallbackProviderType != StorageProviderType.None)
            {
                var fallbackProvider = GetProvider(_fallbackProviderType);
                if (fallbackProvider != null && fallbackProvider.HealthStatus != StorageHealthStatus.Unhealthy)
                {
                    UnityEngine.Debug.LogWarning($"Primary storage provider failed, trying fallback: {_fallbackProviderType}");
                    result = await fallbackProvider.LoadAsync(saveId, cancellationToken);
                }
            }
            
            // Update aggregated statistics
            _aggregatedStatistics.TotalLoads++;
            _aggregatedStatistics.RecordOperation(result.Success, result.Duration, result.BytesProcessed, false);
            
            return result;
        }
        
        public async UniTask<StorageOperationResult> DeleteAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            // Delete from all providers to ensure consistency
            var deleteTasks = new List<UniTask<StorageOperationResult>>();
            var providerTypes = new List<StorageProviderType>();
            
            lock (_lockObject)
            {
                foreach (var kvp in _providers)
                {
                    if (kvp.Value.HealthStatus != StorageHealthStatus.Unhealthy)
                    {
                        deleteTasks.Add(kvp.Value.DeleteAsync(saveId, cancellationToken));
                        providerTypes.Add(kvp.Key);
                    }
                }
            }
            
            if (deleteTasks.Count == 0)
                return StorageOperationResult.CreateFailure("No healthy storage providers available");
            
            var results = await UniTask.WhenAll(deleteTasks);
            
            // Consider operation successful if at least one provider succeeded
            var anySuccess = results.Any(r => r.Success);
            var totalDuration = TimeSpan.FromMilliseconds(results.Average(r => r.Duration.TotalMilliseconds));
            
            _aggregatedStatistics.TotalDeletes++;
            _aggregatedStatistics.RecordOperation(anySuccess, totalDuration);
            
            if (anySuccess)
            {
                return StorageOperationResult.CreateSuccess(totalDuration);
            }
            else
            {
                var errors = string.Join("; ", results.Where(r => !r.Success).Select(r => r.ErrorMessage));
                return StorageOperationResult.CreateFailure($"All delete operations failed: {errors}", duration: totalDuration);
            }
        }
        
        public async UniTask<StorageExistsResult> ExistsAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageExistsResult.CreateFailure("No healthy storage providers available");
            
            return await provider.ExistsAsync(saveId, cancellationToken);
        }
        
        public async UniTask<StorageListResult> GetSaveListAsync(CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageListResult.CreateFailure("No healthy storage providers available");
            
            return await provider.GetSaveListAsync(cancellationToken);
        }
        
        public async UniTask<StorageListResult> GetBackupListAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            ValidateSaveId(saveId);
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageListResult.CreateFailure("No healthy storage providers available");
            
            return await provider.GetBackupListAsync(saveId, cancellationToken);
        }
        
        public async UniTask<StorageMetadataResult> GetMetadataAsync(string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageMetadataResult.CreateFailure("No healthy storage providers available");
            
            return await provider.GetMetadataAsync(saveId, cancellationToken);
        }
        
        public async UniTask<StorageOperationResult> CreateBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageOperationResult.CreateFailure("No healthy storage providers available");
            
            var result = await provider.CreateBackupAsync(saveId, backupId, cancellationToken);
            
            _aggregatedStatistics.TotalBackups++;
            _aggregatedStatistics.RecordOperation(result.Success, result.Duration);
            
            return result;
        }
        
        public async UniTask<StorageOperationResult> RestoreFromBackupAsync(string backupId, string saveId, CancellationToken cancellationToken = default)
        {
            ValidateInitialization();
            
            var provider = GetHealthyProvider();
            if (provider == null)
                return StorageOperationResult.CreateFailure("No healthy storage providers available");
            
            var result = await provider.RestoreFromBackupAsync(backupId, saveId, cancellationToken);
            
            _aggregatedStatistics.TotalRestores++;
            _aggregatedStatistics.RecordOperation(result.Success, result.Duration);
            
            return result;
        }
        
        public async UniTask<StorageHealthResult> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var healthTasks = new List<UniTask<StorageHealthResult>>();
                var providerTypes = new List<StorageProviderType>();
                
                lock (_lockObject)
                {
                    foreach (var kvp in _providers)
                    {
                        healthTasks.Add(kvp.Value.HealthCheckAsync(cancellationToken));
                        providerTypes.Add(kvp.Key);
                    }
                }
                
                if (healthTasks.Count == 0)
                {
                    stopwatch.Stop();
                    return StorageHealthResult.CreateFailure("No storage providers registered", duration: stopwatch.Elapsed);
                }
                
                var results = await UniTask.WhenAll(healthTasks);
                
                // Aggregate health results
                var healthyProviders = 0;
                var degradedProviders = 0;
                var unhealthyProviders = 0;
                var healthMetrics = new Dictionary<string, object>();
                
                for (int i = 0; i < results.Length; i++)
                {
                    var result = results[i];
                    var providerType = providerTypes[i];
                    
                    healthMetrics[$"{providerType}_status"] = result.Success ? result.Status.ToString() : "Error";
                    healthMetrics[$"{providerType}_message"] = result.Success ? result.StatusMessage : result.ErrorMessage;
                    
                    if (result.Success)
                    {
                        switch (result.Status)
                        {
                            case StorageHealthStatus.Healthy:
                                healthyProviders++;
                                break;
                            case StorageHealthStatus.Degraded:
                                degradedProviders++;
                                break;
                            case StorageHealthStatus.Unhealthy:
                                unhealthyProviders++;
                                break;
                        }
                    }
                    else
                    {
                        unhealthyProviders++;
                    }
                }
                
                // Determine overall health status
                StorageHealthStatus overallStatus;
                string statusMessage;
                
                if (healthyProviders == providerTypes.Count)
                {
                    overallStatus = StorageHealthStatus.Healthy;
                    statusMessage = "All storage providers are healthy";
                }
                else if (healthyProviders > 0)
                {
                    overallStatus = StorageHealthStatus.Degraded;
                    statusMessage = $"{healthyProviders}/{providerTypes.Count} storage providers are healthy";
                }
                else
                {
                    overallStatus = StorageHealthStatus.Unhealthy;
                    statusMessage = "No storage providers are healthy";
                }
                
                // Add aggregated statistics
                healthMetrics["total_providers"] = providerTypes.Count;
                healthMetrics["healthy_providers"] = healthyProviders;
                healthMetrics["degraded_providers"] = degradedProviders;
                healthMetrics["unhealthy_providers"] = unhealthyProviders;
                healthMetrics["success_rate"] = _aggregatedStatistics.SuccessRate;
                healthMetrics["total_operations"] = _aggregatedStatistics.TotalOperations;
                
                stopwatch.Stop();
                _healthStatus = overallStatus;
                
                return StorageHealthResult.CreateSuccess(overallStatus, statusMessage, healthMetrics, stopwatch.Elapsed);
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
            ValidateInitialization();
            
            var cleanupTasks = new List<UniTask<StorageOperationResult>>();
            
            lock (_lockObject)
            {
                foreach (var provider in _providers.Values)
                {
                    cleanupTasks.Add(provider.CleanupAsync(cancellationToken));
                }
            }
            
            if (cleanupTasks.Count == 0)
                return StorageOperationResult.CreateFailure("No storage providers available");
            
            var results = await UniTask.WhenAll(cleanupTasks);
            
            var anySuccess = results.Any(r => r.Success);
            var totalDuration = TimeSpan.FromMilliseconds(results.Average(r => r.Duration.TotalMilliseconds));
            var totalBytesProcessed = results.Sum(r => r.BytesProcessed);
            
            if (anySuccess)
            {
                return StorageOperationResult.CreateSuccess(totalDuration, totalBytesProcessed);
            }
            else
            {
                var errors = string.Join("; ", results.Where(r => !r.Success).Select(r => r.ErrorMessage));
                return StorageOperationResult.CreateFailure($"All cleanup operations failed: {errors}", duration: totalDuration);
            }
        }
        
        public StorageProviderStatistics GetStatistics()
        {
            // Aggregate statistics from all providers
            var aggregated = new StorageProviderStatistics
            {
                TotalSaves = _aggregatedStatistics.TotalSaves,
                TotalLoads = _aggregatedStatistics.TotalLoads,
                TotalDeletes = _aggregatedStatistics.TotalDeletes,
                TotalBackups = _aggregatedStatistics.TotalBackups,
                TotalRestores = _aggregatedStatistics.TotalRestores,
                SuccessfulOperations = _aggregatedStatistics.SuccessfulOperations,
                FailedOperations = _aggregatedStatistics.FailedOperations,
                TotalBytesRead = _aggregatedStatistics.TotalBytesRead,
                TotalBytesWritten = _aggregatedStatistics.TotalBytesWritten,
                TotalOperationTime = _aggregatedStatistics.TotalOperationTime,
                AverageOperationTime = _aggregatedStatistics.AverageOperationTime,
                LastOperationTime = _aggregatedStatistics.LastOperationTime,
                CurrentHealth = _healthStatus
            };
            
            return aggregated;
        }
        
        public async UniTask<StorageOperationResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Stop health monitoring
                _healthCheckCancellation?.Cancel();
                _healthCheckCancellation?.Dispose();
                _healthCheckCancellation = null;
                
                // Shutdown all providers
                var shutdownTasks = new List<UniTask<StorageOperationResult>>();
                
                lock (_lockObject)
                {
                    foreach (var provider in _providers.Values)
                    {
                        shutdownTasks.Add(provider.ShutdownAsync(cancellationToken));
                    }
                }
                
                if (shutdownTasks.Count > 0)
                {
                    await UniTask.WhenAll(shutdownTasks);
                }
                
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
        
        #region Health Monitoring
        private async UniTask StartHealthMonitoring(CancellationToken cancellationToken)
        {
            const int healthCheckIntervalSeconds = 30;
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Delay(healthCheckIntervalSeconds * 1000, cancellationToken: cancellationToken);
                    
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _ = HealthCheckAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Health monitoring failed: {ex.Message}");
            }
        }
        #endregion
        
        #region Helper Methods
        private void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("StorageManager is not initialized");
        }
        
        private void ValidateSaveId(string saveId)
        {
            if (string.IsNullOrWhiteSpace(saveId))
                throw new ArgumentException("Save ID cannot be null or empty", nameof(saveId));
            
            if (saveId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Save ID contains invalid characters", nameof(saveId));
        }
        
        private IStorageProvider GetHealthyProvider()
        {
            lock (_lockObject)
            {
                // Try primary provider first
                if (_primaryProviderType != StorageProviderType.None)
                {
                    var primaryProvider = GetProvider(_primaryProviderType);
                    if (primaryProvider != null && primaryProvider.HealthStatus != StorageHealthStatus.Unhealthy)
                    {
                        return primaryProvider;
                    }
                }
                
                // Try fallback provider
                if (_fallbackProviderType != StorageProviderType.None)
                {
                    var fallbackProvider = GetProvider(_fallbackProviderType);
                    if (fallbackProvider != null && fallbackProvider.HealthStatus != StorageHealthStatus.Unhealthy)
                    {
                        return fallbackProvider;
                    }
                }
                
                // Find any healthy provider
                foreach (var provider in _providers.Values)
                {
                    if (provider.HealthStatus != StorageHealthStatus.Unhealthy)
                    {
                        return provider;
                    }
                }
                
                return null;
            }
        }
        
        private IStorageProvider GetProvider(StorageProviderType providerType)
        {
            lock (_lockObject)
            {
                return _providers.TryGetValue(providerType, out var provider) ? provider : null;
            }
        }
        #endregion
    }
}
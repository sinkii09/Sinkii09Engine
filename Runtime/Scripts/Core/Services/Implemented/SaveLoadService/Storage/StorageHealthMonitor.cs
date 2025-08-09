using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Diagnostics;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Monitors the health of storage providers and provides alerts for degraded performance
    /// </summary>
    public class StorageHealthMonitor : IDisposable
    {
        #region Events
        public event Action<StorageHealthAlert> HealthAlertTriggered;
        public event Action<StorageHealthReport> HealthReportGenerated;
        #endregion
        
        #region Private Fields
        private readonly Dictionary<StorageProviderType, IStorageProvider> _providers;
        private readonly Dictionary<StorageProviderType, StorageProviderHealthMetrics> _healthMetrics;
        private readonly StorageHealthConfiguration _configuration;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _monitoringCancellation;
        private bool _isMonitoring;
        private DateTime _lastHealthCheck;
        private int _consecutiveFailures;
        #endregion
        
        #region Properties
        public bool IsMonitoring => _isMonitoring;
        public DateTime LastHealthCheck => _lastHealthCheck;
        public int ConsecutiveFailures => _consecutiveFailures;
        public IReadOnlyDictionary<StorageProviderType, StorageProviderHealthMetrics> HealthMetrics => _healthMetrics;
        #endregion
        
        #region Constructor
        public StorageHealthMonitor(StorageHealthConfiguration configuration = null)
        {
            _providers = new Dictionary<StorageProviderType, IStorageProvider>();
            _healthMetrics = new Dictionary<StorageProviderType, StorageProviderHealthMetrics>();
            _configuration = configuration ?? new StorageHealthConfiguration();
            _isMonitoring = false;
            _lastHealthCheck = DateTime.MinValue;
            _consecutiveFailures = 0;
        }
        #endregion
        
        #region Provider Management
        public void RegisterProvider(IStorageProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            
            lock (_lockObject)
            {
                var providerType = provider.ProviderType;
                
                if (_providers.ContainsKey(providerType))
                {
                    UnityEngine.Debug.LogWarning($"Provider '{providerType}' is already registered for health monitoring. Replacing existing provider.");
                }
                
                _providers[providerType] = provider;
                _healthMetrics[providerType] = new StorageProviderHealthMetrics
                {
                    ProviderType = providerType,
                    RegisteredAt = DateTime.UtcNow,
                    Status = StorageHealthStatus.Unknown
                };
                
                UnityEngine.Debug.Log($"Registered provider '{providerType}' for health monitoring");
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
                    _healthMetrics.Remove(providerType);
                    UnityEngine.Debug.Log($"Unregistered provider '{providerType}' from health monitoring");
                }
            }
        }
        #endregion
        
        #region Health Monitoring
        public async UniTask StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (_isMonitoring)
            {
                UnityEngine.Debug.LogWarning("Health monitoring is already running");
                return;
            }
            
            _monitoringCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isMonitoring = true;
            _consecutiveFailures = 0;
            
            UnityEngine.Debug.Log($"Starting storage health monitoring (interval: {_configuration.MonitoringInterval.TotalSeconds}s)");
            
            _ = MonitoringLoop(_monitoringCancellation.Token);

            await UniTask.CompletedTask;
        }
        
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;
            
            _monitoringCancellation?.Cancel();
            _monitoringCancellation?.Dispose();
            _monitoringCancellation = null;
            _isMonitoring = false;
            
            UnityEngine.Debug.Log("Stopped storage health monitoring");
        }
        
        private async UniTask MonitoringLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Delay((int)_configuration.MonitoringInterval.TotalMilliseconds, cancellationToken: cancellationToken);
                    
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await PerformHealthCheckAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Health monitoring loop failed: {ex.Message}");
                _consecutiveFailures++;
                
                // Try to restart monitoring if failures are below threshold
                if (_consecutiveFailures < _configuration.MaxConsecutiveFailures)
                {
                    UnityEngine.Debug.LogWarning($"Restarting health monitoring after failure ({_consecutiveFailures}/{_configuration.MaxConsecutiveFailures})");
                    await UniTask.Delay(TimeSpan.FromMilliseconds(5000), cancellationToken: cancellationToken);
                    _ = MonitoringLoop(cancellationToken);
                }
                else
                {
                    UnityEngine.Debug.LogError("Maximum consecutive failures reached. Stopping health monitoring.");
                    StopMonitoring();
                }
            }
        }
        
        public async UniTask<StorageHealthReport> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var healthReport = new StorageHealthReport
            {
                Timestamp = DateTime.UtcNow,
                CheckDuration = TimeSpan.Zero,
                ProviderReports = new List<StorageProviderHealthReport>()
            };
            
            try
            {
                var healthTasks = new List<UniTask<StorageProviderHealthReport>>();
                var providerTypes = new List<StorageProviderType>();
                
                lock (_lockObject)
                {
                    foreach (var kvp in _providers)
                    {
                        healthTasks.Add(CheckProviderHealthAsync(kvp.Key, kvp.Value, cancellationToken));
                        providerTypes.Add(kvp.Key);
                    }
                }
                
                if (healthTasks.Count == 0)
                {
                    stopwatch.Stop();
                    healthReport.CheckDuration = stopwatch.Elapsed;
                    healthReport.OverallStatus = StorageHealthStatus.Unknown;
                    healthReport.StatusMessage = "No providers registered for monitoring";
                    return healthReport;
                }
                
                var results = await UniTask.WhenAll(healthTasks);
                healthReport.ProviderReports.AddRange(results);
                
                // Determine overall health status
                var healthyProviders = results.Count(r => r.Status == StorageHealthStatus.Healthy);
                var degradedProviders = results.Count(r => r.Status == StorageHealthStatus.Degraded);
                var unhealthyProviders = results.Count(r => r.Status == StorageHealthStatus.Unhealthy);
                
                if (unhealthyProviders == results.Length)
                {
                    healthReport.OverallStatus = StorageHealthStatus.Unhealthy;
                    healthReport.StatusMessage = "All storage providers are unhealthy";
                }
                else if (healthyProviders == results.Length)
                {
                    healthReport.OverallStatus = StorageHealthStatus.Healthy;
                    healthReport.StatusMessage = "All storage providers are healthy";
                }
                else
                {
                    healthReport.OverallStatus = StorageHealthStatus.Degraded;
                    healthReport.StatusMessage = $"{healthyProviders}/{results.Length} storage providers are healthy";
                }
                
                // Process health alerts
                ProcessHealthAlerts(results);
                
                // Update metrics
                UpdateHealthMetrics(results);
                
                _lastHealthCheck = DateTime.UtcNow;
                _consecutiveFailures = 0;
                
                stopwatch.Stop();
                healthReport.CheckDuration = stopwatch.Elapsed;
                
                // Trigger event
                HealthReportGenerated?.Invoke(healthReport);
                
                return healthReport;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _consecutiveFailures++;
                
                healthReport.CheckDuration = stopwatch.Elapsed;
                healthReport.OverallStatus = StorageHealthStatus.Unhealthy;
                healthReport.StatusMessage = $"Health check failed: {ex.Message}";
                healthReport.Exception = ex;
                
                UnityEngine.Debug.LogError($"Health check failed: {ex.Message}");
                return healthReport;
            }
        }
        
        private async UniTask<StorageProviderHealthReport> CheckProviderHealthAsync(StorageProviderType providerType, IStorageProvider provider, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var report = new StorageProviderHealthReport
            {
                ProviderType = providerType,
                Timestamp = DateTime.UtcNow,
                Status = StorageHealthStatus.Unknown
            };
            
            try
            {
                var healthResult = await provider.HealthCheckAsync(cancellationToken);
                stopwatch.Stop();
                
                report.Status = healthResult.Success ? healthResult.Status : StorageHealthStatus.Unhealthy;
                report.StatusMessage = healthResult.Success ? healthResult.StatusMessage : healthResult.ErrorMessage;
                report.CheckDuration = stopwatch.Elapsed;
                report.HealthMetrics = healthResult.HealthMetrics;
                report.Exception = healthResult.Exception;
                
                // Get provider statistics
                try
                {
                    var statistics = provider.GetStatistics();
                    report.Statistics = statistics;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Failed to get statistics for provider '{providerType}': {ex.Message}");
                }
                
                return report;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                report.Status = StorageHealthStatus.Unhealthy;
                report.StatusMessage = $"Health check failed: {ex.Message}";
                report.CheckDuration = stopwatch.Elapsed;
                report.Exception = ex;
                
                return report;
            }
        }
        
        private void ProcessHealthAlerts(StorageProviderHealthReport[] reports)
        {
            foreach (var report in reports)
            {
                var metrics = _healthMetrics[report.ProviderType];
                var previousStatus = metrics.Status;
                
                // Check for status changes
                if (report.Status != previousStatus)
                {
                    var alert = new StorageHealthAlert
                    {
                        ProviderType = report.ProviderType,
                        AlertType = GetAlertType(previousStatus, report.Status),
                        Timestamp = DateTime.UtcNow,
                        Message = $"Provider '{report.ProviderType}' status changed from {previousStatus} to {report.Status}",
                        Severity = GetAlertSeverity(report.Status),
                        Details = report.StatusMessage
                    };
                    
                    HealthAlertTriggered?.Invoke(alert);
                }
                
                // Check for performance degradation
                if (report.Statistics != null && _configuration.EnablePerformanceAlerts)
                {
                    CheckPerformanceAlerts(report, metrics);
                }
            }
        }
        
        private void CheckPerformanceAlerts(StorageProviderHealthReport report, StorageProviderHealthMetrics metrics)
        {
            if (report.Statistics.SuccessRate < _configuration.MinSuccessRate)
            {
                var alert = new StorageHealthAlert
                {
                    ProviderType = report.ProviderType,
                    AlertType = StorageHealthAlertType.PerformanceDegradation,
                    Timestamp = DateTime.UtcNow,
                    Message = $"Provider '{report.ProviderType}' success rate ({report.Statistics.SuccessRate:P2}) below threshold ({_configuration.MinSuccessRate:P2})",
                    Severity = StorageHealthAlertSeverity.Warning,
                    Details = $"Total operations: {report.Statistics.TotalOperations}, Failed: {report.Statistics.FailedOperations}"
                };
                
                HealthAlertTriggered?.Invoke(alert);
            }
            
            if (report.Statistics.AverageOperationTime > _configuration.MaxAverageOperationTime)
            {
                var alert = new StorageHealthAlert
                {
                    ProviderType = report.ProviderType,
                    AlertType = StorageHealthAlertType.PerformanceDegradation,
                    Timestamp = DateTime.UtcNow,
                    Message = $"Provider '{report.ProviderType}' average operation time ({report.Statistics.AverageOperationTime.TotalMilliseconds:F2}ms) exceeds threshold ({_configuration.MaxAverageOperationTime.TotalMilliseconds:F2}ms)",
                    Severity = StorageHealthAlertSeverity.Warning,
                    Details = $"Total operations: {report.Statistics.TotalOperations}"
                };
                
                HealthAlertTriggered?.Invoke(alert);
            }
        }
        
        private StorageHealthAlertType GetAlertType(StorageHealthStatus previousStatus, StorageHealthStatus currentStatus)
        {
            if (currentStatus == StorageHealthStatus.Unhealthy)
                return StorageHealthAlertType.ProviderFailure;
            
            if (previousStatus == StorageHealthStatus.Healthy && currentStatus == StorageHealthStatus.Degraded)
                return StorageHealthAlertType.PerformanceDegradation;
            
            if (previousStatus != StorageHealthStatus.Healthy && currentStatus == StorageHealthStatus.Healthy)
                return StorageHealthAlertType.ProviderRecovery;
            
            return StorageHealthAlertType.StatusChange;
        }
        
        private StorageHealthAlertSeverity GetAlertSeverity(StorageHealthStatus status)
        {
            return status switch
            {
                StorageHealthStatus.Unhealthy => StorageHealthAlertSeverity.Critical,
                StorageHealthStatus.Degraded => StorageHealthAlertSeverity.Warning,
                StorageHealthStatus.Healthy => StorageHealthAlertSeverity.Info,
                _ => StorageHealthAlertSeverity.Info
            };
        }
        
        private void UpdateHealthMetrics(StorageProviderHealthReport[] reports)
        {
            lock (_lockObject)
            {
                foreach (var report in reports)
                {
                    if (_healthMetrics.TryGetValue(report.ProviderType, out var metrics))
                    {
                        metrics.Status = report.Status;
                        metrics.LastHealthCheck = report.Timestamp;
                        metrics.TotalHealthChecks++;
                        
                        if (report.Status == StorageHealthStatus.Healthy)
                            metrics.HealthyChecks++;
                        else if (report.Status == StorageHealthStatus.Degraded)
                            metrics.DegradedChecks++;
                        else if (report.Status == StorageHealthStatus.Unhealthy)
                            metrics.UnhealthyChecks++;
                        
                        metrics.AverageCheckDuration = CalculateAverageCheckDuration(metrics, report.CheckDuration);
                        metrics.LastStatusMessage = report.StatusMessage;
                    }
                }
            }
        }
        
        private TimeSpan CalculateAverageCheckDuration(StorageProviderHealthMetrics metrics, TimeSpan newDuration)
        {
            if (metrics.TotalHealthChecks <= 1)
                return newDuration;
            
            var totalTicks = metrics.AverageCheckDuration.Ticks * (metrics.TotalHealthChecks - 1) + newDuration.Ticks;
            return new TimeSpan(totalTicks / metrics.TotalHealthChecks);
        }
        #endregion
        
        #region Reports
        public StorageHealthSummary GetHealthSummary()
        {
            lock (_lockObject)
            {
                var summary = new StorageHealthSummary
                {
                    Timestamp = DateTime.UtcNow,
                    TotalProviders = _providers.Count,
                    HealthyProviders = _healthMetrics.Values.Count(m => m.Status == StorageHealthStatus.Healthy),
                    DegradedProviders = _healthMetrics.Values.Count(m => m.Status == StorageHealthStatus.Degraded),
                    UnhealthyProviders = _healthMetrics.Values.Count(m => m.Status == StorageHealthStatus.Unhealthy),
                    IsMonitoring = _isMonitoring,
                    LastHealthCheck = _lastHealthCheck,
                    ConsecutiveFailures = _consecutiveFailures,
                    MonitoringInterval = _configuration.MonitoringInterval
                };
                
                summary.OverallHealthPercentage = summary.TotalProviders > 0 
                    ? (double)summary.HealthyProviders / summary.TotalProviders * 100 
                    : 0;
                
                return summary;
            }
        }
        
        public StorageProviderHealthMetrics GetProviderMetrics(StorageProviderType providerType)
        {
            lock (_lockObject)
            {
                return _healthMetrics.TryGetValue(providerType, out var metrics) ? metrics : null;
            }
        }
        #endregion
        
        #region IDisposable Implementation
        public void Dispose()
        {
            StopMonitoring();
        }
        #endregion
    }
    
    #region Configuration
    public class StorageHealthConfiguration
    {
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxConsecutiveFailures { get; set; } = 3;
        public bool EnablePerformanceAlerts { get; set; } = true;
        public double MinSuccessRate { get; set; } = 0.95; // 95%
        public TimeSpan MaxAverageOperationTime { get; set; } = TimeSpan.FromSeconds(5);
    }
    #endregion
    
    #region Health Models
    public class StorageHealthReport
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan CheckDuration { get; set; }
        public StorageHealthStatus OverallStatus { get; set; }
        public string StatusMessage { get; set; }
        public Exception Exception { get; set; }
        public List<StorageProviderHealthReport> ProviderReports { get; set; }
    }
    
    public class StorageProviderHealthReport
    {
        public StorageProviderType ProviderType { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan CheckDuration { get; set; }
        public StorageHealthStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> HealthMetrics { get; set; }
        public StorageProviderStatistics Statistics { get; set; }
    }
    
    public class StorageProviderHealthMetrics
    {
        public StorageProviderType ProviderType { get; set; }
        public DateTime RegisteredAt { get; set; }
        public StorageHealthStatus Status { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public string LastStatusMessage { get; set; }
        public long TotalHealthChecks { get; set; }
        public long HealthyChecks { get; set; }
        public long DegradedChecks { get; set; }
        public long UnhealthyChecks { get; set; }
        public TimeSpan AverageCheckDuration { get; set; }
        
        public double HealthPercentage => TotalHealthChecks > 0 ? (double)HealthyChecks / TotalHealthChecks * 100 : 0;
    }
    
    public class StorageHealthSummary
    {
        public DateTime Timestamp { get; set; }
        public int TotalProviders { get; set; }
        public int HealthyProviders { get; set; }
        public int DegradedProviders { get; set; }
        public int UnhealthyProviders { get; set; }
        public double OverallHealthPercentage { get; set; }
        public bool IsMonitoring { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public int ConsecutiveFailures { get; set; }
        public TimeSpan MonitoringInterval { get; set; }
    }
    
    public class StorageHealthAlert
    {
        public StorageProviderType ProviderType { get; set; }
        public StorageHealthAlertType AlertType { get; set; }
        public StorageHealthAlertSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
    }
    
    public enum StorageHealthAlertType
    {
        StatusChange,
        ProviderFailure,
        ProviderRecovery,
        PerformanceDegradation
    }
    
    public enum StorageHealthAlertSeverity
    {
        Info,
        Warning,
        Critical
    }
    #endregion
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sinkii09.Engine.Services;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Monitors service performance and triggers GC optimization when needed
    /// Integrates with the service lifecycle to provide automated memory management
    /// </summary>
    [EngineService(Priority = ServicePriority.High, Description = "Monitors service performance and memory usage")]
    public class ServicePerformanceMonitor : IEngineService, IMemoryPressureResponder
    {
        private readonly Dictionary<Type, ServicePerformanceMetrics> _serviceMetrics;
        private readonly Dictionary<Type, long> _lastMemorySnapshots;
        private readonly GCOptimizationManager _gcManager;
        private bool _disposed;
        
        // Performance thresholds
        private const float HighMemoryAllocationThreshold = 50 * 1024 * 1024; // 50MB per service
        private const float FrequentGCTriggerThreshold = 10; // 10 services with high allocation
        
        // Command performance thresholds
        private const float SLOW_COMMAND_THRESHOLD_SECONDS = 10f;
        private const float TIMEOUT_RATE_THRESHOLD = 0.1f;
        private const float RETRY_RATE_THRESHOLD = 0.2f;
        private const float ADAPTIVE_TIMEOUT_BUFFER = 1.2f;
        private const float MIN_TIMEOUT_RATIO = 0.5f;
        private const int MIN_SAMPLES_FOR_ADAPTATION = 5;
        
        public ServicePerformanceMonitor()
        {
            _serviceMetrics = new Dictionary<Type, ServicePerformanceMetrics>();
            _lastMemorySnapshots = new Dictionary<Type, long>();
            _gcManager = GCOptimizationManager.Instance;
        }
        
        /// <summary>
        /// Initialize the performance monitor
        /// </summary>
        public async UniTask<ServiceInitializationResult> InitializeAsync(System.IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                MonitoringLoop(cancellationToken).Forget();
                
                // Yield to ensure async behavior
                await UniTask.Yield();
                
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ServicePerformanceMonitor initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// Shutdown the performance monitor
        /// </summary>
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _disposed = true;
                await UniTask.Yield();
                
                Debug.Log("ServicePerformanceMonitor: Shutdown completed");
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// Health check for the performance monitor
        /// </summary>
        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            var gcStats = _gcManager.GetStatistics();
            var isHealthy = gcStats.AverageGCTimeMs < 10f; // Less than 10ms average GC time
            
            var status = isHealthy ? "Healthy" : $"High GC time: {gcStats.AverageGCTimeMs:F1}ms";
            return isHealthy ? ServiceHealthStatus.Healthy(status) : ServiceHealthStatus.Unhealthy(status);
        }
        
        /// <summary>
        /// Record service performance metrics
        /// </summary>
        public void RecordServiceMetrics<T>(long memoryUsage, TimeSpan executionTime) where T : IEngineService
        {
            RecordServiceMetrics(typeof(T), memoryUsage, executionTime);
        }
        
        /// <summary>
        /// Record service performance metrics by type
        /// </summary>
        public void RecordServiceMetrics(Type serviceType, long memoryUsage, TimeSpan executionTime)
        {
            if (_disposed) return;
            
            if (!_serviceMetrics.TryGetValue(serviceType, out var metrics))
            {
                metrics = new ServicePerformanceMetrics(serviceType);
                _serviceMetrics[serviceType] = metrics;
            }
            
            // Update metrics
            metrics.RecordExecution(memoryUsage, executionTime);
            
            // Check if this service is allocating too much memory
            if (_lastMemorySnapshots.TryGetValue(serviceType, out var lastMemory))
            {
                var memoryDelta = memoryUsage - lastMemory;
                if (memoryDelta > HighMemoryAllocationThreshold)
                {
                    Debug.LogWarning($"Service {serviceType.Name} allocated {memoryDelta / 1024.0 / 1024.0:F1}MB");
                    _ = RequestIncrementalGCAsync();
                }
            }
            
            _lastMemorySnapshots[serviceType] = memoryUsage;
        }
        
        /// <summary>
        /// Get performance metrics for a service
        /// </summary>
        public ServicePerformanceMetrics GetServiceMetrics<T>() where T : IEngineService
        {
            return GetServiceMetrics(typeof(T));
        }
        
        /// <summary>
        /// Get performance metrics for a service by type
        /// </summary>
        public ServicePerformanceMetrics GetServiceMetrics(Type serviceType)
        {
            return _serviceMetrics.TryGetValue(serviceType, out var metrics) ? metrics : null;
        }
        
        /// <summary>
        /// Get all service performance metrics
        /// </summary>
        public IReadOnlyDictionary<Type, ServicePerformanceMetrics> GetAllMetrics()
        {
            return _serviceMetrics;
        }
        
        /// <summary>
        /// Request incremental GC based on service performance
        /// </summary>
        private async UniTask RequestIncrementalGCAsync()
        {
            // Count services with high memory allocation
            var highAllocationServices = _serviceMetrics.Values
                .Count(m => m.AverageMemoryUsage > HighMemoryAllocationThreshold);
                
            if (highAllocationServices >= FrequentGCTriggerThreshold)
            {
                Debug.Log($"Triggering incremental GC: {highAllocationServices} services with high allocation");
                await _gcManager.RequestIncrementalGCAsync(1);
            }
        }
        
        /// <summary>
        /// Background monitoring loop
        /// </summary>
        private async UniTaskVoid MonitoringLoop(CancellationToken cancellationToken)
        {
            while (!_disposed && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
                    
                    // Analyze overall performance
                    AnalyzeOverallPerformance();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in performance monitoring loop: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Analyze overall system performance
        /// </summary>
        private void AnalyzeOverallPerformance()
        {
            if (_serviceMetrics.Count == 0) return;
            
            var totalMemory = _serviceMetrics.Values.Sum(m => m.AverageMemoryUsage);
            var avgExecutionTime = _serviceMetrics.Values.Average(m => m.AverageExecutionTime.TotalMilliseconds);
            
            Debug.Log($"Service Performance Summary: {_serviceMetrics.Count} services, " +
                     $"{totalMemory / 1024.0 / 1024.0:F1}MB total memory, " +
                     $"{avgExecutionTime:F1}ms avg execution");
                     
            // Trigger GC if total memory usage is high
            if (totalMemory > 200 * 1024 * 1024) // 200MB threshold
            {
                _ = _gcManager.RequestIncrementalGCAsync(2);
            }
        }
        
        /// <summary>
        /// Respond to memory pressure events
        /// </summary>
        public async UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            Debug.Log($"ServicePerformanceMonitor responding to memory pressure: {pressureLevel}");
            
            // Clear old metrics to free memory
            var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
            var expiredServices = _serviceMetrics
                .Where(kvp => kvp.Value.LastUpdateTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var serviceType in expiredServices)
            {
                _serviceMetrics.Remove(serviceType);
                _lastMemorySnapshots.Remove(serviceType);
            }
            
            // Request appropriate GC level
            var generation = strategy switch
            {
                MemoryPressureMonitor.CleanupStrategy.Conservative => 0,
                MemoryPressureMonitor.CleanupStrategy.Moderate => 1,
                MemoryPressureMonitor.CleanupStrategy.Aggressive => 2,
                _ => 1
            };
            
            await _gcManager.RequestIncrementalGCAsync(generation);
        }
        
        /// <summary>
        /// Handle memory pressure level changes
        /// </summary>
        public void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel)
        {
            if (newLevel > previousLevel)
            {
                Debug.LogWarning($"ServicePerformanceMonitor: Memory pressure increased to {newLevel}");
            }
        }

        #region Command Performance Tracking
        private readonly Dictionary<Type, CommandPerformanceMetrics> _commandMetrics = new Dictionary<Type, CommandPerformanceMetrics>();

        /// <summary>
        /// Record command performance metrics for script execution
        /// </summary>
        public void RecordCommandMetrics(Type commandType, long memoryUsage, TimeSpan executionTime, bool wasTimeout, bool wasRetried)
        {
            if (_disposed) return;

            if (!_commandMetrics.TryGetValue(commandType, out var metrics))
            {
                metrics = new CommandPerformanceMetrics(commandType);
                _commandMetrics[commandType] = metrics;
            }

            metrics.RecordExecution(memoryUsage, executionTime, wasTimeout, wasRetried);

            // Log performance warnings for slow commands
            if (executionTime.TotalSeconds > SLOW_COMMAND_THRESHOLD_SECONDS)
            {
                Debug.LogWarning($"Command {commandType.Name} took {executionTime.TotalSeconds:F2}s to execute");
            }
        }

        /// <summary>
        /// Get performance metrics for a command type
        /// </summary>
        public CommandPerformanceMetrics GetCommandMetrics(Type commandType)
        {
            _commandMetrics.TryGetValue(commandType, out var metrics);
            return metrics;
        }

        /// <summary>
        /// Get adaptive timeout suggestion based on historical performance
        /// </summary>
        public float GetSuggestedTimeout(Type commandType, float defaultTimeout)
        {
            var metrics = GetCommandMetrics(commandType);
            if (metrics == null || metrics.ExecutionCount < MIN_SAMPLES_FOR_ADAPTATION)
                return defaultTimeout;

            // Use average execution time + buffer for adaptive timeout
            var adaptiveTimeout = (float)(metrics.AverageExecutionTime.TotalSeconds * ADAPTIVE_TIMEOUT_BUFFER);
            return Math.Max(adaptiveTimeout, defaultTimeout * MIN_TIMEOUT_RATIO);
        }

        /// <summary>
        /// Check if command type has performance issues
        /// </summary>
        public bool HasPerformanceIssues(Type commandType)
        {
            var metrics = GetCommandMetrics(commandType);
            if (metrics == null) return false;

            return metrics.TimeoutRate > TIMEOUT_RATE_THRESHOLD || 
                   metrics.RetryRate > RETRY_RATE_THRESHOLD ||   
                   metrics.AverageExecutionTime.TotalSeconds > SLOW_COMMAND_THRESHOLD_SECONDS;
        }
        #endregion
    }
    
    /// <summary>
    /// Performance metrics for a single service
    /// </summary>
    public class ServicePerformanceMetrics
    {
        private readonly Type _serviceType;
        private readonly List<long> _memoryUsageHistory;
        private readonly List<TimeSpan> _executionTimeHistory;
        private const int MaxHistorySize = 100;
        
        public Type ServiceType => _serviceType;
        public DateTime LastUpdateTime { get; private set; }
        public int ExecutionCount { get; private set; }
        
        public long AverageMemoryUsage => _memoryUsageHistory.Count > 0 ? (long)_memoryUsageHistory.Average() : 0;
        public long PeakMemoryUsage => _memoryUsageHistory.Count > 0 ? _memoryUsageHistory.Max() : 0;
        public TimeSpan AverageExecutionTime => _executionTimeHistory.Count > 0 ? 
            TimeSpan.FromTicks((long)_executionTimeHistory.Average(t => t.Ticks)) : TimeSpan.Zero;
        public TimeSpan PeakExecutionTime => _executionTimeHistory.Count > 0 ? 
            _executionTimeHistory.Max() : TimeSpan.Zero;
        
        public ServicePerformanceMetrics(Type serviceType)
        {
            _serviceType = serviceType;
            _memoryUsageHistory = new List<long>();
            _executionTimeHistory = new List<TimeSpan>();
            LastUpdateTime = DateTime.UtcNow;
        }
        
        public void RecordExecution(long memoryUsage, TimeSpan executionTime)
        {
            // Add new records
            _memoryUsageHistory.Add(memoryUsage);
            _executionTimeHistory.Add(executionTime);
            
            // Trim history if needed
            if (_memoryUsageHistory.Count > MaxHistorySize)
            {
                _memoryUsageHistory.RemoveAt(0);
            }
            if (_executionTimeHistory.Count > MaxHistorySize)
            {
                _executionTimeHistory.RemoveAt(0);
            }
            
            ExecutionCount++;
            LastUpdateTime = DateTime.UtcNow;
        }
        
        public override string ToString()
        {
            return $"{_serviceType.Name}: {AverageMemoryUsage / 1024.0 / 1024.0:F1}MB avg, " +
                   $"{AverageExecutionTime.TotalMilliseconds:F1}ms avg exec, {ExecutionCount} calls";
        }
    }

    /// <summary>
    /// Performance metrics for command execution tracking
    /// </summary>
    public class CommandPerformanceMetrics
    {
        private readonly Type _commandType;
        private readonly List<long> _memoryUsageHistory;
        private readonly List<TimeSpan> _executionTimeHistory;
        private int _timeoutCount;
        private int _retryCount;
        
        private const int MaxHistorySize = 100;

        public CommandPerformanceMetrics(Type commandType)
        {
            _commandType = commandType;
            _memoryUsageHistory = new List<long>();
            _executionTimeHistory = new List<TimeSpan>();
        }

        public int ExecutionCount => _executionTimeHistory.Count;
        public float TimeoutRate => ExecutionCount > 0 ? (float)_timeoutCount / ExecutionCount : 0f;
        public float RetryRate => ExecutionCount > 0 ? (float)_retryCount / ExecutionCount : 0f;

        public TimeSpan AverageExecutionTime
        {
            get
            {
                if (_executionTimeHistory.Count == 0) return TimeSpan.Zero;
                var totalTicks = _executionTimeHistory.Sum(t => t.Ticks);
                return new TimeSpan(totalTicks / _executionTimeHistory.Count);
            }
        }

        public long AverageMemoryUsage
        {
            get
            {
                if (_memoryUsageHistory.Count == 0) return 0;
                return (long)_memoryUsageHistory.Average();
            }
        }

        public void RecordExecution(long memoryUsage, TimeSpan executionTime, bool wasTimeout, bool wasRetried)
        {
            _memoryUsageHistory.Add(memoryUsage);
            _executionTimeHistory.Add(executionTime);
            
            if (wasTimeout) _timeoutCount++;
            if (wasRetried) _retryCount++;
            
            // Trim history if needed
            if (_memoryUsageHistory.Count > MaxHistorySize)
            {
                _memoryUsageHistory.RemoveAt(0);
            }
            if (_executionTimeHistory.Count > MaxHistorySize)
            {
                _executionTimeHistory.RemoveAt(0);
            }
        }
        
        public override string ToString()
        {
            return $"{_commandType.Name}: {AverageMemoryUsage / 1024.0 / 1024.0:F1}MB avg, " +
                   $"{AverageExecutionTime.TotalMilliseconds:F1}ms avg exec, {ExecutionCount} calls, " +
                   $"{TimeoutRate:P1} timeout rate, {RetryRate:P1} retry rate";
        }
    }
}
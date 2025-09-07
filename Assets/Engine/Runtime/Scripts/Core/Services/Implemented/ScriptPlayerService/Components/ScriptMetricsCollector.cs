using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services.Performance;
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Centralized collector for script execution performance metrics and GC optimization
    /// </summary>
    public class ScriptMetricsCollector
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly ScriptExecutionContext _executionContext;
        private readonly ServicePerformanceMonitor _performanceMonitor;
        #endregion

        #region Properties
        public ScriptPerformanceMetrics ScriptMetrics => _executionContext.PerformanceMetrics;
        public bool IsPerformanceMonitoringEnabled => _performanceMonitor != null;
        #endregion

        #region Constructor
        public ScriptMetricsCollector(
            ScriptPlayerConfiguration config,
            ScriptExecutionContext executionContext,
            ServicePerformanceMonitor performanceMonitor = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _performanceMonitor = performanceMonitor;
        }
        #endregion

        #region GC Optimization Methods
        /// <summary>
        /// Trigger GC optimization with reason tracking
        /// </summary>
        public async UniTask TriggerGCOptimizationAsync(string reason, CancellationToken cancellationToken = default)
        {
            if (_performanceMonitor == null)
                return;

            try
            {
                // Record GC event timing
                var gcStartTime = DateTime.Now;
                
                // Request GC optimization through performance monitor
                await RequestGCOptimizationAsync(cancellationToken);
                
                var gcTime = (float)(DateTime.Now - gcStartTime).TotalSeconds;
                
                // Record GC metrics
                RecordGCEvent(gcTime, reason);
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"[MetricsCollector] GC optimization triggered: {reason} (took {gcTime:F3}s)");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MetricsCollector] GC optimization failed for {reason}: {ex.Message}");
            }
        }

        /// <summary>
        /// Request GC optimization from performance monitor using direct interface
        /// </summary>
        private async UniTask RequestGCOptimizationAsync(CancellationToken cancellationToken = default)
        {
            // Try direct interface first (more efficient than reflection)
            if (_performanceMonitor is IMemoryPressureResponder memoryResponder)
            {
                // Trigger memory pressure notification to initiate GC
                memoryResponder.OnMemoryPressureLevelChanged(
                    MemoryPressureMonitor.MemoryPressureLevel.Low,
                    MemoryPressureMonitor.MemoryPressureLevel.High);
                await UniTask.Yield(cancellationToken);
                return;
            }

            // Fallback to reflection-based approach
            await RequestGCOptimizationViaReflectionAsync(cancellationToken);
        }

        /// <summary>
        /// Fallback reflection-based GC optimization (less efficient)
        /// </summary>
        private async UniTask RequestGCOptimizationViaReflectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var gcManagerField = _performanceMonitor.GetType().GetField("_gcManager", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (gcManagerField?.GetValue(_performanceMonitor) is object gcManager)
                {
                    var requestMethod = gcManager.GetType().GetMethod("RequestIncrementalGCAsync");
                    if (requestMethod != null)
                    {
                        var task = requestMethod.Invoke(gcManager, new object[] { 0 });
                        if (task is UniTask gcTask)
                        {
                            await gcTask.AttachExternalCancellation(cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Debug.LogWarning($"[MetricsCollector] Reflection-based GC optimization failed: {ex.Message}");
            }
        }
        #endregion

        #region Metrics Recording Methods
        /// <summary>
        /// Record command execution metrics
        /// </summary>
        public void RecordCommandExecution(Type commandType, float executionTimeSeconds)
        {
            _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, executionTimeSeconds);
            
            if (_config.LogCommandExecution && executionTimeSeconds > 1.0f)
            {
                Debug.Log($"[MetricsCollector] Command {commandType.Name} executed in {executionTimeSeconds:F2}s");
            }
        }

        /// <summary>
        /// Record GC event with reason
        /// </summary>
        public void RecordGCEvent(float gcTimeSeconds, string reason = null)
        {
            _executionContext.PerformanceMetrics?.RecordGCEvent(gcTimeSeconds);
            
            if (_config.LogExecutionFlow)
            {
                var reasonText = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
                Debug.Log($"[MetricsCollector] GC event recorded: {gcTimeSeconds:F3}s{reasonText}");
            }
        }

        /// <summary>
        /// Reset all metrics for new script execution
        /// </summary>
        public void ResetMetrics()
        {
            _executionContext.PerformanceMetrics?.Reset();
            
            if (_config.LogExecutionFlow)
            {
                Debug.Log("[MetricsCollector] Performance metrics reset");
            }
        }
        #endregion

        #region Performance Analysis Methods
        /// <summary>
        /// Get performance summary for current script execution
        /// </summary>
        public ScriptPerformanceSummary GetPerformanceSummary()
        {
            var metrics = _executionContext.PerformanceMetrics;
            if (metrics == null)
                return new ScriptPerformanceSummary();

            return new ScriptPerformanceSummary
            {
                AverageCommandTime = metrics.AverageCommandTime,
                PeakCommandTime = metrics.PeakCommandTime,
                MemoryDelta = metrics.MemoryDelta,
                CurrentMemory = metrics.CurrentMemory,
                PeakMemory = metrics.PeakMemory,
                TotalGCTime = metrics.TotalGCTime,
                GCCount = metrics.GCCount,
                StartTime = metrics.StartMemory,
                ExecutionTime = _executionContext.ExecutionTime
            };
        }

        /// <summary>
        /// Check if script execution has performance issues
        /// </summary>
        public bool HasPerformanceIssues()
        {
            var metrics = _executionContext.PerformanceMetrics;
            if (metrics == null) return false;

            const float HIGH_MEMORY_THRESHOLD = 100 * 1024 * 1024; // 100MB
            const float HIGH_GC_TIME_THRESHOLD = 5.0f; // 5 seconds
            const float SLOW_COMMAND_THRESHOLD = 10.0f; // 10 seconds

            return metrics.MemoryDelta > HIGH_MEMORY_THRESHOLD ||
                   metrics.TotalGCTime > HIGH_GC_TIME_THRESHOLD ||
                   metrics.PeakCommandTime > SLOW_COMMAND_THRESHOLD;
        }

        /// <summary>
        /// Get performance recommendations
        /// </summary>
        public string[] GetPerformanceRecommendations()
        {
            var recommendations = new System.Collections.Generic.List<string>();
            var metrics = _executionContext.PerformanceMetrics;
            
            if (metrics == null)
                return recommendations.ToArray();

            if (metrics.MemoryDelta > 50 * 1024 * 1024) // 50MB
            {
                recommendations.Add("High memory usage detected - consider optimizing asset loading");
            }

            if (metrics.PeakCommandTime > 5.0f)
            {
                recommendations.Add("Slow command execution detected - check for blocking operations");
            }

            if (metrics.TotalGCTime > 2.0f)
            {
                recommendations.Add("High GC pressure - consider reducing object allocations");
            }

            if (metrics.GCCount > 10)
            {
                recommendations.Add("Frequent garbage collection - optimize memory allocation patterns");
            }

            return recommendations.ToArray();
        }
        #endregion

        #region Memory Monitoring
        /// <summary>
        /// Check current memory pressure and trigger GC if needed
        /// </summary>
        public async UniTask<bool> CheckMemoryPressureAsync(CancellationToken cancellationToken = default)
        {
            var currentMemory = GC.GetTotalMemory(false);
            var metrics = _executionContext.PerformanceMetrics;
            
            if (metrics == null) return false;

            const long HIGH_MEMORY_THRESHOLD = 200 * 1024 * 1024; // 200MB
            const long CRITICAL_MEMORY_THRESHOLD = 500 * 1024 * 1024; // 500MB

            if (currentMemory > CRITICAL_MEMORY_THRESHOLD)
            {
                await TriggerGCOptimizationAsync("Critical memory pressure", cancellationToken);
                return true;
            }
            else if (currentMemory > HIGH_MEMORY_THRESHOLD && 
                     currentMemory - metrics.StartMemory > HIGH_MEMORY_THRESHOLD / 2)
            {
                await TriggerGCOptimizationAsync("High memory pressure", cancellationToken);
                return true;
            }

            return false;
        }
        #endregion
    }

    /// <summary>
    /// Summary of script performance metrics
    /// </summary>
    public class ScriptPerformanceSummary
    {
        public float AverageCommandTime { get; set; }
        public float PeakCommandTime { get; set; }
        public long MemoryDelta { get; set; }
        public long CurrentMemory { get; set; }
        public long PeakMemory { get; set; }
        public float TotalGCTime { get; set; }
        public int GCCount { get; set; }
        public long StartTime { get; set; }
        public float ExecutionTime { get; set; }

        public override string ToString()
        {
            return $"Avg: {AverageCommandTime:F2}s, Peak: {PeakCommandTime:F2}s, " +
                   $"Memory: {MemoryDelta / 1024.0 / 1024.0:F1}MB, GC: {TotalGCTime:F2}s";
        }
    }
}
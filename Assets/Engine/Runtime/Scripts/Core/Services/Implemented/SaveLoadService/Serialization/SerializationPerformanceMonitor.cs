using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Monitors and tracks serialization performance metrics
    /// </summary>
    public class SerializationPerformanceMonitor
    {
        private readonly Queue<SerializationMetrics> _recentMetrics;
        private readonly Dictionary<string, SerializationStatistics> _statisticsByType;
        private readonly int _maxMetricsHistory;
        
        public SerializationPerformanceMonitor(int maxMetricsHistory = 100)
        {
            _maxMetricsHistory = maxMetricsHistory;
            _recentMetrics = new Queue<SerializationMetrics>();
            _statisticsByType = new Dictionary<string, SerializationStatistics>();
        }
        
        /// <summary>
        /// Record serialization metrics
        /// </summary>
        public void RecordMetrics(SerializationMetrics metrics, string dataType = null)
        {
            if (metrics == null)
                return;
            
            // Add to recent metrics
            _recentMetrics.Enqueue(metrics);
            
            // Maintain history limit
            while (_recentMetrics.Count > _maxMetricsHistory)
            {
                _recentMetrics.Dequeue();
            }
            
            // Update type-specific statistics
            if (!string.IsNullOrEmpty(dataType))
            {
                if (!_statisticsByType.ContainsKey(dataType))
                {
                    _statisticsByType[dataType] = new SerializationStatistics(dataType);
                }
                
                _statisticsByType[dataType].AddMetrics(metrics);
            }
        }
        
        /// <summary>
        /// Get overall performance summary
        /// </summary>
        public PerformanceSummary GetOverallSummary()
        {
            if (_recentMetrics.Count == 0)
                return new PerformanceSummary();
            
            var metrics = _recentMetrics.ToArray();
            
            return new PerformanceSummary
            {
                TotalOperations = metrics.Length,
                AverageSerializationTime = CalculateAverage(metrics, m => m.JsonSerializationTime.TotalMilliseconds),
                AverageCompressionTime = CalculateAverage(metrics, m => m.CompressionTime.TotalMilliseconds),
                AverageEncodingTime = CalculateAverage(metrics, m => m.EncodingTime.TotalMilliseconds),
                AverageTotalTime = CalculateAverage(metrics, m => m.TotalTime.TotalMilliseconds),
                
                AverageJsonSize = CalculateAverage(metrics, m => m.JsonSize),
                AverageCompressedSize = CalculateAverage(metrics, m => m.CompressedSize),
                AverageEncodedSize = CalculateAverage(metrics, m => m.EncodedSize),
                
                AverageCompressionRatio = CalculateAverage(metrics, m => m.CompressionEfficiency),
                AverageOverallRatio = CalculateAverage(metrics, m => m.OverallEfficiency),
                
                FastestOperation = metrics.Min(m => m.TotalTime.TotalMilliseconds),
                SlowestOperation = metrics.Max(m => m.TotalTime.TotalMilliseconds),
                
                SmallestOriginalSize = metrics.Min(m => m.JsonSize),
                LargestOriginalSize = metrics.Max(m => m.JsonSize),
                
                BestCompressionRatio = metrics.Min(m => m.CompressionEfficiency),
                WorstCompressionRatio = metrics.Max(m => m.CompressionEfficiency)
            };
        }
        
        /// <summary>
        /// Get performance summary for specific data type
        /// </summary>
        public PerformanceSummary GetSummaryForType(string dataType)
        {
            if (!_statisticsByType.ContainsKey(dataType))
                return new PerformanceSummary();
            
            return _statisticsByType[dataType].GetSummary();
        }
        
        /// <summary>
        /// Get statistics for all data types
        /// </summary>
        public Dictionary<string, SerializationStatistics> GetAllTypeStatistics()
        {
            return new Dictionary<string, SerializationStatistics>(_statisticsByType);
        }
        
        /// <summary>
        /// Get recent performance trends
        /// </summary>
        public PerformanceTrends GetTrends(int recentCount = 20)
        {
            var recentMetrics = _recentMetrics.TakeLast(Math.Min(recentCount, _recentMetrics.Count)).ToArray();
            
            if (recentMetrics.Length < 2)
                return new PerformanceTrends();
            
            var oldMetrics = recentMetrics.Take(recentMetrics.Length / 2).ToArray();
            var newMetrics = recentMetrics.Skip(recentMetrics.Length / 2).ToArray();
            
            return new PerformanceTrends
            {
                SerializationTimeTrend = CalculateTrend(oldMetrics, newMetrics, m => m.JsonSerializationTime.TotalMilliseconds),
                CompressionTimeTrend = CalculateTrend(oldMetrics, newMetrics, m => m.CompressionTime.TotalMilliseconds),
                TotalTimeTrend = CalculateTrend(oldMetrics, newMetrics, m => m.TotalTime.TotalMilliseconds),
                CompressionRatioTrend = CalculateTrend(oldMetrics, newMetrics, m => m.CompressionEfficiency),
                OverallEfficiencyTrend = CalculateTrend(oldMetrics, newMetrics, m => m.OverallEfficiency)
            };
        }
        
        /// <summary>
        /// Check for performance regressions
        /// </summary>
        public List<PerformanceAlert> CheckForRegressions(double timeThresholdMs = 1000, double ratioThreshold = 0.2)
        {
            var alerts = new List<PerformanceAlert>();
            var trends = GetTrends();
            var summary = GetOverallSummary();
            
            // Check for slow operations
            if (summary.AverageTotalTime > timeThresholdMs)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.SlowOperation,
                    Severity = summary.AverageTotalTime > timeThresholdMs * 2 ? AlertSeverity.High : AlertSeverity.Medium,
                    Message = $"Average serialization time ({summary.AverageTotalTime:F1}ms) exceeds threshold ({timeThresholdMs}ms)",
                    Value = summary.AverageTotalTime,
                    Threshold = timeThresholdMs
                });
            }
            
            // Check for performance degradation
            if (trends.TotalTimeTrend > ratioThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.PerformanceDegradation,
                    Severity = trends.TotalTimeTrend > ratioThreshold * 2 ? AlertSeverity.High : AlertSeverity.Medium,
                    Message = $"Serialization performance has degraded by {trends.TotalTimeTrend * 100:F1}%",
                    Value = trends.TotalTimeTrend,
                    Threshold = ratioThreshold
                });
            }
            
            // Check for poor compression
            if (summary.AverageCompressionRatio > 0.9)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.PoorCompression,
                    Severity = AlertSeverity.Low,
                    Message = $"Compression ratio ({summary.AverageCompressionRatio:F2}) is poor",
                    Value = summary.AverageCompressionRatio,
                    Threshold = 0.9
                });
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Clear all performance data
        /// </summary>
        public void Clear()
        {
            _recentMetrics.Clear();
            _statisticsByType.Clear();
        }
        
        /// <summary>
        /// Generate performance report
        /// </summary>
        public string GenerateReport()
        {
            var summary = GetOverallSummary();
            var trends = GetTrends();
            var alerts = CheckForRegressions();
            
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== Serialization Performance Report ===");
            report.AppendLine($"Total Operations: {summary.TotalOperations}");
            report.AppendLine($"Average Total Time: {summary.AverageTotalTime:F2}ms");
            report.AppendLine($"Average Compression Ratio: {summary.AverageCompressionRatio:F3}");
            report.AppendLine($"Average Overall Efficiency: {summary.AverageOverallRatio:F3}");
            report.AppendLine();
            
            report.AppendLine("=== Performance Breakdown ===");
            report.AppendLine($"Serialization: {summary.AverageSerializationTime:F2}ms");
            report.AppendLine($"Compression: {summary.AverageCompressionTime:F2}ms");
            report.AppendLine($"Encoding: {summary.AverageEncodingTime:F2}ms");
            report.AppendLine();
            
            report.AppendLine("=== Size Statistics ===");
            report.AppendLine($"Average JSON Size: {FormatBytes(summary.AverageJsonSize)}");
            report.AppendLine($"Average Compressed Size: {FormatBytes(summary.AverageCompressedSize)}");
            report.AppendLine($"Average Final Size: {FormatBytes(summary.AverageEncodedSize)}");
            report.AppendLine();
            
            if (trends.HasTrends)
            {
                report.AppendLine("=== Performance Trends ===");
                report.AppendLine($"Serialization Time: {FormatTrend(trends.SerializationTimeTrend)}");
                report.AppendLine($"Compression Time: {FormatTrend(trends.CompressionTimeTrend)}");
                report.AppendLine($"Total Time: {FormatTrend(trends.TotalTimeTrend)}");
                report.AppendLine();
            }
            
            if (alerts.Count > 0)
            {
                report.AppendLine("=== Performance Alerts ===");
                foreach (var alert in alerts)
                {
                    report.AppendLine($"[{alert.Severity}] {alert.Type}: {alert.Message}");
                }
                report.AppendLine();
            }
            
            return report.ToString();
        }
        
        private double CalculateAverage(SerializationMetrics[] metrics, Func<SerializationMetrics, double> selector)
        {
            if (metrics.Length == 0)
                return 0;
            
            return metrics.Average(selector);
        }
        
        private double CalculateTrend(SerializationMetrics[] oldMetrics, SerializationMetrics[] newMetrics, Func<SerializationMetrics, double> selector)
        {
            if (oldMetrics.Length == 0 || newMetrics.Length == 0)
                return 0;
            
            var oldAverage = oldMetrics.Average(selector);
            var newAverage = newMetrics.Average(selector);
            
            if (oldAverage == 0)
                return 0;
            
            return (newAverage - oldAverage) / oldAverage;
        }
        
        private string FormatBytes(double bytes)
        {
            if (bytes < 1024)
                return $"{bytes:F0} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024:F1} KB";
            else
                return $"{bytes / (1024 * 1024):F1} MB";
        }
        
        private string FormatTrend(double trend)
        {
            var percentage = trend * 100;
            var direction = trend > 0 ? "↗" : "↘";
            return $"{direction} {Math.Abs(percentage):F1}%";
        }
    }
    
    /// <summary>
    /// Statistics for specific data type
    /// </summary>
    public class SerializationStatistics
    {
        public string DataType { get; }
        public int OperationCount { get; private set; }
        public double TotalTime { get; private set; }
        public double TotalOriginalSize { get; private set; }
        public double TotalCompressedSize { get; private set; }
        public DateTime FirstOperation { get; private set; }
        public DateTime LastOperation { get; private set; }
        
        public SerializationStatistics(string dataType)
        {
            DataType = dataType;
            FirstOperation = DateTime.UtcNow;
        }
        
        public void AddMetrics(SerializationMetrics metrics)
        {
            OperationCount++;
            TotalTime += metrics.TotalTime.TotalMilliseconds;
            TotalOriginalSize += metrics.JsonSize;
            TotalCompressedSize += metrics.CompressedSize;
            LastOperation = DateTime.UtcNow;
        }
        
        public PerformanceSummary GetSummary()
        {
            if (OperationCount == 0)
                return new PerformanceSummary();
            
            return new PerformanceSummary
            {
                TotalOperations = OperationCount,
                AverageTotalTime = TotalTime / OperationCount,
                AverageJsonSize = TotalOriginalSize / OperationCount,
                AverageCompressedSize = TotalCompressedSize / OperationCount,
                AverageCompressionRatio = TotalOriginalSize > 0 ? TotalCompressedSize / TotalOriginalSize : 1.0
            };
        }
    }
    
    /// <summary>
    /// Overall performance summary
    /// </summary>
    public class PerformanceSummary
    {
        public int TotalOperations { get; set; }
        public double AverageSerializationTime { get; set; }
        public double AverageCompressionTime { get; set; }
        public double AverageEncodingTime { get; set; }
        public double AverageTotalTime { get; set; }
        
        public double AverageJsonSize { get; set; }
        public double AverageCompressedSize { get; set; }
        public double AverageEncodedSize { get; set; }
        
        public double AverageCompressionRatio { get; set; }
        public double AverageOverallRatio { get; set; }
        
        public double FastestOperation { get; set; }
        public double SlowestOperation { get; set; }
        
        public double SmallestOriginalSize { get; set; }
        public double LargestOriginalSize { get; set; }
        
        public double BestCompressionRatio { get; set; }
        public double WorstCompressionRatio { get; set; }
    }
    
    /// <summary>
    /// Performance trends over time
    /// </summary>
    public class PerformanceTrends
    {
        public double SerializationTimeTrend { get; set; }
        public double CompressionTimeTrend { get; set; }
        public double TotalTimeTrend { get; set; }
        public double CompressionRatioTrend { get; set; }
        public double OverallEfficiencyTrend { get; set; }
        
        public bool HasTrends => SerializationTimeTrend != 0 || CompressionTimeTrend != 0 || TotalTimeTrend != 0;
    }
    
    /// <summary>
    /// Performance alert
    /// </summary>
    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
    }
    
    public enum AlertType
    {
        SlowOperation,
        PerformanceDegradation,
        PoorCompression,
        MemoryUsage,
        ErrorRate
    }
    
    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
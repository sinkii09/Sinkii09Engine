using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// High-performance monitoring for ResourcePathResolver operations
    /// Tracks resolution times, template usage, and performance bottlenecks
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly object _statisticsLock = new object();
        private readonly ConcurrentDictionary<string, TemplateUsageStats> _templateUsage;
        private readonly ConcurrentQueue<ResolutionMetric> _recentResolutions;
        private readonly int _maxRecentResolutions;
        private readonly float _maxResolutionTimeMs;

        // Performance counters
        private long _totalResolutions;
        private long _totalResolutionTimeMs;
        private long _maxResolutionTimeMsCounter;
        private long _minResolutionTimeMs = long.MaxValue;
        private DateTime _lastResetTime;

        // Alert thresholds
        private readonly float _slowResolutionThresholdMs;
        private long _slowResolutionCount;

        public PerformanceMonitor(float maxResolutionTimeMs = 5.0f, int maxRecentResolutions = 1000)
        {
            _maxResolutionTimeMs = maxResolutionTimeMs;
            _maxRecentResolutions = maxRecentResolutions;
            _slowResolutionThresholdMs = maxResolutionTimeMs * 0.8f; // 80% of max as warning threshold

            _templateUsage = new ConcurrentDictionary<string, TemplateUsageStats>();
            _recentResolutions = new ConcurrentQueue<ResolutionMetric>();
            _lastResetTime = DateTime.UtcNow;

            UnityEngine.Debug.Log($"[PerformanceMonitor] Initialized with {_maxResolutionTimeMs}ms max resolution time");
        }

        #region Performance Tracking

        /// <summary>
        /// Records a path resolution operation
        /// </summary>
        public void RecordResolution(string templateKey, TimeSpan resolutionTime, bool cacheHit, string resolvedPath)
        {
            var resolutionTimeMs = (long)resolutionTime.TotalMilliseconds;

            // Update global counters
            Interlocked.Increment(ref _totalResolutions);
            Interlocked.Add(ref _totalResolutionTimeMs, resolutionTimeMs);

            // Update min/max times
            UpdateMinMaxTimes(resolutionTimeMs);

            // Check for slow resolutions
            if (resolutionTimeMs > _slowResolutionThresholdMs)
            {
                Interlocked.Increment(ref _slowResolutionCount);
                LogSlowResolution(templateKey, resolutionTimeMs, resolvedPath);
            }

            // Record template usage
            RecordTemplateUsage(templateKey, resolutionTime, cacheHit);

            // Store recent resolution for trend analysis
            StoreRecentResolution(templateKey, resolutionTime, cacheHit, resolvedPath);
        }

        private void UpdateMinMaxTimes(long resolutionTimeMs)
        {
            // Update max time
            long currentMax;
            do
            {
                currentMax = _maxResolutionTimeMsCounter;
                if (resolutionTimeMs <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _maxResolutionTimeMsCounter, resolutionTimeMs, currentMax) != currentMax);

            // Update min time
            long currentMin;
            do
            {
                currentMin = _minResolutionTimeMs;
                if (resolutionTimeMs >= currentMin) break;
            } while (Interlocked.CompareExchange(ref _minResolutionTimeMs, resolutionTimeMs, currentMin) != currentMin);
        }

        private void RecordTemplateUsage(string templateKey, TimeSpan resolutionTime, bool cacheHit)
        {
            _templateUsage.AddOrUpdate(templateKey,
                new TemplateUsageStats(templateKey, resolutionTime, cacheHit),
                (key, existing) =>
                {
                    existing.RecordUsage(resolutionTime, cacheHit);
                    return existing;
                });
        }

        private void StoreRecentResolution(string templateKey, TimeSpan resolutionTime, bool cacheHit, string resolvedPath)
        {
            var metric = new ResolutionMetric
            {
                TemplateKey = templateKey,
                ResolutionTime = resolutionTime,
                CacheHit = cacheHit,
                ResolvedPath = resolvedPath,
                Timestamp = DateTime.UtcNow
            };

            _recentResolutions.Enqueue(metric);

            // Maintain size limit
            while (_recentResolutions.Count > _maxRecentResolutions)
            {
                _recentResolutions.TryDequeue(out _);
            }
        }

        private void LogSlowResolution(string templateKey, long resolutionTimeMs, string resolvedPath)
        {
            if (resolutionTimeMs > _maxResolutionTimeMs)
            {
                UnityEngine.Debug.LogWarning($"[PerformanceMonitor] SLOW RESOLUTION: {templateKey} took {resolutionTimeMs}ms " +
                                           $"(>{_maxResolutionTimeMs}ms threshold) -> {resolvedPath}");
            }
        }

        #endregion

        #region Statistics and Reporting

        /// <summary>
        /// Gets comprehensive performance statistics
        /// </summary>
        public PerformanceStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                var avgResolutionTime = _totalResolutions > 0 ?
                    (double)_totalResolutionTimeMs / _totalResolutions : 0.0;

                var recentResolutions = _recentResolutions.ToArray();
                var recentAvgTime = recentResolutions.Length > 0 ?
                    recentResolutions.Average(r => r.ResolutionTime.TotalMilliseconds) : 0.0;

                var cacheHitRate = recentResolutions.Length > 0 ?
                    (double)recentResolutions.Count(r => r.CacheHit) / recentResolutions.Length : 0.0;

                return new PerformanceStatistics
                {
                    TotalResolutions = _totalResolutions,
                    AverageResolutionTimeMs = avgResolutionTime,
                    RecentAverageResolutionTimeMs = recentAvgTime,
                    MaxResolutionTimeMs = _maxResolutionTimeMsCounter,
                    MinResolutionTimeMs = _minResolutionTimeMs == long.MaxValue ? 0 : _minResolutionTimeMs,
                    SlowResolutionCount = _slowResolutionCount,
                    CacheHitRate = cacheHitRate,
                    MostUsedTemplates = GetMostUsedTemplates(10),
                    SlowestTemplates = GetSlowestTemplates(10),
                    RecentResolutionTrend = GetResolutionTrend(),
                    UptimeSeconds = (DateTime.UtcNow - _lastResetTime).TotalSeconds
                };
            }
        }

        private List<TemplateUsageStats> GetMostUsedTemplates(int count)
        {
            return _templateUsage.Values
                .OrderByDescending(t => t.UsageCount)
                .Take(count)
                .ToList();
        }

        private List<TemplateUsageStats> GetSlowestTemplates(int count)
        {
            return _templateUsage.Values
                .Where(t => t.UsageCount > 0)
                .OrderByDescending(t => t.AverageResolutionTimeMs)
                .Take(count)
                .ToList();
        }

        private ResolutionTrend GetResolutionTrend()
        {
            var recentResolutions = _recentResolutions.ToArray();
            if (recentResolutions.Length < 10) return ResolutionTrend.Stable;

            // Analyze trend over recent resolutions
            var recentHalf = recentResolutions.Skip(recentResolutions.Length / 2).ToArray();
            var olderHalf = recentResolutions.Take(recentResolutions.Length / 2).ToArray();

            var recentAvg = recentHalf.Average(r => r.ResolutionTime.TotalMilliseconds);
            var olderAvg = olderHalf.Average(r => r.ResolutionTime.TotalMilliseconds);

            var change = (recentAvg - olderAvg) / olderAvg;

            if (change > 0.1) return ResolutionTrend.Degrading;
            if (change < -0.1) return ResolutionTrend.Improving;
            return ResolutionTrend.Stable;
        }

        /// <summary>
        /// Gets detailed template statistics
        /// </summary>
        public Dictionary<string, TemplateUsageStats> GetTemplateStatistics()
        {
            return new Dictionary<string, TemplateUsageStats>(_templateUsage);
        }

        /// <summary>
        /// Resets all performance counters
        /// </summary>
        public void Reset()
        {
            lock (_statisticsLock)
            {
                Interlocked.Exchange(ref _totalResolutions, 0);
                Interlocked.Exchange(ref _totalResolutionTimeMs, 0);
                Interlocked.Exchange(ref _maxResolutionTimeMsCounter, 0);
                Interlocked.Exchange(ref _minResolutionTimeMs, long.MaxValue);
                Interlocked.Exchange(ref _slowResolutionCount, 0);

                _templateUsage.Clear();

                // Clear recent resolutions
                while (_recentResolutions.TryDequeue(out _)) { }

                _lastResetTime = DateTime.UtcNow;

                UnityEngine.Debug.Log("[PerformanceMonitor] Performance counters reset");
            }
        }

        #endregion

        #region Health Checks

        /// <summary>
        /// Performs a health check on path resolution performance
        /// </summary>
        public HealthCheckResult PerformHealthCheck()
        {
            var stats = GetStatistics();
            var issues = new List<string>();

            // Check average resolution time
            if (stats.AverageResolutionTimeMs > _maxResolutionTimeMs)
            {
                issues.Add($"Average resolution time ({stats.AverageResolutionTimeMs:F2}ms) exceeds threshold ({_maxResolutionTimeMs}ms)");
            }

            // Check for too many slow resolutions
            var slowResolutionRate = _totalResolutions > 0 ? (double)_slowResolutionCount / _totalResolutions : 0.0;
            if (slowResolutionRate > 0.05) // More than 5% slow resolutions
            {
                issues.Add($"High slow resolution rate: {slowResolutionRate:P1} ({_slowResolutionCount}/{_totalResolutions})");
            }

            // Check cache hit rate
            if (stats.CacheHitRate < 0.8) // Less than 80% cache hit rate
            {
                issues.Add($"Low cache hit rate: {stats.CacheHitRate:P1}");
            }

            // Check for degrading performance trend
            if (stats.RecentResolutionTrend == ResolutionTrend.Degrading)
            {
                issues.Add("Performance trend is degrading");
            }

            var isHealthy = issues.Count == 0;
            return new HealthCheckResult
            {
                IsHealthy = isHealthy,
                Issues = issues,
                Statistics = stats
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class TemplateUsageStats
    {
        public string TemplateKey { get; }
        public long UsageCount { get; private set; }
        public long CacheHits { get; private set; }
        public long CacheMisses { get; private set; }
        public double TotalResolutionTimeMs { get; private set; }
        public double AverageResolutionTimeMs => UsageCount > 0 ? TotalResolutionTimeMs / UsageCount : 0.0;
        public double CacheHitRate => UsageCount > 0 ? (double)CacheHits / UsageCount : 0.0;
        public DateTime FirstUsed { get; }
        public DateTime LastUsed { get; private set; }

        public TemplateUsageStats(string templateKey, TimeSpan initialResolutionTime, bool cacheHit)
        {
            TemplateKey = templateKey;
            UsageCount = 1;
            CacheHits = cacheHit ? 1 : 0;
            CacheMisses = cacheHit ? 0 : 1;
            TotalResolutionTimeMs = initialResolutionTime.TotalMilliseconds;
            FirstUsed = DateTime.UtcNow;
            LastUsed = DateTime.UtcNow;
        }

        public void RecordUsage(TimeSpan resolutionTime, bool cacheHit)
        {
            UsageCount++;
            TotalResolutionTimeMs += resolutionTime.TotalMilliseconds;
            LastUsed = DateTime.UtcNow;

            if (cacheHit)
                CacheHits++;
            else
                CacheMisses++;
        }

        public override string ToString()
        {
            return $"{TemplateKey}: {UsageCount} uses, {AverageResolutionTimeMs:F2}ms avg, {CacheHitRate:P1} hit rate";
        }
    }

    public class ResolutionMetric
    {
        public string TemplateKey { get; set; }
        public TimeSpan ResolutionTime { get; set; }
        public bool CacheHit { get; set; }
        public string ResolvedPath { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PerformanceStatistics
    {
        public long TotalResolutions { get; set; }
        public double AverageResolutionTimeMs { get; set; }
        public double RecentAverageResolutionTimeMs { get; set; }
        public long MaxResolutionTimeMs { get; set; }
        public long MinResolutionTimeMs { get; set; }
        public long SlowResolutionCount { get; set; }
        public double CacheHitRate { get; set; }
        public List<TemplateUsageStats> MostUsedTemplates { get; set; }
        public List<TemplateUsageStats> SlowestTemplates { get; set; }
        public ResolutionTrend RecentResolutionTrend { get; set; }
        public double UptimeSeconds { get; set; }

        public override string ToString()
        {
            return $"Perf Stats: {TotalResolutions} resolutions, {AverageResolutionTimeMs:F2}ms avg, " +
                   $"{CacheHitRate:P1} hit rate, {SlowResolutionCount} slow, Trend: {RecentResolutionTrend}";
        }
    }

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; }
        public PerformanceStatistics Statistics { get; set; }
    }

    public enum ResolutionTrend
    {
        Improving,
        Stable,
        Degrading
    }

    #endregion
}
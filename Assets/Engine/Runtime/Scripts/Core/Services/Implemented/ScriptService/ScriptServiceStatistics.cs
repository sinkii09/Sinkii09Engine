using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Comprehensive statistics for script service performance monitoring
    /// </summary>
    public class ScriptServiceStatistics
    {
        public long TotalScriptsLoaded { get; set; }
        public long TotalLoadFailures { get; set; }
        public long TotalReloads { get; set; }
        public long TotalValidations { get; set; }
        public double AverageLoadTimeMs { get; set; }
        public double AverageValidationTimeMs { get; set; }
        public ScriptCacheInfo CacheInfo { get; set; } = new ScriptCacheInfo();
        public int ActiveLoadingOperations { get; set; }
        public DateTime ServiceStartTime { get; set; }
        public TimeSpan ServiceUptime => DateTime.UtcNow - ServiceStartTime;
        public Dictionary<string, int> ErrorCountByType { get; set; } = new Dictionary<string, int>();
        
        public float SuccessRate => TotalScriptsLoaded + TotalLoadFailures > 0 ? 
            (float)TotalScriptsLoaded / (TotalScriptsLoaded + TotalLoadFailures) * 100f : 0f;
        
        public string GetPerformanceSummary()
        {
            return $"ScriptService Stats: {TotalScriptsLoaded} loaded, " +
                   $"Success Rate: {SuccessRate:F1}%, " +
                   $"Avg Load: {AverageLoadTimeMs:F1}ms, " +
                   $"Uptime: {ServiceUptime:dd\\.hh\\:mm\\:ss}";
        }
    }
}
using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Cache information for script service monitoring
    /// </summary>
    public class ScriptCacheInfo
    {
        public int LoadedScriptCount { get; set; }
        public int MaxCacheSize { get; set; }
        public float CacheUsagePercentage => MaxCacheSize > 0 ? (float)LoadedScriptCount / MaxCacheSize * 100f : 0f;
        public long EstimatedMemoryUsage { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public float CacheHitRatio => (CacheHits + CacheMisses) > 0 ? (float)CacheHits / (CacheHits + CacheMisses) * 100f : 0f;
        public DateTime LastCacheCleanup { get; set; }
        public List<string> LoadedScriptNames { get; set; } = new List<string>();
        
        public string GetCacheSummary()
        {
            return $"Cache: {LoadedScriptCount}/{MaxCacheSize} ({CacheUsagePercentage:F1}%), " +
                   $"Hit Ratio: {CacheHitRatio:F1}%, " +
                   $"Memory: {EstimatedMemoryUsage / 1024 / 1024:F1}MB";
        }
    }
}
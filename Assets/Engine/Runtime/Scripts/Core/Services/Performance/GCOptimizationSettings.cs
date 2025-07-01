using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Settings for optimizing garbage collection to prevent FPS drops
    /// </summary>
    [CreateAssetMenu(fileName = "GCOptimizationSettings", menuName = "Engine/Performance/GC Optimization Settings")]
    public class GCOptimizationSettings : ScriptableObject
    {
        [Header("Incremental GC Settings")]
        [Tooltip("Enable Unity's incremental garbage collector for smoother performance")]
        public bool EnableIncrementalGC = true;
        
        [Tooltip("Maximum milliseconds to spend on GC per frame")]
        [Range(0.5f, 5f)]
        public float MaxMillisecondsPerFrame = 2f;
        
        [Tooltip("Enable async collection spreading over multiple frames")]
        public bool EnableAsyncCollection = true;
        
        [Tooltip("Disable aggressive forced GC collections")]
        public bool DisableAggressiveGC = true;
        
        [Header("Memory Pressure Settings")]
        [Tooltip("Use Unity's Application.lowMemory callback instead of polling")]
        public bool UseUnityLowMemoryCallback = true;
        
        [Tooltip("Only perform GC when frame time is below this threshold (ms)")]
        [Range(8f, 33f)]
        public float FrameTimeBudgetMs = 16.67f; // 60 FPS target
        
        [Header("Collection Strategy")]
        [Tooltip("Minimum time between GC collections (seconds)")]
        [Range(10f, 120f)]
        public float MinCollectionInterval = 60f;
        
        [Tooltip("Use incremental collection for each generation")]
        public bool UseIncrementalForGen0 = true;
        public bool UseIncrementalForGen1 = true;
        public bool UseIncrementalForGen2 = true;
        
        [Header("Monitoring")]
        [Tooltip("Enable performance monitoring and metrics")]
        public bool EnableMetrics = true;
        
        [Tooltip("Log warnings when GC takes too long")]
        public bool LogGCWarnings = true;
        
        [Tooltip("GC duration threshold for warnings (ms)")]
        [Range(1f, 10f)]
        public float GCWarningThresholdMs = 5f;
        
        /// <summary>
        /// Apply these settings to Unity's GC system
        /// </summary>
        public void ApplySettings()
        {
            if (EnableIncrementalGC)
            {
                // Unity 2021.3+ uses Enabled for incremental GC
                UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Enabled;
                UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds = (ulong)(MaxMillisecondsPerFrame * 1_000_000);
            }
            else
            {
                UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled;
            }
            
            if (EnableMetrics)
            {
                Debug.Log($"GC Optimization Settings Applied: Mode={UnityEngine.Scripting.GarbageCollector.GCMode}, TimeSlice={MaxMillisecondsPerFrame}ms");
            }
        }
        
        /// <summary>
        /// Get default settings optimized for performance
        /// </summary>
        public static GCOptimizationSettings GetDefaultSettings()
        {
            var settings = CreateInstance<GCOptimizationSettings>();
            settings.ApplySettings();
            return settings;
        }
    }
}
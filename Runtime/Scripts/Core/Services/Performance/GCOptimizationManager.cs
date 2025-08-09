using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Manages garbage collection optimization to prevent FPS drops
    /// Provides frame-aware incremental GC and performance monitoring
    /// </summary>
    public class GCOptimizationManager : MonoBehaviour
    {
        private static GCOptimizationManager _instance;
        private GCOptimizationSettings _settings;
        private float _lastGCTime;
        private bool _isPerformingGC;
        
        // Performance metrics
        private int _gcFrameCount;
        private float _totalGCTime;
        private float _maxGCTime;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static GCOptimizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GC Optimization Manager]");
                    _instance = go.AddComponent<GCOptimizationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Current GC optimization settings
        /// </summary>
        public GCOptimizationSettings Settings => _settings;
        
        /// <summary>
        /// Average GC time per frame (ms)
        /// </summary>
        public float AverageGCTime => _gcFrameCount > 0 ? _totalGCTime / _gcFrameCount : 0f;
        
        /// <summary>
        /// Maximum GC time recorded (ms)
        /// </summary>
        public float MaxGCTime => _maxGCTime;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load or create default settings
            _settings = Resources.Load<GCOptimizationSettings>("GCOptimizationSettings") 
                       ?? GCOptimizationSettings.GetDefaultSettings();
            
            // Apply settings
            _settings.ApplySettings();
            
            // Register for low memory callback
            if (_settings.UseUnityLowMemoryCallback)
            {
                Application.lowMemory += OnLowMemory;
            }
            
            Debug.Log($"GC Optimization Manager initialized with mode: {GarbageCollector.GCMode}");
        }
        
        /// <summary>
        /// Request incremental garbage collection with frame budget awareness
        /// </summary>
        public async UniTask RequestIncrementalGCAsync(int generation = 0)
        {
            if (_isPerformingGC)
                return;
                
            // Check if enough time has passed since last GC
            if (Time.time - _lastGCTime < _settings.MinCollectionInterval)
                return;
                
            _isPerformingGC = true;
            
            try
            {
                // Wait for a good frame if needed
                if (_settings.EnableAsyncCollection)
                {
                    await WaitForGoodFrameAsync();
                }
                
                // Perform incremental collection
                await PerformIncrementalGCAsync(generation);
                
                _lastGCTime = Time.time;
            }
            finally
            {
                _isPerformingGC = false;
            }
        }
        
        /// <summary>
        /// Wait for a frame with enough time budget
        /// </summary>
        private async UniTask WaitForGoodFrameAsync()
        {
            const int maxWaitFrames = 5;
            int waitedFrames = 0;
            
            while (waitedFrames < maxWaitFrames)
            {
                var frameTime = Time.deltaTime * 1000f;
                if (frameTime < _settings.FrameTimeBudgetMs)
                {
                    break;
                }
                
                await UniTask.Yield();
                waitedFrames++;
            }
        }
        
        /// <summary>
        /// Perform incremental GC with performance tracking
        /// </summary>
        private async UniTask PerformIncrementalGCAsync(int generation)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var nanosecondBudget = (ulong)(_settings.MaxMillisecondsPerFrame * 1_000_000);
                
                switch (generation)
                {
                    case 0:
                        if (_settings.UseIncrementalForGen0)
                        {
                            GarbageCollector.CollectIncremental(nanosecondBudget);
                        }
                        break;
                        
                    case 1:
                        if (_settings.UseIncrementalForGen1)
                        {
                            GarbageCollector.CollectIncremental(nanosecondBudget);
                            await UniTask.Yield();
                            GarbageCollector.CollectIncremental(nanosecondBudget);
                        }
                        break;
                        
                    case 2:
                        if (_settings.UseIncrementalForGen2)
                        {
                            // Spread Gen2 over multiple frames
                            for (int i = 0; i < 3; i++)
                            {
                                GarbageCollector.CollectIncremental(nanosecondBudget);
                                await UniTask.Yield();
                                
                                // Abort if frame time is too high
                                if (Time.deltaTime * 1000f > _settings.FrameTimeBudgetMs * 1.5f)
                                {
                                    break;
                                }
                            }
                        }
                        break;
                }
            }
            finally
            {
                stopwatch.Stop();
                var gcTime = (float)stopwatch.Elapsed.TotalMilliseconds;
                
                // Update metrics
                _gcFrameCount++;
                _totalGCTime += gcTime;
                _maxGCTime = Mathf.Max(_maxGCTime, gcTime);
                
                // Log warning if GC took too long
                if (_settings.LogGCWarnings && gcTime > _settings.GCWarningThresholdMs)
                {
                    Debug.LogWarning($"GC generation {generation} took {gcTime:F1}ms (threshold: {_settings.GCWarningThresholdMs}ms)");
                }
            }
        }
        
        /// <summary>
        /// Handle Unity's low memory callback
        /// </summary>
        private void OnLowMemory()
        {
            Debug.LogWarning("Unity low memory warning - triggering incremental GC");
            _ = RequestIncrementalGCAsync(2);
        }
        
        /// <summary>
        /// Update GC optimization settings at runtime
        /// </summary>
        public void UpdateSettings(GCOptimizationSettings newSettings)
        {
            if (newSettings == null)
                return;
                
            _settings = newSettings;
            _settings.ApplySettings();
            
            Debug.Log($"GC settings updated: Mode={GarbageCollector.GCMode}, TimeSlice={_settings.MaxMillisecondsPerFrame}ms");
        }
        
        /// <summary>
        /// Get current GC statistics
        /// </summary>
        public GCStatistics GetStatistics()
        {
            return new GCStatistics
            {
                Mode = GarbageCollector.GCMode,
                IncrementalTimeSliceNanoseconds = GarbageCollector.incrementalTimeSliceNanoseconds,
                AverageGCTimeMs = AverageGCTime,
                MaxGCTimeMs = MaxGCTime,
                TotalGCFrames = _gcFrameCount,
                IsPerformingGC = _isPerformingGC,
                TimeSinceLastGC = Time.time - _lastGCTime
            };
        }
        
        private void OnDestroy()
        {
            if (_settings?.UseUnityLowMemoryCallback == true)
            {
                Application.lowMemory -= OnLowMemory;
            }
        }
        
        /// <summary>
        /// GC performance statistics
        /// </summary>
        public struct GCStatistics
        {
            public GarbageCollector.Mode Mode;
            public ulong IncrementalTimeSliceNanoseconds;
            public float AverageGCTimeMs;
            public float MaxGCTimeMs;
            public int TotalGCFrames;
            public bool IsPerformingGC;
            public float TimeSinceLastGC;
            
            public override string ToString()
            {
                return $"GC Stats: Mode={Mode}, AvgTime={AverageGCTimeMs:F1}ms, MaxTime={MaxGCTimeMs:F1}ms, " +
                       $"Frames={TotalGCFrames}, TimeSinceGC={TimeSinceLastGC:F1}s";
            }
        }
    }
}
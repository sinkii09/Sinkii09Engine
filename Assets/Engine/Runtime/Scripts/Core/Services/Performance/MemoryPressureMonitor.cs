using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Monitors memory usage and triggers automatic cleanup operations
    /// Implements adaptive memory management with configurable thresholds and response strategies
    /// Enhanced with frame-aware incremental GC to prevent FPS drops
    /// </summary>
    public class MemoryPressureMonitor : IDisposable
    {
        #region Constants
        private const int MAX_CLEANUP_HISTORY = 100;
        private const long COUNTER_RESET_THRESHOLD = 1_000_000;
        private const int ADAPTIVE_THRESHOLD_MIN_CYCLES = 100;
        private const double ADAPTIVE_THRESHOLD_FACTOR = 0.8;
        private const double ADAPTIVE_ADJUSTMENT_FACTOR = 0.95;
        private const double PERCENTAGE_THRESHOLD_LOW = 0.6;
        private const double PERCENTAGE_THRESHOLD_MEDIUM = 0.75;
        private const double PERCENTAGE_THRESHOLD_HIGH = 0.85;
        private const double PERCENTAGE_THRESHOLD_CRITICAL = 0.95;
        private const long MINIMUM_BASE_MEMORY = 100 * 1024 * 1024; // 100MB
        #endregion

        public enum MemoryPressureLevel
        {
            None = 0,
            Low = 1,
            Medium = 2,
            High = 3,
            Critical = 4
        }
        
        public enum CleanupStrategy
        {
            Conservative,  // Only essential cleanup
            Moderate,      // Balanced cleanup
            Aggressive,    // Maximum cleanup
            Custom         // User-defined strategy
        }
        
        private readonly Timer _monitoringTimer;
        private readonly List<IMemoryPressureResponder> _responders;
        private readonly object _respondersLock = new object();
        private readonly MemoryPressureConfiguration _config;
        private readonly GCOptimizationSettings _gcSettings;
        private readonly Queue<MemoryCleanupResult> _cleanupHistory;
        
        // Memory thresholds (in bytes)
        private long _lowPressureThreshold;
        private long _mediumPressureThreshold;
        private long _highPressureThreshold;
        private long _criticalPressureThreshold;
        
        // Current state
        private MemoryPressureLevel _currentPressureLevel;
        private long _lastMemoryUsage;
        private DateTime _lastCleanup;
        private bool _disposed;
        
        // Performance metrics
        private long _monitoringCycles;
        private long _cleanupOperations;
        private long _memoryReclaimed;
        private double _averageMemoryUsage;
        
        /// <summary>
        /// Current memory pressure level
        /// </summary>
        public MemoryPressureLevel CurrentPressureLevel => _currentPressureLevel;
        
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage => GC.GetTotalMemory(false);
        
        /// <summary>
        /// Number of monitoring cycles performed
        /// </summary>
        public long MonitoringCycles => _monitoringCycles;
        
        /// <summary>
        /// Number of cleanup operations triggered
        /// </summary>
        public long CleanupOperations => _cleanupOperations;
        
        /// <summary>
        /// Total memory reclaimed through cleanup operations
        /// </summary>
        public long MemoryReclaimed => _memoryReclaimed;
        
        /// <summary>
        /// Average memory usage over time
        /// </summary>
        public double AverageMemoryUsage => _averageMemoryUsage;
        
        public MemoryPressureMonitor(MemoryPressureConfiguration config = null, GCOptimizationSettings gcSettings = null)
        {
            _config = config ?? MemoryPressureConfiguration.Default();
            _gcSettings = gcSettings ?? GCOptimizationSettings.GetDefaultSettings();
            _responders = new List<IMemoryPressureResponder>();
            _cleanupHistory = new Queue<MemoryCleanupResult>();
            _currentPressureLevel = MemoryPressureLevel.None;
            _lastCleanup = DateTime.UtcNow;
            
            InitializeThresholds();
            
            // Apply GC optimization settings
            _gcSettings.ApplySettings();
            
            // Register for Unity's low memory callback if enabled
            if (_gcSettings.UseUnityLowMemoryCallback)
            {
                Application.lowMemory += OnUnityLowMemory;
            }
            
            // Start monitoring timer
            _monitoringTimer = new Timer(MonitorMemoryPressure, null, 
                _config.MonitoringInterval, _config.MonitoringInterval);
        }
        
        /// <summary>
        /// Register a memory pressure responder
        /// </summary>
        public void RegisterResponder(IMemoryPressureResponder responder)
        {
            if (responder == null)
                return;
                
            lock (_respondersLock)
            {
                if (!_responders.Contains(responder))
                {
                    _responders.Add(responder);
                }
            }
        }
        
        /// <summary>
        /// Unregister a memory pressure responder
        /// </summary>
        public void UnregisterResponder(IMemoryPressureResponder responder)
        {
            if (responder == null)
                return;
                
            lock (_respondersLock)
            {
                _responders.Remove(responder);
            }
        }
        
        /// <summary>
        /// Force a memory pressure check and cleanup if needed
        /// </summary>
        public async UniTask<MemoryCleanupResult> ForceCleanupAsync(CleanupStrategy strategy = CleanupStrategy.Moderate)
        {
            var beforeMemory = CurrentMemoryUsage;
            var result = await PerformCleanupAsync(_currentPressureLevel, strategy, true);
            var afterMemory = CurrentMemoryUsage;
            
            result.MemoryReclaimed = Math.Max(0, beforeMemory - afterMemory);
            Interlocked.Add(ref _memoryReclaimed, result.MemoryReclaimed);
            
            return result;
        }
        
        /// <summary>
        /// Get current memory pressure information
        /// </summary>
        public MemoryPressureInfo GetMemoryPressureInfo()
        {
            var currentMemory = CurrentMemoryUsage;
            var pressureLevel = CalculatePressureLevel(currentMemory);
            
            return new MemoryPressureInfo
            {
                CurrentMemoryUsage = currentMemory,
                PressureLevel = pressureLevel,
                LowThreshold = _lowPressureThreshold,
                MediumThreshold = _mediumPressureThreshold,
                HighThreshold = _highPressureThreshold,
                CriticalThreshold = _criticalPressureThreshold,
                LastCleanupTime = _lastCleanup,
                TimeSinceLastCleanup = DateTime.UtcNow - _lastCleanup,
                ResponderCount = _responders.Count
            };
        }
        
        /// <summary>
        /// Configure memory pressure thresholds
        /// </summary>
        public void ConfigureThresholds(long lowThreshold, long mediumThreshold, long highThreshold, long criticalThreshold)
        {
            if (lowThreshold >= mediumThreshold || mediumThreshold >= highThreshold || highThreshold >= criticalThreshold)
            {
                throw new ArgumentException("Thresholds must be in ascending order");
            }
            
            _lowPressureThreshold = lowThreshold;
            _mediumPressureThreshold = mediumThreshold;
            _highPressureThreshold = highThreshold;
            _criticalPressureThreshold = criticalThreshold;
        }
        
        /// <summary>
        /// Enable or disable adaptive threshold adjustment
        /// </summary>
        public void SetAdaptiveThresholds(bool enabled)
        {
            _config.UseAdaptiveThresholds = enabled;
        }
        
        /// <summary>
        /// Initialize memory thresholds based on available memory
        /// </summary>
        private void InitializeThresholds()
        {
            // Get available memory (rough estimation)
            var availableMemory = GC.GetTotalMemory(false);
            var baseMemory = Math.Max(availableMemory, MINIMUM_BASE_MEMORY);
            
            if (_config.UseAbsoluteThresholds)
            {
                _lowPressureThreshold = _config.LowPressureThreshold;
                _mediumPressureThreshold = _config.MediumPressureThreshold;
                _highPressureThreshold = _config.HighPressureThreshold;
                _criticalPressureThreshold = _config.CriticalPressureThreshold;
            }
            else
            {
                // Use percentage-based thresholds
                _lowPressureThreshold = (long)(baseMemory * PERCENTAGE_THRESHOLD_LOW);
                _mediumPressureThreshold = (long)(baseMemory * PERCENTAGE_THRESHOLD_MEDIUM);
                _highPressureThreshold = (long)(baseMemory * PERCENTAGE_THRESHOLD_HIGH);
                _criticalPressureThreshold = (long)(baseMemory * PERCENTAGE_THRESHOLD_CRITICAL);
            }
        }
        
        /// <summary>
        /// Monitor memory pressure (timer callback)
        /// </summary>
        private void MonitorMemoryPressure(object state)
        {
            if (_disposed || !Application.isPlaying)
                return;
                
            // Skip expensive profiling operations in editor but keep monitoring
#if UNITY_EDITOR
            PerformLightweightMonitoring();
            return;
#endif
                
            try
            {
                Interlocked.Increment(ref _monitoringCycles);
                
                var currentMemory = CurrentMemoryUsage;
                var newPressureLevel = CalculatePressureLevel(currentMemory);
                
                // Update average memory usage
                UpdateAverageMemoryUsage(currentMemory);
                
                // Reset counters if needed to prevent overflow
                ResetCountersIfNeeded();
                
                // Check if pressure level changed or cleanup is needed
                if (newPressureLevel != _currentPressureLevel || ShouldPerformCleanup(newPressureLevel))
                {
                    var previousLevel = _currentPressureLevel;
                    _currentPressureLevel = newPressureLevel;
                    
                    // Notify about pressure level change
                    if (newPressureLevel != previousLevel)
                    {
                        NotifyPressureLevelChanged(previousLevel, newPressureLevel);
                    }
                    
                    // Perform cleanup if needed
                    if (newPressureLevel > MemoryPressureLevel.None)
                    {
                        _ = PerformCleanupAsync(newPressureLevel, GetCleanupStrategy(newPressureLevel));
                    }
                }
                
                _lastMemoryUsage = currentMemory;
                
                // Adaptive threshold adjustment
                if (_config.UseAdaptiveThresholds)
                {
                    AdjustThresholdsAdaptively(currentMemory);
                }
            }
            catch (Exception ex)
            {
                if (Application.isPlaying && !_disposed)
                {
                    Debug.LogError($"Error in memory pressure monitoring: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Calculate current pressure level based on memory usage
        /// </summary>
        private MemoryPressureLevel CalculatePressureLevel(long currentMemory)
        {
            if (currentMemory >= _criticalPressureThreshold)
                return MemoryPressureLevel.Critical;
            if (currentMemory >= _highPressureThreshold)
                return MemoryPressureLevel.High;
            if (currentMemory >= _mediumPressureThreshold)
                return MemoryPressureLevel.Medium;
            if (currentMemory >= _lowPressureThreshold)
                return MemoryPressureLevel.Low;
                
            return MemoryPressureLevel.None;
        }
        
        /// <summary>
        /// Check if cleanup should be performed
        /// </summary>
        private bool ShouldPerformCleanup(MemoryPressureLevel pressureLevel)
        {
            if (pressureLevel == MemoryPressureLevel.None)
                return false;
                
            var timeSinceLastCleanup = DateTime.UtcNow - _lastCleanup;
            
            // Always cleanup on critical pressure
            if (pressureLevel == MemoryPressureLevel.Critical)
                return true;
                
            // Check minimum interval between cleanups
            if (timeSinceLastCleanup < _config.MinimumCleanupInterval)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Get appropriate cleanup strategy for pressure level
        /// </summary>
        private CleanupStrategy GetCleanupStrategy(MemoryPressureLevel pressureLevel)
        {
            return pressureLevel switch
            {
                MemoryPressureLevel.Low => CleanupStrategy.Conservative,
                MemoryPressureLevel.Medium => CleanupStrategy.Moderate,
                MemoryPressureLevel.High => CleanupStrategy.Aggressive,
                MemoryPressureLevel.Critical => CleanupStrategy.Aggressive,
                _ => CleanupStrategy.Conservative
            };
        }
        
        /// <summary>
        /// Perform cleanup operations asynchronously
        /// </summary>
        private async UniTask<MemoryCleanupResult> PerformCleanupAsync(MemoryPressureLevel pressureLevel, CleanupStrategy strategy, bool force = false)
        {
            try
            {
                Interlocked.Increment(ref _cleanupOperations);
                _lastCleanup = DateTime.UtcNow;
                
                var result = new MemoryCleanupResult
                {
                    PressureLevel = pressureLevel,
                    Strategy = strategy,
                    StartTime = DateTime.UtcNow,
                    ResponderResults = new List<ResponderCleanupResult>()
                };
                
                var beforeMemory = CurrentMemoryUsage;
                
                // Execute responders in parallel
                var tasks = new List<UniTask<ResponderCleanupResult>>();
                
                lock (_respondersLock)
                {
                    foreach (var responder in _responders)
                    {
                        tasks.Add(ExecuteResponderAsync(responder, pressureLevel, strategy));
                    }
                }
                
                if (tasks.Count > 0)
                {
                    var responderResults = await UniTask.WhenAll(tasks);
                    result.ResponderResults.AddRange(responderResults);
                }
                
                // Perform garbage collection based on strategy
                if (_gcSettings.DisableAggressiveGC)
                {
                    // Use frame-aware incremental collection
                    await PerformFrameAwareGCAsync(strategy, pressureLevel);
                }
                else
                {
                    // Legacy aggressive collection (not recommended)
                    PerformLegacyGC(strategy, pressureLevel);
                }
                
                var afterMemory = CurrentMemoryUsage;
                result.MemoryReclaimed = Math.Max(0, beforeMemory - afterMemory);
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                result.Success = true;
                
                // Add to cleanup history with size management
                AddToCleanupHistory(result);
                
                return result;
            }
            catch (Exception ex)
            {
                if (Application.isPlaying && !_disposed)
                {
                    Debug.LogError($"Error during memory cleanup: {ex.Message}");
                }
                return new MemoryCleanupResult
                {
                    PressureLevel = pressureLevel,
                    Strategy = strategy,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Execute a single responder asynchronously
        /// </summary>
        private async UniTask<ResponderCleanupResult> ExecuteResponderAsync(IMemoryPressureResponder responder, MemoryPressureLevel pressureLevel, CleanupStrategy strategy)
        {
            var result = new ResponderCleanupResult
            {
                ResponderName = responder.GetType().Name,
                StartTime = DateTime.UtcNow
            };
            
            try
            {
                var beforeMemory = CurrentMemoryUsage;
                await responder.RespondToMemoryPressureAsync(pressureLevel, strategy);
                var afterMemory = CurrentMemoryUsage;
                
                result.MemoryReclaimed = Math.Max(0, beforeMemory - afterMemory);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                if (Application.isPlaying && !_disposed)
                {
                    Debug.LogError($"Error in memory pressure responder {responder.GetType().Name}: {ex.Message}");
                }
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
            }
            
            return result;
        }
        
        /// <summary>
        /// Notify responders about pressure level changes
        /// </summary>
        private void NotifyPressureLevelChanged(MemoryPressureLevel previousLevel, MemoryPressureLevel newLevel)
        {
            lock (_respondersLock)
            {
                foreach (var responder in _responders)
                {
                    try
                    {
                        responder.OnMemoryPressureLevelChanged(previousLevel, newLevel);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error notifying responder {responder.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Update running average of memory usage
        /// </summary>
        private void UpdateAverageMemoryUsage(long currentMemory)
        {
            var currentAverage = _averageMemoryUsage;
            var count = _monitoringCycles;
            _averageMemoryUsage = (currentAverage * (count - 1) + currentMemory) / count;
        }
        
        /// <summary>
        /// Adaptively adjust thresholds based on usage patterns
        /// </summary>
        private void AdjustThresholdsAdaptively(long currentMemory)
        {
            // Simple adaptive logic - could be enhanced
            if (_monitoringCycles > ADAPTIVE_THRESHOLD_MIN_CYCLES) // Only adjust after sufficient data
            {
                var avgMemory = (long)_averageMemoryUsage;
                
                // If average is consistently low, lower thresholds
                if (avgMemory < _lowPressureThreshold * ADAPTIVE_THRESHOLD_FACTOR)
                {
                    _lowPressureThreshold = (long)(_lowPressureThreshold * ADAPTIVE_ADJUSTMENT_FACTOR);
                    _mediumPressureThreshold = (long)(_mediumPressureThreshold * ADAPTIVE_ADJUSTMENT_FACTOR);
                    _highPressureThreshold = (long)(_highPressureThreshold * ADAPTIVE_ADJUSTMENT_FACTOR);
                    _criticalPressureThreshold = (long)(_criticalPressureThreshold * ADAPTIVE_ADJUSTMENT_FACTOR);
                }
            }
        }
        
        /// <summary>
        /// Reset counters if they exceed threshold to prevent overflow and memory accumulation
        /// </summary>
        private void ResetCountersIfNeeded()
        {
            if (_monitoringCycles > COUNTER_RESET_THRESHOLD)
            {
                Interlocked.Exchange(ref _monitoringCycles, 0);
                Interlocked.Exchange(ref _cleanupOperations, 0);
                Interlocked.Exchange(ref _memoryReclaimed, 0);
                _averageMemoryUsage = 0;
            }
        }
        
        /// <summary>
        /// Add cleanup result to history and manage history size
        /// </summary>
        private void AddToCleanupHistory(MemoryCleanupResult result)
        {
            lock (_respondersLock)
            {
                _cleanupHistory.Enqueue(result);
                while (_cleanupHistory.Count > MAX_CLEANUP_HISTORY)
                {
                    _cleanupHistory.Dequeue();
                }
            }
        }
        
        #region Lightweight Editor Monitoring
        
        /// <summary>
        /// Lightweight monitoring for editor - avoids profiler data accumulation
        /// </summary>
        private void PerformLightweightMonitoring()
        {
            try
            {
                // Only basic counter increment - no GC calls or heavy operations
                Interlocked.Increment(ref _monitoringCycles);
                
                // Reset counters periodically to prevent overflow
                ResetCountersIfNeeded();
                
                // Update basic stats without expensive operations
                var basicMemory = Environment.WorkingSet; // Faster than GC.GetTotalMemory()
                var newPressureLevel = CalculatePressureLevelBasic(basicMemory);
                
                if (newPressureLevel != _currentPressureLevel)
                {
                    _currentPressureLevel = newPressureLevel;
                    // No cleanup operations in editor - just track state
                }
            }
            catch (Exception ex)
            {
                if (Application.isPlaying && !_disposed)
                {
                    Debug.LogError($"Error in lightweight memory monitoring: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Calculate pressure level using basic memory without GC calls
        /// </summary>
        private MemoryPressureLevel CalculatePressureLevelBasic(long currentMemory)
        {
            // Use simpler thresholds for editor monitoring
            if (currentMemory >= _criticalPressureThreshold * 2)
                return MemoryPressureLevel.Critical;
            if (currentMemory >= _highPressureThreshold * 2)
                return MemoryPressureLevel.High;
            if (currentMemory >= _mediumPressureThreshold * 2)
                return MemoryPressureLevel.Medium;
            if (currentMemory >= _lowPressureThreshold * 2)
                return MemoryPressureLevel.Low;
                
            return MemoryPressureLevel.None;
        }
        
        #endregion
        
        /// <summary>
        /// Get memory pressure statistics
        /// </summary>
        public MemoryPressureStatistics GetStatistics()
        {
            return new MemoryPressureStatistics
            {
                MonitoringCycles = _monitoringCycles,
                CleanupOperations = _cleanupOperations,
                MemoryReclaimed = _memoryReclaimed,
                AverageMemoryUsage = _averageMemoryUsage,
                CurrentMemoryUsage = CurrentMemoryUsage,
                CurrentPressureLevel = _currentPressureLevel,
                RegisteredResponders = _responders.Count,
                LowThreshold = _lowPressureThreshold,
                MediumThreshold = _mediumPressureThreshold,
                HighThreshold = _highPressureThreshold,
                CriticalThreshold = _criticalPressureThreshold
            };
        }
        
        /// <summary>
        /// Perform frame-aware incremental garbage collection
        /// </summary>
        private async UniTask PerformFrameAwareGCAsync(CleanupStrategy strategy, MemoryPressureLevel pressureLevel)
        {
            var startTime = DateTime.UtcNow;
            
            // Skip frame-aware timing when not on main thread - just proceed with GC
            if (_gcSettings.EnableAsyncCollection)
            {
                // Simple delay to avoid blocking - frame timing requires main thread
                await UniTask.Delay(16); // ~60fps frame delay
            }
            
            // Perform incremental collection based on strategy
            switch (strategy)
            {
                case CleanupStrategy.Conservative:
                    if (_gcSettings.UseIncrementalForGen0)
                    {
                        GarbageCollector.CollectIncremental((ulong)(_gcSettings.MaxMillisecondsPerFrame * 1_000_000));
                    }
                    break;
                    
                case CleanupStrategy.Moderate:
                    if (_gcSettings.UseIncrementalForGen1)
                    {
                        // Collect Gen0 and Gen1 incrementally
                        GarbageCollector.CollectIncremental((ulong)(_gcSettings.MaxMillisecondsPerFrame * 1_000_000));
                        await UniTask.Yield();
                        GarbageCollector.CollectIncremental((ulong)(_gcSettings.MaxMillisecondsPerFrame * 1_000_000));
                    }
                    break;
                    
                case CleanupStrategy.Aggressive:
                    if (_gcSettings.UseIncrementalForGen2)
                    {
                        // Spread Gen2 collection over multiple frames
                        var frameBudget = (ulong)(_gcSettings.MaxMillisecondsPerFrame * 1_000_000);
                        for (int i = 0; i < 3; i++)
                        {
                            GarbageCollector.CollectIncremental(frameBudget);
                            await UniTask.Yield();
                            
                            // Stop if frame time is too high
                            if (Time.deltaTime * 1000f > _gcSettings.FrameTimeBudgetMs * 1.5f)
                                break;
                        }
                    }
                    break;
            }
            
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (_gcSettings.LogGCWarnings && duration > _gcSettings.GCWarningThresholdMs && Application.isPlaying && !_disposed)
            {
                Debug.LogWarning($"GC took {duration:F1}ms (threshold: {_gcSettings.GCWarningThresholdMs}ms)");
            }
        }
        
        /// <summary>
        /// Legacy aggressive GC (not recommended - causes FPS drops)
        /// </summary>
        private void PerformLegacyGC(CleanupStrategy strategy, MemoryPressureLevel pressureLevel)
        {
            switch (strategy)
            {
                case CleanupStrategy.Conservative:
                    GC.Collect(0, GCCollectionMode.Optimized);
                    break;
                    
                case CleanupStrategy.Moderate:
                    GC.Collect(1, GCCollectionMode.Optimized);
                    break;
                    
                case CleanupStrategy.Aggressive:
                    GC.Collect(2, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced);
                    break;
            }
        }
        
        /// <summary>
        /// Handle Unity's low memory callback
        /// </summary>
        private void OnUnityLowMemory()
        {
            Debug.LogWarning("Unity low memory warning received");
            
            // Force a moderate cleanup
            _ = PerformCleanupAsync(MemoryPressureLevel.High, CleanupStrategy.Moderate, true);
        }
        
        /// <summary>
        /// Dispose resources and stop monitoring
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            _monitoringTimer?.Dispose();
            
            // Unregister Unity callbacks
            if (_gcSettings?.UseUnityLowMemoryCallback == true)
            {
                Application.lowMemory -= OnUnityLowMemory;
            }
            
            lock (_respondersLock)
            {
                _responders.Clear();
                _cleanupHistory.Clear();
            }
        }
    }
    
    /// <summary>
    /// Interface for memory pressure responders
    /// </summary>
    public interface IMemoryPressureResponder
    {
        UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy);
        void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel);
    }
    
    /// <summary>
    /// Configuration for memory pressure monitoring
    /// </summary>
    public class MemoryPressureConfiguration
    {
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);  // Increased from 10s
        public TimeSpan MinimumCleanupInterval { get; set; } = TimeSpan.FromSeconds(60);  // Increased from 30s
        public bool UseAdaptiveThresholds { get; set; } = true;
        public bool UseAbsoluteThresholds { get; set; } = false;
        
        // Absolute thresholds (when UseAbsoluteThresholds = true) - Increased for modern games
        public long LowPressureThreshold { get; set; } = 500 * 1024 * 1024;    // 500MB
        public long MediumPressureThreshold { get; set; } = 750 * 1024 * 1024; // 750MB
        public long HighPressureThreshold { get; set; } = 1000 * 1024 * 1024;  // 1GB
        public long CriticalPressureThreshold { get; set; } = 1500 * 1024 * 1024; // 1.5GB
        
        public static MemoryPressureConfiguration Default()
        {
            return new MemoryPressureConfiguration();
        }
    }
    
    /// <summary>
    /// Information about current memory pressure
    /// </summary>
    public struct MemoryPressureInfo
    {
        public long CurrentMemoryUsage { get; set; }
        public MemoryPressureMonitor.MemoryPressureLevel PressureLevel { get; set; }
        public long LowThreshold { get; set; }
        public long MediumThreshold { get; set; }
        public long HighThreshold { get; set; }
        public long CriticalThreshold { get; set; }
        public DateTime LastCleanupTime { get; set; }
        public TimeSpan TimeSinceLastCleanup { get; set; }
        public int ResponderCount { get; set; }
    }
    
    /// <summary>
    /// Result of a memory cleanup operation
    /// </summary>
    public class MemoryCleanupResult
    {
        public MemoryPressureMonitor.MemoryPressureLevel PressureLevel { get; set; }
        public MemoryPressureMonitor.CleanupStrategy Strategy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long MemoryReclaimed { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<ResponderCleanupResult> ResponderResults { get; set; }
    }
    
    /// <summary>
    /// Result of a single responder's cleanup operation
    /// </summary>
    public class ResponderCleanupResult
    {
        public string ResponderName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long MemoryReclaimed { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Memory pressure monitoring statistics
    /// </summary>
    public struct MemoryPressureStatistics
    {
        public long MonitoringCycles { get; set; }
        public long CleanupOperations { get; set; }
        public long MemoryReclaimed { get; set; }
        public double AverageMemoryUsage { get; set; }
        public long CurrentMemoryUsage { get; set; }
        public MemoryPressureMonitor.MemoryPressureLevel CurrentPressureLevel { get; set; }
        public int RegisteredResponders { get; set; }
        public long LowThreshold { get; set; }
        public long MediumThreshold { get; set; }
        public long HighThreshold { get; set; }
        public long CriticalThreshold { get; set; }
        
        public override string ToString()
        {
            return $"MemoryMonitor: {CurrentMemoryUsage / 1024 / 1024}MB current, " +
                   $"{CurrentPressureLevel} pressure, {CleanupOperations} cleanups, " +
                   $"{MemoryReclaimed / 1024 / 1024}MB reclaimed";
        }
    }
}
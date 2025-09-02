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
        
        private readonly List<IMemoryPressureResponder> _responders;
        private readonly object _respondersLock = new object();
        private readonly MemoryPressureConfiguration _config;
        private readonly GCOptimizationSettings _gcSettings;
        
        // UniTask monitoring
        private CancellationTokenSource _cancellationTokenSource;
        
        // Circular buffer for cleanup history (more memory efficient)
        private readonly MemoryCleanupResult[] _cleanupHistoryBuffer;
        private int _cleanupHistoryIndex;
        private int _cleanupHistoryCount;
        
        // Object pooling for cleanup results
        private readonly Queue<MemoryCleanupResult> _cleanupResultPool;
        private readonly Queue<ResponderCleanupResult> _responderResultPool;
        private const int MAX_POOL_SIZE = 20;
        
        // Cached strings to reduce allocations
        private static readonly string ErrorMemoryMonitoringLoop = "Memory pressure monitoring loop failed: ";
        private static readonly string ErrorMonitoringCycle = "Error in memory monitoring cycle: ";
        private static readonly string ErrorCleanupOperation = "Error during memory cleanup: ";
        private static readonly string ErrorResponder = "Error in memory pressure responder ";
        private static readonly string ErrorNotifyingResponder = "Error notifying responder ";
        private static readonly string ErrorLightweightMonitoring = "Error in lightweight memory monitoring: ";
        private static readonly string WarningGCThreshold = "GC took ";
        private static readonly string WarningLowMemory = "Unity low memory warning received";
        
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
        
        // Self-monitoring for memory budget
        private long _lastSelfMemoryCheck;
        private long _monitorStartMemory;
        private bool _selfThrottling;
        
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
            _cleanupHistoryBuffer = new MemoryCleanupResult[MAX_CLEANUP_HISTORY];
            _cleanupHistoryIndex = 0;
            _cleanupHistoryCount = 0;
            
            // Initialize object pools
            _cleanupResultPool = new Queue<MemoryCleanupResult>();
            _responderResultPool = new Queue<ResponderCleanupResult>();
            
            // Pre-populate pools
            for (int i = 0; i < MAX_POOL_SIZE; i++)
            {
                _cleanupResultPool.Enqueue(CreateCleanupResult());
                _responderResultPool.Enqueue(CreateResponderResult());
            }
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

            // Record starting memory for self-monitoring
            _monitorStartMemory = GC.GetTotalMemory(false);
            _lastSelfMemoryCheck = _monitorStartMemory;
            _selfThrottling = false;
            
            
            // Start UniTask monitoring loop
            _cancellationTokenSource = new CancellationTokenSource();
            StartMonitoringLoop().Forget();
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
            var result = await PerformCleanupAsync(_currentPressureLevel, strategy, true, _cancellationTokenSource.Token);
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
        /// Start the UniTask-based monitoring loop
        /// </summary>
        private async UniTaskVoid StartMonitoringLoop()
        {
            try
            {
                await MonitoringLoopAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing
            }
            catch (Exception ex)
            {
                if (!_disposed && Application.isPlaying)
                {
                    LogError(ErrorMemoryMonitoringLoop, ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Main monitoring loop using UniTask
        /// </summary>
        private async UniTask MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
                    // Skip monitoring when not playing
                    if (!Application.isPlaying)
                    {
                        await UniTask.Delay(1000, cancellationToken: cancellationToken); // Check every second in edit mode
                        continue;
                    }
                    
                    await PerformMonitoringCycleAsync(cancellationToken);
                    
                    // Self-monitoring: check if monitor is using too much memory
                    await PerformSelfMonitoringAsync();
                    
                    // Adaptive delay based on pressure level and self-throttling
                    var delay = GetAdaptiveMonitoringInterval();
                    if (_config.EnableSelfThrottling && _selfThrottling)
                    {
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Slow down when throttling
                    }
                    await UniTask.Delay(delay, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation
                }
                catch (Exception ex)
                {
                    if (!_disposed && Application.isPlaying)
                    {
                        LogError(ErrorMonitoringCycle, ex.Message);
                    }
                    
                    // Wait before retrying on error
                    await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
                }
            }
        }
        
        /// <summary>
        /// Perform a single monitoring cycle
        /// </summary>
        private async UniTask PerformMonitoringCycleAsync(CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            if (_config.EnableEditorMonitoring)
            {
                // Lightweight monitoring in editor
                PerformLightweightMonitoring();
            }
            await UniTask.Yield();
            return;
#else
            
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
                    await PerformCleanupAsync(newPressureLevel, GetCleanupStrategy(newPressureLevel), false, cancellationToken);
                }
            }
            
            _lastMemoryUsage = currentMemory;
            
            // Adaptive threshold adjustment
            if (_config.UseAdaptiveThresholds)
            {
                AdjustThresholdsAdaptively(currentMemory);
            }
#endif
        }

        /// <summary>
        /// Get adaptive monitoring interval based on current pressure level
        /// </summary>
        private TimeSpan GetAdaptiveMonitoringInterval()
        {
            return _currentPressureLevel switch
            {
                MemoryPressureLevel.None => _config.MonitoringInterval,
                MemoryPressureLevel.Low => TimeSpan.FromMilliseconds(_config.MonitoringInterval.TotalMilliseconds * 0.8),
                MemoryPressureLevel.Medium => TimeSpan.FromMilliseconds(_config.MonitoringInterval.TotalMilliseconds * 0.5),
                MemoryPressureLevel.High => TimeSpan.FromMilliseconds(_config.MonitoringInterval.TotalMilliseconds * 0.3),
                MemoryPressureLevel.Critical => TimeSpan.FromMilliseconds(_config.MonitoringInterval.TotalMilliseconds * 0.1),
                _ => _config.MonitoringInterval
            };
        }
        
        /// <summary>
        /// Monitor the memory monitor itself to prevent excessive memory usage
        /// </summary>
        private async UniTask PerformSelfMonitoringAsync()
        {
            // Skip self-monitoring if disabled
            if (!_config.EnableSelfThrottling)
                return;
                
            // Only check every 10th cycle to reduce overhead
            if (_monitoringCycles % 10 != 0)
                return;
                
            var currentMemory = GC.GetTotalMemory(false);
            var memoryGrowth = currentMemory - _lastSelfMemoryCheck;
            var totalGrowth = currentMemory - _monitorStartMemory;
            
            // If monitor itself is growing too much, enable throttling
            var maxMonitorMemoryBudget = _config.MaxMonitorMemoryBudget;
            var maxGrowthPerCheck = maxMonitorMemoryBudget / 10; // 10% of budget per check
            
            if (totalGrowth > maxMonitorMemoryBudget || memoryGrowth > maxGrowthPerCheck)
            {
                if (!_selfThrottling)
                {
                    _selfThrottling = true;
                    if (Application.isPlaying)
                    {
                        Debug.LogWarning($"MemoryPressureMonitor self-throttling activated. Total growth: {totalGrowth / 1024 / 1024}MB");
                    }
                }
                
                // Perform emergency cleanup of our own allocations
                await PerformSelfCleanupAsync();
            }
            else if (_selfThrottling && totalGrowth < _config.MaxMonitorMemoryBudget / 2)
            {
                // Disable throttling if memory usage is back to normal
                _selfThrottling = false;
            }
            
            _lastSelfMemoryCheck = currentMemory;
        }
        
        /// <summary>
        /// Perform cleanup of monitor's own allocations
        /// </summary>
        private async UniTask PerformSelfCleanupAsync()
        {
            lock (_respondersLock)
            {
                // Clear excess pooled objects
                while (_cleanupResultPool.Count > MAX_POOL_SIZE / 2)
                {
                    _cleanupResultPool.Dequeue();
                }
                
                while (_responderResultPool.Count > MAX_POOL_SIZE / 2)
                {
                    _responderResultPool.Dequeue();
                }
                
                // Clear older cleanup history more aggressively
                var keepCount = MAX_CLEANUP_HISTORY / 2;
                if (_cleanupHistoryCount > keepCount)
                {
                    var itemsToRemove = _cleanupHistoryCount - keepCount;
                    for (int i = 0; i < itemsToRemove; i++)
                    {
                        var oldIndex = (_cleanupHistoryIndex - _cleanupHistoryCount + i + MAX_CLEANUP_HISTORY) % MAX_CLEANUP_HISTORY;
                        if (_cleanupHistoryBuffer[oldIndex] != null)
                        {
                            ReturnCleanupResultToPool(_cleanupHistoryBuffer[oldIndex]);
                            _cleanupHistoryBuffer[oldIndex] = null;
                        }
                    }
                    _cleanupHistoryCount = keepCount;
                }
            }
            
            // Force a small GC to clean up our released objects
            await UniTask.Yield();
            GC.Collect(0, GCCollectionMode.Optimized);
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
        private async UniTask<MemoryCleanupResult> PerformCleanupAsync(MemoryPressureLevel pressureLevel, CleanupStrategy strategy, bool force = false, CancellationToken cancellationToken = default)
        {
            try
            {
                Interlocked.Increment(ref _cleanupOperations);
                _lastCleanup = DateTime.UtcNow;
                
                var result = GetPooledCleanupResult();
                result.PressureLevel = pressureLevel;
                result.Strategy = strategy;
                result.StartTime = DateTime.UtcNow;
                result.ResponderResults.Clear();
                
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
                    var responderResults = await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);
                    result.ResponderResults.AddRange(responderResults);
                }
                
                // Perform garbage collection based on strategy
                if (_gcSettings.DisableAggressiveGC)
                {
                    // Use frame-aware incremental collection
                    await PerformFrameAwareGCAsync(strategy, pressureLevel, cancellationToken);
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
                    LogError(ErrorCleanupOperation, ex.Message);
                }
                var errorResult = GetPooledCleanupResult();
                errorResult.PressureLevel = pressureLevel;
                errorResult.Strategy = strategy;
                errorResult.Success = false;
                errorResult.ErrorMessage = ex.Message;
                errorResult.ResponderResults.Clear();
                return errorResult;
            }
        }
        
        /// <summary>
        /// Execute a single responder asynchronously
        /// </summary>
        private async UniTask<ResponderCleanupResult> ExecuteResponderAsync(IMemoryPressureResponder responder, MemoryPressureLevel pressureLevel, CleanupStrategy strategy)
        {
            var result = GetPooledResponderResult();
            result.ResponderName = responder.GetType().Name;
            result.StartTime = DateTime.UtcNow;
            result.MemoryReclaimed = 0;
            result.Success = false;
            result.ErrorMessage = null;
            
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
                    LogError(ErrorResponder, responder.GetType().Name, ": ", ex.Message);
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
                        LogError(ErrorNotifyingResponder, responder.GetType().Name, ": ", ex.Message);
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
        /// Add cleanup result to circular buffer history
        /// </summary>
        private void AddToCleanupHistory(MemoryCleanupResult result)
        {
            lock (_respondersLock)
            {
                // Return the old result to pool before overwriting
                var oldResult = _cleanupHistoryBuffer[_cleanupHistoryIndex];
                if (oldResult != null)
                {
                    ReturnCleanupResultToPool(oldResult);
                }
                
                _cleanupHistoryBuffer[_cleanupHistoryIndex] = result;
                _cleanupHistoryIndex = (_cleanupHistoryIndex + 1) % MAX_CLEANUP_HISTORY;
                _cleanupHistoryCount = Math.Min(_cleanupHistoryCount + 1, MAX_CLEANUP_HISTORY);
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
                    LogError(ErrorLightweightMonitoring, ex.Message);
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
        
        #region Object Pooling
        
        /// <summary>
        /// Get a pooled cleanup result or create new if pool is empty
        /// </summary>
        private MemoryCleanupResult GetPooledCleanupResult()
        {
            lock (_respondersLock)
            {
                if (_cleanupResultPool.Count > 0)
                {
                    var pooled = _cleanupResultPool.Dequeue();
                    // Reset properties to default values
                    pooled.PressureLevel = MemoryPressureLevel.None;
                    pooled.Strategy = CleanupStrategy.Conservative;
                    pooled.StartTime = default;
                    pooled.EndTime = default;
                    pooled.Duration = default;
                    pooled.MemoryReclaimed = 0;
                    pooled.Success = false;
                    pooled.ErrorMessage = null;
                    return pooled;
                }
                return CreateCleanupResult();
            }
        }
        
        /// <summary>
        /// Get a pooled responder result or create new if pool is empty
        /// </summary>
        private ResponderCleanupResult GetPooledResponderResult()
        {
            lock (_respondersLock)
            {
                if (_responderResultPool.Count > 0)
                {
                    var pooled = _responderResultPool.Dequeue();
                    // Reset properties to default values
                    pooled.ResponderName = null;
                    pooled.StartTime = default;
                    pooled.EndTime = default;
                    pooled.Duration = default;
                    pooled.MemoryReclaimed = 0;
                    pooled.Success = false;
                    pooled.ErrorMessage = null;
                    return pooled;
                }
                return CreateResponderResult();
            }
        }
        
        /// <summary>
        /// Return cleanup result to pool for reuse
        /// </summary>
        private void ReturnCleanupResultToPool(MemoryCleanupResult result)
        {
            if (result == null) return;
            
            lock (_respondersLock)
            {
                // Return responder results to their pool first
                if (result.ResponderResults != null)
                {
                    foreach (var responderResult in result.ResponderResults)
                    {
                        ReturnResponderResultToPool(responderResult);
                    }
                }
                
                // Return to pool if not at capacity
                if (_cleanupResultPool.Count < MAX_POOL_SIZE)
                {
                    _cleanupResultPool.Enqueue(result);
                }
            }
        }
        
        /// <summary>
        /// Return responder result to pool for reuse
        /// </summary>
        private void ReturnResponderResultToPool(ResponderCleanupResult result)
        {
            if (result == null) return;
            
            lock (_respondersLock)
            {
                if (_responderResultPool.Count < MAX_POOL_SIZE)
                {
                    _responderResultPool.Enqueue(result);
                }
            }
        }
        
        /// <summary>
        /// Create a new cleanup result with initialized collections
        /// </summary>
        private static MemoryCleanupResult CreateCleanupResult()
        {
            return new MemoryCleanupResult
            {
                ResponderResults = new List<ResponderCleanupResult>()
            };
        }
        
        /// <summary>
        /// Create a new responder result
        /// </summary>
        private static ResponderCleanupResult CreateResponderResult()
        {
            return new ResponderCleanupResult();
        }
        
        #endregion
        
        #region Optimized Logging
        
        /// <summary>
        /// Optimized error logging to reduce string allocations
        /// </summary>
        private static void LogError(string prefix, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(prefix + message);
#endif
        }
        
        /// <summary>
        /// Optimized error logging with multiple parts
        /// </summary>
        private static void LogError(string part1, string part2, string part3, string part4)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(part1 + part2 + part3 + part4);
#endif
        }
        
        /// <summary>
        /// Optimized warning logging with multiple parts
        /// </summary>
        private static void LogWarning(string part1, string part2, string part3, string part4, string part5)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(part1 + part2 + part3 + part4 + part5);
#endif
        }
        
        #endregion
        
        #region Statistics and Reporting
        
        /// <summary>
        /// Get memory pressure statistics
        /// </summary>
        public MemoryPressureStatistics GetStatistics()
        {
            var currentMemory = CurrentMemoryUsage;
            return new MemoryPressureStatistics
            {
                MonitoringCycles = _monitoringCycles,
                CleanupOperations = _cleanupOperations,
                MemoryReclaimed = _memoryReclaimed,
                AverageMemoryUsage = _averageMemoryUsage,
                CurrentMemoryUsage = currentMemory,
                CurrentPressureLevel = _currentPressureLevel,
                RegisteredResponders = _responders.Count,
                LowThreshold = _lowPressureThreshold,
                MediumThreshold = _mediumPressureThreshold,
                HighThreshold = _highPressureThreshold,
                CriticalThreshold = _criticalPressureThreshold,
                MonitorMemoryUsage = currentMemory - _monitorStartMemory,
                SelfThrottling = _selfThrottling
            };
        }

        #endregion

        /// <summary>
        /// Perform frame-aware incremental garbage collection
        /// </summary>
        private async UniTask PerformFrameAwareGCAsync(CleanupStrategy strategy, MemoryPressureLevel pressureLevel, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            
            // Skip frame-aware timing when not on main thread - just proceed with GC
            if (_gcSettings.EnableAsyncCollection)
            {
                // Simple delay to avoid blocking - frame timing requires main thread
                await UniTask.Delay(16, cancellationToken: cancellationToken); // ~60fps frame delay
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
                        await UniTask.Yield(cancellationToken);
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
                            await UniTask.Yield(cancellationToken);
                            
                            // Stop if frame time is too high or cancelled
                            if (Time.deltaTime * 1000f > _gcSettings.FrameTimeBudgetMs * 1.5f || cancellationToken.IsCancellationRequested)
                                break;
                        }
                    }
                    break;
            }
            
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            if (_gcSettings.LogGCWarnings && duration > _gcSettings.GCWarningThresholdMs && Application.isPlaying && !_disposed)
            {
                LogWarning(WarningGCThreshold, duration.ToString("F1"), "ms (threshold: ", _gcSettings.GCWarningThresholdMs.ToString(), "ms)");
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
            if (Application.isPlaying)
            {
                Debug.LogWarning(WarningLowMemory);
            }
            
            // Force a moderate cleanup
            _ = PerformCleanupAsync(MemoryPressureLevel.High, CleanupStrategy.Moderate, true, _cancellationTokenSource?.Token ?? default);
        }
        
        /// <summary>
        /// Dispose resources and stop monitoring
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            // Cancel monitoring loop
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            // Unregister Unity callbacks
            if (_gcSettings?.UseUnityLowMemoryCallback == true)
            {
                Application.lowMemory -= OnUnityLowMemory;
            }
            
            lock (_respondersLock)
            {
                _responders.Clear();
                
                // Return all cleanup results to pool and clear circular buffer
                for (int i = 0; i < _cleanupHistoryBuffer.Length; i++)
                {
                    if (_cleanupHistoryBuffer[i] != null)
                    {
                        ReturnCleanupResultToPool(_cleanupHistoryBuffer[i]);
                        _cleanupHistoryBuffer[i] = null;
                    }
                }
                _cleanupHistoryCount = 0;
                _cleanupHistoryIndex = 0;
                
                // Clear pools
                _cleanupResultPool.Clear();
                _responderResultPool.Clear();
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
        
        // Self-monitoring configuration
        public long MaxMonitorMemoryBudget { get; set; } = 10 * 1024 * 1024; // 10MB
        public bool EnableSelfThrottling { get; set; } = true;
        public bool EnableEditorMonitoring { get; set; } = false; // Disabled by default
        
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
        public long MonitorMemoryUsage { get; set; } // Memory used by the monitor itself
        public bool SelfThrottling { get; set; } // Whether monitor is in self-throttling mode
        
        public override string ToString()
        {
            var throttleStatus = SelfThrottling ? " (throttled)" : "";
            return $"MemoryMonitor: {CurrentMemoryUsage / 1024 / 1024}MB current, " +
                   $"{CurrentPressureLevel} pressure, {CleanupOperations} cleanups, " +
                   $"{MemoryReclaimed / 1024 / 1024}MB reclaimed, " +
                   $"monitor: {MonitorMemoryUsage / 1024 / 1024}MB{throttleStatus}";
        }
    }
}
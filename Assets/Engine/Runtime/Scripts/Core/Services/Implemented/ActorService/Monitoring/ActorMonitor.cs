using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Actor monitor implementation providing statistics, validation, and global animation control
    /// Tracks performance metrics, validates actor health, and manages system-wide animations
    /// </summary>
    public class ActorMonitor : IActorMonitor
    {
        #region Private Fields
        
        private readonly IActorRegistry _registry;
        private readonly IActorFactory _factory;
        private readonly ISceneManager _sceneManager;
        private readonly ActorServiceConfiguration _config;
        
        // Configuration
        private ActorMonitorConfiguration _monitorConfig;
        
        // Statistics
        private ActorServiceStatistics _statistics = new();
        private readonly object _statsLock = new();
        
        // Performance tracking
        private readonly ConcurrentDictionary<string, List<TimeSpan>> _operationTimes = new();
        private readonly ConcurrentDictionary<string, int> _operationCounts = new();
        private bool _performanceTrackingEnabled = true;
        
        // Animation control
        private float _globalAnimationSpeed = 1.0f;
        private bool _animationsPaused = false;
        
        // Health monitoring
        private ActorSystemHealthStatus _lastHealthStatus = ActorSystemHealthStatus.Healthy;
        private readonly Stopwatch _healthCheckStopwatch = new();
        
        // Timers for automatic operations
        private readonly System.Threading.Timer _statisticsTimer;
        private readonly System.Threading.Timer _healthCheckTimer;
        
        #endregion
        
        #region Events
        
        public event Action<string, string[]> OnValidationErrorDetected;
        public event Action<ActorSystemHealthStatus> OnHealthStatusChanged;
        public event Action<string, TimeSpan> OnPerformanceThresholdExceeded;
        
        #endregion
        
        #region Constructor
        
        public ActorMonitor(IActorRegistry registry, IActorFactory factory, ISceneManager sceneManager, ActorServiceConfiguration config)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _monitorConfig = new ActorMonitorConfiguration();
            
            // Initialize timers for automatic monitoring
            _statisticsTimer = new System.Threading.Timer(
                _ => UpdateStatistics(), 
                null, 
                TimeSpan.FromSeconds(_monitorConfig.StatisticsUpdateInterval),
                TimeSpan.FromSeconds(_monitorConfig.StatisticsUpdateInterval));
                
            // Disable automatic health checks to prevent threading issues
            // Health checks should be called manually from main thread
            // _healthCheckTimer = new System.Threading.Timer(
            //     _ => PerformAutomaticHealthCheck(), 
            //     null, 
            //     TimeSpan.FromSeconds(_monitorConfig.HealthCheckInterval),
            //     TimeSpan.FromSeconds(_monitorConfig.HealthCheckInterval));
            
            UnityEngine.Debug.Log("[ActorMonitor] Initialized with automatic monitoring");
        }
        
        #endregion
        
        #region Statistics
        
        public ActorServiceStatistics GetStatistics()
        {
            // Update statistics from current registry state
            UpdateStatistics();
            
            lock (_statsLock)
            {
                return new ActorServiceStatistics
                {
                    TotalActors = _registry.ActorCount,
                    CharacterActors = _registry.CharacterActors.Count,
                    BackgroundActors = _registry.BackgroundActors.Count,
                    LoadingActors = 0, // Will be set by factory
                    ErrorActors = _registry.AllActors.Count(a => a.HasError),
                    LoadedActors = _registry.AllActors.Count(a => a.IsLoaded),
                    CustomActors = _registry.ActorCount - _registry.CharacterActors.Count - _registry.BackgroundActors.Count,
                    AverageLoadTime = _statistics.AverageLoadTime,
                    MemoryUsageBytes = CalculateApproximateMemoryUsage(),
                    ActiveAnimations = GetActiveAnimationCount(),
                    GlobalAnimationSpeed = _globalAnimationSpeed,
                    LastUpdateTime = DateTime.UtcNow,
                    PerformanceMonitoringEnabled = _performanceTrackingEnabled
                };
            }
        }
        
        public void UpdateStatistics()
        {
            lock (_statsLock)
            {
                _statistics.TotalActors = _registry.ActorCount;
                _statistics.CharacterActors = _registry.CharacterActors.Count;
                _statistics.BackgroundActors = _registry.BackgroundActors.Count;
                
                // Update memory usage (simplified calculation)
                _statistics.MemoryUsageBytes = CalculateApproximateMemoryUsage();
                
                // Update operation counts
                foreach (var kvp in _operationCounts)
                {
                    // Store operation counts in statistics
                }
            }
        }
        
        public void ResetStatistics()
        {
            lock (_statsLock)
            {
                _statistics = new ActorServiceStatistics
                {
                    LastUpdateTime = DateTime.UtcNow
                };
            }
            
            _operationTimes.Clear();
            _operationCounts.Clear();
            
            UnityEngine.Debug.Log("[ActorMonitor] Statistics reset");
        }
        
        public ActorFactoryStatistics GetFactoryStatistics()
        {
            return _factory?.GetStatistics() ?? new ActorFactoryStatistics();
        }
        
        public SceneManagerStatistics GetSceneStatistics()
        {
            return _sceneManager?.GetStatistics() ?? new SceneManagerStatistics();
        }
        
        #endregion
        
        #region Validation
        
        public Dictionary<string, string[]> ValidateAllActors()
        {
            var validationResults = new Dictionary<string, string[]>();
            
            foreach (var actor in _registry.AllActors)
            {
                // Skip destroyed actors to prevent Unity object access errors
                if (actor == null || (actor is UnityEngine.Object unityObj && unityObj == null))
                {
                    continue;
                }
                
                if (ValidateActor(actor.Id, out var errors))
                {
                    validationResults[actor.Id] = Array.Empty<string>();
                }
                else
                {
                    validationResults[actor.Id] = errors;
                    OnValidationErrorDetected?.Invoke(actor.Id, errors);
                }
            }
            
            return validationResults;
        }
        
        public bool ValidateActor(string actorId, out string[] errors)
        {
            var errorList = new List<string>();
            
            var actor = _registry.GetActor(actorId);
            if (actor == null)
            {
                errors = new[] { "Actor not found in registry" };
                return false;
            }
            
            // Validate actor configuration
            if (!actor.ValidateConfiguration(out var configErrors))
            {
                errorList.AddRange(configErrors);
            }
            
            // Validate GameObject state
            if (actor.GameObject == null)
            {
                errorList.Add("Actor GameObject is null");
            }
            else if (!actor.GameObject.activeInHierarchy && actor.Visible)
            {
                errorList.Add("Actor marked as visible but GameObject is inactive");
            }
            
            // Validate load state consistency
            if (actor.LoadState == ActorLoadState.Error && string.IsNullOrEmpty(actor.LastError))
            {
                errorList.Add("Actor in error state but no error message provided");
            }
            
            errors = errorList.ToArray();
            return errorList.Count == 0;
        }
        
        public bool ValidateSystemHealth(out string[] errors)
        {
            var errorList = new List<string>();
            
            // Check registry consistency
            if (_registry is ActorRegistry registry && !registry.ValidateConsistency(out var registryErrors))
            {
                errorList.AddRange(registryErrors.Select(e => $"Registry: {e}"));
            }
            
            // Check memory usage
            var memoryReport = GetMemoryReport();
            if (memoryReport.TotalMemoryUsage > _config.MemoryPressureThreshold * 1024 * 1024 * 100) // Convert to bytes
            {
                errorList.Add("Memory usage exceeds threshold");
            }
            
            // Check actor count limits
            if (_registry.ActorCount >= _config.MaxActors * 0.9f) // 90% of limit
            {
                errorList.Add("Actor count approaching maximum limit");
            }
            
            // Check for actors in error state
            var actorsInError = _registry.AllActors.Count(a => a.HasError);
            if (actorsInError > 0)
            {
                errorList.Add($"{actorsInError} actors are in error state");
            }
            
            errors = errorList.ToArray();
            return errorList.Count == 0;
        }
        
        public ActorSystemHealthReport PerformHealthCheck()
        {
            _healthCheckStopwatch.Restart();
            
            var report = new ActorSystemHealthReport
            {
                GeneratedAt = DateTime.UtcNow,
                ValidationErrors = ValidateAllActors(),
                MemoryReport = GetMemoryReport(),
                PerformanceReport = GetPerformanceReport()
            };
            
            // Determine overall health
            if (ValidateSystemHealth(out var systemErrors))
            {
                if (report.ValidationErrors.Values.Any(errors => errors.Length > 0))
                {
                    report.OverallHealth = ActorSystemHealthStatus.Warning;
                }
                else
                {
                    report.OverallHealth = ActorSystemHealthStatus.Healthy;
                }
            }
            else
            {
                report.SystemWarnings = systemErrors;
                report.OverallHealth = systemErrors.Any(e => e.Contains("error")) 
                    ? ActorSystemHealthStatus.Critical 
                    : ActorSystemHealthStatus.Warning;
            }
            
            _healthCheckStopwatch.Stop();
            report.HealthCheckDuration = _healthCheckStopwatch.Elapsed;
            
            // Fire event if health status changed
            if (report.OverallHealth != _lastHealthStatus)
            {
                _lastHealthStatus = report.OverallHealth;
                OnHealthStatusChanged?.Invoke(report.OverallHealth);
            }
            
            return report;
        }
        
        #endregion
        
        #region Debug Information
        
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Actor System Debug Info ===");
            
            var stats = GetStatistics();
            info.AppendLine($"Total Actors: {stats.TotalActors}");
            info.AppendLine($"Characters: {stats.CharacterActors}");
            info.AppendLine($"Backgrounds: {stats.BackgroundActors}");
            info.AppendLine($"Loading: {stats.LoadingActors}");
            info.AppendLine($"Errors: {stats.ErrorActors}");
            info.AppendLine($"Memory Usage: {stats.MemoryUsageBytes / (1024 * 1024):F1} MB");
            info.AppendLine($"Uptime: {stats.LastUpdateTime:hh\\:mm\\:ss}");
            info.AppendLine();
            
            // Animation info
            info.AppendLine($"Global Animation Speed: {_globalAnimationSpeed:F2}x");
            info.AppendLine($"Animations Paused: {_animationsPaused}");
            info.AppendLine($"Active Animations: {GetActiveAnimationCount()}");
            info.AppendLine();
            
            // Performance info
            if (_operationTimes.Count > 0)
            {
                info.AppendLine("=== Performance Metrics ===");
                foreach (var kvp in _operationTimes)
                {
                    var avgTime = kvp.Value.Count > 0 ? kvp.Value.Average(t => t.TotalMilliseconds) : 0;
                    info.AppendLine($"{kvp.Key}: {avgTime:F2}ms avg ({kvp.Value.Count} samples)");
                }
            }
            
            return info.ToString();
        }
        
        public string GetActorDebugInfo(string actorId)
        {
            var actor = _registry.GetActor(actorId);
            if (actor == null)
                return $"Actor '{actorId}' not found";
                
            return actor.GetDebugInfo();
        }
        
        public ActorMemoryReport GetMemoryReport()
        {
            var report = new ActorMemoryReport
            {
                TotalMemoryUsage = CalculateApproximateMemoryUsage(),
                TotalGameObjects = _registry.AllActors.Count(a => a.GameObject != null),
                MemoryByActorType = new Dictionary<string, long>()
            };
            
            // Calculate memory by actor type (simplified)
            foreach (var actorType in ActorType.GetAllTypes())
            {
                var actorsOfType = _registry.AllActors.AsEnumerable().Count(a => a.ActorType == actorType);
                report.MemoryByActorType[actorType.ToString()] = actorsOfType * 1024; // Simplified estimate
            }
            
            if (report.TotalGameObjects > 0)
            {
                report.AverageActorMemoryUsage = report.TotalMemoryUsage / report.TotalGameObjects;
            }
            
            // Generate warnings
            var warnings = new List<string>();
            if (report.TotalMemoryUsage > 50 * 1024 * 1024) // 50MB
            {
                warnings.Add("High memory usage detected");
            }
            report.MemoryWarnings = warnings.ToArray();
            
            return report;
        }
        
        public ActorPerformanceReport GetPerformanceReport()
        {
            var report = new ActorPerformanceReport
            {
                ActiveAnimationCount = GetActiveAnimationCount(),
                AverageOperationTimes = new Dictionary<string, TimeSpan>(),
                OperationCounts = new Dictionary<string, int>(_operationCounts),
                ReportGeneratedAt = DateTime.UtcNow
            };
            
            // Calculate average operation times
            foreach (var kvp in _operationTimes)
            {
                if (kvp.Value.Count > 0)
                {
                    var avgTicks = (long)kvp.Value.Average(t => t.Ticks);
                    report.AverageOperationTimes[kvp.Key] = new TimeSpan(avgTicks);
                }
            }
            
            // Generate performance warnings
            var warnings = new List<string>();
            foreach (var kvp in report.AverageOperationTimes)
            {
                if (kvp.Value.TotalMilliseconds > 100) // Operations taking > 100ms
                {
                    warnings.Add($"Slow operation detected: {kvp.Key} ({kvp.Value.TotalMilliseconds:F1}ms)");
                }
            }
            report.PerformanceWarnings = warnings.ToArray();
            
            return report;
        }
        
        #endregion
        
        #region Global Animation Control
        
        public void StopAllAnimations()
        {
            foreach (var actor in _registry.AllActors)
            {
                actor.StopAllAnimations();
            }
            
            _animationsPaused = false;
            UnityEngine.Debug.Log("[ActorMonitor] Stopped all animations");
        }
        
        public void PauseAllAnimations()
        {
            DOTween.PauseAll();
            _animationsPaused = true;
            UnityEngine.Debug.Log("[ActorMonitor] Paused all animations");
        }
        
        public void ResumeAllAnimations()
        {
            DOTween.PlayAll();
            _animationsPaused = false;
            UnityEngine.Debug.Log("[ActorMonitor] Resumed all animations");
        }
        
        public void SetGlobalAnimationSpeed(float speedMultiplier)
        {
            _globalAnimationSpeed = Mathf.Max(0.1f, speedMultiplier);
            DOTween.timeScale = _globalAnimationSpeed;
            UnityEngine.Debug.Log($"[ActorMonitor] Set global animation speed to {_globalAnimationSpeed:F2}x");
        }
        
        public float GetGlobalAnimationSpeed()
        {
            return _globalAnimationSpeed;
        }
        
        public int GetActiveAnimationCount()
        {
            return DOTween.TotalActiveTweens();
        }
        
        #endregion
        
        #region Performance Monitoring
        
        public void StartPerformanceMonitoring()
        {
            _performanceTrackingEnabled = true;
            UnityEngine.Debug.Log("[ActorMonitor] Performance monitoring started");
        }
        
        public void StopPerformanceMonitoring()
        {
            _performanceTrackingEnabled = false;
            UnityEngine.Debug.Log("[ActorMonitor] Performance monitoring stopped");
        }
        
        public TimeSpan GetAverageOperationTime(string operationType)
        {
            if (!_operationTimes.TryGetValue(operationType, out var times) || times.Count == 0)
                return TimeSpan.Zero;
                
            var avgTicks = (long)times.Average(t => t.Ticks);
            return new TimeSpan(avgTicks);
        }
        
        public void RecordOperationTime(string operationType, TimeSpan executionTime)
        {
            if (!_performanceTrackingEnabled)
                return;
                
            _operationTimes.AddOrUpdate(operationType, 
                new List<TimeSpan> { executionTime },
                (key, existing) =>
                {
                    existing.Add(executionTime);
                    // Keep only recent samples to prevent memory growth
                    if (existing.Count > _monitorConfig.MaxPerformanceSamples)
                    {
                        existing.RemoveRange(0, existing.Count - _monitorConfig.MaxPerformanceSamples);
                    }
                    return existing;
                });
                
            _operationCounts.AddOrUpdate(operationType, 1, (key, count) => count + 1);
            
            // Check for performance threshold violations
            if (executionTime.TotalMilliseconds > 500) // 500ms threshold
            {
                OnPerformanceThresholdExceeded?.Invoke(operationType, executionTime);
            }
        }
        
        public OperationPerformanceMetrics GetRecentPerformanceMetrics(string operationType, int sampleSize = 100)
        {
            if (!_operationTimes.TryGetValue(operationType, out var times) || times.Count == 0)
            {
                return new OperationPerformanceMetrics
                {
                    OperationType = operationType,
                    SampleCount = 0
                };
            }
            
            var recentTimes = times.Skip(Math.Max(0, times.Count - sampleSize)).ToList();
            var avgTicks = (long)recentTimes.Average(t => t.Ticks);
            
            return new OperationPerformanceMetrics
            {
                OperationType = operationType,
                AverageTime = new TimeSpan(avgTicks),
                MinTime = new TimeSpan(recentTimes.Min(t => t.Ticks)),
                MaxTime = new TimeSpan(recentTimes.Max(t => t.Ticks)),
                SampleCount = recentTimes.Count,
                SuccessRate = 1.0f // Simplified - would need failure tracking
            };
        }
        
        #endregion
        
        #region Configuration and Runtime Control
        
        public void Configure(bool enableValidation, bool enablePerformanceTracking, float statisticsUpdateInterval)
        {
            _monitorConfig.ValidationEnabled = enableValidation;
            _monitorConfig.PerformanceTrackingEnabled = enablePerformanceTracking;
            _monitorConfig.StatisticsUpdateInterval = statisticsUpdateInterval;
            
            _performanceTrackingEnabled = enablePerformanceTracking;
            
            // Update timer intervals
            _statisticsTimer?.Change(
                TimeSpan.FromSeconds(statisticsUpdateInterval),
                TimeSpan.FromSeconds(statisticsUpdateInterval));
            
            UnityEngine.Debug.Log($"[ActorMonitor] Configuration updated - Validation: {enableValidation}, Performance: {enablePerformanceTracking}");
        }
        
        public ActorMonitorConfiguration GetConfiguration()
        {
            return new ActorMonitorConfiguration
            {
                ValidationEnabled = _monitorConfig.ValidationEnabled,
                PerformanceTrackingEnabled = _monitorConfig.PerformanceTrackingEnabled,
                StatisticsUpdateInterval = _monitorConfig.StatisticsUpdateInterval,
                MaxPerformanceSamples = _monitorConfig.MaxPerformanceSamples,
                AutoHealthChecks = _monitorConfig.AutoHealthChecks,
                HealthCheckInterval = _monitorConfig.HealthCheckInterval
            };
        }
        
        public void SetValidationEnabled(bool enabled)
        {
            _monitorConfig.ValidationEnabled = enabled;
        }
        
        public void SetPerformanceTrackingEnabled(bool enabled)
        {
            _performanceTrackingEnabled = enabled;
            _monitorConfig.PerformanceTrackingEnabled = enabled;
        }
        
        #endregion
        
        #region Private Methods
        
        private long CalculateApproximateMemoryUsage()
        {
            // Simplified memory calculation - cached for performance
            long totalMemory = 0;
            int actorCount = _registry.ActorCount;
            
            // Use estimated memory per actor instead of expensive component enumeration
            // Average actor: ~2KB (GameObject + SpriteRenderer + Animator + CharacterActor)
            totalMemory = actorCount * 2048;
            
            return totalMemory;
        }
        
        private void PerformAutomaticHealthCheck()
        {
            //if (_monitorConfig.AutoHealthChecks)
            //{
            //    // Marshal to main thread using Unity's SynchronizationContext
            //    var context = UnityEngine.UnitySynchronizationContext.Current;
            //    if (context != null)
            //    {
            //        context.Post(_ =>
            //        {
            //            try
            //            {
            //                PerformHealthCheck();
            //            }
            //            catch (Exception ex)
            //            {
            //                UnityEngine.Debug.LogError($"[ActorMonitor] Error during automatic health check: {ex.Message}");
            //            }
            //        }, null);
            //    }
            //    else
            //    {
            //        // Fallback: skip health check if not on main thread and no context
            //        UnityEngine.Debug.LogWarning("[ActorMonitor] Skipping health check - no Unity synchronization context available");
            //    }
            //}
        }
        
        #endregion
        
        #region Disposal
        
        public void Dispose()
        {
            _statisticsTimer?.Dispose();
            _healthCheckTimer?.Dispose();
            
            _operationTimes.Clear();
            _operationCounts.Clear();
            
            UnityEngine.Debug.Log("[ActorMonitor] Disposed");
        }
        
        #endregion
    }
}
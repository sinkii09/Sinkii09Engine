using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services.Performance;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Advanced type-based timeout management system with adaptive timeout strategies.
    /// Uses command types instead of strings for better performance and type safety.
    /// </summary>
    public class TimeoutManager : IDisposable
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly ConcurrentDictionary<Type, CommandPerformanceTracker> _performanceTrackers;
        private readonly ConcurrentDictionary<string, ActiveTimeout> _activeTimeouts;
        private readonly ConcurrentDictionary<string, CancellationTokenRegistration> _tokenRegistrations;
        private readonly object _cleanupLock = new object();
        private DateTime _lastCleanup = DateTime.Now;
        private const int CleanupIntervalMinutes = 5;
        private bool _disposed;
        #endregion

        #region Constructor
        public TimeoutManager(ScriptPlayerConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _performanceTrackers = new ConcurrentDictionary<Type, CommandPerformanceTracker>();
            _activeTimeouts = new ConcurrentDictionary<string, ActiveTimeout>();
            _tokenRegistrations = new ConcurrentDictionary<string, CancellationTokenRegistration>();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Reference to performance monitor for adaptive timeout calculations
        /// </summary>
        public ServicePerformanceMonitor PerformanceMonitor { get; set; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Gets appropriate timeout for a command type based on metadata and performance history
        /// </summary>
        public float GetTimeoutForCommand<T>() where T : ICommand
        {
            return GetTimeoutForCommand(typeof(T));
        }

        /// <summary>
        /// Gets appropriate timeout for a command type based on metadata and performance history
        /// </summary>
        public float GetTimeoutForCommand(Type commandType)
        {
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            var baseTimeout = metadata.Timeout;

            // Check for configuration overrides first
            if (_config.CustomCommandTimeouts?.ContainsKey(commandType) == true)
            {
                baseTimeout = _config.CustomCommandTimeouts[commandType];
            }

            // Apply adaptive timeout if enabled and we have performance data
            if (metadata.AdaptiveTimeout && _config.EnableAdaptiveTimeouts)
            {
                // Use ServicePerformanceMonitor for more accurate adaptive timeouts
                if (PerformanceMonitor != null)
                {
                    var suggestedTimeout = PerformanceMonitor.GetSuggestedTimeout(commandType, baseTimeout);
                    if (Math.Abs(suggestedTimeout - baseTimeout) > 0.1f) // Only use if significantly different
                    {
                        return suggestedTimeout;
                    }
                }
                
                // Fallback to local performance tracking
                if (_performanceTrackers.TryGetValue(commandType, out var tracker))
                {
                    return CalculateAdaptiveTimeout(baseTimeout, tracker);
                }
            }

            return baseTimeout;
        }

        /// <summary>
        /// Creates a timeout token source for command execution with performance tracking
        /// </summary>
        public CancellationTokenSource CreateTimeoutTokenSource<T>(CancellationToken parentToken) where T : ICommand
        {
            return CreateTimeoutTokenSource(typeof(T), parentToken);
        }

        /// <summary>
        /// Creates a timeout token source for command execution with performance tracking
        /// </summary>
        public CancellationTokenSource CreateTimeoutTokenSource(Type commandType, CancellationToken parentToken)
        {
            var timeoutSeconds = GetTimeoutForCommand(commandType);
            var timeoutMs = (int)(timeoutSeconds * 1000);
            
            var timeoutCts = new CancellationTokenSource(timeoutMs);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken, timeoutCts.Token);

            // Track active timeout for monitoring and cleanup
            var timeoutInfo = new ActiveTimeout
            {
                CommandType = commandType,
                TimeoutSeconds = timeoutSeconds,
                StartTime = DateTime.Now,
                TokenSource = linkedCts
            };

            var timeoutId = Guid.NewGuid().ToString();
            _activeTimeouts.TryAdd(timeoutId, timeoutInfo);

            // Clean up when token is disposed - store registration for proper cleanup
            var registration = linkedCts.Token.Register(() => 
            {
                _activeTimeouts.TryRemove(timeoutId, out _);
                _tokenRegistrations.TryRemove(timeoutId, out _);
            });
            _tokenRegistrations.TryAdd(timeoutId, registration);

            return linkedCts;
        }

        /// <summary>
        /// Records command execution completion for adaptive timeout calculation
        /// </summary>
        public void RecordCommandCompletion<T>(TimeSpan executionTime, bool succeeded, Exception error = null) where T : ICommand
        {
            RecordCommandCompletion(typeof(T), executionTime, succeeded, error);
        }

        /// <summary>
        /// Records command execution completion for adaptive timeout calculation
        /// </summary>
        public void RecordCommandCompletion(Type commandType, TimeSpan executionTime, bool succeeded, Exception error = null)
        {
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            
            if (!metadata.TrackPerformance && !_config.EnablePerformanceMonitoring) 
                return;

            var tracker = _performanceTrackers.GetOrAdd(commandType, _ => new CommandPerformanceTracker(commandType));
            tracker.RecordExecution(executionTime, succeeded, error);

            // Periodic cleanup
            PerformPeriodicCleanup();
        }

        /// <summary>
        /// Gets performance statistics for a command type
        /// </summary>
        public CommandPerformanceTracker GetPerformanceTracker<T>() where T : ICommand
        {
            return GetPerformanceTracker(typeof(T));
        }

        /// <summary>
        /// Gets performance statistics for a command type
        /// </summary>
        public CommandPerformanceTracker GetPerformanceTracker(Type commandType)
        {
            return _performanceTrackers.TryGetValue(commandType, out var tracker) ? tracker : null;
        }

        /// <summary>
        /// Executes a command with timeout and performance tracking
        /// </summary>
        public async UniTask<T> ExecuteWithTimeoutAsync<T>(
            Type commandType,
            Func<CancellationToken, UniTask<T>> taskFunc,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CreateTimeoutTokenSource(commandType, cancellationToken);
            var startTime = DateTime.Now;
            
            try
            {
                var result = await taskFunc(timeoutCts.Token);
                var executionTime = DateTime.Now - startTime;
                RecordCommandCompletion(commandType, executionTime, true);
                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                var executionTime = DateTime.Now - startTime;
                RecordCommandCompletion(commandType, executionTime, false);
                
                throw new ScriptExecutionError(
                    $"Command timed out after {executionTime.TotalSeconds:F2}s",
                    ErrorSeverity.Recoverable,
                    ErrorCategory.Timeout,
                    commandType,
                    isRetryable: true)
                    .WithContext("ExecutionTime", executionTime.TotalSeconds)
                    .WithContext("TimeoutLimit", GetTimeoutForCommand(commandType));
            }
            catch (Exception ex)
            {
                var executionTime = DateTime.Now - startTime;
                RecordCommandCompletion(commandType, executionTime, false, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets current timeout manager statistics
        /// </summary>
        public TimeoutManagerStats GetStats()
        {
            return new TimeoutManagerStats
            {
                ActiveTimeouts = _activeTimeouts.Count,
                TrackedCommandTypes = _performanceTrackers.Count,
                TotalExecutions = _performanceTrackers.Values.Sum(t => t.TotalExecutions),
                TotalSuccessfulExecutions = _performanceTrackers.Values.Sum(t => t.SuccessfulExecutions),
                OverallSuccessRate = CalculateOverallSuccessRate()
            };
        }

        /// <summary>
        /// Force cleanup of expired timeouts and old performance data
        /// </summary>
        public void ForceCleanup()
        {
            CleanupExpiredTimeouts();
            CleanupOldPerformanceData();
            CleanupTokenRegistrations();
        }
        #endregion

        #region Private Methods
        private float CalculateAdaptiveTimeout(float baseTimeout, CommandPerformanceTracker tracker)
        {
            // Enhanced adaptive timeout with multiple strategies
            var strategyTimeout = _config.AdaptiveTimeoutStrategy switch
            {
                TimeoutStrategy.Conservative => CalculateConservativeTimeout(baseTimeout, tracker),
                TimeoutStrategy.Aggressive => CalculateAggressiveTimeout(baseTimeout, tracker),
                TimeoutStrategy.Balanced => CalculateBalancedTimeout(baseTimeout, tracker),
                TimeoutStrategy.MachineLearning => CalculateMLBasedTimeout(baseTimeout, tracker),
                _ => CalculateBalancedTimeout(baseTimeout, tracker)
            };

            // Ensure adaptive timeout is within reasonable bounds
            var minTimeout = baseTimeout * _config.MinAdaptiveTimeoutFactor;
            var maxTimeout = baseTimeout * _config.MaxAdaptiveTimeoutFactor;
            
            return Mathf.Clamp(strategyTimeout, minTimeout, maxTimeout);
        }

        /// <summary>
        /// Conservative approach - uses 99th percentile for high reliability
        /// </summary>
        private float CalculateConservativeTimeout(float baseTimeout, CommandPerformanceTracker tracker)
        {
            var p99Time = tracker.GetPercentile(0.99);
            return (float)(p99Time * _config.AdaptiveTimeoutMultiplier * 1.2f); // Extra buffer
        }

        /// <summary>
        /// Aggressive approach - uses 90th percentile for faster execution
        /// </summary>
        private float CalculateAggressiveTimeout(float baseTimeout, CommandPerformanceTracker tracker)
        {
            var p90Time = tracker.GetPercentile(0.90);
            return (float)(p90Time * _config.AdaptiveTimeoutMultiplier * 0.9f); // Reduced buffer
        }

        /// <summary>
        /// Balanced approach - uses 95th percentile with standard buffer
        /// </summary>
        private float CalculateBalancedTimeout(float baseTimeout, CommandPerformanceTracker tracker)
        {
            var p95Time = tracker.GetPercentile(0.95);
            return (float)(p95Time * _config.AdaptiveTimeoutMultiplier);
        }

        /// <summary>
        /// Machine Learning inspired approach - uses recent trend analysis and success rate weighting
        /// </summary>
        private float CalculateMLBasedTimeout(float baseTimeout, CommandPerformanceTracker tracker)
        {
            // Get recent performance trend
            var recentTrend = tracker.GetRecentPerformanceTrend();
            var successRate = tracker.SuccessRate;
            
            // Base calculation on 95th percentile
            var p95Time = tracker.GetPercentile(0.95);
            var baseAdaptive = (float)(p95Time * _config.AdaptiveTimeoutMultiplier);
            
            // Apply trend adjustment
            var trendFactor = 1.0f;
            if (recentTrend > 0.1) // Performance getting worse
            {
                trendFactor = 1.1f + (recentTrend * 0.5f); // Increase timeout
            }
            else if (recentTrend < -0.1) // Performance getting better
            {
                trendFactor = 0.9f + (Math.Abs(recentTrend) * 0.3f); // Slightly decrease timeout
            }
            
            // Apply success rate weighting
            var successFactor = successRate < 0.9f ? 1.1f : 0.95f; // More buffer for unreliable commands
            
            return baseAdaptive * trendFactor * successFactor;
        }

        private void PerformPeriodicCleanup()
        {
            lock (_cleanupLock)
            {
                if ((DateTime.Now - _lastCleanup).TotalMinutes >= CleanupIntervalMinutes)
                {
                    ForceCleanup();
                    _lastCleanup = DateTime.Now;
                }
            }
        }

        private void CleanupExpiredTimeouts()
        {
            var expiredKeys = new List<string>();
            var cutoff = DateTime.Now.AddMinutes(-CleanupIntervalMinutes);

            foreach (var kvp in _activeTimeouts)
            {
                if (kvp.Value.StartTime < cutoff || kvp.Value.TokenSource.IsCancellationRequested)
                {
                    expiredKeys.Add(kvp.Key);
                    kvp.Value.TokenSource?.Dispose();
                    
                    // Also dispose corresponding token registration
                    if (_tokenRegistrations.TryRemove(kvp.Key, out var registration))
                    {
                        registration.Dispose();
                    }
                }
            }

            foreach (var key in expiredKeys)
            {
                _activeTimeouts.TryRemove(key, out _);
            }

            if (Application.isEditor && expiredKeys.Count > 0)
            {
                Debug.Log($"[TimeoutManager] Cleaned up {expiredKeys.Count} expired timeouts");
            }
        }

        private void CleanupTokenRegistrations()
        {
            var orphanedKeys = new List<string>();

            foreach (var kvp in _tokenRegistrations)
            {
                // Clean up registrations that no longer have corresponding active timeouts
                if (!_activeTimeouts.ContainsKey(kvp.Key))
                {
                    orphanedKeys.Add(kvp.Key);
                    kvp.Value.Dispose();
                }
            }

            foreach (var key in orphanedKeys)
            {
                _tokenRegistrations.TryRemove(key, out _);
            }

            if (Application.isEditor && orphanedKeys.Count > 0)
            {
                Debug.Log($"[TimeoutManager] Cleaned up {orphanedKeys.Count} orphaned token registrations");
            }
        }

        private void CleanupOldPerformanceData()
        {
            // Remove performance trackers that haven't been used recently
            var cutoff = DateTime.Now.AddHours(-24);
            var keysToRemove = new List<Type>();

            foreach (var kvp in _performanceTrackers)
            {
                if (kvp.Value.LastUsed < cutoff && kvp.Value.TotalExecutions < 5)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _performanceTrackers.TryRemove(key, out _);
            }

            if (Application.isEditor && keysToRemove.Count > 0)
            {
                Debug.Log($"[TimeoutManager] Cleaned up {keysToRemove.Count} unused performance trackers");
            }
        }

        private float CalculateOverallSuccessRate()
        {
            var totalExecutions = _performanceTrackers.Values.Sum(t => t.TotalExecutions);
            if (totalExecutions == 0) return 1.0f;

            var totalSuccessful = _performanceTrackers.Values.Sum(t => t.SuccessfulExecutions);
            return (float)totalSuccessful / totalExecutions;
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the TimeoutManager and clean up all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Dispose all active timeout token sources
                foreach (var kvp in _activeTimeouts)
                {
                    kvp.Value.TokenSource?.Dispose();
                }
                _activeTimeouts.Clear();

                // Dispose all token registrations
                foreach (var kvp in _tokenRegistrations)
                {
                    kvp.Value.Dispose();
                }
                _tokenRegistrations.Clear();

                // Clear performance trackers
                _performanceTrackers.Clear();

                Debug.Log("[TimeoutManager] Disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TimeoutManager] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
        #endregion

        #region Nested Classes
        private class ActiveTimeout
        {
            public Type CommandType { get; set; }
            public float TimeoutSeconds { get; set; }
            public DateTime StartTime { get; set; }
            public CancellationTokenSource TokenSource { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// Tracks performance statistics for a specific command type
    /// </summary>
    public class CommandPerformanceTracker
    {
        private readonly object _lock = new object();
        private readonly Queue<ExecutionRecord> _recentExecutions = new Queue<ExecutionRecord>();
        private const int MaxHistorySize = 100;

        public Type CommandType { get; }
        public int TotalExecutions { get; private set; }
        public int SuccessfulExecutions { get; private set; }
        public double AverageExecutionTimeSeconds { get; private set; }
        public double MinExecutionTimeSeconds { get; private set; } = double.MaxValue;
        public double MaxExecutionTimeSeconds { get; private set; }
        public DateTime LastUsed { get; private set; } = DateTime.Now;
        public float SuccessRate => TotalExecutions > 0 ? (float)SuccessfulExecutions / TotalExecutions : 1.0f;

        public CommandPerformanceTracker(Type commandType)
        {
            CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        }

        public void RecordExecution(TimeSpan executionTime, bool succeeded, Exception error = null)
        {
            lock (_lock)
            {
                var record = new ExecutionRecord
                {
                    ExecutionTime = executionTime.TotalSeconds,
                    Succeeded = succeeded,
                    Timestamp = DateTime.Now,
                    ErrorType = error?.GetType()
                };

                _recentExecutions.Enqueue(record);
                if (_recentExecutions.Count > MaxHistorySize)
                {
                    _recentExecutions.Dequeue();
                }

                TotalExecutions++;
                if (succeeded) SuccessfulExecutions++;

                var execTimeSeconds = executionTime.TotalSeconds;
                MinExecutionTimeSeconds = Math.Min(MinExecutionTimeSeconds, execTimeSeconds);
                MaxExecutionTimeSeconds = Math.Max(MaxExecutionTimeSeconds, execTimeSeconds);
                
                // Recalculate average from recent executions
                AverageExecutionTimeSeconds = _recentExecutions.Average(r => r.ExecutionTime);
                LastUsed = DateTime.Now;
            }
        }

        public double GetPercentile(double percentile)
        {
            lock (_lock)
            {
                if (_recentExecutions.Count == 0) return 0;

                var sortedTimes = _recentExecutions
                    .Select(r => r.ExecutionTime)
                    .OrderBy(t => t)
                    .ToArray();
                
                var index = (int)(sortedTimes.Length * percentile);
                return sortedTimes[Math.Min(index, sortedTimes.Length - 1)];
            }
        }

        public Dictionary<Type, int> GetErrorFrequency()
        {
            lock (_lock)
            {
                return _recentExecutions
                    .Where(r => !r.Succeeded && r.ErrorType != null)
                    .GroupBy(r => r.ErrorType)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        /// <summary>
        /// Gets the recent performance trend (-1 to 1, where negative is improving, positive is degrading)
        /// </summary>
        public float GetRecentPerformanceTrend()
        {
            lock (_lock)
            {
                if (_recentExecutions.Count < 10) return 0f; // Need enough data points

                var executions = _recentExecutions.ToArray();
                var recentCount = Math.Min(20, executions.Length / 2); // Use last 50% or 20 records, whichever is smaller
                var olderCount = Math.Min(20, executions.Length - recentCount);

                if (recentCount < 5 || olderCount < 5) return 0f; // Need meaningful samples

                // Get average execution time for recent vs older executions
                var recentAvg = executions.Skip(executions.Length - recentCount)
                                         .Average(r => r.ExecutionTime);
                
                var olderAvg = executions.Take(olderCount)
                                        .Average(r => r.ExecutionTime);

                if (olderAvg <= 0) return 0f;

                // Calculate trend as percentage change (positive = worse performance)
                var trend = (float)((recentAvg - olderAvg) / olderAvg);
                
                // Clamp to reasonable range
                return Mathf.Clamp(trend, -1f, 1f);
            }
        }

        public override string ToString()
        {
            return $"{CommandType.Name}: {TotalExecutions} executions, " +
                   $"{SuccessRate:P1} success rate, " +
                   $"{AverageExecutionTimeSeconds:F2}s avg";
        }

        private class ExecutionRecord
        {
            public double ExecutionTime { get; set; }
            public bool Succeeded { get; set; }
            public DateTime Timestamp { get; set; }
            public Type ErrorType { get; set; }
        }
    }

    /// <summary>
    /// Statistics for timeout manager monitoring
    /// </summary>
    public class TimeoutManagerStats
    {
        public int ActiveTimeouts { get; set; }
        public int TrackedCommandTypes { get; set; }
        public int TotalExecutions { get; set; }
        public int TotalSuccessfulExecutions { get; set; }
        public float OverallSuccessRate { get; set; }

        public override string ToString()
        {
            return $"TimeoutManager: {ActiveTimeouts} active, " +
                   $"{TrackedCommandTypes} tracked types, " +
                   $"{TotalExecutions} total executions, " +
                   $"{OverallSuccessRate:P1} success rate";
        }
    }
}
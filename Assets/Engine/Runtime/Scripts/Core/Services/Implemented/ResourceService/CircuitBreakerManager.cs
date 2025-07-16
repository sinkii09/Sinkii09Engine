using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Circuit breaker states
    /// </summary>
    public enum CircuitBreakerStateType
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Failure record for pattern analysis
    /// </summary>
    public class FailureRecord
    {
        public DateTime Timestamp { get; set; }
        public string ErrorType { get; set; }
        public string ErrorMessage { get; set; }
        public double ResponseTime { get; set; }
        public int Severity { get; set; } = 1; // 1-5 scale
    }

    /// <summary>
    /// Success record for health scoring
    /// </summary>
    public class SuccessRecord
    {
        public DateTime Timestamp { get; set; }
        public double ResponseTime { get; set; }
    }

    /// <summary>
    /// Manages circuit breakers for resource providers to handle failures gracefully
    /// </summary>
    public class CircuitBreakerManager
    {
        private readonly Dictionary<ProviderType, CircuitBreakerState> _circuitBreakers;
        private readonly ResourceServiceConfiguration _config;
        private readonly object _lock = new object();

        /// <summary>
        /// Enhanced circuit breaker state with sophisticated failure tracking
        /// </summary>
        public class CircuitBreakerState
        {
            public ProviderType ProviderType { get; set; }
            public int FailureCount { get; set; }
            public DateTime LastFailureTime { get; set; }
            public bool IsOpen { get; set; }
            public DateTime OpenedTime { get; set; }
            public int SuccessCount { get; set; }
            public DateTime LastSuccessTime { get; set; }
            
            // Enhanced tracking for sophisticated algorithms
            public List<FailureRecord> RecentFailures { get; set; } = new List<FailureRecord>();
            public List<SuccessRecord> RecentSuccesses { get; set; } = new List<SuccessRecord>();
            public float HealthScore { get; set; } = 1.0f;
            public int ConsecutiveFailures { get; set; } = 0;
            public int ConsecutiveSuccesses { get; set; } = 0;
            public DateTime LastHealthUpdate { get; set; } = DateTime.UtcNow;
            public double AverageResponseTime { get; set; } = 0;
            public CircuitBreakerStateType StateType { get; set; } = CircuitBreakerStateType.Closed;
            
            // Error pattern recognition
            public Dictionary<string, int> ErrorPatterns { get; set; } = new Dictionary<string, int>();
            public DateTime NextRetryTime { get; set; } = DateTime.MinValue;

            /// <summary>
            /// Check if the circuit breaker should allow a request with enhanced logic
            /// </summary>
            public bool ShouldAttemptRequest(ResourceServiceConfiguration config)
            {
                UpdateHealthScore();
                
                switch (StateType)
                {
                    case CircuitBreakerStateType.Closed:
                        return true;
                        
                    case CircuitBreakerStateType.Open:
                        // Check if we should transition to half-open
                        return ShouldTransitionToHalfOpen(config);
                        
                    case CircuitBreakerStateType.HalfOpen:
                        // In half-open state, allow limited requests based on health score
                        return DateTime.UtcNow >= NextRetryTime && HealthScore > 0.3f;
                        
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Determine if circuit should transition from open to half-open
            /// </summary>
            private bool ShouldTransitionToHalfOpen(ResourceServiceConfiguration config)
            {
                var baseTimeout = TimeSpan.FromSeconds(config.CircuitBreakerTimeoutSeconds);
                
                // Adaptive timeout based on failure patterns
                var adaptiveTimeout = CalculateAdaptiveTimeout(baseTimeout);
                
                if (DateTime.UtcNow - OpenedTime > adaptiveTimeout)
                {
                    StateType = CircuitBreakerStateType.HalfOpen;
                    NextRetryTime = DateTime.UtcNow.AddSeconds(1); // Initial retry delay
                    
                    if (config.EnableDetailedLogging)
                    {
                        Debug.Log($"Circuit breaker for {ProviderType} transitioning to half-open state " +
                                 $"(Health Score: {HealthScore:F2})");
                    }
                    
                    return true;
                }
                
                return false;
            }

            /// <summary>
            /// Calculate adaptive timeout based on error patterns
            /// </summary>
            private TimeSpan CalculateAdaptiveTimeout(TimeSpan baseTimeout)
            {
                // Increase timeout for frequent failures
                var recentFailureRate = GetRecentFailureRate();
                var multiplier = 1.0 + (recentFailureRate * 2.0); // Up to 3x base timeout
                
                // Factor in error severity
                var avgSeverity = GetAverageErrorSeverity();
                multiplier *= (1.0 + avgSeverity * 0.2); // Up to 1x additional for severity
                
                var adaptiveTimeout = TimeSpan.FromMilliseconds(baseTimeout.TotalMilliseconds * multiplier);
                
                // Cap at reasonable maximum (10 minutes)
                return adaptiveTimeout > TimeSpan.FromMinutes(10) 
                    ? TimeSpan.FromMinutes(10) 
                    : adaptiveTimeout;
            }

            /// <summary>
            /// Get recent failure rate (failures per minute)
            /// </summary>
            private double GetRecentFailureRate()
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-5);
                var recentFailures = RecentFailures.Count(f => f.Timestamp > cutoffTime);
                return recentFailures / 5.0; // Failures per minute
            }

            /// <summary>
            /// Get average error severity
            /// </summary>
            private double GetAverageErrorSeverity()
            {
                if (RecentFailures.Count == 0) return 0;
                return RecentFailures.Average(f => f.Severity);
            }

            /// <summary>
            /// Record a successful operation with enhanced tracking
            /// </summary>
            public void RecordSuccess(ResourceServiceConfiguration config, double responseTime = 0)
            {
                SuccessCount++;
                ConsecutiveSuccesses++;
                ConsecutiveFailures = 0;
                LastSuccessTime = DateTime.UtcNow;

                // Record success details
                var successRecord = new SuccessRecord
                {
                    Timestamp = DateTime.UtcNow,
                    ResponseTime = responseTime
                };
                
                RecentSuccesses.Add(successRecord);
                
                // Keep only recent successes (last hour)
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                RecentSuccesses.RemoveAll(s => s.Timestamp < cutoffTime);

                // Update average response time
                if (RecentSuccesses.Count > 0)
                {
                    AverageResponseTime = RecentSuccesses.Average(s => s.ResponseTime);
                }

                // Handle state transitions
                switch (StateType)
                {
                    case CircuitBreakerStateType.HalfOpen:
                        if (ConsecutiveSuccesses >= 3) // 3 consecutive successes to close
                        {
                            StateType = CircuitBreakerStateType.Closed;
                            IsOpen = false;
                            FailureCount = 0;
                            
                            if (config.EnableDetailedLogging)
                            {
                                Debug.Log($"Circuit breaker for {ProviderType} closed after successful recovery " +
                                         $"(Health Score: {HealthScore:F2})");
                            }
                        }
                        else
                        {
                            // Reduce retry delay on success
                            NextRetryTime = DateTime.UtcNow.AddSeconds(0.5);
                        }
                        break;
                        
                    case CircuitBreakerStateType.Open:
                        // Success while open shouldn't happen, but handle gracefully
                        StateType = CircuitBreakerStateType.HalfOpen;
                        NextRetryTime = DateTime.UtcNow.AddSeconds(1);
                        break;
                }
                
                UpdateHealthScore();
            }

            /// <summary>
            /// Record a failed operation with enhanced pattern recognition
            /// </summary>
            public void RecordFailure(ResourceServiceConfiguration config, Exception exception = null, double responseTime = 0)
            {
                FailureCount++;
                ConsecutiveFailures++;
                ConsecutiveSuccesses = 0;
                LastFailureTime = DateTime.UtcNow;

                // Analyze error pattern
                var errorType = "Unknown";
                var errorMessage = "Unknown error";
                var severity = 1;

                if (exception != null)
                {
                    errorType = exception.GetType().Name;
                    errorMessage = exception.Message;
                    severity = ClassifyErrorSeverity(exception);
                    
                    // Track error patterns
                    if (ErrorPatterns.ContainsKey(errorType))
                    {
                        ErrorPatterns[errorType]++;
                    }
                    else
                    {
                        ErrorPatterns[errorType] = 1;
                    }
                }

                // Record failure details
                var failureRecord = new FailureRecord
                {
                    Timestamp = DateTime.UtcNow,
                    ErrorType = errorType,
                    ErrorMessage = errorMessage,
                    ResponseTime = responseTime,
                    Severity = severity
                };
                
                RecentFailures.Add(failureRecord);
                
                // Keep only recent failures (last hour)
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                RecentFailures.RemoveAll(f => f.Timestamp < cutoffTime);

                // Handle state transitions
                switch (StateType)
                {
                    case CircuitBreakerStateType.Closed:
                        if (ShouldOpenCircuit(config))
                        {
                            StateType = CircuitBreakerStateType.Open;
                            IsOpen = true;
                            OpenedTime = DateTime.UtcNow;
                            
                            if (config.EnableDetailedLogging)
                            {
                                Debug.LogWarning($"Circuit breaker for {ProviderType} opened after {ConsecutiveFailures} " +
                                               $"consecutive failures (Health Score: {HealthScore:F2})");
                            }
                        }
                        break;
                        
                    case CircuitBreakerStateType.HalfOpen:
                        // Failure in half-open state - go back to open
                        StateType = CircuitBreakerStateType.Open;
                        OpenedTime = DateTime.UtcNow;
                        
                        // Increase retry delay exponentially
                        var currentDelay = (NextRetryTime - DateTime.UtcNow).TotalSeconds;
                        NextRetryTime = DateTime.UtcNow.AddSeconds(Math.Min(currentDelay * 2, 60)); // Max 1 minute
                        
                        if (config.EnableDetailedLogging)
                        {
                            Debug.LogWarning($"Circuit breaker for {ProviderType} reopened after failure in half-open state");
                        }
                        break;
                }
                
                UpdateHealthScore();
            }

            /// <summary>
            /// Classify error severity based on exception type
            /// </summary>
            private int ClassifyErrorSeverity(Exception exception)
            {
                return exception switch
                {
                    TimeoutException => 3,
                    UnauthorizedAccessException => 4,
                    OutOfMemoryException => 5,
                    System.Net.NetworkInformation.NetworkInformationException => 4,
                    System.IO.IOException => 2,
                    ArgumentException => 1,
                    InvalidOperationException => 2,
                    _ => 1
                };
            }

            /// <summary>
            /// Determine if circuit should open based on sophisticated criteria
            /// </summary>
            private bool ShouldOpenCircuit(ResourceServiceConfiguration config)
            {
                // Traditional threshold check
                if (ConsecutiveFailures >= config.CircuitBreakerFailureThreshold)
                    return true;
                
                // Health score threshold
                if (HealthScore < 0.2f)
                    return true;
                
                // Failure rate threshold (more than 80% failures in last 5 minutes)
                var recentOperations = RecentFailures.Count + RecentSuccesses.Count;
                if (recentOperations >= 5)
                {
                    var failureRate = (double)RecentFailures.Count / recentOperations;
                    if (failureRate > 0.8)
                        return true;
                }
                
                return false;
            }

            /// <summary>
            /// Update health score based on recent performance
            /// </summary>
            private void UpdateHealthScore()
            {
                var now = DateTime.UtcNow;
                var cutoffTime = now.AddMinutes(-10); // Look at last 10 minutes
                
                var recentFailures = RecentFailures.Where(f => f.Timestamp > cutoffTime).ToList();
                var recentSuccesses = RecentSuccesses.Where(s => s.Timestamp > cutoffTime).ToList();
                
                var totalOperations = recentFailures.Count + recentSuccesses.Count;
                
                if (totalOperations == 0)
                {
                    // No recent operations - maintain current health with slow recovery
                    HealthScore = Math.Min(1.0f, HealthScore + 0.01f);
                    return;
                }
                
                // Calculate base health from success rate
                var successRate = (double)recentSuccesses.Count / totalOperations;
                var baseHealth = (float)successRate;
                
                // Adjust for error severity
                if (recentFailures.Count > 0)
                {
                    var avgSeverity = recentFailures.Average(f => f.Severity);
                    var severityPenalty = (avgSeverity - 1) * 0.1f; // Up to 40% penalty
                    baseHealth -= (float)severityPenalty;
                }
                
                // Adjust for response time
                if (recentSuccesses.Count > 0)
                {
                    var avgResponseTime = recentSuccesses.Average(s => s.ResponseTime);
                    if (avgResponseTime > 5000) // Penalty for slow responses (>5s)
                    {
                        var timePenalty = Math.Min(0.3f, (float)(avgResponseTime - 5000) / 10000);
                        baseHealth -= timePenalty;
                    }
                }
                
                // Smooth health score changes
                var alpha = 0.3f; // Smoothing factor
                HealthScore = alpha * Math.Max(0f, Math.Min(1f, baseHealth)) + (1 - alpha) * HealthScore;
                
                LastHealthUpdate = now;
            }

            /// <summary>
            /// Reset the circuit breaker state
            /// </summary>
            public void Reset()
            {
                FailureCount = 0;
                SuccessCount = 0;
                IsOpen = false;
                LastFailureTime = default;
                LastSuccessTime = default;
                OpenedTime = default;
            }
        }

        public CircuitBreakerManager(ResourceServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _circuitBreakers = new Dictionary<ProviderType, CircuitBreakerState>();
        }

        /// <summary>
        /// Register a provider with the circuit breaker manager
        /// </summary>
        public void RegisterProvider(ProviderType providerType, IResourceProvider provider)
        {
            if (!_config.EnableCircuitBreaker) return;

            lock (_lock)
            {
                if (!_circuitBreakers.ContainsKey(providerType))
                {
                    _circuitBreakers[providerType] = new CircuitBreakerState
                    {
                        ProviderType = providerType,
                        FailureCount = 0,
                        IsOpen = false
                    };
                }
            }
        }

        /// <summary>
        /// Check if a provider should be allowed to process a request
        /// </summary>
        public bool ShouldAllowRequest(ProviderType providerType)
        {
            if (!_config.EnableCircuitBreaker) return true;

            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    return state.ShouldAttemptRequest(_config);
                }
            }

            return true; // Allow if not registered
        }

        /// <summary>
        /// Record a successful operation for a provider with enhanced tracking
        /// </summary>
        public void RecordSuccess(ProviderType providerType, double responseTime = 0)
        {
            if (!_config.EnableCircuitBreaker) return;

            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    state.RecordSuccess(_config, responseTime);
                }
            }
        }

        /// <summary>
        /// Record a failed operation for a provider with enhanced pattern recognition
        /// </summary>
        public void RecordFailure(ProviderType providerType, Exception exception = null, double responseTime = 0)
        {
            if (!_config.EnableCircuitBreaker) return;

            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    state.RecordFailure(_config, exception, responseTime);
                    
                    if (exception != null && _config.EnableDetailedLogging)
                    {
                        Debug.LogError($"Provider {providerType} failure: {exception.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Get error pattern analysis for a provider
        /// </summary>
        public Dictionary<string, int> GetErrorPatterns(ProviderType providerType)
        {
            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    return new Dictionary<string, int>(state.ErrorPatterns);
                }
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Get provider health score
        /// </summary>
        public float GetHealthScore(ProviderType providerType)
        {
            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    return state.HealthScore;
                }
                return 1.0f; // Default healthy score
            }
        }

        /// <summary>
        /// Get comprehensive provider analytics
        /// </summary>
        public ProviderAnalytics GetProviderAnalytics(ProviderType providerType)
        {
            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    return new ProviderAnalytics
                    {
                        ProviderType = providerType,
                        HealthScore = state.HealthScore,
                        StateType = state.StateType,
                        ConsecutiveFailures = state.ConsecutiveFailures,
                        ConsecutiveSuccesses = state.ConsecutiveSuccesses,
                        AverageResponseTime = state.AverageResponseTime,
                        RecentFailureCount = state.RecentFailures.Count,
                        RecentSuccessCount = state.RecentSuccesses.Count,
                        ErrorPatterns = new Dictionary<string, int>(state.ErrorPatterns),
                        LastFailureTime = state.LastFailureTime,
                        LastSuccessTime = state.LastSuccessTime
                    };
                }
                
                return new ProviderAnalytics
                {
                    ProviderType = providerType,
                    HealthScore = 1.0f,
                    StateType = CircuitBreakerStateType.Closed
                };
            }
        }

        /// <summary>
        /// Get the current state of a circuit breaker
        /// </summary>
        public CircuitBreakerState GetState(ProviderType providerType)
        {
            lock (_lock)
            {
                return _circuitBreakers.TryGetValue(providerType, out var state) ? state : null;
            }
        }

        /// <summary>
        /// Get statistics for all circuit breakers
        /// </summary>
        public Dictionary<ProviderType, CircuitBreakerState> GetAllStates()
        {
            lock (_lock)
            {
                return new Dictionary<ProviderType, CircuitBreakerState>(_circuitBreakers);
            }
        }

        /// <summary>
        /// Reset a specific circuit breaker
        /// </summary>
        public void ResetCircuitBreaker(ProviderType providerType)
        {
            lock (_lock)
            {
                if (_circuitBreakers.TryGetValue(providerType, out var state))
                {
                    state.Reset();
                    Debug.Log($"Circuit breaker for {providerType} has been reset");
                }
            }
        }

        /// <summary>
        /// Reset all circuit breakers
        /// </summary>
        public void ResetAll()
        {
            lock (_lock)
            {
                foreach (var state in _circuitBreakers.Values)
                {
                    state.Reset();
                }
                Debug.Log("All circuit breakers have been reset");
            }
        }

        /// <summary>
        /// Get circuit breaker statistics for reporting
        /// </summary>
        public CircuitBreakerStatistics GetStatistics()
        {
            var stats = new CircuitBreakerStatistics
            {
                FailureCountByProvider = new Dictionary<ProviderType, int>(),
                CircuitStateByProvider = new Dictionary<ProviderType, bool>(),
                LastFailureTimeByProvider = new Dictionary<ProviderType, DateTime>()
            };

            lock (_lock)
            {
                foreach (var kvp in _circuitBreakers)
                {
                    var state = kvp.Value;
                    stats.FailureCountByProvider[kvp.Key] = state.FailureCount;
                    stats.CircuitStateByProvider[kvp.Key] = state.IsOpen;
                    stats.LastFailureTimeByProvider[kvp.Key] = state.LastFailureTime;
                }
            }

            return stats;
        }
    }

    /// <summary>
    /// Comprehensive analytics for a provider
    /// </summary>
    public class ProviderAnalytics
    {
        public ProviderType ProviderType { get; set; }
        public float HealthScore { get; set; }
        public CircuitBreakerStateType StateType { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int ConsecutiveSuccesses { get; set; }
        public double AverageResponseTime { get; set; }
        public int RecentFailureCount { get; set; }
        public int RecentSuccessCount { get; set; }
        public Dictionary<string, int> ErrorPatterns { get; set; } = new Dictionary<string, int>();
        public DateTime LastFailureTime { get; set; }
        public DateTime LastSuccessTime { get; set; }
        
        public double SuccessRate => RecentFailureCount + RecentSuccessCount > 0 
            ? (double)RecentSuccessCount / (RecentFailureCount + RecentSuccessCount) 
            : 1.0;
            
        public string GetMostCommonError()
        {
            if (ErrorPatterns.Count == 0) return "None";
            return ErrorPatterns.OrderByDescending(kvp => kvp.Value).First().Key;
        }
    }
}
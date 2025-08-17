using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for actor monitoring, statistics, validation, and global animation control
    /// Responsible for tracking performance, validating actors, and managing global animations
    /// </summary>
    public interface IActorMonitor : IDisposable
    {
        #region Statistics
        
        /// <summary>
        /// Gets comprehensive statistics about the actor system
        /// </summary>
        /// <returns>Current actor service statistics</returns>
        ActorServiceStatistics GetStatistics();
        
        /// <summary>
        /// Updates internal statistics by collecting current data from all actors
        /// </summary>
        void UpdateStatistics();
        
        /// <summary>
        /// Resets all statistics counters to zero
        /// </summary>
        void ResetStatistics();
        
        /// <summary>
        /// Gets factory statistics for monitoring creation performance
        /// </summary>
        /// <returns>Factory statistics if available</returns>
        ActorFactoryStatistics GetFactoryStatistics();
        
        /// <summary>
        /// Gets scene manager statistics for monitoring scene operations
        /// </summary>
        /// <returns>Scene manager statistics if available</returns>
        SceneManagerStatistics GetSceneStatistics();
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Validates all actors in the registry for consistency and errors
        /// </summary>
        /// <returns>Dictionary mapping actor IDs to their validation errors (empty array if valid)</returns>
        Dictionary<string, string[]> ValidateAllActors();
        
        /// <summary>
        /// Validates a specific actor for consistency and configuration errors
        /// </summary>
        /// <param name="actorId">ID of actor to validate</param>
        /// <param name="errors">Output array of validation errors</param>
        /// <returns>True if actor is valid, false if errors found</returns>
        bool ValidateActor(string actorId, out string[] errors);
        
        /// <summary>
        /// Validates the overall actor system health
        /// </summary>
        /// <param name="errors">Output array of system-level errors</param>
        /// <returns>True if system is healthy, false if errors found</returns>
        bool ValidateSystemHealth(out string[] errors);
        
        /// <summary>
        /// Performs a comprehensive health check including memory, performance, and consistency
        /// </summary>
        /// <returns>Health check result with detailed information</returns>
        ActorSystemHealthReport PerformHealthCheck();
        
        #endregion
        
        #region Debug Information
        
        /// <summary>
        /// Gets comprehensive debug information about the actor system
        /// </summary>
        /// <returns>Formatted debug information string</returns>
        string GetDebugInfo();
        
        /// <summary>
        /// Gets debug information for a specific actor
        /// </summary>
        /// <param name="actorId">ID of actor to get debug info for</param>
        /// <returns>Formatted debug information for the actor</returns>
        string GetActorDebugInfo(string actorId);
        
        /// <summary>
        /// Gets memory usage information for all actors
        /// </summary>
        /// <returns>Memory usage report</returns>
        ActorMemoryReport GetMemoryReport();
        
        /// <summary>
        /// Gets performance metrics for actor operations
        /// </summary>
        /// <returns>Performance metrics report</returns>
        ActorPerformanceReport GetPerformanceReport();
        
        #endregion
        
        #region Global Animation Control
        
        /// <summary>
        /// Stops all animations on all actors immediately
        /// </summary>
        void StopAllAnimations();
        
        /// <summary>
        /// Pauses all animations on all actors
        /// </summary>
        void PauseAllAnimations();
        
        /// <summary>
        /// Resumes all paused animations on all actors
        /// </summary>
        void ResumeAllAnimations();
        
        /// <summary>
        /// Sets the global animation speed multiplier for all actors
        /// </summary>
        /// <param name="speedMultiplier">Speed multiplier (1.0 = normal, 2.0 = double speed, 0.5 = half speed)</param>
        void SetGlobalAnimationSpeed(float speedMultiplier);
        
        /// <summary>
        /// Gets the current global animation speed multiplier
        /// </summary>
        /// <returns>Current global animation speed</returns>
        float GetGlobalAnimationSpeed();
        
        /// <summary>
        /// Gets the total number of active animations across all actors
        /// </summary>
        /// <returns>Number of active animations</returns>
        int GetActiveAnimationCount();
        
        #endregion
        
        #region Performance Monitoring
        
        /// <summary>
        /// Starts performance monitoring for all actor operations
        /// </summary>
        void StartPerformanceMonitoring();
        
        /// <summary>
        /// Stops performance monitoring
        /// </summary>
        void StopPerformanceMonitoring();
        
        /// <summary>
        /// Gets the average time for a specific operation type
        /// </summary>
        /// <param name="operationType">Type of operation (e.g., "Creation", "StateChange", "Animation")</param>
        /// <returns>Average operation time in milliseconds</returns>
        TimeSpan GetAverageOperationTime(string operationType);
        
        /// <summary>
        /// Records the execution time of an operation for performance tracking
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="executionTime">Time taken to execute</param>
        void RecordOperationTime(string operationType, TimeSpan executionTime);
        
        /// <summary>
        /// Gets performance metrics for the last N operations
        /// </summary>
        /// <param name="operationType">Type of operation to analyze</param>
        /// <param name="sampleSize">Number of recent operations to analyze</param>
        /// <returns>Performance metrics for recent operations</returns>
        OperationPerformanceMetrics GetRecentPerformanceMetrics(string operationType, int sampleSize = 100);
        
        #endregion
        
        #region Configuration and Runtime Control
        
        /// <summary>
        /// Updates monitor configuration at runtime
        /// </summary>
        /// <param name="enableValidation">Whether to enable automatic validation</param>
        /// <param name="enablePerformanceTracking">Whether to track performance metrics</param>
        /// <param name="statisticsUpdateInterval">Interval for automatic statistics updates (in seconds)</param>
        void Configure(bool enableValidation, bool enablePerformanceTracking, float statisticsUpdateInterval);
        
        /// <summary>
        /// Gets the current monitor configuration
        /// </summary>
        /// <returns>Current monitor configuration</returns>
        ActorMonitorConfiguration GetConfiguration();
        
        /// <summary>
        /// Enables or disables automatic validation checks
        /// </summary>
        /// <param name="enabled">Whether validation should be enabled</param>
        void SetValidationEnabled(bool enabled);
        
        /// <summary>
        /// Enables or disables performance tracking
        /// </summary>
        /// <param name="enabled">Whether performance tracking should be enabled</param>
        void SetPerformanceTrackingEnabled(bool enabled);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a validation error is detected
        /// </summary>
        event Action<string, string[]> OnValidationErrorDetected;
        
        /// <summary>
        /// Fired when system health status changes
        /// </summary>
        event Action<ActorSystemHealthStatus> OnHealthStatusChanged;
        
        /// <summary>
        /// Fired when performance thresholds are exceeded
        /// </summary>
        event Action<string, TimeSpan> OnPerformanceThresholdExceeded;
        
        #endregion
    }
    
    /// <summary>
    /// Health check result for the actor system
    /// </summary>
    public class ActorSystemHealthReport
    {
        public ActorSystemHealthStatus OverallHealth { get; set; }
        public Dictionary<string, string[]> ValidationErrors { get; set; } = new();
        public ActorMemoryReport MemoryReport { get; set; }
        public ActorPerformanceReport PerformanceReport { get; set; }
        public string[] SystemWarnings { get; set; } = Array.Empty<string>();
        public DateTime GeneratedAt { get; set; }
        public TimeSpan HealthCheckDuration { get; set; }
    }
    
    /// <summary>
    /// Memory usage report for actors
    /// </summary>
    public class ActorMemoryReport
    {
        public long TotalMemoryUsage { get; set; }
        public long AverageActorMemoryUsage { get; set; }
        public int TotalGameObjects { get; set; }
        public int TotalComponents { get; set; }
        public Dictionary<string, long> MemoryByActorType { get; set; } = new();
        public string[] MemoryWarnings { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// Performance metrics report for actors
    /// </summary>
    public class ActorPerformanceReport
    {
        public float AverageFrameTime { get; set; }
        public int ActiveAnimationCount { get; set; }
        public Dictionary<string, TimeSpan> AverageOperationTimes { get; set; } = new();
        public Dictionary<string, int> OperationCounts { get; set; } = new();
        public string[] PerformanceWarnings { get; set; } = Array.Empty<string>();
        public DateTime ReportGeneratedAt { get; set; }
    }
    
    /// <summary>
    /// Performance metrics for a specific operation type
    /// </summary>
    public class OperationPerformanceMetrics
    {
        public string OperationType { get; set; }
        public TimeSpan AverageTime { get; set; }
        public TimeSpan MinTime { get; set; }
        public TimeSpan MaxTime { get; set; }
        public int SampleCount { get; set; }
        public TimeSpan StandardDeviation { get; set; }
        public float SuccessRate { get; set; }
    }
    
    /// <summary>
    /// Configuration for actor monitor
    /// </summary>
    public class ActorMonitorConfiguration
    {
        public bool ValidationEnabled { get; set; } = true;
        public bool PerformanceTrackingEnabled { get; set; } = true;
        public float StatisticsUpdateInterval { get; set; } = 5.0f;
        public int MaxPerformanceSamples { get; set; } = 1000;
        public bool AutoHealthChecks { get; set; } = true;
        public float HealthCheckInterval { get; set; } = 30.0f;
    }
    
    /// <summary>
    /// System health status enumeration
    /// </summary>
    public enum ActorSystemHealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Error
    }
}
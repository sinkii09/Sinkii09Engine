using System;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Comprehensive metadata attribute for command behavior configuration.
    /// Works alongside CommandAliasAttribute to provide clean dual-attribute system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CommandMetaAttribute : Attribute
    {
        #region Timeout Management
        /// <summary>
        /// Maximum execution time in seconds before timeout
        /// </summary>
        public float Timeout { get; set; } = 30f;
        
        /// <summary>
        /// Enable adaptive timeout based on historical performance
        /// </summary>
        public bool AdaptiveTimeout { get; set; } = true;
        #endregion

        #region Retry Policy
        /// <summary>
        /// Maximum number of retry attempts on failure
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// Base delay between retry attempts in seconds
        /// </summary>
        public float RetryDelay { get; set; } = 1f;
        
        /// <summary>
        /// Strategy for calculating retry delays
        /// </summary>
        public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.Linear;
        #endregion

        #region Command Classification
        /// <summary>
        /// Category of command for behavior inheritance and optimization
        /// </summary>
        public CommandCategory Category { get; set; } = CommandCategory.Generic;
        
        /// <summary>
        /// Whether this command is critical and should halt execution on failure
        /// </summary>
        public bool Critical { get; set; } = false;
        
        /// <summary>
        /// Priority level for execution ordering and resource allocation
        /// </summary>
        public CommandPriority Priority { get; set; } = CommandPriority.Normal;
        #endregion

        #region Recovery Behavior
        /// <summary>
        /// Action to take when command fails after all retries
        /// </summary>
        public FallbackAction Fallback { get; set; } = FallbackAction.Continue;
        
        /// <summary>
        /// Whether this command can be skipped during error recovery
        /// </summary>
        public bool SkippableOnError { get; set; } = true;
        
        /// <summary>
        /// Whether to show user prompt for manual intervention on failure
        /// </summary>
        public bool AllowUserIntervention { get; set; } = false;
        #endregion

        #region Performance
        /// <summary>
        /// Whether command results can be cached for performance
        /// </summary>
        public bool Cacheable { get; set; } = true;
        
        /// <summary>
        /// Enable performance tracking and metrics collection
        /// </summary>
        public bool TrackPerformance { get; set; } = true;
        
        /// <summary>
        /// Expected average execution time for performance monitoring
        /// </summary>
        public float ExpectedDuration { get; set; } = 0f;
        #endregion

        #region Validation
        /// <summary>
        /// Whether to validate parameters before execution
        /// </summary>
        public bool ValidateParameters { get; set; } = true;
        
        /// <summary>
        /// Whether to preload required resources during preprocessing
        /// </summary>
        public bool PreloadResources { get; set; } = false;
        #endregion

        #region Constructor
        public CommandMetaAttribute() { }
        
        /// <summary>
        /// Convenience constructor for most common scenarios
        /// </summary>
        public CommandMetaAttribute(
            float timeout = 30f,
            int maxRetries = 3,
            CommandCategory category = CommandCategory.Generic,
            bool critical = false,
            RetryStrategy retryStrategy = RetryStrategy.Linear,
            FallbackAction fallback = FallbackAction.Continue,
            bool preloadResources = true,
            float expectedDuration = 2f,
            bool trackPerformance = true)
        {
            Timeout = timeout;
            MaxRetries = maxRetries;
            Category = category;
            Critical = critical;
            RetryStrategy = retryStrategy;
            PreloadResources = preloadResources;
            ExpectedDuration = expectedDuration;
            Fallback = fallback;
            TrackPerformance = trackPerformance;
        }
        #endregion
    }

    #region Supporting Enums
    /// <summary>
    /// Categories for command behavior inheritance and optimization
    /// </summary>
    public enum CommandCategory
    {
        /// <summary>Default category for uncategorized commands</summary>
        Generic = 0,
        
        /// <summary>Commands that load external resources (textures, audio, etc.)</summary>
        ResourceLoading = 1,
        
        /// <summary>Dialogue and narrative commands</summary>
        Dialogue = 2,
        
        /// <summary>Animation and visual effect commands</summary>
        Animation = 3,
        
        /// <summary>Audio playback and sound commands</summary>
        Audio = 4,
        
        /// <summary>User interface manipulation commands</summary>
        UI = 5,
        
        /// <summary>Script flow control (if, goto, loop, etc.)</summary>
        FlowControl = 6,
        
        /// <summary>Network communication commands</summary>
        Network = 7,
        
        /// <summary>Timing and delay commands</summary>
        Timing = 8,
        
        /// <summary>Variable manipulation commands</summary>
        Variables = 9,
        
        /// <summary>Save/load state management commands</summary>
        StateManagement = 10,
        
        /// <summary>Actor spawning, positioning, and management</summary>
        ActorManagement = 11,
        
        /// <summary>Scene and environment commands</summary>
        Scene = 12
    }

    /// <summary>
    /// Retry delay calculation strategies
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>Fixed delay between retries</summary>
        Linear = 0,
        
        /// <summary>Exponentially increasing delay (1s, 2s, 4s, 8s...)</summary>
        Exponential = 1,
        
        /// <summary>Adaptive delay based on historical performance</summary>
        Adaptive = 2,
        
        /// <summary>Immediate retry with no delay</summary>
        Immediate = 3
    }

    /// <summary>
    /// Actions to take when command fails after all retries
    /// </summary>
    public enum FallbackAction
    {
        /// <summary>Skip failed command and continue execution</summary>
        Continue = 0,
        
        /// <summary>Retry with fallback parameters or simplified execution</summary>
        Retry = 1,
        
        /// <summary>Stop script execution immediately</summary>
        Stop = 2,
        
        /// <summary>Prompt user for manual intervention</summary>
        Prompt = 3,
        
        /// <summary>Jump to error handling section of script</summary>
        JumpToErrorHandler = 4,
        
        /// <summary>Revert to previous safe state</summary>
        Rollback = 5
    }

    /// <summary>
    /// Command execution priority levels
    /// </summary>
    public enum CommandPriority
    {
        /// <summary>Low priority, can be deferred</summary>
        Low = 0,
        
        /// <summary>Normal priority</summary>
        Normal = 1,
        
        /// <summary>High priority, should execute quickly</summary>
        High = 2,
        
        /// <summary>Critical priority, execute immediately</summary>
        Critical = 3
    }
    #endregion
}
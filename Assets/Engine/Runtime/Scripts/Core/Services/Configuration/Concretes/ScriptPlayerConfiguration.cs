using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the ScriptPlayer service with execution, performance, and debug settings
    /// </summary>
    [CreateAssetMenu(fileName = "ScriptPlayerConfiguration", menuName = "Engine/Services/ScriptPlayerConfiguration", order = 1)]
    public class ScriptPlayerConfiguration : ServiceConfigurationBase
    {
        #region Execution Settings
        [Header("Execution Settings")]
        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("Default playback speed multiplier")]
        private float _defaultPlaybackSpeed = 1.0f;

        [SerializeField]
        [Range(0.1f, 120f)]
        [Tooltip("Maximum time in seconds to wait for a command to execute")]
        private float _commandExecutionTimeout = 30f;

        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("Default delay between lines in seconds")]
        private float _defaultLineDelay = 0f;

        [SerializeField]
        [Tooltip("Enable automatic saving of execution state")]
        private bool _enableAutoSave = true;

        [SerializeField]
        [Range(10f, 300f)]
        [Tooltip("Interval for auto-saving execution state in seconds")]
        private float _autoSaveInterval = 60f;

        [SerializeField]
        [Tooltip("Pause execution when hitting breakpoints")]
        private bool _pauseOnBreakpoints = true;

        [SerializeField]
        [Tooltip("Continue execution after errors")]
        private bool _continueOnError = false;
        #endregion

        #region Performance Settings
        [Header("Performance Settings")]
        [SerializeField]
        [Range(1, 50)]
        [Tooltip("Maximum depth of script call stack")]
        private int _maxExecutionStackDepth = 10;

        [SerializeField]
        [Tooltip("Enable caching of parsed commands")]
        private bool _enableCommandCaching = true;

        [SerializeField]
        [Range(10, 1000)]
        [Tooltip("Maximum number of cached commands")]
        private int _commandCacheSize = 100;

        [SerializeField]
        [Range(1, 100)]
        [Tooltip("Number of lines to batch process")]
        private int _batchProcessingSize = 10;

        [SerializeField]
        [Tooltip("Enable command batching optimization")]
        private bool _enableCommandBatching = true;

        [SerializeField]
        [Range(2, 50)]
        [Tooltip("Number of commands to batch together")]
        private int _batchSize = 5;

        [SerializeField]
        [Tooltip("Enable performance monitoring")]
        private bool _enablePerformanceMonitoring = true;

        [SerializeField]
        [Range(100, 10000)]
        [Tooltip("Maximum variables in execution context")]
        private int _maxContextVariables = 1000;
        #endregion

        #region Debug Settings
        [Header("Debug Settings")]
        [SerializeField]
        [Tooltip("Enable breakpoint support")]
        private bool _enableBreakpoints = true;

        [SerializeField]
        [Tooltip("Log execution flow to console")]
        private bool _logExecutionFlow = false;

        [SerializeField]
        [Tooltip("Enable step-by-step execution mode")]
        private bool _enableStepMode = false;

        [SerializeField]
        [Tooltip("Log command execution details")]
        private bool _logCommandExecution = false;

        [SerializeField]
        [Tooltip("Enable execution history tracking")]
        private bool _enableExecutionHistory = true;

        [SerializeField]
        [Range(10, 1000)]
        [Tooltip("Maximum execution history entries")]
        private int _maxExecutionHistorySize = 100;
        #endregion

        #region Error Handling
        [Header("Error Handling")]
        [SerializeField]
        [Tooltip("Enable error recovery mechanisms")]
        private bool _enableErrorRecovery = true;

        [SerializeField]
        [Range(1, 10)]
        [Tooltip("Maximum retry attempts for failed commands")]
        private int _maxRetryAttempts = 3;

        [SerializeField]
        [Range(0.1f, 5f)]
        [Tooltip("Delay between retry attempts in seconds")]
        private float _retryDelay = 1f;

        [SerializeField]
        [Tooltip("Skip lines with errors instead of stopping")]
        private bool _skipErrorLines = false;

        [SerializeField]
        [Tooltip("Log errors to file")]
        private bool _logErrorsToFile = true;
        #endregion

        #region Fast Forward Settings
        [Header("Fast Forward Settings")]
        [SerializeField]
        [Range(1f, 100f)]
        [Tooltip("Maximum fast forward speed")]
        private float _maxFastForwardSpeed = 10f;

        [SerializeField]
        [Tooltip("Skip animations during fast forward")]
        private bool _skipAnimationsInFastForward = true;

        [SerializeField]
        [Tooltip("Skip delays during fast forward")]
        private bool _skipDelaysInFastForward = true;

        [SerializeField]
        [Tooltip("Skip audio during fast forward")]
        private bool _skipAudioInFastForward = true;
        #endregion

        #region Save/Load Settings
        [Header("Save/Load Settings")]
        [SerializeField]
        [Tooltip("Enable saving execution state")]
        private bool _enableStateSaving = true;

        [SerializeField]
        [Tooltip("Compress saved state data")]
        private bool _compressSavedState = true;

        [SerializeField]
        [Range(1, 10)]
        [Tooltip("Maximum number of save state slots")]
        private int _maxSaveSlots = 5;
        #endregion

        #region Advanced Timeout Settings
        [Header("Advanced Timeout Settings")]
        [SerializeField]
        [Tooltip("Enable adaptive timeout based on command performance history")]
        private bool _enableAdaptiveTimeouts = true;

        [SerializeField]
        [Range(1.2f, 3.0f)]
        [Tooltip("Multiplier for adaptive timeout calculation")]
        private float _adaptiveTimeoutMultiplier = 1.5f;

        [SerializeField]
        [Range(0.5f, 1.0f)]
        [Tooltip("Minimum factor for adaptive timeout (relative to base timeout)")]
        private float _minAdaptiveTimeoutFactor = 0.7f;

        [SerializeField]
        [Range(2.0f, 5.0f)]
        [Tooltip("Maximum factor for adaptive timeout (relative to base timeout)")]
        private float _maxAdaptiveTimeoutFactor = 3.0f;

        [SerializeField]
        [Tooltip("Enable performance tracking for commands")]
        private bool _enablePerformanceTracking = true;

        [SerializeField]
        [Tooltip("Custom timeout overrides for specific command types")]
        private SerializedTypeFloatDictionary _customCommandTimeouts = new SerializedTypeFloatDictionary();

        [SerializeField]
        [Tooltip("Strategy for adaptive timeout calculation")]
        private TimeoutStrategy _adaptiveTimeoutStrategy = TimeoutStrategy.Balanced;
        #endregion

        #region Properties
        // Execution Settings
        public float DefaultPlaybackSpeed => _defaultPlaybackSpeed;
        public float CommandExecutionTimeout => _commandExecutionTimeout;
        public float DefaultLineDelay => _defaultLineDelay;
        public bool EnableAutoSave => _enableAutoSave;
        public float AutoSaveInterval => _autoSaveInterval;
        public bool PauseOnBreakpoints => _pauseOnBreakpoints;
        public bool ContinueOnError => _continueOnError;

        // Performance Settings
        public int MaxExecutionStackDepth => _maxExecutionStackDepth;
        public bool EnableCommandCaching => _enableCommandCaching;
        public int CommandCacheSize => _commandCacheSize;
        public int BatchProcessingSize => _batchProcessingSize;
        public bool EnableCommandBatching => _enableCommandBatching;
        public int BatchSize => _batchSize;
        public bool EnablePerformanceMonitoring => _enablePerformanceMonitoring;
        public int MaxContextVariables => _maxContextVariables;

        // Debug Settings
        public bool EnableBreakpoints => _enableBreakpoints;
        public bool LogExecutionFlow => _logExecutionFlow;
        public bool EnableStepMode => _enableStepMode;
        public bool LogCommandExecution => _logCommandExecution;
        public bool EnableExecutionHistory => _enableExecutionHistory;
        public int MaxExecutionHistorySize => _maxExecutionHistorySize;

        // Error Handling
        public bool EnableErrorRecovery => _enableErrorRecovery;
        public int MaxRetryAttempts => _maxRetryAttempts;
        public float RetryDelay => _retryDelay;
        public bool SkipErrorLines => _skipErrorLines;
        public bool LogErrorsToFile => _logErrorsToFile;

        // Fast Forward Settings
        public float MaxFastForwardSpeed => _maxFastForwardSpeed;
        public bool SkipAnimationsInFastForward => _skipAnimationsInFastForward;
        public bool SkipDelaysInFastForward => _skipDelaysInFastForward;
        public bool SkipAudioInFastForward => _skipAudioInFastForward;

        // Save/Load Settings
        public bool EnableStateSaving => _enableStateSaving;
        public bool CompressSavedState => _compressSavedState;
        public int MaxSaveSlots => _maxSaveSlots;

        // Advanced Timeout Settings
        public bool EnableAdaptiveTimeouts => _enableAdaptiveTimeouts;
        public float AdaptiveTimeoutMultiplier => _adaptiveTimeoutMultiplier;
        public float MinAdaptiveTimeoutFactor => _minAdaptiveTimeoutFactor;
        public float MaxAdaptiveTimeoutFactor => _maxAdaptiveTimeoutFactor;
        public bool EnablePerformanceTracking => _enablePerformanceTracking;
        public Dictionary<Type, float> CustomCommandTimeouts => _customCommandTimeouts?.ToDictionary();
        public TimeoutStrategy AdaptiveTimeoutStrategy => _adaptiveTimeoutStrategy;
        #endregion

        #region Validation
        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;

            // Validate playback speed
            if (_defaultPlaybackSpeed <= 0)
            {
                errors.Add("Default playback speed must be greater than 0");
                isValid = false;
            }

            // Validate timeout
            if (_commandExecutionTimeout <= 0)
            {
                errors.Add("Command execution timeout must be greater than 0");
                isValid = false;
            }

            // Validate stack depth
            if (_maxExecutionStackDepth <= 0)
            {
                errors.Add("Max execution stack depth must be greater than 0");
                isValid = false;
            }

            // Validate cache size
            if (_enableCommandCaching && _commandCacheSize <= 0)
            {
                errors.Add("Command cache size must be greater than 0 when caching is enabled");
                isValid = false;
            }

            // Validate auto-save interval
            if (_enableAutoSave && _autoSaveInterval <= 0)
            {
                errors.Add("Auto-save interval must be greater than 0 when auto-save is enabled");
                isValid = false;
            }

            // Validate retry attempts
            if (_enableErrorRecovery && _maxRetryAttempts <= 0)
            {
                errors.Add("Max retry attempts must be greater than 0 when error recovery is enabled");
                isValid = false;
            }

            return isValid;
        }

        protected override void OnResetToDefaults()
        {
            // Execution Settings
            _defaultPlaybackSpeed = 1.0f;
            _commandExecutionTimeout = 30f;
            _defaultLineDelay = 0f;
            _enableAutoSave = true;
            _autoSaveInterval = 60f;
            _pauseOnBreakpoints = true;
            _continueOnError = false;

            // Performance Settings
            _maxExecutionStackDepth = 10;
            _enableCommandCaching = true;
            _commandCacheSize = 100;
            _batchProcessingSize = 10;
            _enableCommandBatching = true;
            _batchSize = 5;
            _enablePerformanceMonitoring = true;
            _maxContextVariables = 1000;

            // Debug Settings
            _enableBreakpoints = true;
            _logExecutionFlow = false;
            _enableStepMode = false;
            _logCommandExecution = false;
            _enableExecutionHistory = true;
            _maxExecutionHistorySize = 100;

            // Error Handling
            _enableErrorRecovery = true;
            _maxRetryAttempts = 3;
            _retryDelay = 1f;
            _skipErrorLines = false;
            _logErrorsToFile = true;

            // Fast Forward Settings
            _maxFastForwardSpeed = 10f;
            _skipAnimationsInFastForward = true;
            _skipDelaysInFastForward = true;
            _skipAudioInFastForward = true;

            // Save/Load Settings
            _enableStateSaving = true;
            _compressSavedState = true;
            _maxSaveSlots = 5;

            // Advanced Timeout Settings
            _enableAdaptiveTimeouts = true;
            _adaptiveTimeoutMultiplier = 1.5f;
            _minAdaptiveTimeoutFactor = 0.7f;
            _maxAdaptiveTimeoutFactor = 3.0f;
            _enablePerformanceTracking = true;
            _customCommandTimeouts = new SerializedTypeFloatDictionary();
            _adaptiveTimeoutStrategy = TimeoutStrategy.Balanced;
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"ScriptPlayer Config: Speed {_defaultPlaybackSpeed}x, " +
                   $"Timeout {_commandExecutionTimeout}s, " +
                   $"Stack depth {_maxExecutionStackDepth}, " +
                   $"Caching {(_enableCommandCaching ? "enabled" : "disabled")}, " +
                   $"Auto-save {(_enableAutoSave ? $"every {_autoSaveInterval}s" : "disabled")}";
        }

        /// <summary>
        /// Get effective playback speed considering fast forward
        /// </summary>
        public float GetEffectiveSpeed(bool isFastForward, float currentSpeed)
        {
            if (isFastForward)
            {
                return Mathf.Min(currentSpeed * _maxFastForwardSpeed, _maxFastForwardSpeed);
            }
            return currentSpeed;
        }

        /// <summary>
        /// Should skip this type of command in fast forward
        /// </summary>
        public bool ShouldSkipInFastForward(CommandType commandType)
        {
            switch (commandType)
            {
                case CommandType.Animation:
                    return _skipAnimationsInFastForward;
                case CommandType.Delay:
                    return _skipDelaysInFastForward;
                case CommandType.Audio:
                    return _skipAudioInFastForward;
                default:
                    return false;
            }
        }
        #endregion
    }

    /// <summary>
    /// Types of commands for fast forward handling
    /// </summary>
    public enum CommandType
    {
        Generic,
        Animation,
        Delay,
        Audio,
        Dialog,
        Scene
    }

    /// <summary>
    /// Strategies for adaptive timeout calculation
    /// </summary>
    public enum TimeoutStrategy
    {
        /// <summary>
        /// Conservative approach - uses 99th percentile with extra buffer for high reliability
        /// </summary>
        Conservative,

        /// <summary>
        /// Aggressive approach - uses 90th percentile with reduced buffer for faster execution
        /// </summary>
        Aggressive,

        /// <summary>
        /// Balanced approach - uses 95th percentile with standard buffer
        /// </summary>
        Balanced,

        /// <summary>
        /// Machine Learning inspired approach with trend analysis and success rate weighting
        /// </summary>
        MachineLearning
    }
}
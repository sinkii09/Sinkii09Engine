using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services.Performance;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Smart recovery strategies framework for handling script execution errors.
    /// Uses command metadata and error classification for intelligent recovery decisions.
    /// </summary>
    public class ErrorRecoveryManager
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly Dictionary<Type, IErrorRecoveryStrategy> _customStrategies;
        private readonly Dictionary<CommandCategory, CategoryRecoveryPolicy> _categoryPolicies;
        private readonly List<RecoveryAttempt> _recoveryHistory;
        private readonly object _historyLock = new object();
        #endregion

        #region Constructor
        public ErrorRecoveryManager(ScriptPlayerConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _customStrategies = new Dictionary<Type, IErrorRecoveryStrategy>();
            _categoryPolicies = new Dictionary<CommandCategory, CategoryRecoveryPolicy>();
            _recoveryHistory = new List<RecoveryAttempt>();
            
            InitializeDefaultPolicies();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Reference to performance monitor for performance-aware recovery decisions
        /// </summary>
        public ServicePerformanceMonitor PerformanceMonitor { get; set; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Attempts to recover from a script execution error using smart strategies
        /// </summary>
        public async UniTask<RecoveryResult> AttemptRecoveryAsync(
            ScriptExecutionError error,
            ScriptExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var recoveryAttempt = new RecoveryAttempt
            {
                Error = error,
                Timestamp = DateTime.Now,
                ScriptName = context.Script?.Name,
                LineIndex = error.LineIndex
            };

            try
            {
                // Step 1: Check if error should trigger recovery at all
                if (!ShouldAttemptRecovery(error, context))
                {
                    recoveryAttempt.Result = RecoveryResult.NoRecoveryNeeded(error, "Error severity below recovery threshold");
                    RecordRecoveryAttempt(recoveryAttempt);
                    return recoveryAttempt.Result;
                }

                // Step 2: Try custom strategy for specific command type
                if (error.CommandType != null && _customStrategies.TryGetValue(error.CommandType, out var customStrategy))
                {
                    var customResult = await TryCustomStrategyAsync(customStrategy, error, context, cancellationToken);
                    if (customResult.Success)
                    {
                        recoveryAttempt.Result = customResult;
                        recoveryAttempt.StrategyUsed = "Custom";
                        RecordRecoveryAttempt(recoveryAttempt);
                        return customResult;
                    }
                }

                // Step 3: Try category-based recovery policy
                var categoryStrategy = GetCategoryStrategy(error);
                var categoryResult = await TryCategoryStrategyAsync(categoryStrategy, error, context, cancellationToken);
                
                recoveryAttempt.Result = categoryResult;
                recoveryAttempt.StrategyUsed = "Category";
                RecordRecoveryAttempt(recoveryAttempt);
                return categoryResult;
            }
            catch (Exception ex)
            {
                recoveryAttempt.Result = RecoveryResult.RecoveryFailed(error, $"Recovery strategy threw exception: {ex.Message}");
                RecordRecoveryAttempt(recoveryAttempt);
                throw new ScriptExecutionError(
                    $"Error recovery failed: {ex.Message}",
                    ErrorSeverity.Critical,
                    ErrorCategory.Unknown,
                    error.CommandType,
                    error.LineIndex,
                    error.ScriptName,
                    innerException: ex);
            }
        }

        /// <summary>
        /// Registers a custom recovery strategy for a specific command type
        /// </summary>
        public void RegisterCustomStrategy<TCommand>(IErrorRecoveryStrategy strategy) where TCommand : ICommand
        {
            RegisterCustomStrategy(typeof(TCommand), strategy);
        }

        /// <summary>
        /// Registers a custom recovery strategy for a specific command type
        /// </summary>
        public void RegisterCustomStrategy(Type commandType, IErrorRecoveryStrategy strategy)
        {
            if (commandType == null) throw new ArgumentNullException(nameof(commandType));
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            
            _customStrategies[commandType] = strategy;
            
            if (Application.isEditor)
            {
                Debug.Log($"[ErrorRecoveryManager] Registered custom recovery strategy for {commandType.Name}");
            }
        }

        /// <summary>
        /// Updates recovery policy for a command category
        /// </summary>
        public void SetCategoryPolicy(CommandCategory category, CategoryRecoveryPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            
            _categoryPolicies[category] = policy;
            
            if (Application.isEditor)
            {
                Debug.Log($"[ErrorRecoveryManager] Updated recovery policy for {category} category");
            }
        }

        /// <summary>
        /// Gets recovery statistics for monitoring
        /// </summary>
        public RecoveryStatistics GetStatistics()
        {
            lock (_historyLock)
            {
                var stats = new RecoveryStatistics();
                
                foreach (var attempt in _recoveryHistory)
                {
                    stats.TotalAttempts++;
                    
                    if (attempt.Result.Success)
                        stats.SuccessfulRecoveries++;
                    
                    if (attempt.Error.Category != ErrorCategory.Unknown)
                    {
                        if (!stats.RecoveriesByCategory.ContainsKey(attempt.Error.Category))
                            stats.RecoveriesByCategory[attempt.Error.Category] = 0;
                        stats.RecoveriesByCategory[attempt.Error.Category]++;
                    }
                    
                    if (attempt.Error.CommandType != null)
                    {
                        if (!stats.RecoveriesByCommandType.ContainsKey(attempt.Error.CommandType))
                            stats.RecoveriesByCommandType[attempt.Error.CommandType] = 0;
                        stats.RecoveriesByCommandType[attempt.Error.CommandType]++;
                    }
                }
                
                stats.SuccessRate = stats.TotalAttempts > 0 ? (float)stats.SuccessfulRecoveries / stats.TotalAttempts : 0f;
                return stats;
            }
        }

        /// <summary>
        /// Clears old recovery history to prevent memory buildup
        /// </summary>
        public void CleanupHistory()
        {
            lock (_historyLock)
            {
                var cutoff = DateTime.Now.AddHours(-24); // Keep last 24 hours
                _recoveryHistory.RemoveAll(attempt => attempt.Timestamp < cutoff);
            }
        }
        #endregion

        #region Private Methods
        private bool ShouldAttemptRecovery(ScriptExecutionError error, ScriptExecutionContext context)
        {
            // Don't recover from fatal errors unless explicitly configured
            if (error.Severity == ErrorSeverity.Fatal && !_config.EnableErrorRecovery)
                return false;

            // Don't recover if we've already attempted recovery for this line recently
            lock (_historyLock)
            {
                var recentAttempts = _recoveryHistory.FindAll(a => 
                    a.ScriptName == error.ScriptName && 
                    a.LineIndex == error.LineIndex &&
                    (DateTime.Now - a.Timestamp).TotalSeconds < 30);
                
                if (recentAttempts.Count >= 3) // Max 3 recovery attempts per line per 30 seconds
                    return false;
            }

            // Performance-aware recovery decision: skip recovery if command has persistent performance issues
            if (PerformanceMonitor != null && error.CommandType != null)
            {
                if (PerformanceMonitor.HasPerformanceIssues(error.CommandType))
                {
                    Debug.LogWarning($"Skipping recovery for {error.CommandType.Name} due to persistent performance issues");
                    return false;
                }
            }

            return true;
        }

        private async UniTask<RecoveryResult> TryCustomStrategyAsync(
            IErrorRecoveryStrategy strategy, 
            ScriptExecutionError error,
            ScriptExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                return await strategy.AttemptRecoveryAsync(error, context, cancellationToken);
            }
            catch (Exception ex)
            {
                return RecoveryResult.RecoveryFailed(error, $"Custom strategy failed: {ex.Message}");
            }
        }

        private async UniTask<RecoveryResult> TryCategoryStrategyAsync(
            CategoryRecoveryPolicy policy,
            ScriptExecutionError error,
            ScriptExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Apply retry logic if configured
            if (policy.ShouldRetry && error.RetryAttempt < policy.MaxRetries)
            {
                var delay = policy.CalculateRetryDelay(error.RetryAttempt);
                if (delay > 0)
                {
                    await UniTask.Delay((int)(delay * 1000), cancellationToken: cancellationToken);
                }
                
                return RecoveryResult.RetryCommand(error, $"Retrying with {policy.RetryStrategy} strategy");
            }

            // Apply fallback action
            switch (policy.FallbackAction)
            {
                case FallbackAction.Continue:
                    return RecoveryResult.SkipAndContinue(error, "Skipping failed command and continuing execution");
                
                case FallbackAction.Stop:
                    return RecoveryResult.StopExecution(error, "Stopping execution due to critical error");
                
                case FallbackAction.JumpToErrorHandler:
                    var errorHandlerLine = context.FindLabelLineIndex("@error") ?? context.FindLabelLineIndex("@onerror");
                    if (errorHandlerLine.HasValue)
                    {
                        return RecoveryResult.JumpToLine(error, errorHandlerLine.Value, "Jumping to error handler");
                    }
                    goto case FallbackAction.Continue; // Fallback to continue if no error handler
                
                case FallbackAction.Rollback:
                    if (context.CanRollbackToLastCheckpoint())
                    {
                        return RecoveryResult.RollbackToCheckpoint(error, "Rolling back to last stable checkpoint");
                    }
                    goto case FallbackAction.Continue; // Fallback to continue if no checkpoint
                
                case FallbackAction.Prompt:
                    if (policy.AllowUserIntervention && Application.isPlaying)
                    {
                        return RecoveryResult.PromptUser(error, "Requesting user intervention for error recovery");
                    }
                    goto case FallbackAction.Continue; // Fallback to continue if user intervention not available
                
                default:
                    return RecoveryResult.SkipAndContinue(error, "Using default continue fallback");
            }
        }

        private CategoryRecoveryPolicy GetCategoryStrategy(ScriptExecutionError error)
        {
            if (error.CommandMetadata != null && _categoryPolicies.TryGetValue(error.CommandMetadata.Category, out var policy))
            {
                return policy;
            }

            // Return default policy based on error category
            return error.Category switch
            {
                ErrorCategory.Timeout => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = true, 
                    MaxRetries = 2, 
                    RetryStrategy = RetryStrategy.Linear,
                    FallbackAction = FallbackAction.Continue 
                },
                ErrorCategory.ResourceLoading => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = true, 
                    MaxRetries = 3, 
                    RetryStrategy = RetryStrategy.Exponential,
                    FallbackAction = FallbackAction.Continue 
                },
                ErrorCategory.Network => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = true, 
                    MaxRetries = 5, 
                    RetryStrategy = RetryStrategy.Exponential,
                    FallbackAction = FallbackAction.Continue 
                },
                ErrorCategory.Validation => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = false, 
                    FallbackAction = FallbackAction.Continue 
                },
                ErrorCategory.ServiceDependency => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = false, 
                    FallbackAction = FallbackAction.Stop 
                },
                _ => new CategoryRecoveryPolicy 
                { 
                    ShouldRetry = true, 
                    MaxRetries = 1, 
                    FallbackAction = FallbackAction.Continue 
                }
            };
        }

        private void InitializeDefaultPolicies()
        {
            // Set up default policies for each command category
            SetCategoryPolicy(CommandCategory.ResourceLoading, new CategoryRecoveryPolicy
            {
                ShouldRetry = true,
                MaxRetries = 3,
                RetryStrategy = RetryStrategy.Exponential,
                BaseRetryDelay = 2.0f,
                FallbackAction = FallbackAction.Continue,
                AllowUserIntervention = false
            });

            SetCategoryPolicy(CommandCategory.Network, new CategoryRecoveryPolicy
            {
                ShouldRetry = true,
                MaxRetries = 5,
                RetryStrategy = RetryStrategy.Exponential,
                BaseRetryDelay = 1.0f,
                FallbackAction = FallbackAction.Continue,
                AllowUserIntervention = false
            });

            SetCategoryPolicy(CommandCategory.Audio, new CategoryRecoveryPolicy
            {
                ShouldRetry = true,
                MaxRetries = 2,
                RetryStrategy = RetryStrategy.Linear,
                BaseRetryDelay = 0.5f,
                FallbackAction = FallbackAction.Continue,
                AllowUserIntervention = false
            });

            SetCategoryPolicy(CommandCategory.FlowControl, new CategoryRecoveryPolicy
            {
                ShouldRetry = false,
                FallbackAction = FallbackAction.JumpToErrorHandler,
                AllowUserIntervention = true
            });

            SetCategoryPolicy(CommandCategory.StateManagement, new CategoryRecoveryPolicy
            {
                ShouldRetry = true,
                MaxRetries = 2,
                RetryStrategy = RetryStrategy.Linear,
                BaseRetryDelay = 1.0f,
                FallbackAction = FallbackAction.Rollback,
                AllowUserIntervention = true
            });
        }

        private void RecordRecoveryAttempt(RecoveryAttempt attempt)
        {
            lock (_historyLock)
            {
                _recoveryHistory.Add(attempt);
                
                // Limit history size
                if (_recoveryHistory.Count > 1000)
                {
                    _recoveryHistory.RemoveAt(0);
                }
            }
        }
        #endregion

        #region Nested Classes
        private class RecoveryAttempt
        {
            public ScriptExecutionError Error { get; set; }
            public DateTime Timestamp { get; set; }
            public string ScriptName { get; set; }
            public int LineIndex { get; set; }
            public RecoveryResult Result { get; set; }
            public string StrategyUsed { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// Interface for custom error recovery strategies
    /// </summary>
    public interface IErrorRecoveryStrategy
    {
        UniTask<RecoveryResult> AttemptRecoveryAsync(
            ScriptExecutionError error, 
            ScriptExecutionContext context, 
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Recovery policy configuration for command categories
    /// </summary>
    public class CategoryRecoveryPolicy
    {
        public bool ShouldRetry { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.Linear;
        public float BaseRetryDelay { get; set; } = 1.0f;
        public FallbackAction FallbackAction { get; set; } = FallbackAction.Continue;
        public bool AllowUserIntervention { get; set; } = false;

        public float CalculateRetryDelay(int attemptNumber)
        {
            return RetryStrategy switch
            {
                RetryStrategy.Immediate => 0f,
                RetryStrategy.Linear => BaseRetryDelay,
                RetryStrategy.Exponential => BaseRetryDelay * (float)Math.Pow(2, attemptNumber),
                RetryStrategy.Adaptive => BaseRetryDelay * (1 + attemptNumber * 0.5f),
                _ => BaseRetryDelay
            };
        }
    }

    /// <summary>
    /// Result of an error recovery attempt
    /// </summary>
    public class RecoveryResult
    {
        public bool Success { get; }
        public RecoveryAction Action { get; }
        public string Message { get; }
        public int? TargetLine { get; }
        public ScriptExecutionError OriginalError { get; }
        public CommandResult Result { get; }

        private RecoveryResult(bool success, RecoveryAction action, string message, ScriptExecutionError originalError, int? targetLine = null, CommandResult result = null)
        {
            Success = success;
            Action = action;
            Message = message;
            OriginalError = originalError;
            TargetLine = targetLine;
            Result = result ?? GenerateCommandResult(action, targetLine, message);
        }
        
        private static CommandResult GenerateCommandResult(RecoveryAction action, int? targetLine, string message)
        {
            return action switch
            {
                RecoveryAction.Retry => CommandResult.Success(),
                RecoveryAction.Skip => CommandResult.Success(),
                RecoveryAction.Jump when targetLine.HasValue => CommandResult.JumpToLine(targetLine.Value),
                RecoveryAction.Stop => CommandResult.Stop(message),
                RecoveryAction.Rollback => CommandResult.Success(), // Rollback handled elsewhere
                RecoveryAction.Prompt => CommandResult.Success(), // User intervention handled elsewhere
                _ => CommandResult.Success()
            };
        }

        public static RecoveryResult RetryCommand(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.Retry, message, error);

        public static RecoveryResult SkipAndContinue(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.Skip, message, error);

        public static RecoveryResult JumpToLine(ScriptExecutionError error, int lineIndex, string message)
            => new RecoveryResult(true, RecoveryAction.Jump, message, error, lineIndex);

        public static RecoveryResult StopExecution(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.Stop, message, error);

        public static RecoveryResult RollbackToCheckpoint(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.Rollback, message, error);

        public static RecoveryResult PromptUser(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.Prompt, message, error);

        public static RecoveryResult NoRecoveryNeeded(ScriptExecutionError error, string message)
            => new RecoveryResult(true, RecoveryAction.None, message, error);

        public static RecoveryResult RecoveryFailed(ScriptExecutionError error, string message)
            => new RecoveryResult(false, RecoveryAction.None, message, error);

        public override string ToString()
        {
            return $"Recovery {(Success ? "Success" : "Failed")}: {Action} - {Message}";
        }
    }

    /// <summary>
    /// Recovery actions that can be taken
    /// </summary>
    public enum RecoveryAction
    {
        None,
        Retry,
        Skip,
        Jump,
        Stop,
        Rollback,
        Prompt
    }

    /// <summary>
    /// Statistics for error recovery monitoring
    /// </summary>
    public class RecoveryStatistics
    {
        public int TotalAttempts { get; set; }
        public int SuccessfulRecoveries { get; set; }
        public float SuccessRate { get; set; }
        public Dictionary<ErrorCategory, int> RecoveriesByCategory { get; set; } = new Dictionary<ErrorCategory, int>();
        public Dictionary<Type, int> RecoveriesByCommandType { get; set; } = new Dictionary<Type, int>();

        public override string ToString()
        {
            return $"Recovery Stats: {SuccessfulRecoveries}/{TotalAttempts} ({SuccessRate:P1}) success rate";
        }
    }
}
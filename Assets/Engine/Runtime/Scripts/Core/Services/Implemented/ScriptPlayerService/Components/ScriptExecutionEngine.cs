using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services.Performance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Dedicated engine for executing script commands with enhanced error handling and performance tracking
    /// </summary>
    public class ScriptExecutionEngine : IDisposable
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly TimeoutManager _timeoutManager;
        private readonly ErrorRecoveryManager _errorRecoveryManager;
        private readonly ServicePerformanceMonitor _performanceMonitor;
        private readonly ScriptExecutionContext _executionContext;
        private readonly Queue<ICommand> _commandBatch = new Queue<ICommand>();
        private readonly object _batchLock = new object();
        private bool _disposed;
        #endregion

        #region Events
        public event Action<ICommand> CommandExecuting;
        public event Action<ICommand> CommandExecuted;
        public event Action<ICommand, Exception> CommandFailed;
        public event Action<IReadOnlyList<ICommand>, float> BatchExecuted;
        #endregion

        #region Properties
        public ICommand CurrentCommand { get; private set; }
        public int PendingBatchCount => _commandBatch.Count;
        public bool IsBatchingEnabled => _config.EnableCommandBatching;
        #endregion

        #region Constructor
        public ScriptExecutionEngine(
            ScriptPlayerConfiguration config,
            TimeoutManager timeoutManager,
            ErrorRecoveryManager errorRecoveryManager,
            ServicePerformanceMonitor performanceMonitor,
            ScriptExecutionContext executionContext)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
            _performanceMonitor = performanceMonitor;
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Execute a command with batching optimization, error handling, retry logic, and performance tracking
        /// </summary>
        public async UniTask<CommandResult> ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                return CommandResult.Success();

            var commandType = command.GetType();
            var metadata = CommandMetadataCache.GetMetadata(commandType);

            // Check if command is batchable
            if (IsBatchingEnabled && IsBatchableCommand(metadata))
            {
                return await ExecuteWithBatchingAsync(command, cancellationToken);
            }

            return await ExecuteSingleCommandAsync(command, cancellationToken);
        }

        /// <summary>
        /// Execute a single command with full error handling, retry logic, and performance tracking
        /// </summary>
        public async UniTask<CommandResult> ExecuteSingleCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                return CommandResult.Success();

            CurrentCommand = command;
            var commandType = command.GetType();
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            var retryCount = 0;
            CommandResult result = null;
            var startTime = DateTime.Now;

            try
            {
                // Fire command executing event
                CommandExecuting?.Invoke(command);

                if (_config.LogCommandExecution)
                {
                    Debug.Log($"[ExecutionEngine] Executing {metadata.Alias} command (timeout: {metadata.Timeout}s, retries: {metadata.MaxRetries})");
                }

                // Execute command with enhanced error handling
                while (retryCount <= metadata.MaxRetries)
                {
                    try
                    {
                        // Execute command with type-based timeout management
                        result = await _timeoutManager.ExecuteWithTimeoutAsync(
                            commandType,
                            async (token) =>
                            {
                                // Check if command supports CommandResult pattern
                                if (command is IFlowControlCommand flowCommand)
                                {
                                    return await flowCommand.ExecuteWithResultAsync(token);
                                }
                                else
                                {
                                    // Legacy command execution
                                    await command.ExecuteAsync(token);
                                    return CommandResult.Success();
                                }
                            },
                            cancellationToken);

                        // Success - record performance and metrics
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, true);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, false, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);

                        // Fire command executed event
                        CommandExecuted?.Invoke(command);
                        break; // Success, exit retry loop
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Main cancellation - don't retry, just propagate
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, false);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, true, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, false, ex);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, false, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);

                        // Create enhanced script execution error
                        var scriptError = CreateScriptExecutionError(ex, commandType, retryCount);
                        
                        // Check if we should retry
                        if (retryCount < metadata.MaxRetries && IsRetryableError(ex, metadata))
                        {
                            retryCount++;
                            if (_config.LogCommandExecution)
                            {
                                Debug.LogWarning($"[ExecutionEngine] Command {metadata.Alias} failed, retrying ({retryCount}/{metadata.MaxRetries}): {ex.Message}");
                            }
                            
                            // Apply retry delay
                            await ApplyRetryDelay(metadata.RetryDelay, metadata.RetryStrategy, retryCount, cancellationToken);
                            continue;
                        }

                        // Max retries reached - attempt recovery
                        var recoveryResult = await _errorRecoveryManager.AttemptRecoveryAsync(scriptError, _executionContext, cancellationToken);
                        
                        if (recoveryResult.Success)
                        {
                            result = recoveryResult.Result;
                            break;
                        }

                        // Recovery failed - fire error event and return failure
                        CommandFailed?.Invoke(command, ex);
                        return HandleUnrecoverableError(scriptError);
                    }
                }

                return result ?? CommandResult.Success();
            }
            finally
            {
                CurrentCommand = null;
            }
        }

        /// <summary>
        /// Execute multiple commands as a batch for performance optimization
        /// </summary>
        public async UniTask<CommandResult[]> ExecuteBatchAsync(IReadOnlyList<ICommand> commands, CancellationToken cancellationToken = default)
        {
            if (commands == null || commands.Count == 0)
                return new CommandResult[0];

            var startTime = DateTime.Now;
            var results = new CommandResult[commands.Count];
            var batchableCommands = new List<ICommand>();
            var nonBatchableIndexes = new List<int>();

            // Separate batchable from non-batchable commands
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                var metadata = CommandMetadataCache.GetMetadata(command.GetType());
                
                if (IsBatchableCommand(metadata))
                {
                    batchableCommands.Add(command);
                }
                else
                {
                    nonBatchableIndexes.Add(i);
                }
            }

            try
            {
                // Execute batchable commands in parallel
                if (batchableCommands.Count > 0)
                {
                    var batchTasks = batchableCommands.Select(cmd => ExecuteSingleCommandAsync(cmd, cancellationToken));
                    var batchResults = await UniTask.WhenAll(batchTasks);
                    
                    int batchIndex = 0;
                    for (int i = 0; i < commands.Count; i++)
                    {
                        if (!nonBatchableIndexes.Contains(i))
                        {
                            results[i] = batchResults[batchIndex++];
                        }
                    }
                }

                // Execute non-batchable commands sequentially
                foreach (int index in nonBatchableIndexes)
                {
                    results[index] = await ExecuteSingleCommandAsync(commands[index], cancellationToken);
                }

                // Record batch performance
                var executionTime = (float)(DateTime.Now - startTime).TotalSeconds;
                BatchExecuted?.Invoke(commands, executionTime);
                
                if (_config.LogCommandExecution)
                {
                    Debug.Log($"[ExecutionEngine] Batch executed {commands.Count} commands in {executionTime:F3}s (batchable: {batchableCommands.Count}, sequential: {nonBatchableIndexes.Count})");
                }

                return results;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExecutionEngine] Batch execution failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Flush pending batch and execute all queued commands
        /// </summary>
        public async UniTask<CommandResult[]> FlushBatchAsync(CancellationToken cancellationToken = default)
        {
            List<ICommand> batchToExecute;
            
            lock (_batchLock)
            {
                if (_commandBatch.Count == 0)
                    return new CommandResult[0];
                    
                batchToExecute = new List<ICommand>(_commandBatch);
                _commandBatch.Clear();
            }

            return await ExecuteBatchAsync(batchToExecute, cancellationToken);
        }
        #endregion

        #region Private Methods
        private async UniTask<CommandResult> ExecuteWithBatchingAsync(ICommand command, CancellationToken cancellationToken)
        {
            lock (_batchLock)
            {
                _commandBatch.Enqueue(command);
                
                // Check if we should flush the batch
                if (_commandBatch.Count >= _config.BatchSize)
                {
                    // Execute batch immediately if it's full
                    UniTask.Void(async () => await FlushBatchAsync(cancellationToken));
                }
            }

            // For now, execute immediately - could be enhanced with delayed batching
            return await ExecuteSingleCommandAsync(command, cancellationToken);
        }

        private bool IsBatchableCommand(CommandMetadata metadata)
        {
            // Commands are batchable if they:
            // 1. Are not critical
            // 2. Have short timeout (< 1 second)
            // 3. Don't change game state significantly
            // 4. Are marked as batchable in metadata
            return !metadata.Critical && 
                   metadata.Timeout < 1.0f && 
                   metadata.Category != CommandCategory.FlowControl &&
                   metadata.Category != CommandCategory.StateManagement;
        }
        private ScriptExecutionError CreateScriptExecutionError(Exception ex, Type commandType, int retryCount)
        {
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            
            return new ScriptExecutionError(
                $"Command execution failed: {ex.Message}",
                DetermineErrorSeverity(ex, metadata),
                DetermineErrorCategory(ex),
                commandType,
                _executionContext.CurrentLineIndex,
                _executionContext.Script?.Name,
                isRetryable: retryCount < metadata.MaxRetries)
                .WithContext("RetryCount", retryCount)
                .WithContext("MaxRetries", metadata.MaxRetries)
                .WithContext("CommandAlias", metadata.Alias)
                .WithContext("CommandCategory", metadata.Category.ToString())
                .WithException(ex);
        }

        private bool IsRetryableError(Exception ex, CommandMetadata metadata)
        {
            return ex switch
            {
                OperationCanceledException => false, // Don't retry cancellations
                ArgumentException or ArgumentNullException => false, // Parameter errors won't fix themselves
                _ => true // Most other errors are potentially retryable
            };
        }

        private async UniTask ApplyRetryDelay(float baseDelay, RetryStrategy strategy, int retryCount, CancellationToken cancellationToken)
        {
            var delay = strategy switch
            {
                RetryStrategy.Linear => baseDelay,
                RetryStrategy.Exponential => baseDelay * (float)Math.Pow(2, retryCount - 1),
                RetryStrategy.Adaptive => CalculateAdaptiveDelay(baseDelay, retryCount),
                RetryStrategy.Immediate => 0f,
                _ => baseDelay
            };

            if (delay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }
        }

        private float CalculateAdaptiveDelay(float baseDelay, int retryCount)
        {
            // Simple adaptive strategy - could be enhanced with performance data
            return baseDelay * (1 + (retryCount * 0.5f));
        }

        private ErrorSeverity DetermineErrorSeverity(Exception ex, CommandMetadata metadata)
        {
            if (metadata.Critical)
                return ErrorSeverity.Critical;

            return ex switch
            {
                OperationCanceledException => ErrorSeverity.Recoverable,
                ArgumentException or ArgumentNullException => ErrorSeverity.Critical,
                InvalidOperationException => ErrorSeverity.Critical,
                UnauthorizedAccessException => ErrorSeverity.Critical,
                System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException => ErrorSeverity.Critical,
                HttpRequestException => ErrorSeverity.Recoverable,
                NotImplementedException => ErrorSeverity.Fatal,
                OutOfMemoryException => ErrorSeverity.Fatal,
                _ => ErrorSeverity.Critical
            };
        }

        private ErrorCategory DetermineErrorCategory(Exception ex)
        {
            return ex switch
            {
                OperationCanceledException => ErrorCategory.Timeout,
                ArgumentException or ArgumentNullException => ErrorCategory.Validation,
                InvalidOperationException => ErrorCategory.StateManagement,
                UnauthorizedAccessException => ErrorCategory.Security,
                System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException => ErrorCategory.ResourceLoading,
                HttpRequestException => ErrorCategory.Network,
                NotImplementedException => ErrorCategory.Configuration,
                OutOfMemoryException => ErrorCategory.Unknown,
                _ => ErrorCategory.Unknown
            };
        }

        private CommandResult HandleUnrecoverableError(ScriptExecutionError error)
        {
            if (_config.LogCommandExecution)
            {
                Debug.LogError($"[ExecutionEngine] Unrecoverable error: {error.Message}");
            }

            return error.Severity == ErrorSeverity.Fatal 
                ? CommandResult.Stop($"Fatal error: {error.Message}")
                : CommandResult.Success(); // Continue execution for non-fatal errors
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the ScriptExecutionEngine and clean up all resources and events
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Clear command batch
                lock (_batchLock)
                {
                    _commandBatch.Clear();
                }

                // Clear event handlers to prevent memory leaks - simple null assignment
                CommandExecuting = null;
                CommandExecuted = null;
                CommandFailed = null;
                BatchExecuted = null;

                // Clear current command reference
                CurrentCommand = null;

                if (_config.LogCommandExecution)
                    Debug.Log("[ScriptExecutionEngine] Disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptExecutionEngine] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
        #endregion
    }
}
using Sinkii09.Engine.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// High-performance pooling system for CommandResult objects to reduce GC pressure
    /// </summary>
    public class CommandResultPool : IDisposable
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly ConcurrentQueue<PooledCommandResult> _successPool = new ConcurrentQueue<PooledCommandResult>();
        private readonly ConcurrentQueue<PooledCommandResult> _failurePool = new ConcurrentQueue<PooledCommandResult>();
        private readonly ConcurrentQueue<PooledCommandResult> _waitPool = new ConcurrentQueue<PooledCommandResult>();
        private readonly ConcurrentQueue<PooledCommandResult> _stopPool = new ConcurrentQueue<PooledCommandResult>();

        private readonly object _statsLock = new object();
        private int _totalCreated;
        private int _totalReused;
        private int _currentPooled;
        private bool _disposed;
        
        private const int MAX_POOL_SIZE_PER_TYPE = 50; // Prevent memory leaks
        private const int INITIAL_POOL_SIZE = 10;
        #endregion

        #region Properties
        public int TotalSuccessPooled => _successPool.Count;
        public int TotalFailurePooled => _failurePool.Count;
        public int TotalWaitPooled => _waitPool.Count;
        public int TotalStopPooled => _stopPool.Count;
        public int TotalPooled => TotalSuccessPooled + TotalFailurePooled + TotalWaitPooled + TotalStopPooled;
        public float ReuseRatio => _totalCreated > 0 ? (float)_totalReused / _totalCreated : 0f;
        #endregion

        #region Constructor
        public CommandResultPool(ScriptPlayerConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PrewarmPools();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get a success CommandResult from the pool
        /// </summary>
        public CommandResult GetSuccess(object returnValue = null)
        {
            if (_disposed) return CommandResult.Success(returnValue);
            
            if (_successPool.TryDequeue(out var pooledResult))
            {
                Interlocked.Increment(ref _totalReused);
                Interlocked.Decrement(ref _currentPooled);
                
                // Reset and configure the pooled result
                pooledResult.ResetAsSuccess(returnValue);
                return pooledResult;
            }

            // Create new instance if pool is empty
            Interlocked.Increment(ref _totalCreated);
            var newResult = new PooledCommandResult(this);
            newResult.ResetAsSuccess(returnValue);
            return newResult;
        }

        /// <summary>
        /// Get a failure CommandResult from the pool
        /// </summary>
        public CommandResult GetFailure(string errorMessage, Exception exception = null)
        {
            if (_disposed) return CommandResult.Failed(errorMessage, exception);
            
            if (_failurePool.TryDequeue(out var pooledResult))
            {
                Interlocked.Increment(ref _totalReused);
                Interlocked.Decrement(ref _currentPooled);
                
                pooledResult.ResetAsFailure(errorMessage, exception);
                return pooledResult;
            }

            Interlocked.Increment(ref _totalCreated);
            var newResult = new PooledCommandResult(this);
            newResult.ResetAsFailure(errorMessage, exception);
            return newResult;
        }

        /// <summary>
        /// Get a stop CommandResult from the pool
        /// </summary>
        public CommandResult GetStop(string reason = "Command requested stop")
        {
            if (_disposed) return CommandResult.Stop(reason);
            
            if (_stopPool.TryDequeue(out var pooledResult))
            {
                Interlocked.Increment(ref _totalReused);
                Interlocked.Decrement(ref _currentPooled);
                
                pooledResult.ResetAsStop(reason);
                return pooledResult;
            }

            Interlocked.Increment(ref _totalCreated);
            var newResult = new PooledCommandResult(this);
            newResult.ResetAsStop(reason);
            return newResult;
        }

        /// <summary>
        /// Get a jump to line CommandResult from the pool
        /// </summary>
        public CommandResult GetJumpToLine(int lineIndex)
        {
            if (_disposed) return CommandResult.JumpToLine(lineIndex);
            
            if (_waitPool.TryDequeue(out var pooledResult)) // Reuse wait pool for jumps
            {
                Interlocked.Increment(ref _totalReused);
                Interlocked.Decrement(ref _currentPooled);
                
                pooledResult.ResetAsJumpToLine(lineIndex);
                return pooledResult;
            }

            Interlocked.Increment(ref _totalCreated);
            var newResult = new PooledCommandResult(this);
            newResult.ResetAsJumpToLine(lineIndex);
            return newResult;
        }

        /// <summary>
        /// Return a CommandResult to the appropriate pool
        /// </summary>
        internal void Return(PooledCommandResult result)
        {
            if (result == null || _disposed) return;

            // Clear sensitive data before returning to pool
            result.ClearForReuse();

            var targetPool = result.PoolType switch
            {
                PooledResultType.Success => _successPool,
                PooledResultType.Failure => _failurePool,
                PooledResultType.Wait => _waitPool,
                PooledResultType.Stop => _stopPool,
                _ => _successPool
            };

            // Only return to pool if it's not full (prevent memory leaks)
            if (targetPool.Count < MAX_POOL_SIZE_PER_TYPE)
            {
                targetPool.Enqueue(result);
                Interlocked.Increment(ref _currentPooled);
            }
        }

        /// <summary>
        /// Get pooling statistics for monitoring
        /// </summary>
        public CommandResultPoolStats GetStats()
        {
            lock (_statsLock)
            {
                return new CommandResultPoolStats
                {
                    TotalCreated = _totalCreated,
                    TotalReused = _totalReused,
                    CurrentPooled = _currentPooled,
                    SuccessPooled = TotalSuccessPooled,
                    FailurePooled = TotalFailurePooled,
                    WaitPooled = TotalWaitPooled,
                    StopPooled = TotalStopPooled,
                    ReuseRatio = ReuseRatio
                };
            }
        }

        /// <summary>
        /// Clear all pools and reset statistics
        /// </summary>
        public void Clear()
        {
            ClearPool(_successPool);
            ClearPool(_failurePool);
            ClearPool(_waitPool);
            ClearPool(_stopPool);

            lock (_statsLock)
            {
                _totalCreated = 0;
                _totalReused = 0;
                _currentPooled = 0;
            }

            if (_config.LogExecutionFlow)
                Debug.Log("[CommandResultPool] All pools cleared and statistics reset");
        }

        /// <summary>
        /// Trim pools to optimal size to free memory
        /// </summary>
        public void TrimPools()
        {
            TrimPool(_successPool, INITIAL_POOL_SIZE);
            TrimPool(_failurePool, INITIAL_POOL_SIZE);
            TrimPool(_waitPool, INITIAL_POOL_SIZE);
            TrimPool(_stopPool, INITIAL_POOL_SIZE);

            if (_config.LogExecutionFlow)
            {
                var stats = GetStats();
                Debug.Log($"[CommandResultPool] Pools trimmed - Current pooled: {stats.CurrentPooled}");
            }
        }
        #endregion

        #region Private Methods
        private void PrewarmPools()
        {
            // Pre-create some instances to avoid allocations during gameplay
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                var successResult = new PooledCommandResult(this);
                successResult.ResetAsSuccess();
                _successPool.Enqueue(successResult);

                var failureResult = new PooledCommandResult(this);
                failureResult.ResetAsFailure("Pooled instance");
                _failurePool.Enqueue(failureResult);

                var waitResult = new PooledCommandResult(this);
                waitResult.ResetAsJumpToLine(0); // Will be reset when used
                _waitPool.Enqueue(waitResult);

                var stopResult = new PooledCommandResult(this);
                stopResult.ResetAsStop();
                _stopPool.Enqueue(stopResult);
            }

            _currentPooled = INITIAL_POOL_SIZE * 4;
            _totalCreated = INITIAL_POOL_SIZE * 4;

            if (_config.LogExecutionFlow)
                Debug.Log($"[CommandResultPool] Prewarmed pools with {INITIAL_POOL_SIZE} instances each");
        }

        private void ClearPool(ConcurrentQueue<PooledCommandResult> pool)
        {
            while (pool.TryDequeue(out var result))
            {
                result?.Dispose();
                Interlocked.Decrement(ref _currentPooled);
            }
        }

        private void TrimPool(ConcurrentQueue<PooledCommandResult> pool, int targetSize)
        {
            var currentSize = pool.Count;
            var itemsToRemove = Math.Max(0, currentSize - targetSize);

            for (int i = 0; i < itemsToRemove; i++)
            {
                if (pool.TryDequeue(out var result))
                {
                    result?.Dispose();
                    Interlocked.Decrement(ref _currentPooled);
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the CommandResultPool and clean up all pooled objects
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Dispose all pooled results in all queues
                ClearAndDisposePool(_successPool);
                ClearAndDisposePool(_failurePool);
                ClearAndDisposePool(_waitPool);
                ClearAndDisposePool(_stopPool);

                // Reset statistics
                lock (_statsLock)
                {
                    _totalCreated = 0;
                    _totalReused = 0;
                    _currentPooled = 0;
                }

                if (_config.LogExecutionFlow)
                    Debug.Log("[CommandResultPool] Disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandResultPool] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }

        private void ClearAndDisposePool(ConcurrentQueue<PooledCommandResult> pool)
        {
            while (pool.TryDequeue(out var result))
            {
                try
                {
                    result?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CommandResultPool] Error disposing pooled result: {ex.Message}");
                }
                Interlocked.Decrement(ref _currentPooled);
            }
        }
        #endregion
    }

    /// <summary>
    /// Pooled version of CommandResult that automatically returns to pool when disposed
    /// </summary>
    internal class PooledCommandResult : CommandResult, IDisposable
    {
        #region Private Fields
        private readonly CommandResultPool _pool;
        private bool _isDisposed;
        private PooledResultType _poolType;
        #endregion

        #region Constructor
        internal PooledCommandResult(CommandResultPool pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _isDisposed = false;
            _poolType = PooledResultType.Success;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Reset this result as a success
        /// </summary>
        internal void ResetAsSuccess(object returnValue = null)
        {
            IsSuccess = true;
            ErrorMessage = null;
            Exception = null;
            FlowAction = FlowControlAction.Continue;
            ReturnValue = returnValue;
            TargetLineIndex = -1;
            TargetLabel = null;
            SkipInFastForward = false;
            ExecutionTimeMs = 0f;
            _poolType = PooledResultType.Success;
            _isDisposed = false;
        }

        /// <summary>
        /// Reset this result as a failure
        /// </summary>
        internal void ResetAsFailure(string errorMessage, Exception exception = null)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            Exception = exception;
            FlowAction = FlowControlAction.Continue;
            ReturnValue = null;
            TargetLineIndex = -1;
            TargetLabel = null;
            SkipInFastForward = false;
            ExecutionTimeMs = 0f;
            _poolType = PooledResultType.Failure;
            _isDisposed = false;
        }

        /// <summary>
        /// Reset this result as a stop command
        /// </summary>
        internal void ResetAsStop(string reason = "Command requested stop")
        {
            IsSuccess = true;
            ErrorMessage = reason;
            Exception = null;
            FlowAction = FlowControlAction.Stop;
            ReturnValue = null;
            TargetLineIndex = -1;
            TargetLabel = null;
            SkipInFastForward = false;
            ExecutionTimeMs = 0f;
            _poolType = PooledResultType.Stop;
            _isDisposed = false;
        }

        /// <summary>
        /// Reset this result as a jump to line command
        /// </summary>
        internal void ResetAsJumpToLine(int lineIndex)
        {
            IsSuccess = true;
            ErrorMessage = null;
            Exception = null;
            FlowAction = FlowControlAction.JumpToLine;
            ReturnValue = null;
            TargetLineIndex = lineIndex;
            TargetLabel = null;
            SkipInFastForward = false;
            ExecutionTimeMs = 0f;
            _poolType = PooledResultType.Wait; // Use wait pool type for storage
            _isDisposed = false;
        }

        /// <summary>
        /// Clear sensitive data before returning to pool
        /// </summary>
        internal void ClearForReuse()
        {
            ErrorMessage = null;
            Exception = null;
            ReturnValue = null;
            TargetLabel = null;
            ExecutionTimeMs = 0f;
        }

        /// <summary>
        /// Get the pool type for this result
        /// </summary>
        internal PooledResultType PoolType => _poolType;

        public void Dispose()
        {
            if (!_isDisposed && _pool != null)
            {
                _pool.Return(this);
                _isDisposed = true;
            }
        }
        #endregion
    }

    /// <summary>
    /// Types of pooled results for categorization
    /// </summary>
    internal enum PooledResultType
    {
        Success,
        Failure,
        Wait,
        Stop
    }

    /// <summary>
    /// Statistics for CommandResult pooling performance
    /// </summary>
    public struct CommandResultPoolStats
    {
        public int TotalCreated;
        public int TotalReused;
        public int CurrentPooled;
        public int SuccessPooled;
        public int FailurePooled;
        public int WaitPooled;
        public int StopPooled;
        public float ReuseRatio;

        public override string ToString()
        {
            return $"Pool Stats - Created: {TotalCreated}, Reused: {TotalReused}, " +
                   $"Pooled: {CurrentPooled} (S:{SuccessPooled} F:{FailurePooled} W:{WaitPooled} St:{StopPooled}), " +
                   $"Reuse: {ReuseRatio:P1}";
        }
    }
}
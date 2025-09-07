using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Result of script execution containing success status, metrics, and error information
    /// </summary>
    public class ScriptExecutionResult
    {
        /// <summary>
        /// Whether the execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Final state after execution
        /// </summary>
        public PlaybackState FinalState { get; set; }

        /// <summary>
        /// Total lines executed
        /// </summary>
        public int LinesExecuted { get; set; }

        /// <summary>
        /// Total execution time in seconds
        /// </summary>
        public float ExecutionTime { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Exception if execution failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Line index where error occurred
        /// </summary>
        public int ErrorLineIndex { get; set; } = -1;

        /// <summary>
        /// Number of commands executed
        /// </summary>
        public int CommandsExecuted { get; set; }

        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Whether execution was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static ScriptExecutionResult CreateSuccess(int linesExecuted, float executionTime, int commandsExecuted = 0)
        {
            return new ScriptExecutionResult
            {
                Success = true,
                FinalState = PlaybackState.Completed,
                LinesExecuted = linesExecuted,
                ExecutionTime = executionTime,
                CommandsExecuted = commandsExecuted,
                WasCancelled = false
            };
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static ScriptExecutionResult CreateFailure(string errorMessage, Exception exception = null, int errorLineIndex = -1)
        {
            return new ScriptExecutionResult
            {
                Success = false,
                FinalState = PlaybackState.Failed,
                ErrorMessage = errorMessage,
                Exception = exception,
                ErrorLineIndex = errorLineIndex,
                ErrorCount = 1,
                WasCancelled = false
            };
        }

        /// <summary>
        /// Create a stopped result
        /// </summary>
        public static ScriptExecutionResult CreateStopped(int linesExecuted, float executionTime, int commandsExecuted = 0)
        {
            return new ScriptExecutionResult
            {
                Success = true,
                FinalState = PlaybackState.Stopped,
                LinesExecuted = linesExecuted,
                ExecutionTime = executionTime,
                CommandsExecuted = commandsExecuted,
                WasCancelled = false
            };
        }

        /// <summary>
        /// Create a cancelled result
        /// </summary>
        public static ScriptExecutionResult CreateCancelled(int linesExecuted, float executionTime, int commandsExecuted = 0)
        {
            return new ScriptExecutionResult
            {
                Success = false,
                FinalState = PlaybackState.Stopped,
                LinesExecuted = linesExecuted,
                ExecutionTime = executionTime,
                CommandsExecuted = commandsExecuted,
                WasCancelled = true,
                ErrorMessage = "Execution was cancelled"
            };
        }

        /// <summary>
        /// Get a summary of the execution result
        /// </summary>
        public string GetSummary()
        {
            if (Success)
            {
                return $"Script execution {FinalState.ToString().ToLower()}: {LinesExecuted} lines, {CommandsExecuted} commands in {ExecutionTime:F2}s";
            }
            else
            {
                var cancelledText = WasCancelled ? " (cancelled)" : "";
                return $"Script execution failed{cancelledText}: {ErrorMessage} at line {ErrorLineIndex}";
            }
        }
    }
}
using System;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Represents the result of a command execution with flow control capabilities
    /// </summary>
    [Serializable]
    public class CommandResult
    {
        /// <summary>
        /// Whether the command executed successfully
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Exception that occurred during execution
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Flow control action to take after this command
        /// </summary>
        public FlowControlAction FlowAction { get; set; } = FlowControlAction.Continue;

        /// <summary>
        /// Target line index for jump operations (-1 if not applicable)
        /// </summary>
        public int TargetLineIndex { get; set; } = -1;

        /// <summary>
        /// Target label name for jump operations
        /// </summary>
        public string TargetLabel { get; set; }

        /// <summary>
        /// Return value from the command (optional)
        /// </summary>
        public object ReturnValue { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public float ExecutionTimeMs { get; set; }

        /// <summary>
        /// Whether this command should be skipped in fast forward mode
        /// </summary>
        public bool SkipInFastForward { get; set; } = false;

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static CommandResult Success(object returnValue = null)
        {
            return new CommandResult
            {
                IsSuccess = true,
                ReturnValue = returnValue,
                FlowAction = FlowControlAction.Continue
            };
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static CommandResult Failed(string errorMessage, Exception exception = null)
        {
            return new CommandResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                FlowAction = FlowControlAction.Continue
            };
        }

        /// <summary>
        /// Create a result that jumps to a specific line
        /// </summary>
        public static CommandResult JumpToLine(int lineIndex)
        {
            return new CommandResult
            {
                IsSuccess = true,
                FlowAction = FlowControlAction.JumpToLine,
                TargetLineIndex = lineIndex
            };
        }

        /// <summary>
        /// Create a result that jumps to a label
        /// </summary>
        public static CommandResult JumpToLabel(string labelName)
        {
            return new CommandResult
            {
                IsSuccess = true,
                FlowAction = FlowControlAction.JumpToLabel,
                TargetLabel = labelName
            };
        }

        /// <summary>
        /// Create a result that stops script execution
        /// </summary>
        public static CommandResult Stop(string reason = "Command requested stop")
        {
            return new CommandResult
            {
                IsSuccess = true,
                FlowAction = FlowControlAction.Stop,
                ErrorMessage = reason
            };
        }

        /// <summary>
        /// Create a result that returns from current script/subroutine
        /// </summary>
        public static CommandResult Return(object returnValue = null)
        {
            return new CommandResult
            {
                IsSuccess = true,
                FlowAction = FlowControlAction.Return,
                ReturnValue = returnValue
            };
        }

        /// <summary>
        /// Create a result that calls another script
        /// </summary>
        public static CommandResult CallScript(string scriptName)
        {
            return new CommandResult
            {
                IsSuccess = true,
                FlowAction = FlowControlAction.CallScript,
                TargetLabel = scriptName
            };
        }

        /// <summary>
        /// Get a description of this result
        /// </summary>
        public string GetDescription()
        {
            if (!IsSuccess)
            {
                return $"Failed: {ErrorMessage}";
            }

            return FlowAction switch
            {
                FlowControlAction.Continue => "Continue",
                FlowControlAction.JumpToLine => $"Jump to line {TargetLineIndex}",
                FlowControlAction.JumpToLabel => $"Jump to label '{TargetLabel}'",
                FlowControlAction.Stop => $"Stop: {ErrorMessage}",
                FlowControlAction.Return => $"Return: {ReturnValue}",
                FlowControlAction.CallScript => $"Call script '{TargetLabel}'",
                _ => "Unknown action"
            };
        }
    }

    /// <summary>
    /// Types of flow control actions that can be requested by commands
    /// </summary>
    public enum FlowControlAction
    {
        /// <summary>
        /// Continue to the next line normally
        /// </summary>
        Continue,

        /// <summary>
        /// Jump to a specific line index
        /// </summary>
        JumpToLine,

        /// <summary>
        /// Jump to a labeled line
        /// </summary>
        JumpToLabel,

        /// <summary>
        /// Stop script execution
        /// </summary>
        Stop,

        /// <summary>
        /// Return from current script/subroutine
        /// </summary>
        Return,

        /// <summary>
        /// Call another script
        /// </summary>
        CallScript
    }
}
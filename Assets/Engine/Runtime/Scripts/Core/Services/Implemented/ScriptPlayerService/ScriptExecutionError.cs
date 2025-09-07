using System;
using System.Collections.Generic;
using System.Text;
using Sinkii09.Engine.Commands;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Enhanced script execution error with comprehensive context and type-based design.
    /// Uses command types instead of strings for better performance and type safety.
    /// </summary>
    public class ScriptExecutionError : Exception
    {
        #region Properties
        public ErrorSeverity Severity { get; }
        public ErrorCategory Category { get; }
        public Type CommandType { get; }
        public string CommandAlias { get; }
        public int LineIndex { get; }
        public string ScriptName { get; }
        public Dictionary<string, object> Context { get; }
        public bool IsRetryable { get; }
        public int RetryAttempt { get; }
        public DateTime Timestamp { get; }
        public string RecoveryAction { get; }
        public Exception OriginalException { get; }
        public CommandMetadata CommandMetadata { get; }
        #endregion

        #region Constructors
        public ScriptExecutionError(
            string message,
            ErrorSeverity severity,
            ErrorCategory category,
            Type commandType = null,
            int lineIndex = -1,
            string scriptName = null,
            bool isRetryable = false,
            string recoveryAction = null,
            Exception innerException = null) 
            : base(message, innerException)
        {
            Severity = severity;
            Category = category;
            CommandType = commandType;
            CommandAlias = commandType != null ? CommandMetadataCache.GetMetadata(commandType).Alias : null;
            LineIndex = lineIndex;
            ScriptName = scriptName;
            Context = new Dictionary<string, object>();
            IsRetryable = isRetryable;
            RetryAttempt = 0;
            Timestamp = DateTime.Now;
            RecoveryAction = recoveryAction;
            OriginalException = innerException ?? this;
            CommandMetadata = commandType != null ? CommandMetadataCache.GetMetadata(commandType) : null;
        }

        private ScriptExecutionError(ScriptExecutionError original, int retryAttempt) 
            : base(original.Message, original.InnerException)
        {
            Severity = original.Severity;
            Category = original.Category;
            CommandType = original.CommandType;
            CommandAlias = original.CommandAlias;
            LineIndex = original.LineIndex;
            ScriptName = original.ScriptName;
            Context = new Dictionary<string, object>(original.Context);
            IsRetryable = original.IsRetryable;
            RetryAttempt = retryAttempt;
            Timestamp = DateTime.Now;
            RecoveryAction = original.RecoveryAction;
            OriginalException = original.OriginalException;
            CommandMetadata = original.CommandMetadata;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new error instance with incremented retry count
        /// </summary>
        public ScriptExecutionError WithRetryAttempt(int retryAttempt)
        {
            return new ScriptExecutionError(this, retryAttempt);
        }

        /// <summary>
        /// Adds context information to the error
        /// </summary>
        public ScriptExecutionError WithContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }

        /// <summary>
        /// Adds the original exception as context information
        /// </summary>
        public ScriptExecutionError WithException(Exception exception)
        {
            Context["OriginalException"] = exception;
            Context["ExceptionType"] = exception?.GetType().Name;
            Context["ExceptionMessage"] = exception?.Message;
            return this;
        }

        /// <summary>
        /// Gets formatted error message with all context information
        /// </summary>
        public string GetDetailedMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Script Execution Error: {Message}");
            
            if (!string.IsNullOrEmpty(ScriptName))
                sb.AppendLine($"  Script: {ScriptName}");
            
            if (LineIndex >= 0)
                sb.AppendLine($"  Line: {LineIndex}");
            
            if (CommandType != null)
            {
                sb.AppendLine($"  Command: {CommandType.Name}");
                if (!string.IsNullOrEmpty(CommandAlias))
                    sb.AppendLine($"  Alias: @{CommandAlias}");
            }
            
            if (RetryAttempt > 0)
                sb.AppendLine($"  Retry Attempt: {RetryAttempt}");
            
            sb.AppendLine($"  Category: {Category}");
            sb.AppendLine($"  Severity: {Severity}");
            sb.AppendLine($"  Retryable: {IsRetryable}");
            sb.AppendLine($"  Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            
            if (!string.IsNullOrEmpty(RecoveryAction))
                sb.AppendLine($"  Recovery: {RecoveryAction}");
            
            if (CommandMetadata != null)
            {
                sb.AppendLine($"  Command Config: Timeout={CommandMetadata.Timeout}s, MaxRetries={CommandMetadata.MaxRetries}");
            }
            
            if (Context.Count > 0)
            {
                sb.AppendLine("  Context:");
                foreach (var kvp in Context)
                {
                    sb.AppendLine($"    {kvp.Key}: {kvp.Value}");
                }
            }
            
            if (OriginalException != null && OriginalException != this)
            {
                sb.AppendLine($"  Original Error: {OriginalException.Message}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Get concise error summary for logging
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string> { Message };
            
            if (!string.IsNullOrEmpty(ScriptName))
                parts.Add($"Script:{ScriptName}");
            
            if (LineIndex >= 0)
                parts.Add($"Line:{LineIndex}");
            
            if (!string.IsNullOrEmpty(CommandAlias))
                parts.Add($"Cmd:@{CommandAlias}");
            
            if (RetryAttempt > 0)
                parts.Add($"Retry:{RetryAttempt}");
            
            parts.Add($"{Severity}");
            
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Check if this error should trigger a specific recovery action
        /// </summary>
        public bool ShouldTriggerRecovery()
        {
            return CommandMetadata?.Fallback switch
            {
                FallbackAction.Stop => Severity >= ErrorSeverity.Critical,
                FallbackAction.Prompt => CommandMetadata.AllowUserIntervention && Severity >= ErrorSeverity.Recoverable,
                FallbackAction.JumpToErrorHandler => Severity >= ErrorSeverity.Recoverable,
                FallbackAction.Rollback => Severity >= ErrorSeverity.Critical,
                _ => false
            };
        }
        #endregion

        #region Static Factory Methods
        public static ScriptExecutionError ValidationError<TCommand>(string message, int lineIndex, string scriptName)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                message, 
                ErrorSeverity.Critical, 
                ErrorCategory.Validation, 
                typeof(TCommand), 
                lineIndex, 
                scriptName,
                recoveryAction: "Fix script syntax or parameter validation");
        }

        public static ScriptExecutionError TimeoutError<TCommand>(int lineIndex, string scriptName, float timeoutSeconds)
            where TCommand : ICommand
        {
            var metadata = CommandMetadataCache.GetMetadata<TCommand>();
            return new ScriptExecutionError(
                $"Command '{metadata.Alias}' timed out after {timeoutSeconds:F1}s",
                ErrorSeverity.Recoverable,
                ErrorCategory.Timeout,
                typeof(TCommand),
                lineIndex,
                scriptName,
                isRetryable: true,
                recoveryAction: "Increase timeout or check command parameters")
                .WithContext("TimeoutSeconds", timeoutSeconds)
                .WithContext("ExpectedDuration", metadata.ExpectedDuration);
        }

        public static ScriptExecutionError ResourceLoadingError<TCommand>(string resourcePath, int lineIndex, string scriptName, Exception innerException)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                $"Failed to load resource: {resourcePath}",
                ErrorSeverity.Critical,
                ErrorCategory.ResourceLoading,
                typeof(TCommand),
                lineIndex,
                scriptName,
                isRetryable: true,
                recoveryAction: "Check resource path and availability",
                innerException: innerException)
                .WithContext("ResourcePath", resourcePath);
        }

        public static ScriptExecutionError ServiceDependencyError<TCommand>(string serviceName, string scriptName, Exception innerException)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                $"Required service '{serviceName}' is unavailable",
                ErrorSeverity.Fatal,
                ErrorCategory.ServiceDependency,
                typeof(TCommand),
                scriptName: scriptName,
                recoveryAction: "Initialize required service dependencies",
                innerException: innerException)
                .WithContext("ServiceName", serviceName);
        }

        public static ScriptExecutionError ParameterError<TCommand>(string parameterName, object value, string validationMessage, int lineIndex, string scriptName)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                $"Invalid parameter '{parameterName}': {validationMessage}",
                ErrorSeverity.Critical,
                ErrorCategory.Validation,
                typeof(TCommand),
                lineIndex,
                scriptName,
                recoveryAction: "Check parameter format and valid values")
                .WithContext("ParameterName", parameterName)
                .WithContext("ParameterValue", value)
                .WithContext("ValidationMessage", validationMessage);
        }

        public static ScriptExecutionError FlowControlError<TCommand>(string message, int lineIndex, string scriptName)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                message,
                ErrorSeverity.Critical,
                ErrorCategory.FlowControl,
                typeof(TCommand),
                lineIndex,
                scriptName,
                recoveryAction: "Check script flow control logic and labels");
        }

        public static ScriptExecutionError NetworkError<TCommand>(string endpoint, Exception innerException, int lineIndex, string scriptName)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                $"Network request to '{endpoint}' failed",
                ErrorSeverity.Recoverable,
                ErrorCategory.Network,
                typeof(TCommand),
                lineIndex,
                scriptName,
                isRetryable: true,
                recoveryAction: "Check network connectivity and endpoint availability",
                innerException: innerException)
                .WithContext("Endpoint", endpoint);
        }

        public static ScriptExecutionError PermissionError<TCommand>(string operation, int lineIndex, string scriptName)
            where TCommand : ICommand
        {
            return new ScriptExecutionError(
                $"Permission denied for operation: {operation}",
                ErrorSeverity.Critical,
                ErrorCategory.Security,
                typeof(TCommand),
                lineIndex,
                scriptName,
                recoveryAction: "Check file permissions and security settings")
                .WithContext("Operation", operation);
        }
        #endregion
    }

    #region Supporting Enums
    /// <summary>
    /// Error severity levels for appropriate response handling
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>Informational warning, no action needed</summary>
        Warning = 0,
        /// <summary>Error that can be automatically recovered</summary>
        Recoverable = 1,
        /// <summary>Error that stops current operation but allows script to continue</summary>
        Critical = 2,
        /// <summary>Fatal error requiring immediate script termination</summary>
        Fatal = 3
    }

    /// <summary>
    /// Error categories for targeted handling strategies
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>Parameter or syntax validation errors</summary>
        Validation,
        /// <summary>Command parsing and syntax errors</summary>
        Parsing,
        /// <summary>Operation timeout errors</summary>
        Timeout,
        /// <summary>Resource loading and file access errors</summary>
        ResourceLoading,
        /// <summary>Service dependency and initialization errors</summary>
        ServiceDependency,
        /// <summary>Script flow control errors (goto, labels, etc.)</summary>
        FlowControl,
        /// <summary>State management and persistence errors</summary>
        StateManagement,
        /// <summary>Configuration and settings errors</summary>
        Configuration,
        /// <summary>Network communication errors</summary>
        Network,
        /// <summary>Security and permission errors</summary>
        Security,
        /// <summary>External system integration errors</summary>
        ExternalSystem,
        /// <summary>Unknown or unclassified errors</summary>
        Unknown
    }
    #endregion
}
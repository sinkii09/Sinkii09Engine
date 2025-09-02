using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Represents the result of an input rebinding operation
    /// </summary>
    public enum RebindingStatus
    {
        /// <summary>
        /// Rebinding completed successfully
        /// </summary>
        Success = 0,
        
        /// <summary>
        /// Rebinding was cancelled by user or system
        /// </summary>
        Cancelled = 1,
        
        /// <summary>
        /// Rebinding failed due to a conflict with existing binding
        /// </summary>
        Conflict = 2,
        
        /// <summary>
        /// Rebinding failed due to invalid input (system keys, etc.)
        /// </summary>
        InvalidInput = 3,
        
        /// <summary>
        /// Rebinding failed due to timeout
        /// </summary>
        Timeout = 4,
        
        /// <summary>
        /// Rebinding failed due to an error
        /// </summary>
        Error = 5
    }
    
    /// <summary>
    /// Contains detailed information about a rebinding operation result
    /// </summary>
    public class RebindingResult
    {
        #region Properties
        
        /// <summary>
        /// The status of the rebinding operation
        /// </summary>
        public RebindingStatus Status { get; }
        
        /// <summary>
        /// Whether the rebinding operation was successful
        /// </summary>
        public bool IsSuccess => Status == RebindingStatus.Success;
        
        /// <summary>
        /// The action that was being rebound
        /// </summary>
        public string ActionName { get; }
        
        /// <summary>
        /// The binding index that was being modified
        /// </summary>
        public int BindingIndex { get; }
        
        /// <summary>
        /// The new binding path that was set (if successful)
        /// </summary>
        public string NewBindingPath { get; }
        
        /// <summary>
        /// The previous binding path that was replaced
        /// </summary>
        public string PreviousBindingPath { get; }
        
        /// <summary>
        /// Error message or additional information
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Exception that occurred during rebinding (if any)
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// The action that has a conflicting binding (if Status is Conflict)
        /// </summary>
        public string ConflictingAction { get; }
        
        /// <summary>
        /// Display name for the new binding (user-friendly)
        /// </summary>
        public string DisplayName { get; }
        
        #endregion
        
        #region Constructors
        
        private RebindingResult(RebindingStatus status, string actionName, int bindingIndex, 
            string newBindingPath = null, string previousBindingPath = null, 
            string message = null, Exception exception = null, string conflictingAction = null,
            string displayName = null)
        {
            Status = status;
            ActionName = actionName;
            BindingIndex = bindingIndex;
            NewBindingPath = newBindingPath;
            PreviousBindingPath = previousBindingPath;
            Message = message;
            Exception = exception;
            ConflictingAction = conflictingAction;
            DisplayName = displayName;
        }
        
        #endregion
        
        #region Factory Methods
        
        /// <summary>
        /// Create a successful rebinding result
        /// </summary>
        public static RebindingResult Success(string actionName, int bindingIndex, 
            string newBindingPath, string previousBindingPath, string displayName = null)
        {
            return new RebindingResult(RebindingStatus.Success, actionName, bindingIndex,
                newBindingPath, previousBindingPath, displayName: displayName);
        }
        
        /// <summary>
        /// Create a cancelled rebinding result
        /// </summary>
        public static RebindingResult Cancelled(string actionName, int bindingIndex, string message = null)
        {
            return new RebindingResult(RebindingStatus.Cancelled, actionName, bindingIndex, message: message);
        }
        
        /// <summary>
        /// Create a conflict rebinding result
        /// </summary>
        public static RebindingResult Conflict(string actionName, int bindingIndex, 
            string newBindingPath, string conflictingAction, string message = null)
        {
            return new RebindingResult(RebindingStatus.Conflict, actionName, bindingIndex,
                newBindingPath, conflictingAction: conflictingAction, message: message);
        }
        
        /// <summary>
        /// Create an invalid input rebinding result
        /// </summary>
        public static RebindingResult InvalidInput(string actionName, int bindingIndex, 
            string newBindingPath, string message = null)
        {
            return new RebindingResult(RebindingStatus.InvalidInput, actionName, bindingIndex,
                newBindingPath, message: message);
        }
        
        /// <summary>
        /// Create a timeout rebinding result
        /// </summary>
        public static RebindingResult Timeout(string actionName, int bindingIndex, string message = null)
        {
            return new RebindingResult(RebindingStatus.Timeout, actionName, bindingIndex, message: message);
        }
        
        /// <summary>
        /// Create an error rebinding result
        /// </summary>
        public static RebindingResult Error(string actionName, int bindingIndex, 
            Exception exception, string message = null)
        {
            return new RebindingResult(RebindingStatus.Error, actionName, bindingIndex,
                message: message ?? exception?.Message, exception: exception);
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Get a user-friendly description of the result
        /// </summary>
        public string GetDescription()
        {
            return Status switch
            {
                RebindingStatus.Success => $"Successfully rebound {ActionName} to {DisplayName ?? NewBindingPath}",
                RebindingStatus.Cancelled => $"Rebinding of {ActionName} was cancelled",
                RebindingStatus.Conflict => $"Cannot bind {ActionName} to {NewBindingPath} - already used by {ConflictingAction}",
                RebindingStatus.InvalidInput => $"Cannot bind {ActionName} to {NewBindingPath} - invalid input",
                RebindingStatus.Timeout => $"Rebinding of {ActionName} timed out",
                RebindingStatus.Error => $"Error rebinding {ActionName}: {Message}",
                _ => $"Unknown rebinding result for {ActionName}"
            };
        }
        
        public override string ToString()
        {
            return $"RebindingResult({Status}, {ActionName}[{BindingIndex}], {NewBindingPath})";
        }
        
        #endregion
    }
}
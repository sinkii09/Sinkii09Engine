using System;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for input rebinding operations, providing type-safe access to customize controls
    /// </summary>
    public interface IRebindableAction
    {
        #region Events
        
        /// <summary>
        /// Event fired when a rebinding operation starts
        /// </summary>
        event Action<string, int> OnRebindingStarted;
        
        /// <summary>
        /// Event fired when a rebinding operation completes
        /// </summary>
        event Action<RebindingResult> OnRebindingCompleted;
        
        /// <summary>
        /// Event fired when a rebinding operation is cancelled
        /// </summary>
        event Action<string, int> OnRebindingCancelled;
        
        /// <summary>
        /// Event fired when waiting for user input during rebinding
        /// </summary>
        event Action<string, int, float> OnWaitingForInput;
        
        #endregion
        
        #region Player Action Rebinding
        
        /// <summary>
        /// Start an interactive rebinding operation for a player action
        /// </summary>
        /// <param name="action">The player action to rebind</param>
        /// <param name="bindingIndex">The binding index to rebind (default: 0)</param>
        /// <param name="timeout">Timeout in seconds (default: 10)</param>
        /// <returns>Result of the rebinding operation</returns>
        UniTask<RebindingResult> StartRebindAsync(PlayerAction action, int bindingIndex = 0, float timeout = 10f);
        
        /// <summary>
        /// Reset a player action binding to its default
        /// </summary>
        /// <param name="action">The player action to reset</param>
        /// <param name="bindingIndex">The binding index to reset (default: -1 for all)</param>
        /// <returns>True if reset was successful</returns>
        bool ResetBinding(PlayerAction action, int bindingIndex = -1);
        
        /// <summary>
        /// Check if a player action has custom bindings
        /// </summary>
        /// <param name="action">The player action to check</param>
        /// <returns>True if action has custom bindings</returns>
        bool HasCustomBinding(PlayerAction action);
        
        /// <summary>
        /// Get the display string for a player action binding
        /// </summary>
        /// <param name="action">The player action</param>
        /// <param name="bindingIndex">The binding index (default: 0)</param>
        /// <returns>User-friendly display string</returns>
        string GetBindingDisplayString(PlayerAction action, int bindingIndex = 0);
        
        /// <summary>
        /// Get the raw binding path for a player action
        /// </summary>
        /// <param name="action">The player action</param>
        /// <param name="bindingIndex">The binding index (default: 0)</param>
        /// <returns>Raw binding path</returns>
        string GetBindingPath(PlayerAction action, int bindingIndex = 0);
        
        #endregion
        
        #region UI Action Rebinding
        
        /// <summary>
        /// Start an interactive rebinding operation for a UI action
        /// </summary>
        /// <param name="action">The UI action to rebind</param>
        /// <param name="bindingIndex">The binding index to rebind (default: 0)</param>
        /// <param name="timeout">Timeout in seconds (default: 10)</param>
        /// <returns>Result of the rebinding operation</returns>
        UniTask<RebindingResult> StartRebindAsync(UIAction action, int bindingIndex = 0, float timeout = 10f);
        
        /// <summary>
        /// Reset a UI action binding to its default
        /// </summary>
        /// <param name="action">The UI action to reset</param>
        /// <param name="bindingIndex">The binding index to reset (default: -1 for all)</param>
        /// <returns>True if reset was successful</returns>
        bool ResetBinding(UIAction action, int bindingIndex = -1);
        
        /// <summary>
        /// Check if a UI action has custom bindings
        /// </summary>
        /// <param name="action">The UI action to check</param>
        /// <returns>True if action has custom bindings</returns>
        bool HasCustomBinding(UIAction action);
        
        /// <summary>
        /// Get the display string for a UI action binding
        /// </summary>
        /// <param name="action">The UI action</param>
        /// <param name="bindingIndex">The binding index (default: 0)</param>
        /// <returns>User-friendly display string</returns>
        string GetBindingDisplayString(UIAction action, int bindingIndex = 0);
        
        /// <summary>
        /// Get the raw binding path for a UI action
        /// </summary>
        /// <param name="action">The UI action</param>
        /// <param name="bindingIndex">The binding index (default: 0)</param>
        /// <returns>Raw binding path</returns>
        string GetBindingPath(UIAction action, int bindingIndex = 0);
        
        #endregion
        
        #region General Operations
        
        /// <summary>
        /// Cancel any ongoing rebinding operation
        /// </summary>
        void CancelRebind();
        
        /// <summary>
        /// Reset all bindings to defaults
        /// </summary>
        /// <returns>Number of bindings reset</returns>
        int ResetAllBindings();
        
        /// <summary>
        /// Check if a rebinding operation is currently in progress
        /// </summary>
        bool IsRebinding { get; }
        
        /// <summary>
        /// Get the currently rebinding action (if any)
        /// </summary>
        string CurrentlyRebindingAction { get; }
        
        /// <summary>
        /// Save current bindings to persistent storage
        /// </summary>
        /// <returns>True if save was successful</returns>
        UniTask<bool> SaveBindingsAsync();
        
        /// <summary>
        /// Load bindings from persistent storage
        /// </summary>
        /// <returns>True if load was successful</returns>
        UniTask<bool> LoadBindingsAsync();
        
        /// <summary>
        /// Export bindings to a string for sharing/backup
        /// </summary>
        /// <returns>Serialized bindings string</returns>
        string ExportBindings();
        
        /// <summary>
        /// Import bindings from a string
        /// </summary>
        /// <param name="bindingsData">Serialized bindings string</param>
        /// <returns>True if import was successful</returns>
        bool ImportBindings(string bindingsData);
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Check if a binding path is valid and available
        /// </summary>
        /// <param name="bindingPath">The binding path to validate</param>
        /// <param name="excludeAction">Action to exclude from conflict check</param>
        /// <returns>True if binding is valid and available</returns>
        bool IsBindingValid(string bindingPath, string excludeAction = null);
        
        /// <summary>
        /// Find conflicting action for a given binding path
        /// </summary>
        /// <param name="bindingPath">The binding path to check</param>
        /// <param name="excludeAction">Action to exclude from conflict check</param>
        /// <returns>Name of conflicting action, or null if no conflict</returns>
        string FindConflictingAction(string bindingPath, string excludeAction = null);
        
        /// <summary>
        /// Check if a binding path represents a system/reserved key
        /// </summary>
        /// <param name="bindingPath">The binding path to check</param>
        /// <returns>True if binding is reserved</returns>
        bool IsReservedBinding(string bindingPath);
        
        #endregion
    }
}
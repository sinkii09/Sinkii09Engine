using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for the Input Service providing type-safe input management
    /// with context-based input blocking and cross-service coordination
    /// </summary>
    public interface IInputService : IEngineService
    {
        #region Player Input State
        
        /// <summary>
        /// Check if a player action is currently being held down
        /// </summary>
        /// <param name="action">The player action to check</param>
        /// <returns>True if the action is currently pressed</returns>
        bool IsActionPressed(PlayerAction action);
        
        /// <summary>
        /// Check if a player action was triggered this frame
        /// </summary>
        /// <param name="action">The player action to check</param>
        /// <returns>True if the action was triggered this frame</returns>
        bool IsActionTriggered(PlayerAction action);
        
        /// <summary>
        /// Check if a player action was released this frame
        /// </summary>
        /// <param name="action">The player action to check</param>
        /// <returns>True if the action was released this frame</returns>
        bool IsActionReleased(PlayerAction action);
        
        /// <summary>
        /// Get the float value of a player action (for analog inputs)
        /// </summary>
        /// <param name="action">The player action to get value from</param>
        /// <returns>Float value of the action</returns>
        float GetFloatValue(PlayerAction action);
        
        /// <summary>
        /// Get the Vector2 value of a player action (for movement/look inputs)
        /// </summary>
        /// <param name="action">The player action to get value from</param>
        /// <returns>Vector2 value of the action</returns>
        Vector2 GetVector2Value(PlayerAction action);
        
        #endregion
        
        #region UI Input State
        
        /// <summary>
        /// Check if a UI action is currently being held down
        /// </summary>
        /// <param name="action">The UI action to check</param>
        /// <returns>True if the action is currently pressed</returns>
        bool IsActionPressed(UIAction action);
        
        /// <summary>
        /// Check if a UI action was triggered this frame
        /// </summary>
        /// <param name="action">The UI action to check</param>
        /// <returns>True if the action was triggered this frame</returns>
        bool IsActionTriggered(UIAction action);
        
        /// <summary>
        /// Check if a UI action was released this frame
        /// </summary>
        /// <param name="action">The UI action to check</param>
        /// <returns>True if the action was released this frame</returns>
        bool IsActionReleased(UIAction action);
        
        /// <summary>
        /// Get the float value of a UI action
        /// </summary>
        /// <param name="action">The UI action to get value from</param>
        /// <returns>Float value of the action</returns>
        float GetFloatValue(UIAction action);
        
        /// <summary>
        /// Get the Vector2 value of a UI action
        /// </summary>
        /// <param name="action">The UI action to get value from</param>
        /// <returns>Vector2 value of the action</returns>
        Vector2 GetVector2Value(UIAction action);
        
        #endregion
        
        #region Device Information
        
        /// <summary>
        /// Get the currently active input device type
        /// </summary>
        InputDeviceType CurrentDeviceType { get; }
        
        /// <summary>
        /// Check if a gamepad is currently connected
        /// </summary>
        bool IsGamepadConnected { get; }
        
        #endregion
        
        #region Input Context Management
        
        /// <summary>
        /// Register a new input context with the input system
        /// Returns a handle for managing the context lifecycle
        /// </summary>
        /// <param name="context">The input context to register</param>
        /// <returns>Handle for managing context lifecycle</returns>
        IInputContextHandle RegisterContext(InputContext context);
        
        /// <summary>
        /// Get all currently active input contexts, sorted by priority (highest first)
        /// </summary>
        /// <returns>Read-only collection of active contexts</returns>
        System.Collections.Generic.IReadOnlyList<InputContext> GetActiveContexts();
        
        /// <summary>
        /// Get the highest priority context that would handle the specified player action
        /// </summary>
        /// <param name="action">Player action to check</param>
        /// <returns>Context that would handle this action, null if none</returns>
        InputContext GetHandlingContext(PlayerAction action);
        
        /// <summary>
        /// Get the highest priority context that would handle the specified UI action
        /// </summary>
        /// <param name="action">UI action to check</param>
        /// <returns>Context that would handle this action, null if none</returns>
        InputContext GetHandlingContext(UIAction action);
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Wait asynchronously for a specific player action to be triggered
        /// </summary>
        /// <param name="action">The player action to wait for</param>
        /// <returns>UniTask that completes when action is triggered</returns>
        UniTask WaitForAction(PlayerAction action);
        
        /// <summary>
        /// Wait asynchronously for a specific UI action to be triggered
        /// </summary>
        /// <param name="action">The UI action to wait for</param>
        /// <returns>UniTask that completes when action is triggered</returns>
        UniTask WaitForAction(UIAction action);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event triggered when any player action state changes
        /// </summary>
        event Action<PlayerAction, UnityEngine.InputSystem.InputActionPhase> OnPlayerActionTriggered;
        
        /// <summary>
        /// Event triggered when any UI action state changes
        /// </summary>
        event Action<UIAction, UnityEngine.InputSystem.InputActionPhase> OnUIActionTriggered;
        
        /// <summary>
        /// Event triggered when input context is added or removed
        /// Provides the new list of active contexts sorted by priority
        /// </summary>
        event Action<System.Collections.Generic.IReadOnlyList<InputContext>> OnContextsChanged;
        
        /// <summary>
        /// Event triggered when the active input device changes
        /// </summary>
        event Action<InputDeviceType, InputDeviceType> OnDeviceChanged;
        
        #endregion
        
        #region Input Rebinding
        
        /// <summary>
        /// Get access to the rebinding functionality
        /// </summary>
        IRebindableAction Rebinding { get; }
        
        #endregion
    }
    
}
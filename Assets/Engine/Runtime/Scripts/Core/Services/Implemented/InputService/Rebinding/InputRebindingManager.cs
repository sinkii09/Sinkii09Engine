using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages input rebinding operations with conflict detection and validation
    /// </summary>
    public class InputRebindingManager : IRebindableAction, IDisposable
    {
        #region Fields

        private readonly InputSystem_Actions _inputActions;
        private readonly InputServiceConfiguration _config;
        private readonly InputBindingPersistence _persistence;
        private readonly HashSet<string> _reservedBindings;
        
        private InputActionRebindingExtensions.RebindingOperation _currentRebindOperation;
        private bool _isRebinding;
        private string _currentlyRebindingAction;
        private bool _isDisposed;

        #endregion

        #region Events

        public event Action<string, int> OnRebindingStarted;
        public event Action<RebindingResult> OnRebindingCompleted;
        public event Action<string, int> OnRebindingCancelled;
        public event Action<string, int, float> OnWaitingForInput;

        #endregion

        #region Properties

        public bool IsRebinding => _isRebinding;
        public string CurrentlyRebindingAction => _currentlyRebindingAction;

        #endregion

        #region Constructor

        public InputRebindingManager(InputSystem_Actions inputActions, InputServiceConfiguration config, InputBindingPersistence persistence)
        {
            _inputActions = inputActions ?? throw new ArgumentNullException(nameof(inputActions));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            
            // Initialize reserved bindings (system keys that should not be rebound)
            _reservedBindings = new HashSet<string>
            {
                "<Keyboard>/escape",
                "<Keyboard>/f4",
                "<Keyboard>/alt",
                "<Keyboard>/tab",
                "<Keyboard>/printScreen",
                "<Keyboard>/pause",
                "<Keyboard>/capsLock",
                "<Keyboard>/numLock",
                "<Keyboard>/scrollLock",
                "<Keyboard>/leftCmd",
                "<Keyboard>/rightCmd",
                "<Keyboard>/leftWindows",
                "<Keyboard>/rightWindows"
            };
        }

        #endregion

        #region Player Action Rebinding

        public async UniTask<RebindingResult> StartRebindAsync(PlayerAction action, int bindingIndex = 0, float timeout = 10f)
        {
            if (_isRebinding)
            {
                return RebindingResult.Error(action.ToString(), bindingIndex, 
                    new InvalidOperationException("Another rebinding operation is already in progress"));
            }

            var inputAction = GetPlayerInputAction(action);
            if (inputAction == null)
            {
                return RebindingResult.Error(action.ToString(), bindingIndex,
                    new ArgumentException($"Player action {action} not found"));
            }

            return await PerformRebindAsync(inputAction, action.ToString(), bindingIndex, timeout);
        }

        public bool ResetBinding(PlayerAction action, int bindingIndex = -1)
        {
            try
            {
                var inputAction = GetPlayerInputAction(action);
                if (inputAction == null)
                    return false;

                if (bindingIndex == -1)
                {
                    // Reset all bindings for this action
                    inputAction.RemoveAllBindingOverrides();
                }
                else
                {
                    // Reset specific binding
                    if (bindingIndex >= 0 && bindingIndex < inputAction.bindings.Count)
                    {
                        inputAction.RemoveBindingOverride(bindingIndex);
                    }
                }

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[InputRebindingManager] Reset binding for {action} at index {bindingIndex}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to reset binding for {action}: {ex.Message}");
                return false;
            }
        }

        public bool HasCustomBinding(PlayerAction action)
        {
            var inputAction = GetPlayerInputAction(action);
            if (inputAction == null)
                return false;

            return inputAction.bindings.Any(binding => !string.IsNullOrEmpty(binding.overridePath));
        }

        public string GetBindingDisplayString(PlayerAction action, int bindingIndex = 0)
        {
            var inputAction = GetPlayerInputAction(action);
            if (inputAction == null || bindingIndex >= inputAction.bindings.Count)
                return "Unbound";

            var binding = inputAction.bindings[bindingIndex];
            return inputAction.GetBindingDisplayString(bindingIndex);
        }

        public string GetBindingPath(PlayerAction action, int bindingIndex = 0)
        {
            var inputAction = GetPlayerInputAction(action);
            if (inputAction == null || bindingIndex >= inputAction.bindings.Count)
                return null;

            var binding = inputAction.bindings[bindingIndex];
            return binding.effectivePath;
        }

        #endregion

        #region UI Action Rebinding

        public async UniTask<RebindingResult> StartRebindAsync(UIAction action, int bindingIndex = 0, float timeout = 10f)
        {
            if (_isRebinding)
            {
                return RebindingResult.Error(action.ToString(), bindingIndex,
                    new InvalidOperationException("Another rebinding operation is already in progress"));
            }

            var inputAction = GetUIInputAction(action);
            if (inputAction == null)
            {
                return RebindingResult.Error(action.ToString(), bindingIndex,
                    new ArgumentException($"UI action {action} not found"));
            }

            return await PerformRebindAsync(inputAction, action.ToString(), bindingIndex, timeout);
        }

        public bool ResetBinding(UIAction action, int bindingIndex = -1)
        {
            try
            {
                var inputAction = GetUIInputAction(action);
                if (inputAction == null)
                    return false;

                if (bindingIndex == -1)
                {
                    inputAction.RemoveAllBindingOverrides();
                }
                else
                {
                    if (bindingIndex >= 0 && bindingIndex < inputAction.bindings.Count)
                    {
                        inputAction.RemoveBindingOverride(bindingIndex);
                    }
                }

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[InputRebindingManager] Reset binding for {action} at index {bindingIndex}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to reset binding for {action}: {ex.Message}");
                return false;
            }
        }

        public bool HasCustomBinding(UIAction action)
        {
            var inputAction = GetUIInputAction(action);
            if (inputAction == null)
                return false;

            return inputAction.bindings.Any(binding => !string.IsNullOrEmpty(binding.overridePath));
        }

        public string GetBindingDisplayString(UIAction action, int bindingIndex = 0)
        {
            var inputAction = GetUIInputAction(action);
            if (inputAction == null || bindingIndex >= inputAction.bindings.Count)
                return "Unbound";

            return inputAction.GetBindingDisplayString(bindingIndex);
        }

        public string GetBindingPath(UIAction action, int bindingIndex = 0)
        {
            var inputAction = GetUIInputAction(action);
            if (inputAction == null || bindingIndex >= inputAction.bindings.Count)
                return null;

            var binding = inputAction.bindings[bindingIndex];
            return binding.effectivePath;
        }

        #endregion

        #region General Operations

        public void CancelRebind()
        {
            if (_currentRebindOperation != null)
            {
                _currentRebindOperation.Cancel();
                _currentRebindOperation = null;
            }

            if (_isRebinding)
            {
                _isRebinding = false;
                OnRebindingCancelled?.Invoke(_currentlyRebindingAction, 0);
                _currentlyRebindingAction = null;

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("[InputRebindingManager] Rebinding cancelled by user");
                }
            }
        }

        public int ResetAllBindings()
        {
            int resetCount = 0;

            try
            {
                // Reset all player actions - use dynamic access to InputActionMap
                var playerMap = _inputActions.Player.Get();
                foreach (var action in playerMap.actions)
                {
                    if (action.bindings.Any(b => !string.IsNullOrEmpty(b.overridePath)))
                    {
                        action.RemoveAllBindingOverrides();
                        resetCount++;
                    }
                }

                // Reset all UI actions - use dynamic access to InputActionMap
                var uiMap = _inputActions.UI.Get();
                foreach (var action in uiMap.actions)
                {
                    if (action.bindings.Any(b => !string.IsNullOrEmpty(b.overridePath)))
                    {
                        action.RemoveAllBindingOverrides();
                        resetCount++;
                    }
                }

                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[InputRebindingManager] Reset {resetCount} custom bindings");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Error resetting all bindings: {ex.Message}");
            }

            return resetCount;
        }

        public async UniTask<bool> SaveBindingsAsync()
        {
            try
            {
                // Get binding overrides from Unity Input System
                var bindings = _inputActions.SaveBindingOverridesAsJson();
                
                // Save via InputBindingPersistence (which uses SaveLoadService)
                var result = await _persistence.SaveCurrentBindingsAsync(bindings);
                
                if (result && _config.EnableDetailedLogging)
                {
                    Debug.Log("[InputRebindingManager] Saved binding overrides via SaveLoadService");
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to save bindings: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> LoadBindingsAsync()
        {
            try
            {
                // Load via InputBindingPersistence (which uses SaveLoadService)
                var bindings = await _persistence.LoadCurrentBindingsAsync();
                
                if (!string.IsNullOrEmpty(bindings))
                {
                    _inputActions.LoadBindingOverridesFromJson(bindings);
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log("[InputRebindingManager] Loaded binding overrides from SaveLoadService");
                    }
                    
                    return true;
                }
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("[InputRebindingManager] No saved bindings found, using defaults");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to load bindings: {ex.Message}");
                return false;
            }
        }

        public string ExportBindings()
        {
            try
            {
                return _inputActions.SaveBindingOverridesAsJson();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to export bindings: {ex.Message}");
                return null;
            }
        }

        public bool ImportBindings(string bindingsData)
        {
            try
            {
                if (string.IsNullOrEmpty(bindingsData))
                    return false;

                _inputActions.LoadBindingOverridesFromJson(bindingsData);
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("[InputRebindingManager] Imported binding overrides");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputRebindingManager] Failed to import bindings: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Validation

        public bool IsBindingValid(string bindingPath, string excludeAction = null)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return false;

            // Check if it's a reserved binding
            if (IsReservedBinding(bindingPath))
                return false;

            // Check for conflicts
            var conflictingAction = FindConflictingAction(bindingPath, excludeAction);
            return conflictingAction == null;
        }

        public string FindConflictingAction(string bindingPath, string excludeAction = null)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return null;

            // Check all player actions - use dynamic access to InputActionMap
            var playerMap = _inputActions.Player.Get();
            foreach (var action in playerMap.actions)
            {
                if (action.name == excludeAction)
                    continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.effectivePath == bindingPath)
                    {
                        return $"Player.{action.name}";
                    }
                }
            }

            // Check all UI actions - use dynamic access to InputActionMap
            var uiMap = _inputActions.UI.Get();
            foreach (var action in uiMap.actions)
            {
                if (action.name == excludeAction)
                    continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.effectivePath == bindingPath)
                    {
                        return $"UI.{action.name}";
                    }
                }
            }

            return null;
        }

        public bool IsReservedBinding(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
                return false;

            return _reservedBindings.Contains(bindingPath.ToLower());
        }

        #endregion

        #region Private Implementation

        private async UniTask<RebindingResult> PerformRebindAsync(InputAction inputAction, string actionName, int bindingIndex, float timeout)
        {
            _isRebinding = true;
            _currentlyRebindingAction = actionName;

            try
            {
                OnRebindingStarted?.Invoke(actionName, bindingIndex);

                var previousPath = bindingIndex < inputAction.bindings.Count ? 
                    inputAction.bindings[bindingIndex].effectivePath : null;

                var tcs = new UniTaskCompletionSource<RebindingResult>();

                _currentRebindOperation = inputAction.PerformInteractiveRebinding(bindingIndex)
                    .WithTimeout(timeout)
                    .WithControlsExcluding("Mouse")  // Exclude mouse movement by default
                    .OnMatchWaitForAnother(0.1f)     // Wait for additional input
                    .OnCancel(operation =>
                    {
                        tcs.TrySetResult(RebindingResult.Cancelled(actionName, bindingIndex));
                    })
                    .OnComplete(operation =>
                    {
                        var newPath = operation.selectedControl?.path;
                        var result = ValidateAndApplyBinding(actionName, bindingIndex, newPath, previousPath);
                        tcs.TrySetResult(result);
                    });

                // Start the rebind operation
                _currentRebindOperation.Start();

                // Fire progress events and wait for completion
                var progressTask = FireProgressEvents(actionName, bindingIndex, timeout);
                var finalResult = await tcs.Task;
                
                // Auto-save after successful rebind if enabled
                if (finalResult.Status == RebindingStatus.Success)
                {
                    await SaveBindingsAsync();
                }
                
                OnRebindingCompleted?.Invoke(finalResult);

                return finalResult;
            }
            catch (Exception ex)
            {
                return RebindingResult.Error(actionName, bindingIndex, ex);
            }
            finally
            {
                _isRebinding = false;
                _currentlyRebindingAction = null;
                _currentRebindOperation?.Dispose();
                _currentRebindOperation = null;
            }
        }

        private RebindingResult ValidateAndApplyBinding(string actionName, int bindingIndex, string newPath, string previousPath)
        {
            if (string.IsNullOrEmpty(newPath))
            {
                return RebindingResult.Cancelled(actionName, bindingIndex, "No input detected");
            }

            // Check if it's a reserved binding
            if (IsReservedBinding(newPath))
            {
                return RebindingResult.InvalidInput(actionName, bindingIndex, newPath, 
                    "Cannot bind to system/reserved keys");
            }

            // Check for conflicts
            var conflictingAction = FindConflictingAction(newPath, actionName);
            if (conflictingAction != null)
            {
                return RebindingResult.Conflict(actionName, bindingIndex, newPath, conflictingAction);
            }

            // Binding is valid - it's already been applied by Unity
            var displayName = InputControlPath.ToHumanReadableString(newPath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            return RebindingResult.Success(actionName, bindingIndex, newPath, previousPath, displayName);
        }

        private InputAction GetPlayerInputAction(PlayerAction action)
        {
            return action switch
            {
                PlayerAction.Move => _inputActions.Player.Move,
                PlayerAction.Look => _inputActions.Player.Look,
                PlayerAction.Attack => _inputActions.Player.Attack,
                PlayerAction.Interact => _inputActions.Player.Interact,
                PlayerAction.Crouch => _inputActions.Player.Crouch,
                PlayerAction.Jump => _inputActions.Player.Jump,
                PlayerAction.Previous => _inputActions.Player.Previous,
                PlayerAction.Next => _inputActions.Player.Next,
                PlayerAction.Sprint => _inputActions.Player.Sprint,
                _ => null
            };
        }

        private InputAction GetUIInputAction(UIAction action)
        {
            return action switch
            {
                UIAction.Navigate => _inputActions.UI.Navigate,
                UIAction.Submit => _inputActions.UI.Submit,
                UIAction.Cancel => _inputActions.UI.Cancel,
                UIAction.Point => _inputActions.UI.Point,
                UIAction.Click => _inputActions.UI.Click,
                UIAction.RightClick => _inputActions.UI.RightClick,
                UIAction.MiddleClick => _inputActions.UI.MiddleClick,
                UIAction.ScrollWheel => _inputActions.UI.ScrollWheel,
                UIAction.TrackedDevicePosition => _inputActions.UI.TrackedDevicePosition,
                UIAction.TrackedDeviceOrientation => _inputActions.UI.TrackedDeviceOrientation,
                _ => null
            };
        }


        /// <summary>
        /// Fire progress events while rebinding is in progress
        /// </summary>
        private async UniTask FireProgressEvents(string actionName, int bindingIndex, float timeout)
        {
            var startTime = Time.time;
            while (_currentRebindOperation != null && _isRebinding)
            {
                var elapsed = Time.time - startTime;
                if (elapsed >= timeout)
                    break;
                    
                OnWaitingForInput?.Invoke(actionName, bindingIndex, elapsed);
                await UniTask.Yield();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            CancelRebind();
            _isDisposed = true;
        }

        #endregion
    }
}
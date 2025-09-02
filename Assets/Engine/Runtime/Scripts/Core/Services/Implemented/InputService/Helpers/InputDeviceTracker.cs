using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Tracks the currently active input device using event-based detection for optimal performance
    /// </summary>
    public class InputDeviceTracker : IDisposable
    {
        #region Fields

        private InputDeviceType _currentDeviceType = InputDeviceType.Keyboard;
        private float _lastActivityTime;
        private readonly float _deviceTimeout;
        private readonly bool _autoSwitchDevices;
        private readonly bool _preferGamepad;
        private readonly bool _enableDetailedLogging;
        
        private float _cachedMouseMultiplier;
        private float _cachedGamepadMultiplier;
        
        private bool _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Fired when the active input device changes
        /// </summary>
        public event Action<InputDeviceType, InputDeviceType> OnDeviceChanged;

        #endregion

        #region Properties

        /// <summary>
        /// The currently active input device type
        /// </summary>
        public InputDeviceType CurrentDeviceType => _currentDeviceType;

        /// <summary>
        /// Time since last device activity
        /// </summary>
        public float TimeSinceLastActivity => Time.time - _lastActivityTime;

        /// <summary>
        /// Whether a gamepad is currently connected
        /// </summary>
        public bool IsGamepadConnected => Gamepad.current != null;

        #endregion

        #region Constructor

        public InputDeviceTracker(InputServiceConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _deviceTimeout = config.DeviceTimeoutDuration;
            _autoSwitchDevices = config.AutoSwitchDevices;
            _preferGamepad = config.PreferGamepad;
            _enableDetailedLogging = config.EnableDetailedLogging;
            
            UpdateCachedMultipliers(config);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the device tracker and subscribe to events
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(InputDeviceTracker));

            _lastActivityTime = Time.time;

            // Subscribe to Unity Input System events for zero-overhead detection
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onDeviceChange += OnDeviceChange;
            
            // Set initial device type
            DetectInitialDevice();

            if (_enableDetailedLogging)
            {
                Debug.Log($"[InputDeviceTracker] Initialized with device: {_currentDeviceType}");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle input action changes to detect device switches
        /// </summary>
        private void OnActionChange(object obj, InputActionChange change)
        {
            if (_isDisposed || !_autoSwitchDevices)
                return;

            // Only care about performed actions
            if (change == InputActionChange.ActionPerformed)
            {
                var action = obj as InputAction;
                if (action?.activeControl != null)
                {
                    DetectDeviceFromControl(action.activeControl);
                }
            }
        }

        /// <summary>
        /// Handle device connection/disconnection events
        /// </summary>
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (_isDisposed)
                return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    HandleDeviceConnected(device);
                    break;
                    
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    HandleDeviceDisconnected(device);
                    break;
                    
                case InputDeviceChange.UsageChanged:
                    if (_autoSwitchDevices)
                    {
                        UpdateDeviceTypeFromDevice(device);
                    }
                    break;
            }
        }

        #endregion

        #region Device Detection

        /// <summary>
        /// Detect the initial device on startup
        /// </summary>
        private void DetectInitialDevice()
        {
            // Check for gamepad first if preferred
            if (_preferGamepad && Gamepad.current != null)
            {
                SetDeviceType(InputDeviceType.Gamepad);
                return;
            }

            // Otherwise default to keyboard/mouse
            if (Mouse.current != null)
            {
                SetDeviceType(InputDeviceType.Mouse);
            }
            else if (Keyboard.current != null)
            {
                SetDeviceType(InputDeviceType.Keyboard);
            }
            else if (Touchscreen.current != null)
            {
                SetDeviceType(InputDeviceType.Touch);
            }
            else
            {
                SetDeviceType(InputDeviceType.Unknown);
            }
        }

        /// <summary>
        /// Detect device type from an input control
        /// </summary>
        private void DetectDeviceFromControl(InputControl control)
        {
            if (control?.device == null)
                return;

            UpdateDeviceTypeFromDevice(control.device);
        }

        /// <summary>
        /// Update device type based on the input device
        /// </summary>
        private void UpdateDeviceTypeFromDevice(InputDevice device)
        {
            InputDeviceType newType = GetDeviceType(device);
            
            if (newType != InputDeviceType.Unknown && newType != _currentDeviceType)
            {
                SetDeviceType(newType);
            }
        }

        /// <summary>
        /// Get device type from an InputDevice
        /// </summary>
        private InputDeviceType GetDeviceType(InputDevice device)
        {
            return device switch
            {
                Mouse => InputDeviceType.Mouse,
                Keyboard => InputDeviceType.Keyboard,
                Gamepad => InputDeviceType.Gamepad,
                Touchscreen => InputDeviceType.Touch,
                _ => InputDeviceType.Unknown
            };
        }

        /// <summary>
        /// Set the current device type and fire events
        /// </summary>
        private void SetDeviceType(InputDeviceType newType)
        {
            if (newType == _currentDeviceType)
                return;

            var oldType = _currentDeviceType;
            _currentDeviceType = newType;
            _lastActivityTime = Time.time;

            if (_enableDetailedLogging)
            {
                Debug.Log($"[InputDeviceTracker] Device switched from {oldType} to {newType}");
            }

            OnDeviceChanged?.Invoke(oldType, newType);
        }

        #endregion

        #region Device Connection Handling

        private void HandleDeviceConnected(InputDevice device)
        {
            if (_preferGamepad && device is Gamepad)
            {
                // Switch to gamepad immediately if preferred
                SetDeviceType(InputDeviceType.Gamepad);
                
                if (_enableDetailedLogging)
                {
                    Debug.Log("[InputDeviceTracker] Gamepad connected and set as active (preferred)");
                }
            }
        }

        private void HandleDeviceDisconnected(InputDevice device)
        {
            // If the disconnected device was the current one, find alternative
            if (GetDeviceType(device) == _currentDeviceType)
            {
                DetectInitialDevice();
                
                if (_enableDetailedLogging)
                {
                    Debug.Log($"[InputDeviceTracker] Current device disconnected, switched to {_currentDeviceType}");
                }
            }
        }

        #endregion

        #region Sensitivity Management

        /// <summary>
        /// Update cached sensitivity multipliers from configuration
        /// </summary>
        public void UpdateCachedMultipliers(InputServiceConfiguration config)
        {
            _cachedMouseMultiplier = config.MouseSensitivity;
            _cachedGamepadMultiplier = config.GamepadSensitivity;
        }

        /// <summary>
        /// Get the cached sensitivity multiplier for the current device
        /// </summary>
        public float GetSensitivityMultiplier()
        {
            return _currentDeviceType switch
            {
                InputDeviceType.Mouse => _cachedMouseMultiplier,
                InputDeviceType.Keyboard => _cachedMouseMultiplier, // Use mouse sensitivity for keyboard
                InputDeviceType.Gamepad => _cachedGamepadMultiplier,
                InputDeviceType.Touch => 1.0f, // Touch uses raw input
                _ => 1.0f
            };
        }

        #endregion

        #region Timeout Handling

        /// <summary>
        /// Check if the current device has timed out
        /// </summary>
        public void CheckForTimeout()
        {
            if (TimeSinceLastActivity > _deviceTimeout)
            {
                if (_enableDetailedLogging)
                {
                    Debug.LogWarning($"[InputDeviceTracker] Device {_currentDeviceType} timed out after {_deviceTimeout}s");
                }
                
                // Could switch to a default device or fire a timeout event
                DetectInitialDevice();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            // Unsubscribe from events to prevent memory leaks
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onDeviceChange -= OnDeviceChange;

            _isDisposed = true;

            if (_enableDetailedLogging)
            {
                Debug.Log("[InputDeviceTracker] Disposed and unsubscribed from events");
            }
        }

        #endregion
    }
}
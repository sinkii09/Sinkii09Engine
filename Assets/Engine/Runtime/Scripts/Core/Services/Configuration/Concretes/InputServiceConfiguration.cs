using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Input Service with settings for input management,
    /// device handling, and performance optimization
    /// </summary>
    [CreateAssetMenu(fileName = "InputServiceConfiguration", menuName = "Engine/Services/InputServiceConfiguration", order = 9)]
    public class InputServiceConfiguration : ServiceConfigurationBase
    {

        #region Editor Settings

        [Header("Editor Settings")]
        [SerializeField]
        [Tooltip("Enable input system in editor for testing")]
        private bool _enableInEditor = true;

        #endregion

        #region Device Management

        [Header("Device Management")]
        [SerializeField]
        [Tooltip("Automatically switch to the most recently used input device")]
        private bool _autoSwitchDevices = true;

        [SerializeField]
        [Tooltip("Prefer gamepad input over keyboard/mouse when available")]
        private bool _preferGamepad = false;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Time in seconds before considering a device disconnected")]
        private float _deviceTimeoutDuration = 2.0f;

        #endregion

        #region Input Sensitivity

        [Header("Input Sensitivity")]
        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Mouse sensitivity multiplier")]
        private float _mouseSensitivity = 1.0f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Gamepad sensitivity multiplier")]
        private float _gamepadSensitivity = 1.0f;

        [SerializeField]
        [Tooltip("Sensitivity curve for analog inputs")]
        private AnimationCurve _sensitivityCurve = AnimationCurve.Linear(0, 0, 1, 1);

        #endregion

        #region Context Management

        [Header("Context Management")]
        [SerializeField]
        [Tooltip("Enable input context system for blocking input during certain game states")]
        private bool _enableContextSystem = true;

        [SerializeField, Range(1, 10)]
        [Tooltip("Maximum number of input contexts that can be stacked")]
        private int _maxContextStackSize = 5;

        #endregion

        #region Performance

        [Header("Performance")]
        [SerializeField]
        [Tooltip("Enable detailed logging of input events")]
        private bool _enableDetailedLogging = false;

        [SerializeField]
        [Tooltip("Cache input actions for better performance")]
        private bool _enableActionCaching = true;

        [SerializeField, Range(1, 120)]
        [Tooltip("Input update frequency in Hz")]
        private int _inputUpdateRate = 60;

        #endregion

        #region Accessibility

        [Header("Accessibility")]
        [SerializeField]
        [Tooltip("Enable accessibility features like hold-to-press")]
        private bool _enableAccessibilityFeatures = false;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Duration for hold-to-press actions")]
        private float _holdToPressDuration = 1.0f;

        #endregion

        #region Properties

        /// <summary>
        /// Whether to enable input system in editor
        /// </summary>
        public bool EnableInEditor => _enableInEditor;

        /// <summary>
        /// Whether to automatically switch between input devices
        /// </summary>
        public bool AutoSwitchDevices => _autoSwitchDevices;

        /// <summary>
        /// Whether to prefer gamepad input when available
        /// </summary>
        public bool PreferGamepad => _preferGamepad;

        /// <summary>
        /// Device timeout duration in seconds
        /// </summary>
        public float DeviceTimeoutDuration => _deviceTimeoutDuration;

        /// <summary>
        /// Mouse sensitivity multiplier
        /// </summary>
        public float MouseSensitivity => _mouseSensitivity;

        /// <summary>
        /// Gamepad sensitivity multiplier
        /// </summary>
        public float GamepadSensitivity => _gamepadSensitivity;

        /// <summary>
        /// Sensitivity curve for analog inputs
        /// </summary>
        public AnimationCurve SensitivityCurve => _sensitivityCurve;

        /// <summary>
        /// Whether input context system is enabled
        /// </summary>
        public bool EnableContextSystem => _enableContextSystem;

        /// <summary>
        /// Maximum input context stack size
        /// </summary>
        public int MaxContextStackSize => _maxContextStackSize;

        /// <summary>
        /// Whether detailed logging is enabled
        /// </summary>
        public bool EnableDetailedLogging => _enableDetailedLogging;

        /// <summary>
        /// Whether action caching is enabled
        /// </summary>
        public bool EnableActionCaching => _enableActionCaching;

        /// <summary>
        /// Input update rate in Hz
        /// </summary>
        public int InputUpdateRate => _inputUpdateRate;

        /// <summary>
        /// Whether accessibility features are enabled
        /// </summary>
        public bool EnableAccessibilityFeatures => _enableAccessibilityFeatures;

        /// <summary>
        /// Hold-to-press duration in seconds
        /// </summary>
        public float HoldToPressDuration => _holdToPressDuration;

        #endregion

        #region Validation

        protected override void OnValidate()
        {
            base.OnValidate();

            // Note: InputService uses compile-time generated InputSystem_Actions class for type safety

            if (_maxContextStackSize < 1)
            {
                _maxContextStackSize = 1;
            }

            if (_deviceTimeoutDuration < 0.1f)
            {
                _deviceTimeoutDuration = 0.1f;
            }
        }

        #endregion

    }
}
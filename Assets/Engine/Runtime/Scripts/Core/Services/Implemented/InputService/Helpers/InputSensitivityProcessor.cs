using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Processes input values with sensitivity multipliers and curves for optimal feel
    /// </summary>
    public class InputSensitivityProcessor
    {
        #region Fields

        private InputServiceConfiguration _config;
        private InputDeviceTracker _deviceTracker;
        
        private AnimationCurve _sensitivityCurve;
        private bool _enableDetailedLogging;
        
        // Caching for performance
        private readonly Dictionary<float, float> _curveCache = new Dictionary<float, float>();
        private const int MaxCacheSize = 100;
        private float _lastCurveClearTime;
        private const float CurveCacheClearInterval = 5.0f; // Clear cache every 5 seconds

        #endregion

        #region Constructor

        public InputSensitivityProcessor(InputServiceConfiguration config, InputDeviceTracker deviceTracker)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _deviceTracker = deviceTracker ?? throw new ArgumentNullException(nameof(deviceTracker));
            
            UpdateConfiguration(config);
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Update configuration values (for hot-reload support)
        /// </summary>
        public void UpdateConfiguration(InputServiceConfiguration config)
        {
            _config = config;
            _sensitivityCurve = config.SensitivityCurve;
            _enableDetailedLogging = config.EnableDetailedLogging;
            
            // Update device tracker multipliers
            _deviceTracker.UpdateCachedMultipliers(config);
            
            // Clear curve cache when configuration changes
            _curveCache.Clear();
            
            if (_enableDetailedLogging)
            {
                Debug.Log("[InputSensitivityProcessor] Configuration updated");
            }
        }

        #endregion

        #region Vector2 Processing

        /// <summary>
        /// Apply sensitivity to a Vector2 input value
        /// </summary>
        public Vector2 ApplySensitivity(Vector2 rawValue)
        {
            if (rawValue == Vector2.zero)
                return Vector2.zero;

            // Get device-specific multiplier
            float multiplier = _deviceTracker.GetSensitivityMultiplier();
            
            // Apply multiplier
            Vector2 scaled = rawValue * multiplier;
            
            // Apply sensitivity curve
            return ApplyCurveToVector2(scaled);
        }

        /// <summary>
        /// Apply sensitivity to a Vector2 with specific action context
        /// </summary>
        public Vector2 ApplySensitivity(Vector2 rawValue, PlayerAction action)
        {
            // Could apply action-specific processing here if needed
            // For now, use standard processing
            return ApplySensitivity(rawValue);
        }

        /// <summary>
        /// Apply sensitivity to a UI Vector2 input
        /// </summary>
        public Vector2 ApplySensitivity(Vector2 rawValue, UIAction action)
        {
            // UI inputs might have different sensitivity needs
            // For now, use standard processing
            return ApplySensitivity(rawValue);
        }

        #endregion

        #region Float Processing

        /// <summary>
        /// Apply sensitivity to a float input value
        /// </summary>
        public float ApplySensitivity(float rawValue)
        {
            if (Mathf.Approximately(rawValue, 0f))
                return 0f;

            // Get device-specific multiplier
            float multiplier = _deviceTracker.GetSensitivityMultiplier();
            
            // Apply multiplier
            float scaled = rawValue * multiplier;
            
            // Apply sensitivity curve
            return ApplyCurveToFloat(scaled);
        }

        /// <summary>
        /// Apply sensitivity to a float with specific action context
        /// </summary>
        public float ApplySensitivity(float rawValue, PlayerAction action)
        {
            return ApplySensitivity(rawValue);
        }

        /// <summary>
        /// Apply sensitivity to a UI float input
        /// </summary>
        public float ApplySensitivity(float rawValue, UIAction action)
        {
            return ApplySensitivity(rawValue);
        }

        #endregion

        #region Curve Application

        /// <summary>
        /// Apply sensitivity curve to a Vector2
        /// </summary>
        private Vector2 ApplyCurveToVector2(Vector2 value)
        {
            if (_sensitivityCurve == null || _sensitivityCurve.length == 0)
                return value;

            float magnitude = value.magnitude;
            if (magnitude < 0.001f)
                return Vector2.zero;

            // Clamp magnitude to 0-1 range for curve evaluation
            float normalizedMagnitude = Mathf.Clamp01(magnitude);
            
            // Get curved value (with caching)
            float curvedMagnitude = EvaluateCurveWithCache(normalizedMagnitude);
            
            // If original magnitude was > 1, scale the result
            if (magnitude > 1.0f)
            {
                curvedMagnitude *= magnitude;
            }
            
            // Apply curved magnitude to normalized direction
            return value.normalized * curvedMagnitude;
        }

        /// <summary>
        /// Apply sensitivity curve to a float
        /// </summary>
        private float ApplyCurveToFloat(float value)
        {
            if (_sensitivityCurve == null || _sensitivityCurve.length == 0)
                return value;

            float absValue = Mathf.Abs(value);
            if (absValue < 0.001f)
                return 0f;

            // Clamp to 0-1 range for curve evaluation
            float normalized = Mathf.Clamp01(absValue);
            
            // Get curved value (with caching)
            float curved = EvaluateCurveWithCache(normalized);
            
            // If original value was > 1, scale the result
            if (absValue > 1.0f)
            {
                curved *= absValue;
            }
            
            // Restore sign
            return Mathf.Sign(value) * curved;
        }

        /// <summary>
        /// Evaluate curve with caching for performance
        /// </summary>
        private float EvaluateCurveWithCache(float input)
        {
            // Periodic cache cleanup
            if (Time.time - _lastCurveClearTime > CurveCacheClearInterval)
            {
                if (_curveCache.Count > MaxCacheSize)
                {
                    _curveCache.Clear();
                    _lastCurveClearTime = Time.time;
                }
            }

            // Round input to reduce cache entries (0.01 precision)
            float roundedInput = Mathf.Round(input * 100f) / 100f;
            
            // Check cache
            if (_curveCache.TryGetValue(roundedInput, out float cachedValue))
            {
                return cachedValue;
            }
            
            // Evaluate and cache
            float result = _sensitivityCurve.Evaluate(roundedInput);
            _curveCache[roundedInput] = result;
            
            return result;
        }

        #endregion

        #region Dead Zone Processing

        /// <summary>
        /// Apply dead zone to a Vector2 (for analog sticks)
        /// </summary>
        public Vector2 ApplyDeadZone(Vector2 value, float deadZone = 0.1f)
        {
            float magnitude = value.magnitude;
            
            if (magnitude < deadZone)
                return Vector2.zero;
            
            // Remap from deadZone-1 to 0-1
            float remappedMagnitude = (magnitude - deadZone) / (1.0f - deadZone);
            return value.normalized * remappedMagnitude;
        }

        /// <summary>
        /// Apply radial dead zone (better for analog sticks)
        /// </summary>
        public Vector2 ApplyRadialDeadZone(Vector2 value, float innerDeadZone = 0.1f, float outerDeadZone = 0.9f)
        {
            float magnitude = value.magnitude;
            
            if (magnitude < innerDeadZone)
                return Vector2.zero;
            
            if (magnitude > outerDeadZone)
                return value.normalized;
            
            // Remap from innerDeadZone-outerDeadZone to 0-1
            float remappedMagnitude = (magnitude - innerDeadZone) / (outerDeadZone - innerDeadZone);
            return value.normalized * remappedMagnitude;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clear all caches (useful when configuration changes)
        /// </summary>
        public void ClearCaches()
        {
            _curveCache.Clear();
            _lastCurveClearTime = Time.time;
            
            if (_enableDetailedLogging)
            {
                Debug.Log("[InputSensitivityProcessor] Caches cleared");
            }
        }

        #endregion
    }
}
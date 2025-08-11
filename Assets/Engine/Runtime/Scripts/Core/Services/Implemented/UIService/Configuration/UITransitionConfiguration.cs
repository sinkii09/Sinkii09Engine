using System;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for UI transitions - defines settings for each transition type
    /// </summary>
    [CreateAssetMenu(fileName = "UITransitionConfiguration", menuName = "Engine/UI/Transition Configuration", order = 2)]
    public class UITransitionConfiguration : ScriptableObject
    {
        [Header("Default Settings")]
        [SerializeField, Range(0.1f, 2f), Tooltip("Default transition duration")]
        private float _defaultDuration = 0.3f;
        
        [SerializeField, Tooltip("Default easing type for transitions")]
        private Ease _defaultEase = Ease.OutQuart;
        
        [SerializeField, Tooltip("Whether transitions can be interrupted")]
        private bool _allowInterruption = true;

        [Header("Fade Transition")]
        [SerializeField] private FadeTransitionSettings _fadeSettings = new();

        [Header("Slide Transitions")]
        [SerializeField] private SlideTransitionSettings _slideSettings = new();

        [Header("Scale Transitions")]
        [SerializeField] private ScaleTransitionSettings _scaleSettings = new();

        [Header("Push Transitions")]
        [SerializeField] private PushTransitionSettings _pushSettings = new();

        #region Public Properties

        /// <summary>
        /// Default transition duration
        /// </summary>
        public float DefaultDuration => _defaultDuration;

        /// <summary>
        /// Default easing type
        /// </summary>
        public Ease DefaultEase => _defaultEase;

        /// <summary>
        /// Whether transitions can be interrupted
        /// </summary>
        public bool AllowInterruption => _allowInterruption;

        /// <summary>
        /// Fade transition settings
        /// </summary>
        public FadeTransitionSettings FadeSettings => _fadeSettings;

        /// <summary>
        /// Slide transition settings
        /// </summary>
        public SlideTransitionSettings SlideSettings => _slideSettings;

        /// <summary>
        /// Scale transition settings
        /// </summary>
        public ScaleTransitionSettings ScaleSettings => _scaleSettings;

        /// <summary>
        /// Push transition settings
        /// </summary>
        public PushTransitionSettings PushSettings => _pushSettings;

        #endregion

        #region Settings Classes

        [System.Serializable]
        public class FadeTransitionSettings
        {
            [SerializeField, Range(0f, 1f), Tooltip("Alpha value when fading in starts")]
            public float startAlpha = 0f;
            
            [SerializeField, Range(0f, 1f), Tooltip("Alpha value when faded in completely")]
            public float endAlpha = 1f;
            
            [SerializeField, Range(0.1f, 2f), Tooltip("Duration override (0 = use default)")]
            public float durationOverride = 0f;
            
            [SerializeField, Tooltip("Easing override (None = use default)")]
            public Ease easeOverride = Ease.Unset;

            public float GetDuration(float defaultDuration) => durationOverride > 0 ? durationOverride : defaultDuration;
            public Ease GetEase(Ease defaultEase) => easeOverride != Ease.Unset ? easeOverride : defaultEase;
        }

        [System.Serializable]
        public class SlideTransitionSettings
        {
            [SerializeField, Range(0.1f, 2f), Tooltip("Distance multiplier for slide (1.0 = screen width/height)")]
            public float distanceMultiplier = 1.0f;
            
            [SerializeField, Range(0.1f, 2f), Tooltip("Duration override (0 = use default)")]
            public float durationOverride = 0f;
            
            [SerializeField, Tooltip("Easing override (None = use default)")]
            public Ease easeOverride = Ease.Unset;
            
            [SerializeField, Tooltip("Whether to use elastic effect at end")]
            public bool useElasticEffect = false;

            public float GetDuration(float defaultDuration) => durationOverride > 0 ? durationOverride : defaultDuration;
            public Ease GetEase(Ease defaultEase) => easeOverride != Ease.Unset ? easeOverride : defaultEase;
        }

        [System.Serializable]
        public class ScaleTransitionSettings
        {
            [SerializeField, Range(0f, 2f), Tooltip("Starting scale for scale-up transitions")]
            public float startScale = 0.8f;
            
            [SerializeField, Range(0.5f, 2f), Tooltip("Target scale when fully scaled")]
            public float targetScale = 1f;
            
            [SerializeField, Range(0.1f, 2f), Tooltip("Duration override (0 = use default)")]
            public float durationOverride = 0f;
            
            [SerializeField, Tooltip("Easing override (None = use default)")]
            public Ease easeOverride = Ease.Unset;
            
            [SerializeField, Tooltip("Whether to use punch effect")]
            public bool usePunchEffect = false;
            
            [SerializeField, Range(0.1f, 0.5f), Tooltip("Punch strength if enabled")]
            public float punchStrength = 0.2f;

            public float GetDuration(float defaultDuration) => durationOverride > 0 ? durationOverride : defaultDuration;
            public Ease GetEase(Ease defaultEase) => easeOverride != Ease.Unset ? easeOverride : defaultEase;
        }

        [System.Serializable]
        public class PushTransitionSettings
        {
            [SerializeField, Range(0.1f, 2f), Tooltip("Duration override (0 = use default)")]
            public float durationOverride = 0f;
            
            [SerializeField, Tooltip("Easing override (None = use default)")]
            public Ease easeOverride = Ease.Unset;
            
            [SerializeField, Range(0f, 1f), Tooltip("Overlap between incoming and outgoing screens")]
            public float overlapPercentage = 0.2f;
            
            [SerializeField, Tooltip("Direction for push transitions")]
            public PushDirection defaultDirection = PushDirection.Left;

            public float GetDuration(float defaultDuration) => durationOverride > 0 ? durationOverride : defaultDuration;
            public Ease GetEase(Ease defaultEase) => easeOverride != Ease.Unset ? easeOverride : defaultEase;
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            // Ensure valid duration
            _defaultDuration = Mathf.Max(0.01f, _defaultDuration);
            
            // Validate fade settings
            _fadeSettings.startAlpha = Mathf.Clamp01(_fadeSettings.startAlpha);
            _fadeSettings.endAlpha = Mathf.Clamp01(_fadeSettings.endAlpha);
            _fadeSettings.durationOverride = Mathf.Max(0f, _fadeSettings.durationOverride);
            
            // Validate slide settings
            _slideSettings.distanceMultiplier = Mathf.Max(0.1f, _slideSettings.distanceMultiplier);
            _slideSettings.durationOverride = Mathf.Max(0f, _slideSettings.durationOverride);
            
            // Validate scale settings
            _scaleSettings.startScale = Mathf.Max(0f, _scaleSettings.startScale);
            _scaleSettings.targetScale = Mathf.Max(0.1f, _scaleSettings.targetScale);
            _scaleSettings.durationOverride = Mathf.Max(0f, _scaleSettings.durationOverride);
            _scaleSettings.punchStrength = Mathf.Clamp(_scaleSettings.punchStrength, 0.1f, 0.5f);
            
            // Validate push settings
            _pushSettings.durationOverride = Mathf.Max(0f, _pushSettings.durationOverride);
            _pushSettings.overlapPercentage = Mathf.Clamp01(_pushSettings.overlapPercentage);
        }

        #endregion

        #region Editor Support

        /// <summary>
        /// Get a summary of the current configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"UI Transitions: {_defaultDuration:F2}s duration, {_defaultEase} easing, " +
                   $"Interruption {(_allowInterruption ? "enabled" : "disabled")}";
        }

        /// <summary>
        /// Reset all settings to default values (Editor-only)
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void ResetToDefaults()
        {
            // Default settings
            _defaultDuration = 0.3f;
            _defaultEase = Ease.OutQuart;
            _allowInterruption = true;

            // Reset fade settings
            _fadeSettings = new FadeTransitionSettings
            {
                startAlpha = 0f,
                endAlpha = 1f,
                durationOverride = 0f,
                easeOverride = Ease.Unset
            };

            // Reset slide settings
            _slideSettings = new SlideTransitionSettings
            {
                distanceMultiplier = 1.0f,
                durationOverride = 0f,
                easeOverride = Ease.Unset,
                useElasticEffect = false
            };

            // Reset scale settings
            _scaleSettings = new ScaleTransitionSettings
            {
                startScale = 0.8f,
                targetScale = 1f,
                durationOverride = 0f,
                easeOverride = Ease.Unset,
                usePunchEffect = false,
                punchStrength = 0.2f
            };

            // Reset push settings
            _pushSettings = new PushTransitionSettings
            {
                durationOverride = 0f,
                easeOverride = Ease.Unset,
                overlapPercentage = 0.2f,
                defaultDirection = PushDirection.Left
            };

#if UNITY_EDITOR
            // Mark as dirty in editor
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        #endregion
    }

    /// <summary>
    /// Direction for push transitions
    /// </summary>
    public enum PushDirection
    {
        Left = 0,
        Right = 1,
        Up = 2,
        Down = 3
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Specialized audio player for UI sounds with immediate response and feedback patterns
    /// </summary>
    public class UIPlayer : AudioPlayer
    {
        #region Private Fields
        
        private readonly AudioServiceConfiguration _uiConfig;
        private UIInteractionType _interactionType;
        private static readonly Dictionary<UIInteractionType, float> _defaultVolumes = new Dictionary<UIInteractionType, float>();
        private static float _globalUIVolume = 1f;
        private bool _bypassMute = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Type of UI interaction for this sound
        /// </summary>
        public UIInteractionType InteractionType
        {
            get => _interactionType;
            set => _interactionType = value;
        }
        
        /// <summary>
        /// Whether this UI sound bypasses mute settings (for critical notifications)
        /// </summary>
        public bool BypassMute
        {
            get => _bypassMute;
            set => _bypassMute = value;
        }
        
        /// <summary>
        /// Global UI volume multiplier
        /// </summary>
        public static float GlobalUIVolume
        {
            get => _globalUIVolume;
            set => _globalUIVolume = Mathf.Clamp01(value);
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a UI interaction sound plays
        /// </summary>
        public static event Action<UIInteractionType> OnUIInteraction;
        
        /// <summary>
        /// Fired when a notification sound plays
        /// </summary>
        public static event Action<UINotificationType> OnUINotification;
        
        #endregion
        
        #region Constructor
        
        static UIPlayer()
        {
            // Initialize default volumes for UI interaction types
            _defaultVolumes[UIInteractionType.ButtonClick] = 0.8f;
            _defaultVolumes[UIInteractionType.ButtonHover] = 0.4f;
            _defaultVolumes[UIInteractionType.Toggle] = 0.7f;
            _defaultVolumes[UIInteractionType.Slider] = 0.5f;
            _defaultVolumes[UIInteractionType.PageTurn] = 0.6f;
            _defaultVolumes[UIInteractionType.MenuOpen] = 0.9f;
            _defaultVolumes[UIInteractionType.MenuClose] = 0.9f;
            _defaultVolumes[UIInteractionType.Confirm] = 1.0f;
            _defaultVolumes[UIInteractionType.Cancel] = 0.8f;
            _defaultVolumes[UIInteractionType.Error] = 1.0f;
            _defaultVolumes[UIInteractionType.Success] = 1.0f;
            _defaultVolumes[UIInteractionType.Warning] = 0.9f;
            _defaultVolumes[UIInteractionType.Notification] = 0.9f;
            _defaultVolumes[UIInteractionType.Typing] = 0.3f;
            _defaultVolumes[UIInteractionType.Select] = 0.7f;
            _defaultVolumes[UIInteractionType.Deselect] = 0.6f;
        }
        
        public UIPlayer(string audioId, AudioSource audioSource, AudioClip audioClip, AudioServiceConfiguration config)
            : base(audioId, AudioCategory.UI, audioSource, audioClip, config)
        {
            _uiConfig = config;
            
            // UI-specific defaults
            IsLooping = false; // UI sounds don't loop
            _audioSource.spatialBlend = 0f; // Always 2D
            _audioSource.priority = 32; // High priority for responsiveness
        }
        
        #endregion
        
        #region Playback Control
        
        public override async UniTask<IAudioPlayer> PlayAsync(AudioPlayOptions options = default, CancellationToken cancellationToken = default)
        {
            // Apply interaction-specific volume
            if (_defaultVolumes.TryGetValue(_interactionType, out float defaultVolume))
            {
                options.Volume = defaultVolume * _globalUIVolume * options.Volume;
            }
            
            // Handle bypass mute for critical notifications
            if (_bypassMute)
            {
                // Store and restore mute state
                var wasMuted = _audioSource.mute;
                _audioSource.mute = false;
                
                var result = await base.PlayAsync(options, cancellationToken);
                
                // Restore mute state after playing
                _ = RestoreMuteStateAfterPlayback(wasMuted);
                
                return result;
            }
            
            // Fire interaction event
            OnUIInteraction?.Invoke(_interactionType);
            
            return await base.PlayAsync(options, cancellationToken);
        }
        
        #endregion
        
        #region UI Feedback Patterns
        
        /// <summary>
        /// Plays a success pattern (ascending tones)
        /// </summary>
        public async UniTask PlaySuccessPatternAsync(CancellationToken cancellationToken = default)
        {
            _interactionType = UIInteractionType.Success;
            
            // Play with slight pitch shift for ascending effect
            _audioSource.pitch = 0.95f;
            await PlayAsync(AudioPlayOptions.Default, cancellationToken);
            
            await UniTask.Delay(50, cancellationToken: cancellationToken);
            
            _audioSource.pitch = 1.05f;
            await PlayAsync(AudioPlayOptions.Default, cancellationToken);
            
            _audioSource.pitch = 1.0f; // Reset
        }
        
        /// <summary>
        /// Plays an error pattern (descending tones)
        /// </summary>
        public async UniTask PlayErrorPatternAsync(CancellationToken cancellationToken = default)
        {
            _interactionType = UIInteractionType.Error;
            
            // Play with slight pitch shift for descending effect
            _audioSource.pitch = 1.05f;
            await PlayAsync(AudioPlayOptions.Default, cancellationToken);
            
            await UniTask.Delay(50, cancellationToken: cancellationToken);
            
            _audioSource.pitch = 0.95f;
            await PlayAsync(AudioPlayOptions.Default, cancellationToken);
            
            _audioSource.pitch = 1.0f; // Reset
        }
        
        /// <summary>
        /// Plays a notification pattern with optional repeat
        /// </summary>
        public async UniTask PlayNotificationPatternAsync(UINotificationType notificationType, int repeatCount = 1, CancellationToken cancellationToken = default)
        {
            _interactionType = UIInteractionType.Notification;
            OnUINotification?.Invoke(notificationType);
            
            for (int i = 0; i < repeatCount; i++)
            {
                await PlayAsync(AudioPlayOptions.Default, cancellationToken);
                
                if (i < repeatCount - 1)
                {
                    await UniTask.Delay(200, cancellationToken: cancellationToken);
                }
            }
        }
        
        #endregion
        
        #region Haptic Feedback Integration
        
        /// <summary>
        /// Triggers haptic feedback alongside UI sound (for mobile/controller)
        /// </summary>
        public void TriggerHapticFeedback(HapticIntensity intensity = HapticIntensity.Light)
        {
            // This would integrate with platform-specific haptic APIs
            // For example, Unity's Handheld.Vibrate() for mobile
            // Or controller rumble for console platforms
            
            switch (intensity)
            {
                case HapticIntensity.Light:
                    // Light vibration
                    break;
                case HapticIntensity.Medium:
                    // Medium vibration
                    break;
                case HapticIntensity.Heavy:
                    // Heavy vibration
                    break;
            }
        }
        
        #endregion
        
        #region Accessibility Features
        
        /// <summary>
        /// Adjusts UI sound for accessibility (hearing impaired users)
        /// </summary>
        public void ApplyAccessibilitySettings(AccessibilitySettings settings)
        {
            if (settings.EnhancedUIFeedback)
            {
                // Increase volume and add visual feedback cue
                Volume = Mathf.Min(Volume * 1.5f, 1f);
            }
            
            if (settings.SimplifiedAudio)
            {
                // Remove complex effects, use clear tones
                RemoveAudioEffects();
            }
            
            if (settings.VisualOnlyMode)
            {
                // Mute audio but trigger visual feedback
                _audioSource.mute = true;
            }
        }
        
        /// <summary>
        /// Removes audio effects for clarity
        /// </summary>
        private void RemoveAudioEffects()
        {
            // Remove specific audio filter components
            var lowPass = _audioSource.GetComponent<AudioLowPassFilter>();
            if (lowPass != null) GameObject.Destroy(lowPass);
            
            var highPass = _audioSource.GetComponent<AudioHighPassFilter>();
            if (highPass != null) GameObject.Destroy(highPass);
            
            var reverb = _audioSource.GetComponent<AudioReverbFilter>();
            if (reverb != null) GameObject.Destroy(reverb);
            
            var chorus = _audioSource.GetComponent<AudioChorusFilter>();
            if (chorus != null) GameObject.Destroy(chorus);
            
            var echo = _audioSource.GetComponent<AudioEchoFilter>();
            if (echo != null) GameObject.Destroy(echo);
            
            var distortion = _audioSource.GetComponent<AudioDistortionFilter>();
            if (distortion != null) GameObject.Destroy(distortion);
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private async UniTask RestoreMuteStateAfterPlayback(bool wasMuted)
        {
            // Wait for playback to complete
            while (IsPlaying)
            {
                await UniTask.Delay(100);
            }
            
            _audioSource.mute = wasMuted;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Types of UI interactions
    /// </summary>
    public enum UIInteractionType
    {
        ButtonClick,
        ButtonHover,
        Toggle,
        Slider,
        PageTurn,
        MenuOpen,
        MenuClose,
        Confirm,
        Cancel,
        Error,
        Success,
        Warning,
        Notification,
        Typing,
        Select,
        Deselect,
        DragStart,
        DragEnd,
        Drop,
        Invalid
    }
    
    /// <summary>
    /// Types of UI notifications
    /// </summary>
    public enum UINotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Achievement,
        LevelUp,
        Reward,
        Message,
        System
    }
    
    /// <summary>
    /// Haptic feedback intensity levels
    /// </summary>
    public enum HapticIntensity
    {
        Light,
        Medium,
        Heavy
    }
    
    /// <summary>
    /// Accessibility settings for UI audio
    /// </summary>
    public class AccessibilitySettings
    {
        public bool EnhancedUIFeedback { get; set; }
        public bool SimplifiedAudio { get; set; }
        public bool VisualOnlyMode { get; set; }
        public float UIVolumeBoost { get; set; } = 1f;
    }
    
    /// <summary>
    /// Preset UI sound schemes
    /// </summary>
    public class UISoundScheme
    {
        public string Name { get; set; }
        public Dictionary<UIInteractionType, string> SoundMappings { get; set; } = new Dictionary<UIInteractionType, string>();
        
        /// <summary>
        /// Creates a default UI sound scheme
        /// </summary>
        public static UISoundScheme CreateDefault()
        {
            return new UISoundScheme
            {
                Name = "Default",
                SoundMappings = new Dictionary<UIInteractionType, string>
                {
                    { UIInteractionType.ButtonClick, "ui_button_click" },
                    { UIInteractionType.ButtonHover, "ui_button_hover" },
                    { UIInteractionType.Toggle, "ui_toggle" },
                    { UIInteractionType.Slider, "ui_slider" },
                    { UIInteractionType.MenuOpen, "ui_menu_open" },
                    { UIInteractionType.MenuClose, "ui_menu_close" },
                    { UIInteractionType.Confirm, "ui_confirm" },
                    { UIInteractionType.Cancel, "ui_cancel" },
                    { UIInteractionType.Error, "ui_error" },
                    { UIInteractionType.Success, "ui_success" },
                    { UIInteractionType.Warning, "ui_warning" },
                    { UIInteractionType.Notification, "ui_notification" }
                }
            };
        }
    }
}
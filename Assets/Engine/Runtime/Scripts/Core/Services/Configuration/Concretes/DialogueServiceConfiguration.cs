using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Dialogue Service focusing on core dialogue functionality,
    /// text display, voice integration, and system behavior
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueServiceConfiguration", menuName = "Engine/Services/DialogueServiceConfiguration", order = 4)]
    public class DialogueServiceConfiguration : ServiceConfigurationBase
    {
        #region Text Display Settings
        
        [Header("Text Display")]
        [SerializeField, Range(1f, 100f)]
        [Tooltip("Default typing speed in characters per second")]
        private float _defaultTypingSpeed = 30f;
        
        [SerializeField]
        [Tooltip("Enable rich text markup support (bold, italic, color, etc.)")]
        private bool _enableRichText = true;
        
        [SerializeField]
        [Tooltip("Enable text animation effects")]
        private bool _enableTextAnimations = true;
        
        [SerializeField]
        [Tooltip("Default text animation type")]
        private TextAnimationType _defaultAnimationType = TextAnimationType.Typewriter;
        
        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Minimum time between character reveals (prevents too fast typing)")]
        private float _minimumCharacterDelay = 0.01f;
        
        [SerializeField]
        [Tooltip("Allow players to skip text animations")]
        private bool _allowTextSkipping = true;
        
        #endregion
        
        #region Voice Integration Settings
        
        [Header("Voice Integration")]
        [SerializeField]
        [Tooltip("Enable voice playback with dialogue")]
        private bool _enableVoicePlayback = true;
        
        [SerializeField]
        [Tooltip("Automatically advance dialogue after voice completes")]
        private bool _autoAdvanceAfterVoice = true;
        
        [SerializeField, Range(0f, 5f)]
        [Tooltip("Additional delay after voice before auto-advancing")]
        private float _voiceAutoAdvanceDelay = 0.5f;
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default voice volume for dialogue")]
        private float _defaultVoiceVolume = 0.8f;
        
        [SerializeField]
        [Tooltip("Base path for voice audio resources")]
        private string _voiceResourceBasePath = "Audio/Voice";
        
        [SerializeField]
        [Tooltip("Enable voice ducking (lower other audio during voice)")]
        private bool _enableVoiceDucking = true;
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Volume reduction for other audio during voice")]
        private float _voiceDuckingLevel = 0.3f;
        
        #endregion
        
        #region Auto-Advance Settings
        
        [Header("Auto-Advance")]
        [SerializeField]
        [Tooltip("Default auto-advance mode")]
        private AutoAdvanceMode _defaultAutoAdvanceMode = AutoAdvanceMode.Never;
        
        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("Default delay before auto-advancing (seconds)")]
        private float _defaultAutoAdvanceDelay = 2f;
        
        [SerializeField]
        [Tooltip("Allow players to toggle auto-advance mode")]
        private bool _allowAutoAdvanceToggle = true;
        
        [SerializeField]
        [Tooltip("Show auto-advance indicator in UI")]
        private bool _showAutoAdvanceIndicator = true;
        
        #endregion
        
        #region Choice System Settings
        
        [Header("Choice System")]
        [SerializeField, Range(2, 10)]
        [Tooltip("Maximum number of choices that can be displayed")]
        private int _maxChoiceCount = 6;
        
        [SerializeField, Range(1f, 30f)]
        [Tooltip("Time limit for player choices (0 = no limit)")]
        private float _choiceTimeLimit = 0f;
        
        [SerializeField]
        [Tooltip("Default choice when time limit expires")]
        private int _defaultTimeLimitChoice = 0;
        
        [SerializeField]
        [Tooltip("Highlight previously selected choices")]
        private bool _highlightPreviousChoices = true;
        
        [SerializeField]
        [Tooltip("Color for previously selected choices")]
        private Color _previousChoiceColor = new Color(0.7f, 0.7f, 0.7f);
        
        #endregion
        
        #region History System Settings
        
        [Header("History System")]
        [SerializeField, Range(10, 1000)]
        [Tooltip("Maximum number of dialogue lines to keep in history")]
        private int _maxHistoryLines = 100;
        
        [SerializeField]
        [Tooltip("Enable dialogue history/backlog functionality")]
        private bool _enableDialogueHistory = true;
        
        [SerializeField]
        [Tooltip("Allow navigation back to previous dialogue lines")]
        private bool _allowHistoryNavigation = true;
        
        [SerializeField]
        [Tooltip("Save dialogue history between game sessions")]
        private bool _persistHistoryBetweenSessions = false;
        
        [SerializeField]
        [Tooltip("Include narration in dialogue history")]
        private bool _includeNarrationInHistory = true;
        
        [SerializeField]
        [Tooltip("Include thoughts in dialogue history")]
        private bool _includeThoughtsInHistory = true;
        
        #endregion
        
        #region Character Display Settings
        
        [Header("Character Display")]
        [SerializeField]
        [Tooltip("Default color for character names")]
        private Color _defaultCharacterNameColor = Color.white;
        
        [SerializeField]
        [Tooltip("Default color for dialogue text")]
        private Color _defaultDialogueTextColor = Color.white;
        
        [SerializeField]
        [Tooltip("Color for narrator text")]
        private Color _narratorTextColor = new Color(0.9f, 0.9f, 0.9f);
        
        [SerializeField]
        [Tooltip("Color for thought text")]
        private Color _thoughtTextColor = new Color(0.8f, 0.8f, 1f);
        
        [SerializeField]
        [Tooltip("Font size multiplier for character names")]
        private float _characterNameFontScale = 1.2f;
        
        [SerializeField]
        [Tooltip("Enable character emotion integration with ActorService")]
        private bool _enableEmotionIntegration = true;
        
        #endregion
        
        #region Performance Settings
        
        [Header("Performance")]
        [SerializeField]
        [Tooltip("Enable text pooling for better performance")]
        private bool _enableTextPooling = true;
        
        [SerializeField, Range(5, 50)]
        [Tooltip("Initial size of text element pool")]
        private int _textPoolInitialSize = 10;
        
        [SerializeField]
        [Tooltip("Enable async text processing")]
        private bool _enableAsyncTextProcessing = true;
        
        [SerializeField, Range(1, 20)]
        [Tooltip("Maximum concurrent text animations")]
        private int _maxConcurrentAnimations = 3;
        
        [SerializeField]
        [Tooltip("Unload unused voice clips automatically")]
        private bool _autoUnloadVoiceClips = true;
        
        #endregion
        
        #region Debug Settings
        
        [Header("Debug & Development")]
        [SerializeField]
        [Tooltip("Enable dialogue system debugging")]
        private bool _enableDebugMode = false;
        
        [SerializeField]
        [Tooltip("Log dialogue operations to console")]
        private bool _logDialogueOperations = false;
        
        [SerializeField]
        [Tooltip("Show dialogue timing information")]
        private bool _showTimingDebugInfo = false;
        
        [SerializeField]
        [Tooltip("Enable dialogue system profiling")]
        private bool _enableProfiling = false;
        
        [SerializeField]
        [Tooltip("Skip dialogue validation on startup")]
        private bool _skipValidationOnStartup = false;
        
        #endregion
        
        #region Public Properties
        
        // Text Display
        public float DefaultTypingSpeed => _defaultTypingSpeed;
        public bool EnableRichText => _enableRichText;
        public bool EnableTextAnimations => _enableTextAnimations;
        public TextAnimationType DefaultAnimationType => _defaultAnimationType;
        public float MinimumCharacterDelay => _minimumCharacterDelay;
        public bool AllowTextSkipping => _allowTextSkipping;
        
        // Voice Integration
        public bool EnableVoicePlayback => _enableVoicePlayback;
        public bool AutoAdvanceAfterVoice => _autoAdvanceAfterVoice;
        public float VoiceAutoAdvanceDelay => _voiceAutoAdvanceDelay;
        public float DefaultVoiceVolume => _defaultVoiceVolume;
        public string VoiceResourceBasePath => _voiceResourceBasePath;
        public bool EnableVoiceDucking => _enableVoiceDucking;
        public float VoiceDuckingLevel => _voiceDuckingLevel;
        
        // Auto-Advance
        public AutoAdvanceMode DefaultAutoAdvanceMode => _defaultAutoAdvanceMode;
        public float DefaultAutoAdvanceDelay => _defaultAutoAdvanceDelay;
        public bool AllowAutoAdvanceToggle => _allowAutoAdvanceToggle;
        public bool ShowAutoAdvanceIndicator => _showAutoAdvanceIndicator;
        
        // Choice System
        public int MaxChoiceCount => _maxChoiceCount;
        public float ChoiceTimeLimit => _choiceTimeLimit;
        public int DefaultTimeLimitChoice => _defaultTimeLimitChoice;
        public bool HighlightPreviousChoices => _highlightPreviousChoices;
        public Color PreviousChoiceColor => _previousChoiceColor;
        
        // History System
        public int MaxHistoryLines => _maxHistoryLines;
        public bool EnableDialogueHistory => _enableDialogueHistory;
        public bool AllowHistoryNavigation => _allowHistoryNavigation;
        public bool PersistHistoryBetweenSessions => _persistHistoryBetweenSessions;
        public bool IncludeNarrationInHistory => _includeNarrationInHistory;
        public bool IncludeThoughtsInHistory => _includeThoughtsInHistory;
        
        // Character Display
        public Color DefaultCharacterNameColor => _defaultCharacterNameColor;
        public Color DefaultDialogueTextColor => _defaultDialogueTextColor;
        public Color NarratorTextColor => _narratorTextColor;
        public Color ThoughtTextColor => _thoughtTextColor;
        public float CharacterNameFontScale => _characterNameFontScale;
        public bool EnableEmotionIntegration => _enableEmotionIntegration;
        
        // Performance
        public bool EnableTextPooling => _enableTextPooling;
        public int TextPoolInitialSize => _textPoolInitialSize;
        public bool EnableAsyncTextProcessing => _enableAsyncTextProcessing;
        public int MaxConcurrentAnimations => _maxConcurrentAnimations;
        public bool AutoUnloadVoiceClips => _autoUnloadVoiceClips;
        
        // Debug
        public bool EnableDebugMode => _enableDebugMode;
        public bool LogDialogueOperations => _logDialogueOperations;
        public bool ShowTimingDebugInfo => _showTimingDebugInfo;
        public bool EnableProfiling => _enableProfiling;
        public bool SkipValidationOnStartup => _skipValidationOnStartup;
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets the voice resource path for a specific voice ID
        /// </summary>
        public string GetVoiceResourcePath(string voiceId)
        {
            return string.IsNullOrEmpty(voiceId) ? string.Empty : $"{_voiceResourceBasePath}/{voiceId}";
        }
        
        /// <summary>
        /// Gets the color for a specific dialogue type
        /// </summary>
        public Color GetColorForDialogueType(DialogueType type)
        {
            return type switch
            {
                DialogueType.Speech => _defaultDialogueTextColor,
                DialogueType.Thought => _thoughtTextColor,
                DialogueType.Narration => _narratorTextColor,
                DialogueType.System => Color.yellow,
                _ => _defaultDialogueTextColor
            };
        }
        
        /// <summary>
        /// Validates if the typing speed is within acceptable bounds
        /// </summary>
        public bool IsValidTypingSpeed(float speed)
        {
            return speed >= 1f && speed <= 100f;
        }
        
        #endregion
        
        #region Validation
        
        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;
            
            // Validate typing speed
            if (_defaultTypingSpeed <= 0)
            {
                errors.Add("DefaultTypingSpeed must be greater than 0");
                isValid = false;
            }
            
            // Validate minimum character delay
            if (_minimumCharacterDelay <= 0)
            {
                errors.Add("MinimumCharacterDelay must be greater than 0");
                isValid = false;
            }
            
            // Validate auto-advance delay
            if (_defaultAutoAdvanceDelay <= 0)
            {
                errors.Add("DefaultAutoAdvanceDelay must be greater than 0");
                isValid = false;
            }
            
            // Validate choice count
            if (_maxChoiceCount < 2)
            {
                errors.Add("MaxChoiceCount must be at least 2");
                isValid = false;
            }
            
            // Validate history lines
            if (_maxHistoryLines <= 0)
            {
                errors.Add("MaxHistoryLines must be greater than 0");
                isValid = false;
            }
            
            // Validate voice resource path
            if (_enableVoicePlayback && string.IsNullOrEmpty(_voiceResourceBasePath))
            {
                errors.Add("VoiceResourceBasePath is required when voice playback is enabled");
                isValid = false;
            }
            
            // Validate pool settings
            if (_enableTextPooling && _textPoolInitialSize <= 0)
            {
                errors.Add("TextPoolInitialSize must be greater than 0 when pooling is enabled");
                isValid = false;
            }
            
            return isValid;
        }
        
        protected override void OnResetToDefaults()
        {
            // Text Display defaults
            _defaultTypingSpeed = 30f;
            _enableRichText = true;
            _enableTextAnimations = true;
            _defaultAnimationType = TextAnimationType.Typewriter;
            _minimumCharacterDelay = 0.01f;
            _allowTextSkipping = true;
            
            // Voice Integration defaults
            _enableVoicePlayback = true;
            _autoAdvanceAfterVoice = true;
            _voiceAutoAdvanceDelay = 0.5f;
            _defaultVoiceVolume = 0.8f;
            _voiceResourceBasePath = "Audio/Voice";
            _enableVoiceDucking = true;
            _voiceDuckingLevel = 0.3f;
            
            // Auto-Advance defaults
            _defaultAutoAdvanceMode = AutoAdvanceMode.Never;
            _defaultAutoAdvanceDelay = 2f;
            _allowAutoAdvanceToggle = true;
            _showAutoAdvanceIndicator = true;
            
            // Choice System defaults
            _maxChoiceCount = 6;
            _choiceTimeLimit = 0f;
            _defaultTimeLimitChoice = 0;
            _highlightPreviousChoices = true;
            _previousChoiceColor = new Color(0.7f, 0.7f, 0.7f);
            
            // History System defaults
            _maxHistoryLines = 100;
            _enableDialogueHistory = true;
            _allowHistoryNavigation = true;
            _persistHistoryBetweenSessions = false;
            _includeNarrationInHistory = true;
            _includeThoughtsInHistory = true;
            
            // Character Display defaults
            _defaultCharacterNameColor = Color.white;
            _defaultDialogueTextColor = Color.white;
            _narratorTextColor = new Color(0.9f, 0.9f, 0.9f);
            _thoughtTextColor = new Color(0.8f, 0.8f, 1f);
            _characterNameFontScale = 1.2f;
            _enableEmotionIntegration = true;
            
            // Performance defaults
            _enableTextPooling = true;
            _textPoolInitialSize = 10;
            _enableAsyncTextProcessing = true;
            _maxConcurrentAnimations = 3;
            _autoUnloadVoiceClips = true;
            
            // Debug defaults
            _enableDebugMode = false;
            _logDialogueOperations = false;
            _showTimingDebugInfo = false;
            _enableProfiling = false;
            _skipValidationOnStartup = false;
        }
        
        /// <summary>
        /// Gets a summary of the current dialogue configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"DialogueService Config: {_defaultTypingSpeed} cps, " +
                   $"voice {(_enableVoicePlayback ? "enabled" : "disabled")}, " +
                   $"auto-advance {_defaultAutoAdvanceMode}, " +
                   $"history {(_enableDialogueHistory ? _maxHistoryLines + " lines" : "disabled")}, " +
                   $"rich text {(_enableRichText ? "enabled" : "disabled")}";
        }
        
        #endregion
    }
}
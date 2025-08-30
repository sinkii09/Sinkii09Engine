using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    #region UI Integration Classes
    
    /// <summary>
    /// Context for displaying dialogue on UI screens
    /// </summary>
    public class DialogueScreenContext : UIScreenContext
    {
        public DialogueLine Line { get; set; }
        public DialogueChoice[] Choices { get; set; }
        public bool AutoShow { get; set; } = true;
    }
    
    #endregion
    
    #region Core Dialogue Data Structures
    
    /// <summary>
    /// Represents a single line of dialogue in the conversation
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        #region Properties
        
        /// <summary>
        /// Unique identifier for this dialogue line
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Character speaking this line (null for narration)
        /// </summary>
        public string SpeakerId { get; set; }
        
        /// <summary>
        /// The actual dialogue text
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Type of dialogue (speech, thought, narration)
        /// </summary>
        public DialogueType Type { get; set; } = DialogueType.Speech;
        
        /// <summary>
        /// Voice clip identifier for audio playback
        /// </summary>
        public string VoiceId { get; set; }
        
        /// <summary>
        /// Timestamp when this line was shown
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Metadata for this dialogue line
        /// </summary>
        public DialogueMetadata Metadata { get; set; } = new DialogueMetadata();
        
        #endregion
        
        #region Constructors
        
        public DialogueLine() { }
        
        public DialogueLine(string speakerId, string text, DialogueType type = DialogueType.Speech)
        {
            SpeakerId = speakerId;
            Text = text;
            Type = type;
        }
        
        public DialogueLine(string speakerId, string text, string voiceId, DialogueType type = DialogueType.Speech)
        {
            SpeakerId = speakerId;
            Text = text;
            VoiceId = voiceId;
            Type = type;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Creates a copy of this dialogue line
        /// </summary>
        public DialogueLine Clone()
        {
            return new DialogueLine
            {
                Id = this.Id,
                SpeakerId = this.SpeakerId,
                Text = this.Text,
                Type = this.Type,
                VoiceId = this.VoiceId,
                Timestamp = this.Timestamp,
                Metadata = this.Metadata?.Clone()
            };
        }
        
        /// <summary>
        /// Gets the display name for the speaker
        /// </summary>
        public string GetDisplayName()
        {
            if (Type == DialogueType.Narration) return string.Empty;
            return Metadata?.DisplayName ?? SpeakerId ?? "Unknown";
        }
        
        /// <summary>
        /// Gets the color for this dialogue line
        /// </summary>
        public Color GetTextColor()
        {
            return Metadata?.TextColor ?? Color.white;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Represents a player choice option
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        #region Properties
        
        /// <summary>
        /// Unique identifier for this choice
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// The text displayed to the player
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Whether this choice can be selected
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Whether this choice has been selected before
        /// </summary>
        public bool HasBeenSelected { get; set; } = false;
        
        /// <summary>
        /// Color for the choice text
        /// </summary>
        public Color TextColor { get; set; } = Color.white;
        
        /// <summary>
        /// Color for disabled choice text
        /// </summary>
        public Color DisabledColor { get; set; } = Color.gray;
        
        /// <summary>
        /// Custom data associated with this choice
        /// </summary>
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
        
        #endregion
        
        #region Constructors
        
        public DialogueChoice() { }
        
        public DialogueChoice(string text, bool isEnabled = true)
        {
            Text = text;
            IsEnabled = isEnabled;
        }
        
        public DialogueChoice(string text, Color textColor, bool isEnabled = true)
        {
            Text = text;
            TextColor = textColor;
            IsEnabled = isEnabled;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets the effective text color based on enabled state
        /// </summary>
        public Color GetEffectiveTextColor()
        {
            return IsEnabled ? TextColor : DisabledColor;
        }
        
        /// <summary>
        /// Marks this choice as selected
        /// </summary>
        public void MarkAsSelected()
        {
            HasBeenSelected = true;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Metadata associated with dialogue lines
    /// </summary>
    [Serializable]
    public class DialogueMetadata
    {
        #region Display Properties
        
        /// <summary>
        /// Display name for the speaker (overrides character ID)
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// Color for the dialogue text
        /// </summary>
        public Color TextColor { get; set; } = Color.white;
        
        /// <summary>
        /// Color for the speaker name
        /// </summary>
        public Color NameColor { get; set; } = Color.white;
        
        /// <summary>
        /// Font size multiplier for this line
        /// </summary>
        public float FontSizeMultiplier { get; set; } = 1.0f;
        
        #endregion
        
        #region Animation Properties
        
        /// <summary>
        /// Custom typing speed for this line (characters per second)
        /// </summary>
        public float? CustomTypingSpeed { get; set; }
        
        /// <summary>
        /// Delay before showing this line (in seconds)
        /// </summary>
        public float PreDelay { get; set; } = 0f;
        
        /// <summary>
        /// Delay after showing this line (in seconds)
        /// </summary>
        public float PostDelay { get; set; } = 0f;
        
        /// <summary>
        /// Whether this line can be skipped
        /// </summary>
        public bool CanSkip { get; set; } = true;
        
        #endregion
        
        #region Character Properties
        
        /// <summary>
        /// Character emotion for this line
        /// </summary>
        public string Emotion { get; set; }
        
        /// <summary>
        /// Character pose for this line
        /// </summary>
        public string Pose { get; set; }
        
        /// <summary>
        /// Character position for this line
        /// </summary>
        public string Position { get; set; }
        
        #endregion
        
        #region Audio Properties
        
        /// <summary>
        /// Volume multiplier for voice playback
        /// </summary>
        public float VoiceVolume { get; set; } = 1.0f;
        
        /// <summary>
        /// Whether to auto-advance after voice completes
        /// </summary>
        public bool AutoAdvanceAfterVoice { get; set; } = true;
        
        #endregion
        
        #region Custom Data
        
        /// <summary>
        /// Custom properties for extending dialogue functionality
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Creates a copy of this metadata
        /// </summary>
        public DialogueMetadata Clone()
        {
            return new DialogueMetadata
            {
                DisplayName = this.DisplayName,
                TextColor = this.TextColor,
                NameColor = this.NameColor,
                FontSizeMultiplier = this.FontSizeMultiplier,
                CustomTypingSpeed = this.CustomTypingSpeed,
                PreDelay = this.PreDelay,
                PostDelay = this.PostDelay,
                CanSkip = this.CanSkip,
                Emotion = this.Emotion,
                Pose = this.Pose,
                Position = this.Position,
                VoiceVolume = this.VoiceVolume,
                AutoAdvanceAfterVoice = this.AutoAdvanceAfterVoice,
                CustomProperties = new Dictionary<string, object>(this.CustomProperties)
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// Current state of the dialogue system
    /// </summary>
    [Serializable]
    public class DialogueState
    {
        #region Playback State
        
        /// <summary>
        /// Whether dialogue is currently active
        /// </summary>
        public bool IsActive { get; set; } = false;
        
        /// <summary>
        /// Whether text is currently animating
        /// </summary>
        public bool IsTextAnimating { get; set; } = false;
        
        /// <summary>
        /// Whether waiting for player choice
        /// </summary>
        public bool IsWaitingForChoice { get; set; } = false;
        
        /// <summary>
        /// Whether dialogue is paused
        /// </summary>
        public bool IsPaused { get; set; } = false;
        
        /// <summary>
        /// Currently displayed dialogue line
        /// </summary>
        public DialogueLine CurrentLine { get; set; }
        
        /// <summary>
        /// Current choice options (if showing choices)
        /// </summary>
        public DialogueChoice[] CurrentChoices { get; set; }
        
        #endregion
        
        #region Settings
        
        /// <summary>
        /// Current text display speed (characters per second)
        /// </summary>
        public float TextSpeed { get; set; } = 30f;
        
        /// <summary>
        /// Whether auto-advance is enabled
        /// </summary>
        public bool AutoAdvanceEnabled { get; set; } = false;
        
        /// <summary>
        /// Auto-advance delay in seconds
        /// </summary>
        public float AutoAdvanceDelay { get; set; } = 2f;
        
        /// <summary>
        /// Voice volume level
        /// </summary>
        public float VoiceVolume { get; set; } = 1f;
        
        #endregion
        
        #region Character Data
        
        /// <summary>
        /// Character display name mappings
        /// </summary>
        public Dictionary<string, string> CharacterDisplayNames { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Character color mappings
        /// </summary>
        public Dictionary<string, Color> CharacterColors { get; set; } = new Dictionary<string, Color>();
        
        #endregion
        
        #region History
        
        /// <summary>
        /// Complete dialogue history
        /// </summary>
        public List<DialogueLine> DialogueHistory { get; set; } = new List<DialogueLine>();
        
        /// <summary>
        /// Maximum number of lines to keep in history
        /// </summary>
        public int MaxHistoryLines { get; set; } = 100;
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Creates a copy of this dialogue state
        /// </summary>
        public DialogueState Clone()
        {
            return new DialogueState
            {
                IsActive = this.IsActive,
                IsTextAnimating = this.IsTextAnimating,
                IsWaitingForChoice = this.IsWaitingForChoice,
                IsPaused = this.IsPaused,
                CurrentLine = this.CurrentLine?.Clone(),
                CurrentChoices = this.CurrentChoices?.Clone() as DialogueChoice[],
                TextSpeed = this.TextSpeed,
                AutoAdvanceEnabled = this.AutoAdvanceEnabled,
                AutoAdvanceDelay = this.AutoAdvanceDelay,
                VoiceVolume = this.VoiceVolume,
                CharacterDisplayNames = new Dictionary<string, string>(this.CharacterDisplayNames),
                CharacterColors = new Dictionary<string, Color>(this.CharacterColors),
                DialogueHistory = new List<DialogueLine>(this.DialogueHistory),
                MaxHistoryLines = this.MaxHistoryLines
            };
        }
        
        /// <summary>
        /// Resets the dialogue state to defaults
        /// </summary>
        public void Reset()
        {
            IsActive = false;
            IsTextAnimating = false;
            IsWaitingForChoice = false;
            IsPaused = false;
            CurrentLine = null;
            CurrentChoices = null;
            TextSpeed = 30f;
            AutoAdvanceEnabled = false;
            AutoAdvanceDelay = 2f;
            VoiceVolume = 1f;
            CharacterDisplayNames.Clear();
            CharacterColors.Clear();
            DialogueHistory.Clear();
        }
        
        #endregion
    }
    
    #endregion
    
    #region Enums
    
    /// <summary>
    /// Types of dialogue content
    /// </summary>
    public enum DialogueType
    {
        /// <summary>
        /// Character speaking dialogue
        /// </summary>
        Speech = 0,
        
        /// <summary>
        /// Character internal thoughts
        /// </summary>
        Thought = 1,
        
        /// <summary>
        /// Narrator/descriptive text
        /// </summary>
        Narration = 2,
        
        /// <summary>
        /// System messages
        /// </summary>
        System = 3
    }
    
    /// <summary>
    /// Text animation types
    /// </summary>
    public enum TextAnimationType
    {
        /// <summary>
        /// No animation, instant display
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Classic typewriter effect
        /// </summary>
        Typewriter = 1,
        
        /// <summary>
        /// Fade in character by character
        /// </summary>
        FadeIn = 2,
        
        /// <summary>
        /// Wave effect while typing
        /// </summary>
        Wave = 3,
        
        /// <summary>
        /// Shake effect while typing
        /// </summary>
        Shake = 4
    }
    
    /// <summary>
    /// Auto-advance modes
    /// </summary>
    public enum AutoAdvanceMode
    {
        /// <summary>
        /// Never auto-advance
        /// </summary>
        Never = 0,
        
        /// <summary>
        /// Auto-advance after specified time
        /// </summary>
        Time = 1,
        
        /// <summary>
        /// Auto-advance after voice completes
        /// </summary>
        Voice = 2,
        
        /// <summary>
        /// Auto-advance after voice OR time (whichever comes first)
        /// </summary>
        VoiceOrTime = 3
    }
    
    #endregion
    
    #region Save Data Structures
    
    /// <summary>
    /// Save data structure for dialogue history persistence
    /// </summary>
    [Serializable]
    public class DialogueHistorySaveData : SaveData
    {
        /// <summary>
        /// The dialogue history to save
        /// </summary>
        public List<DialogueLine> History { get; set; } = new List<DialogueLine>();
        
        protected override int GetCurrentVersion()
        {
            return Version;
        }

        protected override bool ValidateData()
        {
            // Check if history exists
            if (History == null)
            {
                History = new List<DialogueLine>();
                return false;
            }
            
            // Validate each dialogue line
            for (int i = History.Count - 1; i >= 0; i--)
            {
                var line = History[i];
                
                // Remove null entries
                if (line == null)
                {
                    History.RemoveAt(i);
                    continue;
                }
                
                // Validate required fields
                if (string.IsNullOrEmpty(line.Text))
                {
                    History.RemoveAt(i);
                    continue;
                }
                
                // Ensure metadata exists
                if (line.Metadata == null)
                {
                    line.Metadata = new DialogueMetadata();
                }
                
                // Validate dialogue type is within enum range
                if (!Enum.IsDefined(typeof(DialogueType), line.Type))
                {
                    line.Type = DialogueType.Speech;
                }
            }
            return true;
        }
    }
    
    #endregion
    
    #region Extension Methods
    
    /// <summary>
    /// Extension methods for dialogue types
    /// </summary>
    public static class DialogueExtensions
    {
        /// <summary>
        /// Checks if the dialogue type represents character speech
        /// </summary>
        public static bool IsCharacterSpeech(this DialogueType type)
        {
            return type == DialogueType.Speech || type == DialogueType.Thought;
        }
        
        /// <summary>
        /// Checks if the dialogue type should show a speaker name
        /// </summary>
        public static bool ShowsSpeakerName(this DialogueType type)
        {
            return type == DialogueType.Speech || type == DialogueType.Thought;
        }
        
        /// <summary>
        /// Gets the default text color for a dialogue type
        /// </summary>
        public static Color GetDefaultTextColor(this DialogueType type)
        {
            return type switch
            {
                DialogueType.Speech => Color.white,
                DialogueType.Thought => new Color(0.8f, 0.8f, 1f), // Light blue
                DialogueType.Narration => new Color(0.9f, 0.9f, 0.9f), // Light gray
                DialogueType.System => Color.yellow,
                _ => Color.white
            };
        }
    }
    
    #endregion
}
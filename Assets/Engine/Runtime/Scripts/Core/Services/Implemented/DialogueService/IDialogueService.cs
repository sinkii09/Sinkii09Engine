using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core dialogue service interface for managing character conversations,
    /// player choices, and narrative progression in visual novel style games
    /// </summary>
    public interface IDialogueService : IEngineService
    {
        #region Core Dialogue Display
        
        /// <summary>
        /// Shows a dialogue line from a character with optional voice
        /// </summary>
        /// <param name="speakerId">Character identifier (e.g., "alice", "protagonist")</param>
        /// <param name="text">The dialogue text to display</param>
        /// <param name="voiceId">Optional voice clip identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when dialogue finishes or is skipped</returns>
        UniTask ShowDialogueAsync(string speakerId, string text, string voiceId = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Shows narrator text without a specific speaker
        /// </summary>
        /// <param name="text">The narration text to display</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when narration finishes</returns>
        UniTask ShowNarrationAsync(string text, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Shows character thoughts (internal monologue)
        /// </summary>
        /// <param name="speakerId">Character identifier</param>
        /// <param name="text">The thought text to display</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when thought finishes</returns>
        UniTask ShowThoughtAsync(string speakerId, string text, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Player Choices
        
        /// <summary>
        /// Presents choices to the player and waits for selection
        /// </summary>
        /// <param name="choices">Array of choice options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Selected choice index (0-based)</returns>
        UniTask<int> ShowChoicesAsync(DialogueChoice[] choices, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Presents choices with a prompt/question
        /// </summary>
        /// <param name="prompt">Question or prompt text</param>
        /// <param name="choices">Array of choice options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Selected choice index (0-based)</returns>
        UniTask<int> ShowChoicesWithPromptAsync(string prompt, DialogueChoice[] choices, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Selects a choice by index (typically called by UI components)
        /// </summary>
        /// <param name="choiceIndex">Index of the choice to select</param>
        void SelectChoice(int choiceIndex);
        
        #endregion
        
        #region Text Effects and Display Control
        
        /// <summary>
        /// Shows text with typewriter effect at specified speed
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="charactersPerSecond">Typing speed in characters per second</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when text is fully displayed</returns>
        UniTask ShowTextWithTypewriterAsync(string text, float charactersPerSecond = 30f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Skips current text animation and shows full text immediately
        /// </summary>
        void SkipCurrentText();
        
        /// <summary>
        /// Sets the default text display speed
        /// </summary>
        /// <param name="charactersPerSecond">New speed in characters per second</param>
        void SetTextSpeed(float charactersPerSecond);
        
        #endregion
        
        #region Auto-Advance and Timing Control
        
        /// <summary>
        /// Enables or disables auto-advance mode
        /// </summary>
        /// <param name="enabled">Whether auto-advance should be active</param>
        /// <param name="delaySeconds">Delay before auto-advancing (default: 2 seconds)</param>
        void SetAutoAdvance(bool enabled, float delaySeconds = 2f);
        
        /// <summary>
        /// Pauses dialogue execution
        /// </summary>
        void PauseDialogue();
        
        /// <summary>
        /// Resumes paused dialogue
        /// </summary>
        void ResumeDialogue();
        
        /// <summary>
        /// Waits for specified duration during dialogue
        /// </summary>
        /// <param name="seconds">Duration to wait</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes after the wait</returns>
        UniTask WaitAsync(float seconds, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Dialogue History and Navigation
        
        /// <summary>
        /// Adds a dialogue line to history
        /// </summary>
        /// <param name="line">Dialogue line to add</param>
        void AddToHistory(DialogueLine line);
        
        /// <summary>
        /// Gets the complete dialogue history
        /// </summary>
        /// <returns>Read-only collection of dialogue history</returns>
        IReadOnlyList<DialogueLine> GetDialogueHistory();
        
        /// <summary>
        /// Shows the dialogue history screen
        /// </summary>
        UniTask ShowHistoryAsync();
        
        /// <summary>
        /// Clears all dialogue history
        /// </summary>
        void ClearHistory();
        
        /// <summary>
        /// Goes back to previous dialogue line (if available)
        /// </summary>
        /// <returns>True if navigation was successful</returns>
        bool GoToPreviousLine();
        
        /// <summary>
        /// Fast forwards to current dialogue position
        /// </summary>
        void FastForwardToCurrent();
        
        #endregion
        
        #region Character and Speaker Management
        
        /// <summary>
        /// Sets the display name for a character ID
        /// </summary>
        /// <param name="characterId">Internal character identifier</param>
        /// <param name="displayName">Name to show in dialogue</param>
        void SetCharacterDisplayName(string characterId, string displayName);
        
        /// <summary>
        /// Sets the color for a character's dialogue
        /// </summary>
        /// <param name="characterId">Character identifier</param>
        /// <param name="color">Color for the character's text</param>
        void SetCharacterColor(string characterId, Color color);
        
        /// <summary>
        /// Gets the display name for a character
        /// </summary>
        /// <param name="characterId">Character identifier</param>
        /// <returns>Display name or the ID if no display name is set</returns>
        string GetCharacterDisplayName(string characterId);
        
        #endregion
        
        #region Voice and Audio Integration
        
        /// <summary>
        /// Plays voice for current dialogue with automatic text synchronization
        /// </summary>
        /// <param name="voiceId">Voice clip identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when voice finishes</returns>
        UniTask PlayVoiceAsync(string voiceId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops currently playing voice
        /// </summary>
        void StopVoice();
        
        /// <summary>
        /// Sets voice volume for dialogue
        /// </summary>
        /// <param name="volume">Volume level (0.0 to 1.0)</param>
        void SetVoiceVolume(float volume);
        
        #endregion
        
        #region State Management
        
        /// <summary>
        /// Gets current dialogue state for save/load operations
        /// </summary>
        /// <returns>Current dialogue state</returns>
        DialogueState GetCurrentState();
        
        /// <summary>
        /// Restores dialogue state from saved data
        /// </summary>
        /// <param name="state">State to restore</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask RestoreStateAsync(DialogueState state, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Resets dialogue system to initial state
        /// </summary>
        void ResetDialogueSystem();
        
        #endregion
        
        #region Status Properties
        
        /// <summary>
        /// Whether dialogue is currently active
        /// </summary>
        bool IsDialogueActive { get; }
        
        /// <summary>
        /// Whether text is currently being typed out
        /// </summary>
        bool IsTextAnimating { get; }
        
        /// <summary>
        /// Whether choices are currently being shown
        /// </summary>
        bool IsWaitingForChoice { get; }
        
        /// <summary>
        /// Whether auto-advance is currently enabled
        /// </summary>
        bool IsAutoAdvanceEnabled { get; }
        
        /// <summary>
        /// Whether dialogue is currently paused
        /// </summary>
        bool IsDialoguePaused { get; }
        
        /// <summary>
        /// Current dialogue line being displayed
        /// </summary>
        DialogueLine CurrentDialogueLine { get; }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a dialogue line starts displaying
        /// </summary>
        event Action<DialogueLine> OnDialogueStarted;
        
        /// <summary>
        /// Fired when a dialogue line finishes displaying
        /// </summary>
        event Action<DialogueLine> OnDialogueCompleted;
        
        /// <summary>
        /// Fired when text typing animation begins
        /// </summary>
        event Action<string> OnTextTypingStarted;
        
        /// <summary>
        /// Fired when text typing animation completes
        /// </summary>
        event Action<string> OnTextTypingCompleted;
        
        /// <summary>
        /// Fired when choices are presented to the player
        /// </summary>
        event Action<DialogueChoice[]> OnChoicesShown;
        
        /// <summary>
        /// Fired when player makes a choice selection
        /// </summary>
        event Action<int, DialogueChoice> OnChoiceSelected;
        
        /// <summary>
        /// Fired when voice playback starts
        /// </summary>
        event Action<string> OnVoiceStarted;
        
        /// <summary>
        /// Fired when voice playback completes
        /// </summary>
        event Action<string> OnVoiceCompleted;
        
        /// <summary>
        /// Fired when dialogue is paused
        /// </summary>
        event Action OnDialoguePaused;
        
        /// <summary>
        /// Fired when dialogue is resumed
        /// </summary>
        event Action OnDialogueResumed;
        
        /// <summary>
        /// Fired when auto-advance state changes
        /// </summary>
        event Action<bool> OnAutoAdvanceChanged;
        
        #endregion
    }
}
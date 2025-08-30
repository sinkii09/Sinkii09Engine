using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ZLinq;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core dialogue service implementation for visual novel style character conversations,
    /// text display with typewriter effects, voice synchronization, and player choice handling
    /// </summary>
    [EngineService(ServiceCategory.UserInterface, ServicePriority.High,
        Description = "Manages character dialogue, player choices, and narrative progression"
/*        RequiredServices = new[] { typeof(IUIService), typeof(IAudioService), typeof(IActorService), typeof(ISaveLoadService) }*/)]
    [ServiceConfiguration(typeof(DialogueServiceConfiguration))]
    public class DialogueService : IDialogueService
    {
        #region Dependencies
        
        private readonly DialogueServiceConfiguration _config;
        private IUIService _uiService;
        private IAudioService _audioService;
        private IActorService _actorService;
        private ISaveLoadService _saveLoadService;
        
        #endregion
        
        #region Service State
        
        private CancellationTokenSource _serviceCts;
        private bool _disposed = false;
        private bool _initialized = false;
        
        #endregion
        
        #region Dialogue State
        
        private List<DialogueLine> _dialogueHistory = new List<DialogueLine>();
        private readonly DialogueState _currentState = new DialogueState();
        private readonly Dictionary<string, string> _characterDisplayNames = new Dictionary<string, string>();
        private readonly Dictionary<string, Color> _characterColors = new Dictionary<string, Color>();
        
        #endregion
        
        #region Text Animation State
        
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeAnimations = new ConcurrentDictionary<string, CancellationTokenSource>();
        private CancellationTokenSource _currentTextAnimationCts;
        private bool _isTextAnimating = false;
        
        #endregion
        
        #region Choice and Voice State
        
        private DialogueChoice[] _currentChoices;
        private TaskCompletionSource<int> _choiceCompletionSource;
        private string _currentVoiceId;
        private CancellationTokenSource _currentVoiceCts;
        
        #endregion
        
        #region Auto-Advance State
        
        private CancellationTokenSource _autoAdvanceCts;
        private bool _autoAdvanceEnabled = false;
        private float _autoAdvanceDelay = 2f;
        
        #endregion
        
        #region Events Implementation
        
        public event Action<DialogueLine> OnDialogueStarted;
        public event Action<DialogueLine> OnDialogueCompleted;
        public event Action<string> OnTextTypingStarted;
        public event Action<string> OnTextTypingCompleted;
        public event Action<DialogueChoice[]> OnChoicesShown;
        public event Action<int, DialogueChoice> OnChoiceSelected;
        public event Action<string> OnVoiceStarted;
        public event Action<string> OnVoiceCompleted;
        public event Action OnDialoguePaused;
        public event Action OnDialogueResumed;
        public event Action<bool> OnAutoAdvanceChanged;
        
        #endregion
        
        #region Properties Implementation
        
        public bool IsDialogueActive => _currentState.IsActive;
        public bool IsTextAnimating => _isTextAnimating;
        public bool IsWaitingForChoice => _currentState.IsWaitingForChoice;
        public bool IsAutoAdvanceEnabled => _autoAdvanceEnabled;
        public bool IsDialoguePaused => _currentState.IsPaused;
        public DialogueLine CurrentDialogueLine => _currentState.CurrentLine;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes DialogueService with configuration
        /// </summary>
        public DialogueService(DialogueServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            // Initialize auto-advance settings
            _autoAdvanceEnabled = _config.DefaultAutoAdvanceMode != AutoAdvanceMode.Never;
            _autoAdvanceDelay = _config.DefaultAutoAdvanceDelay;
            
            // Initialize default text speed
            _currentState.TextSpeed = _config.DefaultTypingSpeed;
            _currentState.VoiceVolume = _config.DefaultVoiceVolume;
            _currentState.MaxHistoryLines = _config.MaxHistoryLines;
        }
        
        #endregion
        
        #region IEngineService Implementation
        
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                _serviceCts = new CancellationTokenSource();
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token).Token;
                
                // Resolve dependencies
                _uiService = provider.GetService(typeof(IUIService)) as IUIService;
                _audioService = provider.GetService(typeof(IAudioService)) as IAudioService;
                _actorService = provider.GetService(typeof(IActorService)) as IActorService;
                _saveLoadService = provider.GetService(typeof(ISaveLoadService)) as ISaveLoadService;
                
                // UIService is required for dialogue display
                if (_uiService == null)
                {
                    return ServiceInitializationResult.Failed("UIService dependency is required");
                }
                
                // Initialize character display settings
                InitializeCharacterSettings();
                
                // Load persisted history if enabled
                if (_config.PersistHistoryBetweenSessions && _saveLoadService != null)
                {
                    await LoadPersistedHistoryAsync(combinedToken);
                }
                
                _initialized = true;
                
                if (_config.LogDialogueOperations)
                    Debug.Log("[DialogueService] Successfully initialized");
                
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed($"DialogueService initialization failed: {ex.Message}");
            }
        }
        
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Cancel any active operations
                _serviceCts?.Cancel();
                
                // Stop current dialogue
                if (IsDialogueActive)
                {
                    await StopCurrentDialogueAsync();
                }
                
                // Save history if persistence enabled
                if (_config.PersistHistoryBetweenSessions && _saveLoadService != null)
                {
                    await SaveHistoryAsync(cancellationToken);
                }
                
                // Clean up resources
                CancelAllAnimations();
                _serviceCts?.Dispose();
                
                _initialized = false;
                _disposed = true;
                
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed($"DialogueService shutdown failed: {ex.Message}");
            }
        }
        
        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_disposed || !_initialized)
                {
                    return ServiceHealthStatus.Unhealthy("Service not initialized or disposed");
                }
                
                if (_uiService == null)
                {
                    return ServiceHealthStatus.Unhealthy("UIService dependency is missing");
                }
                
                // Check for excessive active animations
                if (_activeAnimations.Count > _config.MaxConcurrentAnimations * 2)
                {
                    return ServiceHealthStatus.Degraded($"High animation count: {_activeAnimations.Count}");
                }
                
                await UniTask.Yield(cancellationToken);
                return ServiceHealthStatus.Healthy();
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Core Dialogue Display Implementation
        
        public async UniTask ShowDialogueAsync(string speakerId, string text, string voiceId = null, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("DialogueService not initialized");
            
            try
            {
                var dialogueLine = new DialogueLine(speakerId, text, voiceId, DialogueType.Speech);
                await ShowDialogueLineAsync(dialogueLine, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogError($"[DialogueService] Error showing dialogue: {ex.Message}");
                throw;
            }
        }
        
        public async UniTask ShowNarrationAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("DialogueService not initialized");
            
            try
            {
                var dialogueLine = new DialogueLine(null, text, null, DialogueType.Narration);
                await ShowDialogueLineAsync(dialogueLine, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogError($"[DialogueService] Error showing narration: {ex.Message}");
                throw;
            }
        }
        
        public async UniTask ShowThoughtAsync(string speakerId, string text, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("DialogueService not initialized");
            
            try
            {
                var dialogueLine = new DialogueLine(speakerId, text, null, DialogueType.Thought);
                await ShowDialogueLineAsync(dialogueLine, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogError($"[DialogueService] Error showing thought: {ex.Message}");
                throw;
            }
        }
        
        private async UniTask ShowDialogueLineAsync(DialogueLine dialogueLine, CancellationToken cancellationToken = default)
        {
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token).Token;
            
            // Update current state
            _currentState.IsActive = true;
            _currentState.CurrentLine = dialogueLine;
            
            // Apply character colors and display names
            ApplyCharacterSettings(dialogueLine);
            
            // Add to history
            AddToHistory(dialogueLine);
            
            // Show dialogue UI if not already visible
            if (_uiService != null && !_uiService.IsScreenActive(UIScreenType.Dialog))
            {
                var context = new DialogueScreenContext 
                { 
                    Line = dialogueLine,
                    AutoShow = true 
                };
                await _uiService.ShowAsync(UIScreenType.Dialog, context, UIDisplayConfig.Overlay, combinedToken);
            }
            
            // Fire dialogue started event (DialogScreen will handle display)
            OnDialogueStarted?.Invoke(dialogueLine);
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Showing dialogue: {dialogueLine.GetDisplayName()}: {dialogueLine.Text}");
            
            // Handle pre-delay if specified
            if (dialogueLine.Metadata.PreDelay > 0)
            {
                await UniTask.Delay(Mathf.RoundToInt(dialogueLine.Metadata.PreDelay * 1000), cancellationToken: combinedToken);
            }
            
            // Start voice if specified
            UniTask voiceTask = UniTask.CompletedTask;
            if (!string.IsNullOrEmpty(dialogueLine.VoiceId) && _config.EnableVoicePlayback)
            {
                voiceTask = PlayVoiceAsync(dialogueLine.VoiceId, combinedToken);
            }
            
            // Show text with animation
            await ShowTextWithTypewriterAsync(
                dialogueLine.Text, 
                dialogueLine.Metadata.CustomTypingSpeed ?? _currentState.TextSpeed, 
                combinedToken
            );
            
            // Wait for voice to complete if needed
            if (!voiceTask.Status.IsCompleted())
            {
                await voiceTask;
            }
            
            // Handle post-delay if specified
            if (dialogueLine.Metadata.PostDelay > 0)
            {
                await UniTask.Delay(Mathf.RoundToInt(dialogueLine.Metadata.PostDelay * 1000), cancellationToken: combinedToken);
            }
            
            // Handle auto-advance
            if (ShouldAutoAdvance(dialogueLine))
            {
                await HandleAutoAdvanceAsync(combinedToken);
            }
            
            // Fire dialogue completed event
            OnDialogueCompleted?.Invoke(dialogueLine);
        }
        
        #endregion
        
        #region Player Choice Implementation
        
        public async UniTask<int> ShowChoicesAsync(DialogueChoice[] choices, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("DialogueService not initialized");
            
            if (choices == null || choices.Length == 0)
                throw new ArgumentException("Choices cannot be null or empty");
            
            if (choices.Length > _config.MaxChoiceCount)
                throw new ArgumentException($"Too many choices. Maximum allowed: {_config.MaxChoiceCount}");
            
            try
            {
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token).Token;
                
                _currentChoices = choices;
                _currentState.IsWaitingForChoice = true;
                _currentState.CurrentChoices = choices;
                _choiceCompletionSource = new TaskCompletionSource<int>();
                
                // Apply previous choice highlighting
                if (_config.HighlightPreviousChoices)
                {
                    ApplyPreviousChoiceHighlighting(choices);
                }
                
                OnChoicesShown?.Invoke(choices);
                
                if (_config.LogDialogueOperations)
                    Debug.Log($"[DialogueService] Showing {choices.Length} choices");
                
                // Handle choice time limit
                if (_config.ChoiceTimeLimit > 0)
                {
                    _ = HandleChoiceTimeLimitAsync(combinedToken);
                }
                
                // Wait for choice selection
                var selectedIndex = await _choiceCompletionSource.Task;
                
                // Mark choice as selected
                if (selectedIndex >= 0 && selectedIndex < choices.Length)
                {
                    choices[selectedIndex].MarkAsSelected();
                    OnChoiceSelected?.Invoke(selectedIndex, choices[selectedIndex]);
                    
                    if (_config.LogDialogueOperations)
                        Debug.Log($"[DialogueService] Choice selected: {selectedIndex} - {choices[selectedIndex].Text}");
                }
                
                _currentState.IsWaitingForChoice = false;
                _currentState.CurrentChoices = null;
                _currentChoices = null;
                
                return selectedIndex;
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogError($"[DialogueService] Error showing choices: {ex.Message}");
                throw;
            }
        }
        
        public async UniTask<int> ShowChoicesWithPromptAsync(string prompt, DialogueChoice[] choices, CancellationToken cancellationToken = default)
        {
            // Show the prompt first
            await ShowNarrationAsync(prompt, cancellationToken);
            
            // Then show choices
            return await ShowChoicesAsync(choices, cancellationToken);
        }
        
        /// <summary>
        /// Called externally (e.g., by UI or InputService) to select a choice
        /// </summary>
        public void SelectChoice(int choiceIndex)
        {
            if (!IsWaitingForChoice || _choiceCompletionSource == null)
                return;
            
            if (choiceIndex < 0 || choiceIndex >= _currentChoices?.Length)
                return;
            
            var choice = _currentChoices[choiceIndex];
            if (!choice.IsEnabled)
                return;
            
            _choiceCompletionSource?.TrySetResult(choiceIndex);
        }
        
        #endregion
        
        #region Text Animation Implementation
        
        public async UniTask ShowTextWithTypewriterAsync(string text, float charactersPerSecond = 30f, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                return;
            
            if (!_config.EnableTextAnimations || _config.DefaultAnimationType == TextAnimationType.None)
            {
                // Show text immediately
                OnTextTypingStarted?.Invoke(text);
                OnTextTypingCompleted?.Invoke(text);
                return;
            }
            
            try
            {
                _currentTextAnimationCts?.Cancel();
                _currentTextAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
                _isTextAnimating = true;
                
                OnTextTypingStarted?.Invoke(text);
                
                if (_config.LogDialogueOperations)
                    Debug.Log($"[DialogueService] Starting typewriter animation: {charactersPerSecond} cps");
                
                var delay = Mathf.Max(_config.MinimumCharacterDelay, 1f / charactersPerSecond);
                var delayMs = Mathf.RoundToInt(delay * 1000);
                
                for (int i = 0; i <= text.Length; i++)
                {
                    _currentTextAnimationCts.Token.ThrowIfCancellationRequested();
                    
                    // This would typically update UI text display
                    // For now, we'll just simulate the animation timing
                    
                    if (i < text.Length)
                    {
                        await UniTask.Delay(delayMs, cancellationToken: _currentTextAnimationCts.Token);
                    }
                }
                
                _isTextAnimating = false;
                OnTextTypingCompleted?.Invoke(text);
                
                if (_config.LogDialogueOperations)
                    Debug.Log("[DialogueService] Typewriter animation completed");
            }
            catch (OperationCanceledException)
            {
                _isTextAnimating = false;
                OnTextTypingCompleted?.Invoke(text);
                
                if (_config.LogDialogueOperations)
                    Debug.Log("[DialogueService] Typewriter animation cancelled");
            }
        }
        
        public void SkipCurrentText()
        {
            if (!_config.AllowTextSkipping || !_isTextAnimating)
                return;
            
            _currentTextAnimationCts?.Cancel();
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Text animation skipped");
        }
        
        public void SetTextSpeed(float charactersPerSecond)
        {
            _currentState.TextSpeed = Mathf.Clamp(charactersPerSecond, 1f, 100f);
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Text speed set to {_currentState.TextSpeed} cps");
        }
        
        #endregion
        
        #region Auto-Advance Implementation
        
        public void SetAutoAdvance(bool enabled, float delaySeconds = 2f)
        {
            var wasEnabled = _autoAdvanceEnabled;
            _autoAdvanceEnabled = enabled;
            _autoAdvanceDelay = delaySeconds;
            _currentState.AutoAdvanceEnabled = enabled;
            _currentState.AutoAdvanceDelay = delaySeconds;
            
            if (wasEnabled != enabled)
            {
                OnAutoAdvanceChanged?.Invoke(enabled);
                
                if (_config.LogDialogueOperations)
                    Debug.Log($"[DialogueService] Auto-advance {(enabled ? "enabled" : "disabled")} with {delaySeconds}s delay");
            }
        }
        
        private bool ShouldAutoAdvance(DialogueLine dialogueLine)
        {
            if (!_autoAdvanceEnabled)
                return false;
            
            return _config.DefaultAutoAdvanceMode switch
            {
                AutoAdvanceMode.Never => false,
                AutoAdvanceMode.Time => true,
                AutoAdvanceMode.Voice => !string.IsNullOrEmpty(dialogueLine.VoiceId),
                AutoAdvanceMode.VoiceOrTime => true,
                _ => false
            };
        }
        
        private async UniTask HandleAutoAdvanceAsync(CancellationToken cancellationToken)
        {
            try
            {
                _autoAdvanceCts?.Cancel();
                _autoAdvanceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
                
                var delay = _autoAdvanceDelay + _config.VoiceAutoAdvanceDelay;
                await UniTask.Delay(Mathf.RoundToInt(delay * 1000), cancellationToken: _autoAdvanceCts.Token);
                
                // Auto-advance logic would be handled by the calling script system
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }
        
        #endregion
        
        #region Voice Integration Implementation
        
        public async UniTask PlayVoiceAsync(string voiceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(voiceId) || _audioService == null || !_config.EnableVoicePlayback)
                return;
            
            try
            {
                _currentVoiceCts?.Cancel();
                _currentVoiceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
                _currentVoiceId = voiceId;
                
                OnVoiceStarted?.Invoke(voiceId);
                
                var voicePath = _config.GetVoiceResourcePath(voiceId);
                await _audioService.PlayVoiceAsync("dialogue", voiceId, _config.EnableVoiceDucking, _currentVoiceCts.Token);
                
                OnVoiceCompleted?.Invoke(voiceId);
                _currentVoiceId = null;
                
                if (_config.LogDialogueOperations)
                    Debug.Log($"[DialogueService] Voice completed: {voiceId}");
            }
            catch (OperationCanceledException)
            {
                OnVoiceCompleted?.Invoke(voiceId);
                _currentVoiceId = null;
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogError($"[DialogueService] Voice playback error: {ex.Message}");
            }
        }
        
        public void StopVoice()
        {
            if (string.IsNullOrEmpty(_currentVoiceId) || _audioService == null)
                return;
            
            _currentVoiceCts?.Cancel();
            _audioService.StopAsync(_currentVoiceId);
            _currentVoiceId = null;
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Voice stopped");
        }
        
        public void SetVoiceVolume(float volume)
        {
            _currentState.VoiceVolume = Mathf.Clamp01(volume);
            _audioService?.SetCategoryVolume(AudioCategory.Voice, _currentState.VoiceVolume);
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Voice volume set to {_currentState.VoiceVolume}");
        }
        
        #endregion
        
        #region Pause and Resume Implementation
        
        public void PauseDialogue()
        {
            if (_currentState.IsPaused)
                return;
            
            _currentState.IsPaused = true;
            
            // Pause text animation
            _currentTextAnimationCts?.Cancel();
            
            // Pause voice
            if (!string.IsNullOrEmpty(_currentVoiceId))
            {
                _audioService?.Pause(_currentVoiceId);
            }
            
            // Pause auto-advance
            _autoAdvanceCts?.Cancel();
            
            OnDialoguePaused?.Invoke();
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Dialogue paused");
        }
        
        public void ResumeDialogue()
        {
            if (!_currentState.IsPaused)
                return;
            
            _currentState.IsPaused = false;
            
            // Resume voice
            if (!string.IsNullOrEmpty(_currentVoiceId))
            {
                _audioService?.Resume(_currentVoiceId);
            }
            
            OnDialogueResumed?.Invoke();
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Dialogue resumed");
        }
        
        public async UniTask WaitAsync(float seconds, CancellationToken cancellationToken = default)
        {
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token).Token;
            await UniTask.Delay(Mathf.RoundToInt(seconds * 1000), cancellationToken: combinedToken);
        }
        
        #endregion
        
        #region History Management Implementation
        
        public void AddToHistory(DialogueLine line)
        {
            if (line == null)
                return;
            
            // Check if this type should be included in history
            if (!ShouldAddToHistory(line.Type))
                return;
            
            // Add to history
            _dialogueHistory.Add(line.Clone());
            _currentState.DialogueHistory.Add(line.Clone());
            
            // Trim history if it exceeds max size
            if (_dialogueHistory.Count > _config.MaxHistoryLines)
            {
                var removeCount = _dialogueHistory.Count - _config.MaxHistoryLines;
                _dialogueHistory.RemoveRange(0, removeCount);
                _currentState.DialogueHistory.RemoveRange(0, removeCount);
            }
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Added to history: {line.GetDisplayName()}: {line.Text}");
        }
        
        public IReadOnlyList<DialogueLine> GetDialogueHistory()
        {
            return _dialogueHistory.AsReadOnly();
        }
        
        public async UniTask ShowHistoryAsync()
        {
            if (!_config.EnableDialogueHistory)
                return;
            
            // This would typically show a history UI screen
            // For now, we'll just log the history
            if (_config.LogDialogueOperations)
            {
                Debug.Log($"[DialogueService] Showing dialogue history ({_dialogueHistory.Count} lines)");
            }
            
            await UniTask.Yield();
        }
        
        public void ClearHistory()
        {
            _dialogueHistory.Clear();
            _currentState.DialogueHistory.Clear();
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Dialogue history cleared");
        }
        
        public bool GoToPreviousLine()
        {
            if (!_config.AllowHistoryNavigation || _dialogueHistory.Count == 0)
                return false;
            
            // This would implement backwards navigation in dialogue
            // For now, just return false as this requires more complex state management
            return false;
        }
        
        public void FastForwardToCurrent()
        {
            // Skip any active text animations
            SkipCurrentText();
            
            // Cancel auto-advance timer
            _autoAdvanceCts?.Cancel();
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] Fast forwarded to current");
        }
        
        private bool ShouldAddToHistory(DialogueType type)
        {
            return type switch
            {
                DialogueType.Speech => true,
                DialogueType.Thought => _config.IncludeThoughtsInHistory,
                DialogueType.Narration => _config.IncludeNarrationInHistory,
                DialogueType.System => false,
                _ => true
            };
        }
        
        #endregion
        
        #region Character Management Implementation
        
        public void SetCharacterDisplayName(string characterId, string displayName)
        {
            if (string.IsNullOrEmpty(characterId))
                return;
            
            _characterDisplayNames[characterId] = displayName ?? characterId;
            _currentState.CharacterDisplayNames[characterId] = displayName ?? characterId;
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Set display name for {characterId}: {displayName}");
        }
        
        public void SetCharacterColor(string characterId, Color color)
        {
            if (string.IsNullOrEmpty(characterId))
                return;
            
            _characterColors[characterId] = color;
            _currentState.CharacterColors[characterId] = color;
            
            if (_config.LogDialogueOperations)
                Debug.Log($"[DialogueService] Set color for {characterId}: {color}");
        }
        
        public string GetCharacterDisplayName(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return string.Empty;
            
            return _characterDisplayNames.TryGetValue(characterId, out var displayName) ? displayName : characterId;
        }
        
        private void ApplyCharacterSettings(DialogueLine dialogueLine)
        {
            if (string.IsNullOrEmpty(dialogueLine.SpeakerId))
                return;
            
            // Set display name
            if (_characterDisplayNames.TryGetValue(dialogueLine.SpeakerId, out var displayName))
            {
                dialogueLine.Metadata.DisplayName = displayName;
            }
            
            // Set colors
            if (_characterColors.TryGetValue(dialogueLine.SpeakerId, out var color))
            {
                dialogueLine.Metadata.TextColor = color;
                dialogueLine.Metadata.NameColor = color;
            }
            else
            {
                // Use default colors from config
                dialogueLine.Metadata.TextColor = _config.GetColorForDialogueType(dialogueLine.Type);
                dialogueLine.Metadata.NameColor = _config.DefaultCharacterNameColor;
            }
        }
        
        #endregion
        
        #region State Management Implementation
        
        public DialogueState GetCurrentState()
        {
            return _currentState.Clone();
        }
        
        public async UniTask RestoreStateAsync(DialogueState state, CancellationToken cancellationToken = default)
        {
            if (state == null)
                return;
            
            // Stop current dialogue
            if (IsDialogueActive)
            {
                await StopCurrentDialogueAsync();
            }
            
            // Restore state
            _currentState.TextSpeed = state.TextSpeed;
            _currentState.AutoAdvanceEnabled = state.AutoAdvanceEnabled;
            _currentState.AutoAdvanceDelay = state.AutoAdvanceDelay;
            _currentState.VoiceVolume = state.VoiceVolume;
            
            // Restore character settings
            _characterDisplayNames.Clear();
            foreach (var kvp in state.CharacterDisplayNames)
            {
                _characterDisplayNames[kvp.Key] = kvp.Value;
            }
            
            _characterColors.Clear();
            foreach (var kvp in state.CharacterColors)
            {
                _characterColors[kvp.Key] = kvp.Value;
            }
            
            // Restore history
            _dialogueHistory.Clear();
            _dialogueHistory.AddRange(state.DialogueHistory.Select(line => line.Clone()));
            
            // Apply settings
            SetAutoAdvance(state.AutoAdvanceEnabled, state.AutoAdvanceDelay);
            SetTextSpeed(state.TextSpeed);
            SetVoiceVolume(state.VoiceVolume);
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] State restored");
            
            await UniTask.Yield();
        }
        
        public void ResetDialogueSystem()
        {
            // Stop current operations
            _serviceCts?.Cancel();
            _serviceCts = new CancellationTokenSource();
            
            // Reset state
            _currentState.Reset();
            _characterDisplayNames.Clear();
            _characterColors.Clear();
            _dialogueHistory.Clear();
            
            // Reset to config defaults
            _autoAdvanceEnabled = _config.DefaultAutoAdvanceMode != AutoAdvanceMode.Never;
            _autoAdvanceDelay = _config.DefaultAutoAdvanceDelay;
            _currentState.TextSpeed = _config.DefaultTypingSpeed;
            _currentState.VoiceVolume = _config.DefaultVoiceVolume;
            
            if (_config.LogDialogueOperations)
                Debug.Log("[DialogueService] System reset to defaults");
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private void InitializeCharacterSettings()
        {
            // Initialize default character colors from config
            _characterColors["narrator"] = _config.NarratorTextColor;
            _characterColors["system"] = Color.yellow;
        }
        
        private async UniTask LoadPersistedHistoryAsync(CancellationToken cancellationToken)
        {
            try
            {
                var loadResult = await _saveLoadService.LoadAsync<DialogueHistorySaveData>("dialogue_history", cancellationToken);
                _dialogueHistory = loadResult.Data?.History;
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogWarning($"[DialogueService] Failed to load persisted history: {ex.Message}");
            }
        }
        
        private async UniTask SaveHistoryAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _saveLoadService.SaveAsync("dialogue_history", new DialogueHistorySaveData
                {
                    History = _dialogueHistory.AsValueEnumerable().Select(line => line.Clone()).ToList()
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_config.LogDialogueOperations)
                    Debug.LogWarning($"[DialogueService] Failed to save history: {ex.Message}");
            }
        }
        
        private async UniTask StopCurrentDialogueAsync()
        {
            // Cancel all active operations
            _currentTextAnimationCts?.Cancel();
            _currentVoiceCts?.Cancel();
            _autoAdvanceCts?.Cancel();
            
            // Stop voice
            if (!string.IsNullOrEmpty(_currentVoiceId))
            {
                StopVoice();
            }
            
            // Complete any pending choices
            if (_choiceCompletionSource != null && !_choiceCompletionSource.Task.IsCompleted)
            {
                _choiceCompletionSource.TrySetCanceled();
            }
            
            // Reset state
            _currentState.IsActive = false;
            _currentState.IsWaitingForChoice = false;
            _currentState.CurrentLine = null;
            _currentState.CurrentChoices = null;
            _currentChoices = null;
            
            await UniTask.Yield();
        }
        
        private void ApplyPreviousChoiceHighlighting(DialogueChoice[] choices)
        {
            foreach (var choice in choices)
            {
                if (choice.HasBeenSelected)
                {
                    choice.TextColor = _config.PreviousChoiceColor;
                }
            }
        }
        
        private async UniTask HandleChoiceTimeLimitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(Mathf.RoundToInt(_config.ChoiceTimeLimit * 1000), cancellationToken: cancellationToken);
                
                // Time expired, select default choice
                if (IsWaitingForChoice && _choiceCompletionSource != null && !_choiceCompletionSource.Task.IsCompleted)
                {
                    var defaultIndex = Mathf.Clamp(_config.DefaultTimeLimitChoice, 0, _currentChoices.Length - 1);
                    SelectChoice(defaultIndex);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when choice is made before time limit
            }
        }
        
        private void CancelAllAnimations()
        {
            _currentTextAnimationCts?.Cancel();
            _currentVoiceCts?.Cancel();
            _autoAdvanceCts?.Cancel();
            
            foreach (var kvp in _activeAnimations.ToList())
            {
                kvp.Value?.Cancel();
                _activeAnimations.TryRemove(kvp.Key, out _);
            }
        }
        
        #endregion
    }
}
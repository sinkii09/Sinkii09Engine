using Sinkii09.Engine.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System;
using System.Text;
using Sinkii09.Engine;

public class DialogScreen : UIScreen
{
    #region UI Elements
    
    [Header("Text Elements")]
    [SerializeField] private TextMeshProUGUI _speakerNameText;
    [SerializeField] private TextMeshProUGUI _dialogText;
    
    [Header("Choice Elements")]
    [SerializeField] private Transform _choiceContainer;
    [SerializeField] private Button _choiceButtonPrefab;
    
    [Header("Background Elements")]
    [SerializeField] private Image _dialogBox;
    [SerializeField] private Button _continueButton;
    [SerializeField] private GameObject _continueIndicator;
    
    #endregion
    
    #region Private Fields
    
    private IDialogueService _dialogueService;
    private IUIService _uiService;
    private List<Button> _activeChoiceButtons = new List<Button>();
    private CancellationTokenSource _typewriterCts;
    private bool _isTyping;
    private string _currentFullText;
    
    #endregion
    
    #region UIScreen Lifecycle
    
    protected override void OnInitialize(UIScreenContext context)
    {
        base.OnInitialize(context);
        
        // Get services
        _dialogueService = Engine.GetService<IDialogueService>();
        _uiService = Engine.GetService<IUIService>();
        
        // Subscribe to dialogue events
        SubscribeToDialogueEvents();
        
        // Setup continue button
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveAllListeners();
            _continueButton.onClick.AddListener(OnContinueClicked);
        }
        
        // Hide continue indicator initially
        if (_continueIndicator != null)
            _continueIndicator.SetActive(false);
        
        // Handle initial context if provided
        if (context is DialogueScreenContext dialogueContext)
        {
            if (dialogueContext.Line != null)
            {
                _ = DisplayDialogueLine(dialogueContext.Line, CancellationToken.None);
            }
            else if (dialogueContext.Choices != null)
            {
                DisplayChoices(dialogueContext.Choices);
            }
        }
    }
    
    protected override void OnHide()
    {
        base.OnHide();
        
        // Unsubscribe from events
        UnsubscribeFromDialogueEvents();
        
        // Cancel any active typewriter animation
        _typewriterCts?.Cancel();
        
        // Clear choice buttons
        ClearChoices();
    }
    
    #endregion
    
    #region Dialogue Display
    
    public async UniTask DisplayDialogueLine(DialogueLine line, CancellationToken cancellationToken)
    {
        if (line == null) return;
        
        // Update speaker name
        if (_speakerNameText != null)
        {
            var displayName = line.GetDisplayName();
            _speakerNameText.text = displayName;
            _speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(displayName));
            
            // Apply name color if specified
            if (line.Metadata.NameColor != default)
            {
                _speakerNameText.color = line.Metadata.NameColor;
            }
        }
        
        // Update dialogue text with typewriter effect
        if (_dialogText != null)
        {
            _currentFullText = line.Text;
            
            // Apply text color if specified
            if (line.Metadata.TextColor != default)
            {
                _dialogText.color = line.Metadata.TextColor;
            }
            
            // Start typewriter animation
            var speed = line.Metadata.CustomTypingSpeed ?? 30f;
            await ShowTextWithTypewriter(_currentFullText, speed, cancellationToken);
        }
        
        // Hide choices when showing dialogue
        ClearChoices();
        if (_choiceContainer != null)
            _choiceContainer.gameObject.SetActive(false);
    }
    
    public void DisplayChoices(DialogueChoice[] choices)
    {
        if (choices == null || choices.Length == 0) return;
        
        // Clear existing choices
        ClearChoices();
        
        // Show choice container
        if (_choiceContainer != null)
        {
            _choiceContainer.gameObject.SetActive(true);
            
            // Create choice buttons
            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                var choiceIndex = i; // Capture the index for the closure
                
                Button choiceButton = null;
                
                // Create or instantiate button
                if (_choiceButtonPrefab != null)
                {
                    choiceButton = Instantiate(_choiceButtonPrefab, _choiceContainer);
                }
                else
                {
                    // Create basic button if no prefab
                    var buttonGO = new GameObject($"Choice_{i}");
                    buttonGO.transform.SetParent(_choiceContainer);
                    choiceButton = buttonGO.AddComponent<Button>();
                    var text = buttonGO.AddComponent<TextMeshProUGUI>();
                    text.text = choice.Text;
                }
                
                // Set button text
                var buttonText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = choice.Text;
                    
                    // Apply choice color
                    if (choice.TextColor != default)
                    {
                        buttonText.color = choice.TextColor;
                    }
                    else if (choice.HasBeenSelected)
                    {
                        // Dim previously selected choices
                        buttonText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                }
                
                // Set interactable based on choice enabled state
                choiceButton.interactable = choice.IsEnabled;
                
                // Add click listener - use local variable to avoid closure issues
                var localIndex = choiceIndex;
                choiceButton.onClick.RemoveAllListeners();
                choiceButton.onClick.AddListener(() => OnChoiceSelected(localIndex));
                
                _activeChoiceButtons.Add(choiceButton);
            }
        }
        
        // Hide continue indicator when showing choices
        if (_continueIndicator != null)
            _continueIndicator.SetActive(false);
    }
    
    #endregion
    
    #region Typewriter Animation
    
    private async UniTask ShowTextWithTypewriter(string fullText, float charactersPerSecond, CancellationToken cancellationToken)
    {
        if (_dialogText == null || string.IsNullOrEmpty(fullText)) return;
        
        // Cancel previous animation
        _typewriterCts?.Cancel();
        _typewriterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _isTyping = true;
        _dialogText.text = "";
        
        if (_continueIndicator != null)
            _continueIndicator.SetActive(false);
        
        try
        {
            var delay = 1f / charactersPerSecond;
            var stringBuilder = new StringBuilder();
            
            for (int i = 0; i < fullText.Length; i++)
            {
                _typewriterCts.Token.ThrowIfCancellationRequested();
                
                stringBuilder.Append(fullText[i]);
                _dialogText.text = stringBuilder.ToString();
                
                // Skip delay for spaces and punctuation for smoother effect
                if (!char.IsWhiteSpace(fullText[i]) && !char.IsPunctuation(fullText[i]))
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: _typewriterCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was cancelled, show full text
            _dialogText.text = fullText;
        }
        finally
        {
            _isTyping = false;
            
            // Show continue indicator
            if (_continueIndicator != null && _dialogueService != null && _dialogueService.IsDialogueActive)
                _continueIndicator.SetActive(true);
        }
    }
    
    public void SkipTypewriter()
    {
        if (_isTyping)
        {
            _typewriterCts?.Cancel();
            if (_dialogText != null)
                _dialogText.text = _currentFullText;
            _isTyping = false;
            
            if (_continueIndicator != null)
                _continueIndicator.SetActive(true);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnContinueClicked()
    {
        if (_isTyping)
        {
            // Skip current typewriter animation
            SkipTypewriter();
        }
        else if (_dialogueService != null)
        {
            // Notify dialogue service to continue
            _dialogueService.SkipCurrentText();
        }
    }
    
    private void OnChoiceSelected(int choiceIndex)
    {
        if (_dialogueService != null)
        {
            _dialogueService.SelectChoice(choiceIndex);
            
            // Clear choices after selection
            ClearChoices();
            if (_choiceContainer != null)
                _choiceContainer.gameObject.SetActive(false);
        }
    }
    
    #endregion
    
    #region Event Subscriptions
    
    private void SubscribeToDialogueEvents()
    {
        if (_dialogueService == null) return;
        
        _dialogueService.OnDialogueStarted += OnDialogueStarted;
        _dialogueService.OnTextTypingStarted += OnTextTypingStarted;
        _dialogueService.OnTextTypingCompleted += OnTextTypingCompleted;
        _dialogueService.OnChoicesShown += OnChoicesShown;
        _dialogueService.OnDialogueCompleted += OnDialogueCompleted;
    }
    
    private void UnsubscribeFromDialogueEvents()
    {
        if (_dialogueService == null) return;
        
        _dialogueService.OnDialogueStarted -= OnDialogueStarted;
        _dialogueService.OnTextTypingStarted -= OnTextTypingStarted;
        _dialogueService.OnTextTypingCompleted -= OnTextTypingCompleted;
        _dialogueService.OnChoicesShown -= OnChoicesShown;
        _dialogueService.OnDialogueCompleted -= OnDialogueCompleted;
    }
    
    private void OnDialogueStarted(DialogueLine line)
    {
        // Just display the dialogue - DialogueService handles showing the screen
        _ = DisplayDialogueLine(line, CancellationToken.None);
    }
    
    private void OnTextTypingStarted(string text)
    {
        // Text typing started - handled by DisplayDialogueLine
    }
    
    private void OnTextTypingCompleted(string text)
    {
        // Text typing completed
        if (_continueIndicator != null)
            _continueIndicator.SetActive(true);
    }
    
    private void OnChoicesShown(DialogueChoice[] choices)
    {
        DisplayChoices(choices);
    }
    
    private void OnDialogueCompleted(DialogueLine line)
    {
        // Dialogue line completed - can be used for auto-hide logic if needed
    }
    
    #endregion
    
    #region Helper Methods
    
    private void ClearChoices()
    {
        foreach (var button in _activeChoiceButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }
        _activeChoiceButtons.Clear();
    }
    
    #endregion
}

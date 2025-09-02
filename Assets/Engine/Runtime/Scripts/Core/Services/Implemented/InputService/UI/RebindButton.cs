using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// UI component for interactive input rebinding with visual feedback
    /// </summary>
    public class RebindButton : EngineBehaviour
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private PlayerAction _playerAction;
        [SerializeField] private UIAction _uiAction;
        [SerializeField] private bool _usePlayerAction = true;
        [SerializeField] private int _bindingIndex = 0;
        [SerializeField] private float _rebindTimeout = 10f;
        
        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _bindingText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private GameObject _waitingIndicator;
        [SerializeField] private GameObject _conflictWarning;
        
        [Header("Visual Settings")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _rebindingColor = Color.yellow;
        [SerializeField] private Color _successColor = Color.green;
        [SerializeField] private Color _errorColor = Color.red;
        [SerializeField] private bool _useShortDisplayNames = true;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _rebindStartSound;
        [SerializeField] private AudioClip _rebindSuccessSound;
        [SerializeField] private AudioClip _rebindErrorSound;
        [SerializeField] private AudioClip _rebindCancelSound;
        
        #endregion
        
        #region Private Fields
        
        private IRebindableAction _rebindingManager;
        private bool _isRebinding;
        private string _currentActionName;
        private ColorBlock _originalButtonColors;
        
        #endregion
        
        #region Events
        
        public event Action<RebindingResult> OnRebindCompleted;
        public event Action<string, int> OnRebindStarted;
        public event Action<string, int> OnRebindCancelled;
        
        #endregion
        
        #region Properties
        
        public bool IsRebinding => _isRebinding;
        public string CurrentActionName => _currentActionName;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Auto-find UI references if not set
            if (_button == null)
                _button = GetComponent<Button>();
            if (_bindingText == null)
                _bindingText = GetComponentInChildren<TextMeshProUGUI>();
            
            // Store original button colors
            if (_button != null)
            {
                _originalButtonColors = _button.colors;
            }
            
            // Set up button click handler
            if (_button != null)
            {
                _button.onClick.AddListener(OnButtonClicked);
            }
            
            // Determine current action name
            _currentActionName = _usePlayerAction ? _playerAction.ToString() : _uiAction.ToString();
        }

        protected override void OnEngineInitialized(ServiceInitializationReport report)
        {
            if (!report.Success)
            {
                return;
            }
            // Get reference to rebinding manager
            var inputService = Engine.GetService<IInputService>();
            if (inputService != null)
            {
                SetRebindingManager(inputService.Rebinding);
            }
            
            // Update initial display
            UpdateBindingDisplay();
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_rebindingManager != null)
            {
                _rebindingManager.OnRebindingStarted -= OnRebindingStartedInternal;
                _rebindingManager.OnRebindingCompleted -= OnRebindingCompletedInternal;
                _rebindingManager.OnRebindingCancelled -= OnRebindingCancelledInternal;
                _rebindingManager.OnWaitingForInput -= OnWaitingForInputInternal;
            }
            
            // Clean up button listener
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Set the rebinding manager reference
        /// </summary>
        public void SetRebindingManager(IRebindableAction rebindingManager)
        {
            // Unsubscribe from old manager
            if (_rebindingManager != null)
            {
                _rebindingManager.OnRebindingStarted -= OnRebindingStartedInternal;
                _rebindingManager.OnRebindingCompleted -= OnRebindingCompletedInternal;
                _rebindingManager.OnRebindingCancelled -= OnRebindingCancelledInternal;
                _rebindingManager.OnWaitingForInput -= OnWaitingForInputInternal;
            }
            
            _rebindingManager = rebindingManager;
            
            // Subscribe to new manager
            if (_rebindingManager != null)
            {
                _rebindingManager.OnRebindingStarted += OnRebindingStartedInternal;
                _rebindingManager.OnRebindingCompleted += OnRebindingCompletedInternal;
                _rebindingManager.OnRebindingCancelled += OnRebindingCancelledInternal;
                _rebindingManager.OnWaitingForInput += OnWaitingForInputInternal;
                
                UpdateBindingDisplay();
            }
        }
        
        /// <summary>
        /// Start rebinding for this button's action
        /// </summary>
        public async UniTask<RebindingResult> StartRebindAsync()
        {
            if (_rebindingManager == null)
            {
                Debug.LogError("[RebindButton] No rebinding manager set");
                return RebindingResult.Error(_currentActionName, _bindingIndex,
                    new InvalidOperationException("No rebinding manager available"));
            }
            
            if (_isRebinding)
            {
                Debug.LogWarning("[RebindButton] Rebind already in progress");
                return RebindingResult.Cancelled(_currentActionName, _bindingIndex, "Already rebinding");
            }
            
            try
            {
                // Start the appropriate rebind operation
                if (_usePlayerAction)
                {
                    return await _rebindingManager.StartRebindAsync(_playerAction, _bindingIndex, _rebindTimeout);
                }
                else
                {
                    return await _rebindingManager.StartRebindAsync(_uiAction, _bindingIndex, _rebindTimeout);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RebindButton] Error starting rebind: {ex.Message}");
                return RebindingResult.Error(_currentActionName, _bindingIndex, ex);
            }
        }
        
        /// <summary>
        /// Cancel any ongoing rebinding operation
        /// </summary>
        public void CancelRebind()
        {
            if (_isRebinding && _rebindingManager != null)
            {
                _rebindingManager.CancelRebind();
            }
        }
        
        /// <summary>
        /// Reset this button's binding to default
        /// </summary>
        public void ResetToDefault()
        {
            if (_rebindingManager == null)
                return;
            
            bool success;
            if (_usePlayerAction)
            {
                success = _rebindingManager.ResetBinding(_playerAction, _bindingIndex);
            }
            else
            {
                success = _rebindingManager.ResetBinding(_uiAction, _bindingIndex);
            }
            
            if (success)
            {
                UpdateBindingDisplay();
                PlaySound(_rebindSuccessSound);
                ShowStatusMessage("Reset to default", _successColor, 2f);
            }
            else
            {
                PlaySound(_rebindErrorSound);
                ShowStatusMessage("Failed to reset", _errorColor, 2f);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private async void OnButtonClicked()
        {
            if (_isRebinding)
            {
                CancelRebind();
            }
            else
            {
                var result = await StartRebindAsync();
                // Result handling is done in the event callbacks
            }
        }
        
        private void UpdateBindingDisplay()
        {
            if (_bindingText == null || _rebindingManager == null)
                return;
            
            string displayText;
            if (_usePlayerAction)
            {
                var bindingPath = _rebindingManager.GetBindingPath(_playerAction, _bindingIndex);
                displayText = _useShortDisplayNames 
                    ? BindingDisplayFormatter.GetShortDisplayString(bindingPath)
                    : BindingDisplayFormatter.FormatBindingPath(bindingPath);
            }
            else
            {
                var bindingPath = _rebindingManager.GetBindingPath(_uiAction, _bindingIndex);
                displayText = _useShortDisplayNames 
                    ? BindingDisplayFormatter.GetShortDisplayString(bindingPath)
                    : BindingDisplayFormatter.FormatBindingPath(bindingPath);
            }
            
            _bindingText.text = displayText;
        }
        
        private void SetButtonColor(Color color)
        {
            if (_button == null)
                return;
            
            var colors = _button.colors;
            colors.normalColor = color;
            colors.selectedColor = color;
            _button.colors = colors;
        }
        
        private void RestoreButtonColor()
        {
            if (_button != null)
            {
                _button.colors = _originalButtonColors;
            }
        }
        
        private void ShowWaitingIndicator(bool show)
        {
            if (_waitingIndicator != null)
            {
                _waitingIndicator.SetActive(show);
            }
        }
        
        private void ShowConflictWarning(bool show)
        {
            if (_conflictWarning != null)
            {
                _conflictWarning.SetActive(show);
            }
        }
        
        private async void ShowStatusMessage(string message, Color color, float duration)
        {
            if (_statusText == null)
                return;
            
            _statusText.text = message;
            _statusText.color = color;
            _statusText.gameObject.SetActive(true);
            
            await UniTask.Delay(TimeSpan.FromSeconds(duration));
            
            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(false);
            }
        }
        
        private void PlaySound(AudioClip clip)
        {

            // TODO: Integrate with AudioService when available
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnRebindingStartedInternal(string actionName, int bindingIndex)
        {
            if (actionName == _currentActionName && bindingIndex == _bindingIndex)
            {
                _isRebinding = true;
                SetButtonColor(_rebindingColor);
                ShowWaitingIndicator(true);
                ShowConflictWarning(false);
                
                if (_bindingText != null)
                {
                    _bindingText.text = "Press any key...";
                }
                
                PlaySound(_rebindStartSound);
                OnRebindStarted?.Invoke(actionName, bindingIndex);
            }
        }
        
        private void OnRebindingCompletedInternal(RebindingResult result)
        {
            if (result.ActionName == _currentActionName && result.BindingIndex == _bindingIndex)
            {
                _isRebinding = false;
                ShowWaitingIndicator(false);
                
                switch (result.Status)
                {
                    case RebindingStatus.Success:
                        SetButtonColor(_successColor);
                        ShowStatusMessage("Binding updated!", _successColor, 2f);
                        PlaySound(_rebindSuccessSound);
                        break;
                        
                    case RebindingStatus.Conflict:
                        SetButtonColor(_errorColor);
                        ShowConflictWarning(true);
                        ShowStatusMessage($"Conflicts with {result.ConflictingAction}", _errorColor, 3f);
                        PlaySound(_rebindErrorSound);
                        break;
                        
                    case RebindingStatus.InvalidInput:
                        SetButtonColor(_errorColor);
                        ShowStatusMessage("Invalid key", _errorColor, 2f);
                        PlaySound(_rebindErrorSound);
                        break;
                        
                    case RebindingStatus.Timeout:
                        SetButtonColor(_normalColor);
                        ShowStatusMessage("Timed out", Color.gray, 2f);
                        PlaySound(_rebindCancelSound);
                        break;
                        
                    default:
                        SetButtonColor(_errorColor);
                        ShowStatusMessage("Failed", _errorColor, 2f);
                        PlaySound(_rebindErrorSound);
                        break;
                }
                
                // Restore normal color after delay
                UniTask.Delay(TimeSpan.FromSeconds(2f)).ContinueWith(() =>
                {
                    RestoreButtonColor();
                    ShowConflictWarning(false);
                }).Forget();
                
                // Update display with new binding
                UpdateBindingDisplay();
                
                OnRebindCompleted?.Invoke(result);
            }
        }
        
        private void OnRebindingCancelledInternal(string actionName, int bindingIndex)
        {
            if (actionName == _currentActionName && bindingIndex == _bindingIndex)
            {
                _isRebinding = false;
                ShowWaitingIndicator(false);
                RestoreButtonColor();
                UpdateBindingDisplay();
                
                ShowStatusMessage("Cancelled", Color.gray, 2f);
                PlaySound(_rebindCancelSound);
                
                OnRebindCancelled?.Invoke(actionName, bindingIndex);
            }
        }
        
        private void OnWaitingForInputInternal(string actionName, int bindingIndex, float elapsed)
        {
            if (actionName == _currentActionName && bindingIndex == _bindingIndex)
            {
                if (_bindingText != null)
                {
                    var remaining = Mathf.Max(0, _rebindTimeout - elapsed);
                    _bindingText.text = $"Press any key... ({remaining:F0}s)";
                }
            }
        }
        
        #endregion
    }
}
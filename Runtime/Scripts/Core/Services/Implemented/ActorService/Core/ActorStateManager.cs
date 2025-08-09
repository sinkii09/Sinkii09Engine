using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages actor state synchronization and provides advanced state management features
    /// </summary>
    public class ActorStateManager : IDisposable
    {
        private readonly IActor _actor;
        private readonly Dictionary<string, ActorState> _stateHistory = new();
        private readonly Queue<StateTransition> _transitionQueue = new();
        private readonly object _stateLock = new();
        
        // State tracking
        private ActorState _currentState;
        private ActorState _previousState;
        private string _currentStateName = "default";
        private DateTime _lastStateChange;
        
        // Configuration
        private int _maxHistorySize = 20;
        private bool _enableStateValidation = true;
        private bool _enableStateHistory = true;
        private bool _enableAutoSave = false;
        private float _autoSaveInterval = 30f;
        
        // Cancellation and lifecycle
        private CancellationTokenSource _disposeCts;
        private Timer _autoSaveTimer;
        private bool _disposed = false;
        
        // State transition tracking
        private StateTransition _activeTransition;
        private bool _isTransitioning = false;
        
        // Events
        public event Action<ActorState, ActorState> OnStateChanged;
        public event Action<string, ActorState> OnStateNamed;
        public event Action<StateTransition> OnTransitionStarted;
        public event Action<StateTransition> OnTransitionCompleted;
        public event Action<string> OnStateValidationFailed;
        
        // Properties
        public ActorState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState?.Clone();
                }
            }
        }
        
        public ActorState PreviousState
        {
            get
            {
                lock (_stateLock)
                {
                    return _previousState?.Clone();
                }
            }
        }
        
        public string CurrentStateName
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentStateName;
                }
            }
        }
        
        public DateTime LastStateChange
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastStateChange;
                }
            }
        }
        
        public bool IsTransitioning
        {
            get
            {
                lock (_stateLock)
                {
                    return _isTransitioning;
                }
            }
        }
        
        public int StateHistoryCount
        {
            get
            {
                lock (_stateLock)
                {
                    return _stateHistory.Count;
                }
            }
        }
        
        public ActorStateManager(IActor actor)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _disposeCts = new CancellationTokenSource();
            
            // Capture initial state
            CaptureCurrentState();
            
            Debug.Log($"[ActorStateManager] Initialized for actor: {_actor.Id}");
        }
        
        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                // Initialize with current actor state
                await RefreshCurrentStateAsync(linkedCts.Token);
                
                // Setup auto-save if enabled
                if (_enableAutoSave)
                {
                    SetupAutoSave();
                }
                
                Debug.Log($"[ActorStateManager] State manager initialized for {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorStateManager] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        // === Core State Management ===
        
        public async UniTask<bool> ApplyStateAsync(ActorState state, float duration = 0f, string stateName = null, CancellationToken cancellationToken = default)
        {
            if (state == null)
            {
                Debug.LogWarning("[ActorStateManager] Cannot apply null state");
                return false;
            }
            
            ValidateNotDisposed();
            
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                // Validate state if enabled
                if (_enableStateValidation && !ValidateState(state))
                {
                    OnStateValidationFailed?.Invoke($"State validation failed for {state.Id}");
                    return false;
                }
                
                // Create transition
                var transition = new StateTransition
                {
                    FromState = _currentState?.Clone(),
                    ToState = state.Clone(),
                    Duration = duration,
                    StateName = stateName ?? "unnamed",
                    StartTime = DateTime.Now,
                    CancellationToken = linkedCts.Token
                };
                
                return await ExecuteTransitionAsync(transition);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[ActorStateManager] State application cancelled for {_actor.Id}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorStateManager] Failed to apply state: {ex.Message}");
                return false;
            }
        }
        
        public async UniTask<bool> TransitionToStateAsync(string stateName, float duration = 1f, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stateName))
                return false;
            
            var state = GetNamedState(stateName);
            if (state == null)
            {
                Debug.LogWarning($"[ActorStateManager] Named state not found: {stateName}");
                return false;
            }
            
            return await ApplyStateAsync(state, duration, stateName, cancellationToken);
        }
        
        public void CaptureCurrentState(string stateName = null)
        {
            ValidateNotDisposed();
            
            var newState = _actor.GetState();
            
            lock (_stateLock)
            {
                _previousState = _currentState?.Clone();
                _currentState = newState;
                _currentStateName = stateName ?? _currentStateName;
                _lastStateChange = DateTime.Now;
                
                // Add to history if enabled
                if (_enableStateHistory)
                {
                    AddToHistory(stateName ?? $"auto_{DateTime.Now.Ticks}", newState);
                }
            }
            
            OnStateChanged?.Invoke(_previousState, _currentState);
            
            if (!string.IsNullOrEmpty(stateName))
            {
                OnStateNamed?.Invoke(stateName, _currentState);
            }
            
            Debug.Log($"[ActorStateManager] Captured state for {_actor.Id}: {stateName ?? "unnamed"}");
        }
        
        public async UniTask RefreshCurrentStateAsync(CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            try
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                CaptureCurrentState();
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[ActorStateManager] State refresh cancelled for {_actor.Id}");
                throw;
            }
        }
        
        // === Named State Management ===
        
        public void SaveNamedState(string stateName, ActorState state = null)
        {
            if (string.IsNullOrEmpty(stateName))
                return;
            
            ValidateNotDisposed();
            
            var stateToSave = state ?? _actor.GetState();
            
            lock (_stateLock)
            {
                _stateHistory[stateName] = stateToSave.Clone();
                
                // Ensure we don't exceed max history size
                while (_stateHistory.Count > _maxHistorySize)
                {
                    var oldestKey = GetOldestStateKey();
                    if (!string.IsNullOrEmpty(oldestKey))
                        _stateHistory.Remove(oldestKey);
                }
            }
            
            OnStateNamed?.Invoke(stateName, stateToSave);
            Debug.Log($"[ActorStateManager] Saved named state '{stateName}' for {_actor.Id}");
        }
        
        public ActorState GetNamedState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return null;
            
            lock (_stateLock)
            {
                return _stateHistory.TryGetValue(stateName, out var state) ? state.Clone() : null;
            }
        }
        
        public void RemoveNamedState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return;
            
            lock (_stateLock)
            {
                _stateHistory.Remove(stateName);
            }
            
            Debug.Log($"[ActorStateManager] Removed named state '{stateName}' for {_actor.Id}");
        }
        
        public string[] GetNamedStateNames()
        {
            lock (_stateLock)
            {
                var names = new string[_stateHistory.Count];
                _stateHistory.Keys.CopyTo(names, 0);
                return names;
            }
        }
        
        // === State History and Rollback ===
        
        public async UniTask<bool> RollbackToPreviousStateAsync(float duration = 1f, CancellationToken cancellationToken = default)
        {
            if (_previousState == null)
            {
                Debug.LogWarning($"[ActorStateManager] No previous state to rollback to for {_actor.Id}");
                return false;
            }
            
            return await ApplyStateAsync(_previousState, duration, "rollback", cancellationToken);
        }
        
        public async UniTask<bool> RollbackToNamedStateAsync(string stateName, float duration = 1f, CancellationToken cancellationToken = default)
        {
            var state = GetNamedState(stateName);
            if (state == null)
                return false;
            
            return await ApplyStateAsync(state, duration, $"rollback_{stateName}", cancellationToken);
        }
        
        public void ClearHistory()
        {
            lock (_stateLock)
            {
                _stateHistory.Clear();
            }
            
            Debug.Log($"[ActorStateManager] Cleared state history for {_actor.Id}");
        }
        
        // === State Validation ===
        
        public bool ValidateState(ActorState state)
        {
            if (state == null)
                return false;
            
            // Basic validation
            if (state.ActorType != _actor.ActorType)
            {
                Debug.LogWarning($"[ActorStateManager] Actor type mismatch: expected {_actor.ActorType}, got {state.ActorType}");
                return false;
            }
            
            if (state.Id != _actor.Id)
            {
                Debug.LogWarning($"[ActorStateManager] Actor ID mismatch: expected {_actor.Id}, got {state.Id}");
                return false;
            }
            
            // Allow actor to perform custom validation
            if (!_actor.CanApplyState(state))
            {
                Debug.LogWarning($"[ActorStateManager] Actor-specific validation failed for state");
                return false;
            }
            
            return true;
        }
        
        // === State Comparison and Diff ===
        
        public bool IsStateEquivalent(ActorState state1, ActorState state2)
        {
            if (state1 == null && state2 == null)
                return true;
            
            if (state1 == null || state2 == null)
                return false;
            
            return state1.Id == state2.Id &&
                   state1.ActorType == state2.ActorType &&
                   state1.Position == state2.Position &&
                   state1.Rotation == state2.Rotation &&
                   state1.Scale == state2.Scale &&
                   state1.Visible == state2.Visible &&
                   state1.TintColor == state2.TintColor &&
                   state1.Alpha == state2.Alpha;
        }
        
        public StateDiff CompareStates(ActorState fromState, ActorState toState)
        {
            return new StateDiff
            {
                FromState = fromState?.Clone(),
                ToState = toState?.Clone(),
                HasPositionChange = fromState?.Position != toState?.Position,
                HasRotationChange = fromState?.Rotation != toState?.Rotation,
                HasScaleChange = fromState?.Scale != toState?.Scale,
                HasVisibilityChange = fromState?.Visible != toState?.Visible,
                HasColorChange = fromState?.TintColor != toState?.TintColor,
                HasAlphaChange = fromState?.Alpha != toState?.Alpha
            };
        }
        
        // === Configuration ===
        
        public void Configure(int maxHistorySize, bool enableValidation, bool enableHistory, bool enableAutoSave, float autoSaveInterval)
        {
            _maxHistorySize = Mathf.Max(1, maxHistorySize);
            _enableStateValidation = enableValidation;
            _enableStateHistory = enableHistory;
            _enableAutoSave = enableAutoSave;
            _autoSaveInterval = Mathf.Max(1f, autoSaveInterval);
            
            if (_enableAutoSave && _autoSaveTimer == null)
            {
                SetupAutoSave();
            }
            else if (!_enableAutoSave && _autoSaveTimer != null)
            {
                _autoSaveTimer?.Dispose();
                _autoSaveTimer = null;
            }
            
            Debug.Log($"[ActorStateManager] Configured: History={_maxHistorySize}, Validation={_enableStateValidation}, AutoSave={_enableAutoSave}");
        }
        
        // === Debug and Utility ===
        
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"=== ActorStateManager Debug Info: {_actor.Id} ===");
            info.AppendLine($"Current State Name: {_currentStateName}");
            info.AppendLine($"Last State Change: {_lastStateChange}");
            info.AppendLine($"Is Transitioning: {_isTransitioning}");
            info.AppendLine($"History Count: {StateHistoryCount}/{_maxHistorySize}");
            info.AppendLine($"State Validation: {(_enableStateValidation ? "Enabled" : "Disabled")}");
            info.AppendLine($"Auto Save: {(_enableAutoSave ? $"Enabled ({_autoSaveInterval}s)" : "Disabled")}");
            
            if (_currentState != null)
            {
                info.AppendLine($"\nCurrent State:");
                info.AppendLine($"  Position: {_currentState.Position}");
                info.AppendLine($"  Visible: {_currentState.Visible}");
                info.AppendLine($"  Alpha: {_currentState.Alpha:F2}");
                info.AppendLine($"  Timestamp: {_currentState.StateTimestamp}");
            }
            
            lock (_stateLock)
            {
                if (_stateHistory.Count > 0)
                {
                    info.AppendLine($"\nNamed States:");
                    foreach (var kvp in _stateHistory)
                    {
                        info.AppendLine($"  {kvp.Key}: {kvp.Value.StateTimestamp}");
                    }
                }
            }
            
            return info.ToString();
        }
        
        // === Private Methods ===
        
        private async UniTask<bool> ExecuteTransitionAsync(StateTransition transition)
        {
            try
            {
                lock (_stateLock)
                {
                    if (_isTransitioning)
                    {
                        Debug.LogWarning($"[ActorStateManager] Cannot start transition - already transitioning for {_actor.Id}");
                        return false;
                    }
                    
                    _isTransitioning = true;
                    _activeTransition = transition;
                }
                
                OnTransitionStarted?.Invoke(transition);
                
                // Apply the state to the actor
                await _actor.ApplyStateAsync(transition.ToState, transition.Duration, transition.CancellationToken);
                
                // Update our tracked state
                lock (_stateLock)
                {
                    _previousState = transition.FromState?.Clone();
                    _currentState = transition.ToState.Clone();
                    _currentStateName = transition.StateName;
                    _lastStateChange = DateTime.Now;
                    
                    // Add to history
                    if (_enableStateHistory)
                    {
                        AddToHistory(transition.StateName, _currentState);
                    }
                }
                
                transition.CompletedTime = DateTime.Now;
                OnTransitionCompleted?.Invoke(transition);
                OnStateChanged?.Invoke(_previousState, _currentState);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorStateManager] Transition failed: {ex.Message}");
                return false;
            }
            finally
            {
                lock (_stateLock)
                {
                    _isTransitioning = false;
                    _activeTransition = null;
                }
            }
        }
        
        private void AddToHistory(string stateName, ActorState state)
        {
            if (state == null)
                return;
            
            _stateHistory[stateName] = state.Clone();
            
            // Ensure we don't exceed max history size
            while (_stateHistory.Count > _maxHistorySize)
            {
                var oldestKey = GetOldestStateKey();
                if (!string.IsNullOrEmpty(oldestKey))
                    _stateHistory.Remove(oldestKey);
            }
        }
        
        private string GetOldestStateKey()
        {
            string oldestKey = null;
            DateTime oldestTime = DateTime.MaxValue;
            
            foreach (var kvp in _stateHistory)
            {
                if (kvp.Value.StateTimestamp < oldestTime)
                {
                    oldestTime = kvp.Value.StateTimestamp;
                    oldestKey = kvp.Key;
                }
            }
            
            return oldestKey;
        }
        
        private void SetupAutoSave()
        {
            _autoSaveTimer?.Dispose();
            
            _autoSaveTimer = new Timer(_ => AutoSaveCurrentState(), 
                null, 
                TimeSpan.FromSeconds(_autoSaveInterval), 
                TimeSpan.FromSeconds(_autoSaveInterval));
        }
        
        private void AutoSaveCurrentState()
        {
            if (_disposed)
                return;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                SaveNamedState($"autosave_{timestamp}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorStateManager] Auto-save failed: {ex.Message}");
            }
        }
        
        private void ValidateNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActorStateManager));
        }
        
        // === IDisposable Implementation ===
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                _disposeCts.Cancel();
                _autoSaveTimer?.Dispose();
                
                // Clear all state data
                lock (_stateLock)
                {
                    _stateHistory.Clear();
                    _transitionQueue.Clear();
                }
                
                _disposeCts?.Dispose();
                _disposed = true;
                
                Debug.Log($"[ActorStateManager] Disposed for actor: {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorStateManager] Error during disposal: {ex.Message}");
            }
        }
    }
    
    // === Helper Classes ===
    
    public class StateTransition
    {
        public ActorState FromState { get; set; }
        public ActorState ToState { get; set; }
        public string StateName { get; set; }
        public float Duration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public CancellationToken CancellationToken { get; set; }
        
        public TimeSpan ElapsedTime => (CompletedTime ?? DateTime.Now) - StartTime;
        public bool IsCompleted => CompletedTime.HasValue;
    }
    
    public class StateDiff
    {
        public ActorState FromState { get; set; }
        public ActorState ToState { get; set; }
        public bool HasPositionChange { get; set; }
        public bool HasRotationChange { get; set; }
        public bool HasScaleChange { get; set; }
        public bool HasVisibilityChange { get; set; }
        public bool HasColorChange { get; set; }
        public bool HasAlphaChange { get; set; }
        
        public bool HasAnyChange => HasPositionChange || HasRotationChange || HasScaleChange || 
                                   HasVisibilityChange || HasColorChange || HasAlphaChange;
    }
}
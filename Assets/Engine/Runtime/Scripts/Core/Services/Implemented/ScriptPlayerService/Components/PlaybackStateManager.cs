using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Script;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages playback state transitions and validation for script execution
    /// </summary>
    public class PlaybackStateManager : IDisposable
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private readonly ScriptExecutionContext _executionContext;
        private readonly object _stateLock = new object();
        private float _currentPlaybackSpeed;
        private bool _disposed;
        #endregion

        #region Properties
        public PlaybackState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _executionContext?.State ?? PlaybackState.Idle;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    if (_executionContext != null)
                    {
                        var oldState = _executionContext.State;
                        _executionContext.State = value;
                        if (oldState != value)
                        {
                            StateChanged?.Invoke(oldState, value);
                        }
                    }
                }
            }
        }

        public float PlaybackSpeed 
        { 
            get => _currentPlaybackSpeed;
            set => _currentPlaybackSpeed = Mathf.Clamp(value, 0.1f, _config.MaxFastForwardSpeed);
        }

        public bool IsPlaying => State == PlaybackState.Playing;
        public bool IsPaused => State == PlaybackState.Paused;
        public bool IsWaiting => State == PlaybackState.Waiting;
        public bool IsLoading => State == PlaybackState.Loading;
        public bool IsCompleted => State == PlaybackState.Completed;
        public bool IsStopped => State == PlaybackState.Stopped;
        public bool IsFailed => State == PlaybackState.Failed;
        public bool IsIdle => State == PlaybackState.Idle;
        #endregion

        #region Events
        public event Action<PlaybackState, PlaybackState> StateChanged;
        #endregion

        #region Constructor
        public PlaybackStateManager(ScriptPlayerConfiguration config, ScriptExecutionContext executionContext)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _currentPlaybackSpeed = _config.DefaultPlaybackSpeed;
        }
        #endregion

        #region State Transition Methods
        public void TransitionToIdle()
        {
            ValidateTransition(PlaybackState.Idle);
            State = PlaybackState.Idle;
            LogStateTransition("Idle");
        }

        public void TransitionToLoading()
        {
            ValidateTransition(PlaybackState.Loading);
            State = PlaybackState.Loading;
            LogStateTransition("Loading");
        }

        public void TransitionToPlaying()
        {
            ValidateTransition(PlaybackState.Playing);
            State = PlaybackState.Playing;
            LogStateTransition("Playing");
        }

        public void TransitionToWaiting()
        {
            ValidateTransition(PlaybackState.Waiting);
            State = PlaybackState.Waiting;
            LogStateTransition("Waiting");
        }

        public async UniTask TransitionToPausedAsync()
        {
            if (State == PlaybackState.Playing || State == PlaybackState.Waiting)
            {
                State = PlaybackState.Paused;
                LogStateTransition("Paused");
                await UniTask.Yield();
            }
        }

        public void TransitionToStopped()
        {
            State = PlaybackState.Stopped;
            LogStateTransition("Stopped");
        }

        public void TransitionToCompleted()
        {
            ValidateTransition(PlaybackState.Completed);
            State = PlaybackState.Completed;
            LogStateTransition("Completed");
        }

        public void TransitionToFailed(string reason = null)
        {
            State = PlaybackState.Failed;
            var message = string.IsNullOrEmpty(reason) ? "Failed" : $"Failed: {reason}";
            LogStateTransition(message);
        }
        #endregion

        #region Validation Methods
        public bool CanPlay()
        {
            return State == PlaybackState.Idle || 
                   State == PlaybackState.Paused || 
                   State == PlaybackState.Completed;
        }

        public bool CanPause()
        {
            return State == PlaybackState.Playing || State == PlaybackState.Waiting;
        }

        public bool CanResume()
        {
            return State == PlaybackState.Paused;
        }

        public bool CanStop()
        {
            return State == PlaybackState.Playing || 
                   State == PlaybackState.Paused || 
                   State == PlaybackState.Waiting || 
                   State == PlaybackState.Loading;
        }

        public bool IsExecutionActive()
        {
            return State == PlaybackState.Playing || 
                   State == PlaybackState.Waiting || 
                   State == PlaybackState.Loading;
        }

        public bool IsExecutionFinished()
        {
            return State == PlaybackState.Completed || 
                   State == PlaybackState.Stopped || 
                   State == PlaybackState.Failed;
        }
        #endregion

        #region Speed Control
        public void SetPlaybackSpeed(float speed)
        {
            var clampedSpeed = Mathf.Clamp(speed, 0.1f, _config.MaxFastForwardSpeed);
            if (Math.Abs(_currentPlaybackSpeed - clampedSpeed) > 0.01f)
            {
                _currentPlaybackSpeed = clampedSpeed;
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"[StateManager] Playback speed changed to {clampedSpeed:F1}x");
                }
            }
        }

        public void ResetPlaybackSpeed()
        {
            SetPlaybackSpeed(_config.DefaultPlaybackSpeed);
        }
        #endregion

        #region Fast Forward Support
        public bool IsFastForwarding => PlaybackSpeed > 1.0f;

        public void EnableFastForward(float speed = -1f)
        {
            var targetSpeed = speed > 0 ? speed : _config.MaxFastForwardSpeed;
            SetPlaybackSpeed(targetSpeed);
        }

        public void DisableFastForward()
        {
            SetPlaybackSpeed(_config.DefaultPlaybackSpeed);
        }
        #endregion

        #region Wait State Management
        public async UniTask WaitForStateAsync(PlaybackState targetState, CancellationToken cancellationToken = default)
        {
            while (State != targetState && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(cancellationToken);
            }
            
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async UniTask WaitForStateChangeAsync(CancellationToken cancellationToken = default)
        {
            var currentState = State;
            while (State == currentState && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(cancellationToken);
            }
            
            cancellationToken.ThrowIfCancellationRequested();
        }
        #endregion

        #region Private Methods
        private void ValidateTransition(PlaybackState newState)
        {
            var currentState = State;
            
            // Define invalid transitions
            var invalidTransitions = new[]
            {
                (PlaybackState.Failed, PlaybackState.Playing),
                (PlaybackState.Completed, PlaybackState.Playing),
                (PlaybackState.Completed, PlaybackState.Waiting)
            };

            foreach (var (from, to) in invalidTransitions)
            {
                if (currentState == from && newState == to)
                {
                    throw new InvalidOperationException($"Invalid state transition from {from} to {to}");
                }
            }
        }

        private void LogStateTransition(string action)
        {
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"[StateManager] Script execution {action.ToLower()}");
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the PlaybackStateManager and clean up all events
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Clear event handlers to prevent memory leaks - simple null assignment
                StateChanged = null;

                if (_config.LogExecutionFlow)
                    Debug.Log("[PlaybackStateManager] Disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlaybackStateManager] Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
        #endregion
    }
}
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base implementation of IAudioPlayer with comprehensive audio control and effects
    /// </summary>
    public class AudioPlayer : IAudioPlayer, IDisposable
    {
        #region Private Fields
        
        private readonly AudioClip _audioClip;
        private readonly AudioServiceConfiguration _config;
        private CancellationTokenSource _playerCts;
        private Tween _volumeTween;
        private bool _disposed = false;
        
        #endregion
        
        #region Protected Fields
        
        /// <summary>
        /// The AudioSource component used by this player
        /// </summary>
        protected readonly AudioSource _audioSource;
        
        #endregion
        
        #region Properties
        
        public string AudioId { get; private set; }
        public AudioCategory Category { get; private set; }
        
        public float Volume 
        { 
            get => _audioSource.volume;
            set => _audioSource.volume = Mathf.Clamp01(value);
        }
        
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        
        public bool IsPaused { get; private set; }
        
        public bool IsLooping 
        { 
            get => _audioSource.loop;
            set => _audioSource.loop = value;
        }
        
        public float Time 
        { 
            get => _audioSource.time;
            set => _audioSource.time = Mathf.Clamp(value, 0f, Length);
        }
        
        public float Length => _audioClip?.length ?? 0f;
        
        #endregion
        
        #region Events
        
        public event Action<IAudioPlayer> OnPlaybackComplete;
        public Action<IAudioPlayer, string> OnPlaybackError { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new AudioPlayer instance
        /// </summary>
        /// <param name="audioId">Unique identifier for this audio</param>
        /// <param name="category">Audio category</param>
        /// <param name="audioSource">Unity AudioSource component</param>
        /// <param name="audioClip">Audio clip to play</param>
        /// <param name="config">Audio service configuration</param>
        public AudioPlayer(string audioId, AudioCategory category, AudioSource audioSource, AudioClip audioClip, AudioServiceConfiguration config)
        {
            AudioId = audioId ?? throw new ArgumentNullException(nameof(audioId));
            Category = category;
            _audioSource = audioSource ?? throw new ArgumentNullException(nameof(audioSource));
            _audioClip = audioClip ?? throw new ArgumentNullException(nameof(audioClip));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _playerCts = new CancellationTokenSource();
            
            InitializeAudioSource();
        }
        
        #endregion
        
        #region Playback Control
        
        public virtual async UniTask<IAudioPlayer> PlayAsync(AudioPlayOptions options = default, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_disposed)
                {
                    OnPlaybackError?.Invoke(this, "AudioPlayer has been disposed");
                    return this;
                }
                
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _playerCts.Token).Token;
                
                // Apply playback options
                ApplyPlaybackOptions(options);
                
                // Handle fade in
                if (options.FadeInDuration > 0f)
                {
                    _audioSource.volume = 0f;
                    _audioSource.Play();
                    
                    await FadeVolumeAsync(options.Volume, options.FadeInDuration, AudioFadeType.EaseIn, combinedToken);
                }
                else
                {
                    _audioSource.Play();
                }
                
                IsPaused = false;
                
                // Monitor playback completion if not looping
                if (!IsLooping)
                {
                    _ = MonitorPlaybackCompletion(combinedToken);
                }
                
                return this;
            }
            catch (Exception ex)
            {
                OnPlaybackError?.Invoke(this, ex.Message);
                throw;
            }
        }
        
        public async UniTask StopAsync(float fadeOut = 0f, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_disposed || !IsPlaying)
                    return;
                
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _playerCts.Token).Token;
                
                if (fadeOut > 0f)
                {
                    await FadeVolumeAsync(0f, fadeOut, AudioFadeType.EaseOut, combinedToken);
                }
                
                _audioSource.Stop();
                IsPaused = false;
                
                // Invoke completion event
                OnPlaybackComplete?.Invoke(this);
            }
            catch (Exception ex)
            {
                OnPlaybackError?.Invoke(this, ex.Message);
            }
        }
        
        public void Pause()
        {
            if (_disposed || !IsPlaying)
                return;
            
            _audioSource.Pause();
            IsPaused = true;
        }
        
        public void Resume()
        {
            if (_disposed || !IsPaused)
                return;
            
            _audioSource.UnPause();
            IsPaused = false;
        }
        
        #endregion
        
        #region Audio Effects
        
        public async UniTask FadeVolumeAsync(float targetVolume, float duration, AudioFadeType fadeType = AudioFadeType.Linear, CancellationToken cancellationToken = default)
        {
            if (_disposed || duration <= 0f)
            {
                Volume = targetVolume;
                return;
            }
            
            try
            {
                // Kill existing volume tween
                _volumeTween?.Kill();
                
                var startVolume = Volume;
                var ease = GetEaseFromFadeType(fadeType);
                
                _volumeTween = DOTween.To(() => startVolume, x => Volume = x, targetVolume, duration)
                    .SetEase(ease)
                    .OnComplete(() => _volumeTween = null);
                
                await _volumeTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _volumeTween?.Kill();
                throw;
            }
            catch (Exception ex)
            {
                OnPlaybackError?.Invoke(this, $"Volume fade failed: {ex.Message}");
            }
        }
        
        public void SetPosition(Vector3 position)
        {
            if (_disposed)
                return;
            
            _audioSource.transform.position = position;
            
            // Enable 3D spatial audio if position is not zero
            if (position != Vector3.zero && _config.EnableSpatialAudio)
            {
                _audioSource.spatialBlend = 1f; // Full 3D
                _audioSource.maxDistance = _config.MaxAudioDistance;
                _audioSource.rolloffMode = _config.DefaultRolloffMode;
                _audioSource.dopplerLevel = _config.DopplerLevel;
            }
            else
            {
                _audioSource.spatialBlend = 0f; // 2D audio
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeAudioSource()
        {
            _audioSource.clip = _audioClip;
            _audioSource.playOnAwake = false;
            _audioSource.enabled = true;
            
            // Set default audio source properties based on category
            switch (Category)
            {
                case AudioCategory.Music:
                    _audioSource.priority = 64;  // Lower priority number = higher priority
                    _audioSource.spatialBlend = 0f; // 2D audio
                    break;
                    
                case AudioCategory.SFX:
                    _audioSource.priority = 128;
                    _audioSource.spatialBlend = _config.EnableSpatialAudio ? 0.5f : 0f;
                    break;
                    
                case AudioCategory.Voice:
                    _audioSource.priority = 0;   // Highest priority
                    _audioSource.spatialBlend = 0f; // 2D audio
                    break;
                    
                case AudioCategory.Ambient:
                    _audioSource.priority = 200;
                    _audioSource.spatialBlend = _config.EnableSpatialAudio ? 1f : 0f;
                    break;
                    
                case AudioCategory.UI:
                    _audioSource.priority = 32;
                    _audioSource.spatialBlend = 0f; // Always 2D
                    break;
                    
                case AudioCategory.System:
                    _audioSource.priority = 16;
                    _audioSource.spatialBlend = 0f; // Always 2D
                    break;
            }
        }
        
        private void ApplyPlaybackOptions(AudioPlayOptions options)
        {
            // Apply volume
            Volume = options.Volume;
            
            // Apply looping
            IsLooping = options.Loop;
            
            // Apply spatial positioning
            if (options.Position != Vector3.zero)
            {
                SetPosition(options.Position);
            }
            
            // Apply spatial mode
            switch (options.SpatialMode)
            {
                case SpatialAudioMode.None:
                    _audioSource.spatialBlend = 0f;
                    break;
                case SpatialAudioMode.Simple:
                    _audioSource.spatialBlend = 0.5f;
                    break;
                case SpatialAudioMode.Full3D:
                    _audioSource.spatialBlend = 1f;
                    break;
                case SpatialAudioMode.Environmental:
                    _audioSource.spatialBlend = 1f;
                    // Additional environmental effects would be applied here
                    break;
            }
            
            // Apply priority
            _audioSource.priority = GetUnityPriorityFromAudioPriority(options.Priority);
        }
        
        private int GetUnityPriorityFromAudioPriority(AudioPriority priority)
        {
            return priority switch
            {
                AudioPriority.Critical => 0,
                AudioPriority.High => 32,
                AudioPriority.Normal => 128,
                AudioPriority.Low => 256,
                _ => 128
            };
        }
        
        private Ease GetEaseFromFadeType(AudioFadeType fadeType)
        {
            return fadeType switch
            {
                AudioFadeType.Linear => Ease.Linear,
                AudioFadeType.EaseIn => Ease.InQuad,
                AudioFadeType.EaseOut => Ease.OutQuad,
                AudioFadeType.EaseInOut => Ease.InOutQuad,
                AudioFadeType.Custom => Ease.OutQuad, // Default for custom
                _ => Ease.Linear
            };
        }
        
        private async UniTask MonitorPlaybackCompletion(CancellationToken cancellationToken)
        {
            try
            {
                // Wait for audio to finish playing
                while (IsPlaying && !cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                }
                
                // Only invoke completion if we weren't cancelled and the audio actually finished
                if (!cancellationToken.IsCancellationRequested && !IsPlaying && !IsPaused)
                {
                    await OnPlaybackCompleteAsync();
                    OnPlaybackComplete?.Invoke(this);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping or disposing
            }
        }
        
        /// <summary>
        /// Virtual method called when playback completes, can be overridden by derived classes
        /// </summary>
        protected virtual async UniTask OnPlaybackCompleteAsync()
        {
            await UniTask.Yield();
        }
        
        #endregion
        
        #region Disposal
        
        public virtual void Dispose()
        {
            if (_disposed)
                return;
            
            // Kill any active tweens
            _volumeTween?.Kill();
            
            // Stop playback
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            
            // Cancel any ongoing operations
            _playerCts?.Cancel();
            _playerCts?.Dispose();
            
            // Disable audio source for pooling
            if (_audioSource != null)
            {
                _audioSource.enabled = false;
                _audioSource.clip = null;
            }
            
            _disposed = true;
        }
        
        #endregion
    }
}
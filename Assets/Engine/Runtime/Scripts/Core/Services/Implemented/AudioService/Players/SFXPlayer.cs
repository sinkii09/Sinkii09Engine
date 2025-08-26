using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Specialized audio player for sound effects with pooling and rapid-fire support
    /// </summary>
    public class SFXPlayer : AudioPlayer
    {
        #region Private Fields
        
        private readonly AudioServiceConfiguration _sfxConfig;
        private readonly Queue<float> _playbackTimes = new Queue<float>();
        private readonly float _rapidFireThreshold = 0.05f; // 50ms between plays
        private int _instanceCount = 0;
        private bool _autoRelease = true;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Whether this SFX should auto-release to pool when complete
        /// </summary>
        public bool AutoRelease
        {
            get => _autoRelease;
            set => _autoRelease = value;
        }
        
        /// <summary>
        /// Number of instances of this SFX currently playing
        /// </summary>
        public int InstanceCount => _instanceCount;
        
        /// <summary>
        /// Whether this is a rapid-fire SFX (multiple plays in quick succession)
        /// </summary>
        public bool IsRapidFire => _playbackTimes.Count > 2;
        
        /// <summary>
        /// Priority override for this SFX
        /// </summary>
        public AudioPriority PriorityOverride { get; set; } = AudioPriority.Normal;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when SFX instance limit is reached
        /// </summary>
        public static event Action<string, int> OnInstanceLimitReached;
        
        #endregion
        
        #region Constructor
        
        public SFXPlayer(string audioId, AudioSource audioSource, AudioClip audioClip, AudioServiceConfiguration config)
            : base(audioId, AudioCategory.SFX, audioSource, audioClip, config)
        {
            _sfxConfig = config;
            
            // SFX-specific defaults
            IsLooping = false; // SFX typically don't loop
            _autoRelease = true; // Auto-return to pool when done
        }
        
        #endregion
        
        #region Rapid-Fire Management
        
        /// <summary>
        /// Tracks rapid-fire playback for optimization
        /// </summary>
        public void TrackPlayback()
        {
            var currentTime = UnityEngine.Time.time;
            
            // Add current playback time
            _playbackTimes.Enqueue(currentTime);
            
            // Remove old entries (older than 1 second)
            while (_playbackTimes.Count > 0 && currentTime - _playbackTimes.Peek() > 1f)
            {
                _playbackTimes.Dequeue();
            }
            
            // Check for rapid-fire pattern
            if (_playbackTimes.Count > 3)
            {
                OptimizeForRapidFire();
            }
        }
        
        /// <summary>
        /// Optimizes settings for rapid-fire playback
        /// </summary>
        private void OptimizeForRapidFire()
        {
            // Reduce volume slightly to prevent audio clipping
            Volume = Mathf.Min(Volume, 0.8f);
            
            // Increase priority to ensure playback
            PriorityOverride = AudioPriority.High;
        }
        
        #endregion
        
        #region Instance Management
        
        /// <summary>
        /// Increments instance count for this SFX type
        /// </summary>
        public static Dictionary<string, int> InstanceCounts = new Dictionary<string, int>();
        
        /// <summary>
        /// Checks if this SFX can play based on instance limits
        /// </summary>
        public bool CanPlay()
        {
            if (!_sfxConfig.EnableSFXPriority)
                return true;
            
            // Get current instance count for this audio ID
            if (!InstanceCounts.TryGetValue(AudioId, out int count))
            {
                count = 0;
            }
            
            // Check against max simultaneous SFX
            if (count >= _sfxConfig.MaxSimultaneousSFX)
            {
                OnInstanceLimitReached?.Invoke(AudioId, count);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Registers this instance
        /// </summary>
        public void RegisterInstance()
        {
            if (!InstanceCounts.ContainsKey(AudioId))
            {
                InstanceCounts[AudioId] = 0;
            }
            
            InstanceCounts[AudioId]++;
            _instanceCount = InstanceCounts[AudioId];
        }
        
        /// <summary>
        /// Unregisters this instance
        /// </summary>
        public void UnregisterInstance()
        {
            if (InstanceCounts.ContainsKey(AudioId))
            {
                InstanceCounts[AudioId] = Math.Max(0, InstanceCounts[AudioId] - 1);
                _instanceCount = InstanceCounts[AudioId];
            }
        }
        
        #endregion
        
        #region Playback Overrides
        
        public override async UniTask<IAudioPlayer> PlayAsync(AudioPlayOptions options = default, CancellationToken cancellationToken = default)
        {
            // Check if can play
            if (!CanPlay())
            {
                OnPlaybackError?.Invoke(this, "Instance limit reached");
                return this;
            }
            
            // Track playback for rapid-fire detection
            TrackPlayback();
            
            // Register this instance
            RegisterInstance();
            
            // Apply priority override if set
            if (PriorityOverride != AudioPriority.Normal)
            {
                options.Priority = PriorityOverride;
            }
            
            return await base.PlayAsync(options, cancellationToken);
        }
        
        protected override async UniTask OnPlaybackCompleteAsync()
        {
            await base.OnPlaybackCompleteAsync();
            
            // Unregister instance
            UnregisterInstance();
            
            // Auto-release to pool if enabled
            if (_autoRelease)
            {
                // This will be handled by the AudioService/Factory
                Dispose();
            }
        }
        
        #endregion
        
        #region Spatial Effects
        
        /// <summary>
        /// Applies impact effect based on surface type
        /// </summary>
        public void ApplyImpactEffect(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Metal:
                    // Add metallic reverb
                    ApplyReverb(0.8f, 0.5f);
                    break;
                    
                case SurfaceType.Wood:
                    // Dampen high frequencies
                    ApplyLowPassFilter(5000f);
                    break;
                    
                case SurfaceType.Water:
                    // Underwater effect
                    ApplyLowPassFilter(1000f);
                    ApplyReverb(0.3f, 0.8f);
                    break;
                    
                case SurfaceType.Glass:
                    // Sharp, bright sound
                    ApplyHighPassFilter(2000f);
                    break;
            }
        }
        
        /// <summary>
        /// Applies reverb effect
        /// </summary>
        private void ApplyReverb(float roomSize, float wetLevel)
        {
            // This would integrate with Unity's AudioReverbFilter
            // Implementation depends on Unity audio filter components
        }
        
        /// <summary>
        /// Applies low-pass filter
        /// </summary>
        private void ApplyLowPassFilter(float cutoffFrequency)
        {
            if (_audioSource == null) return;
            
            var lowPassFilter = _audioSource.GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
            {
                lowPassFilter = _audioSource.gameObject.AddComponent<AudioLowPassFilter>();
            }
            lowPassFilter.cutoffFrequency = cutoffFrequency;
        }
        
        /// <summary>
        /// Applies high-pass filter
        /// </summary>
        private void ApplyHighPassFilter(float cutoffFrequency)
        {
            if (_audioSource == null) return;
            
            var highPassFilter = _audioSource.GetComponent<AudioHighPassFilter>();
            if (highPassFilter == null)
            {
                highPassFilter = _audioSource.gameObject.AddComponent<AudioHighPassFilter>();
            }
            highPassFilter.cutoffFrequency = cutoffFrequency;
        }
        
        #endregion
        
        #region Randomization
        
        /// <summary>
        /// Applies random pitch variation for variety
        /// </summary>
        public void RandomizePitch(float minPitch = 0.9f, float maxPitch = 1.1f)
        {
            _audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        }
        
        /// <summary>
        /// Applies random volume variation
        /// </summary>
        public void RandomizeVolume(float minVolume = 0.8f, float maxVolume = 1.0f)
        {
            Volume = UnityEngine.Random.Range(minVolume, maxVolume);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Surface types for impact sound effects
    /// </summary>
    public enum SurfaceType
    {
        Default,
        Metal,
        Wood,
        Stone,
        Glass,
        Water,
        Grass,
        Sand,
        Concrete
    }
    
    /// <summary>
    /// SFX pool for managing multiple instances
    /// </summary>
    public class SFXPool
    {
        private readonly Queue<SFXPlayer> _availablePlayers = new Queue<SFXPlayer>();
        private readonly List<SFXPlayer> _activePlayers = new List<SFXPlayer>();
        private readonly AudioServiceConfiguration _config;
        private readonly string _sfxId;
        private readonly AudioClip _clip;
        
        public SFXPool(string sfxId, AudioClip clip, AudioServiceConfiguration config, int initialSize = 3)
        {
            _sfxId = sfxId;
            _clip = clip;
            _config = config;
            
            // Pre-populate pool
            for (int i = 0; i < initialSize; i++)
            {
                var player = CreateNewPlayer();
                _availablePlayers.Enqueue(player);
            }
        }
        
        /// <summary>
        /// Gets an SFX player from the pool
        /// </summary>
        public SFXPlayer GetPlayer()
        {
            SFXPlayer player;
            
            if (_availablePlayers.Count > 0)
            {
                player = _availablePlayers.Dequeue();
            }
            else
            {
                player = CreateNewPlayer();
            }
            
            _activePlayers.Add(player);
            return player;
        }
        
        /// <summary>
        /// Returns a player to the pool
        /// </summary>
        public void ReturnPlayer(SFXPlayer player)
        {
            if (_activePlayers.Remove(player))
            {
                // Reset player state
                player.Volume = 1f;
                player.Time = 0f;
                player.PriorityOverride = AudioPriority.Normal;
                
                _availablePlayers.Enqueue(player);
            }
        }
        
        private SFXPlayer CreateNewPlayer()
        {
            var go = new GameObject($"SFX_{_sfxId}");
            var audioSource = go.AddComponent<AudioSource>();
            return new SFXPlayer(_sfxId, audioSource, _clip, _config);
        }
    }
}
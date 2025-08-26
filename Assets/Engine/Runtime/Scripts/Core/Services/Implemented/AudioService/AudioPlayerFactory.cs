using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Factory class for creating AudioPlayer instances
    /// </summary>
    public class AudioPlayerFactory
    {
        private readonly AudioServiceConfiguration _config;
        private readonly Queue<AudioSource> _audioSourcePool;
        private readonly GameObject _audioSourceParent;
        private readonly Dictionary<string, SFXPool> _sfxPools = new Dictionary<string, SFXPool>();
        
        public AudioPlayerFactory(AudioServiceConfiguration config, Queue<AudioSource> audioSourcePool, GameObject audioSourceParent)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _audioSourcePool = audioSourcePool ?? throw new ArgumentNullException(nameof(audioSourcePool));
            _audioSourceParent = audioSourceParent;
        }
        
        /// <summary>
        /// Creates a new AudioPlayer instance with pooled AudioSource
        /// </summary>
        public AudioPlayer CreateAudioPlayer(string audioId, AudioCategory category, AudioClip audioClip)
        {
            var audioSource = GetPooledAudioSource();
            
            // Create specialized player based on category
            return category switch
            {
                AudioCategory.Music => new MusicPlayer(audioId, audioSource, audioClip, _config),
                AudioCategory.SFX => CreateSFXPlayer(audioId, audioSource, audioClip),
                AudioCategory.Ambient => new AmbientPlayer(audioId, audioSource, audioClip, _config),
                AudioCategory.UI => new UIPlayer(audioId, audioSource, audioClip, _config),
                AudioCategory.Voice => new AudioPlayer(audioId, category, audioSource, audioClip, _config), // Use base for now
                AudioCategory.System => new UIPlayer(audioId, audioSource, audioClip, _config), // System uses UI player
                _ => new AudioPlayer(audioId, category, audioSource, audioClip, _config)
            };
        }
        
        /// <summary>
        /// Creates an SFX player with pooling support
        /// </summary>
        private SFXPlayer CreateSFXPlayer(string audioId, AudioSource audioSource, AudioClip audioClip)
        {
            // Use SFX pooling if enabled
            if (_config.EnableSFXPooling)
            {
                if (!_sfxPools.TryGetValue(audioId, out var pool))
                {
                    pool = new SFXPool(audioId, audioClip, _config, 3);
                    _sfxPools[audioId] = pool;
                }
                return pool.GetPlayer();
            }
            
            return new SFXPlayer(audioId, audioSource, audioClip, _config);
        }
        
        /// <summary>
        /// Returns an AudioPlayer's AudioSource to the pool
        /// </summary>
        public void ReturnAudioPlayer(AudioPlayer player)
        {
            if (player == null)
                return;
            
            player.Dispose();
            
            // The AudioSource is already reset in the Dispose method
            // Add it back to the pool if pooling is enabled
            if (_config.EnableAudioPooling && _audioSourcePool.Count < _config.AudioSourcePoolSize * 2)
            {
                // AudioSource is already cleaned up in AudioPlayer.Dispose()
                // Pool management would be handled by the AudioService
            }
        }
        
        private AudioSource GetPooledAudioSource()
        {
            if (_config.EnableAudioPooling && _audioSourcePool.Count > 0)
            {
                return _audioSourcePool.Dequeue();
            }
            else
            {
                // Create new audio source if pool is empty
                return CreateNewAudioSource();
            }
        }
        
        private AudioSource CreateNewAudioSource()
        {
            var go = new GameObject("AudioPlayer_AudioSource");
            if (_audioSourceParent != null)
            {
                go.transform.SetParent(_audioSourceParent.transform);
            }
            
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            
            return audioSource;
        }
    }
}
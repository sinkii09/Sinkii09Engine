using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core audio service interface with complete audio management capabilities
    /// Provides comprehensive audio playback, mixing, and resource management
    /// </summary>
    public interface IAudioService : IEngineService
    {
        #region Audio Playback Control
        
        /// <summary>
        /// Plays audio with specified category and options
        /// </summary>
        /// <param name="audioId">Audio resource identifier</param>
        /// <param name="category">Audio category for proper mixing</param>
        /// <param name="options">Playback configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Audio player instance for playback control</returns>
        UniTask<IAudioPlayer> PlayAsync(string audioId, AudioCategory category, AudioPlayOptions options = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Plays music with crossfade support
        /// </summary>
        /// <param name="musicId">Music resource identifier</param>
        /// <param name="fadeIn">Fade in duration in seconds</param>
        /// <param name="loop">Whether music should loop</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Music player instance</returns>
        UniTask<IAudioPlayer> PlayMusicAsync(string musicId, float fadeIn = 0f, bool loop = true, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Plays sound effect with priority and pooling support
        /// </summary>
        /// <param name="sfxId">Sound effect resource identifier</param>
        /// <param name="volume">Playback volume (0.0 to 1.0)</param>
        /// <param name="position">3D position for spatial audio</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SFX player instance</returns>
        UniTask<IAudioPlayer> PlaySFXAsync(string sfxId, float volume = 1f, Vector3 position = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Plays character voice with dialogue integration
        /// </summary>
        /// <param name="characterId">Character identifier</param>
        /// <param name="voiceId">Voice line identifier</param>
        /// <param name="duck">Whether to duck background audio</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Voice player instance</returns>
        UniTask<IAudioPlayer> PlayVoiceAsync(string characterId, string voiceId, bool duck = true, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Plays ambient audio with layering support
        /// </summary>
        /// <param name="ambientId">Ambient audio identifier</param>
        /// <param name="loop">Whether ambient should loop</param>
        /// <param name="volume">Playback volume</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Ambient player instance</returns>
        UniTask<IAudioPlayer> PlayAmbientAsync(string ambientId, bool loop = true, float volume = 0.6f, CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Audio Control Operations
        
        /// <summary>
        /// Stops specific audio by ID
        /// </summary>
        /// <param name="audioId">Audio identifier to stop</param>
        /// <param name="fadeOut">Fade out duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask StopAsync(string audioId, float fadeOut = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops all audio in specified category
        /// </summary>
        /// <param name="category">Category to stop</param>
        /// <param name="fadeOut">Fade out duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask StopCategoryAsync(AudioCategory category, float fadeOut = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops all currently playing audio
        /// </summary>
        /// <param name="fadeOut">Fade out duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask StopAllAsync(float fadeOut = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Pauses specific audio by ID
        /// </summary>
        /// <param name="audioId">Audio identifier to pause</param>
        void Pause(string audioId);
        
        /// <summary>
        /// Pauses all audio in category
        /// </summary>
        /// <param name="category">Category to pause</param>
        void PauseCategory(AudioCategory category);
        
        /// <summary>
        /// Pauses all audio
        /// </summary>
        void PauseAll();
        
        /// <summary>
        /// Resumes specific audio by ID
        /// </summary>
        /// <param name="audioId">Audio identifier to resume</param>
        void Resume(string audioId);
        
        /// <summary>
        /// Resumes all audio in category
        /// </summary>
        /// <param name="category">Category to resume</param>
        void ResumeCategory(AudioCategory category);
        
        /// <summary>
        /// Resumes all paused audio
        /// </summary>
        void ResumeAll();
        
        #endregion
        
        #region Volume and Mixing Control
        
        /// <summary>
        /// Sets master volume level
        /// </summary>
        /// <param name="volume">Volume level (0.0 to 1.0)</param>
        void SetMasterVolume(float volume);
        
        /// <summary>
        /// Gets current master volume level
        /// </summary>
        /// <returns>Master volume (0.0 to 1.0)</returns>
        float GetMasterVolume();
        
        /// <summary>
        /// Sets volume for specific audio category
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <param name="volume">Volume level (0.0 to 1.0)</param>
        void SetCategoryVolume(AudioCategory category, float volume);
        
        /// <summary>
        /// Gets volume for specific audio category
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <returns>Category volume (0.0 to 1.0)</returns>
        float GetCategoryVolume(AudioCategory category);
        
        /// <summary>
        /// Mutes or unmutes specific audio category
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <param name="muted">Mute state</param>
        void SetCategoryMuted(AudioCategory category, bool muted);
        
        /// <summary>
        /// Gets mute state for audio category
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <returns>True if category is muted</returns>
        bool GetCategoryMuted(AudioCategory category);
        
        #endregion
        
        #region Audio Mixer Integration
        
        /// <summary>
        /// Applies audio mixer snapshot
        /// </summary>
        /// <param name="snapshotName">Snapshot identifier</param>
        /// <param name="transitionTime">Transition duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask ApplySnapshotAsync(string snapshotName, float transitionTime = 1f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Sets audio mixer parameter
        /// </summary>
        /// <param name="parameterName">Parameter name</param>
        /// <param name="value">Parameter value</param>
        void SetMixerParameter(string parameterName, float value);
        
        /// <summary>
        /// Gets audio mixer parameter value
        /// </summary>
        /// <param name="parameterName">Parameter name</param>
        /// <returns>Parameter value</returns>
        float GetMixerParameter(string parameterName);
        
        #endregion
        
        #region Audio State Management
        
        /// <summary>
        /// Gets current audio state for save/load operations
        /// </summary>
        /// <returns>Complete audio state</returns>
        AudioState GetAudioState();
        
        /// <summary>
        /// Applies audio state from save data
        /// </summary>
        /// <param name="state">Audio state to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask ApplyAudioStateAsync(AudioState state, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Resets audio to default state
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask ResetToDefaultStateAsync(CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Audio Information and Status
        
        /// <summary>
        /// Checks if specific audio is currently playing
        /// </summary>
        /// <param name="audioId">Audio identifier</param>
        /// <returns>True if audio is playing</returns>
        bool IsPlaying(string audioId);
        
        /// <summary>
        /// Checks if any audio in category is playing
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <returns>True if category has playing audio</returns>
        bool IsCategoryPlaying(AudioCategory category);
        
        /// <summary>
        /// Gets all currently active audio players
        /// </summary>
        /// <returns>Collection of active audio players</returns>
        IReadOnlyCollection<IAudioPlayer> GetActivePlayers();
        
        /// <summary>
        /// Gets active players for specific category
        /// </summary>
        /// <param name="category">Audio category</param>
        /// <returns>Collection of category audio players</returns>
        IReadOnlyCollection<IAudioPlayer> GetCategoryPlayers(AudioCategory category);
        
        /// <summary>
        /// Gets audio player by ID
        /// </summary>
        /// <param name="audioId">Audio identifier</param>
        /// <returns>Audio player instance or null</returns>
        IAudioPlayer GetPlayer(string audioId);
        
        #endregion
        
        #region Resource Management
        
        /// <summary>
        /// Preloads audio resources for faster playback
        /// </summary>
        /// <param name="audioIds">Audio identifiers to preload</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask PreloadAudioAsync(string[] audioIds, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads audio resources to free memory
        /// </summary>
        /// <param name="audioIds">Audio identifiers to unload</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask UnloadAudioAsync(string[] audioIds, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Clears all cached audio resources
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask ClearAudioCacheAsync(CancellationToken cancellationToken = default);
        
        #endregion
        
        #region Performance and Debugging
        
        /// <summary>
        /// Gets audio service performance statistics
        /// </summary>
        /// <returns>Performance metrics and statistics</returns>
        AudioServiceStatistics GetStatistics();
        
        /// <summary>
        /// Gets debug information for troubleshooting
        /// </summary>
        /// <returns>Debug information string</returns>
        string GetDebugInfo();
        
        /// <summary>
        /// Validates all audio configurations and resources
        /// </summary>
        /// <returns>Validation results with error messages</returns>
        Dictionary<string, string[]> ValidateAudioResources();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when any audio starts playing
        /// </summary>
        event Action<string, AudioCategory> OnAudioStarted;
        
        /// <summary>
        /// Fired when any audio stops playing
        /// </summary>
        event Action<string, AudioCategory> OnAudioStopped;
        
        /// <summary>
        /// Fired when audio volume changes
        /// </summary>
        event Action<AudioCategory, float> OnVolumeChanged;
        
        /// <summary>
        /// Fired when audio category mute state changes
        /// </summary>
        event Action<AudioCategory, bool> OnMuteChanged;
        
        /// <summary>
        /// Fired when audio mixer snapshot changes
        /// </summary>
        event Action<string> OnSnapshotChanged;
        
        /// <summary>
        /// Fired when audio error occurs
        /// </summary>
        event Action<string, string> OnAudioError;
        
        #endregion
    }
    
    /// <summary>
    /// Interface for individual audio player instances
    /// </summary>
    public interface IAudioPlayer
    {
        #region Properties
        
        /// <summary>
        /// Unique identifier for this audio instance
        /// </summary>
        string AudioId { get; }
        
        /// <summary>
        /// Audio category for this player
        /// </summary>
        AudioCategory Category { get; }
        
        /// <summary>
        /// Current playback volume
        /// </summary>
        float Volume { get; set; }
        
        /// <summary>
        /// Whether audio is currently playing
        /// </summary>
        bool IsPlaying { get; }
        
        /// <summary>
        /// Whether audio is paused
        /// </summary>
        bool IsPaused { get; }
        
        /// <summary>
        /// Whether audio is set to loop
        /// </summary>
        bool IsLooping { get; set; }
        
        /// <summary>
        /// Current playback position in seconds
        /// </summary>
        float Time { get; set; }
        
        /// <summary>
        /// Total length of audio clip in seconds
        /// </summary>
        float Length { get; }
        
        #endregion
        
        #region Playback Control
        
        /// <summary>
        /// Starts audio playback
        /// </summary>
        /// <param name="options">Playback options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask<IAudioPlayer> PlayAsync(AudioPlayOptions options = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops audio playback
        /// </summary>
        /// <param name="fadeOut">Fade out duration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask StopAsync(float fadeOut = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Pauses audio playback
        /// </summary>
        void Pause();
        
        /// <summary>
        /// Resumes audio playback
        /// </summary>
        void Resume();
        
        #endregion
        
        #region Audio Effects
        
        /// <summary>
        /// Fades volume to target level
        /// </summary>
        /// <param name="targetVolume">Target volume level</param>
        /// <param name="duration">Fade duration</param>
        /// <param name="fadeType">Fade curve type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        UniTask FadeVolumeAsync(float targetVolume, float duration, AudioFadeType fadeType = AudioFadeType.Linear, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Sets 3D spatial audio position
        /// </summary>
        /// <param name="position">3D world position</param>
        void SetPosition(Vector3 position);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when audio playback completes
        /// </summary>
        event Action<IAudioPlayer> OnPlaybackComplete;
        
        /// <summary>
        /// Fired when audio playback error occurs
        /// </summary>
        Action<IAudioPlayer, string> OnPlaybackError { get; set; }

            #endregion
    }
    
    /// <summary>
    /// Audio service statistics and performance metrics
    /// </summary>
    public class AudioServiceStatistics
    {
        #region Audio Counts
        
        /// <summary>
        /// Total number of active audio players
        /// </summary>
        public int ActivePlayers { get; set; }
        
        /// <summary>
        /// Number of players by category
        /// </summary>
        public Dictionary<AudioCategory, int> PlayersByCategory { get; set; } = new Dictionary<AudioCategory, int>();
        
        /// <summary>
        /// Total number of cached audio clips
        /// </summary>
        public int CachedClips { get; set; }
        
        /// <summary>
        /// Number of audio clips currently loading
        /// </summary>
        public int LoadingClips { get; set; }
        
        #endregion
        
        #region Performance Metrics
        
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// Average audio loading time
        /// </summary>
        public TimeSpan AverageLoadTime { get; set; }
        
        /// <summary>
        /// Total audio playback time
        /// </summary>
        public TimeSpan TotalPlaybackTime { get; set; }
        
        /// <summary>
        /// Number of audio mixer operations per second
        /// </summary>
        public float MixerOperationsPerSecond { get; set; }
        
        #endregion
        
        #region Status Information
        
        /// <summary>
        /// Current master volume level
        /// </summary>
        public float MasterVolume { get; set; }
        
        /// <summary>
        /// Active audio mixer snapshot
        /// </summary>
        public string ActiveSnapshot { get; set; }
        
        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdateTime { get; set; }
        
        /// <summary>
        /// Service health status
        /// </summary>
        public string HealthStatus { get; set; } = "Healthy";
        
        #endregion
    }
}
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Specialized audio player for music with playlist and crossfade support
    /// </summary>
    public class MusicPlayer : AudioPlayer
    {
        #region Private Fields
        
        private readonly Queue<string> _playlist = new Queue<string>();
        private readonly AudioServiceConfiguration _musicConfig;
        private bool _playlistMode = false;
        private bool _shuffle = false;
        private bool _repeatPlaylist = false;
        private string _nextTrackId;
        private IAudioPlayer _crossfadePlayer;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Current playlist queue
        /// </summary>
        public IReadOnlyCollection<string> Playlist => _playlist.ToArray();
        
        /// <summary>
        /// Whether playlist mode is active
        /// </summary>
        public bool IsPlaylistMode => _playlistMode;
        
        /// <summary>
        /// Whether shuffle is enabled
        /// </summary>
        public bool ShuffleEnabled => _shuffle;
        
        /// <summary>
        /// Whether playlist repeat is enabled
        /// </summary>
        public bool RepeatEnabled => _repeatPlaylist;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when track changes in playlist
        /// </summary>
        public event Action<string, string> OnTrackChanged;
        
        /// <summary>
        /// Fired when playlist completes
        /// </summary>
        public event Action OnPlaylistComplete;
        
        #endregion
        
        #region Constructor
        
        public MusicPlayer(string audioId, AudioSource audioSource, AudioClip audioClip, AudioServiceConfiguration config)
            : base(audioId, AudioCategory.Music, audioSource, audioClip, config)
        {
            _musicConfig = config;
            
            // Music-specific defaults
            IsLooping = config.MusicLoopByDefault;
        }
        
        #endregion
        
        #region Playlist Management
        
        /// <summary>
        /// Sets up a playlist of music tracks
        /// </summary>
        public void SetPlaylist(IEnumerable<string> trackIds, bool shuffle = false, bool repeat = false)
        {
            _playlist.Clear();
            
            var tracks = shuffle ? ShuffleList(new List<string>(trackIds)) : trackIds;
            
            foreach (var trackId in tracks)
            {
                _playlist.Enqueue(trackId);
            }
            
            _playlistMode = true;
            _shuffle = shuffle;
            _repeatPlaylist = repeat;
        }
        
        /// <summary>
        /// Adds a track to the current playlist
        /// </summary>
        public void AddToPlaylist(string trackId)
        {
            _playlist.Enqueue(trackId);
            
            if (!_playlistMode && _playlist.Count > 0)
            {
                _playlistMode = true;
            }
        }
        
        /// <summary>
        /// Clears the current playlist
        /// </summary>
        public void ClearPlaylist()
        {
            _playlist.Clear();
            _playlistMode = false;
            _nextTrackId = null;
        }
        
        /// <summary>
        /// Skips to the next track in playlist
        /// </summary>
        public async UniTask<bool> SkipToNextAsync(float crossfadeDuration = 0f, CancellationToken cancellationToken = default)
        {
            if (!_playlistMode || _playlist.Count == 0)
                return false;
            
            _nextTrackId = _playlist.Dequeue();
            
            // Re-add to queue if repeat is enabled
            if (_repeatPlaylist)
            {
                _playlist.Enqueue(AudioId);
            }
            
            // Trigger crossfade or immediate stop
            if (crossfadeDuration > 0f && _musicConfig.EnableMusicCrossfade)
            {
                await StartCrossfadeAsync(_nextTrackId, crossfadeDuration, cancellationToken);
            }
            else
            {
                await StopAsync(0f, cancellationToken);
                OnTrackChanged?.Invoke(AudioId, _nextTrackId);
            }
            
            return true;
        }
        
        #endregion
        
        #region Crossfade Support
        
        /// <summary>
        /// Starts crossfade to another music track
        /// </summary>
        public async UniTask StartCrossfadeAsync(string nextTrackId, float duration, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(nextTrackId))
                return;
            
            try
            {
                _nextTrackId = nextTrackId;
                
                // Start fading out current track
                var fadeOutTask = FadeVolumeAsync(0f, duration, AudioFadeType.EaseOut, cancellationToken);
                
                // Notify about track change
                OnTrackChanged?.Invoke(AudioId, nextTrackId);
                
                // Wait for fade to complete
                await fadeOutTask;
                
                // Stop current track
                await StopAsync(0f, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Restore volume if cancelled
                Volume = _musicConfig.DefaultMusicVolume;
                throw;
            }
        }
        
        /// <summary>
        /// Crossfades between two music players
        /// </summary>
        public static async UniTask CrossfadeAsync(MusicPlayer outgoing, MusicPlayer incoming, float duration, CancellationToken cancellationToken = default)
        {
            if (outgoing == null || incoming == null || duration <= 0f)
                return;
            
            // Start both transitions simultaneously
            var fadeOutTask = outgoing.FadeVolumeAsync(0f, duration, AudioFadeType.EaseOut, cancellationToken);
            var fadeInTask = incoming.FadeVolumeAsync(incoming._musicConfig.DefaultMusicVolume, duration, AudioFadeType.EaseIn, cancellationToken);
            
            // Start incoming if not already playing
            if (!incoming.IsPlaying)
            {
                incoming.Volume = 0f;
                _ = incoming.PlayAsync(AudioPlayOptions.Default, cancellationToken);
            }
            
            // Wait for both fades to complete
            await UniTask.WhenAll(fadeOutTask, fadeInTask);
            
            // Stop outgoing track
            await outgoing.StopAsync(0f, cancellationToken);
        }
        
        #endregion
        
        #region Beat Synchronization
        
        /// <summary>
        /// Gets the current beat position for rhythm synchronization
        /// </summary>
        /// <param name="bpm">Beats per minute of the track</param>
        /// <returns>Current beat number</returns>
        public float GetCurrentBeat(float bpm)
        {
            if (bpm <= 0f)
                return 0f;
            
            var beatsPerSecond = bpm / 60f;
            return Time * beatsPerSecond;
        }
        
        /// <summary>
        /// Syncs playback to a specific beat
        /// </summary>
        /// <param name="targetBeat">Target beat number</param>
        /// <param name="bpm">Beats per minute</param>
        public void SyncToBeat(float targetBeat, float bpm)
        {
            if (bpm <= 0f)
                return;
            
            var beatsPerSecond = bpm / 60f;
            var targetTime = targetBeat / beatsPerSecond;
            
            Time = Mathf.Clamp(targetTime, 0f, Length);
        }
        
        #endregion
        
        #region Playback Overrides
        
        protected override async UniTask OnPlaybackCompleteAsync()
        {
            await base.OnPlaybackCompleteAsync();
            
            // Handle playlist progression
            if (_playlistMode && _playlist.Count > 0)
            {
                await SkipToNextAsync(_musicConfig.DefaultCrossfadeDuration, CancellationToken.None);
            }
            else if (_playlistMode && _playlist.Count == 0)
            {
                _playlistMode = false;
                OnPlaylistComplete?.Invoke();
            }
        }
        
        #endregion
        
        #region Private Utility Methods
        
        private List<T> ShuffleList<T>(List<T> list)
        {
            var random = new System.Random();
            int n = list.Count;
            
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            
            return list;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Music playlist management
    /// </summary>
    public class MusicPlaylist
    {
        #region Properties
        
        /// <summary>
        /// Playlist name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Track IDs in playlist
        /// </summary>
        public List<string> TrackIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Whether to shuffle tracks
        /// </summary>
        public bool Shuffle { get; set; }
        
        /// <summary>
        /// Whether to repeat playlist
        /// </summary>
        public bool Repeat { get; set; }
        
        /// <summary>
        /// Crossfade duration between tracks
        /// </summary>
        public float CrossfadeDuration { get; set; } = 3f;
        
        #endregion
        
        #region Constructor
        
        public MusicPlaylist(string name, params string[] trackIds)
        {
            Name = name;
            TrackIds.AddRange(trackIds);
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Adds a track to the playlist
        /// </summary>
        public void AddTrack(string trackId)
        {
            if (!string.IsNullOrEmpty(trackId) && !TrackIds.Contains(trackId))
            {
                TrackIds.Add(trackId);
            }
        }
        
        /// <summary>
        /// Removes a track from the playlist
        /// </summary>
        public void RemoveTrack(string trackId)
        {
            TrackIds.Remove(trackId);
        }
        
        /// <summary>
        /// Clears all tracks
        /// </summary>
        public void Clear()
        {
            TrackIds.Clear();
        }
        
        #endregion
    }
}
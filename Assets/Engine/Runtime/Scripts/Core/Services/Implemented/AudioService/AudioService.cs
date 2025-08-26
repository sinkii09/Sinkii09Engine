using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core Audio Service implementation with Unity AudioMixer integration and comprehensive audio management
    /// Follows enhanced service architecture patterns with dependency injection and configuration
    /// </summary>
    [EngineService(ServiceCategory.Audio, ServicePriority.High,
        Description = "Manages all audio playback, mixing, and resource management with Unity AudioMixer integration")]
    [ServiceConfiguration(typeof(AudioServiceConfiguration))]
    public class AudioService : IAudioService
    {
        #region Dependencies
        
        private readonly AudioServiceConfiguration _config;
        private IResourceService _resourceService;
        private IActorService _actorService;
        
        #endregion
        
        #region Service State
        
        private CancellationTokenSource _serviceCts;
        private bool _disposed = false;
        private bool _initialized = false;
        
        #endregion
        
        #region Audio State Management
        
        private readonly AudioState _currentState = new AudioState();
        private readonly ConcurrentDictionary<string, IAudioPlayer> _activePlayers = new ConcurrentDictionary<string, IAudioPlayer>();
        private readonly Dictionary<AudioCategory, List<IAudioPlayer>> _playersByCategory = new Dictionary<AudioCategory, List<IAudioPlayer>>();
        private readonly Dictionary<AudioCategory, string> _categoryBusMap = new Dictionary<AudioCategory, string>();
        
        #endregion
        
        #region Resource Management
        
        private readonly ConcurrentDictionary<string, AudioClip> _audioCache = new ConcurrentDictionary<string, AudioClip>();
        private readonly ConcurrentDictionary<string, UniTask<AudioClip>> _loadingTasks = new ConcurrentDictionary<string, UniTask<AudioClip>>();
        
        #endregion
        
        #region Audio Mixer Integration
        
        private AudioMixer _masterMixer;
        private AudioMixerSnapshot _currentSnapshot;
        private readonly Dictionary<string, AudioMixerGroup> _mixerGroups = new Dictionary<string, AudioMixerGroup>();
        
        #endregion
        
        #region Object Pooling
        
        private readonly Queue<AudioSource> _audioSourcePool = new Queue<AudioSource>();
        private GameObject _audioSourceParent;
        private AudioPlayerFactory _playerFactory;
        
        #endregion
        
        #region Events
        
        public event Action<string, AudioCategory> OnAudioStarted;
        public event Action<string, AudioCategory> OnAudioStopped;
        public event Action<AudioCategory, float> OnVolumeChanged;
        public event Action<AudioCategory, bool> OnMuteChanged;
        public event Action<string> OnSnapshotChanged;
        public event Action<string, string> OnAudioError;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes AudioService with configuration and dependencies
        /// </summary>
        public AudioService(AudioServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            InitializeCategoryMappings();
            InitializePlayerCategories();
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
                _resourceService = provider.GetService(typeof(IResourceService)) as IResourceService;
                _actorService = provider.GetService(typeof(IActorService)) as IActorService;
                
                if (_resourceService == null)
                {
                    return ServiceInitializationResult.Failed("ResourceService dependency is required");
                }
                
                // Initialize audio mixer
                await InitializeAudioMixer(combinedToken);
                
                // Initialize object pooling
                InitializeObjectPooling();
                
                // Initialize player factory
                _playerFactory = new AudioPlayerFactory(_config, _audioSourcePool, _audioSourceParent);
                
                // Initialize audio state
                InitializeAudioState();
                
                // Preload critical audio if enabled
                if (_config.PreloadCriticalAudio)
                {
                    await PreloadCriticalAudio(combinedToken);
                }
                
                // Validate configuration if enabled
                if (_config.ValidateAudioOnStartup)
                {
                    var validationResults = ValidateAudioResources();
                    if (validationResults.Any(kvp => kvp.Value.Length > 0))
                    {
                        LogValidationWarnings(validationResults);
                    }
                }
                
                _initialized = true;
                
                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed($"AudioService initialization failed: {ex.Message}");
            }
        }
        
        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Stop all audio with fade out
                await StopAllAsync(_config.DefaultFadeDuration, cancellationToken);
                
                // Clear caches and resources
                await ClearAudioCacheAsync(cancellationToken);
                
                // Cleanup object pools
                CleanupObjectPooling();
                
                // Dispose cancellation token
                _serviceCts?.Cancel();
                _serviceCts?.Dispose();
                
                _initialized = false;
                _disposed = true;
                
                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed($"AudioService shutdown failed: {ex.Message}");
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
                
                if (_masterMixer == null)
                {
                    return ServiceHealthStatus.Degraded("AudioMixer not properly initialized");
                }
                
                var activeCount = _activePlayers.Count;
                var maxSources = _config.MaxConcurrentSources;
                
                if (activeCount > maxSources * 0.9f)
                {
                    return ServiceHealthStatus.Degraded($"High audio source usage: {activeCount}/{maxSources}");
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
        
        #region Audio Playback Control
        
        public async UniTask<IAudioPlayer> PlayAsync(string audioId, AudioCategory category, AudioPlayOptions options = default, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
                throw new InvalidOperationException("AudioService not initialized");
            
            try
            {
                // Load audio clip
                var audioClip = await LoadAudioClipAsync(audioId, cancellationToken);
                if (audioClip == null)
                {
                    OnAudioError?.Invoke(audioId, "Failed to load audio clip");
                    return null;
                }
                
                // Create audio player
                var player = await CreateAudioPlayerAsync(audioId, category, audioClip, options, cancellationToken);
                
                // Register player
                _activePlayers[audioId] = player;
                _playersByCategory[category].Add(player);
                
                // Start playback
                await player.PlayAsync(options, cancellationToken);
                
                OnAudioStarted?.Invoke(audioId, category);
                
                return player;
            }
            catch (Exception ex)
            {
                OnAudioError?.Invoke(audioId, ex.Message);
                throw;
            }
        }
        
        public async UniTask<IAudioPlayer> PlayMusicAsync(string musicId, float fadeIn = 0f, bool loop = true, CancellationToken cancellationToken = default)
        {
            var options = AudioPlayOptions.Default;
            options.FadeInDuration = fadeIn;
            options.Loop = loop;
            options.Volume = _config.DefaultMusicVolume;
            
            // Handle crossfade if enabled and music is already playing
            if (_config.EnableMusicCrossfade && !string.IsNullOrEmpty(_currentState.CurrentMusicTrack))
            {
                var currentMusic = GetPlayer(_currentState.CurrentMusicTrack);
                if (currentMusic is MusicPlayer currentMusicPlayer && currentMusic.IsPlaying)
                {
                    // Load new music
                    var newMusicClip = await LoadAudioClipAsync(musicId, cancellationToken);
                    if (newMusicClip != null)
                    {
                        var newMusicPlayer = _playerFactory.CreateAudioPlayer(musicId, AudioCategory.Music, newMusicClip) as MusicPlayer;
                        if (newMusicPlayer != null)
                        {
                            // Perform crossfade
                            await MusicPlayer.CrossfadeAsync(currentMusicPlayer, newMusicPlayer, _config.DefaultCrossfadeDuration, cancellationToken);
                            
                            // Update state
                            _currentState.CurrentMusicTrack = musicId;
                            _activePlayers[musicId] = newMusicPlayer;
                            _playersByCategory[AudioCategory.Music].Add(newMusicPlayer);
                            
                            return newMusicPlayer;
                        }
                    }
                }
            }
            
            var player = await PlayAsync(musicId, AudioCategory.Music, options, cancellationToken);
            if (player != null)
            {
                _currentState.CurrentMusicTrack = musicId;
            }
            
            return player;
        }
        
        public async UniTask<IAudioPlayer> PlaySFXAsync(string sfxId, float volume = 1f, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            var options = AudioPlayOptions.Default;
            options.Volume = volume * _config.DefaultSFXVolume;
            options.Position = position;
            options.SpatialMode = _config.EnableSpatialAudio && position != Vector3.zero ? SpatialAudioMode.Simple : SpatialAudioMode.None;
            
            return await PlayAsync(sfxId, AudioCategory.SFX, options, cancellationToken);
        }
        
        public async UniTask<IAudioPlayer> PlayVoiceAsync(string characterId, string voiceId, bool duck = true, CancellationToken cancellationToken = default)
        {
            var fullVoiceId = $"{characterId}_{voiceId}";
            var options = AudioPlayOptions.Default;
            options.Volume = _currentState.GetCategoryVolume(AudioCategory.Voice);
            options.Priority = AudioPriority.Critical;
            
            // Apply audio ducking if enabled
            if (duck && _config.VoiceAudioDucking > 0)
            {
                ApplyAudioDucking(_config.VoiceAudioDucking);
            }
            
            var player = await PlayAsync(fullVoiceId, AudioCategory.Voice, options, cancellationToken);
            
            // Set up ducking removal when voice completes
            if (player != null && duck)
            {
                player.OnPlaybackComplete += _ => RemoveAudioDucking();
            }
            
            return player;
        }
        
        public async UniTask<IAudioPlayer> PlayAmbientAsync(string ambientId, bool loop = true, float volume = 0.6f, CancellationToken cancellationToken = default)
        {
            var options = AudioPlayOptions.Default;
            options.Loop = loop;
            options.Volume = volume * _config.DefaultAmbientVolume;
            options.Priority = AudioPriority.Low;
            
            var player = await PlayAsync(ambientId, AudioCategory.Ambient, options, cancellationToken);
            
            if (player != null && !_currentState.ActiveAmbientSounds.Contains(ambientId))
            {
                _currentState.ActiveAmbientSounds.Add(ambientId);
            }
            
            return player;
        }
        
        #endregion
        
        #region Audio Control Operations
        
        public async UniTask StopAsync(string audioId, float fadeOut = 0f, CancellationToken cancellationToken = default)
        {
            if (_activePlayers.TryGetValue(audioId, out var player))
            {
                await player.StopAsync(fadeOut, cancellationToken);
                RemovePlayer(audioId, player);
                OnAudioStopped?.Invoke(audioId, player.Category);
            }
        }
        
        public async UniTask StopCategoryAsync(AudioCategory category, float fadeOut = 0f, CancellationToken cancellationToken = default)
        {
            if (_playersByCategory.TryGetValue(category, out var players))
            {
                var tasks = players.ToList().Select(p => p.StopAsync(fadeOut, cancellationToken));
                await UniTask.WhenAll(tasks);
                
                foreach (var player in players.ToList())
                {
                    RemovePlayer(player.AudioId, player);
                    OnAudioStopped?.Invoke(player.AudioId, category);
                }
            }
        }
        
        public async UniTask StopAllAsync(float fadeOut = 0f, CancellationToken cancellationToken = default)
        {
            var allPlayers = _activePlayers.Values.ToList();
            var tasks = allPlayers.Select(p => p.StopAsync(fadeOut, cancellationToken));
            await UniTask.WhenAll(tasks);
            
            foreach (var player in allPlayers)
            {
                OnAudioStopped?.Invoke(player.AudioId, player.Category);
            }
            
            ClearAllPlayers();
        }
        
        public void Pause(string audioId)
        {
            if (_activePlayers.TryGetValue(audioId, out var player))
            {
                player.Pause();
            }
        }
        
        public void PauseCategory(AudioCategory category)
        {
            if (_playersByCategory.TryGetValue(category, out var players))
            {
                foreach (var player in players)
                {
                    player.Pause();
                }
            }
        }
        
        public void PauseAll()
        {
            foreach (var player in _activePlayers.Values)
            {
                player.Pause();
            }
            _currentState.GlobalPaused = true;
        }
        
        public void Resume(string audioId)
        {
            if (_activePlayers.TryGetValue(audioId, out var player))
            {
                player.Resume();
            }
        }
        
        public void ResumeCategory(AudioCategory category)
        {
            if (_playersByCategory.TryGetValue(category, out var players))
            {
                foreach (var player in players)
                {
                    player.Resume();
                }
            }
        }
        
        public void ResumeAll()
        {
            foreach (var player in _activePlayers.Values)
            {
                player.Resume();
            }
            _currentState.GlobalPaused = false;
        }
        
        #endregion
        
        #region Volume and Mixing Control
        
        public void SetMasterVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            _currentState.MasterVolume = volume;
            
            if (_masterMixer != null)
            {
                var dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
                _masterMixer.SetFloat("MasterVolume", dbValue);
            }
            
            OnVolumeChanged?.Invoke(AudioCategory.Music, volume); // Use Music as master indicator
        }
        
        public float GetMasterVolume()
        {
            return _currentState.MasterVolume;
        }
        
        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            volume = Mathf.Clamp01(volume);
            _currentState.SetCategoryVolume(category, volume);
            
            if (_masterMixer != null && _categoryBusMap.TryGetValue(category, out var busName))
            {
                var dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
                _masterMixer.SetFloat($"{busName}Volume", dbValue);
            }
            
            OnVolumeChanged?.Invoke(category, volume);
        }
        
        public float GetCategoryVolume(AudioCategory category)
        {
            return _currentState.GetCategoryVolume(category);
        }
        
        public void SetCategoryMuted(AudioCategory category, bool muted)
        {
            _currentState.SetCategoryMuted(category, muted);
            
            if (_masterMixer != null && _categoryBusMap.TryGetValue(category, out var busName))
            {
                var volume = muted ? 0f : _currentState.GetCategoryVolume(category);
                var dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
                _masterMixer.SetFloat($"{busName}Volume", dbValue);
            }
            
            OnMuteChanged?.Invoke(category, muted);
        }
        
        public bool GetCategoryMuted(AudioCategory category)
        {
            return _currentState.GetCategoryMuted(category);
        }
        
        #endregion
        
        #region Audio Mixer Integration
        
        public async UniTask ApplySnapshotAsync(string snapshotName, float transitionTime = 1f, CancellationToken cancellationToken = default)
        {
            if (_masterMixer == null)
                return;
            
            var snapshot = _masterMixer.FindSnapshot(snapshotName);
            if (snapshot != null)
            {
                snapshot.TransitionTo(transitionTime);
                _currentSnapshot = snapshot;
                _currentState.CurrentSnapshot = snapshotName;
                
                // Wait for transition to complete
                await UniTask.Delay(Mathf.RoundToInt(transitionTime * 1000), cancellationToken: cancellationToken);
                
                OnSnapshotChanged?.Invoke(snapshotName);
            }
        }
        
        public void SetMixerParameter(string parameterName, float value)
        {
            _masterMixer?.SetFloat(parameterName, value);
        }
        
        public float GetMixerParameter(string parameterName)
        {
            if (_masterMixer != null && _masterMixer.GetFloat(parameterName, out float value))
            {
                return value;
            }
            return 0f;
        }
        
        #endregion
        
        #region Audio State Management
        
        public AudioState GetAudioState()
        {
            return _currentState.Clone();
        }
        
        public async UniTask ApplyAudioStateAsync(AudioState state, CancellationToken cancellationToken = default)
        {
            if (state == null)
                return;
            
            // Apply volume settings
            SetMasterVolume(state.MasterVolume);
            
            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
            {
                SetCategoryVolume(category, state.GetCategoryVolume(category));
                SetCategoryMuted(category, state.GetCategoryMuted(category));
            }
            
            // Apply snapshot
            if (!string.IsNullOrEmpty(state.CurrentSnapshot))
            {
                await ApplySnapshotAsync(state.CurrentSnapshot, 0f, cancellationToken);
            }
            
            // Restore music if specified
            if (!string.IsNullOrEmpty(state.CurrentMusicTrack))
            {
                await PlayMusicAsync(state.CurrentMusicTrack, 0f, true, cancellationToken);
            }
            
            // Restore ambient sounds
            foreach (var ambientId in state.ActiveAmbientSounds)
            {
                await PlayAmbientAsync(ambientId, true, state.GetCategoryVolume(AudioCategory.Ambient), cancellationToken);
            }
        }
        
        public async UniTask ResetToDefaultStateAsync(CancellationToken cancellationToken = default)
        {
            await StopAllAsync(0f, cancellationToken);
            
            var defaultState = new AudioState();
            await ApplyAudioStateAsync(defaultState, cancellationToken);
        }
        
        #endregion
        
        #region Audio Information and Status
        
        public bool IsPlaying(string audioId)
        {
            return _activePlayers.TryGetValue(audioId, out var player) && player.IsPlaying;
        }
        
        public bool IsCategoryPlaying(AudioCategory category)
        {
            return _playersByCategory.TryGetValue(category, out var players) && players.Any(p => p.IsPlaying);
        }
        
        public IReadOnlyCollection<IAudioPlayer> GetActivePlayers()
        {
            return _activePlayers.Values.ToList().AsReadOnly();
        }
        
        public IReadOnlyCollection<IAudioPlayer> GetCategoryPlayers(AudioCategory category)
        {
            return _playersByCategory.TryGetValue(category, out var players) 
                ? players.AsReadOnly() 
                : new List<IAudioPlayer>().AsReadOnly();
        }
        
        public IAudioPlayer GetPlayer(string audioId)
        {
            _activePlayers.TryGetValue(audioId, out var player);
            return player;
        }
        
        /// <summary>
        /// Gets the current music player if one is playing
        /// </summary>
        public MusicPlayer GetCurrentMusicPlayer()
        {
            if (!string.IsNullOrEmpty(_currentState.CurrentMusicTrack))
            {
                return GetPlayer(_currentState.CurrentMusicTrack) as MusicPlayer;
            }
            return null;
        }
        
        /// <summary>
        /// Gets all active ambient players
        /// </summary>
        public IReadOnlyCollection<AmbientPlayer> GetActiveAmbientPlayers()
        {
            var ambientPlayers = new List<AmbientPlayer>();
            
            if (_playersByCategory.TryGetValue(AudioCategory.Ambient, out var players))
            {
                foreach (var player in players)
                {
                    if (player is AmbientPlayer ambientPlayer && player.IsPlaying)
                    {
                        ambientPlayers.Add(ambientPlayer);
                    }
                }
            }
            
            return ambientPlayers.AsReadOnly();
        }
        
        /// <summary>
        /// Gets active SFX players for a specific sound
        /// </summary>
        public IReadOnlyCollection<SFXPlayer> GetActiveSFXPlayers(string sfxId = null)
        {
            var sfxPlayers = new List<SFXPlayer>();
            
            if (_playersByCategory.TryGetValue(AudioCategory.SFX, out var players))
            {
                foreach (var player in players)
                {
                    if (player is SFXPlayer sfxPlayer && player.IsPlaying)
                    {
                        if (string.IsNullOrEmpty(sfxId) || player.AudioId == sfxId)
                        {
                            sfxPlayers.Add(sfxPlayer);
                        }
                    }
                }
            }
            
            return sfxPlayers.AsReadOnly();
        }
        
        #endregion
        
        #region Resource Management
        
        public async UniTask PreloadAudioAsync(string[] audioIds, CancellationToken cancellationToken = default)
        {
            var loadTasks = audioIds.Select(id => LoadAudioClipAsync(id, cancellationToken));
            await UniTask.WhenAll(loadTasks);
        }
        
        public async UniTask UnloadAudioAsync(string[] audioIds, CancellationToken cancellationToken = default)
        {
            foreach (var audioId in audioIds)
            {
                if (_audioCache.TryRemove(audioId, out var clip))
                {
                    if (clip != null)
                    {
                        Resources.UnloadAsset(clip);
                    }
                }
            }
            
            await UniTask.Yield();
        }
        
        public async UniTask ClearAudioCacheAsync(CancellationToken cancellationToken = default)
        {
            foreach (var kvp in _audioCache.ToList())
            {
                if (kvp.Value != null)
                {
                    Resources.UnloadAsset(kvp.Value);
                }
            }
            
            _audioCache.Clear();
            await UniTask.Yield();
        }
        
        #endregion
        
        #region Specialized Audio Methods
        
        /// <summary>
        /// Plays a music playlist
        /// </summary>
        public async UniTask<MusicPlayer> PlayMusicPlaylistAsync(MusicPlaylist playlist, CancellationToken cancellationToken = default)
        {
            if (playlist == null || playlist.TrackIds.Count == 0)
                return null;
            
            // Get first track
            var firstTrackId = playlist.TrackIds[0];
            var player = await PlayMusicAsync(firstTrackId, 0f, false, cancellationToken);
            
            if (player is MusicPlayer musicPlayer)
            {
                // Set up playlist
                musicPlayer.SetPlaylist(playlist.TrackIds, playlist.Shuffle, playlist.Repeat);
                return musicPlayer;
            }
            
            return null;
        }
        
        /// <summary>
        /// Plays UI sound effect
        /// </summary>
        public async UniTask<UIPlayer> PlayUIAsync(UIInteractionType interactionType, CancellationToken cancellationToken = default)
        {
            // Get UI sound scheme
            var soundScheme = UISoundScheme.CreateDefault();
            
            if (soundScheme.SoundMappings.TryGetValue(interactionType, out string audioId))
            {
                var player = await PlayAsync(audioId, AudioCategory.UI, AudioPlayOptions.Default, cancellationToken);
                
                if (player is UIPlayer uiPlayer)
                {
                    uiPlayer.InteractionType = interactionType;
                    return uiPlayer;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Creates an ambient soundscape
        /// </summary>
        public async UniTask<AmbientPlayer> CreateAmbientSoundscapeAsync(AmbientSoundscape soundscape, CancellationToken cancellationToken = default)
        {
            if (soundscape == null || soundscape.Layers.Count == 0)
                return null;
            
            // Create base ambient player
            var baseLayer = soundscape.Layers[0];
            var player = await PlayAmbientAsync(baseLayer.AudioClipPath, true, baseLayer.Volume, cancellationToken);
            
            if (player is AmbientPlayer ambientPlayer)
            {
                // Set environmental conditions
                ambientPlayer.CurrentWeather = soundscape.DefaultWeather;
                ambientPlayer.CurrentTimeOfDay = soundscape.DefaultTimeOfDay;
                
                // Add additional layers
                for (int i = 1; i < soundscape.Layers.Count; i++)
                {
                    var layer = soundscape.Layers[i];
                    if (layer.AutoStart)
                    {
                        var clip = await LoadAudioClipAsync(layer.AudioClipPath, cancellationToken);
                        if (clip != null)
                        {
                            await ambientPlayer.AddLayerAsync(layer.LayerId, clip, layer.Volume, layer.FadeInDuration, cancellationToken);
                        }
                    }
                }
                
                return ambientPlayer;
            }
            
            return null;
        }
        
        /// <summary>
        /// Plays SFX with surface impact effect
        /// </summary>
        public async UniTask<SFXPlayer> PlayImpactSFXAsync(string sfxId, SurfaceType surface, Vector3 position = default, CancellationToken cancellationToken = default)
        {
            var player = await PlaySFXAsync(sfxId, 1f, position, cancellationToken);
            
            if (player is SFXPlayer sfxPlayer)
            {
                sfxPlayer.ApplyImpactEffect(surface);
                return sfxPlayer;
            }
            
            return null;
        }
        
        #endregion
        
        #region Performance and Debugging
        
        public AudioServiceStatistics GetStatistics()
        {
            var stats = new AudioServiceStatistics
            {
                ActivePlayers = _activePlayers.Count,
                CachedClips = _audioCache.Count,
                LoadingClips = _loadingTasks.Count,
                MasterVolume = _currentState.MasterVolume,
                ActiveSnapshot = _currentState.CurrentSnapshot,
                LastUpdateTime = DateTime.Now,
                HealthStatus = "Healthy"
            };
            
            // Calculate players by category
            foreach (var category in _playersByCategory)
            {
                stats.PlayersByCategory[category.Key] = category.Value.Count;
            }
            
            return stats;
        }
        
        public string GetDebugInfo()
        {
            var info = $"AudioService Debug Info:\n" +
                      $"Initialized: {_initialized}\n" +
                      $"Active Players: {_activePlayers.Count}/{_config.MaxConcurrentSources}\n" +
                      $"Cached Clips: {_audioCache.Count}/{_config.AudioClipCacheSize}\n" +
                      $"Master Volume: {_currentState.MasterVolume:F2}\n" +
                      $"Current Snapshot: {_currentState.CurrentSnapshot}\n";
            
            foreach (var category in _playersByCategory)
            {
                info += $"{category.Key}: {category.Value.Count} players\n";
            }
            
            return info;
        }
        
        public Dictionary<string, string[]> ValidateAudioResources()
        {
            var results = new Dictionary<string, string[]>();
            var errors = new List<string>();
            
            // Validate configuration
            if (_config.MasterAudioMixer == null)
                errors.Add("MasterAudioMixer is not assigned");
            
            if (_config.MaxConcurrentSources <= 0)
                errors.Add("MaxConcurrentSources must be greater than 0");
            
            // Validate mixer groups
            if (_masterMixer != null)
            {
                foreach (var category in _config.SupportedCategories)
                {
                    var busName = _config.GetBusNameForCategory(category);
                    if (!_mixerGroups.ContainsKey(busName))
                    {
                        errors.Add($"AudioMixer group '{busName}' for category {category} not found");
                    }
                }
            }
            
            results["AudioService"] = errors.ToArray();
            return results;
        }
        
        #endregion
        
        #region Private Implementation Methods
        
        private void InitializeCategoryMappings()
        {
            _categoryBusMap[AudioCategory.Music] = _config.MusicBusName;
            _categoryBusMap[AudioCategory.SFX] = _config.SFXBusName;
            _categoryBusMap[AudioCategory.Voice] = _config.VoiceBusName;
            _categoryBusMap[AudioCategory.Ambient] = _config.AmbientBusName;
            _categoryBusMap[AudioCategory.UI] = _config.UIBusName;
            _categoryBusMap[AudioCategory.System] = _config.SystemBusName;
        }
        
        private void InitializePlayerCategories()
        {
            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
            {
                _playersByCategory[category] = new List<IAudioPlayer>();
            }
        }
        
        private async UniTask InitializeAudioMixer(CancellationToken cancellationToken)
        {
            _masterMixer = _config.MasterAudioMixer;
            if (_masterMixer == null)
                return;
            
            // Find mixer groups for each category
            foreach (var kvp in _categoryBusMap)
            {
                var groups = _masterMixer.FindMatchingGroups(kvp.Value);
                if (groups.Length > 0)
                {
                    _mixerGroups[kvp.Value] = groups[0];
                }
            }
            
            // Apply default snapshot
            if (!string.IsNullOrEmpty(_config.DefaultSnapshotName))
            {
                await ApplySnapshotAsync(_config.DefaultSnapshotName, 0f, cancellationToken);
            }
        }
        
        private void InitializeObjectPooling()
        {
            if (!_config.EnableAudioPooling)
                return;
            
            _audioSourceParent = new GameObject("AudioService_AudioSources");
            _audioSourceParent.transform.SetParent(null);
            GameObject.DontDestroyOnLoad(_audioSourceParent);
            
            // Pre-populate pool
            for (int i = 0; i < _config.AudioSourcePoolSize; i++)
            {
                var audioSource = CreatePooledAudioSource();
                _audioSourcePool.Enqueue(audioSource);
            }
        }
        
        private AudioSource CreatePooledAudioSource()
        {
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(_audioSourceParent.transform);
            
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.enabled = false;
            
            return audioSource;
        }
        
        private void InitializeAudioState()
        {
            // Apply default volumes for each category
            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
            {
                var defaultVolume = _config.GetDefaultVolumeForCategory(category);
                SetCategoryVolume(category, defaultVolume);
            }
        }
        
        private async UniTask PreloadCriticalAudio(CancellationToken cancellationToken)
        {
            // This would typically load from a configuration file or asset
            // For now, we'll leave it empty as specific critical audio would be game-dependent
            await UniTask.Yield();
        }
        
        private async UniTask<AudioClip> LoadAudioClipAsync(string audioId, CancellationToken cancellationToken)
        {
            // Check cache first
            if (_audioCache.TryGetValue(audioId, out var cachedClip))
            {
                return cachedClip;
            }
            
            // Check if already loading
            if (_loadingTasks.TryGetValue(audioId, out var existingTask))
            {
                return await existingTask;
            }
            
            // Start loading
            var loadTask = LoadAudioClipFromResourceService(audioId, cancellationToken);
            _loadingTasks[audioId] = loadTask;
            
            try
            {
                var clip = await loadTask;
                
                // Cache if successful and within cache limits
                if (clip != null && _audioCache.Count < _config.AudioClipCacheSize)
                {
                    _audioCache[audioId] = clip;
                }
                
                return clip;
            }
            finally
            {
                _loadingTasks.TryRemove(audioId, out _);
            }
        }
        
        private async UniTask<AudioClip> LoadAudioClipFromResourceService(string audioId, CancellationToken cancellationToken)
        {
            if (_resourceService == null)
                return null;
            
            try
            {
                // Use ResourceService to load the audio clip
                return await _resourceService.LoadResourceAsync<AudioClip>(audioId, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load audio clip '{audioId}': {ex.Message}");
                return null;
            }
        }
        
        private async UniTask<IAudioPlayer> CreateAudioPlayerAsync(string audioId, AudioCategory category, AudioClip clip, AudioPlayOptions options, CancellationToken cancellationToken)
        {
            if (_playerFactory == null)
                throw new InvalidOperationException("AudioPlayerFactory not initialized");
            
            var player = _playerFactory.CreateAudioPlayer(audioId, category, clip);
            
            // Set up event handlers
            player.OnPlaybackComplete += OnPlayerPlaybackComplete;
            player.OnPlaybackError += OnPlayerPlaybackError;
            
            return await UniTask.FromResult<IAudioPlayer>(player);
        }
        
        private void RemovePlayer(string audioId, IAudioPlayer player)
        {
            _activePlayers.TryRemove(audioId, out _);
            
            if (_playersByCategory.TryGetValue(player.Category, out var categoryPlayers))
            {
                categoryPlayers.Remove(player);
            }
            
            // Clean up from state tracking
            if (player.Category == AudioCategory.Music && _currentState.CurrentMusicTrack == audioId)
            {
                _currentState.CurrentMusicTrack = null;
            }
            else if (player.Category == AudioCategory.Ambient)
            {
                _currentState.ActiveAmbientSounds.Remove(audioId);
            }
        }
        
        private void ClearAllPlayers()
        {
            _activePlayers.Clear();
            foreach (var categoryPlayers in _playersByCategory.Values)
            {
                categoryPlayers.Clear();
            }
            
            _currentState.CurrentMusicTrack = null;
            _currentState.ActiveAmbientSounds.Clear();
        }
        
        private void ApplyAudioDucking(float duckLevel)
        {
            // Reduce volume of non-voice categories during voice playback
            var categories = new[] { AudioCategory.Music, AudioCategory.SFX, AudioCategory.Ambient };
            
            foreach (var category in categories)
            {
                if (_playersByCategory.TryGetValue(category, out var players))
                {
                    foreach (var player in players)
                    {
                        var targetVolume = player.Volume * (1f - duckLevel);
                        _ = player.FadeVolumeAsync(targetVolume, 0.2f, AudioFadeType.EaseOut, CancellationToken.None);
                    }
                }
            }
        }
        
        private void RemoveAudioDucking()
        {
            // Restore volume of ducked categories
            var categories = new[] { AudioCategory.Music, AudioCategory.SFX, AudioCategory.Ambient };
            
            foreach (var category in categories)
            {
                if (_playersByCategory.TryGetValue(category, out var players))
                {
                    var categoryVolume = GetCategoryVolume(category);
                    foreach (var player in players)
                    {
                        _ = player.FadeVolumeAsync(categoryVolume, 0.2f, AudioFadeType.EaseIn, CancellationToken.None);
                    }
                }
            }
        }
        
        private void LogValidationWarnings(Dictionary<string, string[]> validationResults)
        {
            foreach (var kvp in validationResults)
            {
                if (kvp.Value.Length > 0)
                {
                    Debug.LogWarning($"AudioService validation warnings for {kvp.Key}: {string.Join(", ", kvp.Value)}");
                }
            }
        }
        
        private void CleanupObjectPooling()
        {
            while (_audioSourcePool.Count > 0)
            {
                var audioSource = _audioSourcePool.Dequeue();
                if (audioSource != null)
                {
                    GameObject.DestroyImmediate(audioSource.gameObject);
                }
            }
            
            if (_audioSourceParent != null)
            {
                GameObject.DestroyImmediate(_audioSourceParent);
            }
        }
        
        private void OnPlayerPlaybackComplete(IAudioPlayer player)
        {
            if (player is AudioPlayer audioPlayer)
            {
                // Handle specialized player cleanup
                switch (player)
                {
                    case MusicPlayer musicPlayer:
                        // Music player handles its own playlist progression
                        if (!musicPlayer.IsPlaylistMode)
                        {
                            RemovePlayer(audioPlayer.AudioId, player);
                        }
                        break;
                        
                    case SFXPlayer sfxPlayer:
                        // SFX players auto-release to pool
                        if (sfxPlayer.AutoRelease)
                        {
                            RemovePlayer(audioPlayer.AudioId, player);
                            _playerFactory?.ReturnAudioPlayer(audioPlayer);
                        }
                        break;
                        
                    case AmbientPlayer ambientPlayer:
                        // Ambient players typically loop, so this shouldn't happen often
                        RemovePlayer(audioPlayer.AudioId, player);
                        break;
                        
                    case UIPlayer uiPlayer:
                        // UI sounds are one-shot, always clean up
                        RemovePlayer(audioPlayer.AudioId, player);
                        _playerFactory?.ReturnAudioPlayer(audioPlayer);
                        break;
                        
                    default:
                        // Default behavior for base AudioPlayer
                        RemovePlayer(audioPlayer.AudioId, player);
                        _playerFactory?.ReturnAudioPlayer(audioPlayer);
                        break;
                }
                
                // Notify listeners
                OnAudioStopped?.Invoke(audioPlayer.AudioId, audioPlayer.Category);
            }
        }
        
        private void OnPlayerPlaybackError(IAudioPlayer player, string error)
        {
            OnAudioError?.Invoke(player.AudioId, error);
            
            // Clean up errored player
            if (player is AudioPlayer audioPlayer)
            {
                RemovePlayer(audioPlayer.AudioId, player);
                _playerFactory?.ReturnAudioPlayer(audioPlayer);
            }
        }
        
        #endregion
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the Audio Service with comprehensive settings for Unity AudioMixer integration,
    /// performance optimization, and audio management
    /// </summary>
    [CreateAssetMenu(fileName = "AudioServiceConfiguration", menuName = "Engine/Services/AudioServiceConfiguration", order = 3)]
    public class AudioServiceConfiguration : ServiceConfigurationBase
    {
        #region Audio Management Settings
        
        [Header("Audio Management")]
        [SerializeField, Range(1, 64)]
        [Tooltip("Maximum number of concurrent audio sources")]
        private int _maxConcurrentSources = 32;
        
        [SerializeField]
        [Tooltip("Enable audio streaming for large files")]
        private bool _enableAudioStreaming = true;
        
        [SerializeField, Range(0f, 10f)]
        [Tooltip("Default fade duration for audio transitions")]
        private float _defaultFadeDuration = 1.0f;
        
        [SerializeField]
        [Tooltip("Supported audio categories for this service")]
        private AudioCategory[] _supportedCategories = 
        {
            AudioCategory.Music,
            AudioCategory.SFX,
            AudioCategory.Voice,
            AudioCategory.Ambient,
            AudioCategory.UI,
            AudioCategory.System
        };
        
        #endregion
        
        #region Resource Management Settings
        
        [Header("Resource Management")]
        [SerializeField, Range(10, 200)]
        [Tooltip("Maximum number of audio clips to keep in cache")]
        private int _audioClipCacheSize = 50;
        
        [SerializeField]
        [Tooltip("Preload critical audio clips at startup")]
        private bool _preloadCriticalAudio = true;
        
        [SerializeField, Range(1f, 50f)]
        [Tooltip("Memory threshold in MB for audio streaming")]
        private float _streamingMemoryThreshold = 10f;
        
        [SerializeField]
        [Tooltip("Base path for audio resources")]
        private string _audioResourceBasePath = "Audio";
        
        [SerializeField, Range(1f, 60f)]
        [Tooltip("Delay before unloading unused audio resources")]
        private float _resourceUnloadDelay = 10f;
        
        #endregion
        
        #region Unity AudioMixer Settings
        
        [Header("Unity AudioMixer Settings")]
        [SerializeField]
        [Tooltip("Master AudioMixer for all audio processing")]
        private AudioMixer _masterAudioMixer;
        
        [SerializeField]
        [Tooltip("Music bus name in the AudioMixer")]
        private string _musicBusName = "Music";
        
        [SerializeField]
        [Tooltip("SFX bus name in the AudioMixer")]
        private string _sfxBusName = "SFX";
        
        [SerializeField]
        [Tooltip("Voice bus name in the AudioMixer")]
        private string _voiceBusName = "Voice";
        
        [SerializeField]
        [Tooltip("Ambient bus name in the AudioMixer")]
        private string _ambientBusName = "Ambient";
        
        [SerializeField]
        [Tooltip("UI bus name in the AudioMixer")]
        private string _uiBusName = "UI";
        
        [SerializeField]
        [Tooltip("System bus name in the AudioMixer")]
        private string _systemBusName = "System";
        
        [SerializeField]
        [Tooltip("Enable dynamic range compression")]
        private bool _enableDynamicRange = true;
        
        [SerializeField]
        [Tooltip("Default AudioMixer snapshot name")]
        private string _defaultSnapshotName = "Default";
        
        #endregion
        
        #region Spatial Audio Settings
        
        [Header("Spatial Audio")]
        [SerializeField]
        [Tooltip("Enable 3D spatial audio processing")]
        private bool _enableSpatialAudio = true;
        
        [SerializeField, Range(1f, 1000f)]
        [Tooltip("Maximum distance for 3D audio attenuation")]
        private float _maxAudioDistance = 50f;
        
        [SerializeField]
        [Tooltip("Default rolloff mode for 3D audio")]
        private AudioRolloffMode _defaultRolloffMode = AudioRolloffMode.Logarithmic;
        
        [SerializeField]
        [Tooltip("Enable 3D audio occlusion")]
        private bool _enable3DOcclusion = true;
        
        [SerializeField]
        [Tooltip("Layer mask for audio occlusion detection")]
        private LayerMask _occlusionLayerMask = -1;
        
        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Doppler effect level for moving audio sources")]
        private float _dopplerLevel = 1f;
        
        #endregion
        
        #region Performance Settings
        
        [Header("Performance")]
        [SerializeField]
        [Tooltip("Enable audio source object pooling")]
        private bool _enableAudioPooling = true;
        
        [SerializeField, Range(5, 50)]
        [Tooltip("Initial size of the AudioSource pool")]
        private int _audioSourcePoolSize = 20;
        
        [SerializeField, Range(0.5f, 0.95f)]
        [Tooltip("Memory pressure threshold for cleanup")]
        private float _memoryPressureThreshold = 0.8f;
        
        [SerializeField]
        [Tooltip("Enable async audio loading")]
        private bool _enableAsyncAudioLoading = true;
        
        [SerializeField, Range(1, 20)]
        [Tooltip("Maximum concurrent audio loading operations")]
        private int _maxConcurrentLoads = 5;
        
        #endregion
        
        #region Voice and Character Integration
        
        [Header("Voice & Character Integration")]
        [SerializeField]
        [Tooltip("Enable automatic lip-sync for character voices")]
        private bool _enableLipSync = false;
        
        [SerializeField]
        [Tooltip("Automatically generate subtitles for voice audio")]
        private bool _autoGenerateSubtitles = false;
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Audio ducking level during voice playback")]
        private float _voiceAudioDucking = 0.3f;
        
        [SerializeField]
        [Tooltip("Base path for character voice resources")]
        private string _characterVoiceBasePath = "Audio/Voice/Characters";
        
        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Default voice audio priority multiplier")]
        private float _voicePriorityMultiplier = 2f;
        
        #endregion
        
        #region Music System Settings
        
        [Header("Music System")]
        [SerializeField]
        [Tooltip("Enable music playlist management")]
        private bool _enableMusicPlaylists = true;
        
        [SerializeField]
        [Tooltip("Enable automatic music crossfading")]
        private bool _enableMusicCrossfade = true;
        
        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("Default crossfade duration for music transitions")]
        private float _defaultCrossfadeDuration = 3.0f;
        
        [SerializeField]
        [Tooltip("Music loops by default")]
        private bool _musicLoopByDefault = true;
        
        [SerializeField]
        [Tooltip("Base path for music resources")]
        private string _musicBasePath = "Audio/Music";
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default music volume level")]
        private float _defaultMusicVolume = 0.8f;
        
        #endregion
        
        #region SFX System Settings
        
        [Header("SFX System")]
        [SerializeField]
        [Tooltip("Enable SFX object pooling")]
        private bool _enableSFXPooling = true;
        
        [SerializeField, Range(1, 20)]
        [Tooltip("Maximum simultaneous SFX sounds")]
        private int _maxSimultaneousSFX = 10;
        
        [SerializeField]
        [Tooltip("Enable SFX priority management")]
        private bool _enableSFXPriority = true;
        
        [SerializeField]
        [Tooltip("Base path for SFX resources")]
        private string _sfxBasePath = "Audio/SFX";
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default SFX volume level")]
        private float _defaultSFXVolume = 1.0f;
        
        #endregion
        
        #region Ambient System Settings
        
        [Header("Ambient System")]
        [SerializeField]
        [Tooltip("Enable ambient audio layering")]
        private bool _enableAmbientLayers = true;
        
        [SerializeField, Range(1, 10)]
        [Tooltip("Maximum number of ambient audio layers")]
        private int _maxAmbientLayers = 5;
        
        [SerializeField]
        [Tooltip("Ambient audio loops automatically")]
        private bool _ambientAutoLoop = true;
        
        [SerializeField]
        [Tooltip("Base path for ambient audio resources")]
        private string _ambientBasePath = "Audio/Ambient";
        
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Default ambient volume level")]
        private float _defaultAmbientVolume = 0.6f;
        
        #endregion
        
        #region Debug and Development Settings
        
        [Header("Debug & Development")]
        [SerializeField]
        [Tooltip("Enable audio debugging tools")]
        private bool _enableAudioDebugger = true;
        
        [SerializeField]
        [Tooltip("Log audio operations for debugging")]
        private bool _logAudioOperations = false;
        
        [SerializeField]
        [Tooltip("Show audio source gizmos in scene view")]
        private bool _showAudioGizmos = true;
        
        [SerializeField]
        [Tooltip("Enable audio performance profiling")]
        private bool _enableAudioProfiling = false;
        
        [SerializeField]
        [Tooltip("Enable audio validation on startup")]
        private bool _validateAudioOnStartup = true;
        
        #endregion
        
        #region Public Properties
        
        // Audio Management
        public int MaxConcurrentSources => _maxConcurrentSources;
        public bool EnableAudioStreaming => _enableAudioStreaming;
        public float DefaultFadeDuration => _defaultFadeDuration;
        public AudioCategory[] SupportedCategories => _supportedCategories;
        
        // Resource Management
        public int AudioClipCacheSize => _audioClipCacheSize;
        public bool PreloadCriticalAudio => _preloadCriticalAudio;
        public float StreamingMemoryThreshold => _streamingMemoryThreshold;
        public string AudioResourceBasePath => _audioResourceBasePath;
        public float ResourceUnloadDelay => _resourceUnloadDelay;
        
        // Unity AudioMixer Settings
        public AudioMixer MasterAudioMixer => _masterAudioMixer;
        public string MusicBusName => _musicBusName;
        public string SFXBusName => _sfxBusName;
        public string VoiceBusName => _voiceBusName;
        public string AmbientBusName => _ambientBusName;
        public string UIBusName => _uiBusName;
        public string SystemBusName => _systemBusName;
        public bool EnableDynamicRange => _enableDynamicRange;
        public string DefaultSnapshotName => _defaultSnapshotName;
        
        // Spatial Audio
        public bool EnableSpatialAudio => _enableSpatialAudio;
        public float MaxAudioDistance => _maxAudioDistance;
        public AudioRolloffMode DefaultRolloffMode => _defaultRolloffMode;
        public bool Enable3DOcclusion => _enable3DOcclusion;
        public LayerMask OcclusionLayerMask => _occlusionLayerMask;
        public float DopplerLevel => _dopplerLevel;
        
        // Performance
        public bool EnableAudioPooling => _enableAudioPooling;
        public int AudioSourcePoolSize => _audioSourcePoolSize;
        public float MemoryPressureThreshold => _memoryPressureThreshold;
        public bool EnableAsyncAudioLoading => _enableAsyncAudioLoading;
        public int MaxConcurrentLoads => _maxConcurrentLoads;
        
        // Voice & Character Integration
        public bool EnableLipSync => _enableLipSync;
        public bool AutoGenerateSubtitles => _autoGenerateSubtitles;
        public float VoiceAudioDucking => _voiceAudioDucking;
        public string CharacterVoiceBasePath => _characterVoiceBasePath;
        public float VoicePriorityMultiplier => _voicePriorityMultiplier;
        
        // Music System
        public bool EnableMusicPlaylists => _enableMusicPlaylists;
        public bool EnableMusicCrossfade => _enableMusicCrossfade;
        public float DefaultCrossfadeDuration => _defaultCrossfadeDuration;
        public bool MusicLoopByDefault => _musicLoopByDefault;
        public string MusicBasePath => _musicBasePath;
        public float DefaultMusicVolume => _defaultMusicVolume;
        
        // SFX System
        public bool EnableSFXPooling => _enableSFXPooling;
        public int MaxSimultaneousSFX => _maxSimultaneousSFX;
        public bool EnableSFXPriority => _enableSFXPriority;
        public string SFXBasePath => _sfxBasePath;
        public float DefaultSFXVolume => _defaultSFXVolume;
        
        // Ambient System
        public bool EnableAmbientLayers => _enableAmbientLayers;
        public int MaxAmbientLayers => _maxAmbientLayers;
        public bool AmbientAutoLoop => _ambientAutoLoop;
        public string AmbientBasePath => _ambientBasePath;
        public float DefaultAmbientVolume => _defaultAmbientVolume;
        
        // Debug & Development
        public bool EnableAudioDebugger => _enableAudioDebugger;
        public bool LogAudioOperations => _logAudioOperations;
        public bool ShowAudioGizmos => _showAudioGizmos;
        public bool EnableAudioProfiling => _enableAudioProfiling;
        public bool ValidateAudioOnStartup => _validateAudioOnStartup;
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets the bus name for specified audio category
        /// </summary>
        public string GetBusNameForCategory(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => _musicBusName,
                AudioCategory.SFX => _sfxBusName,
                AudioCategory.Voice => _voiceBusName,
                AudioCategory.Ambient => _ambientBusName,
                AudioCategory.UI => _uiBusName,
                AudioCategory.System => _systemBusName,
                _ => _sfxBusName
            };
        }
        
        /// <summary>
        /// Gets the base path for specified audio category
        /// </summary>
        public string GetBasePathForCategory(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => _musicBasePath,
                AudioCategory.SFX => _sfxBasePath,
                AudioCategory.Voice => _characterVoiceBasePath,
                AudioCategory.Ambient => _ambientBasePath,
                AudioCategory.UI => $"{_audioResourceBasePath}/UI",
                AudioCategory.System => $"{_audioResourceBasePath}/System",
                _ => _audioResourceBasePath
            };
        }
        
        /// <summary>
        /// Gets default volume for specified audio category
        /// </summary>
        public float GetDefaultVolumeForCategory(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => _defaultMusicVolume,
                AudioCategory.SFX => _defaultSFXVolume,
                AudioCategory.Voice => 1.0f,
                AudioCategory.Ambient => _defaultAmbientVolume,
                AudioCategory.UI => 0.9f,
                AudioCategory.System => 0.8f,
                _ => 1.0f
            };
        }
        
        #endregion
        
        #region Validation
        
        protected override bool OnCustomValidate(List<string> errors)
        {
            bool isValid = true;
            
            // Validate max concurrent sources
            if (_maxConcurrentSources <= 0)
            {
                errors.Add("MaxConcurrentSources must be greater than 0");
                isValid = false;
            }
            
            // Validate cache size
            if (_audioClipCacheSize <= 0)
            {
                errors.Add("AudioClipCacheSize must be greater than 0");
                isValid = false;
            }
            
            // Validate memory threshold
            if (_streamingMemoryThreshold <= 0)
            {
                errors.Add("StreamingMemoryThreshold must be greater than 0");
                isValid = false;
            }
            
            // Validate resource paths
            if (string.IsNullOrEmpty(_audioResourceBasePath))
            {
                errors.Add("AudioResourceBasePath cannot be empty");
                isValid = false;
            }
            
            // Validate AudioMixer
            if (_masterAudioMixer == null)
            {
                errors.Add("MasterAudioMixer is required for proper audio processing");
                isValid = false;
            }
            
            // Validate bus names
            if (string.IsNullOrEmpty(_musicBusName) || string.IsNullOrEmpty(_sfxBusName) ||
                string.IsNullOrEmpty(_voiceBusName) || string.IsNullOrEmpty(_ambientBusName))
            {
                errors.Add("All audio bus names must be specified");
                isValid = false;
            }
            
            // Validate spatial audio settings
            if (_enableSpatialAudio && _maxAudioDistance <= 0)
            {
                errors.Add("MaxAudioDistance must be greater than 0 when spatial audio is enabled");
                isValid = false;
            }
            
            // Validate pool settings
            if (_enableAudioPooling && _audioSourcePoolSize <= 0)
            {
                errors.Add("AudioSourcePoolSize must be greater than 0 when pooling is enabled");
                isValid = false;
            }
            
            // Validate supported categories
            if (_supportedCategories == null || _supportedCategories.Length == 0)
            {
                errors.Add("At least one audio category must be supported");
                isValid = false;
            }
            
            return isValid;
        }
        
        protected override void OnResetToDefaults()
        {
            // Audio Management
            _maxConcurrentSources = 32;
            _enableAudioStreaming = true;
            _defaultFadeDuration = 1.0f;
            _supportedCategories = new AudioCategory[]
            {
                AudioCategory.Music, AudioCategory.SFX, AudioCategory.Voice,
                AudioCategory.Ambient, AudioCategory.UI, AudioCategory.System
            };
            
            // Resource Management
            _audioClipCacheSize = 50;
            _preloadCriticalAudio = true;
            _streamingMemoryThreshold = 10f;
            _audioResourceBasePath = "Audio";
            _resourceUnloadDelay = 10f;
            
            // AudioMixer Settings
            _musicBusName = "Music";
            _sfxBusName = "SFX";
            _voiceBusName = "Voice";
            _ambientBusName = "Ambient";
            _uiBusName = "UI";
            _systemBusName = "System";
            _enableDynamicRange = true;
            _defaultSnapshotName = "Default";
            
            // Spatial Audio
            _enableSpatialAudio = true;
            _maxAudioDistance = 50f;
            _defaultRolloffMode = AudioRolloffMode.Logarithmic;
            _enable3DOcclusion = true;
            _occlusionLayerMask = -1;
            _dopplerLevel = 1f;
            
            // Performance
            _enableAudioPooling = true;
            _audioSourcePoolSize = 20;
            _memoryPressureThreshold = 0.8f;
            _enableAsyncAudioLoading = true;
            _maxConcurrentLoads = 5;
            
            // Voice Settings
            _enableLipSync = false;
            _autoGenerateSubtitles = false;
            _voiceAudioDucking = 0.3f;
            _characterVoiceBasePath = "Audio/Voice/Characters";
            _voicePriorityMultiplier = 2f;
            
            // Music Settings
            _enableMusicPlaylists = true;
            _enableMusicCrossfade = true;
            _defaultCrossfadeDuration = 3.0f;
            _musicLoopByDefault = true;
            _musicBasePath = "Audio/Music";
            _defaultMusicVolume = 0.8f;
            
            // SFX Settings
            _enableSFXPooling = true;
            _maxSimultaneousSFX = 10;
            _enableSFXPriority = true;
            _sfxBasePath = "Audio/SFX";
            _defaultSFXVolume = 1.0f;
            
            // Ambient Settings
            _enableAmbientLayers = true;
            _maxAmbientLayers = 5;
            _ambientAutoLoop = true;
            _ambientBasePath = "Audio/Ambient";
            _defaultAmbientVolume = 0.6f;
            
            // Debug Settings
            _enableAudioDebugger = true;
            _logAudioOperations = false;
            _showAudioGizmos = true;
            _enableAudioProfiling = false;
            _validateAudioOnStartup = true;
        }
        
        /// <summary>
        /// Gets a summary of the current audio configuration
        /// </summary>
        public string GetConfigurationSummary()
        {
            return $"AudioService Config: {_maxConcurrentSources} max sources, " +
                   $"{_audioClipCacheSize} cache size, " +
                   $"streaming {(_enableAudioStreaming ? "enabled" : "disabled")}, " +
                   $"spatial audio {(_enableSpatialAudio ? "enabled" : "disabled")}, " +
                   $"mixer: {(_masterAudioMixer ? _masterAudioMixer.name : "None")}";
        }
        
        #endregion
    }
}
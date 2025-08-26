using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Specialized audio player for ambient sounds with layering and environmental effects
    /// </summary>
    public class AmbientPlayer : AudioPlayer
    {
        #region Private Fields
        
        private readonly AudioServiceConfiguration _ambientConfig;
        private readonly List<AmbientLayer> _layers = new List<AmbientLayer>();
        private float _baseVolume;
        private float _environmentalMultiplier = 1f;
        private WeatherType _currentWeather = WeatherType.Clear;
        private TimeOfDay _currentTimeOfDay = TimeOfDay.Day;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Current ambient layers
        /// </summary>
        public IReadOnlyList<AmbientLayer> Layers => _layers.AsReadOnly();
        
        /// <summary>
        /// Number of active layers
        /// </summary>
        public int LayerCount => _layers.Count;
        
        /// <summary>
        /// Current weather affecting ambient audio
        /// </summary>
        public WeatherType CurrentWeather
        {
            get => _currentWeather;
            set
            {
                _currentWeather = value;
                UpdateEnvironmentalEffects();
            }
        }
        
        /// <summary>
        /// Current time of day affecting ambient audio
        /// </summary>
        public TimeOfDay CurrentTimeOfDay
        {
            get => _currentTimeOfDay;
            set
            {
                _currentTimeOfDay = value;
                UpdateEnvironmentalEffects();
            }
        }
        
        /// <summary>
        /// Environmental volume multiplier
        /// </summary>
        public float EnvironmentalMultiplier
        {
            get => _environmentalMultiplier;
            set => _environmentalMultiplier = Mathf.Clamp01(value);
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a layer is added
        /// </summary>
        public event Action<AmbientLayer> OnLayerAdded;
        
        /// <summary>
        /// Fired when a layer is removed
        /// </summary>
        public event Action<AmbientLayer> OnLayerRemoved;
        
        /// <summary>
        /// Fired when environmental conditions change
        /// </summary>
        public event Action<WeatherType, TimeOfDay> OnEnvironmentChanged;
        
        #endregion
        
        #region Constructor
        
        public AmbientPlayer(string audioId, AudioSource audioSource, AudioClip audioClip, AudioServiceConfiguration config)
            : base(audioId, AudioCategory.Ambient, audioSource, audioClip, config)
        {
            _ambientConfig = config;
            _baseVolume = config.DefaultAmbientVolume;
            
            // Ambient-specific defaults
            IsLooping = true; // Ambient sounds typically loop
            Volume = _baseVolume;
            
            // Set as base layer
            var baseLayer = new AmbientLayer
            {
                LayerId = "base",
                AudioClip = audioClip,
                Volume = 1f,
                IsActive = true
            };
            _layers.Add(baseLayer);
        }
        
        #endregion
        
        #region Layer Management
        
        /// <summary>
        /// Adds an ambient layer
        /// </summary>
        public async UniTask<bool> AddLayerAsync(string layerId, AudioClip clip, float volume = 0.5f, float fadeIn = 2f, CancellationToken cancellationToken = default)
        {
            if (_layers.Count >= _ambientConfig.MaxAmbientLayers)
            {
                Debug.LogWarning($"Cannot add layer '{layerId}': Maximum layer count ({_ambientConfig.MaxAmbientLayers}) reached");
                return false;
            }
            
            // Check if layer already exists
            if (_layers.Exists(l => l.LayerId == layerId))
            {
                Debug.LogWarning($"Layer '{layerId}' already exists");
                return false;
            }
            
            // Create new layer
            var layer = new AmbientLayer
            {
                LayerId = layerId,
                AudioClip = clip,
                Volume = 0f,
                TargetVolume = volume,
                IsActive = true
            };
            
            // Create audio source for layer
            var layerSource = CreateLayerAudioSource(layer);
            layer.AudioSource = layerSource;
            
            // Add to layers
            _layers.Add(layer);
            
            // Start playing the layer
            layerSource.clip = clip;
            layerSource.loop = true;
            layerSource.volume = 0f;
            layerSource.Play();
            
            // Fade in the layer
            if (fadeIn > 0f)
            {
                await FadeLayerAsync(layer, volume, fadeIn, cancellationToken);
            }
            else
            {
                layer.Volume = volume;
                layerSource.volume = volume * _environmentalMultiplier;
            }
            
            OnLayerAdded?.Invoke(layer);
            return true;
        }
        
        /// <summary>
        /// Removes an ambient layer
        /// </summary>
        public async UniTask<bool> RemoveLayerAsync(string layerId, float fadeOut = 2f, CancellationToken cancellationToken = default)
        {
            var layer = _layers.Find(l => l.LayerId == layerId);
            if (layer == null)
                return false;
            
            // Don't remove base layer
            if (layer.LayerId == "base")
            {
                Debug.LogWarning("Cannot remove base ambient layer");
                return false;
            }
            
            // Fade out if specified
            if (fadeOut > 0f)
            {
                await FadeLayerAsync(layer, 0f, fadeOut, cancellationToken);
            }
            
            // Stop and cleanup audio source
            if (layer.AudioSource != null)
            {
                layer.AudioSource.Stop();
                GameObject.Destroy(layer.AudioSource.gameObject);
            }
            
            _layers.Remove(layer);
            OnLayerRemoved?.Invoke(layer);
            
            return true;
        }
        
        /// <summary>
        /// Adjusts volume of a specific layer
        /// </summary>
        public async UniTask AdjustLayerVolumeAsync(string layerId, float targetVolume, float duration = 1f, CancellationToken cancellationToken = default)
        {
            var layer = _layers.Find(l => l.LayerId == layerId);
            if (layer == null)
                return;
            
            await FadeLayerAsync(layer, targetVolume, duration, cancellationToken);
        }
        
        /// <summary>
        /// Toggles a layer on/off
        /// </summary>
        public void ToggleLayer(string layerId, bool active)
        {
            var layer = _layers.Find(l => l.LayerId == layerId);
            if (layer != null)
            {
                layer.IsActive = active;
                if (layer.AudioSource != null)
                {
                    if (active)
                        layer.AudioSource.UnPause();
                    else
                        layer.AudioSource.Pause();
                }
            }
        }
        
        #endregion
        
        #region Environmental Effects
        
        /// <summary>
        /// Updates environmental effects based on weather and time
        /// </summary>
        private void UpdateEnvironmentalEffects()
        {
            // Calculate environmental multiplier based on conditions
            float weatherMultiplier = GetWeatherMultiplier(_currentWeather);
            float timeMultiplier = GetTimeOfDayMultiplier(_currentTimeOfDay);
            
            _environmentalMultiplier = weatherMultiplier * timeMultiplier;
            
            // Apply to all layers
            foreach (var layer in _layers)
            {
                if (layer.AudioSource != null && layer.IsActive)
                {
                    layer.AudioSource.volume = layer.Volume * _environmentalMultiplier * _baseVolume;
                }
            }
            
            // Apply environmental filters
            ApplyEnvironmentalFilters();
            
            OnEnvironmentChanged?.Invoke(_currentWeather, _currentTimeOfDay);
        }
        
        /// <summary>
        /// Gets volume multiplier for weather conditions
        /// </summary>
        private float GetWeatherMultiplier(WeatherType weather)
        {
            return weather switch
            {
                WeatherType.Clear => 1.0f,
                WeatherType.Cloudy => 0.9f,
                WeatherType.Rain => 0.7f,
                WeatherType.Storm => 0.5f,
                WeatherType.Snow => 0.8f,
                WeatherType.Fog => 0.6f,
                _ => 1.0f
            };
        }
        
        /// <summary>
        /// Gets volume multiplier for time of day
        /// </summary>
        private float GetTimeOfDayMultiplier(TimeOfDay timeOfDay)
        {
            return timeOfDay switch
            {
                TimeOfDay.Dawn => 0.8f,
                TimeOfDay.Day => 1.0f,
                TimeOfDay.Dusk => 0.85f,
                TimeOfDay.Night => 0.6f,
                _ => 1.0f
            };
        }
        
        /// <summary>
        /// Applies audio filters based on environmental conditions
        /// </summary>
        private void ApplyEnvironmentalFilters()
        {
            // Apply low-pass filter for underwater or heavy weather
            if (_currentWeather == WeatherType.Storm || _currentWeather == WeatherType.Rain)
            {
                ApplyLowPassFilter(3000f);
            }
            else if (_currentWeather == WeatherType.Fog)
            {
                ApplyLowPassFilter(5000f);
            }
            else
            {
                RemoveLowPassFilter();
            }
            
            // Apply reverb for certain conditions
            if (_currentWeather == WeatherType.Storm)
            {
                ApplyReverb(0.7f, 0.6f);
            }
        }
        
        #endregion
        
        #region Audio Processing
        
        /// <summary>
        /// Applies low-pass filter to all layers
        /// </summary>
        private void ApplyLowPassFilter(float cutoffFrequency)
        {
            foreach (var layer in _layers)
            {
                if (layer.AudioSource != null)
                {
                    var filter = layer.AudioSource.GetComponent<AudioLowPassFilter>();
                    if (filter == null)
                    {
                        filter = layer.AudioSource.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                    filter.cutoffFrequency = cutoffFrequency;
                }
            }
        }
        
        /// <summary>
        /// Removes low-pass filter from all layers
        /// </summary>
        private void RemoveLowPassFilter()
        {
            foreach (var layer in _layers)
            {
                if (layer.AudioSource != null)
                {
                    var filter = layer.AudioSource.GetComponent<AudioLowPassFilter>();
                    if (filter != null)
                    {
                        GameObject.Destroy(filter);
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies reverb effect to create environmental depth
        /// </summary>
        private void ApplyReverb(float roomSize, float wetLevel)
        {
            // This would integrate with Unity's AudioReverbFilter
            // Implementation depends on Unity audio filter components
            foreach (var layer in _layers)
            {
                if (layer.AudioSource != null)
                {
                    var reverb = layer.AudioSource.GetComponent<AudioReverbFilter>();
                    if (reverb == null)
                    {
                        reverb = layer.AudioSource.gameObject.AddComponent<AudioReverbFilter>();
                    }
                    reverb.room = (int)(roomSize * -10000f);
                    reverb.dryLevel = (1f - wetLevel) * 0f;
                    reverb.reverbLevel = wetLevel * 0f;
                }
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        /// <summary>
        /// Creates an audio source for a layer
        /// </summary>
        private AudioSource CreateLayerAudioSource(AmbientLayer layer)
        {
            var go = new GameObject($"AmbientLayer_{layer.LayerId}");
            go.transform.SetParent(_audioSource.transform);
            
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = _audioSource.spatialBlend;
            source.priority = _audioSource.priority;
            
            return source;
        }
        
        /// <summary>
        /// Fades a layer's volume
        /// </summary>
        private async UniTask FadeLayerAsync(AmbientLayer layer, float targetVolume, float duration, CancellationToken cancellationToken)
        {
            if (layer.AudioSource == null)
                return;
            
            layer.TargetVolume = targetVolume;
            var startVolume = layer.Volume;
            
            var tween = DOTween.To(
                () => startVolume,
                x => {
                    layer.Volume = x;
                    layer.AudioSource.volume = x * _environmentalMultiplier * _baseVolume;
                },
                targetVolume,
                duration
            ).SetEase(Ease.InOutQuad);
            
            await tween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        #endregion
        
        #region Disposal
        
        public override void Dispose()
        {
            // Clean up all layers
            foreach (var layer in _layers)
            {
                if (layer.AudioSource != null && layer.LayerId != "base")
                {
                    layer.AudioSource.Stop();
                    GameObject.Destroy(layer.AudioSource.gameObject);
                }
            }
            
            _layers.Clear();
            base.Dispose();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Represents an ambient audio layer
    /// </summary>
    public class AmbientLayer
    {
        public string LayerId { get; set; }
        public AudioClip AudioClip { get; set; }
        public AudioSource AudioSource { get; set; }
        public float Volume { get; set; }
        public float TargetVolume { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Weather types affecting ambient audio
    /// </summary>
    public enum WeatherType
    {
        Clear,
        Cloudy,
        Rain,
        Storm,
        Snow,
        Fog,
        Windy
    }
    
    /// <summary>
    /// Time of day affecting ambient audio
    /// </summary>
    public enum TimeOfDay
    {
        Dawn,
        Day,
        Dusk,
        Night
    }
    
    /// <summary>
    /// Preset ambient soundscapes
    /// </summary>
    public class AmbientSoundscape
    {
        public string Name { get; set; }
        public List<AmbientLayerConfig> Layers { get; set; } = new List<AmbientLayerConfig>();
        public WeatherType DefaultWeather { get; set; } = WeatherType.Clear;
        public TimeOfDay DefaultTimeOfDay { get; set; } = TimeOfDay.Day;
        
        public class AmbientLayerConfig
        {
            public string LayerId { get; set; }
            public string AudioClipPath { get; set; }
            public float Volume { get; set; } = 0.5f;
            public float FadeInDuration { get; set; } = 2f;
            public bool AutoStart { get; set; } = true;
        }
    }
}
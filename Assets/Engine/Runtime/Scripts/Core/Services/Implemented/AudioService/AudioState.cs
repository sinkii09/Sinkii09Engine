using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    #region Audio Play Options and State

    /// <summary>
    /// Represents the current state of audio playback
    /// </summary>
    [Serializable]
    public class AudioState
    {
        #region Volume Settings
        
        /// <summary>
        /// Master volume level (0.0 to 1.0)
        /// </summary>
        public float MasterVolume = 1.0f;
        
        /// <summary>
        /// Music category volume level (0.0 to 1.0)
        /// </summary>
        public float MusicVolume = 0.8f;
        
        /// <summary>
        /// SFX category volume level (0.0 to 1.0)
        /// </summary>
        public float SFXVolume = 1.0f;
        
        /// <summary>
        /// Voice category volume level (0.0 to 1.0)
        /// </summary>
        public float VoiceVolume = 1.0f;
        
        /// <summary>
        /// Ambient category volume level (0.0 to 1.0)
        /// </summary>
        public float AmbientVolume = 0.6f;
        
        /// <summary>
        /// UI category volume level (0.0 to 1.0)
        /// </summary>
        public float UIVolume = 0.9f;
        
        /// <summary>
        /// System category volume level (0.0 to 1.0)
        /// </summary>
        public float SystemVolume = 0.8f;
        
        #endregion
        
        #region Mute Settings
        
        /// <summary>
        /// Whether music audio is muted
        /// </summary>
        public bool MusicMuted = false;
        
        /// <summary>
        /// Whether SFX audio is muted
        /// </summary>
        public bool SFXMuted = false;
        
        /// <summary>
        /// Whether voice audio is muted
        /// </summary>
        public bool VoiceMuted = false;
        
        /// <summary>
        /// Whether ambient audio is muted
        /// </summary>
        public bool AmbientMuted = false;
        
        /// <summary>
        /// Whether UI audio is muted
        /// </summary>
        public bool UIMuted = false;
        
        /// <summary>
        /// Whether system audio is muted
        /// </summary>
        public bool SystemMuted = false;
        
        #endregion
        
        #region Current Playback State
        
        /// <summary>
        /// Currently playing music track ID
        /// </summary>
        public string CurrentMusicTrack;
        
        /// <summary>
        /// List of currently active ambient sound IDs
        /// </summary>
        public List<string> ActiveAmbientSounds = new List<string>();
        
        /// <summary>
        /// Current audio mixer snapshot name
        /// </summary>
        public string CurrentSnapshot = "Default";
        
        /// <summary>
        /// Global audio paused state
        /// </summary>
        public bool GlobalPaused = false;
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets volume level for specified category
        /// </summary>
        public float GetCategoryVolume(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => MusicVolume,
                AudioCategory.SFX => SFXVolume,
                AudioCategory.Voice => VoiceVolume,
                AudioCategory.Ambient => AmbientVolume,
                AudioCategory.UI => UIVolume,
                AudioCategory.System => SystemVolume,
                _ => 1.0f
            };
        }
        
        /// <summary>
        /// Sets volume level for specified category
        /// </summary>
        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            volume = Mathf.Clamp01(volume);
            switch (category)
            {
                case AudioCategory.Music: MusicVolume = volume; break;
                case AudioCategory.SFX: SFXVolume = volume; break;
                case AudioCategory.Voice: VoiceVolume = volume; break;
                case AudioCategory.Ambient: AmbientVolume = volume; break;
                case AudioCategory.UI: UIVolume = volume; break;
                case AudioCategory.System: SystemVolume = volume; break;
            }
        }
        
        /// <summary>
        /// Gets mute state for specified category
        /// </summary>
        public bool GetCategoryMuted(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => MusicMuted,
                AudioCategory.SFX => SFXMuted,
                AudioCategory.Voice => VoiceMuted,
                AudioCategory.Ambient => AmbientMuted,
                AudioCategory.UI => UIMuted,
                AudioCategory.System => SystemMuted,
                _ => false
            };
        }
        
        /// <summary>
        /// Sets mute state for specified category
        /// </summary>
        public void SetCategoryMuted(AudioCategory category, bool muted)
        {
            switch (category)
            {
                case AudioCategory.Music: MusicMuted = muted; break;
                case AudioCategory.SFX: SFXMuted = muted; break;
                case AudioCategory.Voice: VoiceMuted = muted; break;
                case AudioCategory.Ambient: AmbientMuted = muted; break;
                case AudioCategory.UI: UIMuted = muted; break;
                case AudioCategory.System: SystemMuted = muted; break;
            }
        }
        
        /// <summary>
        /// Creates a copy of this audio state
        /// </summary>
        public AudioState Clone()
        {
            var clone = new AudioState
            {
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                SFXVolume = SFXVolume,
                VoiceVolume = VoiceVolume,
                AmbientVolume = AmbientVolume,
                UIVolume = UIVolume,
                SystemVolume = SystemVolume,
                MusicMuted = MusicMuted,
                SFXMuted = SFXMuted,
                VoiceMuted = VoiceMuted,
                AmbientMuted = AmbientMuted,
                UIMuted = UIMuted,
                SystemMuted = SystemMuted,
                CurrentMusicTrack = CurrentMusicTrack,
                CurrentSnapshot = CurrentSnapshot,
                GlobalPaused = GlobalPaused
            };
            
            clone.ActiveAmbientSounds.AddRange(ActiveAmbientSounds);
            return clone;
        }
        
        #endregion
    }
    
    #endregion
}
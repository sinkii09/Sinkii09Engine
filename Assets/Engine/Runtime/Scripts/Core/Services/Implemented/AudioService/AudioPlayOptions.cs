using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    #region Audio Play Options and State

    /// <summary>
    /// Configuration options for audio playback
    /// </summary>
    [Serializable]
    public struct AudioPlayOptions
    {
        /// <summary>
        /// Volume level (0.0 to 1.0)
        /// </summary>
        public float Volume;
        
        /// <summary>
        /// Fade in duration in seconds
        /// </summary>
        public float FadeInDuration;
        
        /// <summary>
        /// Fade out duration in seconds
        /// </summary>
        public float FadeOutDuration;
        
        /// <summary>
        /// Whether audio should loop
        /// </summary>
        public bool Loop;
        
        /// <summary>
        /// 3D position for spatial audio
        /// </summary>
        public Vector3 Position;
        
        /// <summary>
        /// Spatial audio processing mode
        /// </summary>
        public SpatialAudioMode SpatialMode;
        
        /// <summary>
        /// Audio priority level
        /// </summary>
        public AudioPriority Priority;
        
        /// <summary>
        /// Target audio mixer bus name
        /// </summary>
        public string Bus;
        
        /// <summary>
        /// Custom data for specialized audio handling
        /// </summary>
        public Dictionary<string, object> CustomData;
        
        /// <summary>
        /// Creates default audio play options
        /// </summary>
        public static AudioPlayOptions Default => new AudioPlayOptions
        {
            Volume = 1.0f,
            FadeInDuration = 0.0f,
            FadeOutDuration = 0.0f,
            Loop = false,
            Position = Vector3.zero,
            SpatialMode = SpatialAudioMode.None,
            Priority = AudioPriority.Normal,
            Bus = string.Empty,
            CustomData = new Dictionary<string, object>()
        };
    }
    
    #endregion
}
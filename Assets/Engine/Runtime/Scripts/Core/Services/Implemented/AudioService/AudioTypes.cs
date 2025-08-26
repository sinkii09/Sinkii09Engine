using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines categories for audio classification and management
    /// </summary>
    public enum AudioCategory
    {
        /// <summary>
        /// Background music and musical scores
        /// </summary>
        Music = 0,
        
        /// <summary>
        /// Sound effects and environmental audio
        /// </summary>
        SFX = 1,
        
        /// <summary>
        /// Character dialogue and voice acting
        /// </summary>
        Voice = 2,
        
        /// <summary>
        /// Ambient environmental sounds and atmospherics
        /// </summary>
        Ambient = 3,
        
        /// <summary>
        /// User interface sounds and feedback
        /// </summary>
        UI = 4,
        
        /// <summary>
        /// System sounds and notifications
        /// </summary>
        System = 5
    }
    
    /// <summary>
    /// Defines priority levels for audio playback and resource management
    /// </summary>
    public enum AudioPriority
    {
        /// <summary>
        /// Low priority audio that can be interrupted or skipped
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Normal priority audio for standard gameplay elements
        /// </summary>
        Normal = 1,
        
        /// <summary>
        /// High priority audio for important game events
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Critical priority audio that must play (dialogue, cutscenes)
        /// </summary>
        Critical = 3
    }
    
    /// <summary>
    /// Defines fade transition types for audio effects
    /// </summary>
    public enum AudioFadeType
    {
        /// <summary>
        /// Linear fade transition
        /// </summary>
        Linear = 0,
        
        /// <summary>
        /// Ease in transition (slow start, fast end)
        /// </summary>
        EaseIn = 1,
        
        /// <summary>
        /// Ease out transition (fast start, slow end)
        /// </summary>
        EaseOut = 2,
        
        /// <summary>
        /// Ease in-out transition (slow start and end)
        /// </summary>
        EaseInOut = 3,
        
        /// <summary>
        /// Custom curve-based transition
        /// </summary>
        Custom = 4
    }
    
    /// <summary>
    /// Defines spatial audio modes for 3D positioning
    /// </summary>
    public enum SpatialAudioMode
    {
        /// <summary>
        /// No spatial audio processing (2D audio)
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Simple distance-based attenuation
        /// </summary>
        Simple = 1,
        
        /// <summary>
        /// Full 3D spatial audio with positioning and orientation
        /// </summary>
        Full3D = 2,
        
        /// <summary>
        /// Environmental spatial audio with occlusion and reverb
        /// </summary>
        Environmental = 3
    }
}
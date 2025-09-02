using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Represents the type of input device currently being used
    /// </summary>
    public enum InputDeviceType
    {
        /// <summary>
        /// Keyboard input device
        /// </summary>
        Keyboard = 0,
        
        /// <summary>
        /// Mouse input device
        /// </summary>
        Mouse = 1,
        
        /// <summary>
        /// Gamepad/Controller input device
        /// </summary>
        Gamepad = 2,
        
        /// <summary>
        /// Touch screen input device
        /// </summary>
        Touch = 3,
        
        /// <summary>
        /// Unknown or unrecognized input device
        /// </summary>
        Unknown = 4
    }
}
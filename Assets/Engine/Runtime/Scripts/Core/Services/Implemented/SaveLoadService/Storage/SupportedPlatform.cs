using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Supported platforms for storage providers
    /// </summary>
    [Flags]
    public enum SupportedPlatform
    {
        None = 0,
        Editor = 1 << 0,
        Windows = 1 << 1,
        Mac = 1 << 2,
        Linux = 1 << 3,
        iOS = 1 << 4,
        Android = 1 << 5,
        WebGL = 1 << 6,
        PlayStation = 1 << 7,
        Xbox = 1 << 8,
        Switch = 1 << 9,
        
        // Common combinations
        All = Editor | Windows | Mac | Linux | iOS | Android | WebGL | PlayStation | Xbox | Switch,
        Desktop = Windows | Mac | Linux,
        Mobile = iOS | Android,
        Console = PlayStation | Xbox | Switch,
        StandaloneAndEditor = Editor | Desktop,
        AllExceptWebGL = All & ~WebGL,
        AllExceptConsole = Editor | Desktop | Mobile | WebGL
    }
}
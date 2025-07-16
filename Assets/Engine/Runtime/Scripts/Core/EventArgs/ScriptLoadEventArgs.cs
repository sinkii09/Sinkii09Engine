using System;

namespace Sinkii09.Engine.Events
{
    /// <summary>
    /// Event data for script loading operations
    /// </summary>
    public class ScriptLoadEventArgs : EventArgs
    {
        public string ScriptName { get; }
        public bool Success { get; }
        public TimeSpan LoadTime { get; }
        public bool FromCache { get; }
        public string ErrorMessage { get; }

        public ScriptLoadEventArgs(string scriptName, bool success, TimeSpan loadTime, bool fromCache = false, string errorMessage = null)
        {
            ScriptName = scriptName;
            Success = success;
            LoadTime = loadTime;
            FromCache = fromCache;
            ErrorMessage = errorMessage;
        }
    }
}
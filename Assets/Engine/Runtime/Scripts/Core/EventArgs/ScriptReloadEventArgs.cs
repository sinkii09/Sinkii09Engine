using System;

namespace Sinkii09.Engine.Events
{
    /// <summary>
    /// Event data for script hot-reload operations
    /// </summary>
    public class ScriptReloadEventArgs : EventArgs
    {
        public string ScriptName { get; }
        public bool Success { get; }
        public TimeSpan ReloadTime { get; }
        public string ErrorMessage { get; }

        public ScriptReloadEventArgs(string scriptName, bool success, TimeSpan reloadTime, string errorMessage = null)
        {
            ScriptName = scriptName;
            Success = success;
            ReloadTime = reloadTime;
            ErrorMessage = errorMessage;
        }
    }
}
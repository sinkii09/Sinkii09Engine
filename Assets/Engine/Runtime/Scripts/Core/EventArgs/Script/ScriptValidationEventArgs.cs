using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Events
{
    /// <summary>
    /// Event data for script validation operations
    /// </summary>
    public class ScriptValidationEventArgs : EventArgs
    {
        public string ScriptName { get; }
        public bool IsValid { get; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public TimeSpan ValidationTime { get; }

        public ScriptValidationEventArgs(string scriptName, bool isValid, List<string> errors = null, List<string> warnings = null, TimeSpan validationTime = default)
        {
            ScriptName = scriptName;
            IsValid = isValid;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
            ValidationTime = validationTime;
        }
    }
}
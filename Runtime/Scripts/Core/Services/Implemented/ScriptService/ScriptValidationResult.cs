using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Result type for script validation operations
    /// </summary>
    public class ScriptValidationResult
    {
        public bool IsValid { get; }
        public string ScriptName { get; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public TimeSpan ValidationTime { get; }
        
        public ScriptValidationResult(bool isValid, string scriptName, List<string> errors = null, List<string> warnings = null, TimeSpan validationTime = default)
        {
            IsValid = isValid;
            ScriptName = scriptName;
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
            ValidationTime = validationTime;
        }
        
        public static ScriptValidationResult CreateValid(string scriptName, TimeSpan validationTime = default, List<string> warnings = null)
        {
            return new ScriptValidationResult(true, scriptName, null, warnings, validationTime);
        }
        
        public static ScriptValidationResult CreateInvalid(string scriptName, List<string> errors, TimeSpan validationTime = default, List<string> warnings = null)
        {
            return new ScriptValidationResult(false, scriptName, errors, warnings, validationTime);
        }
    }
}
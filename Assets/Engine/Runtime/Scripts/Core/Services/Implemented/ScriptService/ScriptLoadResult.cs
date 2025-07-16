using Sinkii09.Engine.Common.Script;
using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Result type for script loading operations with detailed information
    /// </summary>
    public class ScriptLoadResult
    {
        public bool Success { get; }
        public Script Script { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public TimeSpan LoadTime { get; }
        public bool FromCache { get; }
        
        private ScriptLoadResult(bool success, Script script, string errorMessage, Exception exception, TimeSpan loadTime, bool fromCache)
        {
            Success = success;
            Script = script;
            ErrorMessage = errorMessage;
            Exception = exception;
            LoadTime = loadTime;
            FromCache = fromCache;
        }
        
        public static ScriptLoadResult CreateSuccess(Script script, TimeSpan loadTime, bool fromCache = false)
        {
            return new ScriptLoadResult(true, script, null, null, loadTime, fromCache);
        }
        
        public static ScriptLoadResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan loadTime = default)
        {
            return new ScriptLoadResult(false, null, errorMessage, exception, loadTime, false);
        }
    }
}
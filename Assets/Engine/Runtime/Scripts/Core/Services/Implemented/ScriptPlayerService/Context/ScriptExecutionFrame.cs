using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Represents a single frame in the script execution call stack
    /// </summary>
    [Serializable]
    public class ScriptExecutionFrame
    {
        /// <summary>
        /// Name of the script in this frame
        /// </summary>
        public string ScriptName { get; set; }

        /// <summary>
        /// Line index to return to after this frame completes
        /// </summary>
        public int ReturnLineIndex { get; set; }

        /// <summary>
        /// Local variables for this frame
        /// </summary>
        public Dictionary<string, object> LocalVariables { get; set; }

        /// <summary>
        /// Type of call that created this frame
        /// </summary>
        public FrameType Type { get; set; }

        /// <summary>
        /// Optional label or identifier for this frame
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Timestamp when this frame was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Parent script that called this frame
        /// </summary>
        public string ParentScriptName { get; set; }

        /// <summary>
        /// Line in parent script that created this call
        /// </summary>
        public int ParentLineIndex { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ScriptExecutionFrame()
        {
            LocalVariables = new Dictionary<string, object>();
            CreatedAt = DateTime.UtcNow;
            Type = FrameType.ScriptCall;
        }

        /// <summary>
        /// Create a frame for a script call
        /// </summary>
        public static ScriptExecutionFrame CreateScriptCall(string scriptName, string parentScriptName, int parentLineIndex, int returnLineIndex)
        {
            return new ScriptExecutionFrame
            {
                ScriptName = scriptName,
                ParentScriptName = parentScriptName,
                ParentLineIndex = parentLineIndex,
                ReturnLineIndex = returnLineIndex,
                Type = FrameType.ScriptCall
            };
        }

        /// <summary>
        /// Create a frame for a subroutine call
        /// </summary>
        public static ScriptExecutionFrame CreateSubroutineCall(string label, int returnLineIndex)
        {
            return new ScriptExecutionFrame
            {
                Label = label,
                ReturnLineIndex = returnLineIndex,
                Type = FrameType.SubroutineCall
            };
        }

        /// <summary>
        /// Create a frame for a loop
        /// </summary>
        public static ScriptExecutionFrame CreateLoop(string label, int startLineIndex, int endLineIndex)
        {
            return new ScriptExecutionFrame
            {
                Label = label,
                ReturnLineIndex = startLineIndex,
                ParentLineIndex = endLineIndex,
                Type = FrameType.Loop
            };
        }

        /// <summary>
        /// Get a description of this frame
        /// </summary>
        public string GetDescription()
        {
            switch (Type)
            {
                case FrameType.ScriptCall:
                    return $"Script: {ScriptName} (called from {ParentScriptName}:{ParentLineIndex})";
                case FrameType.SubroutineCall:
                    return $"Subroutine: {Label} (return to line {ReturnLineIndex})";
                case FrameType.Loop:
                    return $"Loop: {Label} (lines {ReturnLineIndex}-{ParentLineIndex})";
                case FrameType.Conditional:
                    return $"Conditional: {Label}";
                default:
                    return $"Frame: {Type}";
            }
        }
    }
}
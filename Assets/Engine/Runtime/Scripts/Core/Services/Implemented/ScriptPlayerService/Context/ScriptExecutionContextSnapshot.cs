using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Snapshot of execution context for serialization that inherits from SaveData
    /// </summary>
    [Serializable]
    public class ScriptExecutionContextSnapshot : SaveData
    {
        [SerializeField] private string scriptName;
        [SerializeField] private int currentLineIndex;
        [SerializeField] private SerializedStringObjectDictionary variables;
        [SerializeField] private SerializedIntList breakpoints;
        [SerializeField] private SerializedStringIntDictionary labels;
        [SerializeField] private float playbackSpeed;
        [SerializeField] private float executionTime;
        [SerializeField] private int linesExecuted;
        [SerializeField] private int commandsExecuted;
        [SerializeField] private bool isStepMode;
        [SerializeField] private ScriptExecutionFrame[] callStackFrames;

        // Properties for external access
        public string ScriptName 
        { 
            get => scriptName; 
            set => scriptName = value; 
        }
        
        public int CurrentLineIndex 
        { 
            get => currentLineIndex; 
            set => currentLineIndex = value; 
        }
        
        public Dictionary<string, object> Variables 
        { 
            get => variables?.ToDictionary() ?? new Dictionary<string, object>(); 
            set => variables?.FromDictionary(value ?? new Dictionary<string, object>()); 
        }
        
        public HashSet<int> Breakpoints 
        { 
            get => breakpoints?.ToHashSet() ?? new HashSet<int>(); 
            set => breakpoints?.FromHashSet(value ?? new HashSet<int>()); 
        }
        
        public Dictionary<string, int> Labels 
        { 
            get => labels?.ToDictionary() ?? new Dictionary<string, int>(); 
            set => labels?.FromDictionary(value ?? new Dictionary<string, int>()); 
        }
        
        public float PlaybackSpeed 
        { 
            get => playbackSpeed; 
            set => playbackSpeed = value; 
        }
        
        public float ExecutionTime 
        { 
            get => executionTime; 
            set => executionTime = value; 
        }
        
        public int LinesExecuted 
        { 
            get => linesExecuted; 
            set => linesExecuted = value; 
        }
        
        public int CommandsExecuted 
        { 
            get => commandsExecuted; 
            set => commandsExecuted = value; 
        }
        
        public bool IsStepMode 
        { 
            get => isStepMode; 
            set => isStepMode = value; 
        }
        
        public ScriptExecutionFrame[] CallStackFrames 
        { 
            get => callStackFrames ?? new ScriptExecutionFrame[0]; 
            set => callStackFrames = value ?? new ScriptExecutionFrame[0]; 
        }

        public ScriptExecutionContextSnapshot()
        {
            variables = new SerializedStringObjectDictionary();
            breakpoints = new SerializedIntList();
            labels = new SerializedStringIntDictionary();
            callStackFrames = new ScriptExecutionFrame[0];
        }

        protected override int GetCurrentVersion()
        {
            return 1; // Version 1.0 of ScriptPlayer execution context
        }

        protected override bool ValidateData()
        {
            // Validate the execution context data
            if (CurrentLineIndex < 0)
                return false;
            
            if (ExecutionTime < 0)
                return false;
            
            if (LinesExecuted < 0 || CommandsExecuted < 0)
                return false;
            
            if (PlaybackSpeed <= 0)
                return false;

            // Variables, Breakpoints, and Labels can be null/empty - that's valid
            
            return true;
        }

        public override void OnBeforeSave()
        {
            base.OnBeforeSave();
            // Any additional preprocessing before saving
        }

        public override void OnAfterLoad()
        {
            base.OnAfterLoad();
            
            // Ensure serialized collections are not null
            variables ??= new SerializedStringObjectDictionary();
            breakpoints ??= new SerializedIntList();
            labels ??= new SerializedStringIntDictionary();
            callStackFrames ??= new ScriptExecutionFrame[0];
        }
    }
}
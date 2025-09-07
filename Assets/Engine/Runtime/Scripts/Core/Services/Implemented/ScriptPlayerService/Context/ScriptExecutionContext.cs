using Sinkii09.Engine.Common.Script;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages the execution context for script playback including state, variables, and call stack
    /// </summary>
    [Serializable]
    public class ScriptExecutionContext : IDisposable
    {
        #region Properties
        /// <summary>
        /// Currently executing script
        /// </summary>
        public Script Script { get; set; }

        /// <summary>
        /// Current line index being executed
        /// </summary>
        public int CurrentLineIndex { get; set; }

        /// <summary>
        /// Variables stored in the execution context
        /// </summary>
        public Dictionary<string, object> Variables { get; private set; }

        /// <summary>
        /// Call stack for nested script execution
        /// </summary>
        public Stack<ScriptExecutionFrame> CallStack { get; private set; }

        /// <summary>
        /// List of breakpoint line indices
        /// </summary>
        public HashSet<int> Breakpoints { get; private set; }

        /// <summary>
        /// Cancellation token source for the current execution
        /// </summary>
        [field:NonSerialized]
        public CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Current playback state
        /// </summary>
        public PlaybackState State { get; set; }

        /// <summary>
        /// Playback speed multiplier
        /// </summary>
        public float PlaybackSpeed { get; set; }

        /// <summary>
        /// Labels mapped to line indices for quick navigation
        /// </summary>
        public Dictionary<string, int> Labels { get; private set; }

        /// <summary>
        /// Execution start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Total execution time in seconds
        /// </summary>
        public float ExecutionTime { get; set; }

        /// <summary>
        /// Number of lines executed
        /// </summary>
        public int LinesExecuted { get; set; }

        /// <summary>
        /// Number of commands executed
        /// </summary>
        public int CommandsExecuted { get; set; }

        /// <summary>
        /// Whether execution is in step mode
        /// </summary>
        public bool IsStepMode { get; set; }

        /// <summary>
        /// Maximum call stack depth allowed
        /// </summary>
        public int MaxCallStackDepth { get; set; }

        /// <summary>
        /// Performance metrics for current execution session
        /// </summary>
        public ScriptPerformanceMetrics PerformanceMetrics { get; private set; }

        /// <summary>
        /// Disposed flag for proper IDisposable pattern
        /// </summary>
        [field:NonSerialized]
        private bool _disposed;
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize a new execution context
        /// </summary>
        public ScriptExecutionContext()
        {
            Variables = new Dictionary<string, object>();
            CallStack = new Stack<ScriptExecutionFrame>();
            Breakpoints = new HashSet<int>();
            Labels = new Dictionary<string, int>();
            State = PlaybackState.Idle;
            PlaybackSpeed = 1.0f;
            MaxCallStackDepth = 10;
            CurrentLineIndex = 0;
            PerformanceMetrics = new ScriptPerformanceMetrics();
        }
        #endregion

        #region Variable Management
        /// <summary>
        /// Set a variable value
        /// </summary>
        public void SetVariable(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Variable name cannot be null or empty", nameof(name));

            Variables[name] = value;
        }

        /// <summary>
        /// Get a variable value
        /// </summary>
        public object GetVariable(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Variables.TryGetValue(name, out var value);
            return value;
        }

        /// <summary>
        /// Get a variable value with type
        /// </summary>
        public T GetVariable<T>(string name, T defaultValue = default)
        {
            var value = GetVariable(name);
            if (value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        /// <summary>
        /// Check if a variable exists
        /// </summary>
        public bool HasVariable(string name)
        {
            return !string.IsNullOrEmpty(name) && Variables.ContainsKey(name);
        }

        /// <summary>
        /// Remove a variable
        /// </summary>
        public bool RemoveVariable(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return Variables.Remove(name);
        }

        /// <summary>
        /// Clear all variables
        /// </summary>
        public void ClearVariables()
        {
            Variables.Clear();
        }
        #endregion

        #region Call Stack Management
        /// <summary>
        /// Push a new frame onto the call stack
        /// </summary>
        public void PushFrame(ScriptExecutionFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            if (CallStack.Count >= MaxCallStackDepth)
                throw new InvalidOperationException($"Call stack depth exceeded maximum of {MaxCallStackDepth}");

            CallStack.Push(frame);
        }

        /// <summary>
        /// Pop a frame from the call stack
        /// </summary>
        public ScriptExecutionFrame PopFrame()
        {
            if (CallStack.Count == 0)
                return null;

            return CallStack.Pop();
        }

        /// <summary>
        /// Peek at the top frame without removing it
        /// </summary>
        public ScriptExecutionFrame PeekFrame()
        {
            if (CallStack.Count == 0)
                return null;

            return CallStack.Peek();
        }

        /// <summary>
        /// Clear the call stack
        /// </summary>
        public void ClearCallStack()
        {
            CallStack.Clear();
        }

        /// <summary>
        /// Get the current call stack depth
        /// </summary>
        public int GetCallStackDepth()
        {
            return CallStack.Count;
        }
        #endregion

        #region Breakpoint Management
        /// <summary>
        /// Add a breakpoint at a line
        /// </summary>
        public void AddBreakpoint(int lineIndex)
        {
            if (lineIndex >= 0)
                Breakpoints.Add(lineIndex);
        }

        /// <summary>
        /// Remove a breakpoint
        /// </summary>
        public bool RemoveBreakpoint(int lineIndex)
        {
            return Breakpoints.Remove(lineIndex);
        }

        /// <summary>
        /// Check if a line has a breakpoint
        /// </summary>
        public bool HasBreakpoint(int lineIndex)
        {
            return Breakpoints.Contains(lineIndex);
        }

        /// <summary>
        /// Clear all breakpoints
        /// </summary>
        public void ClearBreakpoints()
        {
            Breakpoints.Clear();
        }
        #endregion

        #region Label Management
        /// <summary>
        /// Register a label at a specific line
        /// </summary>
        public void RegisterLabel(string label, int lineIndex)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label cannot be null or empty", nameof(label));

            if (lineIndex < 0)
                throw new ArgumentException("Line index must be non-negative", nameof(lineIndex));

            Labels[label] = lineIndex;
        }

        /// <summary>
        /// Get the line index for a label
        /// </summary>
        public int GetLabelLineIndex(string label)
        {
            if (string.IsNullOrEmpty(label))
                return -1;

            Labels.TryGetValue(label, out var lineIndex);
            return lineIndex;
        }

        /// <summary>
        /// Find the line index for a label (nullable return for error recovery)
        /// </summary>
        public int? FindLabelLineIndex(string label)
        {
            if (string.IsNullOrEmpty(label))
                return null;

            if (Labels.TryGetValue(label, out var lineIndex))
                return lineIndex;
            
            return null;
        }

        /// <summary>
        /// Check if a label exists
        /// </summary>
        public bool HasLabel(string label)
        {
            return !string.IsNullOrEmpty(label) && Labels.ContainsKey(label);
        }

        /// <summary>
        /// Clear all labels
        /// </summary>
        public void ClearLabels()
        {
            Labels.Clear();
        }
        #endregion

        #region State Management
        /// <summary>
        /// Reset the context to initial state
        /// </summary>
        public void Reset()
        {
            Script = null;
            CurrentLineIndex = 0;
            State = PlaybackState.Idle;
            PlaybackSpeed = 1.0f;
            ExecutionTime = 0;
            LinesExecuted = 0;
            CommandsExecuted = 0;
            IsStepMode = false;
            StartTime = DateTime.MinValue;

            ClearVariables();
            ClearCallStack();
            ClearLabels();
            PerformanceMetrics?.Reset();

            // Keep breakpoints as they may be persistent

            DisposeCancellationTokenSource();
        }

        /// <summary>
        /// Update execution statistics
        /// </summary>
        public void UpdateExecutionTime()
        {
            if (StartTime != DateTime.MinValue)
            {
                ExecutionTime = (float)(DateTime.UtcNow - StartTime).TotalSeconds;
            }
        }

        /// <summary>
        /// Get execution progress (0.0 to 1.0)
        /// </summary>
        public float GetProgress()
        {
            if (Script == null || Script.Lines == null || Script.Lines.Count == 0)
                return 0f;

            return (float)CurrentLineIndex / Script.Lines.Count;
        }

        /// <summary>
        /// Check if execution can continue
        /// </summary>
        public bool CanContinue()
        {
            return Script != null &&
                   CurrentLineIndex < Script.Lines.Count &&
                   State != PlaybackState.Stopped &&
                   State != PlaybackState.Failed &&
                   State != PlaybackState.Completed &&
                   (CancellationTokenSource == null || !CancellationTokenSource.Token.IsCancellationRequested);
        }

        /// <summary>
        /// Check if at end of script
        /// </summary>
        public bool IsAtEnd()
        {
            return Script != null && CurrentLineIndex >= Script.Lines.Count;
        }

        /// <summary>
        /// Check if rollback to last checkpoint is possible
        /// </summary>
        public bool CanRollbackToLastCheckpoint()
        {
            // Simple implementation - can rollback if we have executed at least one line
            return LinesExecuted > 0 && CurrentLineIndex > 0;
        }
        #endregion

        #region Serialization Support
        /// <summary>
        /// Create a snapshot of the current context for saving
        /// </summary>
        public ScriptExecutionContextSnapshot CreateSnapshot()
        {
            return new ScriptExecutionContextSnapshot
            {
                ScriptName = Script?.Name,
                CurrentLineIndex = CurrentLineIndex,
                Variables = new Dictionary<string, object>(Variables),
                Breakpoints = new HashSet<int>(Breakpoints),
                Labels = new Dictionary<string, int>(Labels),
                PlaybackSpeed = PlaybackSpeed,
                ExecutionTime = ExecutionTime,
                LinesExecuted = LinesExecuted,
                CommandsExecuted = CommandsExecuted,
                IsStepMode = IsStepMode,
                CallStackFrames = CallStack.ToArray()
            };
        }

        /// <summary>
        /// Restore context from a snapshot
        /// </summary>
        public void RestoreFromSnapshot(ScriptExecutionContextSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            CurrentLineIndex = snapshot.CurrentLineIndex;
            Variables = new Dictionary<string, object>(snapshot.Variables);
            Breakpoints = new HashSet<int>(snapshot.Breakpoints);
            Labels = new Dictionary<string, int>(snapshot.Labels);
            PlaybackSpeed = snapshot.PlaybackSpeed;
            ExecutionTime = snapshot.ExecutionTime;
            LinesExecuted = snapshot.LinesExecuted;
            CommandsExecuted = snapshot.CommandsExecuted;
            IsStepMode = snapshot.IsStepMode;

            CallStack.Clear();
            if (snapshot.CallStackFrames != null)
            {
                // Push frames in reverse order to maintain correct stack order
                for (int i = snapshot.CallStackFrames.Length - 1; i >= 0; i--)
                {
                    CallStack.Push(snapshot.CallStackFrames[i]);
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Dispose of the ScriptExecutionContext and clean up all resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for proper IDisposable pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    DisposeCancellationTokenSource();
                    
                    // Clear collections
                    Variables?.Clear();
                    CallStack?.Clear();
                    Breakpoints?.Clear();
                    Labels?.Clear();
                    
                    // Reset metrics
                    PerformanceMetrics?.Reset();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure CancellationTokenSource is disposed even if Dispose() is not called
        /// </summary>
        ~ScriptExecutionContext()
        {
            Dispose(false);
        }

        /// <summary>
        /// Safely dispose of the CancellationTokenSource
        /// </summary>
        private void DisposeCancellationTokenSource()
        {
            try
            {
                if (CancellationTokenSource != null)
                {
                    if (!CancellationTokenSource.IsCancellationRequested)
                    {
                        CancellationTokenSource.Cancel();
                    }
                    CancellationTokenSource.Dispose();
                    CancellationTokenSource = null;
                }
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource was already disposed - this is fine
                CancellationTokenSource = null;
            }
            catch (Exception ex)
            {
                // Log unexpected exceptions but don't rethrow in finalizer
                if (!_disposed) // Only log if this is not called from finalizer
                {
                    Debug.LogWarning($"[ScriptExecutionContext] Error disposing CancellationTokenSource: {ex.Message}");
                }
                CancellationTokenSource = null;
            }
        }
        #endregion
    }
}
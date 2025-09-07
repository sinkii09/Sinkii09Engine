using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Common.Script;
using System;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Service interface for executing and controlling script playback with comprehensive async operations
    /// </summary>
    public interface IScriptPlayerService : IEngineService
    {
        #region State Properties
        /// <summary>
        /// Current playback state of the script player
        /// </summary>
        PlaybackState State { get; }

        /// <summary>
        /// Currently executing script
        /// </summary>
        Script CurrentScript { get; }

        /// <summary>
        /// Current line index being executed
        /// </summary>
        int CurrentLineIndex { get; }

        /// <summary>
        /// Current command being executed
        /// </summary>
        ICommand CurrentCommand { get; }

        /// <summary>
        /// Playback speed multiplier (1.0 = normal speed)
        /// </summary>
        float PlaybackSpeed { get; set; }

        /// <summary>
        /// Whether the script player is currently playing
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Whether the script player is paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Whether the script player is waiting for a command to complete
        /// </summary>
        bool IsWaiting { get; }

        /// <summary>
        /// Get current execution progress (0.0 to 1.0)
        /// </summary>
        float Progress { get; }
        #endregion

        #region Execution Control
        /// <summary>
        /// Play a script from the beginning
        /// </summary>
        /// <param name="script">Script to play</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result</returns>
        UniTask<ScriptExecutionResult> PlayScriptAsync(Script script, CancellationToken cancellationToken = default);

        /// <summary>
        /// Play a script by name (loads from ScriptService)
        /// </summary>
        /// <param name="scriptName">Name of the script to load and play</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result</returns>
        UniTask<ScriptExecutionResult> PlayScriptAsync(string scriptName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Continue playing from a specific line
        /// </summary>
        /// <param name="lineIndex">Line index to start from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result</returns>
        UniTask<ScriptExecutionResult> PlayFromLineAsync(int lineIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pause the current script execution
        /// </summary>
        UniTask PauseAsync();

        /// <summary>
        /// Resume the paused script execution
        /// </summary>
        UniTask ResumeAsync();

        /// <summary>
        /// Stop the current script execution
        /// </summary>
        UniTask StopAsync();

        /// <summary>
        /// Reset the script player to initial state
        /// </summary>
        UniTask ResetAsync();
        #endregion

        #region Navigation Control
        /// <summary>
        /// Step forward one line
        /// </summary>
        /// <returns>True if successful, false if at end</returns>
        UniTask<bool> StepForwardAsync();

        /// <summary>
        /// Step backward one line
        /// </summary>
        /// <returns>True if successful, false if at beginning</returns>
        UniTask<bool> StepBackwardAsync();

        /// <summary>
        /// Skip to a specific line
        /// </summary>
        /// <param name="lineIndex">Target line index</param>
        UniTask SkipToLineAsync(int lineIndex);

        /// <summary>
        /// Skip to a labeled line
        /// </summary>
        /// <param name="label">Label to skip to</param>
        UniTask SkipToLabelAsync(string label);

        /// <summary>
        /// Fast forward through the script
        /// </summary>
        /// <param name="speed">Fast forward speed multiplier</param>
        UniTask FastForwardAsync(float speed = 2.0f);
        #endregion

        #region Execution Context
        /// <summary>
        /// Set a variable in the execution context
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <param name="value">Variable value</param>
        void SetVariable(string name, object value);

        /// <summary>
        /// Get a variable from the execution context
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <returns>Variable value or null</returns>
        object GetVariable(string name);

        /// <summary>
        /// Check if a variable exists
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <returns>True if variable exists</returns>
        bool HasVariable(string name);

        /// <summary>
        /// Clear all variables
        /// </summary>
        void ClearVariables();

        /// <summary>
        /// Get the current execution context
        /// </summary>
        ScriptExecutionContext GetExecutionContext();
        #endregion

        #region Breakpoint Management
        /// <summary>
        /// Add a breakpoint at a specific line
        /// </summary>
        /// <param name="lineIndex">Line index for the breakpoint</param>
        void AddBreakpoint(int lineIndex);

        /// <summary>
        /// Remove a breakpoint
        /// </summary>
        /// <param name="lineIndex">Line index to remove breakpoint from</param>
        void RemoveBreakpoint(int lineIndex);

        /// <summary>
        /// Clear all breakpoints
        /// </summary>
        void ClearBreakpoints();

        /// <summary>
        /// Check if a line has a breakpoint
        /// </summary>
        /// <param name="lineIndex">Line index to check</param>
        /// <returns>True if breakpoint exists</returns>
        bool HasBreakpoint(int lineIndex);
        #endregion

        #region Save/Load State
        /// <summary>
        /// Save the current execution state
        /// </summary>
        /// <returns>Serialized execution state</returns>
        UniTask<string> SaveExecutionStateAsync();

        /// <summary>
        /// Load a previously saved execution state
        /// </summary>
        /// <param name="state">Serialized execution state</param>
        UniTask LoadExecutionStateAsync(string state);
        #endregion

        #region Events
        /// <summary>
        /// Fired when a script starts playing
        /// </summary>
        event Action<Script> ScriptStarted;

        /// <summary>
        /// Fired when a script completes execution
        /// </summary>
        event Action<Script, ScriptExecutionResult> ScriptCompleted;

        /// <summary>
        /// Fired when a script execution fails
        /// </summary>
        event Action<Script, Exception> ScriptFailed;

        /// <summary>
        /// Fired before a line is executed
        /// </summary>
        event Action<ScriptLine, int> LineExecuting;

        /// <summary>
        /// Fired after a line is executed
        /// </summary>
        event Action<ScriptLine, int> LineExecuted;

        /// <summary>
        /// Fired before a command is executed
        /// </summary>
        event Action<ICommand> CommandExecuting;

        /// <summary>
        /// Fired after a command is executed
        /// </summary>
        event Action<ICommand> CommandExecuted;

        /// <summary>
        /// Fired when playback state changes
        /// </summary>
        event Action<PlaybackState, PlaybackState> StateChanged;

        /// <summary>
        /// Fired when progress changes
        /// </summary>
        event Action<float> ProgressChanged;

        /// <summary>
        /// Fired when a breakpoint is hit
        /// </summary>
        event Action<int> BreakpointHit;

        /// <summary>
        /// Fired when a variable changes
        /// </summary>
        event Action<string, object> VariableChanged;
        #endregion
    }
}
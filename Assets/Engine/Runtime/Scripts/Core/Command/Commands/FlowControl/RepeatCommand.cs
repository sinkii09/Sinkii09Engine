using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// High-performance command for repetitive loops with thread-safe counter management
    /// 
    /// Usage examples:
    /// @repeat count:5 label:loopStart
    /// @repeat count:10 line:15 counter:i
    /// @repeat count:3 label:action counter:customLoop
    /// </summary>
    [Serializable]
    [CommandAlias("repeat")]
    public class RepeatCommand : Command, IFlowControlCommand
    {
        #region Parameters
        [Header("Loop Control")]
        [ParameterAlias(ParameterAliases.Count)]
        [RequiredParameter]
        public IntegerParameter count = new IntegerParameter(); // Number of times to repeat
        [ParameterAlias(ParameterAliases.Counter)]
        public StringParameter counterVar = new StringParameter(); // Variable to track iterations (optional)
        
        [Header("Loop Target")]
        [ParameterAlias(ParameterAliases.Label)]
        public StringParameter label = new StringParameter(); // Label to jump to
        [ParameterAlias(ParameterAliases.GotoLine)]
        public IntegerParameter line = new IntegerParameter(); // Line to jump to
        
        [Header("Behavior (Optional)")]
        [ParameterAlias(ParameterAliases.ZeroBased)]
        public BooleanParameter zeroBasedCounter = new BooleanParameter(); // Start counter at 0 instead of 1
        #endregion

        #region Static Configuration
        private static int s_loopIdCounter = 0;
        private const string LOOP_VAR_PREFIX = "_repeatLoop_";
        private const int MAX_LOOP_COUNT = 10000; // Safety limit
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            var result = await ExecuteWithResultAsync(token);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.ErrorMessage, result.Exception);
            }
        }

        public async UniTask<CommandResult> ExecuteWithResultAsync(CancellationToken token = default)
        {
            try
            {
                var scriptPlayer = Engine.GetService<IScriptPlayerService>();
                if (scriptPlayer == null)
                {
                    return CommandResult.Failed("ScriptPlayerService is not available for repeat command");
                }

                // Validate parameters
                if (!ValidateParameters(out string error))
                {
                    return CommandResult.Failed(error);
                }

                // Get loop variable name
                string loopVar = GetLoopVariableName();

                // Get current iteration count
                int currentIteration = GetCurrentIteration(scriptPlayer, loopVar);

                // Increment counter
                bool isZeroBased = zeroBasedCounter.HasValue && zeroBasedCounter.Value;
                int newIteration = currentIteration + 1;
                int displayIteration = isZeroBased ? newIteration - 1 : newIteration;

                scriptPlayer.SetVariable(loopVar, newIteration);

                // Check if we should continue looping
                bool shouldContinue = newIteration <= count.Value;

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[RepeatCommand] Loop iteration {displayIteration}/{(isZeroBased ? count.Value - 1 : count.Value)}");
                }

                if (shouldContinue)
                {
                    // Jump to loop target
                    return ExecuteJump();
                }

                // Loop completed, clean up if needed
                CleanupLoopVariable(scriptPlayer, loopVar);

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[RepeatCommand] Loop completed after {count.Value} iterations");
                }

                return CommandResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RepeatCommand] Failed to execute: {ex.Message}");
                return CommandResult.Failed($"Repeat command execution failed: {ex.Message}", ex);
            }
        }

        #region Helper Methods
        private bool ValidateParameters(out string error)
        {
            error = null;

            if (!count.HasValue || count.Value <= 0)
            {
                error = "Repeat command requires a positive count";
                return false;
            }

            if (count.Value > MAX_LOOP_COUNT)
            {
                error = $"Repeat count ({count.Value}) exceeds maximum allowed ({MAX_LOOP_COUNT})";
                return false;
            }

            if ((!label.HasValue || string.IsNullOrEmpty(label.Value)) && !line.HasValue)
            {
                error = "Repeat command requires either a label or line target";
                return false;
            }

            return true;
        }

        private string GetLoopVariableName()
        {
            // Use user-specified variable name or generate unique internal one
            if (counterVar.HasValue && !string.IsNullOrEmpty(counterVar.Value))
            {
                return counterVar.Value;
            }

            // Generate thread-safe unique ID
            int loopId = System.Threading.Interlocked.Increment(ref s_loopIdCounter);
            return $"{LOOP_VAR_PREFIX}{loopId}";
        }

        private int GetCurrentIteration(IScriptPlayerService scriptPlayer, string loopVar)
        {
            if (!scriptPlayer.HasVariable(loopVar))
            {
                return 0; // First iteration
            }

            var iterVar = scriptPlayer.GetVariable(loopVar);
            return iterVar switch
            {
                int i => i,
                long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
                float f when f >= int.MinValue && f <= int.MaxValue => (int)f,
                double d when d >= int.MinValue && d <= int.MaxValue => (int)d,
                _ => 0 // Invalid type, reset to 0
            };
        }

        private CommandResult ExecuteJump()
        {
            if (label.HasValue && !string.IsNullOrEmpty(label.Value))
            {
                return CommandResult.JumpToLabel(label.Value);
            }

            if (line.HasValue)
            {
                return CommandResult.JumpToLine(line.Value - 1); // Convert to 0-based
            }

            // This shouldn't happen due to validation, but handle gracefully
            return CommandResult.Failed("No valid jump target found");
        }

        private void CleanupLoopVariable(IScriptPlayerService scriptPlayer, string loopVar)
        {
            // Only clean up internal variables (those with our prefix)
            if (loopVar.StartsWith(LOOP_VAR_PREFIX))
            {
                scriptPlayer.SetVariable(loopVar, null);
            }
            // User-specified variables are left intact for potential reuse
        }
        #endregion
    }
}
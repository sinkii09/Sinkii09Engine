using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to jump to a specific line or label in the script
    /// 
    /// Usage examples:
    /// @goto start
    /// @goto label:mainMenu
    /// @goto line:25
    /// </summary>
    [Serializable]
    [CommandAlias("goto")]
    public class GotoCommand : Command, IFlowControlCommand
    {
        #region Parameters
        [Header("Target")]
        [ParameterAlias(ParameterAliases.Label)]
        public StringParameter label = new StringParameter();
        [ParameterAlias(ParameterAliases.GotoLine)]
        public IntegerParameter line = new IntegerParameter();
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            // Use the flow control version
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
                // Determine target and return appropriate flow control result
                if (label.HasValue && !string.IsNullOrEmpty(label.Value))
                {
                    // Jump to label
                    Debug.Log($"[GotoCommand] Jumping to label '{label.Value}'");
                    return CommandResult.JumpToLabel(label.Value);
                }
                else if (line.HasValue)
                {
                    // Jump to line number (convert to 0-based index)
                    int targetLine = line.Value - 1; // Script lines are 1-based in user input
                    Debug.Log($"[GotoCommand] Jumping to line {line.Value}");
                    return CommandResult.JumpToLine(targetLine);
                }
                else
                {
                    return CommandResult.Failed("Goto command requires either a label or line number");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GotoCommand] Failed to execute: {ex.Message}");
                return CommandResult.Failed($"Goto command execution failed: {ex.Message}", ex);
            }
        }
    }
}
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to jump to a specific line or label in the script (alias for goto)
    /// 
    /// Usage examples:
    /// @jump start
    /// @jump label:mainMenu
    /// @jump line:25
    /// </summary>
    [Serializable]
    [CommandAlias("jump")]
    public class JumpCommand : Command, IFlowControlCommand
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
                    Debug.Log($"[JumpCommand] Jumping to label '{label.Value}'");
                    return CommandResult.JumpToLabel(label.Value);
                }
                else if (line.HasValue)
                {
                    // Jump to line number (convert to 0-based index)
                    int targetLine = line.Value - 1; // Script lines are 1-based in user input
                    Debug.Log($"[JumpCommand] Jumping to line {line.Value}");
                    return CommandResult.JumpToLine(targetLine);
                }
                else
                {
                    return CommandResult.Failed("Jump command requires either a label or line number");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JumpCommand] Failed to execute: {ex.Message}");
                return CommandResult.Failed($"Jump command execution failed: {ex.Message}", ex);
            }
        }
    }
}
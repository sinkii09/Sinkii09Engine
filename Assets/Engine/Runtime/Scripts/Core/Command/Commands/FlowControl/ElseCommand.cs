using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Lightweight command that marks an else branch in the script
    /// Used primarily as a marker/label for conditional flow control
    /// 
    /// Usage examples:
    /// @if left:health op:> val:0 goto:alive else:dead
    /// @else label:dead
    /// # Handle else case here
    /// </summary>
    [Serializable]
    [CommandAlias("else")]
    public class ElseCommand : Command
    {
        #region Parameters
        [Header("Optional Identifier")]
        [ParameterAlias(ParameterAliases.Label)]
        public StringParameter label = new StringParameter(); // Optional label identifier
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // The else command is primarily a marker/label in the script
                // It performs minimal work to maintain high performance
                
                if (Debug.isDebugBuild && label.HasValue && !string.IsNullOrEmpty(label.Value))
                {
                    Debug.Log($"[ElseCommand] Executing else block '{label.Value}'");
                }

                // Continue execution normally with minimal overhead
                await UniTask.Yield();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ElseCommand] Failed to execute: {ex.Message}");
                throw;
            }
        }
    }
}
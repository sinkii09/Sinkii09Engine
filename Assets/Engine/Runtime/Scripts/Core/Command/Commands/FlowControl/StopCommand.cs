using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command for stopping script execution with optional user messaging
    /// 
    /// Usage examples:
    /// @stop
    /// @stop reason:"Game Over"
    /// @stop reason:"Script completed successfully" show:true
    /// </summary>
    [Serializable]
    [CommandAlias("stop")]
    public class StopCommand : Command, IFlowControlCommand
    {
        #region Parameters
        [Header("Stop Information")]
        [ParameterAlias(ParameterAliases.Reason)]
        public StringParameter reason = new StringParameter(); // Reason for stopping
        [ParameterAlias(ParameterAliases.Show)]
        public BooleanParameter showMessage = new BooleanParameter(); // Whether to show stop message
        #endregion

        #region Static Configuration
        private const string DEFAULT_STOP_REASON = "Script execution stopped by command";
        private const string MESSAGE_STYLE_FORMAT = "<align=center><i>{0}</i></align>";
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
                string stopReason = GetStopReason();

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[StopCommand] Stopping script execution: {stopReason}");
                }

                // Show message to user if requested
                if (ShouldShowMessage())
                {
                    await DisplayStopMessageAsync(stopReason, token);
                }

                return CommandResult.Stop(stopReason);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StopCommand] Failed to execute: {ex.Message}");
                return CommandResult.Failed($"Stop command execution failed: {ex.Message}", ex);
            }
        }

        #region Helper Methods
        private string GetStopReason()
        {
            return reason.HasValue && !string.IsNullOrEmpty(reason.Value) 
                ? reason.Value 
                : DEFAULT_STOP_REASON;
        }

        private bool ShouldShowMessage()
        {
            return showMessage.HasValue && showMessage.Value;
        }

        private async UniTask DisplayStopMessageAsync(string stopReason, CancellationToken token)
        {
            var dialogueService = Engine.GetService<IDialogueService>();
            if (dialogueService == null)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning("[StopCommand] DialogueService not available, cannot show stop message");
                }
                return;
            }

            try
            {
                string styledMessage = string.Format(MESSAGE_STYLE_FORMAT, stopReason);
                await dialogueService.ShowNarrationAsync(styledMessage);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StopCommand] Failed to show stop message: {ex.Message}");
            }
        }
        #endregion
    }
}
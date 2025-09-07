using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to pause script execution for dramatic timing
    /// Integrates with DialogueService for consistent timing control
    /// 
    /// Usage examples:
    /// @wait 2.5
    /// @wait duration:1.0
    /// @wait 3.0 skipable:true
    /// @wait duration:5.0 message:"Loading..." skipable:false
    /// </summary>
    [Serializable]
    [CommandAlias("wait")]
    [CommandMeta(
        timeout: 300f, // Long timeout for wait commands
        maxRetries: 1,
        category: CommandCategory.Timing,
        critical: false,
        fallback: FallbackAction.Continue,
        expectedDuration: 5f)]
    public class WaitCommand : Command
    {
        #region Core Parameters
        
        [Header("Timing")]
        [RequiredParameter]
        [ParameterAlias(ParameterAliases.Duration)]
        public DecimalParameter duration = new DecimalParameter();  // Duration in seconds
        
        #endregion
        
        #region Behavior Parameters
        
        [Header("Behavior")]
        public BooleanParameter skipable = new BooleanParameter();        // Can be skipped by player
        public StringParameter message = new StringParameter();    // Optional message to display
        public BooleanParameter showTimer = new BooleanParameter();      // Show countdown timer
        
        #endregion
        
        #region Visual Parameters
        
        [Header("Visual Effects")]
        public StringParameter effect = new StringParameter();     // Visual effect during wait
        public StringParameter color = new StringParameter();     // Message text color
        public DecimalParameter opacity = new DecimalParameter();  // Message opacity
        
        #endregion
        
        #region Command Execution
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get duration (required parameter)
                float waitDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                if (waitDuration <= 0)
                {
                    Debug.LogWarning("[WaitCommand] Duration must be positive, using 1 second default");
                    waitDuration = 1.0f;
                }
                
                // Get dialogue service for consistent timing
                var dialogueService = Engine.GetService<IDialogueService>();
                
                // Show optional message
                if (!string.IsNullOrEmpty(message.Value))
                {
                    await ShowWaitMessage();
                }
                
                // Apply visual effect if specified
                ApplyWaitEffect();
                
                // Perform the wait
                await PerformWait(waitDuration, token);
                
                // Clear any visual effects
                ClearWaitEffect();
                
                Debug.Log($"[WaitCommand] Successfully waited for {waitDuration} seconds");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[WaitCommand] Wait was cancelled or skipped");
                ClearWaitEffect();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WaitCommand] Error executing command: {ex.Message}");
                ClearWaitEffect();
                throw;
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private async UniTask ShowWaitMessage()
        {
            var dialogueService = Engine.GetService<IDialogueService>();
            if (dialogueService == null)
            {
                // Fallback to debug log if DialogueService not available
                Debug.Log($"[WaitCommand] {message.Value}");
                return;
            }
            
            // Create styled message text
            var styledMessage = CreateStyledMessage();
            
            try
            {
                // Show message as narration
                await dialogueService.ShowNarrationAsync(styledMessage);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WaitCommand] Failed to show wait message: {ex.Message}");
                Debug.Log($"[WaitCommand] {message.Value}");
            }
        }
        
        private string CreateStyledMessage()
        {
            var msg = message.Value;
            
            // Apply color styling
            if (!string.IsNullOrEmpty(color.Value))
            {
                var messageColor = ParseColor(color.Value);
                var colorHex = ColorUtility.ToHtmlStringRGB(messageColor);
                msg = $"<color=#{colorHex}>{msg}</color>";
            }
            
            // Apply opacity
            if (opacity.HasValue)
            {
                var alpha = Mathf.Clamp01((float)opacity.Value);
                var alphaHex = ((int)(alpha * 255)).ToString("X2");
                if (msg.Contains("<color="))
                {
                    msg = msg.Replace(">", alphaHex + ">");
                }
                else
                {
                    msg = $"<color=#FFFFFF{alphaHex}>{msg}</color>";
                }
            }
            
            // Apply centering for wait messages
            msg = $"<align=center><i>{msg}</i></align>";
            
            return msg;
        }
        
        private async UniTask PerformWait(float waitDuration, CancellationToken token)
        {
            var dialogueService = Engine.GetService<IDialogueService>();
            
            if (dialogueService != null)
            {
                // Use DialogueService wait for consistency
                await dialogueService.WaitAsync(waitDuration, token);
            }
            else
            {
                // Fallback to direct UniTask delay
                await UniTask.Delay(Mathf.RoundToInt(waitDuration * 1000), cancellationToken: token);
            }
        }
        
        private void ApplyWaitEffect()
        {
            if (string.IsNullOrEmpty(effect.Value))
                return;
            
            // Apply visual effects during wait
            switch (effect.Value.ToLower())
            {
                case "fade":
                case "fadeout":
                    Debug.Log("[WaitCommand] Applying fade effect");
                    // This would integrate with your visual effects system
                    break;
                    
                case "blur":
                    Debug.Log("[WaitCommand] Applying blur effect");
                    // This would integrate with post-processing
                    break;
                    
                case "dim":
                case "darken":
                    Debug.Log("[WaitCommand] Applying dim effect");
                    // This would dim the screen
                    break;
                    
                case "pulse":
                    Debug.Log("[WaitCommand] Applying pulse effect");
                    // This would create a pulsing effect
                    break;
                    
                default:
                    Debug.Log($"[WaitCommand] Unknown effect: {effect.Value}");
                    break;
            }
        }
        
        private void ClearWaitEffect()
        {
            if (string.IsNullOrEmpty(effect.Value))
                return;
            
            Debug.Log($"[WaitCommand] Clearing effect: {effect.Value}");
            // This would clear any applied visual effects
        }
        
        private Color ParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
                return Color.white;
            
            // Handle named colors
            switch (colorString.ToLower())
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "gray": case "grey": return Color.gray;
                case "orange": return new Color(1f, 0.65f, 0f);
                case "purple": return new Color(0.5f, 0f, 0.5f);
                case "pink": return new Color(1f, 0.75f, 0.8f);
            }
            
            // Handle hex colors (#RRGGBB or #RRGGBBAA)
            if (colorString.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(colorString, out var hexColor))
                {
                    return hexColor;
                }
            }
            
            return Color.white;
        }
        
        #endregion
    }
}
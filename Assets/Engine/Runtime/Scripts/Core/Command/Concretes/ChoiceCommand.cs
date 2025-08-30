using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to present player choices and handle selection
    /// Integrates with DialogueService for comprehensive choice management
    /// 
    /// Usage examples:
    /// @choice "Yes, I agree" "No, I disagree" "I need time to think"
    /// @choice option1:"Accept the quest" option2:"Decline" option3:"Ask for more information"
    /// @choice "Continue" color:green "Go back" color:red timeout:10 default:0
    /// </summary>
    [Serializable]
    [CommandAlias("choice")]
    public class ChoiceCommand : Command
    {
        #region Choice Options
        
        [Header("Choice Options")]
        public StringParameter option1 = new StringParameter();
        public StringParameter option2 = new StringParameter();
        public StringParameter option3 = new StringParameter();
        public StringParameter option4 = new StringParameter();
        public StringParameter option5 = new StringParameter();
        public StringParameter option6 = new StringParameter();
        
        #endregion
        
        #region Choice Colors
        
        [Header("Choice Colors")]
        public StringParameter color1 = new StringParameter();
        public StringParameter color2 = new StringParameter();
        public StringParameter color3 = new StringParameter();
        public StringParameter color4 = new StringParameter();
        public StringParameter color5 = new StringParameter();
        public StringParameter color6 = new StringParameter();
        
        #endregion
        
        #region Choice Enabled State
        
        [Header("Choice Availability")]
        public BooleanParameter enabled1 = new BooleanParameter { Value = true };
        public BooleanParameter enabled2 = new BooleanParameter { Value = true };
        public BooleanParameter enabled3 = new BooleanParameter { Value = true };
        public BooleanParameter enabled4 = new BooleanParameter { Value = true };
        public BooleanParameter enabled5 = new BooleanParameter { Value = true };
        public BooleanParameter enabled6 = new BooleanParameter { Value = true };
        
        #endregion
        
        #region Timing and Behavior
        
        [Header("Timing Control")]
        public StringParameter prompt = new StringParameter();       // Optional prompt text
        public DecimalParameter timeout = new DecimalParameter();    // Time limit in seconds
        public IntegerParameter defaultChoice = new IntegerParameter(); // Default when timeout
        public BooleanParameter showTimer = new BooleanParameter();        // Show countdown timer
        
        #endregion
        
        #region Result Storage
        
        [Header("Result Variables")]
        public StringParameter resultVar = new StringParameter();    // Variable to store result
        public StringParameter selectedTextVar = new StringParameter(); // Variable to store selected text
        
        #endregion
        
        #region Command Execution
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get dialogue service
                var dialogueService = Engine.GetService<IDialogueService>();
                if (dialogueService == null)
                {
                    Debug.LogError("[ChoiceCommand] DialogueService not found");
                    return;
                }
                
                // Create choice array from parameters
                var choices = CreateChoicesFromParameters();
                if (choices.Length == 0)
                {
                    Debug.LogError("[ChoiceCommand] No valid choices provided");
                    return;
                }
                
                // Setup timeout if specified
                SetupChoiceTimeout(dialogueService);
                
                // Show choices (with optional prompt)
                int selectedIndex;
                if (!string.IsNullOrEmpty(prompt.Value))
                {
                    selectedIndex = await dialogueService.ShowChoicesWithPromptAsync(prompt.Value, choices, token);
                }
                else
                {
                    selectedIndex = await dialogueService.ShowChoicesAsync(choices, token);
                }
                
                // Handle result
                await HandleChoiceResult(selectedIndex, choices);
                
                Debug.Log($"[ChoiceCommand] Choice selected: {selectedIndex} - {choices[selectedIndex].Text}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChoiceCommand] Error executing command: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private DialogueChoice[] CreateChoicesFromParameters()
        {
            var choicesList = new List<DialogueChoice>();
            
            // Process each option parameter
            var options = new[] { option1, option2, option3, option4, option5, option6 };
            var colors = new[] { color1, color2, color3, color4, color5, color6 };
            var enabledStates = new[] { enabled1, enabled2, enabled3, enabled4, enabled5, enabled6 };
            
            for (int i = 0; i < options.Length; i++)
            {
                if (!string.IsNullOrEmpty(options[i].Value))
                {
                    var choice = new DialogueChoice
                    {
                        Text = options[i].Value,
                        IsEnabled = enabledStates[i].HasValue ? enabledStates[i].Value : true
                    };
                    
                    // Apply color if specified
                    if (!string.IsNullOrEmpty(colors[i].Value))
                    {
                        choice.TextColor = ParseColor(colors[i].Value);
                    }
                    
                    choicesList.Add(choice);
                }
            }
            
            return choicesList.ToArray();
        }
        
        private void SetupChoiceTimeout(IDialogueService dialogueService)
        {
            // This would typically be handled by the DialogueService configuration
            // For now, we'll just log the timeout setup
            if (timeout.HasValue && timeout.Value > 0)
            {
                Debug.Log($"[ChoiceCommand] Choice timeout set to {timeout.Value} seconds");
                
                if (defaultChoice.HasValue)
                {
                    Debug.Log($"[ChoiceCommand] Default choice on timeout: {defaultChoice.Value}");
                }
            }
        }
        
        private async UniTask HandleChoiceResult(int selectedIndex, DialogueChoice[] choices)
        {
            // Store result in variable if specified
            if (!string.IsNullOrEmpty(resultVar.Value))
            {
                // This would integrate with your variable/state system
                SetGameVariable(resultVar.Value, selectedIndex);
                Debug.Log($"[ChoiceCommand] Set variable '{resultVar.Value}' to {selectedIndex}");
            }
            
            // Store selected text in variable if specified
            if (!string.IsNullOrEmpty(selectedTextVar.Value) && selectedIndex >= 0 && selectedIndex < choices.Length)
            {
                SetGameVariable(selectedTextVar.Value, choices[selectedIndex].Text);
                Debug.Log($"[ChoiceCommand] Set variable '{selectedTextVar.Value}' to '{choices[selectedIndex].Text}'");
            }
            
            await UniTask.Yield();
        }
        
        private void SetGameVariable(string variableName, object value)
        {
            // This would integrate with your game's variable/state system
            // For now, we'll just log the operation
            Debug.Log($"[ChoiceCommand] Setting game variable: {variableName} = {value}");
            
            // Example integration points:
            // - Story variables system
            // - SaveLoadService for persistent variables
            // - ScriptService for script-level variables
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
            
            // Handle RGB format (r,g,b)
            if (colorString.Contains(","))
            {
                var parts = colorString.Split(',');
                if (parts.Length >= 3 && 
                    float.TryParse(parts[0], out var r) && 
                    float.TryParse(parts[1], out var g) && 
                    float.TryParse(parts[2], out var b))
                {
                    var a = parts.Length > 3 && float.TryParse(parts[3], out var aVal) ? aVal : 1f;
                    return new Color(r, g, b, a);
                }
            }
            
            return Color.white;
        }
        
        #endregion
    }
}
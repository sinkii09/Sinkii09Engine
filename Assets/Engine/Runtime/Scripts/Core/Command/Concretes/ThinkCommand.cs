using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to display character thoughts (internal monologue)
    /// Integrates with DialogueService for consistent text display
    /// 
    /// Usage examples:
    /// @think character:Alice text:"What should I do now?"
    /// @think Alice "This seems suspicious..." speed:15
    /// @think character:protagonist text:"I hope I made the right choice." color:lightblue
    /// </summary>
    [Serializable]
    [CommandAlias("think")]
    public class ThinkCommand : Command
    {
        #region Core Parameters
        
        [Header("Character & Content")]
        public StringParameter character = new StringParameter();
        public StringParameter text = new StringParameter();
        
        #endregion
        
        #region Text Display Parameters
        
        [Header("Text Display")]
        public DecimalParameter speed = new DecimalParameter();     // Characters per second
        public StringParameter color = new StringParameter();      // Text color (default: light blue)
        public StringParameter nameColor = new StringParameter();  // Name color for thoughts
        public BooleanParameter skipable = new BooleanParameter();       // Can be skipped
        
        #endregion
        
        #region Timing Parameters
        
        [Header("Timing Control")]
        public DecimalParameter preDelay = new DecimalParameter();  // Delay before showing
        public DecimalParameter postDelay = new DecimalParameter(); // Delay after showing
        public BooleanParameter autoAdvance = new BooleanParameter();     // Auto-advance override
        public DecimalParameter autoAdvanceDelay = new DecimalParameter();
        
        #endregion
        
        #region Visual Effects Parameters
        
        [Header("Visual Effects")]
        public StringParameter style = new StringParameter();       // Thought display style
        public DecimalParameter opacity = new DecimalParameter();   // Text opacity (for dream-like effects)
        public BooleanParameter italics = new BooleanParameter();         // Force italic text
        
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
                    Debug.LogError("[ThinkCommand] DialogueService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(text.Value))
                {
                    Debug.LogError("[ThinkCommand] Text parameter is required");
                    return;
                }
                
                // Parse character ID
                var characterId = ParseCharacterId();
                if (string.IsNullOrEmpty(characterId))
                {
                    Debug.LogError("[ThinkCommand] Character parameter is required for thoughts");
                    return;
                }
                
                // Create thought dialogue line
                var thoughtLine = CreateThoughtLine(characterId);
                
                // Apply visual styling for thoughts
                ApplyThoughtStyling(thoughtLine);
                
                // Show thought using DialogueService
                await ShowThoughtAsync(dialogueService, thoughtLine, token);
                
                Debug.Log($"[ThinkCommand] Successfully displayed thought: {characterId}: {text.Value}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkCommand] Error executing command: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private string ParseCharacterId()
        {
            var charValue = character.Value;
            
            // Handle "character:Alice" format
            if (!string.IsNullOrEmpty(charValue) && charValue.Contains(":"))
            {
                var parts = charValue.Split(':');
                return parts.Length > 1 ? parts[1] : parts[0];
            }
            
            // Handle direct character name
            return charValue;
        }
        
        private DialogueLine CreateThoughtLine(string characterId)
        {
            var line = new DialogueLine(characterId, text.Value, null, DialogueType.Thought);
            
            // Apply metadata from parameters
            if (speed.HasValue)
                line.Metadata.CustomTypingSpeed = (float)speed.Value;
            
            if (preDelay.HasValue)
                line.Metadata.PreDelay = (float)preDelay.Value;
                
            if (postDelay.HasValue)
                line.Metadata.PostDelay = (float)postDelay.Value;
            
            if (skipable.HasValue)
                line.Metadata.CanSkip = skipable.Value;
            
            // Set thought-specific defaults if not overridden
            if (!color.HasValue || string.IsNullOrEmpty(color.Value))
            {
                // Default thought color (light blue)
                line.Metadata.TextColor = new Color(0.8f, 0.8f, 1f);
            }
            else
            {
                line.Metadata.TextColor = ParseColor(color.Value);
            }
                
            if (!string.IsNullOrEmpty(nameColor.Value))
                line.Metadata.NameColor = ParseColor(nameColor.Value);
            else
                line.Metadata.NameColor = line.Metadata.TextColor; // Match text color
            
            // Apply opacity if specified
            if (opacity.HasValue)
            {
                var currentColor = line.Metadata.TextColor;
                currentColor.a = Mathf.Clamp01((float)opacity.Value);
                line.Metadata.TextColor = currentColor;
            }
            
            return line;
        }
        
        private void ApplyThoughtStyling(DialogueLine thoughtLine)
        {
            // Apply italic styling for thoughts if requested or by default
            if (!italics.HasValue || italics.Value)
            {
                // Wrap text in italic tags for rich text support
                thoughtLine.Text = $"<i>{thoughtLine.Text}</i>";
            }
            
            // Apply custom styling based on style parameter
            if (!string.IsNullOrEmpty(style.Value))
            {
                switch (style.Value.ToLower())
                {
                    case "dream":
                    case "dreamy":
                        // Dreamy effect with reduced opacity and wave
                        thoughtLine.Metadata.TextColor = new Color(0.9f, 0.8f, 1f, 0.8f);
                        thoughtLine.Text = $"<color=#E6CCFF><i>~ {thoughtLine.Text} ~</i></color>";
                        break;
                        
                    case "whisper":
                        // Whispered thoughts with smaller text
                        thoughtLine.Text = $"<size=85%><i>({thoughtLine.Text})</i></size>";
                        break;
                        
                    case "inner":
                    case "internal":
                        // Standard internal monologue
                        thoughtLine.Text = $"<color=#B3CCFF><i>{thoughtLine.Text}</i></color>";
                        break;
                        
                    case "memory":
                        // Memory-style thoughts with sepia tone
                        thoughtLine.Text = $"<color=#CCAA88><i>《 {thoughtLine.Text} 》</i></color>";
                        break;
                        
                    default:
                        // Unknown style, use default italic
                        thoughtLine.Text = $"<i>{thoughtLine.Text}</i>";
                        break;
                }
            }
        }
        
        private async UniTask ShowThoughtAsync(IDialogueService dialogueService, DialogueLine thoughtLine, CancellationToken token)
        {
            // Handle auto-advance override
            if (autoAdvance.HasValue)
            {
                var delay = autoAdvanceDelay.HasValue ? (float)autoAdvanceDelay.Value : 1.5f; // Shorter default for thoughts
                dialogueService.SetAutoAdvance(autoAdvance.Value, delay);
            }
            
            // Show thought using DialogueService
            await dialogueService.ShowThoughtAsync(
                thoughtLine.SpeakerId, 
                thoughtLine.Text, 
                token
            );
        }
        
        private Color ParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
                return new Color(0.8f, 0.8f, 1f); // Default light blue for thoughts
            
            // Handle named colors with thought-appropriate variations
            switch (colorString.ToLower())
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "lightblue": return new Color(0.8f, 0.8f, 1f);
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "gray": case "grey": return Color.gray;
                case "orange": return new Color(1f, 0.65f, 0f);
                case "purple": return new Color(0.5f, 0f, 0.5f);
                case "pink": return new Color(1f, 0.75f, 0.8f);
                case "silver": return new Color(0.8f, 0.8f, 0.8f);
                case "gold": return new Color(1f, 0.84f, 0f);
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
            
            return new Color(0.8f, 0.8f, 1f); // Default light blue for thoughts
        }
        
        #endregion
    }
}
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to display narrator text and scene descriptions
    /// Integrates with DialogueService for consistent text display
    /// 
    /// Usage examples:
    /// @narrate text:"The sun was setting over the mountains..."
    /// @narrate "Alice walked into the classroom." speed:25
    /// @narrate text:"Meanwhile, in another part of town..." style:scene color:gray
    /// </summary>
    [Serializable]
    [CommandAlias("narrator")]
    public class NarrateCommand : Command
    {
        #region Core Parameters
        
        [Header("Content")]
        public StringParameter text = new StringParameter();
        
        #endregion
        
        #region Text Display Parameters
        
        [Header("Text Display")]
        public DecimalParameter speed = new DecimalParameter();     // Characters per second
        public StringParameter color = new StringParameter();      // Text color (default: light gray)
        public BooleanParameter skipable = new BooleanParameter();       // Can be skipped
        
        #endregion
        
        #region Timing Parameters
        
        [Header("Timing Control")]
        public DecimalParameter preDelay = new DecimalParameter();  // Delay before showing
        public DecimalParameter postDelay = new DecimalParameter(); // Delay after showing
        public BooleanParameter autoAdvance = new BooleanParameter();     // Auto-advance override
        public DecimalParameter autoAdvanceDelay = new DecimalParameter();
        
        #endregion
        
        #region Style Parameters
        
        [Header("Narrative Style")]
        public StringParameter style = new StringParameter();       // Narration style
        public StringParameter alignment = new StringParameter();   // Text alignment (center, left, right)
        public DecimalParameter fontSize = new DecimalParameter();  // Font size multiplier
        public BooleanParameter italic = new BooleanParameter();          // Italic styling
        
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
                    Debug.LogError("[NarrateCommand] DialogueService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(text.Value))
                {
                    Debug.LogError("[NarrateCommand] Text parameter is required");
                    return;
                }
                
                // Create narration dialogue line
                var narrationLine = CreateNarrationLine();
                
                // Apply narrative styling
                ApplyNarrativeStyle(narrationLine);
                
                // Show narration using DialogueService
                await ShowNarrationAsync(dialogueService, narrationLine, token);
                
                Debug.Log($"[NarrateCommand] Successfully displayed narration: {text.Value}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NarrateCommand] Error executing command: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Private Helper Methods
        
        private DialogueLine CreateNarrationLine()
        {
            var line = new DialogueLine(null, text.Value, null, DialogueType.Narration);
            
            // Apply metadata from parameters
            if (speed.HasValue)
                line.Metadata.CustomTypingSpeed = (float)speed.Value;
            
            if (preDelay.HasValue)
                line.Metadata.PreDelay = (float)preDelay.Value;
                
            if (postDelay.HasValue)
                line.Metadata.PostDelay = (float)postDelay.Value;
            
            if (skipable.HasValue)
                line.Metadata.CanSkip = skipable.Value;
            
            // Set narration-specific defaults if not overridden
            if (!color.HasValue || string.IsNullOrEmpty(color.Value))
            {
                // Default narration color (light gray)
                line.Metadata.TextColor = new Color(0.9f, 0.9f, 0.9f);
            }
            else
            {
                line.Metadata.TextColor = ParseColor(color.Value);
            }
            
            // Apply font size multiplier
            if (fontSize.HasValue)
                line.Metadata.FontSizeMultiplier = (float)fontSize.Value;
            
            return line;
        }
        
        private void ApplyNarrativeStyle(DialogueLine narrationLine)
        {
            var styledText = narrationLine.Text;
            
            // Apply italic styling if requested
            if (italic.HasValue && italic.Value)
            {
                styledText = $"<i>{styledText}</i>";
            }
            
            // Apply alignment if specified
            if (!string.IsNullOrEmpty(alignment.Value))
            {
                switch (alignment.Value.ToLower())
                {
                    case "center":
                    case "centre":
                        styledText = $"<align=center>{styledText}</align>";
                        break;
                    case "right":
                        styledText = $"<align=right>{styledText}</align>";
                        break;
                    case "left":
                        styledText = $"<align=left>{styledText}</align>";
                        break;
                }
            }
            
            // Apply style-based formatting
            if (!string.IsNullOrEmpty(style.Value))
            {
                switch (style.Value.ToLower())
                {
                    case "scene":
                    case "description":
                        // Scene description style with centering and italics
                        styledText = $"<align=center><i><color=#CCCCCC>{styledText}</color></i></align>";
                        break;
                        
                    case "time":
                    case "temporal":
                        // Time transition style
                        styledText = $"<align=center><b><color=#DDDDDD>~ {styledText} ~</color></b></align>";
                        break;
                        
                    case "location":
                    case "place":
                        // Location description style
                        styledText = $"<align=center><size=110%><color=#D0D0D0>【 {styledText} 】</color></size></align>";
                        break;
                        
                    case "action":
                        // Action description style
                        styledText = $"<color=#E0E0E0><i>* {styledText} *</i></color>";
                        break;
                        
                    case "chapter":
                    case "title":
                        // Chapter/title style with larger text
                        styledText = $"<align=center><size=130%><b><color=#F0F0F0>{styledText}</color></b></size></align>";
                        break;
                        
                    case "subtitle":
                        // Subtitle style
                        styledText = $"<align=center><size=90%><color=#C0C0C0><i>{styledText}</i></color></size></align>";
                        break;
                        
                    case "whisper":
                        // Whispered narration
                        styledText = $"<size=85%><color=#B0B0B0><i>({styledText})</i></color></size>";
                        break;
                        
                    case "dramatic":
                        // Dramatic emphasis
                        styledText = $"<align=center><b><size=120%><color=#FFFFFF>{styledText}</color></size></b></align>";
                        break;
                        
                    default:
                        // Unknown style, use default
                        break;
                }
            }
            
            narrationLine.Text = styledText;
        }
        
        private async UniTask ShowNarrationAsync(IDialogueService dialogueService, DialogueLine narrationLine, CancellationToken token)
        {
            // Handle auto-advance override
            if (autoAdvance.HasValue)
            {
                var delay = autoAdvanceDelay.HasValue ? (float)autoAdvanceDelay.Value : 2.5f; // Longer default for narration
                dialogueService.SetAutoAdvance(autoAdvance.Value, delay);
            }
            
            // Show narration using DialogueService
            await dialogueService.ShowNarrationAsync(narrationLine.Text, token);
        }
        
        private Color ParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
                return new Color(0.9f, 0.9f, 0.9f); // Default light gray for narration
            
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
                case "lightgray": case "lightgrey": return new Color(0.9f, 0.9f, 0.9f);
                case "darkgray": case "darkgrey": return new Color(0.6f, 0.6f, 0.6f);
                case "orange": return new Color(1f, 0.65f, 0f);
                case "purple": return new Color(0.5f, 0f, 0.5f);
                case "pink": return new Color(1f, 0.75f, 0.8f);
                case "silver": return new Color(0.8f, 0.8f, 0.8f);
                case "gold": return new Color(1f, 0.84f, 0f);
                case "sepia": return new Color(0.8f, 0.7f, 0.5f);
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
            
            return new Color(0.9f, 0.9f, 0.9f); // Default light gray for narration
        }
        
        #endregion
    }
}
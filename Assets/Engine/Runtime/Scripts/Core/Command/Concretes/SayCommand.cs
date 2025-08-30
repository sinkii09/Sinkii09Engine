using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to display character dialogue with voice and visual effects
    /// Integrates with DialogueService for comprehensive dialogue management
    /// 
    /// Usage examples:
    /// @say character:Alice text:"Hello there!" voice:alice_greeting_01
    /// @say Alice "Welcome to our school!" alice_welcome_01
    /// @say character:Alice text:"How are you feeling today?" speed:20 color:blue
    /// </summary>
    [Serializable]
    [CommandAlias("say")]
    public class SayCommand : Command
    {
        #region Core Parameters
        
        [Header("Character & Content")]
        public StringParameter character = new StringParameter();
        public StringParameter text = new StringParameter();
        
        [Header("Voice Integration")]
        public StringParameter voice = new StringParameter();
        public DecimalParameter voiceVolume = new DecimalParameter();
        
        #endregion
        
        #region Text Display Parameters
        
        [Header("Text Display")]
        public DecimalParameter speed = new DecimalParameter(); // Characters per second
        public StringParameter color = new StringParameter();   // Text color
        public StringParameter nameColor = new StringParameter(); // Name color
        public BooleanParameter skipable = new BooleanParameter();     // Can be skipped
        
        #endregion
        
        #region Timing Parameters
        
        [Header("Timing Control")]
        public DecimalParameter preDelay = new DecimalParameter();  // Delay before showing
        public DecimalParameter postDelay = new DecimalParameter(); // Delay after showing
        public BooleanParameter autoAdvance = new BooleanParameter();     // Auto-advance override
        public DecimalParameter autoAdvanceDelay = new DecimalParameter();
        
        #endregion
        
        #region Character Expression Parameters
        
        [Header("Character Expression")]
        public StringParameter emotion = new StringParameter();     // Character emotion
        public StringParameter pose = new StringParameter();       // Character pose
        public StringParameter position = new StringParameter();   // Character position
        
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
                    Debug.LogError("[SayCommand] DialogueService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(text.Value))
                {
                    Debug.LogError("[SayCommand] Text parameter is required");
                    return;
                }
                
                // Parse character ID (handle both "character:Alice" and "Alice" formats)
                var characterId = ParseCharacterId();
                
                // Create dialogue line
                var dialogueLine = CreateDialogueLine(characterId);
                
                // Apply character expression changes if specified
                await ApplyCharacterExpression(characterId, token);
                
                // Show dialogue using DialogueService
                await ShowDialogueAsync(dialogueService, dialogueLine, token);
                
                Debug.Log($"[SayCommand] Successfully displayed dialogue: {characterId}: {text.Value}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SayCommand] Error executing command: {ex.Message}");
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
            
            // Handle direct character name or empty (narrator)
            return charValue ?? string.Empty;
        }
        
        private DialogueLine CreateDialogueLine(string characterId)
        {
            var dialogueType = string.IsNullOrEmpty(characterId) ? DialogueType.Narration : DialogueType.Speech;
            var line = new DialogueLine(characterId, text.Value, voice.Value, dialogueType);
            
            // Apply metadata from parameters
            if (speed.HasValue)
                line.Metadata.CustomTypingSpeed = (float)speed.Value;
            
            if (preDelay.HasValue)
                line.Metadata.PreDelay = (float)preDelay.Value;
                
            if (postDelay.HasValue)
                line.Metadata.PostDelay = (float)postDelay.Value;
            
            if (skipable.HasValue)
                line.Metadata.CanSkip = skipable.Value;
            
            if (!string.IsNullOrEmpty(color.Value))
                line.Metadata.TextColor = ParseColor(color.Value);
                
            if (!string.IsNullOrEmpty(nameColor.Value))
                line.Metadata.NameColor = ParseColor(nameColor.Value);
            
            if (!string.IsNullOrEmpty(emotion.Value))
                line.Metadata.Emotion = emotion.Value;
                
            if (!string.IsNullOrEmpty(pose.Value))
                line.Metadata.Pose = pose.Value;
                
            if (!string.IsNullOrEmpty(position.Value))
                line.Metadata.Position = position.Value;
            
            if (voiceVolume.HasValue)
                line.Metadata.VoiceVolume = Mathf.Clamp01((float)voiceVolume.Value);
            
            return line;
        }
        
        private async UniTask ApplyCharacterExpression(string characterId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(characterId))
                return;
            
            // Get actor service for character expression changes
            var actorService = Engine.GetService<IActorService>();
            if (actorService == null)
                return;
            
            var actor = actorService.GetActor(characterId);
            if (actor == null)
                return;
            
            // Apply emotion change
            if (!string.IsNullOrEmpty(emotion.Value))
            {
                // This would integrate with your existing actor emotion system
                Debug.Log($"[SayCommand] Setting emotion for {characterId}: {emotion.Value}");
            }
            
            // Apply pose change
            if (!string.IsNullOrEmpty(pose.Value))
            {
                // This would integrate with your existing actor pose system
                Debug.Log($"[SayCommand] Setting pose for {characterId}: {pose.Value}");
            }
            
            // Apply position change
            if (!string.IsNullOrEmpty(position.Value))
            {
                // This would integrate with your existing actor positioning
                Debug.Log($"[SayCommand] Setting position for {characterId}: {position.Value}");
            }
            
            await UniTask.Yield();
        }
        
        private async UniTask ShowDialogueAsync(IDialogueService dialogueService, DialogueLine dialogueLine, CancellationToken token)
        {
            // Handle auto-advance override
            if (autoAdvance.HasValue)
            {
                var delay = autoAdvanceDelay.HasValue ? (float)autoAdvanceDelay.Value : 2f;
                dialogueService.SetAutoAdvance(autoAdvance.Value, delay);
            }
            
            // Show dialogue based on type
            if (dialogueLine.Type == DialogueType.Narration)
            {
                await dialogueService.ShowNarrationAsync(dialogueLine.Text, token);
            }
            else
            {
                await dialogueService.ShowDialogueAsync(
                    dialogueLine.SpeakerId, 
                    dialogueLine.Text, 
                    dialogueLine.VoiceId, 
                    token
                );
            }
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
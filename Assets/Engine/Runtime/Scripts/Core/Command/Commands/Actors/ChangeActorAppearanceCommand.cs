using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to change an actor's appearance with smooth transitions
    /// Supports both character and background actors with type-safe parameters
    /// 
    /// Usage examples:
    /// @char Alice expression:Happy duration:0.5
    /// @char Alice expression:Surprised pose:Pointing outfit:1 duration:1.0
    /// @char Alice look:left color:red duration:0.3
    /// @bg classroom variant:2 transition:slide duration:2.0
    /// @appearance Alice.Happy.Standing.01 duration:1.5
    /// </summary>
    [Serializable]
    [CommandAlias("appearance")]
    public class ChangeActorAppearanceCommand : Command
    {
        [Header("Actor Identification")]
        public StringParameter actorId = new StringParameter();
        
        [Header("Character Appearance")]
        public StringParameter expression = new StringParameter();
        public StringParameter pose = new StringParameter();
        public IntegerParameter outfit = new IntegerParameter();
        public StringParameter lookDirection = new StringParameter(); // "left", "right", "center", "up", "down"
        public StringParameter characterColor = new StringParameter();
        
        [Header("Background Appearance")]
        public StringParameter location = new StringParameter();
        public IntegerParameter variant = new IntegerParameter();
        public StringParameter backgroundType = new StringParameter();
        public StringParameter transitionType = new StringParameter(); // "fade", "slide", "zoom", "wipe"
        
        [Header("Animation")]
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter easing = new StringParameter();
        
        [Header("Special Actions")]
        public StringParameter speak = new StringParameter(); // Make character speak
        public StringParameter emote = new StringParameter(); // Temporary emote expression
        public DecimalParameter emoteIntensity = new DecimalParameter(); // Emote intensity (0-1)
        public StringParameter react = new StringParameter(); // Physical reaction type
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[ChangeActorAppearanceCommand] ActorService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(actorId.Value))
                {
                    Debug.LogError("[ChangeActorAppearanceCommand] Actor ID is required");
                    return;
                }
                
                // Get existing actor
                var actor = actorService.GetActor(actorId.Value);
                if (actor == null)
                {
                    Debug.LogError($"[ChangeActorAppearanceCommand] Actor not found: {actorId.Value}");
                    return;
                }
                
                var animationDuration = duration.HasValue ? (float)duration.Value : 0.5f;
                
                // Handle special actions first
                await HandleSpecialActions(actor, token);
                
                // Apply appearance changes based on actor type
                if (actor is ICharacterActor character)
                {
                    await ChangeCharacterAppearance(character, animationDuration, token);
                }
                else if (actor is IBackgroundActor background)
                {
                    await ChangeBackgroundAppearance(background, animationDuration, token);
                }
                
                Debug.Log($"[ChangeActorAppearanceCommand] Successfully changed appearance for: {actorId.Value}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChangeActorAppearanceCommand] Error executing command: {ex.Message}");
            }
        }
        
        private async UniTask HandleSpecialActions(IActor actor, CancellationToken token)
        {
            if (actor is ICharacterActor character)
            {
                // Handle speaking
                if (!string.IsNullOrEmpty(speak.Value))
                {
                    await character.SpeakAsync(speak.Value, token);
                }
                
                // Handle emoting
                if (!string.IsNullOrEmpty(emote.Value))
                {
                    if (Enum.TryParse<CharacterEmotion>(emote.Value, true, out var emotion))
                    {
                        var emoteDuration = duration.HasValue ? (float)duration.Value : 2.0f;
                        await character.EmoteAsync(emotion, emoteDuration, token);
                    }
                }
                
                // Handle reactions
                if (!string.IsNullOrEmpty(react.Value))
                {
                    var intensity = emoteIntensity.HasValue ? (float)emoteIntensity.Value : 1.0f;
                    
                    // Try parsing as reaction type first
                    if (Enum.TryParse<CharacterReactionType>(react.Value, true, out var reactionType))
                    {
                        await character.ReactAsync(reactionType, CharacterEmotion.Neutral, intensity, token);
                    }
                    // Try parsing as emotion-based reaction
                    else if (Enum.TryParse<CharacterEmotion>(react.Value, true, out var reactionEmotion))
                    {
                        await character.ReactAsync(reactionEmotion, intensity, token);
                    }
                }
            }
        }
        
        private async UniTask ChangeCharacterAppearance(ICharacterActor character, float animationDuration, CancellationToken token)
        {
            // Change expression
            if (!string.IsNullOrEmpty(expression.Value))
            {

            }

            //// Change pose
            //if (!string.IsNullOrEmpty(pose.Value))
            //{
            //    if (Enum.TryParse<CharacterPose>(pose.Value, true, out var characterPose))
            //    {
            //        await character.ChangePoseAsync(characterPose, animationDuration, token);
            //    }
            //    else
            //    {
            //        Debug.LogWarning($"[ChangeActorAppearanceCommand] Invalid pose: {pose.Value}");
            //    }
            //}

            //// Change outfit
            //if (outfit.HasValue)
            //{
            //    await character.ChangeOutfitAsync((int)outfit.Value, animationDuration, token);
            //}

            //// Change look direction
            //if (!string.IsNullOrEmpty(lookDirection.Value))
            //{
            //    var direction = ParseLookDirection(lookDirection.Value);
            //    await character.ChangeLookDirectionAsync(direction, animationDuration * 0.5f, token);
            //}

            //// Change character color
            if (!string.IsNullOrEmpty(characterColor.Value))
            {
                var color = ParseColor(characterColor.Value);
                character.CharacterColor = color;
            }

            // TODO: Implement expression and pose changes
            var newAppearance = new CharacterAppearance
            {
                Expression = CharacterEmotion.Happy
                //Expression = expression.Value,
                //Pose = pose.Value,
                //OutfitIndex = outfit.HasValue ? (int)outfit.Value : -1,
                //LookDirection = ParseLookDirection(lookDirection.Value),
                //Color = ParseColor(characterColor.Value)
            };

            await character.ChangeAppearanceAsync(newAppearance, animationDuration, token);
        }
        
        private async UniTask ChangeBackgroundAppearance(IBackgroundActor background, float animationDuration, CancellationToken token)
        {
            bool appearanceChanged = false;
            var currentAppearance = background.Appearance;
            var newAppearance = currentAppearance;
            
            // Change location
            if (!string.IsNullOrEmpty(location.Value))
            {
                if (Enum.TryParse<SceneLocation>(location.Value, true, out var loc))
                {
                    newAppearance = newAppearance.WithLocation(loc);
                    appearanceChanged = true;
                }
                else
                {
                    Debug.LogWarning($"[ChangeActorAppearanceCommand] Invalid location: {location.Value}");
                }
            }
            
            // Change variant
            if (variant.HasValue)
            {
                newAppearance = newAppearance.WithVariant((int)variant.Value);
                appearanceChanged = true;
            }
            
            // Change background type
            if (!string.IsNullOrEmpty(backgroundType.Value))
            {
                if (Enum.TryParse<BackgroundType>(backgroundType.Value, true, out var bgType))
                {
                    newAppearance = newAppearance.WithType(bgType);
                    appearanceChanged = true;
                }
                else
                {
                    Debug.LogWarning($"[ChangeActorAppearanceCommand] Invalid background type: {backgroundType.Value}");
                }
            }
            
            // Apply appearance change with transition
            if (appearanceChanged)
            {
                var transition = ParseTransitionType(transitionType.Value);
                await background.TransitionToAsync(newAppearance, transition, animationDuration, token);
            }
        }
        
        private CharacterLookDirection ParseLookDirection(string direction)
        {
            return direction.ToLower() switch
            {
                "left" => CharacterLookDirection.Left,
                "right" => CharacterLookDirection.Right,
                "center" => CharacterLookDirection.Center,
                "up" => CharacterLookDirection.Up,
                "down" => CharacterLookDirection.Down,
                "up-left" or "upleft" => CharacterLookDirection.UpLeft,
                "up-right" or "upright" => CharacterLookDirection.UpRight,
                "down-left" or "downleft" => CharacterLookDirection.DownLeft,
                "down-right" or "downright" => CharacterLookDirection.DownRight,
                _ => CharacterLookDirection.Center
            };
        }
        
        private SceneTransitionType ParseTransitionType(string transition)
        {
            if (string.IsNullOrEmpty(transition))
                return SceneTransitionType.Fade;
                
            return transition.ToLower() switch
            {
                "fade" => SceneTransitionType.Fade,
                "slide" => SceneTransitionType.Slide,
                "zoom" => SceneTransitionType.Zoom,
                "wipe" => SceneTransitionType.Wipe,
                "dissolve" => SceneTransitionType.Dissolve,
                "rotate" => SceneTransitionType.Rotate,
                "none" or "instant" => SceneTransitionType.None,
                "custom" => SceneTransitionType.Custom,
                _ => SceneTransitionType.Fade
            };
        }
        
        private Color ParseColor(string colorString)
        {
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
                case "orange": return new Color(1f, 0.5f, 0f);
                case "purple": return new Color(0.5f, 0f, 1f);
                case "pink": return new Color(1f, 0.75f, 0.8f);
                case "brown": return new Color(0.6f, 0.3f, 0f);
            }
            
            // Handle hex colors (#RRGGBB or #RRGGBBAA)
            if (colorString.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(colorString, out var hexColor))
                {
                    return hexColor;
                }
            }
            
            // Handle RGBA format (r,g,b,a)
            if (colorString.Contains(","))
            {
                var parts = colorString.Split(',');
                if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[0], out var r) && 
                        float.TryParse(parts[1], out var g) && 
                        float.TryParse(parts[2], out var b))
                    {
                        var a = parts.Length > 3 && float.TryParse(parts[3], out var aVal) ? aVal : 1f;
                        return new Color(r, g, b, a);
                    }
                }
            }
            
            Debug.LogWarning($"[ChangeActorAppearanceCommand] Could not parse color: {colorString}, using white");
            return Color.white;
        }
    }
}
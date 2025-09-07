using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to show an actor with specified appearance and transition
    /// Supports both character and background actors with type-safe parameters
    /// 
    /// Usage examples:
    /// @show char:Alice expression:Happy pos:center fade:1.0
    /// @show bg:classroom variant:1 transition:fade duration:2.0
    /// @show Alice.Happy.Standing.01 pos:left duration:1.5
    /// </summary>
    [Serializable]
    [CommandAlias("show")]
    [CommandMeta(
        timeout: 15f,
        maxRetries: 2,
        category: CommandCategory.ActorManagement,
        retryStrategy: RetryStrategy.Linear,
        fallback: FallbackAction.Continue,
        preloadResources: true,
        expectedDuration: 2f)]
    public class ShowActorCommand : Command
    {
        [Header("Actor Identification")]
        [RequiredParameter]
        [ParameterAlias(ParameterAliases.Actor)]
        [NotEmpty("Actor ID cannot be empty")]
        public ActorIdParameter actorId = new ActorIdParameter();
        [ParameterAlias(ParameterAliases.Type)]
        [DefaultValue("character")]
        public StringParameter actorType = new StringParameter(); // "char", "bg", "character", "background"
        
        [Header("Character Appearance")]
        public StringParameter expression = new StringParameter();
        public StringParameter pose = new StringParameter();
        public IntegerParameter outfit = new IntegerParameter();
        
        [Header("Background Appearance")]
        public StringParameter location = new StringParameter();
        public IntegerParameter variant = new IntegerParameter();
        public StringParameter backgroundType = new StringParameter();
        
        [Header("Positioning")]
        [ParameterAlias(ParameterAliases.Position)]
        [DefaultValue("center")]
        public Vector3Parameter position = new Vector3Parameter(); // "left", "center", "right" or Vector3
        [DefaultValue("0,0,0")]
        public Vector3Parameter rotation = new Vector3Parameter(); // Euler angles as Vector3
        [DefaultValue("1,1,1")]
        public Vector3Parameter scale = new Vector3Parameter(); // Vector3 scale
        
        [Header("Animation")]
        [ParameterAlias(ParameterAliases.Duration)]
        [Range(0.0, 10.0)]
        [DefaultValue(1.0f)]
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter transition = new StringParameter(); // "fade", "slide", "zoom", "instant"
        public StringParameter easing = new StringParameter(); // DOTween easing types
        
        [Header("Visual Properties")]
        [Range(0.0, 1.0)]
        [DefaultValue(1.0f)]
        public DecimalParameter alpha = new DecimalParameter();
        [DefaultValue("white")]
        public ColorParameter tintColor = new ColorParameter(); // Color name or hex
        [DefaultValue(0)]
        public IntegerParameter sortingOrder = new IntegerParameter();
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[ShowActorCommand] ActorService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(actorId.Value))
                {
                    Debug.LogError("[ShowActorCommand] Actor ID is required");
                    return;
                }
                
                // Parse actor type and ID
                var (parsedActorType, parsedId) = ParseActorIdentifier();
                
                // Create or get existing actor
                IActor actor = await GetOrCreateActor(actorService, parsedActorType, parsedId, token);
                if (actor == null)
                {
                    Debug.LogError($"[ShowActorCommand] Failed to create/get actor: {parsedId}");
                    return;
                }
                
                // Apply appearance if specified
                await ApplyAppearance(actor, token);
                
                // Apply positioning
                ApplyPositioning(actor);
                
                // Apply visual properties
                ApplyVisualProperties(actor);
                
                // Show with transition
                var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                await ShowActorWithTransition(actor, animationDuration, token);
                
                Debug.Log($"[ShowActorCommand] Successfully showed {parsedActorType} actor: {parsedId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShowActorCommand] Error executing command: {ex.Message}");
            }
        }
        
        private (ActorType actorType, string id) ParseActorIdentifier()
        {
            // Handle composite ID like "char:Alice" or "Alice.Happy.Standing.01"
            var id = actorId.Value;
            var type = actorType.Value?.ToLower();
            
            // Parse composite format (char:Alice)
            if (id.Contains(":"))
            {
                var parts = id.Split(':');
                if (parts.Length == 2)
                {
                    type = parts[0].ToLower();
                    id = parts[1];
                }
            }
            
            // Parse dot notation (Alice.Happy.Standing.01)
            if (id.Contains("."))
            {
                var parts = id.Split('.');
                id = parts[0]; // First part is always the actor name
            }
            
            // Determine actor type
            ActorType actorTypeEnum = type switch
            {
                "char" or "character" => ActorType.Character,
                "bg" or "background" => ActorType.Background,
                "prop" => ActorType.Prop,
                "effect" => ActorType.Effect,
                "ui" => ActorType.UI,
                _ => ActorType.Character // Default to character
            };
            
            return (actorTypeEnum, id);
        }
        
        private async UniTask<IActor> GetOrCreateActor(IActorService actorService, ActorType actorType, string id, CancellationToken token)
        {
            // Try to get existing actor first
            var existingActor = actorService.GetActor(id);
            if (existingActor != null)
            {
                return existingActor;
            }
            var position = Vector3.zero; // Default position if not specified
            // Create new actor based on type
            if (actorType == ActorType.Character)
            {
                var appearance = CreateCharacterAppearance();
                return await actorService.CreateCharacterActorAsync(id, appearance, position, token);
            }
            else if (actorType == ActorType.Background)
            {
                var appearance = CreateBackgroundAppearance();
                return await actorService.CreateBackgroundActorAsync(id, appearance, position, token);
            }
            
            Debug.LogWarning($"[ShowActorCommand] Unsupported actor type: {actorType}");
            return null;
        }
        
        private CharacterAppearance CreateCharacterAppearance()
        {
            // Parse expression
            var expr = CharacterEmotion.Neutral;
            if (!string.IsNullOrEmpty(expression.Value))
            {
                if (Enum.TryParse<CharacterEmotion>(expression.Value, true, out var parsedExpr))
                    expr = parsedExpr;
            }
            
            // Parse pose
            var characterPose = CharacterPose.Standing;
            if (!string.IsNullOrEmpty(pose.Value))
            {
                if (Enum.TryParse<CharacterPose>(pose.Value, true, out var parsedPose))
                    characterPose = parsedPose;
            }
            
            // Get outfit ID
            var outfitId = outfit.HasValue ? (int)outfit.Value : 0;
            
            return new CharacterAppearance(expr, characterPose, outfitId);
        }
        
        private BackgroundAppearance CreateBackgroundAppearance()
        {
            // Parse background type
            var bgType = BackgroundType.Scene;
            if (!string.IsNullOrEmpty(backgroundType.Value))
            {
                if (Enum.TryParse<BackgroundType>(backgroundType.Value, true, out var parsedType))
                    bgType = parsedType;
            }
            
            // Parse location
            var loc = SceneLocation.Classroom;
            if (!string.IsNullOrEmpty(location.Value))
            {
                if (Enum.TryParse<SceneLocation>(location.Value, true, out var parsedLoc))
                    loc = parsedLoc;
            }
            
            // Get variant ID
            var variantId = variant.HasValue ? (int)variant.Value : 0;
            
            return new BackgroundAppearance(bgType, loc, variantId);
        }
        
        private async UniTask ApplyAppearance(IActor actor, CancellationToken token)
        {
            if (actor is ICharacterActor character)
            {
                var appearance = CreateCharacterAppearance();
                await character.ChangeAppearanceAsync(appearance, 0.1f, token);
            }
            else if (actor is IBackgroundActor background)
            {
                var appearance = CreateBackgroundAppearance();
                await background.ChangeAppearanceAsync(appearance, 0.1f, token);
            }
        }
        
        private void ApplyPositioning(IActor actor)
        {

            if (position.HasValue)
            {
                actor.Position = position.Value;
            }
            else
            {
                actor.Position = Vector3.zero;
            }

            if (rotation.HasValue)
            {
                actor.Rotation = Quaternion.Euler(rotation.Value.x, rotation.Value.y, rotation.Value.z);
            }
            else
            {
                actor.Rotation = Quaternion.identity;
            }

            if (scale.HasValue)
            {
                actor.Scale = scale.Value;
            }
            else
            {
                actor.Scale = Vector3.one;
            }

        }
        
        private void ApplyVisualProperties(IActor actor)
        {
            // Apply alpha
            if (alpha.HasValue)
            {
                actor.Alpha = Mathf.Clamp01((float)alpha.Value);
            }

            // Apply tint color
            if (tintColor.HasValue)
                actor.TintColor = tintColor.Value;


            // Apply sorting order
            if (sortingOrder.HasValue)
            {
                actor.SortingOrder = (int)sortingOrder.Value;
            }
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
            
            return Color.white;
        }
        
        private async UniTask ShowActorWithTransition(IActor actor, float duration, CancellationToken token)
        {
            // Parse transition type
            var transitionType = transition.Value?.ToLower() ?? "fade";
            
            // Parse easing
            var ease = DG.Tweening.Ease.OutQuad; // Default easing
            if (!string.IsNullOrEmpty(easing.Value))
            {
                if (Enum.TryParse<DG.Tweening.Ease>(easing.Value, true, out var parsedEase))
                    ease = parsedEase;
            }
            
            // Apply transition
            switch (transitionType)
            {
                case "instant":
                    actor.Visible = true;
                    break;
                    
                case "fade":
                default:
                    await actor.ChangeVisibilityAsync(true, duration, ease, token);
                    break;
                    
                case "slide":
                    // Slide in from off-screen
                    var originalPos = actor.Position;
                    actor.Position = originalPos + Vector3.left * 10f; // Start off-screen
                    actor.Visible = true;
                    await actor.ChangePositionAsync(originalPos, duration, ease, token);
                    break;
                    
                case "zoom":
                    // Zoom in from small scale
                    var originalScale = actor.Scale;
                    actor.Scale = Vector3.zero;
                    actor.Visible = true;
                    await actor.ChangeScaleAsync(originalScale, duration, ease, token);
                    break;
            }
        }
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using Sinkii09.Engine.Services;
using ZLinq;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to move, rotate, and scale actors with smooth animations
    /// Supports both individual and batch operations with DOTween integration
    /// 
    /// Usage examples:
    /// @move Alice pos:left duration:1.5
    /// @move Alice pos:2,1,0 rotation:0,0,15 scale:1.2 duration:2.0 easing:InOutQuad
    /// @move Alice,Bob pos:left,right duration:1.0
    /// @move all pos:center duration:2.0
    /// @animate Alice shake:1.0 bounce:0.5 duration:1.0
    /// </summary>
    [Serializable]
    [CommandAlias("move")]
    public class MoveActorCommand : Command
    {
        [Header("Actor Identification")]
        public StringParameter actorId = new StringParameter(); // Single actor or comma-separated list
        
        [Header("Transform Properties")]
        public StringParameter position = new StringParameter(); // "left", "center", "right" or "x,y,z"
        public StringParameter rotation = new StringParameter(); // "x,y,z" euler angles
        public StringParameter scale = new StringParameter(); // Single float or "x,y,z"
        
        [Header("Animation")]
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter easing = new StringParameter(); // DOTween easing types
        public DecimalParameter delay = new DecimalParameter(); // Animation delay
        
        [Header("Special Animations")]
        public DecimalParameter shake = new DecimalParameter(); // Shake strength
        public DecimalParameter bounce = new DecimalParameter(); // Bounce strength
        public DecimalParameter punch = new DecimalParameter(); // Punch effect strength
        public StringParameter loop = new StringParameter(); // "restart", "yoyo", "incremental"
        public IntegerParameter loopCount = new IntegerParameter(); // Number of loops (-1 for infinite)
        
        [Header("Visual Properties")]
        public DecimalParameter alpha = new DecimalParameter();
        public StringParameter tintColor = new StringParameter();
        public IntegerParameter sortingOrder = new IntegerParameter();
        
        [Header("Relative Movement")]
        public BooleanParameter relative = new BooleanParameter(); // Move relative to current position
        public StringParameter offsetFrom = new StringParameter(); // "current", "world", "screen"
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[MoveActorCommand] ActorService not found");
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(actorId.Value))
                {
                    Debug.LogError("[MoveActorCommand] Actor ID is required");
                    return;
                }
                
                // Parse actor IDs (supports batch operations)
                var actorIds = ParseActorIds(actorId.Value);
                var actors = await GetActors(actorService, actorIds);
                
                if (actors.Count == 0)
                {
                    Debug.LogError($"[MoveActorCommand] No valid actors found for: {actorId.Value}");
                    return;
                }
                
                // Parse animation parameters
                var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                var animationDelay = delay.HasValue ? (float)delay.Value : 0f;
                var ease = ParseEasing(easing.Value);
                
                // Execute animations for all actors
                var animationTasks = new System.Collections.Generic.List<UniTask>();
                
                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    var actorDelay = animationDelay + (i * 0.1f); // Stagger animations slightly
                    animationTasks.Add(AnimateActor(actor, animationDuration, actorDelay, ease, i, actors.Count, token));
                }
                
                // Wait for all animations to complete
                await UniTask.WhenAll(animationTasks);
                
                Debug.Log($"[MoveActorCommand] Successfully animated {actors.Count} actor(s)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoveActorCommand] Error executing command: {ex.Message}");
            }
        }
        
        private System.Collections.Generic.List<string> ParseActorIds(string actorIdString)
        {
            var ids = new System.Collections.Generic.List<string>();
            
            // Handle special cases
            if (actorIdString.ToLower() == "all")
            {
                var actorService = Engine.GetService<IActorService>();
                if (actorService != null)
                {
                    return actorService.GetActorIds().AsValueEnumerable().ToList();
                }
                Debug.LogError("[MoveActorCommand] ActorService not available for 'all' actors");
                return ids;
            }
            
            // Parse comma-separated list
            if (actorIdString.Contains(","))
            {
                var parts = actorIdString.Split(',');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        ids.Add(trimmed);
                }
            }
            else
            {
                ids.Add(actorIdString.Trim());
            }
            
            return ids;
        }
        
        private UniTask<System.Collections.Generic.List<IActor>> GetActors(IActorService actorService, System.Collections.Generic.List<string> actorIds)
        {
            var actors = new System.Collections.Generic.List<IActor>();
            
            foreach (var id in actorIds)
            {
                var actor = actorService.GetActor(id);
                if (actor != null)
                {
                    actors.Add(actor);
                }
                else
                {
                    Debug.LogWarning($"[MoveActorCommand] Actor not found: {id}");
                }
            }
            
            return UniTask.FromResult(actors);
        }
        
        private async UniTask AnimateActor(IActor actor, float animationDuration, float animationDelay, Ease ease, int index, int totalActors, CancellationToken token)
        {
            try
            {
                // Apply delay
                if (animationDelay > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(animationDelay), cancellationToken: token);
                }
                
                // Only create sequence if special animations are needed
                Sequence sequence = null;
                if (HasSpecialAnimations())
                {
                    sequence = DOTween.Sequence();
                    ApplySpecialAnimations(sequence, actor, animationDuration, ease);
                }
                
                // Apply transform changes
                await ApplyTransformChanges(actor, animationDuration, ease, index, totalActors, token);
                
                // Apply visual property changes
                ApplyVisualPropertyChanges(actor);
                
                // Execute sequence if it was created
                if (sequence != null)
                {
                    await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoveActorCommand] Error animating actor {actor.Id}: {ex.Message}");
            }
        }
        
        private bool HasSpecialAnimations()
        {
            return (shake.HasValue && shake.Value > 0) ||
                   (bounce.HasValue && bounce.Value > 0) ||
                   (punch.HasValue && punch.Value > 0) ||
                   !string.IsNullOrEmpty(loop.Value);
        }
        
        private void ApplySpecialAnimations(Sequence sequence, IActor actor, float animationDuration, Ease ease)
        {
            var transform = actor.Transform;
            
            // Shake animation
            if (shake.HasValue && shake.Value > 0)
            {
                var shakeStrength = (float)shake.Value;
                sequence.Join(transform.DOShakePosition(animationDuration, shakeStrength).SetEase(ease));
            }
            
            // Bounce animation
            if (bounce.HasValue && bounce.Value > 0)
            {
                var bounceStrength = (float)bounce.Value;
                sequence.Join(transform.DOPunchPosition(Vector3.up * bounceStrength, animationDuration).SetEase(ease));
            }
            
            // Punch animation
            if (punch.HasValue && punch.Value > 0)
            {
                var punchStrength = (float)punch.Value;
                sequence.Join(transform.DOPunchScale(Vector3.one * punchStrength, animationDuration).SetEase(ease));
            }
            
            // Apply loop settings
            if (!string.IsNullOrEmpty(loop.Value))
            {
                var loopType = ParseLoopType(loop.Value);
                var loops = loopCount.HasValue ? (int)loopCount.Value : 1;
                sequence.SetLoops(loops, loopType);
            }
        }
        
        private async UniTask ApplyTransformChanges(IActor actor, float animationDuration, Ease ease, int index, int totalActors, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            // Position change
            if (!string.IsNullOrEmpty(position.Value))
            {
                var targetPosition = ParsePosition(position.Value, actor, index, totalActors);
                tasks.Add(actor.ChangePositionAsync(targetPosition, animationDuration, ease, token));
            }
            
            // Rotation change
            if (!string.IsNullOrEmpty(rotation.Value))
            {
                var targetRotation = ParseRotation(rotation.Value);
                tasks.Add(actor.ChangeRotationAsync(targetRotation, animationDuration, ease, token));
            }
            
            // Scale change
            if (!string.IsNullOrEmpty(scale.Value))
            {
                var targetScale = ParseScale(scale.Value);
                tasks.Add(actor.ChangeScaleAsync(targetScale, animationDuration, ease, token));
            }
            
            // Alpha change
            if (alpha.HasValue)
            {
                var targetAlpha = Mathf.Clamp01((float)alpha.Value);
                tasks.Add(actor.ChangeAlphaAsync(targetAlpha, animationDuration, ease, token));
            }
            
            // Wait for all transform changes to complete
            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }
        
        private Vector3 ParsePosition(string positionString, IActor actor, int index, int totalActors)
        {
            var basePosition = Vector3.zero;
            
            // Handle Naninovel-style percentage positioning (e.g., "45,10" = 45% from left, 10% from bottom)
            if (positionString.Contains(","))
            {
                var parts = positionString.Split(',');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
                    {
                        // Check if values are percentages (0-100) or world coordinates
                        if (x <= 100 && y <= 100 && x >= 0 && y >= 0)
                        {
                            // Naninovel-style percentage positioning
                            basePosition = ConvertPercentageToWorldPosition(x, y);
                        }
                        else
                        {
                            // World coordinates
                            var z = parts.Length > 2 && float.TryParse(parts[2], out var zVal) ? zVal : 0f;
                            basePosition = new Vector3(x, y, z);
                        }
                    }
                }
            }
            else
            {
                // Handle named positions (converted to percentages)
                switch (positionString.ToLower())
                {
                    case "left": basePosition = ConvertPercentageToWorldPosition(15, 0); break;
                    case "center": basePosition = ConvertPercentageToWorldPosition(50, 0); break;
                    case "right": basePosition = ConvertPercentageToWorldPosition(85, 0); break;
                    case "farleft": case "far-left": basePosition = ConvertPercentageToWorldPosition(5, 0); break;
                    case "farright": case "far-right": basePosition = ConvertPercentageToWorldPosition(95, 0); break;
                    case "top": basePosition = ConvertPercentageToWorldPosition(50, 80); break;
                    case "bottom": basePosition = ConvertPercentageToWorldPosition(50, 10); break;
                    default:
                        // Try to parse as single world coordinate
                        if (float.TryParse(positionString, out var singleVal))
                        {
                            basePosition = new Vector3(singleVal, 0f, 0f);
                        }
                        break;
                }
            }
            
            // Handle relative positioning
            if (relative.HasValue && relative.Value)
            {
                basePosition += actor.Position;
            }
            
            // Handle batch positioning (spread actors horizontally)
            if (totalActors > 1)
            {
                var spacing = 10f; // 10% spacing between actors
                var totalWidth = (totalActors - 1) * spacing;
                var startPercentage = 50 - (totalWidth * 0.5f); // Center the group
                var actorPercentage = startPercentage + (index * spacing);
                basePosition = ConvertPercentageToWorldPosition(actorPercentage, GetYPercentageFromPosition(basePosition));
            }
            
            return basePosition;
        }
        
        /// <summary>
        /// Convert Naninovel-style percentage position to Unity world coordinates
        /// x: 0-100 percentage from left edge of screen
        /// y: 0-100 percentage from bottom edge of screen
        /// </summary>
        private Vector3 ConvertPercentageToWorldPosition(float xPercent, float yPercent)
        {
            // Get screen bounds in world space
            var camera = Camera.main ?? Camera.current;
            if (camera == null)
            {
                Debug.LogWarning("[MoveActorCommand] No camera found, using default positioning");
                return new Vector3(xPercent * 0.1f - 5f, yPercent * 0.1f - 5f, 0f);
            }
            
            // Calculate world bounds
            var screenHeight = camera.orthographicSize * 2f;
            var screenWidth = screenHeight * camera.aspect;
            
            // Convert percentages to world position
            var worldX = (xPercent / 100f) * screenWidth - (screenWidth * 0.5f);
            var worldY = (yPercent / 100f) * screenHeight - (screenHeight * 0.5f);
            
            return new Vector3(worldX, worldY, 0f);
        }
        
        /// <summary>
        /// Extract Y percentage from a world position (for batch positioning)
        /// </summary>
        private float GetYPercentageFromPosition(Vector3 worldPosition)
        {
            var camera = Camera.main ?? Camera.current;
            if (camera == null) return 0f;
            
            var screenHeight = camera.orthographicSize * 2f;
            return ((worldPosition.y + (screenHeight * 0.5f)) / screenHeight) * 100f;
        }
        
        private Quaternion ParseRotation(string rotationString)
        {
            // Parse Euler angles (x,y,z)
            if (rotationString.Contains(","))
            {
                var parts = rotationString.Split(',');
                if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[0], out var x) && 
                        float.TryParse(parts[1], out var y) && 
                        float.TryParse(parts[2], out var z))
                    {
                        return Quaternion.Euler(x, y, z);
                    }
                }
            }
            
            return Quaternion.identity;
        }
        
        private Vector3 ParseScale(string scaleString)
        {
            // Single float (uniform scale)
            if (float.TryParse(scaleString, out var uniformScale))
            {
                return Vector3.one * uniformScale;
            }
            
            // Vector3 format (x,y,z)
            if (scaleString.Contains(","))
            {
                var parts = scaleString.Split(',');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
                    {
                        var z = parts.Length > 2 && float.TryParse(parts[2], out var zVal) ? zVal : 1f;
                        return new Vector3(x, y, z);
                    }
                }
            }
            
            return Vector3.one;
        }
        
        private Ease ParseEasing(string easingString)
        {
            if (string.IsNullOrEmpty(easingString))
                return Ease.OutQuad;
                
            if (Enum.TryParse<Ease>(easingString, true, out var ease))
                return ease;
                
            // Handle common aliases
            return easingString.ToLower() switch
            {
                "linear" => Ease.Linear,
                "smooth" => Ease.InOutQuad,
                "fast" => Ease.OutQuart,
                "slow" => Ease.InQuart,
                "bounce" => Ease.OutBounce,
                "elastic" => Ease.OutElastic,
                "back" => Ease.OutBack,
                _ => Ease.OutQuad
            };
        }
        
        private LoopType ParseLoopType(string loopString)
        {
            return loopString.ToLower() switch
            {
                "restart" => LoopType.Restart,
                "yoyo" => LoopType.Yoyo,
                "incremental" => LoopType.Incremental,
                _ => LoopType.Restart
            };
        }
        
        private void ApplyVisualPropertyChanges(IActor actor)
        {
            // Apply tint color
            if (!string.IsNullOrEmpty(tintColor.Value))
            {
                var color = ParseColor(tintColor.Value);
                actor.TintColor = color;
            }
            
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
            
            // Handle hex colors
            if (colorString.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(colorString, out var hexColor))
                {
                    return hexColor;
                }
            }
            
            return Color.white;
        }
    }
}
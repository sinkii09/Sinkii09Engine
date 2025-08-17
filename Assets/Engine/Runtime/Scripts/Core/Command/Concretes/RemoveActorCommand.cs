using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command to remove actors with smooth exit transitions
    /// Supports both individual and batch removal operations
    /// 
    /// Usage examples:
    /// @remove Alice fade:1.0
    /// @remove Alice,Bob duration:0.5
    /// @remove all fade:2.0
    /// @remove * transition:slide duration:1.5
    /// @hide Alice duration:1.0 (alias for remove with fade)
    /// @clear (removes all actors instantly)
    /// </summary>
    [Serializable]
    [CommandAlias("remove")]
    public class RemoveActorCommand : Command
    {
        [Header("Actor Identification")]
        public StringParameter actorId = new StringParameter(); // Single actor, comma-separated list, "all", or "*"
        
        [Header("Removal Options")]
        public StringParameter transition = new StringParameter(); // "fade", "slide", "zoom", "instant"
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter easing = new StringParameter();
        public DecimalParameter delay = new DecimalParameter(); // Delay before starting removal
        
        [Header("Transition Direction")]
        public StringParameter direction = new StringParameter(); // "left", "right", "up", "down" for slide/zoom
        public BooleanParameter staggered = new BooleanParameter(); // Stagger removal for multiple actors
        public DecimalParameter staggerDelay = new DecimalParameter(); // Delay between staggered removals
        
        [Header("Resource Management")]
        public BooleanParameter unloadResources = new BooleanParameter(); // Unload actor resources
        public BooleanParameter destroyGameObject = new BooleanParameter(); // Destroy Unity GameObject
        public BooleanParameter keepInMemory = new BooleanParameter(); // Keep actor in memory for quick re-show
        
        [Header("Special Effects")]
        public DecimalParameter fadeToAlpha = new DecimalParameter(); // Fade to specific alpha instead of 0
        public StringParameter fadeToColor = new StringParameter(); // Fade to specific color
        public BooleanParameter particles = new BooleanParameter(); // Show particle effect on removal
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[RemoveActorCommand] ActorService not found");
                    return;
                }
                
                // Handle special case for clear command (remove all instantly)
                if (string.IsNullOrEmpty(actorId.Value) || actorId.Value.ToLower() == "clear")
                {
                    await ClearAllActors(actorService, token);
                    return;
                }
                
                // Parse actor IDs
                var actorIds = ParseActorIds(actorId.Value, actorService);
                if (actorIds.Count == 0)
                {
                    Debug.LogError($"[RemoveActorCommand] No valid actors found for: {actorId.Value}");
                    return;
                }
                
                // Get actors to remove
                var actors = await GetActors(actorService, actorIds);
                if (actors.Count == 0)
                {
                    Debug.LogWarning($"[RemoveActorCommand] No actors found to remove for: {actorId.Value}");
                    return;
                }
                
                // Parse animation parameters
                var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                var animationDelay = delay.HasValue ? (float)delay.Value : 0f;
                var ease = ParseEasing(easing.Value);
                var transitionType = ParseTransitionType(transition.Value);
                
                // Execute removal animations
                if (staggered.HasValue && staggered.Value && actors.Count > 1)
                {
                    await RemoveActorsStaggered(actorService, actors, transitionType, animationDuration, animationDelay, ease, token);
                }
                else
                {
                    await RemoveActorsSimultaneous(actorService, actors, transitionType, animationDuration, animationDelay, ease, token);
                }
                
                Debug.Log($"[RemoveActorCommand] Successfully removed {actors.Count} actor(s)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoveActorCommand] Error executing command: {ex.Message}");
            }
        }
        
        private System.Collections.Generic.List<string> ParseActorIds(string actorIdString, IActorService actorService)
        {
            var ids = new System.Collections.Generic.List<string>();
            
            // Handle special cases
            var normalizedId = actorIdString.ToLower().Trim();
            if (normalizedId == "all" || normalizedId == "*")
            {
                // TODO: Get all active actor IDs from service
                // For now, return empty list and handle in GetActors
                ids.Add("*");
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
            
            // Handle wildcard
            if (actorIds.Count == 1 && actorIds[0] == "*")
            {
                // TODO: Implement GetAllActors() in ActorService
                Debug.LogWarning("[RemoveActorCommand] GetAllActors not yet implemented");
                return UniTask.FromResult(actors);
            }
            
            // Get individual actors
            foreach (var id in actorIds)
            {
                var actor = actorService.GetActor(id);
                if (actor != null)
                {
                    actors.Add(actor);
                }
                else
                {
                    Debug.LogWarning($"[RemoveActorCommand] Actor not found: {id}");
                }
            }
            
            return UniTask.FromResult(actors);
        }
        
        private async UniTask ClearAllActors(IActorService actorService, CancellationToken token)
        {
            try
            {
                // TODO: Implement ClearAllActors() in ActorService
                Debug.Log("[RemoveActorCommand] Clearing all actors instantly");
                await actorService.ClearSceneAsync(token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoveActorCommand] Error clearing all actors: {ex.Message}");
            }
        }
        
        private async UniTask RemoveActorsStaggered(IActorService actorService, System.Collections.Generic.List<IActor> actors, 
            TransitionType transitionType, float animationDuration, float baseDelay, Ease ease, CancellationToken token)
        {
            var stagger = staggerDelay.HasValue ? (float)staggerDelay.Value : 0.2f;
            
            var tasks = new System.Collections.Generic.List<UniTask>();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var delay = baseDelay + (i * stagger);
                tasks.Add(RemoveActor(actorService, actor, transitionType, animationDuration, delay, ease, token));
            }
            
            await UniTask.WhenAll(tasks);
        }
        
        private async UniTask RemoveActorsSimultaneous(IActorService actorService, System.Collections.Generic.List<IActor> actors, 
            TransitionType transitionType, float animationDuration, float baseDelay, Ease ease, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            foreach (var actor in actors)
            {
                tasks.Add(RemoveActor(actorService, actor, transitionType, animationDuration, baseDelay, ease, token));
            }
            
            await UniTask.WhenAll(tasks);
        }
        
        private async UniTask RemoveActor(IActorService actorService, IActor actor, TransitionType transitionType, 
            float animationDuration, float animationDelay, Ease ease, CancellationToken token)
        {
            try
            {
                // Apply delay
                if (animationDelay > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(animationDelay), cancellationToken: token);
                }
                
                // Show particle effects if requested
                if (particles.HasValue && particles.Value)
                {
                    await ShowRemovalParticles(actor, token);
                }
                
                // Execute removal transition
                await ExecuteRemovalTransition(actor, transitionType, animationDuration, ease, token);
                
                // Handle resource management
                await HandleResourceCleanup(actorService, actor, token);
                
                Debug.Log($"[RemoveActorCommand] Removed actor: {actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoveActorCommand] Error removing actor {actor.Id}: {ex.Message}");
            }
        }
        
        private async UniTask ExecuteRemovalTransition(IActor actor, TransitionType transitionType, 
            float animationDuration, Ease ease, CancellationToken token)
        {
            switch (transitionType)
            {
                case TransitionType.Instant:
                    actor.Visible = false;
                    break;
                    
                case TransitionType.Fade:
                    var targetAlpha = fadeToAlpha.HasValue ? (float)fadeToAlpha.Value : 0f;
                    await actor.ChangeAlphaAsync(targetAlpha, animationDuration, ease, token);
                    if (targetAlpha <= 0f)
                        actor.Visible = false;
                    break;
                    
                case TransitionType.Slide:
                    var slideDirection = ParseDirection(direction.Value);
                    var targetPosition = actor.Position + slideDirection * 10f; // Move off-screen
                    await actor.ChangePositionAsync(targetPosition, animationDuration, ease, token);
                    actor.Visible = false;
                    break;
                    
                case TransitionType.Zoom:
                    await actor.ChangeScaleAsync(Vector3.zero, animationDuration, ease, token);
                    actor.Visible = false;
                    break;
                    
                case TransitionType.FadeAndSlide:
                    var fadeTask = actor.ChangeAlphaAsync(0f, animationDuration, ease, token);
                    var slideDir = ParseDirection(direction.Value);
                    var slideTarget = actor.Position + slideDir * 5f;
                    var slideTask = actor.ChangePositionAsync(slideTarget, animationDuration, ease, token);
                    await UniTask.WhenAll(fadeTask, slideTask);
                    actor.Visible = false;
                    break;
                    
                case TransitionType.ColorFade:
                    if (!string.IsNullOrEmpty(fadeToColor.Value))
                    {
                        var targetColor = ParseColor(fadeToColor.Value);
                        await actor.ChangeTintColorAsync(targetColor, animationDuration, ease, token);
                    }
                    await actor.ChangeAlphaAsync(0f, animationDuration * 0.5f, ease, token);
                    actor.Visible = false;
                    break;
                    
                default:
                    // Default fade
                    await actor.ChangeAlphaAsync(0f, animationDuration, ease, token);
                    actor.Visible = false;
                    break;
            }
        }
        
        private async UniTask HandleResourceCleanup(IActorService actorService, IActor actor, CancellationToken token)
        {
            // Don't cleanup if keeping in memory
            if (keepInMemory.HasValue && keepInMemory.Value)
            {
                return;
            }
            
            // Unload resources if requested
            if (unloadResources.HasValue && unloadResources.Value)
            {
                await actor.UnloadResourcesAsync(token);
            }
            
            // Destroy GameObject if requested (default true)
            var shouldDestroy = !destroyGameObject.HasValue || destroyGameObject.Value;
            if (shouldDestroy)
            {
                await actorService.UnloadActorResourcesAsync(actor.Id, token);
            }
        }
        
        private async UniTask ShowRemovalParticles(IActor actor, CancellationToken token)
        {
            try
            {
                // TODO: Implement particle system for removal effects
                Debug.Log($"[RemoveActorCommand] Showing removal particles for {actor.Id}");
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoveActorCommand] Error showing particles: {ex.Message}");
            }
        }
        
        private TransitionType ParseTransitionType(string transitionString)
        {
            if (string.IsNullOrEmpty(transitionString))
                return TransitionType.Fade;
                
            return transitionString.ToLower() switch
            {
                "instant" or "none" => TransitionType.Instant,
                "fade" => TransitionType.Fade,
                "slide" => TransitionType.Slide,
                "zoom" => TransitionType.Zoom,
                "fade-slide" or "fadeslide" => TransitionType.FadeAndSlide,
                "color" or "color-fade" => TransitionType.ColorFade,
                _ => TransitionType.Fade
            };
        }
        
        private Vector3 ParseDirection(string directionString)
        {
            if (string.IsNullOrEmpty(directionString))
                return Vector3.left; // Default slide left
                
            return directionString.ToLower() switch
            {
                "left" => Vector3.left,
                "right" => Vector3.right,
                "up" => Vector3.up,
                "down" => Vector3.down,
                "up-left" => (Vector3.up + Vector3.left).normalized,
                "up-right" => (Vector3.up + Vector3.right).normalized,
                "down-left" => (Vector3.down + Vector3.left).normalized,
                "down-right" => (Vector3.down + Vector3.right).normalized,
                _ => Vector3.left
            };
        }
        
        private Ease ParseEasing(string easingString)
        {
            if (string.IsNullOrEmpty(easingString))
                return Ease.OutQuad;
                
            if (Enum.TryParse<Ease>(easingString, true, out var ease))
                return ease;
                
            return easingString.ToLower() switch
            {
                "linear" => Ease.Linear,
                "smooth" => Ease.InOutQuad,
                "fast" => Ease.OutQuart,
                "slow" => Ease.InQuart,
                _ => Ease.OutQuad
            };
        }
        
        private Color ParseColor(string colorString)
        {
            switch (colorString.ToLower())
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "red": return Color.red;
                case "transparent": return Color.clear;
                default:
                    if (colorString.StartsWith("#") && ColorUtility.TryParseHtmlString(colorString, out var color))
                        return color;
                    return Color.white;
            }
        }
        
        private enum TransitionType
        {
            Instant,
            Fade,
            Slide,
            Zoom,
            FadeAndSlide,
            ColorFade
        }
    }
}
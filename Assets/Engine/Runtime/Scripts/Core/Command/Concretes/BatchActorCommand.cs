using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using System.Linq;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command for performing batch operations on multiple actors simultaneously
    /// Supports complex multi-actor animations and state changes with synchronization
    /// 
    /// Usage examples:
    /// @batch Alice,Bob,Charlie expression:Happy duration:1.0
    /// @batch all pos:center fade:2.0 stagger:0.3
    /// @batch Alice.Happy.Standing,Bob.Sad.Sitting duration:1.5
    /// @sync Alice:left Bob:right Charlie:center duration:2.0
    /// @ensemble cast expression:Surprised reaction:Shock stagger:0.2
    /// </summary>
    [Serializable]
    [CommandAlias("batch")]
    public class BatchActorCommand : Command
    {
        [Header("Actor Selection")]
        public StringParameter actors = new StringParameter(); // Comma-separated actor list or "all"
        public StringParameter actorFilter = new StringParameter(); // Filter by type: "characters", "backgrounds", "visible", "hidden"
        
        [Header("Batch Operations")]
        public StringParameter operation = new StringParameter(); // "show", "hide", "move", "appearance", "animate"
        public StringParameter expressions = new StringParameter(); // Per-actor expressions (Alice:Happy,Bob:Sad)
        public StringParameter positions = new StringParameter(); // Per-actor positions (Alice:left,Bob:right)
        public StringParameter poses = new StringParameter(); // Per-actor poses
        
        [Header("Synchronization")]
        public BooleanParameter synchronized = new BooleanParameter(); // Execute simultaneously
        public DecimalParameter stagger = new DecimalParameter(); // Delay between each actor
        public StringParameter staggerOrder = new StringParameter(); // "sequence", "reverse", "random", "byDistance"
        public StringParameter waitFor = new StringParameter(); // Wait condition: "all", "first", "last", "none"
        
        [Header("Common Properties")]
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter transition = new StringParameter();
        public StringParameter easing = new StringParameter();
        public DecimalParameter alpha = new DecimalParameter();
        public StringParameter tintColor = new StringParameter();
        
        [Header("Formation and Layout")]
        public StringParameter formation = new StringParameter(); // "line", "circle", "arc", "grid", "custom"
        public StringParameter spacing = new StringParameter(); // Distance between actors
        public StringParameter alignment = new StringParameter(); // "center", "left", "right"
        public StringParameter direction = new StringParameter(); // Formation direction
        
        [Header("Complex Animations")]
        public StringParameter sequence = new StringParameter(); // Named animation sequence
        public BooleanParameter wave = new BooleanParameter(); // Wave-like animation
        public DecimalParameter waveAmplitude = new DecimalParameter();
        public DecimalParameter waveFrequency = new DecimalParameter();
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[BatchActorCommand] ActorService not found");
                    return;
                }
                
                // Get target actors
                var targetActors = await GetTargetActors(actorService);
                if (targetActors.Count == 0)
                {
                    Debug.LogWarning("[BatchActorCommand] No actors found for batch operation");
                    return;
                }
                
                // Parse operation parameters
                var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                var staggerDelay = stagger.HasValue ? (float)stagger.Value : 0f;
                
                // Execute batch operation
                var operationType = operation.Value?.ToLower() ?? "appearance";
                
                switch (operationType)
                {
                    case "show":
                        await ExecuteShowBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    case "hide":
                        await ExecuteHideBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    case "move":
                        await ExecuteMoveBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    case "appearance":
                        await ExecuteAppearanceBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    case "animate":
                        await ExecuteAnimationBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    case "formation":
                        await ExecuteFormationBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                        
                    default:
                        await ExecuteAppearanceBatch(targetActors, animationDuration, staggerDelay, token);
                        break;
                }
                
                Debug.Log($"[BatchActorCommand] Successfully executed batch operation on {targetActors.Count} actors");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BatchActorCommand] Error executing batch command: {ex.Message}");
            }
        }
        
        private async UniTask<System.Collections.Generic.List<IActor>> GetTargetActors(IActorService actorService)
        {
            var targetActors = new System.Collections.Generic.List<IActor>();
            
            // Handle actor filter
            if (!string.IsNullOrEmpty(actorFilter.Value))
            {
                targetActors = await GetFilteredActors(actorService, actorFilter.Value);
            }
            // Handle specific actor list
            else if (!string.IsNullOrEmpty(actors.Value))
            {
                var actorIds = ParseActorList(actors.Value);
                foreach (var id in actorIds)
                {
                    var actor = actorService.GetActor(id);
                    if (actor != null)
                        targetActors.Add(actor);
                }
            }
            
            // Apply stagger ordering
            if (!string.IsNullOrEmpty(staggerOrder.Value))
            {
                targetActors = ApplyStaggerOrder(targetActors, staggerOrder.Value);
            }
            
            return targetActors;
        }
        
        private async UniTask<System.Collections.Generic.List<IActor>> GetFilteredActors(IActorService actorService, string filter)
        {
            var actors = new System.Collections.Generic.List<IActor>();
            
            // TODO: Implement GetAllActors() and filtering in ActorService
            switch (filter.ToLower())
            {
                case "all":
                    // actors = await actorService.GetAllActorsAsync();
                    break;
                case "characters":
                    // actors = await actorService.GetActorsByTypeAsync(ActorType.Character);
                    break;
                case "backgrounds":
                    // actors = await actorService.GetActorsByTypeAsync(ActorType.Background);
                    break;
                case "visible":
                    // actors = await actorService.GetVisibleActorsAsync();
                    break;
                case "hidden":
                    // actors = await actorService.GetHiddenActorsAsync();
                    break;
            }
            
            Debug.LogWarning($"[BatchActorCommand] Actor filtering not yet implemented for: {filter}");
            return await UniTask.FromResult(actors);
        }
        
        private System.Collections.Generic.List<string> ParseActorList(string actorString)
        {
            var ids = new System.Collections.Generic.List<string>();
            
            if (actorString.Contains(","))
            {
                foreach (var part in actorString.Split(','))
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        ids.Add(trimmed);
                }
            }
            else
            {
                ids.Add(actorString.Trim());
            }
            
            return ids;
        }
        
        private System.Collections.Generic.List<IActor> ApplyStaggerOrder(System.Collections.Generic.List<IActor> actors, string orderType)
        {
            switch (orderType.ToLower())
            {
                case "reverse":
                    actors.Reverse();
                    break;
                    
                case "random":
                    // Simple random shuffle
                    for (int i = 0; i < actors.Count; i++)
                    {
                        var temp = actors[i];
                        var randomIndex = UnityEngine.Random.Range(i, actors.Count);
                        actors[i] = actors[randomIndex];
                        actors[randomIndex] = temp;
                    }
                    break;
                    
                case "bydistance":
                    // Sort by distance from camera/center
                    actors = actors.OrderBy(a => Vector3.Distance(a.Position, Vector3.zero)).ToList();
                    break;
                    
                case "sequence":
                default:
                    // Keep original order
                    break;
            }
            
            return actors;
        }
        
        private async UniTask ExecuteShowBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var delay = staggerDelay * i;
                
                tasks.Add(DelayedShowActor(actor, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private async UniTask ExecuteHideBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var delay = staggerDelay * i;
                
                tasks.Add(DelayedHideActor(actor, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private async UniTask ExecuteMoveBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            var positionMap = ParsePerActorValues(positions.Value);
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var delay = staggerDelay * i;
                var targetPosition = GetActorTargetPosition(actor, positionMap, i, actors.Count);
                
                tasks.Add(DelayedMoveActor(actor, targetPosition, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private async UniTask ExecuteAppearanceBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            var expressionMap = ParsePerActorValues(expressions.Value);
            var poseMap = ParsePerActorValues(poses.Value);
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var delay = staggerDelay * i;
                
                tasks.Add(DelayedChangeAppearance(actor, expressionMap, poseMap, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private async UniTask ExecuteAnimationBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            // Wave animation
            if (wave.HasValue && wave.Value)
            {
                await ExecuteWaveAnimation(actors, duration, staggerDelay, token);
                return;
            }
            
            // Custom sequence
            if (!string.IsNullOrEmpty(sequence.Value))
            {
                await ExecuteNamedSequence(actors, sequence.Value, duration, staggerDelay, token);
                return;
            }
            
            // Default animation (appearance change)
            await ExecuteAppearanceBatch(actors, duration, staggerDelay, token);
        }
        
        private async UniTask ExecuteFormationBatch(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var formationType = formation.Value?.ToLower() ?? "line";
            var positions = CalculateFormationPositions(actors.Count, formationType);
            
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var targetPosition = positions[i];
                var delay = staggerDelay * i;
                
                tasks.Add(DelayedMoveActor(actor, targetPosition, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private System.Collections.Generic.Dictionary<string, string> ParsePerActorValues(string valueString)
        {
            var map = new System.Collections.Generic.Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(valueString))
                return map;
                
            // Format: "Alice:Happy,Bob:Sad,Charlie:Neutral"
            var pairs = valueString.Split(',');
            foreach (var pair in pairs)
            {
                if (pair.Contains(':'))
                {
                    var parts = pair.Split(':');
                    if (parts.Length == 2)
                    {
                        map[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            
            return map;
        }
        
        private Vector3 GetActorTargetPosition(IActor actor, System.Collections.Generic.Dictionary<string, string> positionMap, int index, int totalActors)
        {
            // Check for specific actor position
            if (positionMap.ContainsKey(actor.Id))
            {
                return ParsePosition(positionMap[actor.Id]);
            }
            
            // Use formation positioning
            var formationType = formation.Value?.ToLower() ?? "line";
            var positions = CalculateFormationPositions(totalActors, formationType);
            return positions[index];
        }
        
        private Vector3 ParsePosition(string positionString)
        {
            switch (positionString.ToLower())
            {
                case "left": return new Vector3(-3f, 0f, 0f);
                case "center": return new Vector3(0f, 0f, 0f);
                case "right": return new Vector3(3f, 0f, 0f);
                case "far-left": return new Vector3(-5f, 0f, 0f);
                case "far-right": return new Vector3(5f, 0f, 0f);
                default:
                    if (positionString.Contains(","))
                    {
                        var parts = positionString.Split(',');
                        if (parts.Length >= 2 && 
                            float.TryParse(parts[0], out var x) && 
                            float.TryParse(parts[1], out var y))
                        {
                            var z = parts.Length > 2 && float.TryParse(parts[2], out var zVal) ? zVal : 0f;
                            return new Vector3(x, y, z);
                        }
                    }
                    return Vector3.zero;
            }
        }
        
        private System.Collections.Generic.List<Vector3> CalculateFormationPositions(int actorCount, string formationType)
        {
            var positions = new System.Collections.Generic.List<Vector3>();
            var actorSpacing = !string.IsNullOrEmpty(spacing.Value) && float.TryParse(spacing.Value, out var space) ? space : 2f;
            
            switch (formationType)
            {
                case "line":
                    for (int i = 0; i < actorCount; i++)
                    {
                        var x = (i - (actorCount - 1) * 0.5f) * actorSpacing;
                        positions.Add(new Vector3(x, 0, 0));
                    }
                    break;
                    
                case "circle":
                    var radius = actorSpacing * actorCount / (2 * Mathf.PI);
                    for (int i = 0; i < actorCount; i++)
                    {
                        var angle = i * 2 * Mathf.PI / actorCount;
                        var x = Mathf.Cos(angle) * radius;
                        var y = Mathf.Sin(angle) * radius;
                        positions.Add(new Vector3(x, y, 0));
                    }
                    break;
                    
                case "arc":
                    var arcRadius = actorSpacing * 2;
                    var arcAngle = 120f * Mathf.Deg2Rad; // 120 degree arc
                    for (int i = 0; i < actorCount; i++)
                    {
                        var angle = -arcAngle * 0.5f + (i * arcAngle / (actorCount - 1));
                        var x = Mathf.Cos(angle) * arcRadius;
                        var y = Mathf.Sin(angle) * arcRadius;
                        positions.Add(new Vector3(x, y, 0));
                    }
                    break;
                    
                case "grid":
                    var cols = Mathf.CeilToInt(Mathf.Sqrt(actorCount));
                    for (int i = 0; i < actorCount; i++)
                    {
                        var row = i / cols;
                        var col = i % cols;
                        var x = (col - (cols - 1) * 0.5f) * actorSpacing;
                        var y = row * actorSpacing;
                        positions.Add(new Vector3(x, y, 0));
                    }
                    break;
                    
                default:
                    // Default to line formation
                    goto case "line";
            }
            
            return positions;
        }
        
        private async UniTask ExecuteWaveAnimation(System.Collections.Generic.List<IActor> actors, float duration, float staggerDelay, CancellationToken token)
        {
            var amplitude = waveAmplitude.HasValue ? (float)waveAmplitude.Value : 1f;
            var frequency = waveFrequency.HasValue ? (float)waveFrequency.Value : 1f;
            
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                var phase = i * frequency;
                var delay = staggerDelay * i;
                
                tasks.Add(DelayedWaveAnimation(actor, amplitude, phase, duration, delay, token));
            }
            
            await WaitForCompletion(tasks);
        }
        
        private async UniTask ExecuteNamedSequence(System.Collections.Generic.List<IActor> actors, string sequenceName, float duration, float staggerDelay, CancellationToken token)
        {
            // TODO: Implement named sequence system
            Debug.LogWarning($"[BatchActorCommand] Named sequence not implemented: {sequenceName}");
            await ExecuteAppearanceBatch(actors, duration, staggerDelay, token);
        }
        
        private async UniTask DelayedShowActor(IActor actor, float duration, float delay, CancellationToken token)
        {
            if (delay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
            await actor.ChangeVisibilityAsync(true, duration, cancellationToken: token);
        }
        
        private async UniTask DelayedHideActor(IActor actor, float duration, float delay, CancellationToken token)
        {
            if (delay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
            await actor.ChangeVisibilityAsync(false, duration, cancellationToken: token);
        }
        
        private async UniTask DelayedMoveActor(IActor actor, Vector3 targetPosition, float duration, float delay, CancellationToken token)
        {
            if (delay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
            await actor.ChangePositionAsync(targetPosition, duration, cancellationToken: token);
        }
        
        private async UniTask DelayedChangeAppearance(IActor actor, System.Collections.Generic.Dictionary<string, string> expressionMap, 
            System.Collections.Generic.Dictionary<string, string> poseMap, float duration, float delay, CancellationToken token)
        {
            if (delay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
            if (actor is ICharacterActor character)
            {
                // Change expression if specified
                if (expressionMap.ContainsKey(actor.Id))
                {
                    if (Enum.TryParse<CharacterEmotion>(expressionMap[actor.Id], true, out var emotion))
                    {
                        await character.ChangeExpressionAsync(emotion, duration, token);
                    }
                }
                
                // Change pose if specified
                if (poseMap.ContainsKey(actor.Id))
                {
                    if (Enum.TryParse<CharacterPose>(poseMap[actor.Id], true, out var pose))
                    {
                        await character.ChangePoseAsync(pose, duration, token);
                    }
                }
            }
        }
        
        private async UniTask DelayedWaveAnimation(IActor actor, float amplitude, float phase, float duration, float delay, CancellationToken token)
        {
            if (delay > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                
            var originalPosition = actor.Position;
            var waveOffset = Vector3.up * amplitude * Mathf.Sin(phase);
            var targetPosition = originalPosition + waveOffset;
            
            await actor.ChangePositionAsync(targetPosition, duration * 0.5f, cancellationToken: token);
            await actor.ChangePositionAsync(originalPosition, duration * 0.5f, cancellationToken: token);
        }
        
        private async UniTask WaitForCompletion(System.Collections.Generic.List<UniTask> tasks)
        {
            var waitCondition = waitFor.Value?.ToLower() ?? "all";
            
            switch (waitCondition)
            {
                case "all":
                    await UniTask.WhenAll(tasks);
                    break;
                    
                case "first":
                    await UniTask.WhenAny(tasks);
                    break;
                    
                case "last":
                    // Start all tasks but only wait for the last one
                    if (tasks.Count > 0)
                        await tasks[tasks.Count - 1];
                    break;
                    
                case "none":
                    // Don't wait at all
                    break;
                    
                default:
                    await UniTask.WhenAll(tasks);
                    break;
            }
        }
    }
}
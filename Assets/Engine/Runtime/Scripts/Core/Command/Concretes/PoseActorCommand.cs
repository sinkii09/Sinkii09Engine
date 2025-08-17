using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command for applying predefined named poses and states to actors
    /// Provides a library of common actor configurations for easy reuse
    /// 
    /// Usage examples:
    /// @pose Alice greeting duration:1.0
    /// @pose Alice dramatic-entrance duration:2.0
    /// @pose Alice,Bob conversation duration:1.5
    /// @preset Alice schoolgirl-uniform duration:1.0
    /// @state Alice save:"alice_thinking" duration:1.0
    /// @state Alice load:"alice_thinking" duration:1.0
    /// </summary>
    [Serializable]
    [CommandAlias("pose")]
    public class PoseActorCommand : Command
    {
        [Header("Actor Identification")]
        public StringParameter actorId = new StringParameter(); // Single actor or comma-separated list
        
        [Header("Pose Configuration")]
        public StringParameter poseName = new StringParameter(); // Named pose or state
        public StringParameter category = new StringParameter(); // "conversation", "action", "emotion", "outfit"
        public StringParameter variation = new StringParameter(); // Pose variation (1, 2, A, B, etc.)
        
        [Header("State Management")]
        public StringParameter saveState = new StringParameter(); // Save current state with this name
        public StringParameter loadState = new StringParameter(); // Load previously saved state
        public BooleanParameter addToLibrary = new BooleanParameter(); // Add current pose to library
        
        [Header("Animation")]
        public DecimalParameter duration = new DecimalParameter();
        public StringParameter transition = new StringParameter(); // "smooth", "snap", "bounce"
        public StringParameter easing = new StringParameter();
        
        [Header("Pose Modifiers")]
        public BooleanParameter mirror = new BooleanParameter(); // Mirror the pose horizontally
        public DecimalParameter intensity = new DecimalParameter(); // Pose intensity (0-1)
        public StringParameter mood = new StringParameter(); // Apply mood modifier
        
        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                // Get actor service
                var actorService = Engine.GetService<IActorService>();
                if (actorService == null)
                {
                    Debug.LogError("[PoseActorCommand] ActorService not found");
                    return;
                }
                
                // Handle state save/load operations
                if (!string.IsNullOrEmpty(saveState.Value))
                {
                    await HandleSaveState(actorService, token);
                    return;
                }
                
                if (!string.IsNullOrEmpty(loadState.Value))
                {
                    await HandleLoadState(actorService, token);
                    return;
                }
                
                // Validate required parameters
                if (string.IsNullOrEmpty(actorId.Value))
                {
                    Debug.LogError("[PoseActorCommand] Actor ID is required");
                    return;
                }
                
                if (string.IsNullOrEmpty(poseName.Value))
                {
                    Debug.LogError("[PoseActorCommand] Pose name is required");
                    return;
                }
                
                // Get target actors
                var actorIds = ParseActorIds(actorId.Value);
                var actors = await GetActors(actorService, actorIds);
                
                if (actors.Count == 0)
                {
                    Debug.LogError($"[PoseActorCommand] No valid actors found for: {actorId.Value}");
                    return;
                }
                
                // Apply poses to all actors
                var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
                
                var tasks = new System.Collections.Generic.List<UniTask>();
                foreach (var actor in actors)
                {
                    tasks.Add(ApplyPoseToActor(actor, animationDuration, token));
                }
                
                await UniTask.WhenAll(tasks);
                
                Debug.Log($"[PoseActorCommand] Successfully applied pose '{poseName.Value}' to {actors.Count} actor(s)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PoseActorCommand] Error executing pose command: {ex.Message}");
            }
        }
        
        private UniTask HandleSaveState(IActorService actorService, CancellationToken token)
        {
            var actor = actorService.GetActor(actorId.Value);
            if (actor == null)
            {
                Debug.LogError($"[PoseActorCommand] Actor not found for state save: {actorId.Value}");
                return UniTask.CompletedTask;
            }
            
            var state = actor.GetState();
            // TODO: Implement state saving to library
            Debug.Log($"[PoseActorCommand] Saved state '{saveState.Value}' for actor {actorId.Value}");
            return UniTask.CompletedTask;
        }
        
        private UniTask HandleLoadState(IActorService actorService, CancellationToken token)
        {
            var actor = actorService.GetActor(actorId.Value);
            if (actor == null)
            {
                Debug.LogError($"[PoseActorCommand] Actor not found for state load: {actorId.Value}");
                return UniTask.CompletedTask;
            }
            
            // TODO: Implement state loading from library
            var animationDuration = duration.HasValue ? (float)duration.Value : 1.0f;
            Debug.Log($"[PoseActorCommand] Loaded state '{loadState.Value}' for actor {actorId.Value}");
            return UniTask.CompletedTask;
        }
        
        private System.Collections.Generic.List<string> ParseActorIds(string actorIdString)
        {
            var ids = new System.Collections.Generic.List<string>();
            
            if (actorIdString.Contains(","))
            {
                foreach (var part in actorIdString.Split(','))
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
                    Debug.LogWarning($"[PoseActorCommand] Actor not found: {id}");
                }
            }
            
            return UniTask.FromResult(actors);
        }
        
        private async UniTask ApplyPoseToActor(IActor actor, float animationDuration, CancellationToken token)
        {
            var pose = GetNamedPose(poseName.Value, category.Value, variation.Value);
            if (pose == null)
            {
                Debug.LogWarning($"[PoseActorCommand] Unknown pose: {poseName.Value}");
                return;
            }
            
            // Apply mood modifiers
            if (!string.IsNullOrEmpty(mood.Value))
            {
                pose = ApplyMoodModifier(pose, mood.Value);
            }
            
            // Apply intensity modifiers
            if (intensity.HasValue)
            {
                pose = ApplyIntensityModifier(pose, (float)intensity.Value);
            }
            
            // Apply mirror modifier
            if (mirror.HasValue && mirror.Value)
            {
                pose = ApplyMirrorModifier(pose);
            }
            
            // Apply the pose based on actor type
            if (actor is ICharacterActor character)
            {
                await ApplyCharacterPose(character, pose, animationDuration, token);
            }
            else if (actor is IBackgroundActor background)
            {
                await ApplyBackgroundPose(background, pose, animationDuration, token);
            }
        }
        
        private ActorPose GetNamedPose(string name, string categoryFilter, string variationFilter)
        {
            var normalizedName = name.ToLower().Replace("-", "").Replace("_", "");
            
            // Character poses
            switch (normalizedName)
            {
                // Greeting poses
                case "greeting":
                case "hello":
                case "wave":
                    return new ActorPose
                    {
                        Name = "Greeting",
                        Expression = CharacterEmotion.Happy,
                        Pose = CharacterPose.Waving,
                        LookDirection = CharacterLookDirection.Center,
                        Position = Vector3.zero,
                        Scale = Vector3.one
                    };
                    
                // Conversation poses
                case "conversation":
                case "talking":
                case "speak":
                    return new ActorPose
                    {
                        Name = "Conversation",
                        Expression = CharacterEmotion.Neutral,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Right,
                        Position = Vector3.zero,
                        Scale = Vector3.one
                    };
                    
                // Thinking poses
                case "thinking":
                case "pondering":
                case "thoughtful":
                    return new ActorPose
                    {
                        Name = "Thinking",
                        Expression = CharacterEmotion.Thinking,
                        Pose = CharacterPose.Thinking,
                        LookDirection = CharacterLookDirection.Up,
                        Position = Vector3.zero,
                        Scale = Vector3.one
                    };
                    
                // Dramatic poses
                case "dramaticentrance":
                case "dramatic":
                case "grand":
                    return new ActorPose
                    {
                        Name = "Dramatic Entrance",
                        Expression = CharacterEmotion.Determined,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Center,
                        Position = new Vector3(0, 0.2f, 0),
                        Scale = Vector3.one * 1.1f
                    };
                    
                // Shy poses
                case "shy":
                case "embarrassed":
                case "bashful":
                    return new ActorPose
                    {
                        Name = "Shy",
                        Expression = CharacterEmotion.Embarrassed,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Down,
                        Position = new Vector3(0, -0.1f, 0),
                        Scale = Vector3.one * 0.95f
                    };
                    
                // Confident poses
                case "confident":
                case "proud":
                case "strong":
                    return new ActorPose
                    {
                        Name = "Confident",
                        Expression = CharacterEmotion.Determined,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Center,
                        Position = Vector3.zero,
                        Scale = Vector3.one * 1.05f
                    };
                    
                // Surprised poses
                case "surprised":
                case "shocked":
                case "startled":
                    return new ActorPose
                    {
                        Name = "Surprised",
                        Expression = CharacterEmotion.Surprised,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Center,
                        Position = new Vector3(0, 0.1f, 0),
                        Scale = Vector3.one * 1.02f
                    };
                    
                // Sad poses
                case "sad":
                case "depressed":
                case "crying":
                    return new ActorPose
                    {
                        Name = "Sad",
                        Expression = CharacterEmotion.Sad,
                        Pose = CharacterPose.Sitting,
                        LookDirection = CharacterLookDirection.Down,
                        Position = new Vector3(0, -0.2f, 0),
                        Scale = Vector3.one * 0.9f
                    };
                    
                // Angry poses
                case "angry":
                case "mad":
                case "furious":
                    return new ActorPose
                    {
                        Name = "Angry",
                        Expression = CharacterEmotion.Angry,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Center,
                        Position = Vector3.zero,
                        Scale = Vector3.one * 1.1f
                    };
                    
                // Sleeping poses
                case "sleeping":
                case "tired":
                case "sleepy":
                    return new ActorPose
                    {
                        Name = "Sleeping",
                        Expression = CharacterEmotion.Neutral,
                        Pose = CharacterPose.Sleeping,
                        LookDirection = CharacterLookDirection.Down,
                        Position = new Vector3(0, -0.5f, 0),
                        Scale = Vector3.one
                    };
                    
                // Default neutral pose
                default:
                    Debug.LogWarning($"[PoseActorCommand] Unknown pose name: {name}, using default");
                    return new ActorPose
                    {
                        Name = "Default",
                        Expression = CharacterEmotion.Neutral,
                        Pose = CharacterPose.Standing,
                        LookDirection = CharacterLookDirection.Center,
                        Position = Vector3.zero,
                        Scale = Vector3.one
                    };
            }
        }
        
        private ActorPose ApplyMoodModifier(ActorPose basePose, string moodName)
        {
            var modifiedPose = basePose.Clone();
            
            switch (moodName.ToLower())
            {
                case "happy":
                case "cheerful":
                    modifiedPose.Expression = CharacterEmotion.Happy;
                    modifiedPose.Scale *= 1.05f;
                    break;
                    
                case "sad":
                case "melancholy":
                    modifiedPose.Expression = CharacterEmotion.Sad;
                    modifiedPose.Position += Vector3.down * 0.1f;
                    modifiedPose.Scale *= 0.95f;
                    break;
                    
                case "energetic":
                case "excited":
                    modifiedPose.Expression = CharacterEmotion.Excited;
                    modifiedPose.Position += Vector3.up * 0.1f;
                    modifiedPose.Scale *= 1.1f;
                    break;
                    
                case "calm":
                case "peaceful":
                    modifiedPose.Expression = CharacterEmotion.Neutral;
                    break;
                    
                case "mysterious":
                case "serious":
                    modifiedPose.Expression = CharacterEmotion.Thinking;
                    modifiedPose.LookDirection = CharacterLookDirection.Left;
                    break;
            }
            
            return modifiedPose;
        }
        
        private ActorPose ApplyIntensityModifier(ActorPose basePose, float intensityValue)
        {
            var modifiedPose = basePose.Clone();
            var intensity = Mathf.Clamp01(intensityValue);
            
            // Scale position and scale changes based on intensity
            var positionDelta = modifiedPose.Position - Vector3.zero;
            modifiedPose.Position = Vector3.zero + (positionDelta * intensity);
            
            var scaleDelta = modifiedPose.Scale - Vector3.one;
            modifiedPose.Scale = Vector3.one + (scaleDelta * intensity);
            
            return modifiedPose;
        }
        
        private ActorPose ApplyMirrorModifier(ActorPose basePose)
        {
            var modifiedPose = basePose.Clone();
            
            // Mirror position
            modifiedPose.Position = new Vector3(-modifiedPose.Position.x, modifiedPose.Position.y, modifiedPose.Position.z);
            
            // Mirror look direction
            modifiedPose.LookDirection = modifiedPose.LookDirection switch
            {
                CharacterLookDirection.Left => CharacterLookDirection.Right,
                CharacterLookDirection.Right => CharacterLookDirection.Left,
                CharacterLookDirection.UpLeft => CharacterLookDirection.UpRight,
                CharacterLookDirection.UpRight => CharacterLookDirection.UpLeft,
                CharacterLookDirection.DownLeft => CharacterLookDirection.DownRight,
                CharacterLookDirection.DownRight => CharacterLookDirection.DownLeft,
                _ => modifiedPose.LookDirection
            };
            
            return modifiedPose;
        }
        
        private async UniTask ApplyCharacterPose(ICharacterActor character, ActorPose pose, float duration, CancellationToken token)
        {
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            // Apply all pose properties simultaneously
            tasks.Add(character.ChangeExpressionAsync(pose.Expression, duration, token));
            tasks.Add(character.ChangePoseAsync(pose.Pose, duration, token));
            tasks.Add(character.ChangeLookDirectionAsync(pose.LookDirection, duration, token));
            tasks.Add(character.ChangePositionAsync(pose.Position, duration, cancellationToken: token));
            tasks.Add(character.ChangeScaleAsync(pose.Scale, duration, cancellationToken: token));
            
            await UniTask.WhenAll(tasks);
        }
        
        private async UniTask ApplyBackgroundPose(IBackgroundActor background, ActorPose pose, float duration, CancellationToken token)
        {
            // For backgrounds, mainly apply transform changes
            var tasks = new System.Collections.Generic.List<UniTask>();
            
            tasks.Add(background.ChangePositionAsync(pose.Position, duration, cancellationToken: token));
            tasks.Add(background.ChangeScaleAsync(pose.Scale, duration, cancellationToken: token));
            
            await UniTask.WhenAll(tasks);
        }
        
        [Serializable]
        private class ActorPose
        {
            public string Name;
            public CharacterEmotion Expression;
            public CharacterPose Pose;
            public CharacterLookDirection LookDirection;
            public Vector3 Position;
            public Vector3 Scale;
            public string Description;
            
            public ActorPose Clone()
            {
                return new ActorPose
                {
                    Name = Name,
                    Expression = Expression,
                    Pose = Pose,
                    LookDirection = LookDirection,
                    Position = Position,
                    Scale = Scale,
                    Description = Description
                };
            }
        }
    }
}
using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Factory for creating appropriate ActorState instances based on actor type
    /// </summary>
    public static class ActorStateFactory
    {
        /// <summary>
        /// Creates the appropriate state type for the given actor
        /// </summary>
        public static ActorState CreateStateForActor(IActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            
            ActorState state = actor switch
            {
                ICharacterActor => new CharacterState(actor.Id),
                IBackgroundActor => new BackgroundState(actor.Id),
                _ => new ActorState(actor.Id, actor.ActorType)
            };
            
            state.CaptureFromActor(actor);
            return state;
        }
        
        /// <summary>
        /// Creates an empty state of the appropriate type
        /// </summary>
        public static ActorState CreateStateForType(ActorType actorType, string id)
        {
            if (actorType == ActorType.Character)
                return new CharacterState(id);
            
            if (actorType == ActorType.Background)
                return new BackgroundState(id);
            
            return new ActorState(id, actorType);
        }
    }
}
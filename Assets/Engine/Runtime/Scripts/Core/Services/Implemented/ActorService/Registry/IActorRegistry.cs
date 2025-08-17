using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for actor storage and retrieval operations
    /// Responsible for managing the actor registry with thread-safe operations
    /// </summary>
    public interface IActorRegistry
    {
        #region Properties
        
        IReadOnlyCollection<IActor> AllActors { get; }
        IReadOnlyCollection<ICharacterActor> CharacterActors { get; }
        IReadOnlyCollection<IBackgroundActor> BackgroundActors { get; }
        int ActorCount { get; }
        
        #endregion
        
        #region Registration Operations
        
        bool RegisterActor(IActor actor);
        bool UnregisterActor(string actorId);
        
        #endregion
        
        #region Basic Retrieval Operations
        
        IActor GetActor(string id);
        ICharacterActor GetCharacterActor(string id);
        IBackgroundActor GetBackgroundActor(string id);
        T GetActor<T>(string id) where T : class, IActor;
        
        #endregion
        
        #region Safe Retrieval Operations
        
        bool TryGetActor(string id, out IActor actor);
        bool TryGetCharacterActor(string id, out ICharacterActor actor);
        bool TryGetBackgroundActor(string id, out IBackgroundActor actor);
        
        #endregion
        
        #region Collection Operations
        
        IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor;
        string[] GetActorIds();
        
        #endregion
        
        #region Utility Operations
        
        bool HasActor(string id);
        void Clear();
        
        #endregion
        
        #region Events
        
        event Action<IActor> OnActorRegistered;
        event Action<string> OnActorUnregistered;
        
        #endregion
    }
}
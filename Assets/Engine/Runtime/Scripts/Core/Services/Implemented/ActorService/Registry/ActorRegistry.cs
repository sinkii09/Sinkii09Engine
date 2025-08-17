using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Thread-safe actor registry implementation for storing and retrieving actors
    /// Manages three separate collections for efficient type-specific lookups
    /// </summary>
    public class ActorRegistry : IActorRegistry
    {
        #region Private Fields
        
        private readonly ConcurrentDictionary<string, IActor> _actors = new();
        private readonly ConcurrentDictionary<string, ICharacterActor> _characterActors = new();
        private readonly ConcurrentDictionary<string, IBackgroundActor> _backgroundActors = new();
        
        #endregion
        
        #region Properties
        
        public IReadOnlyCollection<IActor> AllActors => 
            _actors.Values.ToArray();
        
        public IReadOnlyCollection<ICharacterActor> CharacterActors => 
            _characterActors.Values.ToArray();
        
        public IReadOnlyCollection<IBackgroundActor> BackgroundActors => 
            _backgroundActors.Values.ToArray();
        
        public int ActorCount => _actors.Count;
        
        #endregion
        
        #region Events
        
        public event Action<IActor> OnActorRegistered;
        public event Action<string> OnActorUnregistered;
        
        #endregion
        
        #region Registration Operations
        
        public bool RegisterActor(IActor actor)
        {
            if (actor == null)
            {
                Debug.LogWarning("[ActorRegistry] Cannot register null actor");
                return false;
            }
            
            if (string.IsNullOrEmpty(actor.Id))
            {
                Debug.LogWarning("[ActorRegistry] Cannot register actor with empty ID");
                return false;
            }
            
            // Check if actor already exists
            if (_actors.ContainsKey(actor.Id))
            {
                Debug.LogWarning($"[ActorRegistry] Actor '{actor.Id}' is already registered");
                return false;
            }
            
            // Register in main collection
            if (!_actors.TryAdd(actor.Id, actor))
            {
                Debug.LogError($"[ActorRegistry] Failed to register actor '{actor.Id}' in main collection");
                return false;
            }
            
            // Register in type-specific collections
            try
            {
                if (actor is ICharacterActor characterActor)
                {
                    _characterActors.TryAdd(actor.Id, characterActor);
                }
                else if (actor is IBackgroundActor backgroundActor)
                {
                    _backgroundActors.TryAdd(actor.Id, backgroundActor);
                }
                
                // Fire event
                OnActorRegistered?.Invoke(actor);
                
                Debug.Log($"[ActorRegistry] Successfully registered actor '{actor.Id}' ({actor.ActorType})");
                return true;
            }
            catch (Exception ex)
            {
                // Rollback main registration on failure
                _actors.TryRemove(actor.Id, out _);
                Debug.LogError($"[ActorRegistry] Failed to register actor '{actor.Id}': {ex.Message}");
                return false;
            }
        }
        
        public bool UnregisterActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                Debug.LogWarning("[ActorRegistry] Cannot unregister actor with empty ID");
                return false;
            }
            
            // Remove from main collection
            if (!_actors.TryRemove(actorId, out var removedActor))
            {
                Debug.LogWarning($"[ActorRegistry] Actor '{actorId}' not found for unregistration");
                return false;
            }
            
            // Remove from type-specific collections
            _characterActors.TryRemove(actorId, out _);
            _backgroundActors.TryRemove(actorId, out _);
            
            // Fire event
            OnActorUnregistered?.Invoke(actorId);
            
            Debug.Log($"[ActorRegistry] Successfully unregistered actor '{actorId}'");
            return true;
        }
        
        #endregion
        
        #region Basic Retrieval Operations
        
        public IActor GetActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
                
            _actors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public ICharacterActor GetCharacterActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
                
            _characterActors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public IBackgroundActor GetBackgroundActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
                
            _backgroundActors.TryGetValue(id, out var actor);
            return actor;
        }
        
        public T GetActor<T>(string id) where T : class, IActor
        {
            return GetActor(id) as T;
        }
        
        #endregion
        
        #region Safe Retrieval Operations
        
        public bool TryGetActor(string id, out IActor actor)
        {
            actor = null;
            
            if (string.IsNullOrEmpty(id))
                return false;
                
            return _actors.TryGetValue(id, out actor);
        }
        
        public bool TryGetCharacterActor(string id, out ICharacterActor actor)
        {
            actor = null;
            
            if (string.IsNullOrEmpty(id))
                return false;
                
            return _characterActors.TryGetValue(id, out actor);
        }
        
        public bool TryGetBackgroundActor(string id, out IBackgroundActor actor)
        {
            actor = null;
            
            if (string.IsNullOrEmpty(id))
                return false;
                
            return _backgroundActors.TryGetValue(id, out actor);
        }
        
        #endregion
        
        #region Collection Operations
        
        public IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor
        {
            if (typeof(T) == typeof(ICharacterActor))
            {
                return _characterActors.Values.Cast<T>().ToList().AsReadOnly();
            }
            
            if (typeof(T) == typeof(IBackgroundActor))
            {
                return _backgroundActors.Values.Cast<T>().ToList().AsReadOnly();
            }
            
            // For IActor or other types, filter from main collection
            return _actors.Values.OfType<T>().ToList().AsReadOnly();
        }
        
        public string[] GetActorIds()
        {
            return _actors.Keys.ToArray();
        }
        
        #endregion
        
        #region Utility Operations
        
        public bool HasActor(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
                
            return _actors.ContainsKey(id);
        }
        
        public void Clear()
        {
            var actorIds = _actors.Keys.ToArray();
            
            _actors.Clear();
            _characterActors.Clear();
            _backgroundActors.Clear();
            
            // Fire events for all unregistered actors
            foreach (var actorId in actorIds)
            {
                OnActorUnregistered?.Invoke(actorId);
            }
            
            Debug.Log($"[ActorRegistry] Cleared registry - removed {actorIds.Length} actors");
        }
        
        #endregion
        
        #region Debug and Validation
        
        public string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Actor Registry Debug Info ===");
            info.AppendLine($"Total Actors: {_actors.Count}");
            info.AppendLine($"Character Actors: {_characterActors.Count}");
            info.AppendLine($"Background Actors: {_backgroundActors.Count}");
            info.AppendLine();
            
            if (_actors.Count > 0)
            {
                info.AppendLine("Registered Actors:");
                foreach (var kvp in _actors)
                {
                    var actor = kvp.Value;
                    info.AppendLine($"  {kvp.Key}: {actor.ActorType} ({actor.GetType().Name})");
                }
            }
            
            return info.ToString();
        }
        
        public bool ValidateConsistency(out string[] errors)
        {
            var errorList = new List<string>();
            
            // Check that all type-specific actors are in main collection
            foreach (var characterActor in _characterActors.Values)
            {
                if (!_actors.ContainsKey(characterActor.Id))
                {
                    errorList.Add($"Character actor '{characterActor.Id}' not found in main collection");
                }
            }
            
            foreach (var backgroundActor in _backgroundActors.Values)
            {
                if (!_actors.ContainsKey(backgroundActor.Id))
                {
                    errorList.Add($"Background actor '{backgroundActor.Id}' not found in main collection");
                }
            }
            
            // Check for orphaned entries in main collection
            foreach (var actor in _actors.Values)
            {
                if (actor is ICharacterActor && !_characterActors.ContainsKey(actor.Id))
                {
                    errorList.Add($"Character actor '{actor.Id}' missing from character collection");
                }
                else if (actor is IBackgroundActor && !_backgroundActors.ContainsKey(actor.Id))
                {
                    errorList.Add($"Background actor '{actor.Id}' missing from background collection");
                }
            }
            
            errors = errorList.ToArray();
            return errors.Length == 0;
        }
        
        #endregion
    }
}
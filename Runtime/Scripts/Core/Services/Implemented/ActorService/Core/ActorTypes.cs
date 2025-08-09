using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Extensible actor type system using struct-based enum pattern for compile-time safety and performance
    /// </summary>
    [Serializable]
    public struct ActorType : IEquatable<ActorType>
    {
        public readonly int Value;
        public readonly string Name;
        
        private ActorType(int value, string name)
        {
            Value = value;
            Name = name;
        }
        
        // Built-in actor types
        public static readonly ActorType Character = new(1, nameof(Character));
        public static readonly ActorType Background = new(2, nameof(Background));
        public static readonly ActorType Prop = new(3, nameof(Prop));
        public static readonly ActorType Effect = new(4, nameof(Effect));
        public static readonly ActorType UI = new(5, nameof(UI));
        
        // Registry for custom types
        private static readonly Dictionary<int, ActorType> _registeredTypes = new()
        {
            [1] = Character,
            [2] = Background,
            [3] = Prop,
            [4] = Effect,
            [5] = UI
        };
        
        private static readonly Dictionary<string, ActorType> _typesByName = new()
        {
            [nameof(Character)] = Character,
            [nameof(Background)] = Background,
            [nameof(Prop)] = Prop,
            [nameof(Effect)] = Effect,
            [nameof(UI)] = UI
        };
        
        /// <summary>
        /// Registers a custom actor type
        /// </summary>
        /// <param name="value">Unique integer value (must be > 1000 for custom types)</param>
        /// <param name="name">Human-readable name</param>
        /// <returns>New ActorType instance</returns>
        public static ActorType Register(int value, string name)
        {
            if (value <= 1000)
            {
                throw new ArgumentException("Custom actor type values must be greater than 1000 to avoid conflicts with built-in types", nameof(value));
            }
            
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Actor type name cannot be null or empty", nameof(name));
            }
            
            if (_registeredTypes.ContainsKey(value))
            {
                throw new ArgumentException($"Actor type with value {value} is already registered", nameof(value));
            }
            
            if (_typesByName.ContainsKey(name))
            {
                throw new ArgumentException($"Actor type with name '{name}' is already registered", nameof(name));
            }
            
            var type = new ActorType(value, name);
            _registeredTypes[value] = type;
            _typesByName[name] = type;
            
            Debug.Log($"[ActorType] Registered custom actor type: {name} (ID: {value})");
            return type;
        }
        
        /// <summary>
        /// Gets all registered actor types
        /// </summary>
        public static IReadOnlyCollection<ActorType> GetAllTypes() => _registeredTypes.Values;
        
        /// <summary>
        /// Tries to get an actor type by name
        /// </summary>
        public static bool TryGetByName(string name, out ActorType actorType)
        {
            return _typesByName.TryGetValue(name, out actorType);
        }
        
        /// <summary>
        /// Tries to get an actor type by value
        /// </summary>
        public static bool TryGetByValue(int value, out ActorType actorType)
        {
            return _registeredTypes.TryGetValue(value, out actorType);
        }
        
        // Equality and conversion operators
        public bool Equals(ActorType other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ActorType other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Name;
        
        public static bool operator ==(ActorType left, ActorType right) => left.Equals(right);
        public static bool operator !=(ActorType left, ActorType right) => !left.Equals(right);
        
        // Implicit conversion to int for serialization efficiency
        public static implicit operator int(ActorType type) => type.Value;
        
        // Explicit conversion from int with validation
        public static explicit operator ActorType(int value)
        {
            if (_registeredTypes.TryGetValue(value, out var type))
                return type;
            
            Debug.LogWarning($"[ActorType] Unknown actor type value: {value}. Creating temporary type.");
            return new ActorType(value, $"Unknown_{value}");
        }
        
        // String conversion for debugging and serialization
        public static explicit operator string(ActorType type) => type.Name;
        
        // Parse from string
        public static ActorType Parse(string name)
        {
            if (TryGetByName(name, out var type))
                return type;
            
            throw new ArgumentException($"Unknown actor type name: {name}");
        }
        
        public static bool TryParse(string name, out ActorType actorType)
        {
            return TryGetByName(name, out actorType);
        }
    }
    
    /// <summary>
    /// Character-specific enums for strongly-typed appearance management
    /// </summary>
    public enum CharacterExpression
    {
        Neutral = 0,
        Happy = 1,
        Sad = 2,
        Angry = 3,
        Surprised = 4,
        Confused = 5,
        Embarrassed = 6,
        Determined = 7,
        Excited = 8,
        Worried = 9,
        Thinking = 10,
        Smiling = 11
    }
    
    public enum CharacterPose
    {
        Standing = 0,
        Sitting = 1,
        Walking = 2,
        Running = 3,
        Waving = 4,
        Pointing = 5,
        Thinking = 6,
        Sleeping = 7,
        Kneeling = 8,
        Jumping = 9,
        Dancing = 10,
        Fighting = 11
    }
    
    public enum CharacterLookDirection
    {
        Center = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4,
        UpLeft = 5,
        UpRight = 6,
        DownLeft = 7,
        DownRight = 8
    }
    
    /// <summary>
    /// Background-specific enums for scene management
    /// </summary>
    public enum BackgroundType
    {
        Scene = 0,
        Environment = 1,
        Sky = 2,
        Overlay = 3,
        UI = 4,
        Effect = 5,
        Particle = 6
    }
    
    public enum SceneLocation
    {
        // Indoor locations
        Classroom = 0,
        Library = 1,
        Cafeteria = 2,
        Hallway = 3,
        Bedroom = 4,
        Kitchen = 5,
        Bathroom = 6,
        Office = 7,
        
        // Outdoor locations
        Park = 100,
        Beach = 101,
        Forest = 102,
        Mountain = 103,
        City = 104,
        Garden = 105,
        Playground = 106,
        Street = 107,
        
        // Special locations
        Space = 200,
        Fantasy = 201,
        Abstract = 202,
        Dream = 203
    }
    
    public enum SceneTransitionType
    {
        None = 0,
        Fade = 1,
        Slide = 2,
        Wipe = 3,
        Dissolve = 4,
        Zoom = 5,
        Rotate = 6,
        Custom = 7
    }
    
    /// <summary>
    /// General actor enums
    /// </summary>
    public enum ActorVisibilityState
    {
        Hidden = 0,
        Visible = 1,
        FadingIn = 2,
        FadingOut = 3,
        Transparent = 4
    }
    
    public enum ActorLoadState
    {
        Unloaded = 0,
        Loading = 1,
        Loaded = 2,
        Error = 3,
        Unloading = 4
    }
}
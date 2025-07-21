using System;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Composite appearance structure for character actors with automatic resource path generation
    /// </summary>
    [Serializable]
    public struct CharacterAppearance : IEquatable<CharacterAppearance>
    {
        [SerializeField] private CharacterExpression _expression;
        [SerializeField] private CharacterPose _pose;
        [SerializeField] private int _outfitId;
        
        public CharacterExpression Expression
        {
            get => _expression;
            set => _expression = value;
        }
        
        public CharacterPose Pose
        {
            get => _pose;
            set => _pose = value;
        }
        
        public int OutfitId
        {
            get => _outfitId;
            set => _outfitId = Mathf.Max(0, value); // Ensure non-negative
        }
        
        /// <summary>
        /// Creates a new character appearance
        /// </summary>
        public CharacterAppearance(CharacterExpression expression, CharacterPose pose = CharacterPose.Standing, int outfitId = 0)
        {
            _expression = expression;
            _pose = pose;
            _outfitId = Mathf.Max(0, outfitId);
        }
        
        /// <summary>
        /// Convenient factory method for creating appearances
        /// </summary>
        public static CharacterAppearance Create(CharacterExpression expression, CharacterPose pose = CharacterPose.Standing, int outfit = 0)
            => new(expression, pose, outfit);
        
        /// <summary>
        /// Generates standardized resource path for this appearance
        /// </summary>
        /// <param name="characterName">Name of the character</param>
        /// <param name="basePath">Base resource path (default: "Characters")</param>
        /// <returns>Full resource path</returns>
        public string GetResourcePath(string characterName, string basePath = "Characters")
        {
            if (string.IsNullOrEmpty(characterName))
                throw new ArgumentException("Character name cannot be null or empty", nameof(characterName));
            
            return $"{basePath}/{characterName}/{_expression}_{_pose}_{_outfitId:D2}";
        }
        
        /// <summary>
        /// Gets alternative resource paths for fallback loading
        /// </summary>
        public string[] GetFallbackPaths(string characterName, string basePath = "Characters")
        {
            return new[]
            {
                GetResourcePath(characterName, basePath),
                $"{basePath}/{characterName}/{_expression}_{CharacterPose.Standing}_{_outfitId:D2}", // Fallback to standing pose
                $"{basePath}/{characterName}/{CharacterExpression.Neutral}_{_pose}_{_outfitId:D2}", // Fallback to neutral expression
                $"{basePath}/{characterName}/{CharacterExpression.Neutral}_{CharacterPose.Standing}_00", // Default appearance
                $"{basePath}/Default/Default_Appearance" // Ultimate fallback
            };
        }
        
        /// <summary>
        /// Creates appearance with different expression
        /// </summary>
        public CharacterAppearance WithExpression(CharacterExpression expression)
            => new(expression, _pose, _outfitId);
        
        /// <summary>
        /// Creates appearance with different pose
        /// </summary>
        public CharacterAppearance WithPose(CharacterPose pose)
            => new(_expression, pose, _outfitId);
        
        /// <summary>
        /// Creates appearance with different outfit
        /// </summary>
        public CharacterAppearance WithOutfit(int outfitId)
            => new(_expression, _pose, outfitId);
        
        // Equality implementation
        public bool Equals(CharacterAppearance other)
            => _expression == other._expression && _pose == other._pose && _outfitId == other._outfitId;
        
        public override bool Equals(object obj)
            => obj is CharacterAppearance other && Equals(other);
        
        public override int GetHashCode()
            => HashCode.Combine(_expression, _pose, _outfitId);
        
        public static bool operator ==(CharacterAppearance left, CharacterAppearance right)
            => left.Equals(right);
        
        public static bool operator !=(CharacterAppearance left, CharacterAppearance right)
            => !left.Equals(right);
        
        public override string ToString()
            => $"{_expression}_{_pose}_Outfit{_outfitId:D2}";
        
        // Predefined common appearances for convenience
        public static readonly CharacterAppearance Default = new(CharacterExpression.Neutral, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Happy = new(CharacterExpression.Happy, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Sad = new(CharacterExpression.Sad, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Surprised = new(CharacterExpression.Surprised, CharacterPose.Standing, 0);
    }
    
    /// <summary>
    /// Composite appearance structure for background actors with automatic resource path generation
    /// </summary>
    [Serializable]
    public struct BackgroundAppearance : IEquatable<BackgroundAppearance>
    {
        [SerializeField] private BackgroundType _type;
        [SerializeField] private SceneLocation _location;
        [SerializeField] private int _variantId;
        
        public BackgroundType Type
        {
            get => _type;
            set => _type = value;
        }
        
        public SceneLocation Location
        {
            get => _location;
            set => _location = value;
        }
        
        public int VariantId
        {
            get => _variantId;
            set => _variantId = Mathf.Max(0, value); // Ensure non-negative
        }
        
        /// <summary>
        /// Creates a new background appearance
        /// </summary>
        public BackgroundAppearance(BackgroundType type, SceneLocation location, int variantId = 0)
        {
            _type = type;
            _location = location;
            _variantId = Mathf.Max(0, variantId);
        }
        
        /// <summary>
        /// Convenient factory method for creating background appearances
        /// </summary>
        public static BackgroundAppearance Create(BackgroundType type, SceneLocation location, int variant = 0)
            => new(type, location, variant);
        
        /// <summary>
        /// Generates standardized resource path for this background
        /// </summary>
        /// <param name="basePath">Base resource path (default: "Backgrounds")</param>
        /// <returns>Full resource path</returns>
        public string GetResourcePath(string basePath = "Backgrounds")
        {
            return $"{basePath}/{_type}/{_location}_{_variantId:D2}";
        }
        
        /// <summary>
        /// Gets alternative resource paths for fallback loading
        /// </summary>
        public string[] GetFallbackPaths(string basePath = "Backgrounds")
        {
            return new[]
            {
                GetResourcePath(basePath),
                $"{basePath}/{_type}/{_location}_00", // Fallback to variant 0
                $"{basePath}/Scene/{_location}_00", // Fallback to Scene type
                $"{basePath}/Default/Default_Background" // Ultimate fallback
            };
        }
        
        /// <summary>
        /// Creates background with different type
        /// </summary>
        public BackgroundAppearance WithType(BackgroundType type)
            => new(type, _location, _variantId);
        
        /// <summary>
        /// Creates background with different location
        /// </summary>
        public BackgroundAppearance WithLocation(SceneLocation location)
            => new(_type, location, _variantId);
        
        /// <summary>
        /// Creates background with different variant
        /// </summary>
        public BackgroundAppearance WithVariant(int variantId)
            => new(_type, _location, variantId);
        
        // Equality implementation
        public bool Equals(BackgroundAppearance other)
            => _type == other._type && _location == other._location && _variantId == other._variantId;
        
        public override bool Equals(object obj)
            => obj is BackgroundAppearance other && Equals(other);
        
        public override int GetHashCode()
            => HashCode.Combine(_type, _location, _variantId);
        
        public static bool operator ==(BackgroundAppearance left, BackgroundAppearance right)
            => left.Equals(right);
        
        public static bool operator !=(BackgroundAppearance left, BackgroundAppearance right)
            => !left.Equals(right);
        
        public override string ToString()
            => $"{_type}_{_location}_V{_variantId:D2}";
        
        // Predefined common backgrounds for convenience
        public static readonly BackgroundAppearance DefaultClassroom = new(BackgroundType.Scene, SceneLocation.Classroom, 0);
        public static readonly BackgroundAppearance DefaultLibrary = new(BackgroundType.Scene, SceneLocation.Library, 0);
        public static readonly BackgroundAppearance DefaultPark = new(BackgroundType.Environment, SceneLocation.Park, 0);
        public static readonly BackgroundAppearance Black = new(BackgroundType.Scene, SceneLocation.Abstract, 0);
    }
    
    /// <summary>
    /// Generic appearance data for other actor types
    /// </summary>
    [Serializable]
    public struct GenericAppearance : IEquatable<GenericAppearance>
    {
        [SerializeField] private string _appearanceId;
        [SerializeField] private int _variantId;
        
        public string AppearanceId
        {
            get => _appearanceId ?? string.Empty;
            set => _appearanceId = value ?? string.Empty;
        }
        
        public int VariantId
        {
            get => _variantId;
            set => _variantId = Mathf.Max(0, value);
        }
        
        public GenericAppearance(string appearanceId, int variantId = 0)
        {
            _appearanceId = appearanceId ?? string.Empty;
            _variantId = Mathf.Max(0, variantId);
        }
        
        public string GetResourcePath(ActorType actorType, string basePath = "Actors")
        {
            return $"{basePath}/{actorType.Name}/{_appearanceId}_{_variantId:D2}";
        }
        
        public bool Equals(GenericAppearance other)
            => _appearanceId == other._appearanceId && _variantId == other._variantId;
        
        public override bool Equals(object obj)
            => obj is GenericAppearance other && Equals(other);
        
        public override int GetHashCode()
            => HashCode.Combine(_appearanceId, _variantId);
        
        public static bool operator ==(GenericAppearance left, GenericAppearance right)
            => left.Equals(right);
        
        public static bool operator !=(GenericAppearance left, GenericAppearance right)
            => !left.Equals(right);
        
        public override string ToString()
            => $"{_appearanceId}_V{_variantId:D2}";
    }
}
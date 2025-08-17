using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base appearance interface for polymorphic support and addressable integration
    /// </summary>
    public interface IAppearance
    {
        /// <summary>
        /// Gets the addressable key for this appearance
        /// </summary>
        string GetAddressableKey(string actorId);
        
        /// <summary>
        /// Gets addressable labels for batch loading
        /// </summary>
        string[] GetAddressableLabels();
        
        /// <summary>
        /// Gets path parameters for ResourcePathResolver integration
        /// </summary>
        PathParameter[] GetPathParameters(string actorId);
    }
    /// <summary>
    /// Composite appearance structure for character actors with automatic resource path generation
    /// </summary>
    [Serializable]
    public struct CharacterAppearance : IAppearance, IEquatable<CharacterAppearance>
    {
        [SerializeField] private CharacterEmotion _expression;
        [SerializeField] private CharacterPose _pose;
        [SerializeField] private int _outfitId;
        
        // Addressable integration - optional for backward compatibility
        [SerializeField] private AssetReference _spriteReference;
        [SerializeField] private string _addressableKey;
        
        public CharacterEmotion Expression
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
        /// Addressable asset reference for direct loading (optional)
        /// </summary>
        public AssetReference SpriteReference
        {
            get => _spriteReference;
            set => _spriteReference = value;
        }
        
        /// <summary>
        /// Custom addressable key override (optional)
        /// </summary>
        public string AddressableKey
        {
            get => _addressableKey;
            set => _addressableKey = value;
        }
        
        /// <summary>
        /// Creates a new character appearance
        /// </summary>
        public CharacterAppearance(CharacterEmotion expression, CharacterPose pose = CharacterPose.Standing, int outfitId = 0)
        {
            _expression = expression;
            _pose = pose;
            _outfitId = Mathf.Max(0, outfitId);
            _spriteReference = null;
            _addressableKey = string.Empty;
        }
        
        /// <summary>
        /// Creates appearance with addressable reference
        /// </summary>
        public CharacterAppearance(CharacterEmotion expression, CharacterPose pose, int outfitId, AssetReference spriteReference, string addressableKey = null)
        {
            _expression = expression;
            _pose = pose;
            _outfitId = Mathf.Max(0, outfitId);
            _spriteReference = spriteReference;
            _addressableKey = addressableKey ?? string.Empty;
        }
        
        /// <summary>
        /// Convenient factory method for creating appearances
        /// </summary>
        public static CharacterAppearance Create(CharacterEmotion expression, CharacterPose pose = CharacterPose.Standing, int outfit = 0)
            => new(expression, pose, outfit);
        
        /// <summary>
        /// Gets addressable key using standardized naming convention
        /// </summary>
        /// <param name="characterName">Name of the character</param>
        /// <returns>Addressable key for asset loading</returns>
        public string GetAddressableKey(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                throw new ArgumentException("Character name cannot be null or empty", nameof(characterName));
            
            // Use custom key if provided, otherwise generate standard key
            if (!string.IsNullOrEmpty(_addressableKey))
                return _addressableKey;
                
            return $"char_{characterName.ToLower()}_{_expression.ToString().ToLower()}_{_pose.ToString().ToLower()}_{_outfitId:D2}";
        }
        
        /// <summary>
        /// Gets character label for batch loading
        /// </summary>
        /// <param name="characterName">Name of the character</param>
        /// <returns>Addressable label for character assets</returns>
        public string GetCharacterLabel(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                throw new ArgumentException("Character name cannot be null or empty", nameof(characterName));
                
            return $"character_{characterName.ToLower()}";
        }
        
        /// <summary>
        /// Gets expression label for batch loading common expressions
        /// </summary>
        /// <returns>Addressable label for expression assets</returns>
        public string GetExpressionLabel()
        {
            return $"expression_{_expression.ToString().ToLower()}";
        }
        
        /// <summary>
        /// Gets all addressable labels for this appearance (IAppearance implementation)
        /// </summary>
        /// <returns>Array of addressable labels for batch loading</returns>
        public string[] GetAddressableLabels()
        {
            return new[]
            {
                $"expression_{_expression.ToString().ToLower()}",
                $"pose_{_pose.ToString().ToLower()}",
                $"outfit_{_outfitId:D2}"
            };
        }
        
        /// <summary>
        /// Gets path parameters for ResourcePathResolver integration (IAppearance implementation)
        /// </summary>
        /// <param name="actorId">Actor identifier</param>
        /// <returns>Array of path parameters for template substitution</returns>
        public PathParameter[] GetPathParameters(string actorId)
        {
            return new PathParameter[]
            {
                new(PathParameterNames.ACTOR_TYPE, "Character"),
                new(PathParameterNames.ACTOR_ID, actorId),
                new(PathParameterNames.CHARACTER_NAME, actorId),
                new(PathParameterNames.APPEARANCE, $"{_expression}_{_pose}_{_outfitId:D2}"),
                new(PathParameterNames.EXPRESSION, _expression.ToString()),
                new(PathParameterNames.POSE, _pose.ToString()),
                new(PathParameterNames.OUTFIT, _outfitId.ToString("D2")),
                new(PathParameterNames.DEFAULT_EXPRESSION, CharacterEmotion.Neutral.ToString()),
                new(PathParameterNames.DEFAULT_POSE, CharacterPose.Standing.ToString()),
                new(PathParameterNames.DEFAULT_OUTFIT, "00"),
                new(PathParameterNames.ADDRESSABLES_KEY, GetAddressableKey(actorId))
            };
        }
        
        
        /// <summary>
        /// Creates appearance with different expression
        /// </summary>
        public CharacterAppearance WithExpression(CharacterEmotion expression)
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
        public static readonly CharacterAppearance Default = new(CharacterEmotion.Neutral, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Happy = new(CharacterEmotion.Happy, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Sad = new(CharacterEmotion.Sad, CharacterPose.Standing, 0);
        public static readonly CharacterAppearance Surprised = new(CharacterEmotion.Surprised, CharacterPose.Standing, 0);
    }
    
    /// <summary>
    /// Composite appearance structure for background actors with automatic resource path generation
    /// </summary>
    [Serializable]
    public struct BackgroundAppearance : IAppearance, IEquatable<BackgroundAppearance>
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
        /// Gets addressable key using standardized naming convention (IAppearance implementation)
        /// </summary>
        /// <param name="actorId">Actor ID (not used for backgrounds but required by interface)</param>
        /// <returns>Addressable key for background asset loading</returns>
        public string GetAddressableKey(string actorId)
        {
            return $"bg_{_type.ToString().ToLower()}_{_location.ToString().ToLower()}_{_variantId:D2}";
        }
        
        /// <summary>
        /// Gets all addressable labels for this background (IAppearance implementation)
        /// </summary>
        /// <returns>Array of addressable labels for batch loading</returns>
        public string[] GetAddressableLabels()
        {
            return new[]
            {
                $"background_{_type.ToString().ToLower()}",
                $"location_{_location.ToString().ToLower()}"
            };
        }
        
        /// <summary>
        /// Gets path parameters for ResourcePathResolver integration (IAppearance implementation)
        /// </summary>
        /// <param name="actorId">Actor ID (not used for backgrounds but required by interface)</param>
        /// <returns>Array of path parameters for template substitution</returns>
        public PathParameter[] GetPathParameters(string actorId)
        {
            return new PathParameter[]
            {
                new(PathParameterNames.ACTOR_TYPE, "Background"),
                new(PathParameterNames.ACTOR_ID, actorId),
                new(PathParameterNames.BACKGROUND_TYPE, _type.ToString()),
                new(PathParameterNames.LOCATION, _location.ToString()),
                new(PathParameterNames.VARIANT, _variantId.ToString("D2")),
                new(PathParameterNames.APPEARANCE, $"{_type}_{_location}_{_variantId:D2}"),
                new(PathParameterNames.DEFAULT_LOCATION, SceneLocation.Classroom.ToString()),
                new(PathParameterNames.DEFAULT_VARIANT, "00"),
                new(PathParameterNames.DEFAULT_TRANSITION, "Fade"),
                new(PathParameterNames.ADDRESSABLES_KEY, GetAddressableKey(actorId))
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
        public static readonly BackgroundAppearance Default = new(BackgroundType.Scene, SceneLocation.Classroom, 0);
        public static readonly BackgroundAppearance DefaultClassroom = new(BackgroundType.Scene, SceneLocation.Classroom, 0);
        public static readonly BackgroundAppearance DefaultLibrary = new(BackgroundType.Scene, SceneLocation.Library, 0);
        public static readonly BackgroundAppearance DefaultPark = new(BackgroundType.Environment, SceneLocation.Park, 0);
        public static readonly BackgroundAppearance Black = new(BackgroundType.Scene, SceneLocation.Abstract, 0);
    }
    
    /// <summary>
    /// Generic appearance data for other actor types
    /// </summary>
    [Serializable]
    public struct GenericAppearance : IAppearance, IEquatable<GenericAppearance>
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
        
        /// <summary>
        /// Gets addressable key using standardized naming convention (IAppearance implementation)
        /// </summary>
        /// <param name="actorId">Actor ID for addressable key generation</param>
        /// <returns>Addressable key for generic asset loading</returns>
        public string GetAddressableKey(string actorId)
        {
            return $"generic_{actorId.ToLower()}_{_appearanceId.ToLower()}_{_variantId:D2}";
        }
        
        /// <summary>
        /// Gets all addressable labels for this generic appearance (IAppearance implementation)
        /// </summary>
        /// <returns>Array of addressable labels for batch loading</returns>
        public string[] GetAddressableLabels()
        {
            return new[]
            {
                $"generic_{_appearanceId.ToLower()}",
                $"variant_{_variantId:D2}"
            };
        }
        
        /// <summary>
        /// Gets path parameters for ResourcePathResolver integration (IAppearance implementation)
        /// </summary>
        /// <param name="actorId">Actor ID for path parameter generation</param>
        /// <returns>Array of path parameters for template substitution</returns>
        public PathParameter[] GetPathParameters(string actorId)
        {
            return new PathParameter[]
            {
                new(PathParameterNames.ACTOR_TYPE, "Generic"),
                new(PathParameterNames.ACTOR_ID, actorId),
                new(PathParameterNames.APPEARANCE_ID, _appearanceId),
                new(PathParameterNames.VARIANT, _variantId.ToString("D2")),
                new(PathParameterNames.APPEARANCE, $"{_appearanceId}_{_variantId:D2}"),
                new(PathParameterNames.DISPLAY_NAME, actorId)
            };
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
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base metadata for all actors containing configuration and type information
    /// </summary>
    [Serializable]
    public abstract class ActorMetadata : ScriptableObject
    {
        [Header("Basic Information")]
        [SerializeField] protected string _actorId;
        [SerializeField] protected string _displayName;
        [SerializeField] protected string _description;
        [SerializeField, HideInInspector] protected int _actorTypeValue;
        
        [Header("Resource Settings")]
        [SerializeField] protected string _resourceBasePath = "Actors";
        [SerializeField] protected bool _preloadResources = true;
        [SerializeField] protected int _resourceCacheSize = 10;
        
        [Header("Performance Settings")]
        [SerializeField] protected bool _enableObjectPooling = true;
        [SerializeField] protected int _poolSize = 5;
        [SerializeField] protected bool _enableBatching = false;
        
        [Header("Animation Settings")]
        [SerializeField] protected float _defaultAnimationDuration = 1.0f;
        [SerializeField] protected DG.Tweening.Ease _defaultEase = DG.Tweening.Ease.OutQuad;
        [SerializeField] protected bool _enableAnimationSequences = true;
        
        // Properties
        public string ActorId
        {
            get => _actorId ?? string.Empty;
            set => _actorId = value ?? string.Empty;
        }
        
        public string DisplayName
        {
            get => _displayName ?? string.Empty;
            set => _displayName = value ?? string.Empty;
        }
        
        public string Description
        {
            get => _description ?? string.Empty;
            set => _description = value ?? string.Empty;
        }
        
        public ActorType ActorType
        {
            get => (ActorType)_actorTypeValue;
            protected set => _actorTypeValue = value.Value;
        }
        
        public string ResourceBasePath
        {
            get => _resourceBasePath ?? "Actors";
            set => _resourceBasePath = value ?? "Actors";
        }
        
        public bool PreloadResources
        {
            get => _preloadResources;
            set => _preloadResources = value;
        }
        
        public int ResourceCacheSize
        {
            get => _resourceCacheSize;
            set => _resourceCacheSize = Mathf.Max(1, value);
        }
        
        public bool EnableObjectPooling
        {
            get => _enableObjectPooling;
            set => _enableObjectPooling = value;
        }
        
        public int PoolSize
        {
            get => _poolSize;
            set => _poolSize = Mathf.Max(1, value);
        }
        
        public bool EnableBatching
        {
            get => _enableBatching;
            set => _enableBatching = value;
        }
        
        public float DefaultAnimationDuration
        {
            get => _defaultAnimationDuration;
            set => _defaultAnimationDuration = Mathf.Max(0f, value);
        }
        
        public DG.Tweening.Ease DefaultEase
        {
            get => _defaultEase;
            set => _defaultEase = value;
        }
        
        public bool EnableAnimationSequences
        {
            get => _enableAnimationSequences;
            set => _enableAnimationSequences = value;
        }
        
        /// <summary>
        /// Validates the metadata configuration
        /// </summary>
        public virtual bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            
            if (string.IsNullOrEmpty(_actorId))
                errors.Add("Actor ID cannot be empty");
            
            if (string.IsNullOrEmpty(_displayName))
                errors.Add("Display name cannot be empty");
            
            if (string.IsNullOrEmpty(_resourceBasePath))
                errors.Add("Resource base path cannot be empty");
            
            if (_resourceCacheSize <= 0)
                errors.Add("Resource cache size must be greater than 0");
            
            if (_poolSize <= 0)
                errors.Add("Pool size must be greater than 0");
            
            if (_defaultAnimationDuration < 0)
                errors.Add("Default animation duration cannot be negative");
            
            return errors.Count == 0;
        }
        
        /// <summary>
        /// Gets a summary of this metadata for debugging
        /// </summary>
        public virtual string GetSummary()
        {
            return $"ActorMetadata[{_actorId}] - {_displayName} ({ActorType}) - Pool: {_poolSize}, Cache: {_resourceCacheSize}";
        }
        
        /// <summary>
        /// Resets to default values - called when creating new instances
        /// </summary>
        protected virtual void Reset()
        {
            _resourceBasePath = "Actors";
            _preloadResources = true;
            _resourceCacheSize = 10;
            _enableObjectPooling = true;
            _poolSize = 5;
            _enableBatching = false;
            _defaultAnimationDuration = 1.0f;
            _defaultEase = DG.Tweening.Ease.OutQuad;
            _enableAnimationSequences = true;
        }
    }
    
    /// <summary>
    /// Metadata specific to character actors
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterMetadata", menuName = "Engine/Actor/Character Metadata", order = 1)]
    public class CharacterMetadata : ActorMetadata
    {
        [Header("Character Appearance")]
        [SerializeField] private CharacterExpression _defaultExpression = CharacterExpression.Neutral;
        [SerializeField] private CharacterPose _defaultPose = CharacterPose.Standing;
        [SerializeField] private int _defaultOutfit = 0;
        [SerializeField] private CharacterLookDirection _defaultLookDirection = CharacterLookDirection.Center;
        
        [Header("Available Expressions")]
        [SerializeField] private CharacterExpression[] _supportedExpressions = 
        {
            CharacterExpression.Neutral,
            CharacterExpression.Happy,
            CharacterExpression.Sad
        };
        
        [Header("Available Poses")]
        [SerializeField] private CharacterPose[] _supportedPoses =
        {
            CharacterPose.Standing,
            CharacterPose.Sitting
        };
        
        [Header("Outfits")]
        [SerializeField] private string[] _outfitNames = { "Default" };
        [SerializeField] private Color _defaultCharacterColor = Color.white;
        
        [Header("Character Animation")]
        [SerializeField] private float _expressionChangeSpeed = 0.5f;
        [SerializeField] private float _poseChangeSpeed = 1.0f;
        [SerializeField] private float _lookDirectionSpeed = 0.3f;
        
        // Properties
        public CharacterAppearance DefaultAppearance => 
            new(_defaultExpression, _defaultPose, _defaultOutfit);
        
        public CharacterExpression DefaultExpression
        {
            get => _defaultExpression;
            set => _defaultExpression = value;
        }
        
        public CharacterPose DefaultPose
        {
            get => _defaultPose;
            set => _defaultPose = value;
        }
        
        public int DefaultOutfit
        {
            get => _defaultOutfit;
            set => _defaultOutfit = Mathf.Max(0, value);
        }
        
        public CharacterLookDirection DefaultLookDirection
        {
            get => _defaultLookDirection;
            set => _defaultLookDirection = value;
        }
        
        public IReadOnlyList<CharacterExpression> SupportedExpressions =>
            Array.AsReadOnly(_supportedExpressions ?? new CharacterExpression[0]);
        
        public IReadOnlyList<CharacterPose> SupportedPoses =>
            Array.AsReadOnly(_supportedPoses ?? new CharacterPose[0]);
        
        public IReadOnlyList<string> OutfitNames =>
            Array.AsReadOnly(_outfitNames ?? new string[0]);
        
        public Color DefaultCharacterColor
        {
            get => _defaultCharacterColor;
            set => _defaultCharacterColor = value;
        }
        
        public float ExpressionChangeSpeed
        {
            get => _expressionChangeSpeed;
            set => _expressionChangeSpeed = Mathf.Max(0f, value);
        }
        
        public float PoseChangeSpeed
        {
            get => _poseChangeSpeed;
            set => _poseChangeSpeed = Mathf.Max(0f, value);
        }
        
        public float LookDirectionSpeed
        {
            get => _lookDirectionSpeed;
            set => _lookDirectionSpeed = Mathf.Max(0f, value);
        }
        
        public CharacterMetadata()
        {
            ActorType = ActorType.Character;
        }
        
        /// <summary>
        /// Checks if an appearance is valid for this character
        /// </summary>
        public bool IsValidAppearance(CharacterAppearance appearance)
        {
            return Array.IndexOf(_supportedExpressions, appearance.Expression) >= 0 &&
                   Array.IndexOf(_supportedPoses, appearance.Pose) >= 0 &&
                   appearance.OutfitId < (_outfitNames?.Length ?? 0);
        }
        
        /// <summary>
        /// Gets the resource path for a specific appearance
        /// </summary>
        public string GetAppearanceResourcePath(CharacterAppearance appearance)
        {
            return appearance.GetResourcePath(ActorId, ResourceBasePath);
        }
        
        public override bool Validate(out List<string> errors)
        {
            var isValid = base.Validate(out errors);
            
            if (_supportedExpressions == null || _supportedExpressions.Length == 0)
                errors.Add("At least one supported expression must be defined");
            
            if (_supportedPoses == null || _supportedPoses.Length == 0)
                errors.Add("At least one supported pose must be defined");
            
            if (_outfitNames == null || _outfitNames.Length == 0)
                errors.Add("At least one outfit must be defined");
            
            if (!IsValidAppearance(DefaultAppearance))
                errors.Add("Default appearance is not valid according to supported expressions/poses");
            
            if (_expressionChangeSpeed < 0)
                errors.Add("Expression change speed cannot be negative");
            
            if (_poseChangeSpeed < 0)
                errors.Add("Pose change speed cannot be negative");
            
            if (_lookDirectionSpeed < 0)
                errors.Add("Look direction speed cannot be negative");
            
            return errors.Count == 0;
        }
        
        protected override void Reset()
        {
            base.Reset();
            ActorType = ActorType.Character;
            _defaultExpression = CharacterExpression.Neutral;
            _defaultPose = CharacterPose.Standing;
            _defaultOutfit = 0;
            _defaultLookDirection = CharacterLookDirection.Center;
            _supportedExpressions = new[] { CharacterExpression.Neutral, CharacterExpression.Happy, CharacterExpression.Sad };
            _supportedPoses = new[] { CharacterPose.Standing, CharacterPose.Sitting };
            _outfitNames = new[] { "Default" };
            _defaultCharacterColor = Color.white;
            _expressionChangeSpeed = 0.5f;
            _poseChangeSpeed = 1.0f;
            _lookDirectionSpeed = 0.3f;
        }
        
        public override string GetSummary()
        {
            return $"CharacterMetadata[{ActorId}] - {DisplayName} - Expressions: {SupportedExpressions.Count}, Poses: {SupportedPoses.Count}, Outfits: {OutfitNames.Count}";
        }
    }
    
    /// <summary>
    /// Metadata specific to background actors
    /// </summary>
    [CreateAssetMenu(fileName = "BackgroundMetadata", menuName = "Engine/Actor/Background Metadata", order = 2)]
    public class BackgroundMetadata : ActorMetadata
    {
        [Header("Background Appearance")]
        [SerializeField] private BackgroundType _defaultBackgroundType = BackgroundType.Scene;
        [SerializeField] private SceneLocation _defaultLocation = SceneLocation.Classroom;
        [SerializeField] private int _defaultVariant = 0;
        
        [Header("Available Locations")]
        [SerializeField] private SceneLocation[] _supportedLocations =
        {
            SceneLocation.Classroom,
            SceneLocation.Library,
            SceneLocation.Cafeteria
        };
        
        [Header("Variants")]
        [SerializeField] private string[] _variantNames = { "Day", "Night" };
        
        [Header("Scene Settings")]
        [SerializeField] private SceneTransitionType _defaultTransition = SceneTransitionType.Fade;
        [SerializeField] private float _transitionDuration = 2.0f;
        [SerializeField] private float _parallaxFactor = 1.0f;
        [SerializeField] private bool _canBeMainBackground = true;
        
        // Properties
        public BackgroundAppearance DefaultAppearance =>
            new(_defaultBackgroundType, _defaultLocation, _defaultVariant);
        
        public BackgroundType DefaultBackgroundType
        {
            get => _defaultBackgroundType;
            set => _defaultBackgroundType = value;
        }
        
        public SceneLocation DefaultLocation
        {
            get => _defaultLocation;
            set => _defaultLocation = value;
        }
        
        public int DefaultVariant
        {
            get => _defaultVariant;
            set => _defaultVariant = Mathf.Max(0, value);
        }
        
        public IReadOnlyList<SceneLocation> SupportedLocations =>
            Array.AsReadOnly(_supportedLocations ?? new SceneLocation[0]);
        
        public IReadOnlyList<string> VariantNames =>
            Array.AsReadOnly(_variantNames ?? new string[0]);
        
        public SceneTransitionType DefaultTransition
        {
            get => _defaultTransition;
            set => _defaultTransition = value;
        }
        
        public float TransitionDuration
        {
            get => _transitionDuration;
            set => _transitionDuration = Mathf.Max(0f, value);
        }
        
        public float ParallaxFactor
        {
            get => _parallaxFactor;
            set => _parallaxFactor = value;
        }
        
        public bool CanBeMainBackground
        {
            get => _canBeMainBackground;
            set => _canBeMainBackground = value;
        }
        
        public BackgroundMetadata()
        {
            ActorType = ActorType.Background;
        }
        
        /// <summary>
        /// Checks if an appearance is valid for this background
        /// </summary>
        public bool IsValidAppearance(BackgroundAppearance appearance)
        {
            return Array.IndexOf(_supportedLocations, appearance.Location) >= 0 &&
                   appearance.VariantId < (_variantNames?.Length ?? 0);
        }
        
        /// <summary>
        /// Gets the resource path for a specific appearance
        /// </summary>
        public string GetAppearanceResourcePath(BackgroundAppearance appearance)
        {
            return appearance.GetResourcePath(ResourceBasePath);
        }
        
        public override bool Validate(out List<string> errors)
        {
            var isValid = base.Validate(out errors);
            
            if (_supportedLocations == null || _supportedLocations.Length == 0)
                errors.Add("At least one supported location must be defined");
            
            if (_variantNames == null || _variantNames.Length == 0)
                errors.Add("At least one variant must be defined");
            
            if (!IsValidAppearance(DefaultAppearance))
                errors.Add("Default appearance is not valid according to supported locations/variants");
            
            if (_transitionDuration < 0)
                errors.Add("Transition duration cannot be negative");
            
            return errors.Count == 0;
        }
        
        protected override void Reset()
        {
            base.Reset();
            ActorType = ActorType.Background;
            _defaultBackgroundType = BackgroundType.Scene;
            _defaultLocation = SceneLocation.Classroom;
            _defaultVariant = 0;
            _supportedLocations = new[] { SceneLocation.Classroom, SceneLocation.Library, SceneLocation.Cafeteria };
            _variantNames = new[] { "Day", "Night" };
            _defaultTransition = SceneTransitionType.Fade;
            _transitionDuration = 2.0f;
            _parallaxFactor = 1.0f;
            _canBeMainBackground = true;
        }
        
        public override string GetSummary()
        {
            return $"BackgroundMetadata[{ActorId}] - {DisplayName} - Locations: {SupportedLocations.Count}, Variants: {VariantNames.Count}";
        }
    }
    
    /// <summary>
    /// Registry for managing actor metadata
    /// </summary>
    public static class ActorMetadataRegistry
    {
        private static readonly Dictionary<string, ActorMetadata> _metadataCache = new();
        private static readonly Dictionary<ActorType, List<ActorMetadata>> _metadataByType = new();
        
        /// <summary>
        /// Registers metadata for an actor
        /// </summary>
        public static void RegisterMetadata(ActorMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            
            if (string.IsNullOrEmpty(metadata.ActorId))
            {
                Debug.LogError("[ActorMetadataRegistry] Cannot register metadata with empty ID");
                return;
            }
            
            _metadataCache[metadata.ActorId] = metadata;
            
            if (!_metadataByType.TryGetValue(metadata.ActorType, out var list))
            {
                list = new List<ActorMetadata>();
                _metadataByType[metadata.ActorType] = list;
            }
            
            if (!list.Contains(metadata))
                list.Add(metadata);
            
            Debug.Log($"[ActorMetadataRegistry] Registered metadata: {metadata.ActorId} ({metadata.ActorType})");
        }
        
        /// <summary>
        /// Gets metadata by actor ID
        /// </summary>
        public static T GetMetadata<T>(string actorId) where T : ActorMetadata
        {
            if (string.IsNullOrEmpty(actorId))
                return null;
            
            return _metadataCache.TryGetValue(actorId, out var metadata) ? metadata as T : null;
        }
        
        /// <summary>
        /// Gets all metadata of a specific type
        /// </summary>
        public static IReadOnlyList<T> GetMetadataByType<T>() where T : ActorMetadata
        {
            var actorType = typeof(T) == typeof(CharacterMetadata) ? ActorType.Character :
                           typeof(T) == typeof(BackgroundMetadata) ? ActorType.Background :
                           ActorType.Character; // Default fallback
            
            if (_metadataByType.TryGetValue(actorType, out var list))
            {
                return list.OfType<T>().ToList().AsReadOnly();
            }
            
            return new List<T>().AsReadOnly();
        }
        
        /// <summary>
        /// Removes metadata from registry
        /// </summary>
        public static void UnregisterMetadata(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
                return;
            
            if (_metadataCache.TryGetValue(actorId, out var metadata))
            {
                _metadataCache.Remove(actorId);
                
                if (_metadataByType.TryGetValue(metadata.ActorType, out var list))
                {
                    list.Remove(metadata);
                }
                
                Debug.Log($"[ActorMetadataRegistry] Unregistered metadata: {actorId}");
            }
        }
        
        /// <summary>
        /// Clears all registered metadata
        /// </summary>
        public static void Clear()
        {
            _metadataCache.Clear();
            _metadataByType.Clear();
            Debug.Log("[ActorMetadataRegistry] Cleared all metadata");
        }
        
        /// <summary>
        /// Gets debug information about registered metadata
        /// </summary>
        public static string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Actor Metadata Registry ===");
            info.AppendLine($"Total Registered: {_metadataCache.Count}");
            
            foreach (var kvp in _metadataByType)
            {
                info.AppendLine($"{kvp.Key}: {kvp.Value.Count} entries");
            }
            
            info.AppendLine();
            info.AppendLine("Registered Metadata:");
            foreach (var metadata in _metadataCache.Values)
            {
                info.AppendLine($"  {metadata.GetSummary()}");
            }
            
            return info.ToString();
        }
    }
}
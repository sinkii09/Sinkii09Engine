using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base serializable state for all actors with enum-safe serialization for performance
    /// </summary>
    [Serializable]
    public class ActorState
    {
        [SerializeField] private string _id;
        [SerializeField] private int _actorTypeValue; // ActorType serialized as int for efficiency
        [SerializeField] private string _displayName;
        [SerializeField] private bool _visible;
        [SerializeField] private int _visibilityStateValue; // Enum as int
        [SerializeField] private Vector3 _position;
        [SerializeField] private Quaternion _rotation;
        [SerializeField] private Vector3 _scale = Vector3.one;
        [SerializeField] private Color _tintColor = Color.white;
        [SerializeField] private float _alpha = 1.0f;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private DateTime _stateTimestamp;
        
        // Custom data storage for specialized states
        [SerializeField] private List<string> _customDataKeys = new();
        [SerializeField] private List<string> _customDataValues = new();
        
        // Properties with type-safe access
        public string Id
        {
            get => _id ?? string.Empty;
            set => _id = value ?? string.Empty;
        }
        
        public ActorType ActorType
        {
            get => (ActorType)_actorTypeValue;
            set => _actorTypeValue = value.Value;
        }
        
        public string DisplayName
        {
            get => _displayName ?? string.Empty;
            set => _displayName = value ?? string.Empty;
        }
        
        public bool Visible
        {
            get => _visible;
            set => _visible = value;
        }
        
        public ActorVisibilityState VisibilityState
        {
            get => (ActorVisibilityState)_visibilityStateValue;
            set => _visibilityStateValue = (int)value;
        }
        
        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }
        
        public Quaternion Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }
        
        public Vector3 Scale
        {
            get => _scale;
            set => _scale = value;
        }
        
        public Color TintColor
        {
            get => _tintColor;
            set => _tintColor = value;
        }
        
        public float Alpha
        {
            get => _alpha;
            set => _alpha = Mathf.Clamp01(value);
        }
        
        public int SortingOrder
        {
            get => _sortingOrder;
            set => _sortingOrder = value;
        }
        
        public DateTime StateTimestamp
        {
            get => _stateTimestamp;
            set => _stateTimestamp = value;
        }
        
        // Custom data management
        public void SetCustomData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            
            var index = _customDataKeys.IndexOf(key);
            if (index >= 0)
            {
                _customDataValues[index] = value ?? string.Empty;
            }
            else
            {
                _customDataKeys.Add(key);
                _customDataValues.Add(value ?? string.Empty);
            }
        }
        
        public string GetCustomData(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            
            var index = _customDataKeys.IndexOf(key);
            return index >= 0 ? _customDataValues[index] : string.Empty;
        }
        
        public bool HasCustomData(string key)
        {
            return !string.IsNullOrEmpty(key) && _customDataKeys.Contains(key);
        }
        
        public void RemoveCustomData(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            
            var index = _customDataKeys.IndexOf(key);
            if (index >= 0)
            {
                _customDataKeys.RemoveAt(index);
                _customDataValues.RemoveAt(index);
            }
        }
        
        public IReadOnlyList<string> GetCustomDataKeys() => _customDataKeys.AsReadOnly();
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public ActorState()
        {
            _stateTimestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Constructor with basic actor information
        /// </summary>
        public ActorState(string id, ActorType actorType)
        {
            _id = id ?? string.Empty;
            _actorTypeValue = actorType.Value;
            _stateTimestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Applies this state to the given actor
        /// </summary>
        public virtual void ApplyToActor(IActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            
            if (actor.Id != Id)
            {
                Debug.LogWarning($"[ActorState] ID mismatch when applying state. Expected: {Id}, Actual: {actor.Id}");
            }
            
            actor.DisplayName = DisplayName;
            actor.Visible = Visible;
            actor.Position = Position;
            actor.Rotation = Rotation;
            actor.Scale = Scale;
            actor.TintColor = TintColor;
            actor.Alpha = Alpha;
            actor.SortingOrder = SortingOrder;
        }
        
        /// <summary>
        /// Captures state from the given actor
        /// </summary>
        public virtual void CaptureFromActor(IActor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            
            Id = actor.Id;
            ActorType = actor.ActorType;
            DisplayName = actor.DisplayName;
            Visible = actor.Visible;
            VisibilityState = actor.VisibilityState;
            Position = actor.Position;
            Rotation = actor.Rotation;
            Scale = actor.Scale;
            TintColor = actor.TintColor;
            Alpha = actor.Alpha;
            SortingOrder = actor.SortingOrder;
            StateTimestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Validates if this state can be applied to the given actor
        /// </summary>
        public virtual bool CanApplyToActor(IActor actor)
        {
            if (actor == null)
                return false;
            
            // Basic validation - can be overridden in specialized states
            return actor.ActorType == ActorType;
        }
        
        /// <summary>
        /// Creates a copy of this state
        /// </summary>
        public virtual ActorState Clone()
        {
            var clone = new ActorState();
            CopyTo(clone);
            return clone;
        }
        
        /// <summary>
        /// Copies this state's data to another state
        /// </summary>
        protected virtual void CopyTo(ActorState other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            
            other._id = _id;
            other._actorTypeValue = _actorTypeValue;
            other._displayName = _displayName;
            other._visible = _visible;
            other._visibilityStateValue = _visibilityStateValue;
            other._position = _position;
            other._rotation = _rotation;
            other._scale = _scale;
            other._tintColor = _tintColor;
            other._alpha = _alpha;
            other._sortingOrder = _sortingOrder;
            other._stateTimestamp = _stateTimestamp;
            
            other._customDataKeys = new List<string>(_customDataKeys);
            other._customDataValues = new List<string>(_customDataValues);
        }
        
        public override string ToString()
        {
            return $"ActorState[{Id}] - {ActorType} - Visible: {Visible}, Position: {Position}";
        }
    }
    
    /// <summary>
    /// Specialized state for character actors with enum-based serialization
    /// </summary>
    [Serializable]
    public class CharacterState : ActorState
    {
        [SerializeField] private int _expressionValue;
        [SerializeField] private int _poseValue;
        [SerializeField] private int _lookDirectionValue;
        [SerializeField] private int _outfitId;
        [SerializeField] private Color _characterColor = Color.white;
        [SerializeField] private string _currentEmotion = string.Empty;
        
        // Type-safe properties with enum conversion
        public CharacterEmotion Expression
        {
            get => (CharacterEmotion)_expressionValue;
            set => _expressionValue = (int)value;
        }
        
        public CharacterPose Pose
        {
            get => (CharacterPose)_poseValue;
            set => _poseValue = (int)value;
        }
        
        public CharacterLookDirection LookDirection
        {
            get => (CharacterLookDirection)_lookDirectionValue;
            set => _lookDirectionValue = (int)value;
        }
        
        public int OutfitId
        {
            get => _outfitId;
            set => _outfitId = Mathf.Max(0, value);
        }
        
        public Color CharacterColor
        {
            get => _characterColor;
            set => _characterColor = value;
        }
        
        public string CurrentEmotion
        {
            get => _currentEmotion ?? string.Empty;
            set => _currentEmotion = value ?? string.Empty;
        }
        
        public CharacterAppearance Appearance
        {
            get => new CharacterAppearance(Expression, Pose, OutfitId);
            set
            {
                Expression = value.Expression;
                Pose = value.Pose;
                OutfitId = value.OutfitId;
            }
        }
        
        public CharacterState() : base()
        {
        }
        
        public CharacterState(string id) : base(id, ActorType.Character)
        {
        }
        
        public override void ApplyToActor(IActor actor)
        {
            base.ApplyToActor(actor);
            
            if (actor is ICharacterActor character)
            {
                character.Appearance = Appearance;
                character.LookDirection = LookDirection;
                character.CharacterColor = CharacterColor;
                character.CurrentEmotion = CurrentEmotion;
            }
        }
        
        public override void CaptureFromActor(IActor actor)
        {
            base.CaptureFromActor(actor);
            
            if (actor is ICharacterActor character)
            {
                Appearance = character.Appearance;
                LookDirection = character.LookDirection;
                CharacterColor = character.CharacterColor;
                CurrentEmotion = character.CurrentEmotion;
            }
        }
        
        public override bool CanApplyToActor(IActor actor)
        {
            return base.CanApplyToActor(actor) && actor is ICharacterActor;
        }
        
        public override ActorState Clone()
        {
            var clone = new CharacterState();
            CopyTo(clone);
            return clone;
        }
        
        protected override void CopyTo(ActorState other)
        {
            base.CopyTo(other);
            
            if (other is CharacterState characterState)
            {
                characterState._expressionValue = _expressionValue;
                characterState._poseValue = _poseValue;
                characterState._lookDirectionValue = _lookDirectionValue;
                characterState._outfitId = _outfitId;
                characterState._characterColor = _characterColor;
                characterState._currentEmotion = _currentEmotion;
            }
        }
        
        public override string ToString()
        {
            return $"CharacterState[{Id}] - {Expression}/{Pose} - Visible: {Visible}, Position: {Position}";
        }
    }
    
    /// <summary>
    /// Specialized state for background actors with scene-specific data
    /// </summary>
    [Serializable]
    public class BackgroundState : ActorState
    {
        [SerializeField] private int _backgroundTypeValue;
        [SerializeField] private int _locationValue;
        [SerializeField] private int _variantId;
        [SerializeField] private int _transitionTypeValue;
        [SerializeField] private float _parallaxFactor = 1.0f;
        [SerializeField] private bool _isMainBackground;
        
        // Type-safe properties with enum conversion
        public BackgroundType BackgroundType
        {
            get => (BackgroundType)_backgroundTypeValue;
            set => _backgroundTypeValue = (int)value;
        }
        
        public SceneLocation Location
        {
            get => (SceneLocation)_locationValue;
            set => _locationValue = (int)value;
        }
        
        public int VariantId
        {
            get => _variantId;
            set => _variantId = Mathf.Max(0, value);
        }
        
        public SceneTransitionType TransitionType
        {
            get => (SceneTransitionType)_transitionTypeValue;
            set => _transitionTypeValue = (int)value;
        }
        
        public float ParallaxFactor
        {
            get => _parallaxFactor;
            set => _parallaxFactor = value;
        }
        
        public bool IsMainBackground
        {
            get => _isMainBackground;
            set => _isMainBackground = value;
        }
        
        public BackgroundAppearance Appearance
        {
            get => new BackgroundAppearance(BackgroundType, Location, VariantId);
            set
            {
                BackgroundType = value.Type;
                Location = value.Location;
                VariantId = value.VariantId;
            }
        }
        
        public BackgroundState() : base()
        {
        }
        
        public BackgroundState(string id) : base(id, ActorType.Background)
        {
        }
        
        public override void ApplyToActor(IActor actor)
        {
            base.ApplyToActor(actor);
            
            if (actor is IBackgroundActor background)
            {
                background.Appearance = Appearance;
                background.TransitionType = TransitionType;
                background.ParallaxFactor = ParallaxFactor;
                background.IsMainBackground = IsMainBackground;
            }
        }
        
        public override void CaptureFromActor(IActor actor)
        {
            base.CaptureFromActor(actor);
            
            if (actor is IBackgroundActor background)
            {
                Appearance = background.Appearance;
                TransitionType = background.TransitionType;
                ParallaxFactor = background.ParallaxFactor;
                IsMainBackground = background.IsMainBackground;
            }
        }
        
        public override bool CanApplyToActor(IActor actor)
        {
            return base.CanApplyToActor(actor) && actor is IBackgroundActor;
        }
        
        public override ActorState Clone()
        {
            var clone = new BackgroundState();
            CopyTo(clone);
            return clone;
        }
        
        protected override void CopyTo(ActorState other)
        {
            base.CopyTo(other);
            
            if (other is BackgroundState backgroundState)
            {
                backgroundState._backgroundTypeValue = _backgroundTypeValue;
                backgroundState._locationValue = _locationValue;
                backgroundState._variantId = _variantId;
                backgroundState._transitionTypeValue = _transitionTypeValue;
                backgroundState._parallaxFactor = _parallaxFactor;
                backgroundState._isMainBackground = _isMainBackground;
            }
        }
        
        public override string ToString()
        {
            return $"BackgroundState[{Id}] - {Location} V{VariantId} - Main: {IsMainBackground}";
        }
    }
}
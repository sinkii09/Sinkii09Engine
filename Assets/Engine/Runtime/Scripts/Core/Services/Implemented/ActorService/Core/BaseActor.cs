using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Generic base actor implementation with strongly-typed appearance support
    /// </summary>
    /// <typeparam name="TAppearance">The strongly-typed appearance type</typeparam>
    public abstract class BaseActor<TAppearance> : MonoBehaviour, IActor<TAppearance>
        where TAppearance : struct, IAppearance
    {
        [Header("Actor Identity")]
        [SerializeField] protected string _id;
        [SerializeField] protected string _displayName;
        
        [Header("Actor State")]
        [SerializeField] protected TAppearance _appearance;
        [SerializeField] protected bool _visible = true;
        [SerializeField] protected Color _tintColor = Color.white;
        [SerializeField] protected float _alpha = 1.0f;
        [SerializeField] protected int _sortingOrder = 0;
        
        // State tracking
        protected ActorVisibilityState _visibilityState = ActorVisibilityState.Visible;
        protected ActorLoadState _loadState = ActorLoadState.Unloaded;
        protected bool _hasError = false;
        protected string _lastError = string.Empty;
        protected float _loadProgress = 0f;
        
        // Services
        protected IResourceService _resourceService;
        protected bool _disposed = false;
        
        // Events
        public event Action<IActor> OnLoaded;
        public event Action<IActor> OnUnloaded;
        public event Action<IActor, string> OnError;
        public event Action<IActor, bool> OnVisibilityChanged;
        
        // Properties
        public string Id => _id;
        public abstract ActorType ActorType { get; }
        
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }
        
        /// <summary>
        /// Strongly-typed appearance property
        /// </summary>
        public TAppearance Appearance
        {
            get => _appearance;
            set => _appearance = value;
        }
        
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    gameObject.SetActive(value);
                    _visibilityState = value ? ActorVisibilityState.Visible : ActorVisibilityState.Hidden;
                    OnVisibilityChanged?.Invoke(this, value);
                }
            }
        }
        
        public ActorVisibilityState VisibilityState => _visibilityState;
        public ActorLoadState LoadState => _loadState;
        
        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }
        
        public Quaternion Rotation
        {
            get => transform.rotation;
            set => transform.rotation = value;
        }
        
        public Vector3 Scale
        {
            get => transform.localScale;
            set => transform.localScale = value;
        }
        
        public virtual Color TintColor
        {
            get => _tintColor;
            set => _tintColor = value;
        }
        
        public virtual float Alpha
        {
            get => _alpha;
            set => _alpha = Mathf.Clamp01(value);
        }
        
        public int SortingOrder
        {
            get => _sortingOrder;
            set => _sortingOrder = value;
        }
        
        public bool IsLoaded => _loadState == ActorLoadState.Loaded;
        public float LoadProgress => _loadProgress;
        public bool HasError => _hasError;
        public string LastError => _lastError;
        
        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        
        // Polymorphic appearance access
        public IAppearance GetAppearance() => _appearance;
        
        public async UniTask SetAppearanceAsync(IAppearance appearance, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            if (appearance is TAppearance typedAppearance)
            {
                await ChangeAppearanceAsync(typedAppearance, duration, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Invalid appearance type. Expected {typeof(TAppearance).Name}, got {appearance?.GetType().Name ?? "null"}");
            }
        }
        
        // Abstract methods for specialized implementations
        protected abstract UniTask LoadAppearanceAssetsAsync(TAppearance appearance, CancellationToken cancellationToken);
        protected abstract UniTask AnimateAppearanceChangeAsync(TAppearance oldAppearance, TAppearance newAppearance, float duration, CancellationToken cancellationToken);
        
        // Virtual methods that can be overridden
        protected virtual void Awake()
        {
            if (string.IsNullOrEmpty(_id))
                _id = gameObject.name;
                
            if (string.IsNullOrEmpty(_displayName))
                _displayName = _id;
                
            _resourceService = Engine.GetService<IResourceService>();
        }
        
        protected virtual void Start()
        {
            // Initialize with current settings
            gameObject.SetActive(_visible);
        }
        
        // IActor implementation
        public virtual async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _loadState = ActorLoadState.Loading;
                _loadProgress = 0.1f;
                
                // Load initial appearance
                await LoadAppearanceAssetsAsync(_appearance, cancellationToken);
                _loadProgress = 0.8f;
                
                // Apply initial settings
                gameObject.SetActive(_visible);
                _loadProgress = 1.0f;
                
                _loadState = ActorLoadState.Loaded;
                OnLoaded?.Invoke(this);
                
                Debug.Log($"[BaseActor] Initialized actor: {Id}");
            }
            catch (Exception ex)
            {
                _hasError = true;
                _lastError = ex.Message;
                _loadState = ActorLoadState.Error;
                OnError?.Invoke(this, ex.Message);
                throw;
            }
        }
        
        public virtual async UniTask LoadResourcesAsync(CancellationToken cancellationToken = default)
        {
            await LoadAppearanceAssetsAsync(_appearance, cancellationToken);
        }
        
        public virtual async UniTask UnloadResourcesAsync(CancellationToken cancellationToken = default)
        {
            _loadState = ActorLoadState.Unloading;
            
            // Stop all animations
            StopAllAnimations();
            
            // Unload resources (implementation specific)
            await UniTask.CompletedTask;
            
            _loadState = ActorLoadState.Unloaded;
            OnUnloaded?.Invoke(this);
        }
        
        public virtual async UniTask DestroyAsync(CancellationToken cancellationToken = default)
        {
            await UnloadResourcesAsync(cancellationToken);
            
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Type-safe appearance change
        /// </summary>
        public virtual async UniTask ChangeAppearanceAsync(TAppearance newAppearance, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var oldAppearance = _appearance;
            _appearance = newAppearance;
            
            try
            {
                // Load new appearance assets
                await LoadAppearanceAssetsAsync(newAppearance, cancellationToken);
                
                // Apply visual changes with animation
                await AnimateAppearanceChangeAsync(oldAppearance, newAppearance, duration, cancellationToken);
            }
            catch (Exception ex)
            {
                // Revert on failure
                _appearance = oldAppearance;
                _hasError = true;
                _lastError = ex.Message;
                OnError?.Invoke(this, ex.Message);
                throw;
            }
        }
        
        // Animation methods
        public virtual async UniTask ChangePositionAsync(Vector3 position, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            if (duration <= 0)
            {
                Position = position;
                return;
            }
            
            await transform.DOMove(position, duration).SetEase(ease).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public virtual async UniTask ChangeRotationAsync(Quaternion rotation, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            if (duration <= 0)
            {
                Rotation = rotation;
                return;
            }
            
            await transform.DORotateQuaternion(rotation, duration).SetEase(ease).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public virtual async UniTask ChangeScaleAsync(Vector3 scale, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            if (duration <= 0)
            {
                Scale = scale;
                return;
            }
            
            await transform.DOScale(scale, duration).SetEase(ease).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public virtual async UniTask ChangeVisibilityAsync(bool visible, float duration = 1.0f, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            if (duration <= 0)
            {
                Visible = visible;
                return;
            }
            
            _visibilityState = visible ? ActorVisibilityState.FadingIn : ActorVisibilityState.FadingOut;
            
            var targetAlpha = visible ? 1.0f : 0.0f;
            await ChangeAlphaAsync(targetAlpha, duration, ease, cancellationToken);
            
            Visible = visible;
        }
        
        public virtual async UniTask ChangeAlphaAsync(float alpha, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            alpha = Mathf.Clamp01(alpha);
            
            if (duration <= 0)
            {
                Alpha = alpha;
                return;
            }
            
            await DOTween.To(() => Alpha, x => Alpha = x, alpha, duration).SetEase(ease).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public virtual async UniTask ChangeTintColorAsync(Color color, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            if (duration <= 0)
            {
                TintColor = color;
                return;
            }
            
            await DOTween.To(() => TintColor, x => TintColor = x, color, duration).SetEase(ease).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public virtual async UniTask PlayAnimationSequenceAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            // Default implementation - override in specialized actors
            Debug.LogWarning($"[BaseActor] Animation sequence '{sequenceName}' not implemented for {GetType().Name}");
            await UniTask.CompletedTask;
        }
        
        public virtual void StopAllAnimations()
        {
            transform.DOKill();
            DOTween.Kill(this);
        }
        
        // State management
        public virtual ActorState GetState()
        {
            return new ActorState
            {
                Id = Id,
                ActorType = ActorType,
                Visible = Visible,
                Position = Position,
                Rotation = Rotation,
                Scale = Scale,
                TintColor = TintColor
            };
        }
        
        public virtual async UniTask ApplyStateAsync(ActorState state, float duration = 0f, CancellationToken cancellationToken = default)
        {
            var tasks = new[]
            {
                ChangePositionAsync(state.Position, duration, cancellationToken: cancellationToken),
                ChangeScaleAsync(state.Scale, duration, cancellationToken: cancellationToken),
                ChangeTintColorAsync(state.TintColor, duration, cancellationToken: cancellationToken),
                ChangeVisibilityAsync(state.Visible, duration, cancellationToken: cancellationToken)
            };
            
            await UniTask.WhenAll(tasks);
            
            // Apply rotation separately (quaternion interpolation)
            await ChangeRotationAsync(state.Rotation, duration, cancellationToken: cancellationToken);
        }
        
        public virtual bool CanApplyState(ActorState state)
        {
            return state != null && state.ActorType == ActorType;
        }
        
        public virtual async UniTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            await LoadAppearanceAssetsAsync(_appearance, cancellationToken);
        }
        
        public virtual bool ValidateConfiguration(out string[] errors)
        {
            var errorList = new System.Collections.Generic.List<string>();
            
            if (string.IsNullOrEmpty(Id))
                errorList.Add("Actor ID cannot be empty");
                
            if (_resourceService == null)
                errorList.Add("ResourceService not found");
            
            errors = errorList.ToArray();
            return errorList.Count == 0;
        }
        
        public virtual string GetDebugInfo()
        {
            return $"Actor: {Id} ({GetType().Name})\n" +
                   $"State: {LoadState}\n" +
                   $"Visible: {Visible}\n" +
                   $"Position: {Position}\n" +
                   $"Appearance: {_appearance}";
        }
        
        // Disposal
        public virtual void Dispose()
        {
            if (_disposed) return;
            
            StopAllAnimations();
            
            OnLoaded = null;
            OnUnloaded = null;
            OnError = null;
            OnVisibilityChanged = null;
            
            _disposed = true;
        }
        
        protected virtual void OnDestroy()
        {
            Dispose();
        }
    }
}
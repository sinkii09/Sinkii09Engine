using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base MonoBehaviour implementation of IActor with DOTween Pro integration
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class MonoBehaviourActor : MonoBehaviour, IActor
    {
        [Header("Actor Settings")]
        [SerializeField] protected string _id;
        [SerializeField] protected string _displayName;
        [SerializeField] protected int _actorTypeValue;
        [SerializeField] protected bool _visible = true;
        [SerializeField] protected int _sortingOrder = 0;
        
        [Header("Animation Settings")]
        [SerializeField] protected float _defaultAnimationDuration = 1.0f;
        [SerializeField] protected Ease _defaultEase = Ease.OutQuad;
        [SerializeField] protected bool _enableAnimationSequences = true;
        
        // Core components
        protected SpriteRenderer _renderer;
        protected ActorResourceManager _resourceManager;
        protected ActorAnimationSystem _animationSystem;
        
        // State management
        protected ActorLoadState _loadState = ActorLoadState.Unloaded;
        protected ActorVisibilityState _visibilityState = ActorVisibilityState.Hidden;
        protected float _loadProgress = 0f;
        protected bool _hasError = false;
        protected string _lastError = string.Empty;
        
        // Cancellation and lifecycle
        protected CancellationTokenSource _disposeCts;
        protected CancellationTokenSource _operationsCts;
        protected readonly object _stateLock = new();
        
        // DOTween management
        protected readonly List<Tween> _activeTweens = new();
        protected Sequence _currentSequence;
        
        // Events
        public event Action<IActor> OnLoaded;
        public event Action<IActor> OnUnloaded;
        public event Action<IActor, string> OnError;
        public event Action<IActor, bool> OnVisibilityChanged;
        
        // === IActor Properties ===
        
        public string Id
        {
            get => _id ?? string.Empty;
            protected set => _id = value ?? string.Empty;
        }
        
        public ActorType ActorType
        {
            get => (ActorType)_actorTypeValue;
            protected set => _actorTypeValue = value.Value;
        }
        
        public string DisplayName
        {
            get => _displayName ?? string.Empty;
            set => _displayName = value ?? string.Empty;
        }
        
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    UpdateVisibility();
                    OnVisibilityChanged?.Invoke(this, value);
                }
            }
        }
        
        public ActorVisibilityState VisibilityState
        {
            get
            {
                lock (_stateLock)
                {
                    return _visibilityState;
                }
            }
            protected set
            {
                lock (_stateLock)
                {
                    _visibilityState = value;
                }
            }
        }
        
        public ActorLoadState LoadState
        {
            get
            {
                lock (_stateLock)
                {
                    return _loadState;
                }
            }
            protected set
            {
                lock (_stateLock)
                {
                    _loadState = value;
                }
            }
        }
        
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
        
        public Color TintColor
        {
            get => _renderer != null ? _renderer.color : Color.white;
            set
            {
                if (_renderer != null)
                {
                    var color = value;
                    color.a = Alpha; // Preserve alpha
                    _renderer.color = color;
                }
            }
        }
        
        public float Alpha
        {
            get => _renderer != null ? _renderer.color.a : 1f;
            set
            {
                if (_renderer != null)
                {
                    var color = _renderer.color;
                    color.a = Mathf.Clamp01(value);
                    _renderer.color = color;
                }
            }
        }
        
        public int SortingOrder
        {
            get => _sortingOrder;
            set
            {
                _sortingOrder = value;
                if (_renderer != null)
                    _renderer.sortingOrder = value;
            }
        }
        
        public abstract object Appearance { get; set; }
        
        public bool IsLoaded => LoadState == ActorLoadState.Loaded;
        
        public float LoadProgress
        {
            get
            {
                lock (_stateLock)
                {
                    return _loadProgress;
                }
            }
            protected set
            {
                lock (_stateLock)
                {
                    _loadProgress = Mathf.Clamp01(value);
                }
            }
        }
        
        public bool HasError
        {
            get
            {
                lock (_stateLock)
                {
                    return _hasError;
                }
            }
        }
        
        public string LastError
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastError ?? string.Empty;
                }
            }
        }
        
        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        
        // === Unity Lifecycle ===
        
        protected virtual void Awake()
        {
            InitializeComponents();
            InitializeCancellationTokens();
            
            if (string.IsNullOrEmpty(_id))
                _id = $"{GetType().Name}_{GetInstanceID()}";
        }
        
        protected virtual void Start()
        {
            // Initialize asynchronously without blocking Start
            _ = InitializeAsync(_disposeCts.Token);
        }
        
        protected virtual void OnDestroy()
        {
            Dispose();
        }
        
        // === Initialization ===
        
        protected virtual void InitializeComponents()
        {
            // Get required components
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                Debug.LogError($"[{Id}] SpriteRenderer component is required for MonoBehaviourActor");
                return;
            }
            
            // Initialize subsystems
            _resourceManager = new ActorResourceManager(this);
            _animationSystem = new ActorAnimationSystem(this, _renderer);
            
            // Apply initial settings
            _renderer.sortingOrder = _sortingOrder;
            UpdateVisibility();
        }
        
        protected virtual void InitializeCancellationTokens()
        {
            _disposeCts = new CancellationTokenSource();
            _operationsCts = new CancellationTokenSource();
        }
        
        // === IActor Implementation ===
        
        public virtual async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                LoadState = ActorLoadState.Loading;
                LoadProgress = 0.1f;
                
                // Initialize resource manager
                await _resourceManager.InitializeAsync(linkedCts.Token);
                LoadProgress = 0.3f;
                
                // Initialize animation system
                await _animationSystem.InitializeAsync(linkedCts.Token);
                LoadProgress = 0.5f;
                
                // Custom initialization for derived classes
                await OnInitializeAsync(linkedCts.Token);
                LoadProgress = 0.8f;
                
                LoadState = ActorLoadState.Loaded;
                LoadProgress = 1.0f;
                
                OnLoaded?.Invoke(this);
                Debug.Log($"[{Id}] Actor initialized successfully");
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{Id}] Actor initialization cancelled");
                LoadState = ActorLoadState.Unloaded;
                throw;
            }
            catch (Exception ex)
            {
                SetError($"Initialization failed: {ex.Message}");
                LoadState = ActorLoadState.Error;
                throw;
            }
        }
        
        public virtual async UniTask LoadResourcesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                LoadState = ActorLoadState.Loading;
                await _resourceManager.LoadResourcesAsync(linkedCts.Token);
                LoadState = ActorLoadState.Loaded;
                
                OnLoaded?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{Id}] Resource loading cancelled");
                throw;
            }
            catch (Exception ex)
            {
                SetError($"Resource loading failed: {ex.Message}");
                LoadState = ActorLoadState.Error;
                throw;
            }
        }
        
        public virtual async UniTask UnloadResourcesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                LoadState = ActorLoadState.Unloading;
                await _resourceManager.UnloadResourcesAsync(linkedCts.Token);
                LoadState = ActorLoadState.Unloaded;
                
                OnUnloaded?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{Id}] Resource unloading cancelled");
                throw;
            }
            catch (Exception ex)
            {
                SetError($"Resource unloading failed: {ex.Message}");
                throw;
            }
        }
        
        public virtual async UniTask DestroyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Stop all animations first
                StopAllAnimations();
                
                // Unload resources
                if (LoadState != ActorLoadState.Unloaded)
                {
                    await UnloadResourcesAsync(cancellationToken);
                }
                
                // Custom cleanup
                await OnDestroyAsync(cancellationToken);
                
                // Destroy GameObject
                if (gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(gameObject);
                    else
                        DestroyImmediate(gameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Id}] Error during actor destruction: {ex.Message}");
                throw;
            }
        }
        
        // === Animation Methods (DOTween Integration) ===
        
        public virtual async UniTask ChangePositionAsync(Vector3 position, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimatePositionAsync(position, duration, ease, cancellationToken);
        }
        
        public virtual async UniTask ChangeRotationAsync(Quaternion rotation, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimateRotationAsync(rotation, duration, ease, cancellationToken);
        }
        
        public virtual async UniTask ChangeScaleAsync(Vector3 scale, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimateScaleAsync(scale, duration, ease, cancellationToken);
        }
        
        public virtual async UniTask ChangeVisibilityAsync(bool visible, float duration = 1.0f, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimateVisibilityAsync(visible, duration, ease, cancellationToken);
        }
        
        public virtual async UniTask ChangeAlphaAsync(float alpha, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimateAlphaAsync(alpha, duration, ease, cancellationToken);
        }
        
        public virtual async UniTask ChangeTintColorAsync(Color color, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            await _animationSystem.AnimateTintColorAsync(color, duration, ease, cancellationToken);
        }
        
        public abstract UniTask ChangeAppearanceAsync(object appearance, float duration = 0f, CancellationToken cancellationToken = default);
        
        public virtual async UniTask PlayAnimationSequenceAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            await _animationSystem.PlaySequenceAsync(sequenceName, cancellationToken);
        }
        
        public virtual void StopAllAnimations()
        {
            _animationSystem.StopAllAnimations();
        }
        
        // === State Management ===
        
        public virtual ActorState GetState()
        {
            var state = CreateStateInstance();
            state.CaptureFromActor(this);
            return state;
        }
        
        public virtual async UniTask ApplyStateAsync(ActorState state, float duration = 0f, CancellationToken cancellationToken = default)
        {
            if (state == null || !CanApplyState(state))
                return;
            
            try
            {
                if (duration <= 0f)
                {
                    // Apply immediately
                    state.ApplyToActor(this);
                }
                else
                {
                    // Animate to new state
                    await AnimateToStateAsync(state, duration, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                SetError($"Failed to apply state: {ex.Message}");
                throw;
            }
        }
        
        public virtual bool CanApplyState(ActorState state)
        {
            return state != null && state.ActorType == ActorType;
        }
        
        // === Utility Methods ===
        
        public virtual async UniTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _resourceManager.RefreshAsync(cancellationToken);
                UpdateVisibility();
            }
            catch (Exception ex)
            {
                SetError($"Refresh failed: {ex.Message}");
                throw;
            }
        }
        
        public virtual bool ValidateConfiguration(out string[] errors)
        {
            var errorList = new List<string>();
            
            if (string.IsNullOrEmpty(_id))
                errorList.Add("Actor ID cannot be empty");
            
            if (_renderer == null)
                errorList.Add("SpriteRenderer component is required");
            
            if (_resourceManager == null)
                errorList.Add("Resource manager is not initialized");
            
            if (_animationSystem == null)
                errorList.Add("Animation system is not initialized");
            
            // Allow derived classes to add validation
            ValidateConfigurationCustom(errorList);
            
            errors = errorList.ToArray();
            return errors.Length == 0;
        }
        
        public virtual string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"=== Actor Debug Info: {Id} ===");
            info.AppendLine($"Type: {ActorType}");
            info.AppendLine($"Display Name: {DisplayName}");
            info.AppendLine($"Load State: {LoadState}");
            info.AppendLine($"Visibility State: {VisibilityState}");
            info.AppendLine($"Load Progress: {LoadProgress:P}");
            info.AppendLine($"Has Error: {HasError}");
            if (HasError)
                info.AppendLine($"Last Error: {LastError}");
            info.AppendLine($"Position: {Position}");
            info.AppendLine($"Scale: {Scale}");
            info.AppendLine($"Alpha: {Alpha:F2}");
            info.AppendLine($"Sorting Order: {SortingOrder}");
            info.AppendLine($"Active Tweens: {_animationSystem?.ActiveTweenCount ?? 0}");
            
            return info.ToString();
        }
        
        // === Protected Virtual Methods for Derived Classes ===
        
        protected virtual async UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            await UniTask.CompletedTask;
        }
        
        protected virtual async UniTask OnDestroyAsync(CancellationToken cancellationToken)
        {
            await UniTask.CompletedTask;
        }
        
        protected virtual ActorState CreateStateInstance()
        {
            return new ActorState(Id, ActorType);
        }
        
        protected virtual async UniTask AnimateToStateAsync(ActorState state, float duration, CancellationToken cancellationToken)
        {
            // Create animation sequence to transition to new state
            var sequence = DOTween.Sequence();
            
            // Add position, rotation, scale animations
            sequence.Join(transform.DOMove(state.Position, duration));
            sequence.Join(transform.DORotate(state.Rotation.eulerAngles, duration));
            sequence.Join(transform.DOScale(state.Scale, duration));
            
            // Add color/alpha animations
            var targetColor = state.TintColor;
            targetColor.a = state.Alpha;
            sequence.Join(DOTween.To(() => _renderer.color, x => _renderer.color = x, targetColor, duration));
            
            // Add visibility animation
            if (state.Visible != Visible)
            {
                sequence.Join(_animationSystem.CreateVisibilityTween(state.Visible, duration));
            }
            
            sequence.SetEase(_defaultEase);
            await WaitForSequenceAsync(sequence, cancellationToken);
            
            // Apply final state properties
            state.ApplyToActor(this);
        }
        
        protected virtual void ValidateConfigurationCustom(List<string> errors)
        {
            // Override in derived classes to add custom validation
        }
        
        private async UniTask WaitForSequenceAsync(Sequence sequence, CancellationToken cancellationToken)
        {
            if (sequence == null || !sequence.IsActive())
                return;
            
            while (sequence.IsActive() && !sequence.IsComplete())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.DelayFrame(1);
            }
        }
        
        // === Private Methods ===
        
        private void UpdateVisibility()
        {
            if (_renderer != null)
                _renderer.enabled = _visible;
        }
        
        private void SetError(string error)
        {
            lock (_stateLock)
            {
                _hasError = true;
                _lastError = error;
            }
            
            Debug.LogError($"[{Id}] {error}");
            OnError?.Invoke(this, error);
        }
        
        // === IDisposable Implementation ===
        
        protected bool _disposed = false;
        
        public virtual void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                // Cancel all operations
                _disposeCts?.Cancel();
                _operationsCts?.Cancel();
                
                // Stop all animations
                StopAllAnimations();
                
                // Dispose subsystems
                _resourceManager?.Dispose();
                _animationSystem?.Dispose();
                
                // Dispose cancellation tokens
                _disposeCts?.Dispose();
                _operationsCts?.Dispose();
                
                _disposed = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Id}] Error during disposal: {ex.Message}");
            }
        }
    }
}
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Concrete implementation of a background actor with scene management features
    /// Handles background appearances, scene transitions, and environmental effects
    /// </summary>
    public class BackgroundActor : BaseActor<BackgroundAppearance>, IBackgroundActor
    {
        #region Inspector Fields
        
        [Header("Background Properties")]
        [SerializeField] private SceneTransitionType _transitionType = SceneTransitionType.Fade;
        [SerializeField] private float _parallaxFactor = 0.0f;
        [SerializeField] private bool _isMainBackground = false;
        
        [Header("Background Components")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Canvas _backgroundCanvas;
        [SerializeField] private UnityEngine.UI.Image _backgroundImage;
        
        [Header("Background Animation Settings")]
        [SerializeField] private float _transitionDuration = 2.0f;
        [SerializeField] private float _fadeInDuration = 1.0f;
        [SerializeField] private float _fadeOutDuration = 1.0f;
        
        [Header("Parallax Settings")]
        [SerializeField] private Transform _parallaxTransform;
        [SerializeField] private float _parallaxSpeed = 1.0f;
        
        #endregion
        
        #region Properties
        
        public override ActorType ActorType => ActorType.Background;
        
        public SceneTransitionType TransitionType
        {
            get => _transitionType;
            set => _transitionType = value;
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
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the background actor with basic properties
        /// Called by ActorFactory when creating new actors
        /// </summary>
        public void Initialize(string actorId, BackgroundAppearance appearance)
        {
            _id = actorId;
            _displayName = actorId;
            _appearance = appearance;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            base.Awake();
            
            // Setup background components based on available renderers
            SetupBackgroundComponents();
            
            // Set appropriate sorting order for backgrounds
            _sortingOrder = _isMainBackground ? -100 : -50;
        }
        
        protected override void Start()
        {
            base.Start();
            
            // Apply initial background settings
            ApplyBackgroundSettings();
        }
        
        private void Update()
        {
            // Handle parallax scrolling if enabled
            if (_parallaxFactor != 0 && _parallaxTransform != null)
            {
                UpdateParallaxPosition();
            }
        }
        
        #endregion
        
        #region BaseActor Implementation
        
        protected override async UniTask LoadAppearanceAssetsAsync(BackgroundAppearance appearance, CancellationToken cancellationToken)
        {
            try
            {
                // Generate addressable key for the background
                var addressableKey = appearance.GetAddressableKey(_id);
                
                // Try to load as Sprite first
                var spriteResource = await _resourceService.LoadResourceAsync<Sprite>(addressableKey, cancellationToken);
                
                if (spriteResource != null && spriteResource.IsValid)
                {
                    ApplySpriteToRenderer(spriteResource.Asset);
                    UnityEngine.Debug.Log($"[BackgroundActor] Loaded background sprite for {_id}: {addressableKey}");
                    return;
                }
                
                // Try to load as Texture2D if Sprite failed
                var textureResource = await _resourceService.LoadResourceAsync<Texture2D>(addressableKey, cancellationToken);
                
                if (textureResource != null && textureResource.IsValid)
                {
                    var sprite = Sprite.Create(textureResource.Asset, 
                        new Rect(0, 0, textureResource.Asset.width, textureResource.Asset.height), 
                        new Vector2(0.5f, 0.5f));
                    ApplySpriteToRenderer(sprite);
                    UnityEngine.Debug.Log($"[BackgroundActor] Loaded background texture for {_id}: {addressableKey}");
                    return;
                }
                
                // Fallback to default background
                UnityEngine.Debug.LogWarning($"[BackgroundActor] Failed to load background for {_id}: {addressableKey}, using default");
                await LoadDefaultBackground(cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[BackgroundActor] Error loading background for {_id}: {ex.Message}");
                await LoadDefaultBackground(cancellationToken);
            }
        }
        
        protected override async UniTask AnimateAppearanceChangeAsync(BackgroundAppearance oldAppearance, BackgroundAppearance newAppearance, float duration, CancellationToken cancellationToken)
        {
            // Animate background transition based on transition type
            switch (_transitionType)
            {
                case SceneTransitionType.Fade:
                    await AnimateFadeTransition(duration, cancellationToken);
                    break;
                    
                case SceneTransitionType.Slide:
                    await AnimateSlideTransition(duration, cancellationToken);
                    break;
                    
                case SceneTransitionType.Zoom:
                    await AnimateZoomTransition(duration, cancellationToken);
                    break;
                    
                case SceneTransitionType.Wipe:
                    await AnimateWipeTransition(duration, cancellationToken);
                    break;
                    
                default:
                    await AnimateFadeTransition(duration, cancellationToken);
                    break;
            }
        }
        
        #endregion
        
        #region IBackgroundActor Implementation
        
        public async UniTask ChangeLocationAsync(SceneLocation location, int variantId = 0, float duration = 2.0f, CancellationToken cancellationToken = default)
        {
            var newAppearance = _appearance.WithLocation(location).WithVariant(variantId);
            await ChangeAppearanceAsync(newAppearance, duration, cancellationToken);
        }
        
        public async UniTask TransitionToAsync(BackgroundAppearance newBackground, SceneTransitionType transition, float duration = 2.0f, CancellationToken cancellationToken = default)
        {
            var oldTransition = _transitionType;
            _transitionType = transition;
            
            try
            {
                await ChangeAppearanceAsync(newBackground, duration, cancellationToken);
            }
            finally
            {
                _transitionType = oldTransition; // Restore original transition type
            }
        }
        
        public async UniTask SetAsMainBackgroundAsync(CancellationToken cancellationToken = default)
        {
            _isMainBackground = true;
            _sortingOrder = -100;
            
            // Apply main background settings
            ApplyBackgroundSettings();
            
            // Ensure it's visible and properly positioned
            await FadeInAsync(_fadeInDuration, cancellationToken);
            
            UnityEngine.Debug.Log($"[BackgroundActor] Set {_id} as main background");
        }
        
        public async UniTask FadeInAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            await ChangeAlphaAsync(1.0f, duration, cancellationToken: cancellationToken);
        }
        
        public async UniTask FadeOutAsync(float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            await ChangeAlphaAsync(0.0f, duration, cancellationToken: cancellationToken);
        }
        
        #endregion
        
        #region Private Methods
        
        private void SetupBackgroundComponents()
        {
            // Prioritize Canvas/Image setup for UI backgrounds
            _backgroundCanvas = GetComponentInChildren<Canvas>();
            if (_backgroundCanvas != null)
            {
                _backgroundImage = _backgroundCanvas.GetComponentInChildren<UnityEngine.UI.Image>();
                if (_backgroundImage == null)
                {
                    var imageObject = new GameObject("BackgroundImage");
                    imageObject.transform.SetParent(_backgroundCanvas.transform);
                    _backgroundImage = imageObject.AddComponent<UnityEngine.UI.Image>();
                    
                    // Setup for full screen
                    var rectTransform = _backgroundImage.GetComponent<RectTransform>();
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                }
            }
            
            // Fallback to SpriteRenderer
            if (_backgroundCanvas == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    var spriteObject = new GameObject("BackgroundSprite");
                    spriteObject.transform.SetParent(transform);
                    spriteObject.transform.localPosition = Vector3.zero;
                    _spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
                }
            }
            
            // Setup parallax transform
            if (_parallaxTransform == null)
            {
                _parallaxTransform = _spriteRenderer?.transform ?? _backgroundImage?.transform ?? transform;
            }
        }
        
        private void ApplySpriteToRenderer(Sprite sprite)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.sprite = sprite;
                _backgroundImage.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, _alpha);
            }
            else if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
                _spriteRenderer.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, _alpha);
                _spriteRenderer.sortingOrder = _sortingOrder;
            }
        }
        
        private void ApplyBackgroundSettings()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = _sortingOrder;
                _spriteRenderer.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, _alpha);
            }
            
            if (_backgroundCanvas != null)
            {
                _backgroundCanvas.sortingOrder = _sortingOrder;
                if (_backgroundImage != null)
                {
                    _backgroundImage.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, _alpha);
                }
            }
        }
        
        private void UpdateParallaxPosition()
        {
            // Simple parallax scrolling - could be enhanced with camera following
            var offset = Camera.main != null ? Camera.main.transform.position * _parallaxFactor * _parallaxSpeed : Vector3.zero;
            _parallaxTransform.position = transform.position + new Vector3(offset.x, offset.y, 0);
        }
        
        private async UniTask LoadDefaultBackground(CancellationToken cancellationToken)
        {
            try
            {
                // Try to load a default background
                var defaultResource = await _resourceService.LoadResourceAsync<Sprite>("bg_default_scene_classroom_00", cancellationToken);
                if (defaultResource != null && defaultResource.IsValid)
                {
                    ApplySpriteToRenderer(defaultResource.Asset);
                }
                else
                {
                    CreateFallbackBackground();
                }
            }
            catch
            {
                CreateFallbackBackground();
            }
        }
        
        private void CreateFallbackBackground()
        {
            // Create a simple gradient background as fallback
            var texture = new Texture2D(512, 512);
            var topColor = Color.blue;
            var bottomColor = Color.cyan;
            
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    var lerpValue = (float)y / texture.height;
                    var color = Color.Lerp(bottomColor, topColor, lerpValue);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            ApplySpriteToRenderer(sprite);
            
            UnityEngine.Debug.Log($"[BackgroundActor] Created fallback background for {_id}");
        }
        
        #endregion
        
        #region Transition Animations
        
        private async UniTask AnimateFadeTransition(float duration, CancellationToken cancellationToken)
        {
            // Fade out, change, fade in
            await ChangeAlphaAsync(0f, duration * 0.3f, cancellationToken: cancellationToken);
            await ChangeAlphaAsync(_alpha, duration * 0.7f, cancellationToken: cancellationToken);
        }
        
        private async UniTask AnimateSlideTransition(float duration, CancellationToken cancellationToken)
        {
            var originalPosition = transform.position;
            var slideDistance = Screen.width;
            
            // Slide out
            await ChangePositionAsync(originalPosition + Vector3.right * slideDistance, duration * 0.5f, cancellationToken: cancellationToken);
            
            // Reset position off-screen left and slide in
            transform.position = originalPosition + Vector3.left * slideDistance;
            await ChangePositionAsync(originalPosition, duration * 0.5f, cancellationToken: cancellationToken);
        }
        
        private async UniTask AnimateZoomTransition(float duration, CancellationToken cancellationToken)
        {
            var originalScale = transform.localScale;
            
            // Zoom out
            await ChangeScaleAsync(Vector3.zero, duration * 0.3f, cancellationToken: cancellationToken);
            
            // Zoom in
            await ChangeScaleAsync(originalScale, duration * 0.7f, cancellationToken: cancellationToken);
        }
        
        private async UniTask AnimateWipeTransition(float duration, CancellationToken cancellationToken)
        {
            // Simple wipe effect using scale
            var originalScale = transform.localScale;
            
            // Wipe out (scale X to 0)
            var wipeOutScale = new Vector3(0, originalScale.y, originalScale.z);
            await ChangeScaleAsync(wipeOutScale, duration * 0.5f, cancellationToken: cancellationToken);
            
            // Wipe in (scale X back to original)
            await ChangeScaleAsync(originalScale, duration * 0.5f, cancellationToken: cancellationToken);
        }
        
        #endregion
        
        #region Alpha Override for Background Renderers
        
        public override float Alpha
        {
            get => base.Alpha;
            set
            {
                base.Alpha = value;
                
                // Apply alpha to background-specific renderers
                if (_backgroundImage != null)
                {
                    var color = _backgroundImage.color;
                    color.a = value;
                    _backgroundImage.color = color;
                }
                
                if (_spriteRenderer != null)
                {
                    var color = _spriteRenderer.color;
                    color.a = value;
                    _spriteRenderer.color = color;
                }
            }
        }
        
        public override Color TintColor
        {
            get => base.TintColor;
            set
            {
                base.TintColor = value;
                
                // Apply tint to background-specific renderers
                if (_backgroundImage != null)
                {
                    _backgroundImage.color = new Color(value.r, value.g, value.b, _alpha);
                }
                
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.color = new Color(value.r, value.g, value.b, _alpha);
                }
            }
        }
        
        #endregion
        
        #region Validation
        
        public override bool ValidateConfiguration(out string[] errors)
        {
            var baseErrors = new System.Collections.Generic.List<string>();
            
            // Call base validation
            if (!base.ValidateConfiguration(out var parentErrors))
            {
                baseErrors.AddRange(parentErrors);
            }
            
            // Background-specific validation
            if (_backgroundImage == null && _spriteRenderer == null)
                baseErrors.Add("BackgroundActor requires either a UI Image or SpriteRenderer component");
                
            if (_parallaxFactor != 0 && _parallaxTransform == null)
                baseErrors.Add("BackgroundActor with parallax requires a parallax transform");
            
            errors = baseErrors.ToArray();
            return baseErrors.Count == 0;
        }
        
        public override string GetDebugInfo()
        {
            var baseInfo = base.GetDebugInfo();
            return $"{baseInfo}\n" +
                   $"Transition Type: {_transitionType}\n" +
                   $"Parallax Factor: {_parallaxFactor}\n" +
                   $"Is Main Background: {_isMainBackground}\n" +
                   $"Background Type: {_appearance.Type}\n" +
                   $"Scene Location: {_appearance.Location}\n" +
                   $"Variant: {_appearance.VariantId}";
        }
        
        #endregion
    }
}
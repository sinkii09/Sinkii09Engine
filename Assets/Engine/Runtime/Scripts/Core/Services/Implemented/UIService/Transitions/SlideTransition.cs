using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Slide transition using DOTween for directional animations (Plain C# implementation)
    /// </summary>
    public class SlideTransition : BaseUITransition
    {
        private readonly TransitionType _slideDirection;
        private readonly float _distanceMultiplier;
        private readonly bool _useElasticEffect;
        
        // Track original positions per screen to prevent accumulation
        private readonly System.Collections.Generic.Dictionary<UIScreen, Vector2> _originalPositions = new();
        private Vector2 _startPosition;
        private Vector2 _endPosition;
        
        public override TransitionType TransitionType => _slideDirection;
        
        public SlideDirection Direction => TransitionTypeToDirection(_slideDirection);

        /// <summary>
        /// Constructor with configuration settings
        /// </summary>
        public SlideTransition(TransitionType slideDirection, UITransitionConfiguration.SlideTransitionSettings settings, float defaultDuration, Ease defaultEase)
            : base(settings?.GetDuration(defaultDuration) ?? defaultDuration, settings?.GetEase(defaultEase) ?? defaultEase)
        {
            _slideDirection = slideDirection;
            _distanceMultiplier = settings?.distanceMultiplier ?? 1.0f;
            _useElasticEffect = settings?.useElasticEffect ?? false;
        }

        /// <summary>
        /// Constructor with manual parameters
        /// </summary>
        public SlideTransition(TransitionType slideDirection, float distanceMultiplier = 1.0f, float duration = 0.3f, Ease easeType = Ease.OutQuart, bool useElasticEffect = false)
            : base(duration, easeType)
        {
            _slideDirection = slideDirection;
            _distanceMultiplier = distanceMultiplier;
            _useElasticEffect = useElasticEffect;
        }
        
        public override void SetupInitialState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            
            // Store original position only if not already stored
            if (!_originalPositions.ContainsKey(screen))
            {
                _originalPositions[screen] = rectTransform.anchoredPosition;
            }
            
            CalculatePositions(screen, rectTransform);
            rectTransform.anchoredPosition = _startPosition;
        }
        
        public override void ResetToDefaultState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            
            // Always return to the true original position
            if (_originalPositions.TryGetValue(screen, out var originalPos))
            {
                rectTransform.anchoredPosition = originalPos;
            }
        }
        
        protected override Tween CreateInTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            
            // Store original position if not already stored
            if (!_originalPositions.ContainsKey(screen))
            {
                _originalPositions[screen] = rectTransform.anchoredPosition;
            }
            
            CalculatePositions(screen, rectTransform);
            rectTransform.anchoredPosition = _startPosition;
            
            var tween = rectTransform.DOAnchorPos(_endPosition, _duration);
            
            // Apply elastic effect if enabled
            if (_useElasticEffect)
            {
                tween.SetEase(Ease.OutElastic, 0.8f, 0.3f);
            }
            
            return tween;
        }
        
        protected override Tween CreateOutTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            
            CalculatePositions(screen, rectTransform);
            
            return rectTransform.DOAnchorPos(_startPosition, _duration);
        }
        
        private void CalculatePositions(UIScreen screen, RectTransform rectTransform)
        {
            // Use the stored original position as the target
            var originalPosition = _originalPositions[screen];
            _endPosition = originalPosition;
            
            float distance = GetCanvasDistance(rectTransform) * _distanceMultiplier;
            
            _startPosition = _slideDirection switch
            {
                TransitionType.SlideLeft => originalPosition + Vector2.right * distance,
                TransitionType.SlideRight => originalPosition + Vector2.left * distance,
                TransitionType.SlideUp => originalPosition + Vector2.down * distance,
                TransitionType.SlideDown => originalPosition + Vector2.up * distance,
                _ => originalPosition
            };
        }
        
        private float GetCanvasDistance(RectTransform rectTransform)
        {
            // Get the canvas dimensions in UI space
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    return _slideDirection switch
                    {
                        TransitionType.SlideLeft or TransitionType.SlideRight => canvasRect.rect.width,
                        TransitionType.SlideUp or TransitionType.SlideDown => canvasRect.rect.height,
                        _ => canvasRect.rect.width
                    };
                }
            }
            
            // Fallback to screen dimensions if canvas not found
            return _slideDirection switch
            {
                TransitionType.SlideLeft or TransitionType.SlideRight => Screen.width,
                TransitionType.SlideUp or TransitionType.SlideDown => Screen.height,
                _ => Screen.width
            };
        }
        
        private static SlideDirection TransitionTypeToDirection(TransitionType transitionType)
        {
            return transitionType switch
            {
                TransitionType.SlideLeft => SlideDirection.Left,
                TransitionType.SlideRight => SlideDirection.Right,
                TransitionType.SlideUp => SlideDirection.Up,
                TransitionType.SlideDown => SlideDirection.Down,
                _ => SlideDirection.Left
            };
        }
        
        private static TransitionType DirectionToTransitionType(SlideDirection direction)
        {
            return direction switch
            {
                SlideDirection.Left => TransitionType.SlideLeft,
                SlideDirection.Right => TransitionType.SlideRight,
                SlideDirection.Up => TransitionType.SlideUp,
                SlideDirection.Down => TransitionType.SlideDown,
                _ => TransitionType.SlideLeft
            };
        }
        
        public override void Dispose()
        {
            _originalPositions.Clear();
            base.Dispose();
        }
    }
    
    /// <summary>
    /// Slide direction for transitions
    /// </summary>
    public enum SlideDirection
    {
        Left,
        Right,
        Up,
        Down
    }
}
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Scale transition using DOTween for zoom in/out effects (Plain C# implementation)
    /// </summary>
    public class ScaleTransition : BaseUITransition
    {
        private readonly TransitionType _scaleDirection;
        private readonly float _startScale;
        private readonly float _targetScale;
        private readonly bool _usePunchEffect;
        private readonly float _punchStrength;
        
        private Vector3 _originalScale;
        
        public override TransitionType TransitionType => _scaleDirection;

        /// <summary>
        /// Constructor with configuration settings
        /// </summary>
        public ScaleTransition(TransitionType scaleDirection, UITransitionConfiguration.ScaleTransitionSettings settings, float defaultDuration, Ease defaultEase)
            : base(settings?.GetDuration(defaultDuration) ?? defaultDuration, settings?.GetEase(defaultEase) ?? defaultEase)
        {
            _scaleDirection = scaleDirection;
            _startScale = settings?.startScale ?? 0.8f;
            _targetScale = settings?.targetScale ?? 1f;
            _usePunchEffect = settings?.usePunchEffect ?? false;
            _punchStrength = settings?.punchStrength ?? 0.2f;
        }

        /// <summary>
        /// Constructor with manual parameters
        /// </summary>
        public ScaleTransition(TransitionType scaleDirection, float startScale = 0.8f, float targetScale = 1f, float duration = 0.3f, Ease easeType = Ease.OutQuart, bool usePunchEffect = false, float punchStrength = 0.2f)
            : base(duration, easeType)
        {
            _scaleDirection = scaleDirection;
            _startScale = startScale;
            _targetScale = targetScale;
            _usePunchEffect = usePunchEffect;
            _punchStrength = punchStrength;
        }
        
        public override void SetupInitialState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            _originalScale = rectTransform.localScale;
            
            if (_scaleDirection == TransitionType.ScaleUp)
                rectTransform.localScale = Vector3.one * _startScale;
            else
                rectTransform.localScale = Vector3.one * _targetScale;
        }
        
        public override void ResetToDefaultState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            rectTransform.localScale = _originalScale;
        }
        
        protected override Tween CreateInTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            _originalScale = rectTransform.localScale;
            
            var sequence = CreateSequence();
            
            if (_scaleDirection == TransitionType.ScaleUp)
            {
                rectTransform.localScale = Vector3.one * _startScale;
                var scaleTween = rectTransform.DOScale(Vector3.one * _targetScale, _duration);
                sequence.Append(scaleTween);
                
                // Add punch effect if enabled
                if (_usePunchEffect)
                {
                    sequence.Append(rectTransform.DOPunchScale(Vector3.one * _punchStrength, _duration * 0.3f, 3, 0.5f));
                }
            }
            else // ScaleDown
            {
                rectTransform.localScale = Vector3.one * _targetScale;
                sequence.Append(rectTransform.DOScale(Vector3.one * _startScale, _duration));
            }
            
            return sequence;
        }
        
        protected override Tween CreateOutTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            var sequence = CreateSequence();
            
            if (_scaleDirection == TransitionType.ScaleUp)
            {
                sequence.Append(rectTransform.DOScale(Vector3.one * _startScale, _duration));
            }
            else // ScaleDown
            {
                sequence.Append(rectTransform.DOScale(Vector3.one * _targetScale, _duration));
            }
            
            return sequence;
        }
    }
}
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Fade in/out transition using DOTween (Plain C# implementation)
    /// </summary>
    public class FadeTransition : BaseUITransition
    {
        private readonly float _startAlpha;
        private readonly float _endAlpha;
        
        public override TransitionType TransitionType => TransitionType.Fade;

        /// <summary>
        /// Constructor with configuration settings
        /// </summary>
        public FadeTransition(UITransitionConfiguration.FadeTransitionSettings settings, float defaultDuration, Ease defaultEase)
            : base(settings?.GetDuration(defaultDuration) ?? defaultDuration, settings?.GetEase(defaultEase) ?? defaultEase)
        {
            _startAlpha = settings?.startAlpha ?? 0f;
            _endAlpha = settings?.endAlpha ?? 1f;
        }

        /// <summary>
        /// Constructor with manual parameters
        /// </summary>
        public FadeTransition(float startAlpha = 0f, float endAlpha = 1f, float duration = 0.3f, Ease easeType = Ease.OutQuart)
            : base(duration, easeType)
        {
            _startAlpha = startAlpha;
            _endAlpha = endAlpha;
        }
        
        public override void SetupInitialState(UIScreen screen)
        {
            var canvasGroup = GetOrCreateCanvasGroup(screen);
            canvasGroup.alpha = _startAlpha;
        }
        
        public override void ResetToDefaultState(UIScreen screen)
        {
            var canvasGroup = GetOrCreateCanvasGroup(screen);
            canvasGroup.alpha = _endAlpha;
        }
        
        protected override Tween CreateInTween(UIScreen screen)
        {
            var canvasGroup = GetOrCreateCanvasGroup(screen);
            canvasGroup.alpha = _startAlpha;
            
            return canvasGroup.DOFade(_endAlpha, _duration);
        }
        
        protected override Tween CreateOutTween(UIScreen screen)
        {
            var canvasGroup = GetOrCreateCanvasGroup(screen);
            
            return canvasGroup.DOFade(_startAlpha, _duration);
        }
    }
}
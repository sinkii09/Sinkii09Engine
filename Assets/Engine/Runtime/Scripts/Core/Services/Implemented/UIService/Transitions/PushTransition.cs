using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Push transition that slides the current screen out while the new screen slides in (Plain C# implementation)
    /// </summary>
    public class PushTransition : BaseUITransition
    {
        private readonly PushDirection _pushDirection;
        private readonly float _overlapPercentage;
        
        private Vector2 _originalPosition;
        private UIScreen _outgoingScreen;
        
        public override TransitionType TransitionType => TransitionType.Push;
        
        public PushDirection Direction => _pushDirection;

        /// <summary>
        /// Constructor with configuration settings
        /// </summary>
        public PushTransition(UITransitionConfiguration.PushTransitionSettings settings, float defaultDuration, Ease defaultEase)
            : base(settings?.GetDuration(defaultDuration) ?? defaultDuration, settings?.GetEase(defaultEase) ?? defaultEase)
        {
            _pushDirection = settings?.defaultDirection ?? PushDirection.Left;
            _overlapPercentage = settings?.overlapPercentage ?? 0.2f;
        }

        /// <summary>
        /// Constructor with manual parameters
        /// </summary>
        public PushTransition(PushDirection pushDirection = PushDirection.Left, float overlapPercentage = 0.2f, float duration = 0.3f, Ease easeType = Ease.OutQuart)
            : base(duration, easeType)
        {
            _pushDirection = pushDirection;
            _overlapPercentage = overlapPercentage;
        }
        
        public override void SetupInitialState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            _originalPosition = rectTransform.anchoredPosition;
            
            // Position incoming screen off-screen
            var startPosition = GetOffScreenPosition(true);
            rectTransform.anchoredPosition = startPosition;
        }
        
        public override void ResetToDefaultState(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            rectTransform.anchoredPosition = _originalPosition;
        }
        
        protected override Tween CreateInTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            _originalPosition = rectTransform.anchoredPosition;
            
            // Position incoming screen off-screen
            var startPosition = GetOffScreenPosition(true);
            rectTransform.anchoredPosition = startPosition;
            
            // Animate to original position
            return rectTransform.DOAnchorPos(_originalPosition, _duration);
        }
        
        protected override Tween CreateOutTween(UIScreen screen)
        {
            var rectTransform = GetScreenRectTransform(screen);
            
            // Move outgoing screen off-screen in opposite direction
            var endPosition = GetOffScreenPosition(false);
            return rectTransform.DOAnchorPos(endPosition, _duration);
        }
        
        /// <summary>
        /// Create a push transition that animates both screens simultaneously
        /// </summary>
        public Tween CreatePushTween(UIScreen incomingScreen, UIScreen outgoingScreen)
        {
            var sequence = CreateSequence();
            
            // Setup incoming screen
            var incomingRect = GetScreenRectTransform(incomingScreen);
            var incomingOriginal = incomingRect.anchoredPosition;
            var incomingStart = GetOffScreenPosition(true);
            incomingRect.anchoredPosition = incomingStart;
            
            // Setup outgoing screen
            var outgoingRect = GetScreenRectTransform(outgoingScreen);
            var outgoingEnd = GetOffScreenPosition(false);
            
            // Create overlapped animation based on overlap percentage
            var overlapDuration = _duration * _overlapPercentage;
            var mainDuration = _duration - overlapDuration;
            
            // Animate both screens with overlap
            sequence.Append(incomingRect.DOAnchorPos(incomingOriginal, _duration));
            sequence.Insert(overlapDuration, outgoingRect.DOAnchorPos(outgoingEnd, mainDuration));
            
            return sequence;
        }
        
        private Vector2 GetOffScreenPosition(bool isIncoming)
        {
            float distance = GetScreenDistance();
            
            // For incoming screens, they start from the direction we're pushing towards
            // For outgoing screens, they end up in the direction we're pushing from
            var multiplier = isIncoming ? 1f : -1f;
            
            return _pushDirection switch
            {
                PushDirection.Left => _originalPosition + Vector2.right * distance * multiplier,
                PushDirection.Right => _originalPosition + Vector2.left * distance * multiplier,
                PushDirection.Up => _originalPosition + Vector2.down * distance * multiplier,
                PushDirection.Down => _originalPosition + Vector2.up * distance * multiplier,
                _ => _originalPosition
            };
        }
        
        private float GetScreenDistance()
        {
            return _pushDirection switch
            {
                PushDirection.Left or PushDirection.Right => Screen.width,
                PushDirection.Up or PushDirection.Down => Screen.height,
                _ => Screen.width
            };
        }
    }
}
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Plain C# base class for UI transitions using DOTween for smooth animations
    /// No longer inherits from MonoBehaviour - pure C# implementation
    /// </summary>
    public abstract class BaseUITransition : IUITransition
    {
        protected float _duration = 0.3f;
        protected Ease _easeType = Ease.OutQuart;
        protected bool _canInterrupt = true;
        protected Tween _currentTween;
        protected bool _isAnimating;
        protected bool _disposed;
        
        public abstract TransitionType TransitionType { get; }
        
        public virtual float Duration 
        { 
            get => _duration;
            set => _duration = Mathf.Max(0.01f, value);
        }
        
        public virtual AnimationCurve Curve 
        { 
            get => _easeType.ToCurve();
            set => _easeType = value?.ToEase() ?? Ease.OutQuart;
        }
        
        public virtual Ease EaseType
        {
            get => _easeType;
            set => _easeType = value;
        }
        
        public virtual bool CanInterrupt => _canInterrupt && _isAnimating;
        public virtual bool IsAnimating => _isAnimating;

        /// <summary>
        /// Constructor with duration and ease configuration
        /// </summary>
        protected BaseUITransition(float duration, Ease easeType, bool canInterrupt = true)
        {
            _duration = Mathf.Max(0.01f, duration);
            _easeType = easeType;
            _canInterrupt = canInterrupt;
        }

        
        public virtual async UniTask AnimateInAsync(UIScreen screen, CancellationToken cancellationToken = default)
        {
            if (screen == null || _disposed) return;
            
            await StartAnimationAsync(screen, true, cancellationToken);
        }
        
        public virtual async UniTask AnimateOutAsync(UIScreen screen, CancellationToken cancellationToken = default)
        {
            if (screen == null || _disposed) return;
            
            await StartAnimationAsync(screen, false, cancellationToken);
        }
        
        public abstract void SetupInitialState(UIScreen screen);
        public abstract void ResetToDefaultState(UIScreen screen);
        
        protected abstract Tween CreateInTween(UIScreen screen);
        protected abstract Tween CreateOutTween(UIScreen screen);
        
        public virtual void Interrupt()
        {
            if (_canInterrupt && _currentTween != null)
            {
                _currentTween.Kill();
                _currentTween = null;
                _isAnimating = false;
            }
        }
        
        protected virtual async UniTask StartAnimationAsync(UIScreen screen, bool isAnimatingIn, CancellationToken cancellationToken)
        {
            if (_disposed) return;
            
            if (_isAnimating && !CanInterrupt)
                return;
                
            // Interrupt current animation if running
            if (_isAnimating)
                Interrupt();
            
            _isAnimating = true;
            
            try
            {
                // Setup initial state
                if (isAnimatingIn)
                    SetupInitialState(screen);
                
                // Create the appropriate tween
                _currentTween = isAnimatingIn ? CreateInTween(screen) : CreateOutTween(screen);
                
                if (_currentTween != null)
                {
                    _currentTween
                        .SetEase(_easeType)
                        .SetUpdate(true); // Use unscaled time
                    
                    // Wait for tween completion with cancellation support
                    await _currentTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken: cancellationToken);
                }
                
                // Reset state after out animation
                if (!isAnimatingIn)
                    ResetToDefaultState(screen);
            }
            catch (OperationCanceledException)
            {
                // Animation was cancelled - kill tween if still running
                _currentTween?.Kill();
            }
            finally
            {
                _isAnimating = false;
                _currentTween = null;
            }
        }
        
        /// <summary>
        /// Get the RectTransform of the screen for position/scale animations
        /// </summary>
        protected RectTransform GetScreenRectTransform(UIScreen screen)
        {
            return screen.transform as RectTransform ?? screen.GetComponent<RectTransform>();
        }
        
        /// <summary>
        /// Get the CanvasGroup of the screen for alpha animations
        /// </summary>
        protected CanvasGroup GetOrCreateCanvasGroup(UIScreen screen)
        {
            var canvasGroup = screen.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = screen.gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }
        
        /// <summary>
        /// Helper method to create a sequence of tweens
        /// </summary>
        protected Sequence CreateSequence()
        {
            return DOTween.Sequence().SetUpdate(true);
        }

        #region IDisposable Implementation

        public virtual void Dispose()
        {
            if (_disposed) return;
            
            Interrupt();
            
            // Kill any remaining tweens associated with this transition
            if (_currentTween != null)
            {
                _currentTween.Kill();
                _currentTween = null;
            }
            
            _disposed = true;
        }

        #endregion
    }
    
    /// <summary>
    /// Extension methods for DOTween integration
    /// </summary>
    public static class DOTweenExtensions
    {
        public static AnimationCurve ToCurve(this Ease ease)
        {
            // Convert DOTween ease to Unity AnimationCurve (simplified mapping)
            return ease switch
            {
                Ease.Linear => AnimationCurve.Linear(0, 0, 1, 1),
                Ease.InQuad => new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2)),
                Ease.OutQuad => new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0)),
                Ease.InOutQuad => AnimationCurve.EaseInOut(0, 0, 1, 1),
                _ => AnimationCurve.EaseInOut(0, 0, 1, 1)
            };
        }
        
        public static Ease ToEase(this AnimationCurve curve)
        {
            // Convert Unity AnimationCurve to DOTween ease (simplified)
            if (curve == null || curve.keys.Length < 2)
                return Ease.OutQuart;
                
            // Simple heuristic based on curve shape
            var startTangent = curve.keys[0].outTangent;
            var endTangent = curve.keys[curve.keys.Length - 1].inTangent;
            
            if (Mathf.Approximately(startTangent, endTangent))
                return Ease.Linear;
            else if (startTangent < endTangent)
                return Ease.InOutQuad;
            else
                return Ease.OutQuart;
        }
    }
}
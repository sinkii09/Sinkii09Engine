using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages UI transitions and provides transition creation and coordination (Plain C# implementation)
    /// </summary>
    public class UITransitionManager : IDisposable
    {
        private readonly UITransitionConfiguration _config;
        private readonly Dictionary<TransitionType, IUITransition> _transitionCache = new();
        private readonly Dictionary<UIScreen, IUITransition> _activeTransitions = new();
        private bool _disposed;
        
        public UITransitionManager(UITransitionConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        
        /// <summary>
        /// Get or create a transition for the specified type
        /// </summary>
        public IUITransition GetTransition(TransitionType transitionType)
        {
            if (_disposed) return null;
            
            if (_transitionCache.TryGetValue(transitionType, out var cachedTransition))
            {
                return cachedTransition;
            }
            
            var transition = CreateTransition(transitionType);
            if (transition != null)
            {
                _transitionCache[transitionType] = transition;
            }
            
            return transition;
        }
        
        /// <summary>
        /// Animate a screen in with the specified transition
        /// </summary>
        public async UniTask AnimateScreenInAsync(UIScreen screen, TransitionType transitionType = TransitionType.None, CancellationToken cancellationToken = default)
        {
            if (screen == null || _disposed) return;
            
            var effectiveTransitionType = transitionType == TransitionType.None ? TransitionType.Fade : transitionType;
            var transition = GetTransition(effectiveTransitionType);
            
            if (transition == null) return;
            
            // Track active transition
            _activeTransitions[screen] = transition;
            
            try
            {
                await transition.AnimateInAsync(screen, cancellationToken);
            }
            finally
            {
                _activeTransitions.Remove(screen);
            }
        }
        
        /// <summary>
        /// Animate a screen out with the specified transition
        /// </summary>
        public async UniTask AnimateScreenOutAsync(UIScreen screen, TransitionType transitionType = TransitionType.None, CancellationToken cancellationToken = default)
        {
            if (screen == null || _disposed) return;
            
            var effectiveTransitionType = transitionType == TransitionType.None ? TransitionType.Fade : transitionType;
            var transition = GetTransition(effectiveTransitionType);
            
            if (transition == null) return;
            
            // Track active transition
            _activeTransitions[screen] = transition;
            
            try
            {
                await transition.AnimateOutAsync(screen, cancellationToken);
            }
            finally
            {
                _activeTransitions.Remove(screen);
            }
        }
        
        /// <summary>
        /// Animate a screen replacement with push transition
        /// </summary>
        public async UniTask AnimateScreenReplaceAsync(UIScreen incomingScreen, UIScreen outgoingScreen, TransitionType transitionType = TransitionType.Push, CancellationToken cancellationToken = default)
        {
            if (incomingScreen == null || outgoingScreen == null || _disposed) return;
            
            var effectiveTransitionType = transitionType == TransitionType.None ? TransitionType.Push : transitionType;
            
            if (effectiveTransitionType == TransitionType.Push)
            {
                var pushTransition = GetTransition(TransitionType.Push) as PushTransition;
                if (pushTransition != null)
                {
                    var tween = pushTransition.CreatePushTween(incomingScreen, outgoingScreen);
                    if (tween != null)
                    {
                        await tween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken: cancellationToken);
                    }
                }
            }
            else
            {
                // For non-push transitions, animate them separately
                var outTask = AnimateScreenOutAsync(outgoingScreen, effectiveTransitionType, cancellationToken);
                var inTask = AnimateScreenInAsync(incomingScreen, effectiveTransitionType, cancellationToken);
                
                await UniTask.WhenAll(outTask, inTask);
            }
        }
        
        /// <summary>
        /// Stop all active transitions
        /// </summary>
        public void StopAllTransitions()
        {
            if (_disposed) return;
            
            foreach (var transition in _activeTransitions.Values)
            {
                transition.Interrupt();
            }
            _activeTransitions.Clear();
        }
        
        /// <summary>
        /// Stop transition for a specific screen
        /// </summary>
        public void StopTransition(UIScreen screen)
        {
            if (_disposed) return;
            
            if (_activeTransitions.TryGetValue(screen, out var transition))
            {
                transition.Interrupt();
                _activeTransitions.Remove(screen);
            }
        }
        
        /// <summary>
        /// Check if a screen is currently transitioning
        /// </summary>
        public bool IsTransitioning(UIScreen screen)
        {
            if (_disposed) return false;
            return _activeTransitions.ContainsKey(screen);
        }
        
        /// <summary>
        /// Get the number of active transitions
        /// </summary>
        public int ActiveTransitionCount => _disposed ? 0 : _activeTransitions.Count;
        
        private IUITransition CreateTransition(TransitionType transitionType)
        {
            if (_disposed) return null;
            
            return transitionType switch
            {
                TransitionType.Fade => new FadeTransition(_config.FadeSettings, _config.DefaultDuration, _config.DefaultEase),
                
                TransitionType.SlideLeft => new SlideTransition(TransitionType.SlideLeft, _config.SlideSettings, _config.DefaultDuration, _config.DefaultEase),
                TransitionType.SlideRight => new SlideTransition(TransitionType.SlideRight, _config.SlideSettings, _config.DefaultDuration, _config.DefaultEase),
                TransitionType.SlideUp => new SlideTransition(TransitionType.SlideUp, _config.SlideSettings, _config.DefaultDuration, _config.DefaultEase),
                TransitionType.SlideDown => new SlideTransition(TransitionType.SlideDown, _config.SlideSettings, _config.DefaultDuration, _config.DefaultEase),
                
                TransitionType.ScaleUp => new ScaleTransition(TransitionType.ScaleUp, _config.ScaleSettings, _config.DefaultDuration, _config.DefaultEase),
                TransitionType.ScaleDown => new ScaleTransition(TransitionType.ScaleDown, _config.ScaleSettings, _config.DefaultDuration, _config.DefaultEase),
                
                TransitionType.Push => new PushTransition(_config.PushSettings, _config.DefaultDuration, _config.DefaultEase),
                TransitionType.Cover => new PushTransition(_config.PushSettings, _config.DefaultDuration, _config.DefaultEase),
                
                _ => new FadeTransition(_config.FadeSettings, _config.DefaultDuration, _config.DefaultEase)
            };
        }
        
        #region IDisposable Implementation

        public void Dispose()
        {
            if (_disposed) return;
            
            StopAllTransitions();
            
            // Dispose all cached transitions
            foreach (var transition in _transitionCache.Values)
            {
                transition?.Dispose();
            }
            _transitionCache.Clear();
            
            // Kill all DOTween tweens to prevent memory leaks
            DOTween.KillAll();
            
            _disposed = true;
        }

        #endregion
    }
}
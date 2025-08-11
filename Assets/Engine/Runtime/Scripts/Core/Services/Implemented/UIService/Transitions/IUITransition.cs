using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for UI screen transition animations (Plain C# implementation)
    /// </summary>
    public interface IUITransition : IDisposable
    {
        /// <summary>
        /// The type of transition this implementation handles
        /// </summary>
        TransitionType TransitionType { get; }
        
        /// <summary>
        /// Duration of the transition animation in seconds
        /// </summary>
        float Duration { get; set; }
        
        /// <summary>
        /// DOTween easing type for the transition
        /// </summary>
        Ease EaseType { get; set; }
        
        /// <summary>
        /// Animation curve for the transition (converted from/to Ease)
        /// </summary>
        AnimationCurve Curve { get; set; }
        
        /// <summary>
        /// Animate a screen entering the view
        /// </summary>
        /// <param name="screen">Screen to animate in</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the animation</returns>
        UniTask AnimateInAsync(UIScreen screen, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animate a screen exiting the view
        /// </summary>
        /// <param name="screen">Screen to animate out</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the animation</returns>
        UniTask AnimateOutAsync(UIScreen screen, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Setup the screen for the initial state before animation
        /// </summary>
        /// <param name="screen">Screen to setup</param>
        void SetupInitialState(UIScreen screen);
        
        /// <summary>
        /// Reset the screen to its default state after animation
        /// </summary>
        /// <param name="screen">Screen to reset</param>
        void ResetToDefaultState(UIScreen screen);
        
        /// <summary>
        /// Check if this transition can be interrupted
        /// </summary>
        bool CanInterrupt { get; }
        
        /// <summary>
        /// Check if this transition is currently animating
        /// </summary>
        bool IsAnimating { get; }
        
        /// <summary>
        /// Interrupt the current animation if possible
        /// </summary>
        void Interrupt();
    }
}
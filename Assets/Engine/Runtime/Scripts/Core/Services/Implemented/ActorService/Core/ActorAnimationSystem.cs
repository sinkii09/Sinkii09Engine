using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Advanced animation system with DOTween Pro integration for actors
    /// Features sequence management, custom easing, and performance optimization
    /// </summary>
    public class ActorAnimationSystem : IDisposable
    {
        private readonly IActor _actor;
        private readonly Transform _transform;
        private readonly SpriteRenderer _renderer;
        
        // Animation management
        private readonly Dictionary<string, Tween> _namedTweens = new();
        private readonly Dictionary<string, Sequence> _namedSequences = new();
        private readonly List<Tween> _activeTweens = new();
        private readonly object _animationLock = new();
        
        // Animation sequences and timelines
        private readonly Dictionary<string, AnimationSequenceData> _sequenceLibrary = new();
        
        // Performance optimization
        private readonly Queue<Tween> _tweenPool = new();
        private readonly int _maxPoolSize = 20;
        
        // Configuration
        private float _defaultDuration = 1.0f;
        private Ease _defaultEase = Ease.OutQuad;
        private bool _enableTweenRecycling = true;
        private bool _enableSequenceOptimization = true;
        
        // State
        private CancellationTokenSource _disposeCts;
        private bool _disposed = false;
        
        public int ActiveTweenCount
        {
            get
            {
                lock (_animationLock)
                {
                    return _activeTweens.Count;
                }
            }
        }
        
        public bool HasActiveAnimations => ActiveTweenCount > 0;
        
        public ActorAnimationSystem(IActor actor, SpriteRenderer renderer)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _transform = actor.Transform;
            _disposeCts = new CancellationTokenSource();
            
            // Configure DOTween for optimal performance
            ConfigureDOTween();
            
            Debug.Log($"[ActorAnimationSystem] Initialized for actor: {_actor.Id}");
        }
        
        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Initialize animation sequences from configuration
                await LoadAnimationSequencesAsync(cancellationToken);
                
                // Pre-warm tween pool
                PrewarmTweenPool();
                
                Debug.Log($"[ActorAnimationSystem] Animation system initialized for {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorAnimationSystem] Failed to initialize: {ex.Message}");
                throw;
            }
        }
        
        // === Core Animation Methods ===
        
        public async UniTask AnimatePositionAsync(Vector3 target, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var tween = _transform.DOMove(target, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "position", cancellationToken);
        }
        
        public async UniTask AnimateRotationAsync(Quaternion target, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var tween = _transform.DORotate(target.eulerAngles, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "rotation", cancellationToken);
        }
        
        public async UniTask AnimateScaleAsync(Vector3 target, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var tween = _transform.DOScale(target, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "scale", cancellationToken);
        }
        
        public async UniTask AnimateAlphaAsync(float target, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var tween = DOTween.ToAlpha(() => _renderer.color, x => _renderer.color = x, target, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "alpha", cancellationToken);
        }
        
        public async UniTask AnimateTintColorAsync(Color target, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            // Preserve alpha value
            target.a = _renderer.color.a;
            
            var tween = DOTween.To(() => _renderer.color, x => _renderer.color = x, target, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "tintColor", cancellationToken);
        }
        
        public async UniTask AnimateVisibilityAsync(bool visible, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var tween = CreateVisibilityTween(visible, duration).SetEase(ease);
            
            await ExecuteTweenAsync(tween, "visibility", cancellationToken);
        }
        
        // === Advanced Animation Features ===
        
        public async UniTask PlaySequenceAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            if (!_sequenceLibrary.TryGetValue(sequenceName, out var sequenceData))
            {
                Debug.LogWarning($"[ActorAnimationSystem] Unknown animation sequence: {sequenceName}");
                return;
            }
            
            var sequence = CreateAnimationSequence(sequenceData);
            
            try
            {
                await WaitForSequenceAsync(sequence, cancellationToken);
            }
            finally
            {
                CleanupSequence(sequence, sequenceName);
            }
        }
        
        public async UniTask PlayComplexSequenceAsync(Action<Sequence> sequenceBuilder, CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();
            
            var sequence = DOTween.Sequence();
            sequenceBuilder?.Invoke(sequence);
            
            try
            {
                await WaitForSequenceAsync(sequence, cancellationToken);
            }
            finally
            {
                sequence.Kill();
            }
        }
        
        public Tween CreateCustomTween(System.Action<float> onUpdate, float from, float to, float duration)
        {
            ValidateNotDisposed();
            
            var tween = DOTween.To(() => from, x => onUpdate(x), to, duration);
            RegisterTween(tween, "custom");
            return tween;
        }
        
        public Sequence CreateCustomSequence()
        {
            ValidateNotDisposed();
            return DOTween.Sequence();
        }
        
        // === Animation Control ===
        
        public void StopAllAnimations()
        {
            lock (_animationLock)
            {
                foreach (var tween in _activeTweens)
                {
                    tween?.Kill();
                }
                _activeTweens.Clear();
                
                foreach (var sequence in _namedSequences.Values)
                {
                    sequence?.Kill();
                }
                _namedSequences.Clear();
                
                _namedTweens.Clear();
            }
            
            Debug.Log($"[ActorAnimationSystem] Stopped all animations for {_actor.Id}");
        }
        
        public void StopNamedAnimation(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            
            lock (_animationLock)
            {
                if (_namedTweens.TryGetValue(name, out var tween))
                {
                    tween.Kill();
                    _namedTweens.Remove(name);
                    _activeTweens.Remove(tween);
                }
                
                if (_namedSequences.TryGetValue(name, out var sequence))
                {
                    sequence.Kill();
                    _namedSequences.Remove(name);
                }
            }
        }
        
        public void PauseAllAnimations()
        {
            lock (_animationLock)
            {
                foreach (var tween in _activeTweens)
                {
                    tween?.Pause();
                }
                
                foreach (var sequence in _namedSequences.Values)
                {
                    sequence?.Pause();
                }
            }
        }
        
        public void ResumeAllAnimations()
        {
            lock (_animationLock)
            {
                foreach (var tween in _activeTweens)
                {
                    tween?.Play();
                }
                
                foreach (var sequence in _namedSequences.Values)
                {
                    sequence?.Play();
                }
            }
        }
        
        // === Utility Methods ===
        
        public Tween CreateVisibilityTween(bool visible, float duration)
        {
            if (visible)
            {
                // Fade in
                _renderer.enabled = true;
                return DOTween.ToAlpha(() => _renderer.color, x => _renderer.color = x, 1f, duration)
                    .OnComplete(() => _actor.Visible = true);
            }
            else
            {
                // Fade out
                return DOTween.ToAlpha(() => _renderer.color, x => _renderer.color = x, 0f, duration)
                    .OnComplete(() =>
                    {
                        _renderer.enabled = false;
                        _actor.Visible = false;
                    });
            }
        }
        
        public void SetAnimationSpeed(float speed)
        {
            DOTween.timeScale = Mathf.Max(0f, speed);
        }
        
        public void ResetAnimationSpeed()
        {
            DOTween.timeScale = 1f;
        }
        
        // === Configuration ===
        
        public void Configure(float defaultDuration, Ease defaultEase, bool enableRecycling = true, bool enableOptimization = true)
        {
            _defaultDuration = Mathf.Max(0f, defaultDuration);
            _defaultEase = defaultEase;
            _enableTweenRecycling = enableRecycling;
            _enableSequenceOptimization = enableOptimization;
        }
        
        public void RegisterSequence(string name, AnimationSequenceData sequenceData)
        {
            if (string.IsNullOrEmpty(name) || sequenceData == null)
                return;
            
            _sequenceLibrary[name] = sequenceData;
            Debug.Log($"[ActorAnimationSystem] Registered sequence: {name}");
        }
        
        // === Private Methods ===
        
        private void ConfigureDOTween()
        {
            // Configure DOTween for optimal performance
            DOTween.SetTweensCapacity(200, 50); // tweensCapacity, sequencesCapacity
            DOTween.useSafeMode = false; // Better performance in production
        }
        
        private async UniTask LoadAnimationSequencesAsync(CancellationToken cancellationToken)
        {
            // Load predefined animation sequences
            // This would typically load from configuration or asset files
            
            // Example: Bounce entrance
            RegisterSequence("bounce_in", new AnimationSequenceData
            {
                Name = "bounce_in",
                Duration = 1.0f,
                Steps = new List<AnimationStepData>
                {
                    new() { Type = AnimationType.Scale, Target = Vector3.zero, Duration = 0f, Ease = Ease.Linear },
                    new() { Type = AnimationType.Scale, Target = Vector3.one * 1.2f, Duration = 0.3f, Ease = Ease.OutQuad },
                    new() { Type = AnimationType.Scale, Target = Vector3.one, Duration = 0.2f, Ease = Ease.InQuad }
                }
            });
            
            // Example: Shake animation
            RegisterSequence("shake", new AnimationSequenceData
            {
                Name = "shake",
                Duration = 0.5f,
                Steps = new List<AnimationStepData>
                {
                    new() { Type = AnimationType.Position, Target = Vector3.right * 0.1f, Duration = 0.1f, Ease = Ease.InOutSine, IsRelative = true },
                    new() { Type = AnimationType.Position, Target = Vector3.left * 0.2f, Duration = 0.1f, Ease = Ease.InOutSine, IsRelative = true },
                    new() { Type = AnimationType.Position, Target = Vector3.right * 0.2f, Duration = 0.1f, Ease = Ease.InOutSine, IsRelative = true },
                    new() { Type = AnimationType.Position, Target = Vector3.left * 0.1f, Duration = 0.1f, Ease = Ease.InOutSine, IsRelative = true },
                    new() { Type = AnimationType.Position, Target = Vector3.zero, Duration = 0.1f, Ease = Ease.InOutSine, IsRelative = true }
                }
            });
            
            await UniTask.CompletedTask;
        }
        
        private Tween CreateOptimizedTween()
        {
            if (_enableTweenRecycling && _tweenPool.Count > 0)
            {
                return _tweenPool.Dequeue();
            }
            
            return null; // Let DOTween create new tween
        }
        
        private void PrewarmTweenPool()
        {
            if (!_enableTweenRecycling)
                return;
            
            // Pre-create some tweens for better performance
            for (int i = 0; i < 5; i++)
            {
                var tween = DOTween.To(() => 0f, x => { }, 1f, 1f).SetAutoKill(false);
                tween.Pause();
                _tweenPool.Enqueue(tween);
            }
        }
        
        private async UniTask ExecuteTweenAsync(Tween tween, string name, CancellationToken cancellationToken)
        {
            if (tween == null)
                return;
            
            try
            {
                RegisterTween(tween, name);
                
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                
                await WaitForTweenAsync(tween, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                tween.Kill();
                throw;
            }
            finally
            {
                CleanupTween(tween);
            }
        }
        
        private void RegisterTween(Tween tween, string name)
        {
            if (tween == null)
                return;
            
            lock (_animationLock)
            {
                _activeTweens.Add(tween);
                
                if (!string.IsNullOrEmpty(name))
                {
                    // Stop existing tween with same name
                    if (_namedTweens.TryGetValue(name, out var existingTween))
                    {
                        existingTween.Kill();
                        _activeTweens.Remove(existingTween);
                    }
                    
                    _namedTweens[name] = tween;
                }
            }
        }
        
        private void CleanupTween(Tween tween)
        {
            if (tween == null)
                return;
            
            lock (_animationLock)
            {
                _activeTweens.Remove(tween);
                
                // Remove from named tweens
                var keysToRemove = new List<string>();
                foreach (var kvp in _namedTweens)
                {
                    if (kvp.Value == tween)
                        keysToRemove.Add(kvp.Key);
                }
                
                foreach (var key in keysToRemove)
                {
                    _namedTweens.Remove(key);
                }
            }
            
            // Recycle tween if possible
            if (_enableTweenRecycling && _tweenPool.Count < _maxPoolSize)
            {
                tween.Rewind();
                tween.Pause();
                _tweenPool.Enqueue(tween);
            }
            else
            {
                tween.Kill();
            }
        }
        
        private void CleanupSequence(Sequence sequence, string name)
        {
            if (sequence == null)
                return;
            
            lock (_animationLock)
            {
                if (!string.IsNullOrEmpty(name) && _namedSequences.ContainsKey(name))
                {
                    _namedSequences.Remove(name);
                }
            }
            
            sequence.Kill();
        }
        
        private Sequence CreateAnimationSequence(AnimationSequenceData data)
        {
            var sequence = DOTween.Sequence();
            
            foreach (var step in data.Steps)
            {
                Tween tween = step.Type switch
                {
                    AnimationType.Position => step.IsRelative 
                        ? _transform.DOMove(_transform.position + (Vector3)step.Target, step.Duration)
                        : _transform.DOMove((Vector3)step.Target, step.Duration),
                    AnimationType.Rotation => _transform.DORotate(((Quaternion)step.Target).eulerAngles, step.Duration),
                    AnimationType.Scale => _transform.DOScale((Vector3)step.Target, step.Duration),
                    AnimationType.Alpha => DOTween.ToAlpha(() => _renderer.color, x => _renderer.color = x, (float)step.Target, step.Duration),
                    AnimationType.Color => DOTween.To(() => _renderer.color, x => _renderer.color = x, (Color)step.Target, step.Duration),
                    _ => null
                };
                
                if (tween != null)
                {
                    tween.SetEase(step.Ease);
                    
                    if (step.Delay > 0)
                        tween.SetDelay(step.Delay);
                    
                    if (step.IsJoined)
                        sequence.Join(tween);
                    else
                        sequence.Append(tween);
                }
            }
            
            return sequence;
        }
        
        private async UniTask WaitForTweenAsync(Tween tween, CancellationToken cancellationToken)
        {
            if (tween == null || !tween.IsActive())
                return;
            
            while (tween.IsActive() && !tween.IsComplete())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.DelayFrame(1);
            }
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
        
        private void ValidateNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActorAnimationSystem));
        }
        
        // === IDisposable Implementation ===
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            try
            {
                _disposeCts.Cancel();
                StopAllAnimations();
                
                // Clear tween pool
                while (_tweenPool.Count > 0)
                {
                    _tweenPool.Dequeue().Kill();
                }
                
                _disposeCts?.Dispose();
                _disposed = true;
                
                Debug.Log($"[ActorAnimationSystem] Disposed for actor: {_actor.Id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorAnimationSystem] Error during disposal: {ex.Message}");
            }
        }
    }
    
    // === Animation Data Structures ===
    
    [Serializable]
    public class AnimationSequenceData
    {
        public string Name;
        public float Duration;
        public List<AnimationStepData> Steps = new();
        public bool Loop = false;
        public LoopType LoopType = LoopType.Restart;
    }
    
    [Serializable]
    public class AnimationStepData
    {
        public AnimationType Type;
        public object Target;
        public float Duration;
        public float Delay = 0f;
        public Ease Ease = Ease.OutQuad;
        public bool IsJoined = false;
        public bool IsRelative = false;
    }
    
    public enum AnimationType
    {
        Position,
        Rotation,
        Scale,
        Alpha,
        Color,
        Custom
    }
}
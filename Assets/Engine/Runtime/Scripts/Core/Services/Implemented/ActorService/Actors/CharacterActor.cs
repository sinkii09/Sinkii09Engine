using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Concrete implementation of a character actor with character-specific features
    /// Handles character appearances, expressions, poses, and character interactions
    /// </summary>
    public class CharacterActor : BaseActor<CharacterAppearance>, ICharacterActor
    {
        #region Inspector Fields
        
        [Header("Character Properties")]
        [SerializeField] private CharacterLookDirection _lookDirection = CharacterLookDirection.Right;
        [SerializeField] private Color _characterColor = Color.white;
        [SerializeField] private string _currentEmotion = "Neutral";
        
        [Header("Character Components")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;
        
        [Header("Character Animation Settings")]
        [SerializeField] private float _expressionTransitionDuration = 0.5f;
        [SerializeField] private float _poseTransitionDuration = 1.0f;
        [SerializeField] private float _lookDirectionDuration = 0.3f;
        
        #endregion
        
        #region Properties
        
        public override ActorType ActorType => ActorType.Character;
        
        public CharacterLookDirection LookDirection
        {
            get => _lookDirection;
            set => _lookDirection = value;
        }
        
        public Color CharacterColor
        {
            get => _characterColor;
            set => _characterColor = value;
        }
        
        public string CurrentEmotion
        {
            get => _currentEmotion;
            set => _currentEmotion = value;
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the character actor with basic properties
        /// Called by ActorFactory when creating new actors
        /// </summary>
        public void Initialize(string actorId, CharacterAppearance appearance)
        {
            _id = actorId;
            _displayName = actorId;
            _appearance = appearance;
            _currentEmotion = appearance.Expression.ToString();
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            base.Awake();
            
            // Get or add required components
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                
            if (_spriteRenderer == null)
            {
                var spriteObject = new GameObject("Sprite");
                spriteObject.transform.SetParent(transform);
                spriteObject.transform.localPosition = Vector3.zero;
                _spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            }
            
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }
        
        protected override void Start()
        {
            base.Start();
            
            // Apply initial character settings
            ApplyCharacterColor();
            ApplyLookDirection();
        }
        
        #endregion
        
        #region BaseActor Implementation
        
        protected override async UniTask LoadAppearanceAssetsAsync(CharacterAppearance appearance, CancellationToken cancellationToken)
        {
            try
            {
                // Generate addressable key for the appearance
                var addressableKey = appearance.GetAddressableKey(_id);
                // Load sprite using ResourceService
                var spriteResource = await _resourceService.LoadResourceAsync<Sprite>(addressableKey, cancellationToken);
                
                if (spriteResource != null && spriteResource.IsValid)
                {
                    _spriteRenderer.sprite = spriteResource.Asset;
                    UnityEngine.Debug.Log($"[CharacterActor] Loaded sprite for {_id}");
                }
                else
                {
                    // Fallback to default character sprite
                    UnityEngine.Debug.LogWarning($"[CharacterActor] Failed to load sprite for {_id}, using default");
                    await LoadDefaultCharacterSprite(cancellationToken);
                }
                
                // Update current emotion based on expression
                _currentEmotion = appearance.Expression.ToString();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterActor] Error loading appearance for {_id}: {ex.Message}");
                await LoadDefaultCharacterSprite(cancellationToken);
            }
        }
        
        protected override async UniTask AnimateAppearanceChangeAsync(CharacterAppearance oldAppearance, CharacterAppearance newAppearance, float duration, CancellationToken cancellationToken)
        {
            // Create a sequence of animations for smooth appearance change
            var sequence = DOTween.Sequence();
            
            // Fade out current appearance
            sequence.Append(_spriteRenderer.DOFade(0f, duration * 0.3f));
            
            // Change sprite in the middle
            sequence.AppendCallback(() =>
            {
                // Sprite should already be loaded by LoadAppearanceAssetsAsync
                ApplyCharacterColor();
            });
            
            // Fade in new appearance
            sequence.Append(_spriteRenderer.DOFade(_alpha, duration * 0.7f));
            
            // Animate any pose or expression changes
            if (oldAppearance.Pose != newAppearance.Pose)
            {
                sequence.Join(AnimatePoseChange(oldAppearance.Pose, newAppearance.Pose, duration));
            }
            
            await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        #endregion
        
        #region ICharacterActor Implementation
        
        public async UniTask ChangeExpressionAsync(CharacterEmotion emotion, float duration = 0.5f, CancellationToken cancellationToken = default)
        {
            var newAppearance = _appearance.WithExpression(emotion);
            await ChangeAppearanceAsync(newAppearance, duration, cancellationToken);
            _currentEmotion = emotion.ToString();
        }
        
        public async UniTask ChangePoseAsync(CharacterPose pose, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var newAppearance = _appearance.WithPose(pose);
            await ChangeAppearanceAsync(newAppearance, duration, cancellationToken);
        }
        
        public async UniTask ChangeLookDirectionAsync(CharacterLookDirection direction, float duration = 0.5f, CancellationToken cancellationToken = default)
        {
            _lookDirection = direction;
            
            // Animate the look direction change
            var targetScale = transform.localScale;
            targetScale.x = direction == CharacterLookDirection.Left ? -Mathf.Abs(targetScale.x) : Mathf.Abs(targetScale.x);
            
            await transform.DOScale(targetScale, duration).AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        
        public async UniTask ChangeOutfitAsync(int outfitId, float duration = 1.0f, CancellationToken cancellationToken = default)
        {
            var newAppearance = _appearance.WithOutfit(outfitId);
            await ChangeAppearanceAsync(newAppearance, duration, cancellationToken);
        }
        
        public async UniTask SpeakAsync(string message, CancellationToken cancellationToken = default)
        {
            try
            {
                UnityEngine.Debug.Log($"[CharacterActor] {_displayName}: {message}");
                
                // Animate speaking (could integrate with dialogue system)
                var speakSequence = DOTween.Sequence();
                
                // Slight bounce animation while speaking
                speakSequence.Append(transform.DOScale(transform.localScale * 1.05f, 0.1f));
                speakSequence.Append(transform.DOScale(transform.localScale, 0.1f));
                
                // Play speaking animation if animator exists
                if (_animator != null)
                {
                    _animator.SetTrigger("Speak"); // Could be made configurable if needed
                }
                
                await speakSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterActor] Error during speak animation: {ex.Message}");
            }
        }
        
        public async UniTask EmoteAsync(CharacterEmotion emotion, float duration = 2.0f, CancellationToken cancellationToken = default)
        {
            var originalExpression = _appearance.Expression;
            
            try
            {
                // Change to emote expression
                await ChangeExpressionAsync(emotion, 0.3f, cancellationToken);
                
                // Hold the expression
                await UniTask.Delay(TimeSpan.FromSeconds(duration - 0.6f), cancellationToken: cancellationToken);
                
                // Return to original expression
                await ChangeExpressionAsync(originalExpression, 0.3f, cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterActor] Error during emote: {ex.Message}");
                // Try to return to original expression
                try
                {
                    await ChangeExpressionAsync(originalExpression, 0.1f, CancellationToken.None);
                }
                catch { /* Silent fail on recovery */ }
            }
        }
        
        public async UniTask ReactAsync(CharacterEmotion emotion, float intensity = 1.0f, CancellationToken cancellationToken = default)
        {
            await ReactAsync(CharacterReactionType.EmotionBased, emotion, intensity, cancellationToken);
        }
        
        public async UniTask ReactAsync(CharacterReactionType reactionType, CharacterEmotion emotion, float intensity = 1.0f, CancellationToken cancellationToken = default)
        {
            try
            {
                UnityEngine.Debug.Log($"[CharacterActor] {_displayName} reacts: {emotion} (intensity: {intensity}) with {reactionType}");
                
                var reactionSequence = DOTween.Sequence();
                
                // Handle physical reaction type
                if (reactionType != CharacterReactionType.EmotionBased)
                {
                    switch (reactionType)
                    {
                        case CharacterReactionType.Stumble:
                            reactionSequence.Append(transform.DOShakePosition(0.5f, 0.2f * intensity));
                            break;
                        case CharacterReactionType.Shiver:
                            reactionSequence.Append(transform.DOShakeScale(1.0f, Vector3.one * (0.05f * intensity)));
                            break;
                        case CharacterReactionType.Bounce:
                            reactionSequence.Append(transform.DOPunchPosition(Vector3.up * (0.2f * intensity), 0.6f));
                            break;
                        case CharacterReactionType.Shake:
                            reactionSequence.Append(transform.DOShakePosition(1.0f, 0.15f * intensity));
                            break;
                        case CharacterReactionType.Shrink:
                            reactionSequence.Append(transform.DOScale(transform.localScale * (0.9f - 0.1f * intensity), 0.3f));
                            reactionSequence.Append(transform.DOScale(transform.localScale, 0.3f));
                            break;
                        case CharacterReactionType.Puff:
                            reactionSequence.Append(transform.DOScale(transform.localScale * (1.1f + 0.1f * intensity), 0.3f));
                            reactionSequence.Append(transform.DOScale(transform.localScale, 0.3f));
                            break;
                        case CharacterReactionType.Spin:
                            reactionSequence.Append(transform.DORotate(new Vector3(0, 0, 360f * intensity), 0.8f, RotateMode.FastBeyond360));
                            break;
                        case CharacterReactionType.Jump:
                            reactionSequence.Append(transform.DOJump(transform.position, 0.3f * intensity, 1, 0.8f));
                            break;
                        case CharacterReactionType.Duck:
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y - (0.2f * intensity), 0.2f));
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y, 0.2f));
                            break;
                    }
                }
                else
                {
                    // Handle emotion-based reactions
                    switch (emotion)
                    {
                        case CharacterEmotion.Shock:
                        case CharacterEmotion.Surprised:
                            reactionSequence.Append(transform.DOPunchScale(Vector3.one * (0.2f * intensity), 0.5f));
                            break;
                            
                        case CharacterEmotion.Joy:
                        case CharacterEmotion.Happy:
                        case CharacterEmotion.Bliss:
                            reactionSequence.Append(transform.DOPunchPosition(Vector3.up * (0.1f * intensity), 0.8f));
                            break;
                            
                        case CharacterEmotion.Sad:
                        case CharacterEmotion.Disappointed:
                        case CharacterEmotion.Despair:
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y - (0.1f * intensity), 0.5f));
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y, 0.5f));
                            break;
                            
                        case CharacterEmotion.Angry:
                        case CharacterEmotion.Rage:
                            reactionSequence.Append(transform.DOShakePosition(1.0f, 0.1f * intensity));
                            break;
                            
                        case CharacterEmotion.Fear:
                        case CharacterEmotion.Panic:
                            reactionSequence.Append(transform.DOShakeScale(0.8f, Vector3.one * (0.1f * intensity)));
                            break;
                            
                        case CharacterEmotion.Confused:
                            reactionSequence.Append(transform.DORotate(new Vector3(0, 0, 15f * intensity), 0.2f));
                            reactionSequence.Append(transform.DORotate(new Vector3(0, 0, -15f * intensity), 0.2f));
                            reactionSequence.Append(transform.DORotate(Vector3.zero, 0.2f));
                            break;
                            
                        case CharacterEmotion.Excited:
                            reactionSequence.Append(transform.DOPunchPosition(Vector3.up * (0.15f * intensity), 0.6f));
                            reactionSequence.Join(transform.DOPunchScale(Vector3.one * (0.1f * intensity), 0.6f));
                            break;
                            
                        case CharacterEmotion.Love:
                            reactionSequence.Append(transform.DOScale(transform.localScale * (1.1f + 0.1f * intensity), 0.3f));
                            reactionSequence.Append(transform.DOScale(transform.localScale, 0.3f));
                            break;
                            
                        case CharacterEmotion.Embarrassed:
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y - (0.05f * intensity), 0.3f));
                            reactionSequence.Append(transform.DOLocalMoveY(transform.localPosition.y, 0.3f));
                            break;
                            
                        case CharacterEmotion.Thinking:
                            reactionSequence.Append(transform.DORotate(new Vector3(0, 0, 5f), 0.5f));
                            reactionSequence.Append(transform.DORotate(Vector3.zero, 0.5f));
                            break;
                            
                        case CharacterEmotion.Neutral:
                        default:
                            // Minimal reaction for neutral or unknown types
                            reactionSequence.Append(transform.DOPunchScale(Vector3.one * (0.05f * intensity), 0.3f));
                            break;
                    }
                }
                
                // Set animator trigger based on reaction type or emotion
                if (_animator != null)
                {
                    string triggerName = reactionType == CharacterReactionType.EmotionBased ? emotion.ToString() : reactionType.ToString();
                    _animator.SetTrigger(triggerName);
                }
                
                await reactionSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterActor] Error during reaction: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private async UniTask LoadDefaultCharacterSprite(CancellationToken cancellationToken)
        {
            try
            {
                // Try to load a default character sprite
                var defaultResource = await _resourceService.LoadResourceAsync<Sprite>("char_default_neutral_standing_00", cancellationToken);
                if (defaultResource != null && defaultResource.IsValid)
                {
                    _spriteRenderer.sprite = defaultResource.Asset;
                }
                else
                {
                    // Create a simple colored sprite as ultimate fallback
                    CreateFallbackSprite();
                }
            }
            catch
            {
                CreateFallbackSprite();
            }
        }
        
        private void CreateFallbackSprite()
        {
            // Create a simple colored rectangle as fallback
            var texture = new Texture2D(100, 150);
            var fillColor = _characterColor == Color.white ? Color.cyan : _characterColor;
            
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    texture.SetPixel(x, y, fillColor);
                }
            }
            texture.Apply();
            
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            _spriteRenderer.sprite = sprite;
            
            UnityEngine.Debug.Log($"[CharacterActor] Created fallback sprite for {_id}");
        }
        
        private void ApplyCharacterColor()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _characterColor;
            }
        }
        
        private void ApplyLookDirection()
        {
            var scale = transform.localScale;
            scale.x = _lookDirection == CharacterLookDirection.Left ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
        
        private Tween AnimatePoseChange(CharacterPose oldPose, CharacterPose newPose, float duration)
        {
            // Simple pose change animation - could be expanded with more complex animations
            if (_animator != null)
            {
                _animator.SetTrigger(newPose.ToString());
            }
            
            // Return a simple scale animation as placeholder
            return transform.DOPunchScale(Vector3.one * 0.1f, duration * 0.5f);
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
            
            // Character-specific validation
            if (_spriteRenderer == null)
                baseErrors.Add("CharacterActor requires a SpriteRenderer component");
                
            if (string.IsNullOrEmpty(_currentEmotion))
                baseErrors.Add("CharacterActor requires a current emotion to be set");
            
            errors = baseErrors.ToArray();
            return baseErrors.Count == 0;
        }
        
        public override string GetDebugInfo()
        {
            var baseInfo = base.GetDebugInfo();
            return $"{baseInfo}\n" +
                   $"Look Direction: {_lookDirection}\n" +
                   $"Character Color: {_characterColor}\n" +
                   $"Current Emotion: {_currentEmotion}\n" +
                   $"Expression: {_appearance.Expression}\n" +
                   $"Pose: {_appearance.Pose}\n" +
                   $"Outfit: {_appearance.OutfitId}";
        }
        
        #endregion
    }
}
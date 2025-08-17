using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using DG.Tweening; // DOTween integration

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base non-generic interface for polymorphic collections and service management
    /// </summary>
    public interface IActor : IDisposable
    {
        // Core Identity Properties
        string Id { get; }
        ActorType ActorType { get; }
        string DisplayName { get; set; }
        
        // Visibility and State Management  
        bool Visible { get; set; }
        ActorVisibilityState VisibilityState { get; }
        ActorLoadState LoadState { get; }
        
        // Transform Properties
        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }
        Vector3 Scale { get; set; }
        
        // Visual Properties
        Color TintColor { get; set; }
        float Alpha { get; set; }
        int SortingOrder { get; set; }
        
        // Resource Management
        bool IsLoaded { get; }
        float LoadProgress { get; }
        bool HasError { get; }
        string LastError { get; }
        
        // Unity Integration
        GameObject GameObject { get; }
        Transform Transform { get; }
        
        // Generic appearance access for polymorphic scenarios
        IAppearance GetAppearance();
        UniTask SetAppearanceAsync(IAppearance appearance, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        // Events
        event Action<IActor> OnLoaded;
        event Action<IActor> OnUnloaded;
        event Action<IActor, string> OnError;
        event Action<IActor, bool> OnVisibilityChanged;
        
        // === Core Lifecycle Methods ===
        
        /// <summary>
        /// Initializes the actor with the given configuration
        /// </summary>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Loads all required resources for this actor
        /// </summary>
        UniTask LoadResourcesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Unloads resources and prepares for destruction
        /// </summary>
        UniTask UnloadResourcesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Destroys the actor and cleans up all resources
        /// </summary>
        UniTask DestroyAsync(CancellationToken cancellationToken = default);
        
        // === Animation Methods (DOTween Integration) ===
        
        /// <summary>
        /// Animates position change with DOTween easing
        /// </summary>
        UniTask ChangePositionAsync(Vector3 position, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animates rotation change with DOTween easing
        /// </summary>
        UniTask ChangeRotationAsync(Quaternion rotation, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animates scale change with DOTween easing
        /// </summary>
        UniTask ChangeScaleAsync(Vector3 scale, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animates visibility change with fade effect
        /// </summary>
        UniTask ChangeVisibilityAsync(bool visible, float duration = 1.0f, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animates alpha change
        /// </summary>
        UniTask ChangeAlphaAsync(float alpha, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Animates tint color change
        /// </summary>
        UniTask ChangeTintColorAsync(Color color, float duration, Ease ease = Ease.OutQuad, CancellationToken cancellationToken = default);
        
        
        /// <summary>
        /// Plays a complex animation sequence
        /// </summary>
        UniTask PlayAnimationSequenceAsync(string sequenceName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops all current animations
        /// </summary>
        void StopAllAnimations();
        
        // === State Management ===
        
        /// <summary>
        /// Captures the current state of the actor for serialization
        /// </summary>
        ActorState GetState();
        
        /// <summary>
        /// Applies a previously captured state to the actor
        /// </summary>
        UniTask ApplyStateAsync(ActorState state, float duration = 0f, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates if the given state can be applied to this actor
        /// </summary>
        bool CanApplyState(ActorState state);
        
        // === Utility Methods ===
        
        /// <summary>
        /// Refreshes the actor's visual representation
        /// </summary>
        UniTask RefreshAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates the actor's current configuration
        /// </summary>
        bool ValidateConfiguration(out string[] errors);
        
        /// <summary>
        /// Gets debug information about the actor
        /// </summary>
        string GetDebugInfo();
    }
    
    /// <summary>
    /// Generic actor interface with strongly-typed appearance
    /// </summary>
    /// <typeparam name="TAppearance">The strongly-typed appearance type</typeparam>
    public interface IActor<TAppearance> : IActor 
        where TAppearance : struct, IAppearance
    {
        /// <summary>
        /// Strongly-typed appearance property
        /// </summary>
        TAppearance Appearance { get; set; }
        
        /// <summary>
        /// Changes appearance with type safety
        /// </summary>
        UniTask ChangeAppearanceAsync(TAppearance newAppearance, float duration = 1.0f, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Specialized interface for character actors with character-specific features
    /// </summary>
    public interface ICharacterActor : IActor<CharacterAppearance>
    {
        
        // Character-specific properties
        CharacterLookDirection LookDirection { get; set; }
        Color CharacterColor { get; set; }
        string CurrentEmotion { get; set; }
        
        // Character-specific animation methods (ChangeAppearanceAsync inherited from IActor<CharacterAppearance>)
        UniTask ChangeExpressionAsync(CharacterEmotion emotion, float duration = 0.5f, CancellationToken cancellationToken = default);
        UniTask ChangePoseAsync(CharacterPose pose, float duration = 1.0f, CancellationToken cancellationToken = default);
        UniTask ChangeLookDirectionAsync(CharacterLookDirection direction, float duration = 0.5f, CancellationToken cancellationToken = default);
        UniTask ChangeOutfitAsync(int outfitId, float duration = 1.0f, CancellationToken cancellationToken = default);
        
        // Character interaction methods
        UniTask SpeakAsync(string message, CancellationToken cancellationToken = default);
        UniTask EmoteAsync(CharacterEmotion emotion, float duration = 2.0f, CancellationToken cancellationToken = default);
        UniTask ReactAsync(CharacterEmotion emotion, float intensity = 1.0f, CancellationToken cancellationToken = default);
        UniTask ReactAsync(CharacterReactionType reactionType, CharacterEmotion emotion, float intensity = 1.0f, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Specialized interface for background actors with scene management features
    /// </summary>
    public interface IBackgroundActor : IActor<BackgroundAppearance>
    {
        
        // Background-specific properties
        SceneTransitionType TransitionType { get; set; }
        float ParallaxFactor { get; set; }
        bool IsMainBackground { get; set; }
        
        // Background-specific animation methods (ChangeAppearanceAsync inherited from IActor<BackgroundAppearance>)
        UniTask ChangeLocationAsync(SceneLocation location, int variantId = 0, float duration = 2.0f, CancellationToken cancellationToken = default);
        UniTask TransitionToAsync(BackgroundAppearance newBackground, SceneTransitionType transition, float duration = 2.0f, CancellationToken cancellationToken = default);
        
        // Scene management methods
        UniTask SetAsMainBackgroundAsync(CancellationToken cancellationToken = default);
        UniTask FadeInAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
        UniTask FadeOutAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
    }
}
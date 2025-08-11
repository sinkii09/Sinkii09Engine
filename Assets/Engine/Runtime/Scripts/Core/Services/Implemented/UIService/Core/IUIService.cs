using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Core interface for UI management system providing screen lifecycle and navigation
    /// </summary>
    public interface IUIService : IEngineService
    {
        #region Screen Management

        /// <summary>
        /// Asynchronously show a UI screen using enum type
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ShowAsync(UIScreenType screenType, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Asynchronously show a UI screen using enum type with context data
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="context">Context data to pass to the screen</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ShowAsync(UIScreenType screenType, UIScreenContext context, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Asynchronously show a UI screen using enum type with context data and display configuration
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="context">Context data to pass to the screen</param>
        /// <param name="displayConfig">Display configuration for the screen</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ShowAsync(UIScreenType screenType, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously hide a specific UI screen using enum type
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask HideAsync(UIScreenType screenType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hide all instances of a specific screen type (useful for multiple instance screens)
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask HideAllInstancesAsync(UIScreenType screenType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hide the topmost instance of a specific screen type
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask HideTopInstanceAsync(UIScreenType screenType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the currently active screen
        /// </summary>
        /// <returns>Currently active UIScreen or null if none</returns>
        UIScreen GetActiveScreen();

        /// <summary>
        /// Get the currently active screen type
        /// </summary>
        /// <returns>Currently active screen type or UIScreenType.None if none</returns>
        UIScreenType GetActiveScreenType();

        /// <summary>
        /// Check if a specific screen is currently active using enum type
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <returns>True if the screen is currently active</returns>
        bool IsScreenActive(UIScreenType screenType);

        /// <summary>
        /// Check if a specific screen is currently active using asset reference (legacy support)
        /// </summary>
        /// <param name="screenAsset">ScriptableObject reference to check</param>
        /// <returns>True if the screen is currently active</returns>
        bool IsScreenActive(UIScreenAsset screenAsset);
        
        /// <summary>
        /// Get navigation breadcrumbs for the current stack
        /// </summary>
        /// <returns>List of screen IDs in the navigation stack</returns>
        IReadOnlyList<string> GetNavigationBreadcrumbs();
        
        /// <summary>
        /// Get current stack depth
        /// </summary>
        /// <returns>Number of screens in the stack</returns>
        int GetStackDepth();
        
        /// <summary>
        /// Get maximum allowed stack depth
        /// </summary>
        /// <returns>Maximum stack depth configured</returns>
        int GetMaxStackDepth();

        #endregion

        #region Navigation

        /// <summary>
        /// Navigate back to the previous screen in the stack
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask PopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pop screens until reaching the specified screen type
        /// </summary>
        /// <param name="screenType">Target screen type to pop to</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask PopToAsync(UIScreenType screenType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace the current screen with a new one using enum type
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ReplaceAsync(UIScreenType screenType, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Replace the current screen with a new one using enum type with context data
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="context">Context data to pass to the screen</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ReplaceAsync(UIScreenType screenType, UIScreenContext context, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Replace the current screen with a new one using enum type with context data and display configuration
        /// </summary>
        /// <param name="screenType">Type-safe screen identifier</param>
        /// <param name="context">Context data to pass to the screen</param>
        /// <param name="displayConfig">Display configuration for the screen</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ReplaceAsync(UIScreenType screenType, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace the current screen with a new one using asset reference (legacy support)
        /// </summary>
        /// <param name="screenAsset">ScriptableObject reference to the new screen</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ReplaceAsync(UIScreenAsset screenAsset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all screens from the stack
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the operation</returns>
        UniTask ClearAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Fluent Navigation

        /// <summary>
        /// Create a fluent navigation builder for complex navigation operations
        /// </summary>
        /// <returns>Navigation builder for method chaining</returns>
        IUINavigationBuilder Navigate();

        #endregion

        #region Memory Management

        /// <summary>
        /// Handle memory pressure by cleaning up cached and pooled resources
        /// </summary>
        /// <param name="pressureLevel">Memory pressure level (0.0 to 1.0)</param>
        void RespondToMemoryPressure(float pressureLevel);

        /// <summary>
        /// Get memory usage statistics for UI system
        /// </summary>
        /// <returns>Tuple containing cache and pool statistics</returns>
        (int cachedScreens, int totalPooledInstances, int totalActiveInstances, double cacheEfficiency, double poolEfficiency) GetMemoryStats();

        (int available, int active, int totalCreated, double efficiency) GetPoolStats(UIScreenType screenType);

        #endregion

        #region Advanced Features (Future Implementation)

        // TODO: Implement blur effect for modal backdrops
        // void SetBackdropBlurEnabled(bool enabled);
        // void SetBackdropBlurIntensity(float intensity);

        #endregion
    }
}
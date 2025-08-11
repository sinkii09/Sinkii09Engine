using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Fluent builder interface for UI navigation operations
    /// </summary>
    public interface IUINavigationBuilder
    {
        /// <summary>
        /// Set the target screen type to navigate to
        /// </summary>
        /// <param name="screenType">The screen type to show</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder To(UIScreenType screenType);
        
        /// <summary>
        /// Add data to the navigation context
        /// </summary>
        /// <typeparam name="T">Type of data to add</typeparam>
        /// <param name="data">Data instance to add</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithData<T>(T data);
        
        /// <summary>
        /// Set the complete context for the navigation
        /// </summary>
        /// <param name="context">Context to use for navigation</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithContext(UIScreenContext context);
        
        /// <summary>
        /// Set the transition type for this navigation (applies to both in and out transitions)
        /// </summary>
        /// <param name="transition">Transition animation to use</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithTransition(TransitionType transition);
        
        /// <summary>
        /// Set the in transition type for showing this screen
        /// </summary>
        /// <param name="transition">Transition animation to use when showing</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithInTransition(TransitionType transition);
        
        /// <summary>
        /// Set the out transition type for hiding this screen
        /// </summary>
        /// <param name="transition">Transition animation to use when hiding</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithOutTransition(TransitionType transition);
        
        /// <summary>
        /// Show the screen as a modal (blocking interaction with previous screens)
        /// </summary>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder AsModal();
        
        /// <summary>
        /// Show the screen as an overlay (keeping previous screens active)
        /// </summary>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder AsOverlay();
        
        /// <summary>
        /// Set custom display configuration for the screen
        /// </summary>
        /// <param name="displayConfig">Display configuration to apply</param>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder WithDisplayConfig(UIDisplayConfig displayConfig);
        
        /// <summary>
        /// Clear the entire screen stack before showing the new screen
        /// </summary>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder ClearStack();
        
        /// <summary>
        /// Replace the current screen instead of pushing to the stack
        /// </summary>
        /// <returns>Builder for method chaining</returns>
        IUINavigationBuilder Replace();
        
        /// <summary>
        /// Execute the navigation operation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the navigation operation</returns>
        UniTask ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Context interface providing access to UIService dependencies for components
    /// </summary>
    public interface IUIServiceContext
    {
        /// <summary>
        /// UI service configuration
        /// </summary>
        UIServiceConfiguration Configuration { get; }

        /// <summary>
        /// Resource service for loading assets
        /// </summary>
        IResourceService ResourceService { get; }

        /// <summary>
        /// Screen registry for type-to-asset mapping
        /// </summary>
        UIScreenRegistry ScreenRegistry { get; }

        /// <summary>
        /// UI root canvas
        /// </summary>
        Canvas UIRoot { get; }

        /// <summary>
        /// Get another component by type (for inter-component communication)
        /// </summary>
        T GetComponent<T>() where T : class, IUIComponent;

        /// <summary>
        /// Check if a component exists and is initialized
        /// </summary>
        bool HasComponent<T>() where T : class, IUIComponent;
    }
}
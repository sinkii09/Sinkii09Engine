using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base class for all UI screens with simple lifecycle management
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public abstract class UIScreen : MonoBehaviour
    {
        // Screen asset is now injected at runtime by UIService - no manual reference needed
        private UIScreenAsset _screenAsset;

        /// <summary>
        /// The screen asset that defines this screen (automatically injected by UIService)
        /// </summary>
        public UIScreenAsset ScreenAsset 
        { 
            get => _screenAsset;
            internal set => _screenAsset = value;
        }
        
        /// <summary>
        /// The screen type (derived from the injected ScreenAsset)
        /// </summary>
        public UIScreenType ScreenType => _screenAsset?.ScreenType ?? UIScreenType.None;

        /// <summary>
        /// Whether this screen is currently active and visible
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// The canvas component for this screen
        /// </summary>
        public Canvas Canvas { get; private set; }

        /// <summary>
        /// Context data passed to this screen during navigation
        /// </summary>
        protected UIScreenContext Context { get; private set; }

        /// <summary>
        /// Display configuration for this screen
        /// </summary>
        public UIDisplayConfig DisplayConfig { get; private set; }

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            Canvas = GetComponent<Canvas>();
            
            // Ensure screen starts inactive
            if (Canvas != null)
            {
                Canvas.enabled = false;
            }
            
            IsActive = false;
            
            // Note: ScreenAsset will be injected by UIService at runtime - no validation needed here
        }

        #endregion

        #region Screen Lifecycle

        /// <summary>
        /// Called when the screen is initialized with context data. Override to handle initialization.
        /// </summary>
        /// <param name="context">Context data for this screen</param>
        protected virtual void OnInitialize(UIScreenContext context)
        {
            // Default implementation - override in derived classes
        }

        /// <summary>
        /// Called when the screen is shown. Override to implement custom show logic.
        /// </summary>
        protected virtual void OnShow()
        {
            // Default implementation - override in derived classes
        }

        /// <summary>
        /// Called when the screen is hidden. Override to implement custom hide logic.
        /// </summary>
        protected virtual void OnHide()
        {
            // Default implementation - override in derived classes
        }

        /// <summary>
        /// Called when the screen is being destroyed. Override to implement cleanup.
        /// </summary>
        protected virtual void OnDestroy()
        {
            // Default implementation - override in derived classes
        }

        /// <summary>
        /// Called when back button/key is pressed. Return false to prevent default back behavior.
        /// </summary>
        /// <returns>True to allow back navigation, false to prevent it</returns>
        public virtual bool OnBackPressed()
        {
            return true; // Allow back navigation by default
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Internal method to initialize the screen with context - called by UIService
        /// </summary>
        /// <param name="context">Context data for this screen</param>
        /// <param name="displayConfig">Display configuration for this screen</param>
        internal void InternalInitialize(UIScreenContext context, UIDisplayConfig displayConfig = null)
        {
            // Validate that ScreenAsset has been injected
            if (_screenAsset == null)
            {
                Debug.LogError($"UIScreen '{gameObject.name}' was not properly initialized - ScreenAsset is null. " +
                              "This should be automatically injected by UIService.", this);
            }
            
            Context = context ?? new UIScreenContext();
            DisplayConfig = displayConfig ?? UIDisplayConfig.Normal;
            
            // Apply display configuration to canvas
            ApplyDisplayConfiguration();
            
            OnInitialize(Context);
        }

        /// <summary>
        /// Internal method to show the screen - called by UIService
        /// </summary>
        internal void InternalShow()
        {
            if (IsActive) return;

            IsActive = true;
            
            if (Canvas != null)
            {
                Canvas.enabled = true;
            }
            
            gameObject.SetActive(true);
            OnShow();
        }

        /// <summary>
        /// Internal method to hide the screen - called by UIService
        /// </summary>
        internal void InternalHide()
        {
            if (!IsActive) return;

            IsActive = false;
            OnHide();
            
            if (Canvas != null)
            {
                Canvas.enabled = false;
            }
            
            gameObject.SetActive(false);
        }

        #endregion

        #region Display Configuration

        /// <summary>
        /// Apply the display configuration to this screen's canvas
        /// </summary>
        private void ApplyDisplayConfiguration()
        {
            if (Canvas == null || DisplayConfig == null) return;

            // Apply sorting order offset
            var baseSortingOrder = Canvas.sortingOrder;
            var newSortingOrder = baseSortingOrder + DisplayConfig.SortingOrderOffset;
            
            Debug.Log($"[UIScreen] {gameObject.name} - Base sorting order: {baseSortingOrder}, Offset: {DisplayConfig.SortingOrderOffset}, New: {newSortingOrder}");
            
            Canvas.sortingOrder = newSortingOrder;

            // Handle modal/overlay specific setup
            if (DisplayConfig.IsModal || DisplayConfig.IsOverlay)
            {
                // Ensure the screen is on top of others
                Canvas.overrideSorting = true;
                
                Debug.Log($"[UIScreen] {gameObject.name} - Canvas override sorting enabled, final order: {Canvas.sortingOrder}");
                
                // For modals, we'll create a backdrop in the UIService
                // For overlays, just ensure proper layering
            }
        }

        /// <summary>
        /// Handle input for modal behavior (ESC key, etc.)
        /// </summary>
        protected virtual void Update()
        {
            if (IsActive && DisplayConfig != null && DisplayConfig.IsModal && DisplayConfig.CloseOnEscape)
            {
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                {
                    OnModalCloseRequested();
                }
            }
        }

        /// <summary>
        /// Called when a modal close is requested (ESC key, backdrop click, etc.)
        /// Override to provide custom close behavior
        /// </summary>
        protected virtual void OnModalCloseRequested()
        {
            // Default behavior - hide the screen
            // The UIService will handle the actual closing
            if (DisplayConfig?.IsModal == true)
            {
                // Notify UIService to close this modal
                // This is handled through events or direct service calls
                InternalHide();
            }
        }

        /// <summary>
        /// Check if this screen blocks interaction with underlying screens
        /// </summary>
        public bool BlocksInteraction => DisplayConfig?.BlocksInteraction ?? false;

        #endregion
    }
}
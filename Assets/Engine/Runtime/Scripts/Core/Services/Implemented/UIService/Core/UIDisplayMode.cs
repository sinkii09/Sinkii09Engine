namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines how a UI screen should be displayed in relation to other screens
    /// </summary>
    public enum UIDisplayMode
    {
        /// <summary>
        /// Normal screen behavior - replaces or stacks according to service configuration
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// Overlay screen - displays on top without affecting underlying screens
        /// Underlying screens remain interactive
        /// </summary>
        Overlay = 1,
        
        /// <summary>
        /// Modal screen - displays on top and blocks interaction with underlying screens
        /// Usually includes a backdrop that prevents interaction
        /// </summary>
        Modal = 2
    }
    
    /// <summary>
    /// Configuration for modal backdrop behavior
    /// </summary>
    public enum ModalBackdropBehavior
    {
        /// <summary>
        /// No backdrop - just block interaction
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Semi-transparent dark backdrop
        /// </summary>
        Dimmed = 1,
        
        /// <summary>
        /// Blur effect backdrop
        /// </summary>
        Blurred = 2,
        
        /// <summary>
        /// Custom backdrop (user-defined)
        /// </summary>
        Custom = 3
    }
    
    /// <summary>
    /// Configuration for UI display behavior
    /// </summary>
    [System.Serializable]
    public class UIDisplayConfig
    {
        /// <summary>
        /// How the screen should be displayed
        /// </summary>
        public UIDisplayMode DisplayMode = UIDisplayMode.Normal;
        
        /// <summary>
        /// Backdrop behavior for modal screens
        /// </summary>
        public ModalBackdropBehavior BackdropBehavior = ModalBackdropBehavior.Dimmed;
        
        /// <summary>
        /// Custom sorting order offset for this screen
        /// </summary>
        public int SortingOrderOffset = 0;
        
        /// <summary>
        /// Whether clicking the backdrop should close the modal
        /// </summary>
        public bool CloseOnBackdropClick = true;
        
        /// <summary>
        /// Whether pressing ESC/back should close the modal
        /// </summary>
        public bool CloseOnEscape = true;
        
        /// <summary>
        /// Backdrop opacity for dimmed backdrop (0.0 to 1.0)
        /// </summary>
        public float BackdropOpacity = 0.5f;
        
        /// <summary>
        /// Blur radius for blurred backdrop (pixels)
        /// </summary>
        public float BlurRadius = 10f;
        
        /// <summary>
        /// Number of blur iterations for quality vs performance trade-off
        /// </summary>
        public int BlurIterations = 3;
        
        /// <summary>
        /// Blur animation duration when showing/hiding
        /// </summary>
        public float BlurAnimationDuration = 0.3f;
        
        /// <summary>
        /// Transition type for showing this screen
        /// </summary>
        public TransitionType InTransition = TransitionType.None;
        
        /// <summary>
        /// Transition type for hiding this screen
        /// </summary>
        public TransitionType OutTransition = TransitionType.None;
        
        /// <summary>
        /// Check if this configuration represents a modal display
        /// </summary>
        public bool IsModal => DisplayMode == UIDisplayMode.Modal;
        
        /// <summary>
        /// Check if this configuration represents an overlay display
        /// </summary>
        public bool IsOverlay => DisplayMode == UIDisplayMode.Overlay;
        
        /// <summary>
        /// Check if this configuration blocks interaction with underlying screens
        /// </summary>
        public bool BlocksInteraction => DisplayMode == UIDisplayMode.Modal;
        
        /// <summary>
        /// Create a default normal display config
        /// </summary>
        public static UIDisplayConfig Normal => new UIDisplayConfig();
        
        /// <summary>
        /// Create a default overlay display config
        /// </summary>
        public static UIDisplayConfig Overlay => new UIDisplayConfig
        {
            DisplayMode = UIDisplayMode.Overlay,
            SortingOrderOffset = 100,
            InTransition = TransitionType.Fade,
            OutTransition = TransitionType.Fade
        };
        
        /// <summary>
        /// Create a default modal display config
        /// </summary>
        public static UIDisplayConfig Modal => new UIDisplayConfig
        {
            DisplayMode = UIDisplayMode.Modal,
            BackdropBehavior = ModalBackdropBehavior.Dimmed,
            SortingOrderOffset = 200,
            CloseOnBackdropClick = true,
            CloseOnEscape = true,
            BackdropOpacity = 0.5f,
            InTransition = TransitionType.ScaleUp,
            OutTransition = TransitionType.ScaleDown
        };
        
        /// <summary>
        /// Create a blurred modal display config
        /// </summary>
        public static UIDisplayConfig BlurredModal => new UIDisplayConfig
        {
            DisplayMode = UIDisplayMode.Modal,
            BackdropBehavior = ModalBackdropBehavior.Blurred,
            SortingOrderOffset = 200,
            CloseOnBackdropClick = true,
            CloseOnEscape = true,
            BackdropOpacity = 0.3f,
            BlurRadius = 12f,
            BlurIterations = 3,
            BlurAnimationDuration = 0.4f,
            InTransition = TransitionType.ScaleUp,
            OutTransition = TransitionType.ScaleDown
        };
    }
}
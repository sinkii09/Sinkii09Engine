namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Types of screen transition animations
    /// </summary>
    public enum TransitionType
    {
        /// <summary>
        /// No transition animation
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Fade in/out transition
        /// </summary>
        Fade = 1,
        
        /// <summary>
        /// Slide from left
        /// </summary>
        SlideLeft = 2,
        
        /// <summary>
        /// Slide from right
        /// </summary>
        SlideRight = 3,
        
        /// <summary>
        /// Slide from top
        /// </summary>
        SlideUp = 4,
        
        /// <summary>
        /// Slide from bottom
        /// </summary>
        SlideDown = 5,
        
        /// <summary>
        /// Scale up transition
        /// </summary>
        ScaleUp = 6,
        
        /// <summary>
        /// Scale down transition
        /// </summary>
        ScaleDown = 7,
        
        /// <summary>
        /// Push transition (slides current screen out while new screen slides in)
        /// </summary>
        Push = 8,
        
        /// <summary>
        /// Cover transition (new screen slides over current screen)
        /// </summary>
        Cover = 9
    }
}
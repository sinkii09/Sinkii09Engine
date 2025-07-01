namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines categories for engine services to enable filtering and organization
    /// </summary>
    public enum ServiceCategory
    {
        /// <summary>
        /// Core engine services required for basic functionality
        /// </summary>
        Core = 0,
        
        /// <summary>
        /// Gameplay-related services (actors, world management, etc.)
        /// </summary>
        Gameplay = 1,
        
        /// <summary>
        /// Rendering and graphics services
        /// </summary>
        Rendering = 2,
        
        /// <summary>
        /// Audio and sound services
        /// </summary>
        Audio = 3,
        
        /// <summary>
        /// Input handling services
        /// </summary>
        Input = 4,
        
        /// <summary>
        /// Networking and communication services
        /// </summary>
        Networking = 5,
        
        /// <summary>
        /// UI and interface services
        /// </summary>
        UserInterface = 6,
        
        /// <summary>
        /// Development and debugging tools
        /// </summary>
        Development = 7,
        
        /// <summary>
        /// Test services and mock implementations
        /// Only included in development builds or when explicitly enabled
        /// </summary>
        Test = 8,
        
        /// <summary>
        /// Performance monitoring and profiling services
        /// </summary>
        Performance = 9,
        
        /// <summary>
        /// Configuration and settings services
        /// </summary>
        Configuration = 10
    }
}
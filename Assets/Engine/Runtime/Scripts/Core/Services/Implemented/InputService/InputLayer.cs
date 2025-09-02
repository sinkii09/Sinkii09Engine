namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Semantic layers for input context priority management
    /// Higher values have higher priority in input processing
    /// </summary>
    public enum InputLayer
    {
        /// <summary>
        /// Base gameplay layer - movement, combat, interaction
        /// Lowest priority, other contexts can override
        /// </summary>
        Gameplay = 0,
        
        /// <summary>
        /// Gameplay UI layer - HUD, minimap, health bars
        /// Overlays on gameplay but doesn't block core actions
        /// </summary>
        GameplayUI = 100,
        
        /// <summary>
        /// Menu layer - inventory, character sheet, map
        /// Blocks gameplay but allows system functions
        /// </summary>
        Menus = 200,
        
        /// <summary>
        /// Modal layer - dialogs, confirmations, tutorials
        /// High priority, typically blocks most other input
        /// </summary>
        Modal = 300,
        
        /// <summary>
        /// Critical layer - errors, warnings, important notifications
        /// Very high priority, can interrupt other contexts
        /// </summary>
        Critical = 400,
        
        /// <summary>
        /// System layer - pause menu, settings, dev console
        /// Highest priority, overrides everything else
        /// </summary>
        System = 500
    }
}
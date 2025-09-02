namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines how an input filter processes actions
    /// Determines the behavior of the Actions set in ActionFilter
    /// </summary>
    public enum InputFilterMode
    {
        /// <summary>
        /// Pass through all actions without filtering
        /// Actions set is ignored - everything is allowed
        /// Default mode for contexts that don't need filtering
        /// </summary>
        PassThrough,
        
        /// <summary>
        /// Only allow actions in the Actions set (whitelist)
        /// All other actions are blocked
        /// Useful for tutorials or restricted modes
        /// </summary>
        AllowOnly,
        
        /// <summary>
        /// Block all actions except those in Actions set (blacklist with exceptions)
        /// Most common mode - block everything except specific actions
        /// Useful for cutscenes, dialogs, and modal contexts
        /// </summary>
        BlockExcept
    }
}
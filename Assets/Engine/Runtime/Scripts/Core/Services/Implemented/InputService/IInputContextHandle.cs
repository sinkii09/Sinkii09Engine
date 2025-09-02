using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Handle for managing the lifecycle of an input context
    /// Provides type-safe, RAII-based context management without magic strings
    /// </summary>
    public interface IInputContextHandle : IDisposable
    {
        #region Properties
        
        /// <summary>
        /// Unique identifier for this context
        /// </summary>
        Guid Id { get; }
        
        /// <summary>
        /// Whether this context is currently active in the input system
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// The layer this context operates on
        /// </summary>
        InputLayer Layer { get; }
        
        /// <summary>
        /// Effective priority for this context (Layer + creation timestamp)
        /// Higher values are processed first
        /// </summary>
        long EffectivePriority { get; }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Manually remove this context from the input system
        /// Same as Dispose() but more explicit
        /// </summary>
        void Remove();
        
        #endregion
    }
}
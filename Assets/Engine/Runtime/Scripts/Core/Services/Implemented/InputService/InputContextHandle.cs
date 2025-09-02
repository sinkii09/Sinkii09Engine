using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Implementation of IInputContextHandle providing lifecycle management for input contexts
    /// </summary>
    internal class InputContextHandle : IInputContextHandle
    {
        #region Fields
        
        private readonly Action<Guid> _removeCallback;
        private bool _disposed;
        
        #endregion
        
        #region Properties
        
        public Guid Id { get; }
        public bool IsActive { get; private set; }
        public InputLayer Layer { get; }
        public long EffectivePriority { get; }
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Create a new context handle
        /// </summary>
        /// <param name="id">Context ID</param>
        /// <param name="layer">Context layer</param>
        /// <param name="effectivePriority">Computed priority</param>
        /// <param name="removeCallback">Callback to remove context from InputService</param>
        internal InputContextHandle(Guid id, InputLayer layer, long effectivePriority, Action<Guid> removeCallback)
        {
            Id = id;
            Layer = layer;
            EffectivePriority = effectivePriority;
            IsActive = true;
            _removeCallback = removeCallback ?? throw new ArgumentNullException(nameof(removeCallback));
        }
        
        #endregion
        
        #region IInputContextHandle Implementation
        
        public void Remove()
        {
            if (_disposed || !IsActive)
                return;
                
            try
            {
                _removeCallback(Id);
                IsActive = false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[InputContextHandle] Error removing context {Id}: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            Remove();
            _disposed = true;
            
            // No need for GC.SuppressFinalize since we don't have a finalizer
        }
        
        #endregion
        
        #region Debugging
        
        public override string ToString()
        {
            return $"InputContextHandle[{Id:D}] Layer={Layer} Priority={EffectivePriority} Active={IsActive}";
        }
        
        #endregion
    }
}
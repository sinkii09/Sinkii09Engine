using System;
using UnityEngine;

namespace Sinkii09.Engine.Services.AutoSave
{
    public abstract class AutoSaveTriggerBase : IAutoSaveTrigger
    {
        public abstract string TriggerName { get; }
        
        public bool IsEnabled { get; protected set; }
        
        public event Action<AutoSaveReason> OnTrigger;
        
        protected AutoSaveServiceConfiguration _config;
        
        public virtual void Initialize(AutoSaveServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            OnInitialize();
        }
        
        public virtual void Shutdown()
        {
            OnShutdown();
            IsEnabled = false;
        }
        
        public virtual void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            OnUpdate(deltaTime);
        }
        
        protected virtual void OnInitialize() { }
        
        protected virtual void OnShutdown() { }
        
        protected virtual void OnUpdate(float deltaTime) { }
        
        protected void TriggerAutoSave(AutoSaveReason reason)
        {
            if (IsEnabled)
            {
                Debug.Log($"[{TriggerName}] Triggering auto-save: {reason}");
                OnTrigger?.Invoke(reason);
            }
        }
    }
}
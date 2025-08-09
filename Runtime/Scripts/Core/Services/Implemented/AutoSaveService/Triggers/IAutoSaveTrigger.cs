using System;

namespace Sinkii09.Engine.Services.AutoSave
{
    public interface IAutoSaveTrigger
    {
        string TriggerName { get; }
        
        bool IsEnabled { get; }
        
        event Action<AutoSaveReason> OnTrigger;
        
        void Initialize(AutoSaveServiceConfiguration config);
        
        void Shutdown();
        
        void Update(float deltaTime);
    }
}
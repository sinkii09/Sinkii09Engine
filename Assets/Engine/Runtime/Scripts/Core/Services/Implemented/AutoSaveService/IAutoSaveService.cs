using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sinkii09.Engine.Services.AutoSave
{
    public interface IAutoSaveService : IEngineService
    {
        event Action<AutoSaveEvent> AutoSaveCompleted;

        event Action<AutoSaveEvent> AutoSaveFailed;

        bool IsAutoSaveEnabled { get; }

        bool CanAutoSave { get; }

        DateTime LastAutoSaveTime { get; }

        IReadOnlyList<string> AutoSaveSlots { get; }

        UniTask<SaveResult> TriggerAutoSaveAsync(AutoSaveReason reason = AutoSaveReason.Manual, CancellationToken cancellationToken = default);

        void RegisterProvider(IAutoSaveProvider provider);

        void UnregisterProvider(IAutoSaveProvider provider);

        void RegisterTrigger(IAutoSaveTrigger trigger);

        void UnregisterTrigger(IAutoSaveTrigger trigger);

        void SetAutoSaveEnabled(bool enabled);

        void UpdateTimers(float deltaTime);

        void ClearOldAutoSaves(int maxToKeep = -1);

        UniTask<bool> ValidateAutoSaveConditionsAsync(CancellationToken cancellationToken = default);
    }
}
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Services.AutoSave
{
    public interface IAutoSaveProvider
    {
        string ProviderName { get; }

        UniTask<SaveData> CreateSaveDataAsync(CancellationToken cancellationToken = default);

        bool CanAutoSave();

        AutoSavePriority GetAutoSavePriority();

        bool MeetsConditions(AutoSaveConditions conditions);

        void OnAutoSaveCompleted(AutoSaveEvent autoSaveEvent);

        void OnAutoSaveFailed(AutoSaveEvent autoSaveEvent);
    }
}
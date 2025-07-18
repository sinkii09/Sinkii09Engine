using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Events;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sinkii09.Engine.Services
{
    public interface ISaveLoadService : IEngineService
    {
        UniTask<SaveResult> SaveAsync(string saveId, SaveData data, CancellationToken cancellationToken = default);

        UniTask<LoadResult<T>> LoadAsync<T>(string saveId, CancellationToken cancellationToken = default) where T : SaveData;

        UniTask<bool> ExistsAsync(string saveId, CancellationToken cancellationToken = default);

        UniTask<bool> DeleteAsync(string saveId, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<SaveMetadata>> GetAllSavesAsync(CancellationToken cancellationToken = default);

        UniTask<SaveMetadata> GetSaveMetadataAsync(string saveId, CancellationToken cancellationToken = default);

        UniTask<bool> ValidateSaveAsync(string saveId, CancellationToken cancellationToken = default);

        UniTask<SaveResult> AutoSaveAsync(SaveData data, CancellationToken cancellationToken = default);

        UniTask<LoadResult<T>> LoadLatestAsync<T>(CancellationToken cancellationToken = default) where T : SaveData;

        UniTask<bool> CreateBackupAsync(string saveId, CancellationToken cancellationToken = default);

        UniTask<bool> RestoreBackupAsync(string saveId, string backupId, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<string>> GetBackupsAsync(string saveId, CancellationToken cancellationToken = default);

        event Action<SaveEventArgs> OnSaveStarted;
        event Action<SaveEventArgs> OnSaveCompleted;
        event Action<SaveErrorEventArgs> OnSaveFailed;
        event Action<LoadEventArgs> OnLoadStarted;
        event Action<LoadEventArgs> OnLoadCompleted;
        event Action<LoadErrorEventArgs> OnLoadFailed;

        SaveLoadServiceStatistics GetStatistics();

        void ResetStatistics();

        PerformanceSummary GetPerformanceSummary();

        string GeneratePerformanceReport();
    }
}
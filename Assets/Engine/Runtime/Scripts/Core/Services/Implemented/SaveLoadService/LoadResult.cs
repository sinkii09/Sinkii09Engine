using System;

namespace Sinkii09.Engine.Services
{
    public class LoadResult<T> where T : SaveData
    {
        public bool Success { get; }
        public T Data { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public TimeSpan Duration { get; }
        public string SaveId { get; }
        public long FileSize { get; }

        private LoadResult(bool success, T data, string saveId, string message, Exception exception, TimeSpan duration, long fileSize)
        {
            Success = success;
            Data = data;
            SaveId = saveId;
            Message = message;
            Exception = exception;
            Duration = duration;
            FileSize = fileSize;
        }

        public static LoadResult<T> CreateSuccess(T data, string saveId, TimeSpan duration, long fileSize)
        {
            return new LoadResult<T>(true, data, saveId, "Load completed successfully", null, duration, fileSize);
        }

        public static LoadResult<T> CreateFailure(string saveId, Exception exception, TimeSpan duration)
        {
            return new LoadResult<T>(false, null, saveId, $"Load failed: {exception.Message}", exception, duration, 0);
        }

        public static LoadResult<T> CreateNotFound(string saveId, TimeSpan duration)
        {
            return new LoadResult<T>(false, null, saveId, $"Save file '{saveId}' not found", null, duration, 0);
        }
    }
}
using System;

namespace Sinkii09.Engine.Services
{
    public class SaveResult
    {
        public bool Success { get; }
        public string SaveId { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public TimeSpan Duration { get; }
        public long CompressedSize { get; }
        public long UncompressedSize { get; }
        public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 1.0;
        
        private SaveResult(bool success, string saveId, string message, Exception exception, TimeSpan duration, long compressedSize, long uncompressedSize)
        {
            Success = success;
            SaveId = saveId;
            Message = message;
            Exception = exception;
            Duration = duration;
            CompressedSize = compressedSize;
            UncompressedSize = uncompressedSize;
        }
        
        public static SaveResult CreateSuccess(string saveId, TimeSpan duration, long compressedSize, long uncompressedSize)
        {
            return new SaveResult(true, saveId, "Save completed successfully", null, duration, compressedSize, uncompressedSize);
        }
        
        public static SaveResult CreateFailure(string saveId, Exception exception, TimeSpan duration)
        {
            return new SaveResult(false, saveId, $"Save failed: {exception.Message}", exception, duration, 0, 0);
        }
    }
}
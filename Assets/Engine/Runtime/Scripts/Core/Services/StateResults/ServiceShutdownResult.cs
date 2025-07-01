using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Result of service shutdown operation
    /// </summary>
    public class ServiceShutdownResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan ShutdownTime { get; private set; }
        
        private ServiceShutdownResult(bool isSuccess, string errorMessage = null, Exception exception = null, TimeSpan shutdownTime = default)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            ShutdownTime = shutdownTime;
        }
        
        /// <summary>
        /// Create a successful shutdown result
        /// </summary>
        public static ServiceShutdownResult Success(TimeSpan? shutdownTime = null)
        {
            return new ServiceShutdownResult(true, shutdownTime: shutdownTime ?? TimeSpan.Zero);
        }
        
        /// <summary>
        /// Create a failed shutdown result with error message
        /// </summary>
        public static ServiceShutdownResult Failed(string errorMessage, Exception exception = null)
        {
            return new ServiceShutdownResult(false, errorMessage, exception);
        }
        
        /// <summary>
        /// Create a failed shutdown result with exception
        /// </summary>
        public static ServiceShutdownResult Failed(Exception exception)
        {
            return new ServiceShutdownResult(false, exception?.Message, exception);
        }
        
        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"Shutdown succeeded in {ShutdownTime.TotalMilliseconds:F2}ms";
            }
            else
            {
                return $"Shutdown failed: {ErrorMessage}";
            }
        }
    }
}
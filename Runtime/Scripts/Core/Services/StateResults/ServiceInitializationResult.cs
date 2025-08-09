using System;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Result of service initialization operation
    /// </summary>
    public class ServiceInitializationResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan InitializationTime { get; private set; }
        
        private ServiceInitializationResult(bool isSuccess, string errorMessage = null, Exception exception = null, TimeSpan initializationTime = default)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            InitializationTime = initializationTime;
        }
        
        /// <summary>
        /// Create a successful initialization result
        /// </summary>
        public static ServiceInitializationResult Success(TimeSpan? initializationTime = null)
        {
            return new ServiceInitializationResult(true, initializationTime: initializationTime ?? TimeSpan.Zero);
        }
        
        /// <summary>
        /// Create a failed initialization result with error message
        /// </summary>
        public static ServiceInitializationResult Failed(string errorMessage, Exception exception = null)
        {
            return new ServiceInitializationResult(false, errorMessage, exception);
        }
        
        /// <summary>
        /// Create a failed initialization result with exception
        /// </summary>
        public static ServiceInitializationResult Failed(Exception exception)
        {
            return new ServiceInitializationResult(false, exception?.Message, exception);
        }
        
        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"Initialization succeeded in {InitializationTime.TotalMilliseconds:F2}ms";
            }
            else
            {
                return $"Initialization failed: {ErrorMessage}";
            }
        }
    }
}
using System;

namespace Sinkii09.Engine.Events
{
    public class SaveErrorEventArgs : SaveEventArgs
    {
        public Exception Exception { get; }
        public string ErrorCode { get; }
        
        public SaveErrorEventArgs(string saveId, Exception exception, string errorCode = null) : base(saveId)
        {
            Exception = exception;
            ErrorCode = errorCode ?? GetErrorCodeFromException(exception);
        }
        
        private static string GetErrorCodeFromException(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => "ACCESS_DENIED",
                System.IO.IOException => "IO_ERROR",
                OutOfMemoryException => "OUT_OF_MEMORY",
                NotSupportedException => "NOT_SUPPORTED",
                _ => "UNKNOWN_ERROR"
            };
        }
    }
}
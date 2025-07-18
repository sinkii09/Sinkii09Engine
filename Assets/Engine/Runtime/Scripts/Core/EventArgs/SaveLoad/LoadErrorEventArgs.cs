using System;

namespace Sinkii09.Engine.Events
{
    public class LoadErrorEventArgs : LoadEventArgs
    {
        public Exception Exception { get; }
        public string ErrorCode { get; }

        public LoadErrorEventArgs(string saveId, Exception exception, string errorCode = null) : base(saveId)
        {
            Exception = exception;
            ErrorCode = errorCode ?? GetErrorCodeFromException(exception);
        }

        private static string GetErrorCodeFromException(Exception ex)
        {
            return ex switch
            {
                System.IO.FileNotFoundException => "FILE_NOT_FOUND",
                UnauthorizedAccessException => "ACCESS_DENIED",
                System.IO.IOException => "IO_ERROR",
                InvalidOperationException => "INVALID_OPERATION",
                System.Runtime.Serialization.SerializationException => "SERIALIZATION_ERROR",
                _ => "UNKNOWN_ERROR"
            };
        }
    }
}
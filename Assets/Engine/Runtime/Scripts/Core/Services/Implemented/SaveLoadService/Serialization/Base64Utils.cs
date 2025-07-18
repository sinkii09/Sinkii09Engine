using System;
using System.Text;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Efficient Base64 encoding and decoding utilities with streaming support for large data
    /// </summary>
    public class Base64Utils
    {
        private const int DEFAULT_CHUNK_SIZE = 4096; // 4KB chunks for streaming
        private const int MIN_STREAMING_SIZE = 10240; // 10KB - use streaming for larger data
        
        /// <summary>
        /// Encode binary data to Base64 asynchronously
        /// </summary>
        public async UniTask<Base64Result> EncodeAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                return Base64Result.CreateFailure("Input data is null");
            
            if (data.Length == 0)
                return Base64Result.CreateSuccess(new byte[0], TimeSpan.Zero);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                byte[] encodedData;
                
                if (data.Length >= MIN_STREAMING_SIZE)
                {
                    // Use streaming for large data
                    encodedData = await EncodeStreamingAsync(data, cancellationToken);
                }
                else
                {
                    // Use direct encoding for small data
                    var base64String = Convert.ToBase64String(data);
                    encodedData = Encoding.UTF8.GetBytes(base64String);
                }
                
                stopwatch.Stop();
                
                return Base64Result.CreateSuccess(encodedData, stopwatch.Elapsed, data.Length, encodedData.Length);
            }
            catch (OperationCanceledException)
            {
                return Base64Result.CreateFailure("Encoding was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Base64Result.CreateFailure($"Encoding failed: {ex.Message}", stopwatch.Elapsed, ex);
            }
        }
        
        /// <summary>
        /// Decode Base64 data to binary asynchronously
        /// </summary>
        public async UniTask<Base64Result> DecodeAsync(byte[] encodedData, CancellationToken cancellationToken = default)
        {
            if (encodedData == null)
                return Base64Result.CreateFailure("Input data is null");
            
            if (encodedData.Length == 0)
                return Base64Result.CreateSuccess(new byte[0], TimeSpan.Zero);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                byte[] decodedData;
                
                if (encodedData.Length >= MIN_STREAMING_SIZE)
                {
                    // Use streaming for large data
                    decodedData = await DecodeStreamingAsync(encodedData, cancellationToken);
                }
                else
                {
                    // Use direct decoding for small data
                    var base64String = Encoding.UTF8.GetString(encodedData);
                    decodedData = Convert.FromBase64String(base64String);
                }
                
                stopwatch.Stop();
                
                return Base64Result.CreateSuccess(decodedData, stopwatch.Elapsed, encodedData.Length, decodedData.Length);
            }
            catch (OperationCanceledException)
            {
                return Base64Result.CreateFailure("Decoding was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Base64Result.CreateFailure($"Decoding failed: {ex.Message}", stopwatch.Elapsed, ex);
            }
        }
        
        /// <summary>
        /// Encode string to Base64
        /// </summary>
        public string EncodeString(string text, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            
            encoding = encoding ?? Encoding.UTF8;
            var bytes = encoding.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }
        
        /// <summary>
        /// Decode Base64 string to text
        /// </summary>
        public string DecodeString(string base64Text, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(base64Text))
                return string.Empty;
            
            encoding = encoding ?? Encoding.UTF8;
            var bytes = Convert.FromBase64String(base64Text);
            return encoding.GetString(bytes);
        }
        
        /// <summary>
        /// Validate if string is valid Base64
        /// </summary>
        public bool IsValidBase64(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;
            
            try
            {
                // Check if length is multiple of 4
                if (input.Length % 4 != 0)
                    return false;
                
                // Try to decode
                Convert.FromBase64String(input);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Validate if byte array contains valid Base64 encoded data
        /// </summary>
        public bool IsValidBase64(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;
            
            try
            {
                var base64String = Encoding.UTF8.GetString(data);
                return IsValidBase64(base64String);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Calculate encoded size for given input size
        /// </summary>
        public long CalculateEncodedSize(long inputSize)
        {
            if (inputSize <= 0)
                return 0;
            
            // Base64 encoding increases size by ~33% (4 chars for every 3 bytes)
            // Plus padding if needed
            return ((inputSize + 2) / 3) * 4;
        }
        
        /// <summary>
        /// Calculate decoded size for given Base64 size (approximate)
        /// </summary>
        public long CalculateDecodedSize(long base64Size)
        {
            if (base64Size <= 0)
                return 0;
            
            // Approximate: 3 bytes for every 4 Base64 characters
            return (base64Size / 4) * 3;
        }
        
        private async UniTask<byte[]> EncodeStreamingAsync(byte[] data, CancellationToken cancellationToken)
        {
            var result = new StringBuilder();
            int offset = 0;
            
            while (offset < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Process chunk (must be multiple of 3 for clean Base64 encoding)
                int chunkSize = Math.Min(DEFAULT_CHUNK_SIZE, data.Length - offset);
                
                // Adjust chunk size to be multiple of 3
                chunkSize = (chunkSize / 3) * 3;
                if (chunkSize == 0 && offset < data.Length)
                    chunkSize = data.Length - offset; // Handle remaining bytes
                
                // Extract chunk
                var chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);
                
                // Encode chunk
                var encodedChunk = Convert.ToBase64String(chunk);
                result.Append(encodedChunk);
                
                offset += chunkSize;
                
                // Yield control periodically
                if (offset % (DEFAULT_CHUNK_SIZE * 10) == 0)
                {
                    await UniTask.Yield();
                }
            }
            
            return Encoding.UTF8.GetBytes(result.ToString());
        }
        
        private async UniTask<byte[]> DecodeStreamingAsync(byte[] encodedData, CancellationToken cancellationToken)
        {
            var base64String = Encoding.UTF8.GetString(encodedData);
            var resultList = new System.Collections.Generic.List<byte>();
            
            int offset = 0;
            int chunkSize = (DEFAULT_CHUNK_SIZE / 4) * 4; // Ensure multiple of 4 for Base64
            
            while (offset < base64String.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Get chunk size
                int currentChunkSize = Math.Min(chunkSize, base64String.Length - offset);
                
                // Extract chunk
                string chunk = base64String.Substring(offset, currentChunkSize);
                
                // Decode chunk
                byte[] decodedChunk = Convert.FromBase64String(chunk);
                resultList.AddRange(decodedChunk);
                
                offset += currentChunkSize;
                
                // Yield control periodically
                if (offset % (chunkSize * 10) == 0)
                {
                    await UniTask.Yield();
                }
            }
            
            return resultList.ToArray();
        }
    }
    
    /// <summary>
    /// Result of Base64 encoding/decoding operation
    /// </summary>
    public class Base64Result
    {
        public bool Success { get; }
        public byte[] Data { get; }
        public TimeSpan Duration { get; }
        public long InputSize { get; }
        public long OutputSize { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        
        private Base64Result(bool success, byte[] data, TimeSpan duration, long inputSize, long outputSize, 
            string errorMessage = null, Exception exception = null)
        {
            Success = success;
            Data = data;
            Duration = duration;
            InputSize = inputSize;
            OutputSize = outputSize;
            ErrorMessage = errorMessage;
            Exception = exception;
        }
        
        public static Base64Result CreateSuccess(byte[] data, TimeSpan duration, long inputSize = 0, long outputSize = 0)
        {
            return new Base64Result(true, data, duration, inputSize, outputSize > 0 ? outputSize : data?.Length ?? 0);
        }
        
        public static Base64Result CreateFailure(string errorMessage, TimeSpan duration = default, Exception exception = null)
        {
            return new Base64Result(false, null, duration, 0, 0, errorMessage, exception);
        }
        
        /// <summary>
        /// Size change ratio (output size / input size)
        /// </summary>
        public double SizeRatio => InputSize > 0 ? (double)OutputSize / InputSize : 1.0;
        
        /// <summary>
        /// Size change in bytes
        /// </summary>
        public long SizeChange => OutputSize - InputSize;
        
        /// <summary>
        /// Size change as percentage
        /// </summary>
        public double SizeChangePercentage => InputSize > 0 ? (double)SizeChange / InputSize * 100 : 0;
        
        /// <summary>
        /// Processing speed in bytes per second
        /// </summary>
        public double BytesPerSecond => Duration.TotalSeconds > 0 ? InputSize / Duration.TotalSeconds : 0;
        
        /// <summary>
        /// Processing speed in MB per second
        /// </summary>
        public double MegabytesPerSecond => BytesPerSecond / (1024 * 1024);
    }
}
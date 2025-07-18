using System;
using System.IO;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Compression algorithms supported by the system
    /// </summary>
    public enum CompressionAlgorithm
    {
        None,
        GZip,
        Deflate
    }
    
    /// <summary>
    /// Manages data compression and decompression with configurable algorithms and performance optimization
    /// </summary>
    public class CompressionManager
    {
        private const int DEFAULT_BUFFER_SIZE = 4096;
        private const int MIN_COMPRESSION_SIZE = 100; // Don't compress data smaller than this
        private const int UNKNOWN_COMPRESSION_LEVEL = -1; // Indicates unknown compression level during decompression
        
        /// <summary>
        /// Compress data asynchronously
        /// </summary>
        public async UniTask<CompressionResult> CompressAsync(byte[] data, CompressionLevel level = CompressionLevel.Balanced, CancellationToken cancellationToken = default)
        {
            if (data == null || data.Length == 0)
                return CompressionResult.CreateFailure("Input data is null or empty");
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Skip compression for very small data
                if (data.Length < MIN_COMPRESSION_SIZE)
                {
                    return CompressionResult.CreateSuccess(data, stopwatch.Elapsed, CompressionAlgorithm.None, 0);
                }
                
                byte[] compressedData;
                CompressionAlgorithm algorithm;
                int compressionLevel;
                
                switch (level)
                {
                    case CompressionLevel.None:
                        compressedData = data;
                        algorithm = CompressionAlgorithm.None;
                        compressionLevel = 0;
                        break;
                        
                    case CompressionLevel.Fastest:
                        compressedData = await CompressWithGZipAsync(data, System.IO.Compression.CompressionLevel.Fastest, cancellationToken);
                        algorithm = CompressionAlgorithm.GZip;
                        compressionLevel = 1;
                        break;
                        
                    case CompressionLevel.Balanced:
                        compressedData = await CompressWithGZipAsync(data, System.IO.Compression.CompressionLevel.Optimal, cancellationToken);
                        algorithm = CompressionAlgorithm.GZip;
                        compressionLevel = 2;
                        break;
                        
                    case CompressionLevel.Maximum:
                        // Use Deflate for maximum compression
                        compressedData = await CompressWithDeflateAsync(data, System.IO.Compression.CompressionLevel.Optimal, cancellationToken);
                        algorithm = CompressionAlgorithm.Deflate;
                        compressionLevel = 3;
                        break;
                        
                    default:
                        throw new ArgumentException($"Unsupported compression level: {level}");
                }
                
                stopwatch.Stop();
                
                return CompressionResult.CreateSuccess(
                    compressedData, 
                    stopwatch.Elapsed, 
                    algorithm, 
                    compressionLevel,
                    data.Length,
                    compressedData.Length);
            }
            catch (OperationCanceledException)
            {
                return CompressionResult.CreateFailure("Compression was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CompressionResult.CreateFailure($"Compression failed: {ex.Message}", stopwatch.Elapsed, ex);
            }
        }
        
        /// <summary>
        /// Decompress data asynchronously
        /// </summary>
        public async UniTask<CompressionResult> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
        {
            if (compressedData == null || compressedData.Length == 0)
                return CompressionResult.CreateFailure("Input data is null or empty");
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Try to detect compression format and decompress accordingly
                byte[] decompressedData;
                CompressionAlgorithm algorithm = CompressionAlgorithm.None;
                
                // Check for GZip magic number (1f 8b)
                if (compressedData.Length >= 2 && compressedData[0] == 0x1f && compressedData[1] == 0x8b)
                {
                    decompressedData = await DecompressWithGZipAsync(compressedData, cancellationToken);
                    algorithm = CompressionAlgorithm.GZip;
                }
                // Check for Deflate (no magic number, so try it if GZip fails)
                else
                {
                    try
                    {
                        decompressedData = await DecompressWithDeflateAsync(compressedData, cancellationToken);
                        algorithm = CompressionAlgorithm.Deflate;
                    }
                    catch
                    {
                        // If all compression methods fail, assume data is not compressed
                        decompressedData = compressedData;
                        algorithm = CompressionAlgorithm.None;
                    }
                }
                
                stopwatch.Stop();
                
                return CompressionResult.CreateSuccess(
                    decompressedData, 
                    stopwatch.Elapsed, 
                    algorithm, 
                    UNKNOWN_COMPRESSION_LEVEL, // Unknown compression level for decompression
                    compressedData.Length,
                    decompressedData.Length);
            }
            catch (OperationCanceledException)
            {
                return CompressionResult.CreateFailure("Decompression was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CompressionResult.CreateFailure($"Decompression failed: {ex.Message}", stopwatch.Elapsed, ex);
            }
        }
        
        /// <summary>
        /// Get compression ratio estimate for data
        /// </summary>
        public double EstimateCompressionRatio(byte[] data, CompressionLevel level)
        {
            if (data == null || data.Length == 0)
                return 1.0;
            
            // Estimate based on data characteristics and compression level
            double baseRatio = EstimateBaseCompressionRatio(data);
            
            switch (level)
            {
                case CompressionLevel.None:
                    return 1.0;
                case CompressionLevel.Fastest:
                    return Math.Max(0.7, baseRatio * 1.2); // Less efficient but faster
                case CompressionLevel.Balanced:
                    return baseRatio;
                case CompressionLevel.Maximum:
                    return Math.Max(0.4, baseRatio * 0.8); // More efficient but slower
                default:
                    return baseRatio;
            }
        }
        
        /// <summary>
        /// Check if data would benefit from compression
        /// </summary>
        public bool ShouldCompress(byte[] data, CompressionLevel level)
        {
            if (data == null || data.Length < MIN_COMPRESSION_SIZE)
                return false;
            
            if (level == CompressionLevel.None)
                return false;
            
            // Estimate if compression would save significant space
            var estimatedRatio = EstimateCompressionRatio(data, level);
            return estimatedRatio < 0.9; // Only compress if we save at least 10%
        }
        
        private async UniTask<byte[]> CompressWithGZipAsync(byte[] data, System.IO.Compression.CompressionLevel level, CancellationToken cancellationToken)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, level))
                {
                    await WriteDataAsync(gzipStream, data, cancellationToken);
                }
                
                return outputStream.ToArray();
            }
        }
        
        private async UniTask<byte[]> DecompressWithGZipAsync(byte[] compressedData, CancellationToken cancellationToken)
        {
            using (var inputStream = new MemoryStream(compressedData))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                await CopyStreamAsync(gzipStream, outputStream, cancellationToken);
                return outputStream.ToArray();
            }
        }
        
        private async UniTask<byte[]> CompressWithDeflateAsync(byte[] data, System.IO.Compression.CompressionLevel level, CancellationToken cancellationToken)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(outputStream, level))
                {
                    await WriteDataAsync(deflateStream, data, cancellationToken);
                }
                
                return outputStream.ToArray();
            }
        }
        
        private async UniTask<byte[]> DecompressWithDeflateAsync(byte[] compressedData, CancellationToken cancellationToken)
        {
            using (var inputStream = new MemoryStream(compressedData))
            using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                await CopyStreamAsync(deflateStream, outputStream, cancellationToken);
                return outputStream.ToArray();
            }
        }
        
        private async UniTask WriteDataAsync(Stream stream, byte[] data, CancellationToken cancellationToken)
        {
            int bytesWritten = 0;
            int bufferSize = Math.Min(DEFAULT_BUFFER_SIZE, data.Length);
            
            while (bytesWritten < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int bytesToWrite = Math.Min(bufferSize, data.Length - bytesWritten);
                await stream.WriteAsync(data, bytesWritten, bytesToWrite, cancellationToken);
                bytesWritten += bytesToWrite;
                
                // Yield control periodically for large data
                if (bytesWritten % (bufferSize * 10) == 0)
                {
                    await UniTask.Yield();
                }
            }
        }
        
        private async UniTask CopyStreamAsync(Stream source, Stream destination, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[DEFAULT_BUFFER_SIZE];
            int bytesRead;
            
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                
                // Yield control periodically
                await UniTask.Yield();
            }
        }
        
        private double EstimateBaseCompressionRatio(byte[] data)
        {
            if (data.Length < 100)
                return 0.9; // Small data doesn't compress well
            
            // Analyze data entropy to estimate compressibility
            var entropy = CalculateEntropy(data);
            
            // Higher entropy = less compressible
            // Entropy ranges from 0 (perfectly predictable) to 8 (random)
            if (entropy > 7.5)
                return 0.95; // Very random data
            else if (entropy > 6.0)
                return 0.8;  // Moderately random
            else if (entropy > 4.0)
                return 0.6;  // Some patterns
            else
                return 0.4;  // Highly repetitive
        }
        
        private double CalculateEntropy(byte[] data)
        {
            if (data.Length == 0)
                return 0;
            
            // Count frequency of each byte value
            var frequencies = new int[256];
            foreach (byte b in data)
            {
                frequencies[b]++;
            }
            
            // Calculate entropy
            double entropy = 0;
            int length = data.Length;
            
            for (int i = 0; i < 256; i++)
            {
                if (frequencies[i] > 0)
                {
                    double probability = (double)frequencies[i] / length;
                    entropy -= probability * Math.Log(probability);
                }
            }
            
            return entropy;
        }
    }
    
    /// <summary>
    /// Result of compression or decompression operation
    /// </summary>
    public class CompressionResult
    {
        public bool Success { get; }
        public byte[] Data { get; }
        public TimeSpan Duration { get; }
        public CompressionAlgorithm Algorithm { get; }
        public int CompressionLevel { get; }
        public long OriginalSize { get; }
        public long ProcessedSize { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        
        private CompressionResult(bool success, byte[] data, TimeSpan duration, CompressionAlgorithm algorithm, 
            int compressionLevel, long originalSize, long processedSize, string errorMessage = null, Exception exception = null)
        {
            Success = success;
            Data = data;
            Duration = duration;
            Algorithm = algorithm;
            CompressionLevel = compressionLevel;
            OriginalSize = originalSize;
            ProcessedSize = processedSize;
            ErrorMessage = errorMessage;
            Exception = exception;
        }
        
        public static CompressionResult CreateSuccess(byte[] data, TimeSpan duration, CompressionAlgorithm algorithm, 
            int compressionLevel, long originalSize = 0, long processedSize = 0)
        {
            return new CompressionResult(true, data, duration, algorithm, compressionLevel, 
                originalSize, processedSize > 0 ? processedSize : data?.Length ?? 0);
        }
        
        public static CompressionResult CreateFailure(string errorMessage, TimeSpan duration = default, Exception exception = null)
        {
            return new CompressionResult(false, null, duration, CompressionAlgorithm.None, 0, 0, 0, errorMessage, exception);
        }
        
        /// <summary>
        /// Compression ratio (processed size / original size)
        /// </summary>
        public double CompressionRatio => OriginalSize > 0 ? (double)ProcessedSize / OriginalSize : 1.0;
        
        /// <summary>
        /// Space saved in bytes
        /// </summary>
        public long SpaceSaved => Math.Max(0, OriginalSize - ProcessedSize);
        
        /// <summary>
        /// Space saved as percentage
        /// </summary>
        public double SpaceSavedPercentage => OriginalSize > 0 ? (double)SpaceSaved / OriginalSize * 100 : 0;
        
        /// <summary>
        /// Processing speed in bytes per second
        /// </summary>
        public double BytesPerSecond => Duration.TotalSeconds > 0 ? OriginalSize / Duration.TotalSeconds : 0;
    }
}
using System;
using System.Text;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Unity JsonUtility-based binary serializer with compression and validation
    /// </summary>
    public class GameDataSerializer : BinarySerializerBase
    {
        private const byte MAGIC_BYTE = 0x53; // 'S' for Sinkii09
        private const byte FORMAT_VERSION = 1;
        
        private readonly CompressionManager _compressionManager;
        private readonly Base64Utils _base64Utils;
        
        public GameDataSerializer(CompressionManager compressionManager = null, Base64Utils base64Utils = null) 
            : base(CreateSerializerInfo())
        {
            _compressionManager = compressionManager ?? new CompressionManager();
            _base64Utils = base64Utils ?? new Base64Utils();
        }
        
        public override async UniTask<SerializationResult> SerializeAsync<T>(T data, SerializationContext context = null, CancellationToken cancellationToken = default)
        {
            context = context ?? SerializationContext.Default();
            var stopwatch = Stopwatch.StartNew();
            var metrics = CreateMetrics();
            
            try
            {
                context.UpdateProgress(0, 5, "Starting serialization");
                
                // Step 1: Validation
                if (context.Settings.EnableValidation)
                {
                    context.UpdateProgress(1, 5, "Validating data");
                    var validationStart = Stopwatch.StartNew();
                    
                    if (!CanSerialize(data))
                    {
                        return SerializationResult.CreateFailure(
                            $"Data of type {typeof(T).Name} cannot be serialized", 
                            null, stopwatch.Elapsed);
                    }
                    
                    // Additional validation for SaveData types
                    if (data is SaveData saveData && !saveData.Validate())
                    {
                        return SerializationResult.CreateFailure(
                            "Save data validation failed", 
                            null, stopwatch.Elapsed);
                    }
                    
                    validationStart.Stop();
                    metrics.ValidationTime = validationStart.Elapsed;
                }
                
                // Step 2: JSON Serialization
                context.UpdateProgress(2, 5, "Converting to JSON");
                var jsonStart = Stopwatch.StartNew();
                
                string jsonString;
                try
                {
                    jsonString = JsonUtility.ToJson(data, context.Settings.EnableDebugLogging);
                }
                catch (Exception ex)
                {
                    return SerializationResult.CreateFailure(
                        $"JSON serialization failed: {ex.Message}", ex, stopwatch.Elapsed);
                }
                
                if (string.IsNullOrEmpty(jsonString))
                {
                    return SerializationResult.CreateFailure(
                        "JSON serialization produced empty result", null, stopwatch.Elapsed);
                }
                
                jsonStart.Stop();
                metrics.JsonSerializationTime = jsonStart.Elapsed;
                metrics.JsonSize = Encoding.UTF8.GetByteCount(jsonString);
                
                // Step 3: Binary Conversion
                context.UpdateProgress(3, 5, "Converting to binary");
                var binaryStart = Stopwatch.StartNew();
                
                byte[] binaryData = ConvertToBinary(jsonString, context);
                
                binaryStart.Stop();
                metrics.BinaryConversionTime = binaryStart.Elapsed;
                metrics.BinarySize = binaryData.Length;
                
                // Step 4: Compression
                context.UpdateProgress(4, 5, "Compressing data");
                byte[] finalData = binaryData;
                
                if (context.Settings.EnableCompression)
                {
                    var compressionStart = Stopwatch.StartNew();
                    
                    var compressionResult = await _compressionManager.CompressAsync(
                        binaryData, context.Settings.CompressionLevel, cancellationToken);
                    
                    if (compressionResult.Success)
                    {
                        finalData = compressionResult.Data;
                        metrics.CompressionTime = compressionResult.Duration;
                        metrics.CompressedSize = finalData.Length;
                        metrics.CompressionLevel = (int)context.Settings.CompressionLevel;
                        metrics.CompressionAlgorithm = compressionResult.Algorithm; // Use actual algorithm from result
                    }
                    else
                    {
                        context.AddWarning($"Compression failed: {compressionResult.ErrorMessage}");
                        metrics.CompressedSize = binaryData.Length;
                    }
                    
                    compressionStart.Stop();
                    if (metrics.CompressionTime == default)
                        metrics.CompressionTime = compressionStart.Elapsed;
                }
                else
                {
                    metrics.CompressedSize = binaryData.Length;
                }
                
                // Step 5: Final Encoding
                context.UpdateProgress(5, 5, "Final encoding");
                var encodingStart = Stopwatch.StartNew();
                
                byte[] encodedData = finalData;
                if (context.Settings.EncodingType == "Base64")
                {
                    var base64Result = await _base64Utils.EncodeAsync(finalData, cancellationToken);
                    if (base64Result.Success)
                    {
                        encodedData = base64Result.Data;
                        metrics.EncodingType = "Base64";
                    }
                    else
                    {
                        context.AddWarning($"Base64 encoding failed: {base64Result.ErrorMessage}");
                    }
                }
                
                encodingStart.Stop();
                metrics.EncodingTime = encodingStart.Elapsed;
                metrics.EncodedSize = encodedData.Length;
                
                stopwatch.Stop();
                context.Metrics = metrics;
                
                return SerializationResult.CreateSuccess(
                    encodedData, 
                    metrics.JsonSize, 
                    stopwatch.Elapsed, 
                    metrics);
            }
            catch (OperationCanceledException)
            {
                return SerializationResult.CreateFailure(
                    "Serialization was cancelled", null, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                context.AddError($"Unexpected error during serialization: {ex.Message}");
                return SerializationResult.CreateFailure(
                    $"Serialization failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        public override async UniTask<DeserializationResult<T>> DeserializeAsync<T>(byte[] data, SerializationContext context = null, CancellationToken cancellationToken = default)
        {
            context = context ?? SerializationContext.Default();
            var stopwatch = Stopwatch.StartNew();
            var metrics = CreateMetrics();
            
            try
            {
                context.UpdateProgress(0, 5, "Starting deserialization");
                
                if (data == null || data.Length == 0)
                {
                    return DeserializationResult<T>.CreateFailure(
                        "Input data is null or empty", null, stopwatch.Elapsed);
                }
                
                // Step 1: Validation
                if (context.Settings.EnableValidation)
                {
                    context.UpdateProgress(1, 5, "Validating input");
                    var validationStart = Stopwatch.StartNew();
                    
                    if (!CanDeserialize<T>(data))
                    {
                        return DeserializationResult<T>.CreateFailure(
                            "Input data failed validation", null, stopwatch.Elapsed);
                    }
                    
                    validationStart.Stop();
                    metrics.ValidationTime = validationStart.Elapsed;
                }
                
                byte[] workingData = data;
                
                // Step 2: Decoding
                context.UpdateProgress(2, 5, "Decoding data");
                var decodingStart = Stopwatch.StartNew();
                
                if (context.Settings.EncodingType == "Base64")
                {
                    var decodeResult = await _base64Utils.DecodeAsync(data, cancellationToken);
                    if (decodeResult.Success)
                    {
                        workingData = decodeResult.Data;
                        metrics.EncodingType = "Base64";
                    }
                    else
                    {
                        return DeserializationResult<T>.CreateFailure(
                            $"Base64 decoding failed: {decodeResult.ErrorMessage}", 
                            null, stopwatch.Elapsed);
                    }
                }
                
                decodingStart.Stop();
                metrics.EncodingTime = decodingStart.Elapsed;
                
                // Step 3: Decompression
                context.UpdateProgress(3, 5, "Decompressing data");
                if (context.Settings.EnableCompression)
                {
                    var decompressionStart = Stopwatch.StartNew();
                    
                    var decompressionResult = await _compressionManager.DecompressAsync(
                        workingData, cancellationToken);
                    
                    if (decompressionResult.Success)
                    {
                        workingData = decompressionResult.Data;
                        metrics.CompressionTime = decompressionResult.Duration;
                        metrics.CompressionAlgorithm = decompressionResult.Algorithm; // Use actual algorithm from result
                    }
                    else
                    {
                        return DeserializationResult<T>.CreateFailure(
                            $"Decompression failed: {decompressionResult.ErrorMessage}", 
                            null, stopwatch.Elapsed);
                    }
                    
                    decompressionStart.Stop();
                    if (metrics.CompressionTime == default)
                        metrics.CompressionTime = decompressionStart.Elapsed;
                }
                
                // Step 4: Binary to JSON conversion
                context.UpdateProgress(4, 5, "Converting from binary");
                var binaryStart = Stopwatch.StartNew();
                
                string jsonString = ConvertFromBinary(workingData, context);
                if (string.IsNullOrEmpty(jsonString))
                {
                    return DeserializationResult<T>.CreateFailure(
                        "Binary to JSON conversion failed", null, stopwatch.Elapsed);
                }
                
                binaryStart.Stop();
                metrics.BinaryConversionTime = binaryStart.Elapsed;
                metrics.BinarySize = workingData.Length;
                metrics.JsonSize = Encoding.UTF8.GetByteCount(jsonString);
                
                // Step 5: JSON Deserialization
                context.UpdateProgress(5, 5, "Converting from JSON");
                var jsonStart = Stopwatch.StartNew();
                
                T result;
                try
                {
                    result = JsonUtility.FromJson<T>(jsonString);
                }
                catch (Exception ex)
                {
                    return DeserializationResult<T>.CreateFailure(
                        $"JSON deserialization failed: {ex.Message}", ex, stopwatch.Elapsed);
                }
                
                if (result == null)
                {
                    return DeserializationResult<T>.CreateFailure(
                        "JSON deserialization produced null result", null, stopwatch.Elapsed);
                }
                
                jsonStart.Stop();
                metrics.JsonSerializationTime = jsonStart.Elapsed;
                
                stopwatch.Stop();
                context.Metrics = metrics;
                
                return DeserializationResult<T>.CreateSuccess(
                    result, 
                    metrics.JsonSize, 
                    stopwatch.Elapsed, 
                    metrics);
            }
            catch (OperationCanceledException)
            {
                return DeserializationResult<T>.CreateFailure(
                    "Deserialization was cancelled", null, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                context.AddError($"Unexpected error during deserialization: {ex.Message}");
                return DeserializationResult<T>.CreateFailure(
                    $"Deserialization failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        private byte[] ConvertToBinary(string jsonString, SerializationContext context)
        {
            var jsonBytes = context.Settings.TextEncoding.GetBytes(jsonString);
            
            if (!context.Settings.IncludeMagicBytes && !context.Settings.IncludeMetadata)
            {
                return jsonBytes;
            }
            
            // Calculate header size
            var includeMagicByte = context.Settings.IncludeMagicBytes;
            var metadataBytes = context.Settings.IncludeMetadata ? 
                CreateMetadataBytes(context) : new byte[0];
            
            // Create final array with header + data
            var totalSize = (includeMagicByte ? 1 : 0) + // Magic byte
                           1 + // Format version
                           4 + metadataBytes.Length + // Metadata length + metadata
                           4 + jsonBytes.Length; // Data length + data
            
            var result = new byte[totalSize];
            var offset = 0;
            
            // Write magic byte
            if (includeMagicByte)
            {
                result[offset++] = MAGIC_BYTE;
            }
            
            // Write format version
            result[offset++] = FORMAT_VERSION;
            
            // Write metadata length and metadata
            BitConverter.GetBytes(metadataBytes.Length).CopyTo(result, offset);
            offset += 4;
            if (metadataBytes.Length > 0)
            {
                Array.Copy(metadataBytes, 0, result, offset, metadataBytes.Length);
                offset += metadataBytes.Length;
            }
            
            // Write data length and data
            BitConverter.GetBytes(jsonBytes.Length).CopyTo(result, offset);
            offset += 4;
            Array.Copy(jsonBytes, 0, result, offset, jsonBytes.Length);
            
            return result;
        }
        
        private string ConvertFromBinary(byte[] binaryData, SerializationContext context)
        {
            var offset = 0;
            
            // Validate magic byte if expected
            if (context.Settings.ValidateMagicBytes)
            {
                if (binaryData.Length < 1)
                {
                    throw new InvalidOperationException("Data too short to contain magic byte");
                }
                
                if (binaryData[0] != MAGIC_BYTE)
                {
                    throw new InvalidOperationException($"Invalid magic byte: expected 0x{MAGIC_BYTE:X2}, got 0x{binaryData[0]:X2}");
                }
                offset++;
            }
            
            // Read format version
            if (offset >= binaryData.Length)
            {
                throw new InvalidOperationException("Data too short to contain format version");
            }
            
            var formatVersion = binaryData[offset++];
            if (formatVersion != FORMAT_VERSION)
            {
                throw new InvalidOperationException($"Unsupported format version: {formatVersion}");
            }
            
            // Read metadata length
            if (offset + 4 > binaryData.Length)
            {
                throw new InvalidOperationException("Data too short to contain metadata length");
            }
            
            var metadataLength = BitConverter.ToInt32(binaryData, offset);
            offset += 4;
            
            // Skip metadata for now (could be used for validation)
            if (metadataLength > 0)
            {
                if (offset + metadataLength > binaryData.Length)
                {
                    throw new InvalidOperationException("Data too short to contain metadata");
                }
                offset += metadataLength;
            }
            
            // Read data length
            if (offset + 4 > binaryData.Length)
            {
                throw new InvalidOperationException("Data too short to contain data length");
            }
            
            var dataLength = BitConverter.ToInt32(binaryData, offset);
            offset += 4;
            
            // Read actual JSON data
            if (offset + dataLength > binaryData.Length)
            {
                throw new InvalidOperationException("Data too short to contain actual data");
            }
            
            var jsonBytes = new byte[dataLength];
            Array.Copy(binaryData, offset, jsonBytes, 0, dataLength);
            
            return context.Settings.TextEncoding.GetString(jsonBytes);
        }
        
        private byte[] CreateMetadataBytes(SerializationContext context)
        {
            // Simple metadata: timestamp + source info
            var metadata = new
            {
                timestamp = context.StartTime.ToBinary(),
                sourceId = context.SourceId ?? "",
                sourceType = context.SourceType ?? "",
                settings = new
                {
                    compression = context.Settings.EnableCompression,
                    compressionLevel = (int)context.Settings.CompressionLevel,
                    compressionAlgorithm = context.Settings.CompressionAlgorithm.ToString(),
                    encoding = context.Settings.EncodingType
                }
            };
            
            var metadataJson = JsonUtility.ToJson(metadata);
            return context.Settings.TextEncoding.GetBytes(metadataJson);
        }
        
        protected override bool ValidateMagicBytes(byte[] data)
        {
            if (data.Length < 1)
                return false;
                
            return data[0] == MAGIC_BYTE;
        }
        
        private static SerializerInfo CreateSerializerInfo()
        {
            return new SerializerInfo
            {
                Name = "GameDataSerializer",
                Version = "1.0.0",
                SupportedFormats = new[] { "JSON", "Binary" },
                SupportedCompressionAlgorithms = new[] { CompressionAlgorithm.GZip, CompressionAlgorithm.Deflate, CompressionAlgorithm.None },
                SupportedEncodingTypes = new[] { "Base64", "Binary" },
                SupportsStreaming = false,
                SupportsCompression = true,
                SupportsEncryption = false,
                MaxDataSize = 100 * 1024 * 1024, // 100MB
                Capabilities = new SerializerCapabilities
                {
                    SupportsUnityObjects = true,
                    SupportsCustomTypes = true,
                    SupportsPolymorphism = false,
                    SupportsCircularReferences = false,
                    SupportsLargeObjects = true,
                    SupportsParallelProcessing = false,
                    SupportsVersioning = true,
                    SupportsMagicBytes = true,
                    SupportsChecksums = false,
                    SupportsMetadata = true
                }
            };
        }
    }
}
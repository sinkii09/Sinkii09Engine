using System;
using System.Text;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using System.Diagnostics;
using Newtonsoft.Json;

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
        private IEncryptionProvider _encryptionProvider;
        private KeyDerivationManager _keyDerivationManager;
        private IntegrityValidator _integrityValidator;
        
        public GameDataSerializer(CompressionManager compressionManager = null, Base64Utils base64Utils = null, 
            IEncryptionProvider encryptionProvider = null, KeyDerivationManager keyDerivationManager = null, 
            IntegrityValidator integrityValidator = null) 
            : base(CreateSerializerInfo())
        {
            _compressionManager = compressionManager ?? new CompressionManager();
            _base64Utils = base64Utils ?? new Base64Utils();
            _encryptionProvider = encryptionProvider;
            _keyDerivationManager = keyDerivationManager;
            _integrityValidator = integrityValidator;
        }

        /// <summary>
        /// Initialize security components for encryption support
        /// </summary>
        public override async UniTask<bool> InitializeSecurityAsync(SecurityConfiguration securityConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                if (securityConfig?.EnableEncryption == true)
                {
                    _encryptionProvider = new AESEncryptionProvider();
                    _keyDerivationManager = new KeyDerivationManager();
                    _integrityValidator = new IntegrityValidator();

                    var encryptionInit = _encryptionProvider.InitializeAsync(securityConfig, cancellationToken);
                    var keyDerivationInit = _keyDerivationManager.InitializeAsync(securityConfig, cancellationToken);
                    var integrityInit = _integrityValidator.InitializeAsync(securityConfig, cancellationToken);

                    var results = await UniTask.WhenAll(encryptionInit, keyDerivationInit, integrityInit);
                    
                    return results.Item1 && results.Item2 && results.Item3;
                }
                return true; // No encryption, initialization successful
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize serializer security: {ex.Message}");
                return false;
            }
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
                
                string jsonString = string.Empty;
                try
                {                    
                    // Try Newtonsoft.Json first (supports properties, collections, etc.)
                    try
                    {
                        var jsonSettings = new JsonSerializerSettings
                        {
                            Formatting = context.Settings.EnableDebugLogging ? Formatting.Indented : Formatting.None,
                            NullValueHandling = NullValueHandling.Include,
                            DefaultValueHandling = DefaultValueHandling.Include,
                            DateFormatHandling = DateFormatHandling.IsoDateFormat
                        };

                        jsonString = JsonConvert.SerializeObject(data, jsonSettings);
                        UnityEngine.Debug.Log($"[GameDataSerializer] Newtonsoft.Json result: '{jsonString}' (Length: {jsonString?.Length ?? 0})");
                    }
                    catch (Exception newtonsoftEx)
                    {
                        UnityEngine.Debug.LogWarning($"[GameDataSerializer] Newtonsoft.Json failed: {newtonsoftEx.Message}, falling back to JsonUtility");
                        
                        // Fallback to JsonUtility for Unity-specific types
                        jsonString = JsonUtility.ToJson(data, context.Settings.EnableDebugLogging);
                        UnityEngine.Debug.Log($"[GameDataSerializer] JsonUtility fallback result: '{jsonString}' (Length: {jsonString?.Length ?? 0})");
                    }
                }
                catch (Exception ex)
                {
                    return SerializationResult.CreateFailure(
                        $"JSON serialization failed: {ex.Message}", ex, stopwatch.Elapsed);
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
                context.UpdateProgress(4, 7, "Compressing data");
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

                // Step 5: Encryption
                context.UpdateProgress(5, 7, "Encrypting data");
                if (context.Settings.EnableEncryption && _encryptionProvider != null && _keyDerivationManager != null)
                {
                    var encryptionStart = Stopwatch.StartNew();
                    
                    // Derive encryption key
                    var keyResult = await _keyDerivationManager.DeriveKeyForSaveAsync(
                        context.Settings.EncryptionPassword, context.SourceId, cancellationToken: cancellationToken);
                    
                    if (!keyResult.Success)
                    {
                        return SerializationResult.CreateFailure(
                            $"Key derivation failed: {keyResult.ErrorMessage}", keyResult.Exception, stopwatch.Elapsed);
                    }

                    // Create encryption context
                    var encryptionContext = EncryptionContext.ForSave(context.SourceId, 
                        context.Settings.EncryptionKeyId ?? "default", context.Settings.SecurityConfiguration);

                    // Encrypt data
                    var encryptionResult = await _encryptionProvider.EncryptAsync(
                        finalData, keyResult.DerivedKey, encryptionContext, cancellationToken);

                    // Secure clear the derived key
                    _keyDerivationManager.SecureClear(keyResult.DerivedKey);

                    if (!encryptionResult.Success)
                    {
                        return SerializationResult.CreateFailure(
                            $"Encryption failed: {encryptionResult.ErrorMessage}", encryptionResult.Exception, stopwatch.Elapsed);
                    }

                    // Pack encrypted data with IV and auth tag
                    finalData = encryptionResult.ToPackedFormat();
                    
                    encryptionStart.Stop();
                    metrics.EncryptionTime = encryptionStart.Elapsed;
                    metrics.EncryptedSize = finalData.Length;
                    metrics.EncryptionAlgorithm = _encryptionProvider.AlgorithmName;
                }

                // Step 6: Integrity Validation
                context.UpdateProgress(6, 7, "Validating integrity");
                if (context.Settings.EnableIntegrityValidation && _integrityValidator != null)
                {
                    var integrityStart = Stopwatch.StartNew();
                    
                    var checksumResult = await _integrityValidator.CalculateChecksumAsync(finalData, cancellationToken: cancellationToken);
                    if (checksumResult.Success)
                    {
                        metrics.DataChecksum = checksumResult.Checksum;
                        metrics.ChecksumAlgorithm = checksumResult.Algorithm;
                    }
                    
                    integrityStart.Stop();
                    metrics.IntegrityValidationTime = integrityStart.Elapsed;
                }
                
                // Step 7: Final Encoding
                context.UpdateProgress(7, 7, "Final encoding");
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
                context.UpdateProgress(0, 7, "Starting deserialization");
                
                if (data == null || data.Length == 0)
                {
                    return DeserializationResult<T>.CreateFailure(
                        "Input data is null or empty", null, stopwatch.Elapsed);
                }
                
                // Step 1: Validation
                if (context.Settings.EnableValidation)
                {
                    context.UpdateProgress(1, 7, "Validating input");
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
                context.UpdateProgress(2, 7, "Decoding data");
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

                // Step 3: Integrity Validation
                context.UpdateProgress(3, 7, "Validating integrity");
                if (context.Settings.EnableIntegrityValidation && _integrityValidator != null && !string.IsNullOrEmpty(context.Settings.ExpectedChecksum))
                {
                    var integrityStart = Stopwatch.StartNew();
                    
                    var integrityResult = await _integrityValidator.ValidateIntegrityAsync(
                        workingData, context.Settings.ExpectedChecksum, cancellationToken: cancellationToken);
                    
                    if (!integrityResult.IsValid)
                    {
                        return DeserializationResult<T>.CreateFailure(
                            $"Integrity validation failed: {integrityResult.ErrorMessage}", 
                            integrityResult.Exception, stopwatch.Elapsed);
                    }
                    
                    integrityStart.Stop();
                    metrics.IntegrityValidationTime = integrityStart.Elapsed;
                }

                // Step 4: Decryption
                context.UpdateProgress(4, 7, "Decrypting data");
                if (context.Settings.EnableEncryption && _encryptionProvider != null && _keyDerivationManager != null)
                {
                    var decryptionStart = Stopwatch.StartNew();
                    
                    // Derive decryption key
                    var keyResult = await _keyDerivationManager.DeriveKeyForSaveAsync(
                        context.Settings.EncryptionPassword, context.SourceId, cancellationToken: cancellationToken);
                    
                    if (!keyResult.Success)
                    {
                        return DeserializationResult<T>.CreateFailure(
                            $"Key derivation failed: {keyResult.ErrorMessage}", keyResult.Exception, stopwatch.Elapsed);
                    }

                    // Unpack encrypted data (IV, auth tag, encrypted data)
                    var (iv, authTag, encryptedData) = AESEncryptionExtensions.FromPackedFormat(workingData, AESEncryptionProvider.AES_IV_SIZE_BYTES, AESEncryptionProvider.AES_TAG_SIZE_BYTES);

                    // Create decryption context
                    var encryptionContext = EncryptionContext.ForLoad(context.SourceId, 
                        context.Settings.EncryptionKeyId ?? "default", context.Settings.SecurityConfiguration);

                    // Decrypt data
                    var decryptionResult = await _encryptionProvider.DecryptAsync(
                        encryptedData, keyResult.DerivedKey, iv, authTag, encryptionContext, cancellationToken);

                    // Secure clear the derived key
                    _keyDerivationManager.SecureClear(keyResult.DerivedKey);

                    if (!decryptionResult.Success)
                    {
                        return DeserializationResult<T>.CreateFailure(
                            $"Decryption failed: {decryptionResult.ErrorMessage}", decryptionResult.Exception, stopwatch.Elapsed);
                    }

                    workingData = decryptionResult.DecryptedData;
                    
                    decryptionStart.Stop();
                    metrics.EncryptionTime = decryptionStart.Elapsed;
                    metrics.EncryptionAlgorithm = _encryptionProvider.AlgorithmName;
                }
                
                // Step 5: Decompression
                context.UpdateProgress(5, 7, "Decompressing data");
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
                
                // Step 6: Binary to JSON conversion
                context.UpdateProgress(6, 7, "Converting from binary");
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
                
                // Step 7: JSON Deserialization
                context.UpdateProgress(7, 7, "Converting from JSON");
                var jsonStart = Stopwatch.StartNew();
                
                T result = default;
                try
                {
                    // Try Newtonsoft.Json first
                    try
                    {
                        var jsonSettings = new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Include,
                            DefaultValueHandling = DefaultValueHandling.Include,
                            DateFormatHandling = DateFormatHandling.IsoDateFormat
                        };

                        result = JsonConvert.DeserializeObject<T>(jsonString, jsonSettings);
                        UnityEngine.Debug.Log($"[GameDataSerializer] Newtonsoft.Json deserialization successful");
                    }
                    catch (Exception newtonsoftEx)
                    {
                        UnityEngine.Debug.LogWarning($"[GameDataSerializer] Newtonsoft.Json deserialization failed: {newtonsoftEx.Message}, falling back to JsonUtility");
                        
                        // Fallback to JsonUtility
                        result = JsonUtility.FromJson<T>(jsonString);
                    }
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
                    compressionLevel = context.Settings.CompressionLevel,
                    encoding = context.Settings.EncodingType
                }
            };

            var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.None);
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
                SupportsEncryption = true,
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
                    SupportsChecksums = true,
                    SupportsMetadata = true
                }
            };
        }
    }
}
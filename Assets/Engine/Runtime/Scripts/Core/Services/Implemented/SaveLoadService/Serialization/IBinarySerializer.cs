using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for binary serialization with performance monitoring
    /// </summary>
    public interface IBinarySerializer
    {
        /// <summary>
        /// Serialize object to byte array with performance tracking
        /// </summary>
        UniTask<SerializationResult> SerializeAsync<T>(T data, SerializationContext context = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deserialize byte array to object with performance tracking
        /// </summary>
        UniTask<DeserializationResult<T>> DeserializeAsync<T>(byte[] data, SerializationContext context = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Initialize security components for encryption support
        /// </summary>
        UniTask<bool> InitializeSecurityAsync(SecurityConfiguration securityConfig, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validate that data can be serialized
        /// </summary>
        bool CanSerialize<T>(T data);
        
        /// <summary>
        /// Validate that byte array can be deserialized to type T
        /// </summary>
        bool CanDeserialize<T>(byte[] data);
        
        /// <summary>
        /// Get estimated serialized size without actual serialization
        /// </summary>
        long EstimateSerializedSize<T>(T data);
        
        /// <summary>
        /// Get serializer information and capabilities
        /// </summary>
        SerializerInfo GetInfo();
    }
    
    /// <summary>
    /// Result of serialization operation with performance metrics
    /// </summary>
    public class SerializationResult
    {
        public bool Success { get; }
        public byte[] Data { get; }
        public long OriginalSize { get; }
        public long SerializedSize { get; }
        public TimeSpan Duration { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public SerializationMetrics Metrics { get; }
        
        private SerializationResult(bool success, byte[] data, long originalSize, long serializedSize, 
            TimeSpan duration, string errorMessage = null, Exception exception = null, SerializationMetrics metrics = null)
        {
            Success = success;
            Data = data;
            OriginalSize = originalSize;
            SerializedSize = serializedSize;
            Duration = duration;
            ErrorMessage = errorMessage;
            Exception = exception;
            Metrics = metrics ?? new SerializationMetrics();
        }
        
        public static SerializationResult CreateSuccess(byte[] data, long originalSize, TimeSpan duration, SerializationMetrics metrics = null)
        {
            return new SerializationResult(true, data, originalSize, data?.Length ?? 0, duration, null, null, metrics);
        }
        
        public static SerializationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new SerializationResult(false, null, 0, 0, duration, errorMessage, exception);
        }
        
        /// <summary>
        /// Compression ratio (serialized size / original size)
        /// </summary>
        public double CompressionRatio => OriginalSize > 0 ? (double)SerializedSize / OriginalSize : 1.0;
        
        /// <summary>
        /// Space saved in bytes
        /// </summary>
        public long SpaceSaved => Math.Max(0, OriginalSize - SerializedSize);
        
        /// <summary>
        /// Space saved as percentage
        /// </summary>
        public double SpaceSavedPercentage => OriginalSize > 0 ? (double)SpaceSaved / OriginalSize * 100 : 0;
    }
    
    /// <summary>
    /// Result of deserialization operation with performance metrics
    /// </summary>
    public class DeserializationResult<T>
    {
        public bool Success { get; }
        public T Data { get; }
        public long DeserializedSize { get; }
        public TimeSpan Duration { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }
        public SerializationMetrics Metrics { get; }
        
        private DeserializationResult(bool success, T data, long deserializedSize, TimeSpan duration, 
            string errorMessage = null, Exception exception = null, SerializationMetrics metrics = null)
        {
            Success = success;
            Data = data;
            DeserializedSize = deserializedSize;
            Duration = duration;
            ErrorMessage = errorMessage;
            Exception = exception;
            Metrics = metrics ?? new SerializationMetrics();
        }
        
        public static DeserializationResult<T> CreateSuccess(T data, long deserializedSize, TimeSpan duration, SerializationMetrics metrics = null)
        {
            return new DeserializationResult<T>(true, data, deserializedSize, duration, null, null, metrics);
        }
        
        public static DeserializationResult<T> CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new DeserializationResult<T>(false, default(T), 0, duration, errorMessage, exception);
        }
    }
    
    /// <summary>
    /// Serialization performance metrics
    /// </summary>
    public class SerializationMetrics
    {
        public TimeSpan JsonSerializationTime { get; set; }
        public TimeSpan BinaryConversionTime { get; set; }
        public TimeSpan CompressionTime { get; set; }
        public TimeSpan EncodingTime { get; set; }
        public TimeSpan ValidationTime { get; set; }
        public TimeSpan EncryptionTime { get; set; }
        public TimeSpan IntegrityValidationTime { get; set; }
        
        public long JsonSize { get; set; }
        public long BinarySize { get; set; }
        public long CompressedSize { get; set; }
        public long EncryptedSize { get; set; }
        public long EncodedSize { get; set; }
        
        public int CompressionLevel { get; set; }
        public CompressionAlgorithm CompressionAlgorithm { get; set; }
        public string EncodingType { get; set; }
        public string EncryptionAlgorithm { get; set; }
        public string DataChecksum { get; set; }
        public string ChecksumAlgorithm { get; set; }
        
        /// <summary>
        /// Total processing time
        /// </summary>
        public TimeSpan TotalTime => JsonSerializationTime + BinaryConversionTime + CompressionTime + EncryptionTime + IntegrityValidationTime + EncodingTime + ValidationTime;
        
        /// <summary>
        /// Compression efficiency (compressed size / binary size)
        /// </summary>
        public double CompressionEfficiency => BinarySize > 0 ? (double)CompressedSize / BinarySize : 1.0;
        
        /// <summary>
        /// Overall efficiency (final size / original size)
        /// </summary>
        public double OverallEfficiency => JsonSize > 0 ? (double)EncodedSize / JsonSize : 1.0;
    }
    
    /// <summary>
    /// Information about serializer capabilities
    /// </summary>
    public class SerializerInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string[] SupportedFormats { get; set; }
        public CompressionAlgorithm[] SupportedCompressionAlgorithms { get; set; }
        public string[] SupportedEncodingTypes { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsCompression { get; set; }
        public bool SupportsEncryption { get; set; }
        public long MaxDataSize { get; set; }
        public SerializerCapabilities Capabilities { get; set; }
        
        public SerializerInfo()
        {
            SupportedFormats = new string[0];
            SupportedCompressionAlgorithms = new CompressionAlgorithm[0];
            SupportedEncodingTypes = new string[0];
            Capabilities = new SerializerCapabilities();
        }
    }
    
    /// <summary>
    /// Detailed serializer capabilities
    /// </summary>
    public class SerializerCapabilities
    {
        public bool SupportsUnityObjects { get; set; }
        public bool SupportsCustomTypes { get; set; }
        public bool SupportsPolymorphism { get; set; }
        public bool SupportsCircularReferences { get; set; }
        public bool SupportsLargeObjects { get; set; }
        public bool SupportsParallelProcessing { get; set; }
        public bool SupportsVersioning { get; set; }
        public bool SupportsMagicBytes { get; set; }
        public bool SupportsChecksums { get; set; }
        public bool SupportsMetadata { get; set; }
    }
    
    /// <summary>
    /// Base implementation of IBinarySerializer with common functionality
    /// </summary>
    public abstract class BinarySerializerBase : IBinarySerializer
    {
        protected readonly SerializerInfo _info;
        
        protected BinarySerializerBase(SerializerInfo info)
        {
            _info = info ?? throw new ArgumentNullException(nameof(info));
        }
        
        public abstract UniTask<SerializationResult> SerializeAsync<T>(T data, SerializationContext context = null, CancellationToken cancellationToken = default);
        
        public abstract UniTask<DeserializationResult<T>> DeserializeAsync<T>(byte[] data, SerializationContext context = null, CancellationToken cancellationToken = default);
        
        public virtual async UniTask<bool> InitializeSecurityAsync(SecurityConfiguration securityConfig, CancellationToken cancellationToken = default)
        {
            // Default implementation - no security support
            await UniTask.CompletedTask;
            return true;
        }
        
        public virtual bool CanSerialize<T>(T data)
        {
            if (data == null)
                return false;
                
            var type = typeof(T);
            
            // Check if type is serializable
            if (!type.IsSerializable && !HasSerializableAttribute(type))
                return false;
                
            // Check size limits
            if (_info.MaxDataSize > 0)
            {
                var estimatedSize = EstimateSerializedSize(data);
                if (estimatedSize > _info.MaxDataSize)
                {
                    UnityEngine.Debug.LogWarning($"Data size estimation ({estimatedSize} bytes) exceeds maximum allowed size ({_info.MaxDataSize} bytes) for type {type.Name}");
                    return false;
                }
            }
            
            return true;
        }
        
        public virtual bool CanDeserialize<T>(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            if (data.Length < 4)
                return false;

            var type = typeof(T);
            if (!type.IsSerializable && !HasSerializableAttribute(type))
                return false;
            return true;
        }
        
        public virtual long EstimateSerializedSize<T>(T data)
        {
            if (data == null)
                return 0;
                
            try
            {
                // Use Newtonsoft.Json for accurate size estimation (matches actual serialization)
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat
                };
                
                var json = JsonConvert.SerializeObject(data, jsonSettings);
                var jsonSize = System.Text.Encoding.UTF8.GetByteCount(json);
                
                // Add overhead for binary format (magic bytes, metadata, compression, etc.)
                // Rough estimation: JSON size + 25% overhead for binary format
                var estimatedBinarySize = (long)(jsonSize * 1.25f);
                
                return estimatedBinarySize;
            }
            catch (Exception)
            {
                // Fallback to Unity JsonUtility if Newtonsoft.Json fails
                try
                {
                    var json = UnityEngine.JsonUtility.ToJson(data);
                    return System.Text.Encoding.UTF8.GetByteCount(json);
                }
                catch
                {
                    // If both fail, return a conservative estimate based on object type
                    return EstimateObjectSize(data);
                }
            }
        }
        
        /// <summary>
        /// Fallback size estimation based on object reflection
        /// </summary>
        private long EstimateObjectSize<T>(T data)
        {
            if (data == null) return 0;
            
            var type = data.GetType();
            
            // Primitive types
            if (type.IsPrimitive)
            {
                return System.Runtime.InteropServices.Marshal.SizeOf(type);
            }
            
            // String estimation
            if (data is string str)
            {
                return System.Text.Encoding.UTF8.GetByteCount(str);
            }
            
            // Conservative estimate for complex objects: 1KB base + field count * 50 bytes
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            return 1024 + (fields.Length + properties.Length) * 50;
        }
        
        public SerializerInfo GetInfo()
        {
            return _info;
        }
        
        protected virtual bool HasSerializableAttribute(Type type)
        {
            return type.IsDefined(typeof(SerializableAttribute), false);
        }
        
        protected virtual bool ValidateMagicBytes(byte[] data)
        {
            // Default magic bytes validation - can be overridden
            var magicBytes = System.Text.Encoding.UTF8.GetBytes("S");
            
            if (data.Length < magicBytes.Length)
                return false;
                
            for (int i = 0; i < magicBytes.Length; i++)
            {
                if (data[i] != magicBytes[i])
                    return false;
            }
            
            return true;
        }
        
        protected virtual SerializationMetrics CreateMetrics()
        {
            return new SerializationMetrics
            {
                CompressionAlgorithm = CompressionAlgorithm.None,
                EncodingType = "UTF8",
                CompressionLevel = 0
            };
        }
    }
}
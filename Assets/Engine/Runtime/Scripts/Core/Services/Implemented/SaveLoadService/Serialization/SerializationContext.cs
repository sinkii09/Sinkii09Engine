using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Context for serialization operations containing settings, options, and tracking
    /// </summary>
    public class SerializationContext
    {
        #region Properties
        
        /// <summary>
        /// Serialization settings
        /// </summary>
        public SerializationSettings Settings { get; set; }
        
        /// <summary>
        /// Performance tracking information
        /// </summary>
        public SerializationMetrics Metrics { get; set; }
        
        /// <summary>
        /// Custom options for specific serialization needs
        /// </summary>
        public Dictionary<string, object> Options { get; set; }
        
        /// <summary>
        /// Validation results from pre-serialization checks
        /// </summary>
        public ValidationResult ValidationResult { get; set; }
        
        /// <summary>
        /// Errors and warnings collected during serialization
        /// </summary>
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }
        
        /// <summary>
        /// Source information for debugging and logging
        /// </summary>
        public string SourceId { get; set; }
        public string SourceType { get; set; }
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Progress tracking for large operations
        /// </summary>
        public SerializationProgress Progress { get; set; }
        
        #endregion
        
        #region Constructor
        
        public SerializationContext()
        {
            Settings = new SerializationSettings();
            Metrics = new SerializationMetrics();
            Options = new Dictionary<string, object>();
            Errors = new List<string>();
            Warnings = new List<string>();
            Progress = new SerializationProgress();
            StartTime = DateTime.UtcNow;
        }
        
        public SerializationContext(SerializationSettings settings) : this()
        {
            Settings = settings ?? new SerializationSettings();
        }
        
        #endregion
        
        #region Methods
        
        /// <summary>
        /// Add error to context
        /// </summary>
        public void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
                Errors.Add(error);
        }
        
        /// <summary>
        /// Add warning to context
        /// </summary>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
                Warnings.Add(warning);
        }
        
        /// <summary>
        /// Check if context has errors
        /// </summary>
        public bool HasErrors => Errors.Count > 0;
        
        /// <summary>
        /// Check if context has warnings
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;
        
        /// <summary>
        /// Get or set custom option
        /// </summary>
        public T GetOption<T>(string key, T defaultValue = default(T))
        {
            if (Options.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }
        
        /// <summary>
        /// Set custom option
        /// </summary>
        public void SetOption<T>(string key, T value)
        {
            Options[key] = value;
        }
        
        /// <summary>
        /// Update progress
        /// </summary>
        public void UpdateProgress(int currentStep, int totalSteps, string currentOperation = null)
        {
            Progress.CurrentStep = currentStep;
            Progress.TotalSteps = totalSteps;
            Progress.CurrentOperation = currentOperation ?? Progress.CurrentOperation;
            Progress.Percentage = totalSteps > 0 ? (double)currentStep / totalSteps * 100 : 0;
        }
        
        /// <summary>
        /// Reset context for reuse
        /// </summary>
        public void Reset()
        {
            Metrics = new SerializationMetrics();
            Errors.Clear();
            Warnings.Clear();
            ValidationResult = null;
            Progress.Reset();
            StartTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Create a copy of this context
        /// </summary>
        public SerializationContext Clone()
        {
            var clone = new SerializationContext(Settings)
            {
                SourceId = SourceId,
                SourceType = SourceType,
                StartTime = StartTime
            };
            
            // Copy options
            foreach (var option in Options)
            {
                clone.Options[option.Key] = option.Value;
            }
            
            return clone;
        }
        
        #endregion
        
        #region Static Factory Methods
        
        /// <summary>
        /// Create default context
        /// </summary>
        public static SerializationContext Default()
        {
            return new SerializationContext();
        }
        
        /// <summary>
        /// Create context for save operation
        /// </summary>
        public static SerializationContext ForSave(string saveId, Type dataType, bool enableCompression = true, CompressionLevel compressionLevel = CompressionLevel.Balanced, bool enableValidation = true)
        {
            var context = new SerializationContext
            {
                SourceId = saveId,
                SourceType = dataType?.Name ?? "Unknown",
                Settings = new SerializationSettings
                {
                    EnableCompression = enableCompression,
                    CompressionLevel = compressionLevel,
                    EnableValidation = enableValidation,
                    IncludeMagicBytes = true,
                    IncludeMetadata = true
                }
            };
            
            return context;
        }
        
        /// <summary>
        /// Create context for load operation
        /// </summary>
        public static SerializationContext ForLoad(string saveId, Type dataType, bool strictValidation = true)
        {
            var context = new SerializationContext
            {
                SourceId = saveId,
                SourceType = dataType?.Name ?? "Unknown",
                Settings = new SerializationSettings
                {
                    EnableValidation = strictValidation,
                    ValidateMagicBytes = true,
                    ValidateChecksums = true
                }
            };
            
            return context;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Settings for serialization operations
    /// </summary>
    public class SerializationSettings
    {
        // Compression settings
        public bool EnableCompression { get; set; } = true;
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Balanced;
        public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.GZip;
        
        // Encoding settings
        public string EncodingType { get; set; } = "Base64";
        public System.Text.Encoding TextEncoding { get; set; } = System.Text.Encoding.UTF8;
        
        // Validation settings
        public bool EnableValidation { get; set; } = true;
        public bool ValidateMagicBytes { get; set; } = true;
        public bool ValidateChecksums { get; set; } = true;
        public bool ValidateSchema { get; set; } = true;
        
        // Format settings
        public bool IncludeMagicBytes { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeChecksums { get; set; } = true;
        public bool IncludeTimestamp { get; set; } = true;

        // Security settings
        public bool EnableEncryption { get; set; } = false;
        public bool EnableIntegrityValidation { get; set; } = true;
        public string EncryptionPassword { get; set; }
        public string EncryptionKeyId { get; set; }
        public string ExpectedChecksum { get; set; }
        public SecurityConfiguration SecurityConfiguration { get; set; }
        
        // Performance settings
        public bool EnableStreaming { get; set; } = false;
        public int BufferSize { get; set; } = 4096;
        public int MaxConcurrentOperations { get; set; } = 1;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        
        // Debug settings
        public bool EnableDebugLogging { get; set; } = false;
        public bool CollectDetailedMetrics { get; set; } = false;
        public bool PreserveIntermediateSteps { get; set; } = false;
        
        /// <summary>
        /// Create default settings
        /// </summary>
        public static SerializationSettings Default()
        {
            return new SerializationSettings();
        }
        
        /// <summary>
        /// Create high-performance settings (prioritize speed)
        /// </summary>
        public static SerializationSettings HighPerformance()
        {
            return new SerializationSettings
            {
                EnableCompression = false,
                CompressionLevel = CompressionLevel.Fastest,
                EnableValidation = false,
                ValidateChecksums = false,
                IncludeMetadata = false,
                EnableStreaming = true,
                BufferSize = 8192,
                CollectDetailedMetrics = false
            };
        }
        
        /// <summary>
        /// Create high-compression settings (prioritize size)
        /// </summary>
        public static SerializationSettings HighCompression()
        {
            return new SerializationSettings
            {
                EnableCompression = true,
                CompressionLevel = CompressionLevel.Maximum,
                EnableValidation = true,
                ValidateChecksums = true,
                IncludeMetadata = true,
                EnableStreaming = true,
                BufferSize = 16384,
                CollectDetailedMetrics = true
            };
        }
        
        /// <summary>
        /// Create secure settings (prioritize integrity)
        /// </summary>
        public static SerializationSettings Secure()
        {
            return new SerializationSettings
            {
                EnableValidation = true,
                ValidateMagicBytes = true,
                ValidateChecksums = true,
                ValidateSchema = true,
                IncludeMagicBytes = true,
                IncludeMetadata = true,
                IncludeChecksums = true,
                IncludeTimestamp = true,
                CollectDetailedMetrics = true
            };
        }
    }
    
    /// <summary>
    /// Compression level options
    /// </summary>
    public enum CompressionLevel
    {
        None = 0,
        Fastest = 1,
        Balanced = 2,
        Maximum = 3
    }
    
    /// <summary>
    /// Progress tracking for serialization operations
    /// </summary>
    public class SerializationProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public double Percentage { get; set; }
        public string CurrentOperation { get; set; }
        public DateTime LastUpdate { get; set; }
        
        public SerializationProgress()
        {
            Reset();
        }
        
        public void Reset()
        {
            CurrentStep = 0;
            TotalSteps = 0;
            Percentage = 0;
            CurrentOperation = "";
            LastUpdate = DateTime.UtcNow;
        }
        
        public bool IsComplete => CurrentStep >= TotalSteps && TotalSteps > 0;
        
        public override string ToString()
        {
            if (TotalSteps == 0)
                return "Not started";
                
            return $"{CurrentStep}/{TotalSteps} ({Percentage:F1}%) - {CurrentOperation}";
        }
    }
}
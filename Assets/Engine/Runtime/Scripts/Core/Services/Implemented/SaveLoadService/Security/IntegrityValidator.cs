using System;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;
using System.Text;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Integrity validation result
    /// </summary>
    public class IntegrityValidationResult
    {
        public bool IsValid { get; private set; }
        public bool ChecksumValid { get; private set; }
        public bool StructureValid { get; private set; }
        public bool MagicBytesValid { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan Duration { get; private set; }
        public string ExpectedChecksum { get; private set; }
        public string ActualChecksum { get; private set; }

        private IntegrityValidationResult() { }

        public static IntegrityValidationResult CreateValid(string checksum, TimeSpan duration)
        {
            return new IntegrityValidationResult
            {
                IsValid = true,
                ChecksumValid = true,
                StructureValid = true,
                MagicBytesValid = true,
                ActualChecksum = checksum,
                ExpectedChecksum = checksum,
                Duration = duration
            };
        }

        public static IntegrityValidationResult CreateInvalid(string errorMessage, string expectedChecksum = null, string actualChecksum = null, Exception exception = null, TimeSpan duration = default)
        {
            return new IntegrityValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                ExpectedChecksum = expectedChecksum,
                ActualChecksum = actualChecksum,
                Exception = exception,
                Duration = duration
            };
        }

        public static IntegrityValidationResult CreatePartiallyValid(bool checksumValid, bool structureValid, bool magicBytesValid, string errorMessage, TimeSpan duration)
        {
            return new IntegrityValidationResult
            {
                IsValid = checksumValid && structureValid && magicBytesValid,
                ChecksumValid = checksumValid,
                StructureValid = structureValid,
                MagicBytesValid = magicBytesValid,
                ErrorMessage = errorMessage,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Checksum calculation result
    /// </summary>
    public class ChecksumResult
    {
        public bool Success { get; private set; }
        public string Checksum { get; private set; }
        public string Algorithm { get; private set; }
        public string ErrorMessage { get; private set; }
        public TimeSpan Duration { get; private set; }

        private ChecksumResult() { }

        public static ChecksumResult CreateSuccess(string checksum, string algorithm, TimeSpan duration)
        {
            return new ChecksumResult
            {
                Success = true,
                Checksum = checksum,
                Algorithm = algorithm,
                Duration = duration
            };
        }

        public static ChecksumResult CreateFailure(string errorMessage, TimeSpan duration = default)
        {
            return new ChecksumResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Checksum algorithm options
    /// </summary>
    public enum ChecksumAlgorithm
    {
        SHA256 = 0,
        SHA512 = 1,
        MD5 = 2,        // Not recommended for security but fast
        CRC32 = 3       // Fast but not cryptographically secure
    }

    /// <summary>
    /// Data integrity validator for save files
    /// Provides checksum validation, structure validation, and corruption detection
    /// </summary>
    public class IntegrityValidator : IDisposable
    {
        #region Constants
        private const byte MAGIC_BYTE = 0x53; // Single byte magic identifier for Sinkii09 engine
        private const int MIN_VALID_DATA_SIZE = 16; // Minimum size for valid save data
        #endregion

        #region Private Fields
        private SecurityConfiguration _configuration;
        private IntegrityValidatorStatistics _statistics;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        #endregion

        #region Properties
        public ChecksumAlgorithm DefaultAlgorithm { get; private set; } = ChecksumAlgorithm.SHA256;
        public bool EnableMagicBytesValidation { get; private set; } = true;
        public bool EnableStructureValidation { get; private set; } = true;
        public bool EnableChecksumValidation { get; private set; } = true;
        #endregion

        #region Constructor
        public IntegrityValidator()
        {
            _statistics = new IntegrityValidatorStatistics();
            _isInitialized = false;
            _isDisposed = false;
        }
        #endregion

        #region Initialization
        public async UniTask<bool> InitializeAsync(SecurityConfiguration configuration, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isInitialized)
                    return true;

                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

                // Apply configuration settings
                EnableChecksumValidation = _configuration.EnableChecksumValidation;
                EnableMagicBytesValidation = _configuration.EnableIntegrityValidation;
                EnableStructureValidation = _configuration.EnableCorruptionDetection;

                // Reset statistics
                _statistics.Reset();

                await UniTask.Delay(25, cancellationToken: cancellationToken); // Simulate initialization

                _isInitialized = true;

                if (_configuration.EnableAuditLogging)
                {
                    UnityEngine.Debug.Log($"Integrity validator initialized: Checksum={EnableChecksumValidation}, Structure={EnableStructureValidation}, Magic={EnableMagicBytesValidation}");
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize integrity validator: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Checksum Operations
        /// <summary>
        /// Calculate checksum for data
        /// </summary>
        public async UniTask<ChecksumResult> CalculateChecksumAsync(byte[] data, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateInitialization();

                if (data == null || data.Length == 0)
                    throw new ArgumentException("Data cannot be null or empty", nameof(data));

                string checksum = await UniTask.RunOnThreadPool(() => ComputeChecksum(data, algorithm), cancellationToken: cancellationToken);

                stopwatch.Stop();
                _statistics.RecordChecksum(true, stopwatch.Elapsed);

                return ChecksumResult.CreateSuccess(checksum, algorithm.ToString(), stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordChecksum(false, stopwatch.Elapsed);

                return ChecksumResult.CreateFailure($"Checksum calculation failed: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private string ComputeChecksum(byte[] data, ChecksumAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ChecksumAlgorithm.SHA256:
                    using (var sha256 = SHA256.Create())
                    {
                        var hash = sha256.ComputeHash(data);
                        return Convert.ToBase64String(hash);
                    }

                case ChecksumAlgorithm.SHA512:
                    using (var sha512 = SHA512.Create())
                    {
                        var hash = sha512.ComputeHash(data);
                        return Convert.ToBase64String(hash);
                    }

                case ChecksumAlgorithm.MD5:
                    using (var md5 = MD5.Create())
                    {
                        var hash = md5.ComputeHash(data);
                        return Convert.ToBase64String(hash);
                    }

                case ChecksumAlgorithm.CRC32:
                    // Simple CRC32 implementation
                    uint crc = ComputeCRC32(data);
                    return crc.ToString("X8");

                default:
                    throw new ArgumentException($"Unsupported checksum algorithm: {algorithm}");
            }
        }

        private uint ComputeCRC32(byte[] data)
        {
            const uint polynomial = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
                }
            }

            return ~crc;
        }
        #endregion

        #region Integrity Validation
        /// <summary>
        /// Validate data integrity comprehensively
        /// </summary>
        public async UniTask<IntegrityValidationResult> ValidateIntegrityAsync(byte[] data, string expectedChecksum = null, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateInitialization();

                if (data == null)
                {
                    stopwatch.Stop();
                    _statistics.RecordValidation(false, stopwatch.Elapsed);
                    return IntegrityValidationResult.CreateInvalid("Data cannot be null", duration: stopwatch.Elapsed);
                }

                bool magicBytesValid = true;
                bool structureValid = true;
                bool checksumValid = true;
                string actualChecksum = null;

                // 1. Magic byte validation
                if (EnableMagicBytesValidation)
                {
                    magicBytesValid = await ValidateMagicByteAsync(data, cancellationToken);
                }

                // 2. Structure validation
                if (EnableStructureValidation)
                {
                    structureValid = await ValidateDataStructureAsync(data, cancellationToken);
                }

                // 3. Checksum validation
                if (EnableChecksumValidation && !string.IsNullOrEmpty(expectedChecksum))
                {
                    var checksumResult = await CalculateChecksumAsync(data, algorithm, cancellationToken);
                    if (checksumResult.Success)
                    {
                        actualChecksum = checksumResult.Checksum;
                        checksumValid = string.Equals(expectedChecksum, actualChecksum, StringComparison.Ordinal);
                    }
                    else
                    {
                        checksumValid = false;
                    }
                }

                stopwatch.Stop();

                bool overallValid = magicBytesValid && structureValid && checksumValid;
                _statistics.RecordValidation(overallValid, stopwatch.Elapsed);

                if (overallValid)
                {
                    return IntegrityValidationResult.CreateValid(actualChecksum, stopwatch.Elapsed);
                }
                else
                {
                    var errors = new System.Collections.Generic.List<string>();
                    if (!magicBytesValid) errors.Add("Magic byte invalid");
                    if (!structureValid) errors.Add("Data structure invalid");
                    if (!checksumValid) errors.Add("Checksum validation failed");

                    var errorMessage = string.Join(", ", errors);

                    if (!checksumValid && expectedChecksum != null)
                    {
                        return IntegrityValidationResult.CreateInvalid(errorMessage, expectedChecksum, actualChecksum, duration: stopwatch.Elapsed);
                    }
                    else
                    {
                        return IntegrityValidationResult.CreatePartiallyValid(checksumValid, structureValid, magicBytesValid, errorMessage, stopwatch.Elapsed);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordValidation(false, stopwatch.Elapsed);

                return IntegrityValidationResult.CreateInvalid($"Integrity validation failed: {ex.Message}", exception: ex, duration: stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Validate single magic byte at the beginning of data
        /// </summary>
        private async UniTask<bool> ValidateMagicByteAsync(byte[] data, CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                if (data.Length < 1)
                    return false;

                return data[0] == MAGIC_BYTE;
            }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Validate basic data structure integrity
        /// </summary>
        private async UniTask<bool> ValidateDataStructureAsync(byte[] data, CancellationToken cancellationToken)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                // Basic size validation
                if (data.Length < MIN_VALID_DATA_SIZE)
                    return false;

                // Check for completely zeroed data (potential corruption)
                bool hasNonZeroBytes = false;
                for (int i = 0; i < Math.Min(data.Length, 100); i++) // Check first 100 bytes
                {
                    if (data[i] != 0)
                    {
                        hasNonZeroBytes = true;
                        break;
                    }
                }

                if (!hasNonZeroBytes)
                    return false;

                // Additional structure validation could be added here
                // For example, JSON structure validation, binary header validation, etc.

                return true;
            }, cancellationToken: cancellationToken);
        }
        #endregion

        #region Corruption Detection
        /// <summary>
        /// Detect potential data corruption patterns
        /// </summary>
        public async UniTask<bool> DetectCorruptionAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateInitialization();

                if (data == null || data.Length == 0)
                    return true; // Consider null/empty as corrupted

                return await UniTask.RunOnThreadPool(() =>
                {
                    // Pattern 1: Check for excessive repeated bytes
                    if (HasExcessiveRepeatedBytes(data))
                        return true;

                    // Pattern 2: Check for unrealistic data distribution
                    if (HasUnrealisticDistribution(data))
                        return true;

                    // Pattern 3: Check for null byte sequences
                    if (HasSuspiciousNullSequences(data))
                        return true;

                    return false; // No corruption detected
                }, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                if (_configuration?.EnableAuditLogging == true)
                {
                    UnityEngine.Debug.LogError($"Corruption detection failed: {ex.Message}");
                }
                return true; // Consider errors as potential corruption
            }
        }

        private bool HasExcessiveRepeatedBytes(byte[] data)
        {
            const int maxRepeatedBytes = 50; // Maximum consecutive identical bytes
            
            if (data.Length < maxRepeatedBytes)
                return false;

            int consecutiveCount = 1;
            byte lastByte = data[0];

            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] == lastByte)
                {
                    consecutiveCount++;
                    if (consecutiveCount > maxRepeatedBytes)
                        return true;
                }
                else
                {
                    consecutiveCount = 1;
                    lastByte = data[i];
                }
            }

            return false;
        }

        private bool HasUnrealisticDistribution(byte[] data)
        {
            // Check if too many bytes are the same value
            var byteCounts = new int[256];
            
            foreach (byte b in data)
            {
                byteCounts[b]++;
            }

            // If any single byte value represents more than 80% of the data, it's suspicious
            double threshold = data.Length * 0.8;
            foreach (int count in byteCounts)
            {
                if (count > threshold)
                    return true;
            }

            return false;
        }

        private bool HasSuspiciousNullSequences(byte[] data)
        {
            const int maxNullSequence = 100; // Maximum consecutive null bytes
            
            int nullCount = 0;

            foreach (byte b in data)
            {
                if (b == 0)
                {
                    nullCount++;
                    if (nullCount > maxNullSequence)
                        return true;
                }
                else
                {
                    nullCount = 0;
                }
            }

            return false;
        }
        #endregion

        #region Statistics
        public IntegrityValidatorStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new IntegrityValidatorStatistics
                {
                    TotalValidations = _statistics.TotalValidations,
                    SuccessfulValidations = _statistics.SuccessfulValidations,
                    FailedValidations = _statistics.FailedValidations,
                    TotalChecksums = _statistics.TotalChecksums,
                    SuccessfulChecksums = _statistics.SuccessfulChecksums,
                    FailedChecksums = _statistics.FailedChecksums,
                    TotalValidationTime = _statistics.TotalValidationTime,
                    TotalChecksumTime = _statistics.TotalChecksumTime,
                    LastValidation = _statistics.LastValidation,
                    LastChecksum = _statistics.LastChecksum
                };
            }
        }
        #endregion

        #region Helper Methods
        private void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Integrity validator is not initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(IntegrityValidator));
        }
        #endregion

        #region Disposal
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _isInitialized = false;

                if (_configuration?.EnableAuditLogging == true)
                {
                    UnityEngine.Debug.Log("Integrity validator disposed");
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Statistics for integrity validator performance monitoring
    /// </summary>
    public class IntegrityValidatorStatistics
    {
        public int TotalValidations { get; set; }
        public int SuccessfulValidations { get; set; }
        public int FailedValidations { get; set; }
        public int TotalChecksums { get; set; }
        public int SuccessfulChecksums { get; set; }
        public int FailedChecksums { get; set; }
        public TimeSpan TotalValidationTime { get; set; }
        public TimeSpan TotalChecksumTime { get; set; }
        public DateTime LastValidation { get; set; }
        public DateTime LastChecksum { get; set; }

        public double ValidationSuccessRate => TotalValidations > 0 ? (double)SuccessfulValidations / TotalValidations : 0.0;
        public double ChecksumSuccessRate => TotalChecksums > 0 ? (double)SuccessfulChecksums / TotalChecksums : 0.0;
        public TimeSpan AverageValidationTime => TotalValidations > 0 ? TimeSpan.FromTicks(TotalValidationTime.Ticks / TotalValidations) : TimeSpan.Zero;
        public TimeSpan AverageChecksumTime => TotalChecksums > 0 ? TimeSpan.FromTicks(TotalChecksumTime.Ticks / TotalChecksums) : TimeSpan.Zero;

        public void Reset()
        {
            TotalValidations = 0;
            SuccessfulValidations = 0;
            FailedValidations = 0;
            TotalChecksums = 0;
            SuccessfulChecksums = 0;
            FailedChecksums = 0;
            TotalValidationTime = TimeSpan.Zero;
            TotalChecksumTime = TimeSpan.Zero;
            LastValidation = default;
            LastChecksum = default;
        }

        public void RecordValidation(bool success, TimeSpan duration)
        {
            TotalValidations++;
            if (success)
                SuccessfulValidations++;
            else
                FailedValidations++;
            
            TotalValidationTime = TotalValidationTime.Add(duration);
            LastValidation = DateTime.UtcNow;
        }

        public void RecordChecksum(bool success, TimeSpan duration)
        {
            TotalChecksums++;
            if (success)
                SuccessfulChecksums++;
            else
                FailedChecksums++;
            
            TotalChecksumTime = TotalChecksumTime.Add(duration);
            LastChecksum = DateTime.UtcNow;
        }
    }
}
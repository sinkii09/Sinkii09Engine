using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Encryption result containing encrypted data and metadata
    /// </summary>
    public class EncryptionResult
    {
        public bool Success { get; private set; }
        public byte[] EncryptedData { get; private set; }
        public byte[] InitializationVector { get; private set; }
        public byte[] AuthenticationTag { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan Duration { get; private set; }
        public int OriginalSize { get; private set; }
        public int EncryptedSize { get; private set; }

        private EncryptionResult() { }

        public static EncryptionResult CreateSuccess(byte[] encryptedData, byte[] iv, byte[] authTag, TimeSpan duration, int originalSize)
        {
            return new EncryptionResult
            {
                Success = true,
                EncryptedData = encryptedData,
                InitializationVector = iv,
                AuthenticationTag = authTag,
                Duration = duration,
                OriginalSize = originalSize,
                EncryptedSize = encryptedData.Length
            };
        }

        public static EncryptionResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new EncryptionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Decryption result containing decrypted data and metadata
    /// </summary>
    public class DecryptionResult
    {
        public bool Success { get; private set; }
        public byte[] DecryptedData { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan Duration { get; private set; }
        public int EncryptedSize { get; private set; }
        public int DecryptedSize { get; private set; }
        public bool IntegrityVerified { get; private set; }

        private DecryptionResult() { }

        public static DecryptionResult CreateSuccess(byte[] decryptedData, TimeSpan duration, int encryptedSize, bool integrityVerified = true)
        {
            return new DecryptionResult
            {
                Success = true,
                DecryptedData = decryptedData,
                Duration = duration,
                EncryptedSize = encryptedSize,
                DecryptedSize = decryptedData.Length,
                IntegrityVerified = integrityVerified
            };
        }

        public static DecryptionResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new DecryptionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Encryption context containing configuration and metadata
    /// </summary>
    public class EncryptionContext
    {
        public string SaveId { get; set; }
        public string KeyIdentifier { get; set; }
        public SecurityConfiguration Configuration { get; set; }
        public bool EnableIntegrityCheck { get; set; } = true;
        public bool EnableAuditLogging { get; set; } = true;

        public static EncryptionContext ForSave(string saveId, string keyId, SecurityConfiguration config)
        {
            return new EncryptionContext
            {
                SaveId = saveId,
                KeyIdentifier = keyId,
                Configuration = config,
                EnableIntegrityCheck = config?.EnableIntegrityValidation ?? true,
                EnableAuditLogging = config?.EnableAuditLogging ?? true
            };
        }

        public static EncryptionContext ForLoad(string saveId, string keyId, SecurityConfiguration config)
        {
            return new EncryptionContext
            {
                SaveId = saveId,
                KeyIdentifier = keyId,
                Configuration = config,
                EnableIntegrityCheck = config?.EnableIntegrityValidation ?? true,
                EnableAuditLogging = config?.EnableAuditLogging ?? false // Less verbose for loads
            };
        }
    }

    /// <summary>
    /// Abstract interface for encryption providers
    /// </summary>
    public interface IEncryptionProvider
    {
        /// <summary>
        /// Encryption algorithm identifier
        /// </summary>
        string AlgorithmName { get; }

        /// <summary>
        /// Key size in bits
        /// </summary>
        int KeySizeBits { get; }

        /// <summary>
        /// Initialization vector size in bytes
        /// </summary>
        int IVSizeBytes { get; }

        /// <summary>
        /// Authentication tag size in bytes (for authenticated encryption)
        /// </summary>
        int AuthTagSizeBytes { get; }

        /// <summary>
        /// Whether this provider supports authenticated encryption
        /// </summary>
        bool SupportsAuthentication { get; }

        /// <summary>
        /// Initialize the encryption provider with configuration
        /// </summary>
        UniTask<bool> InitializeAsync(SecurityConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypt data asynchronously
        /// </summary>
        /// <param name="plainData">Data to encrypt</param>
        /// <param name="encryptionKey">Encryption key bytes</param>
        /// <param name="context">Encryption context and metadata</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encryption result with encrypted data and metadata</returns>
        UniTask<EncryptionResult> EncryptAsync(byte[] plainData, byte[] encryptionKey, EncryptionContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypt data asynchronously
        /// </summary>
        /// <param name="encryptedData">Data to decrypt</param>
        /// <param name="encryptionKey">Decryption key bytes</param>
        /// <param name="initializationVector">Initialization vector used during encryption</param>
        /// <param name="authenticationTag">Authentication tag for verification (if applicable)</param>
        /// <param name="context">Decryption context and metadata</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decryption result with decrypted data</returns>
        UniTask<DecryptionResult> DecryptAsync(byte[] encryptedData, byte[] encryptionKey, byte[] initializationVector, byte[] authenticationTag, EncryptionContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a cryptographically secure random initialization vector
        /// </summary>
        byte[] GenerateIV();

        /// <summary>
        /// Validate that the provided key is suitable for this encryption provider
        /// </summary>
        /// <param name="key">Key to validate</param>
        /// <returns>True if key is valid, false otherwise</returns>
        bool ValidateKey(byte[] key);

        /// <summary>
        /// Securely clear sensitive data from memory
        /// </summary>
        /// <param name="sensitiveData">Data to clear</param>
        void SecureClear(byte[] sensitiveData);

        /// <summary>
        /// Get encryption provider statistics and performance metrics
        /// </summary>
        EncryptionProviderStatistics GetStatistics();

        /// <summary>
        /// Cleanup resources
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Statistics for encryption provider performance monitoring
    /// </summary>
    public class EncryptionProviderStatistics
    {
        public int TotalEncryptions { get; set; }
        public int TotalDecryptions { get; set; }
        public int SuccessfulEncryptions { get; set; }
        public int SuccessfulDecryptions { get; set; }
        public int FailedEncryptions { get; set; }
        public int FailedDecryptions { get; set; }
        public TimeSpan TotalEncryptionTime { get; set; }
        public TimeSpan TotalDecryptionTime { get; set; }
        public long TotalBytesEncrypted { get; set; }
        public long TotalBytesDecrypted { get; set; }
        public DateTime LastEncryption { get; set; }
        public DateTime LastDecryption { get; set; }

        public double SuccessRateEncryption => TotalEncryptions > 0 ? (double)SuccessfulEncryptions / TotalEncryptions : 0.0;
        public double SuccessRateDecryption => TotalDecryptions > 0 ? (double)SuccessfulDecryptions / TotalDecryptions : 0.0;
        public TimeSpan AverageEncryptionTime => TotalEncryptions > 0 ? TimeSpan.FromTicks(TotalEncryptionTime.Ticks / TotalEncryptions) : TimeSpan.Zero;
        public TimeSpan AverageDecryptionTime => TotalDecryptions > 0 ? TimeSpan.FromTicks(TotalDecryptionTime.Ticks / TotalDecryptions) : TimeSpan.Zero;

        public void Reset()
        {
            TotalEncryptions = 0;
            TotalDecryptions = 0;
            SuccessfulEncryptions = 0;
            SuccessfulDecryptions = 0;
            FailedEncryptions = 0;
            FailedDecryptions = 0;
            TotalEncryptionTime = TimeSpan.Zero;
            TotalDecryptionTime = TimeSpan.Zero;
            TotalBytesEncrypted = 0;
            TotalBytesDecrypted = 0;
            LastEncryption = default;
            LastDecryption = default;
        }

        public void RecordEncryption(bool success, TimeSpan duration, int bytesProcessed)
        {
            TotalEncryptions++;
            if (success)
            {
                SuccessfulEncryptions++;
                TotalBytesEncrypted += bytesProcessed;
            }
            else
            {
                FailedEncryptions++;
            }
            TotalEncryptionTime = TotalEncryptionTime.Add(duration);
            LastEncryption = DateTime.UtcNow;
        }

        public void RecordDecryption(bool success, TimeSpan duration, int bytesProcessed)
        {
            TotalDecryptions++;
            if (success)
            {
                SuccessfulDecryptions++;
                TotalBytesDecrypted += bytesProcessed;
            }
            else
            {
                FailedDecryptions++;
            }
            TotalDecryptionTime = TotalDecryptionTime.Add(duration);
            LastDecryption = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Encryption algorithm types
    /// </summary>
    public enum EncryptionAlgorithm
    {
        None = 0,
        AES_256_GCM = 1,
        AES_256_CBC = 2,
        ChaCha20_Poly1305 = 3
    }
}
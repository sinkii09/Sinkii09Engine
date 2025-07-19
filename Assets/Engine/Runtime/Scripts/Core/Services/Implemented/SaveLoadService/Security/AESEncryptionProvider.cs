using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// AES-256-GCM encryption provider implementation
    /// Provides authenticated encryption with integrity protection
    /// </summary>
    public class AESEncryptionProvider : IEncryptionProvider, IDisposable
    {
        #region Constants
        public const int AES_IV_SIZE_BYTES = 16; // 128 bits for CBC
        public const int AES_TAG_SIZE_BYTES = 16; // 128 bits authentication tag
        private const int AES_KEY_SIZE_BITS = 256;
        private const int AES_KEY_SIZE_BYTES = AES_KEY_SIZE_BITS / 8; // 32 bytes
        private const string ALGORITHM_NAME = "AES-256-CBC+HMAC";
        #endregion

        #region Private Fields
        private SecurityConfiguration _configuration;
        private EncryptionProviderStatistics _statistics;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        private readonly RNGCryptoServiceProvider _rng;
        #endregion

        #region Properties
        public string AlgorithmName => ALGORITHM_NAME;
        public int KeySizeBits => AES_KEY_SIZE_BITS;
        public int IVSizeBytes => AES_IV_SIZE_BYTES;
        public int AuthTagSizeBytes => AES_TAG_SIZE_BYTES;
        public bool SupportsAuthentication => true;
        #endregion

        #region Constructor
        public AESEncryptionProvider()
        {
            _statistics = new EncryptionProviderStatistics();
            _rng = new RNGCryptoServiceProvider();
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

                // Validate configuration
                if (!ValidateConfiguration())
                    return false;

                // Reset statistics
                _statistics.Reset();

                // Perform actual encryption provider initialization
                await InitializeCryptographicProviders(cancellationToken);

                _isInitialized = true;

                if (_configuration.EnableAuditLogging)
                {
                    Debug.Log($"AES-256-GCM encryption provider initialized successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize AES encryption provider: {ex.Message}");
                return false;
            }
        }

        private bool ValidateConfiguration()
        {
            if (_configuration == null)
                return false;

            // Validate key derivation settings
            if (_configuration.KeyDerivationIterations < 10000)
            {
                Debug.LogWarning("Key derivation iterations below recommended minimum (10,000)");
            }

            return true;
        }

        /// <summary>
        /// Initialize cryptographic providers and validate system capabilities
        /// </summary>
        private async UniTask InitializeCryptographicProviders(CancellationToken cancellationToken)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    // Test AES availability and performance
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = AES_KEY_SIZE_BITS;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        
                        // Verify key size support
                        if (!aes.LegalKeySizes.Any(ks => ks.MinSize <= AES_KEY_SIZE_BITS && ks.MaxSize >= AES_KEY_SIZE_BITS))
                        {
                            throw new NotSupportedException($"AES-{AES_KEY_SIZE_BITS} is not supported on this platform");
                        }

                        // Test HMAC availability
                        using (var hmac = new HMACSHA256())
                        {
                            // Verify HMAC-SHA256 is available
                            if (hmac.HashSize != 256)
                            {
                                throw new NotSupportedException("HMAC-SHA256 is not properly supported on this platform");
                            }
                        }

                        // Validate RNG functionality
                        byte[] testBytes = new byte[32];
                        _rng.GetBytes(testBytes);
                        
                        // Ensure RNG is producing non-zero bytes (basic entropy check)
                        if (testBytes.All(b => b == 0))
                        {
                            throw new InvalidOperationException("RNG is not producing random data");
                        }

                        if (_configuration.EnableAuditLogging)
                        {
                            Debug.Log($"Cryptographic providers validated: AES-{AES_KEY_SIZE_BITS}-CBC, HMAC-SHA256, RNG");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to initialize cryptographic providers: {ex.Message}", ex);
                }
            }, cancellationToken: cancellationToken);
        }
        #endregion

        #region Encryption
        public async UniTask<EncryptionResult> EncryptAsync(byte[] plainData, byte[] encryptionKey, EncryptionContext context, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateInitialization();
                ValidateEncryptionInput(plainData, encryptionKey, context);

                // Generate secure IV
                byte[] iv = GenerateIV();
                byte[] encryptedData = null;
                byte[] authTag = null;

                // Perform encryption on thread pool to avoid blocking main thread
                await UniTask.RunOnThreadPool(() =>
                {
                    // Use AES CBC with HMAC for authenticated encryption (compatible with Unity)
                    using (var aes = Aes.Create())
                    {
                        aes.Key = encryptionKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using (var encryptor = aes.CreateEncryptor())
                        {
                            encryptedData = encryptor.TransformFinalBlock(plainData, 0, plainData.Length);
                        }

                        // Calculate HMAC for authentication
                        using (var hmac = new HMACSHA256(encryptionKey))
                        {
                            var dataToAuth = new byte[iv.Length + encryptedData.Length];
                            Array.Copy(iv, 0, dataToAuth, 0, iv.Length);
                            Array.Copy(encryptedData, 0, dataToAuth, iv.Length, encryptedData.Length);
                            authTag = hmac.ComputeHash(dataToAuth);
                            
                            // Truncate to 16 bytes for compatibility
                            Array.Resize(ref authTag, AES_TAG_SIZE_BYTES);
                        }
                    }
                }, cancellationToken: cancellationToken);

                stopwatch.Stop();

                // Record statistics
                _statistics.RecordEncryption(true, stopwatch.Elapsed, plainData.Length);

                // Audit logging
                if (context.EnableAuditLogging && _configuration.EnableAuditLogging)
                {
                    LogSecurityEvent($"Encryption successful for save '{context.SaveId}', size: {plainData.Length} bytes");
                }

                return EncryptionResult.CreateSuccess(encryptedData, iv, authTag, stopwatch.Elapsed, plainData.Length);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordEncryption(false, stopwatch.Elapsed, 0);

                var errorMessage = $"AES encryption failed for save '{context?.SaveId}': {ex.Message}";

                if (context?.EnableAuditLogging == true && _configuration?.EnableAuditLogging == true)
                {
                    LogSecurityEvent(errorMessage, true);
                }

                return EncryptionResult.CreateFailure(errorMessage, ex, stopwatch.Elapsed);
            }
        }

        private void ValidateEncryptionInput(byte[] plainData, byte[] encryptionKey, EncryptionContext context)
        {
            if (plainData == null || plainData.Length == 0)
                throw new ArgumentException("Plain data cannot be null or empty", nameof(plainData));

            if (!ValidateKey(encryptionKey))
                throw new ArgumentException("Invalid encryption key", nameof(encryptionKey));

            if (context == null)
                throw new ArgumentNullException(nameof(context));
        }
        #endregion

        #region Decryption
        public async UniTask<DecryptionResult> DecryptAsync(byte[] encryptedData, byte[] encryptionKey, byte[] initializationVector, byte[] authenticationTag, EncryptionContext context, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateInitialization();
                ValidateDecryptionInput(encryptedData, encryptionKey, initializationVector, authenticationTag, context);

                byte[] decryptedData = null;

                // Perform decryption on thread pool
                await UniTask.RunOnThreadPool(() =>
                {
                    // Verify HMAC first
                    using (var hmac = new HMACSHA256(encryptionKey))
                    {
                        var dataToAuth = new byte[initializationVector.Length + encryptedData.Length];
                        Array.Copy(initializationVector, 0, dataToAuth, 0, initializationVector.Length);
                        Array.Copy(encryptedData, 0, dataToAuth, initializationVector.Length, encryptedData.Length);
                        var computedTag = hmac.ComputeHash(dataToAuth);
                        
                        // Truncate to 16 bytes for comparison
                        Array.Resize(ref computedTag, AES_TAG_SIZE_BYTES);
                        
                        // Constant-time comparison
                        bool authValid = true;
                        for (int i = 0; i < AES_TAG_SIZE_BYTES; i++)
                        {
                            authValid &= (computedTag[i] == authenticationTag[i]);
                        }
                        
                        if (!authValid)
                            throw new CryptographicException("Authentication tag verification failed");
                    }

                    // Decrypt data using AES CBC
                    using (var aes = Aes.Create())
                    {
                        aes.Key = encryptionKey;
                        aes.IV = initializationVector;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using (var decryptor = aes.CreateDecryptor())
                        {
                            decryptedData = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                        }
                    }
                }, cancellationToken: cancellationToken);

                stopwatch.Stop();

                // Record statistics
                _statistics.RecordDecryption(true, stopwatch.Elapsed, decryptedData.Length);

                // Audit logging (less verbose for loads)
                if (context.EnableAuditLogging && _configuration.EnableAuditLogging && _configuration.VerboseAuditLogging)
                {
                    LogSecurityEvent($"Decryption successful for save '{context.SaveId}', size: {decryptedData.Length} bytes");
                }

                return DecryptionResult.CreateSuccess(decryptedData, stopwatch.Elapsed, encryptedData.Length, true);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                _statistics.RecordDecryption(false, stopwatch.Elapsed, 0);

                var errorMessage = $"AES decryption failed for save '{context?.SaveId}': Authentication failed or data corrupted";

                if (context?.EnableAuditLogging == true && _configuration?.EnableAuditLogging == true)
                {
                    LogSecurityEvent(errorMessage, true);
                }

                return DecryptionResult.CreateFailure(errorMessage, ex, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordDecryption(false, stopwatch.Elapsed, 0);

                var errorMessage = $"AES decryption failed for save '{context?.SaveId}': {ex.Message}";

                if (context?.EnableAuditLogging == true && _configuration?.EnableAuditLogging == true)
                {
                    LogSecurityEvent(errorMessage, true);
                }

                return DecryptionResult.CreateFailure(errorMessage, ex, stopwatch.Elapsed);
            }
        }

        private void ValidateDecryptionInput(byte[] encryptedData, byte[] encryptionKey, byte[] iv, byte[] authTag, EncryptionContext context)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

            if (!ValidateKey(encryptionKey))
                throw new ArgumentException("Invalid encryption key", nameof(encryptionKey));

            if (iv == null || iv.Length != AES_IV_SIZE_BYTES)
                throw new ArgumentException($"Invalid initialization vector size. Expected {AES_IV_SIZE_BYTES} bytes", nameof(iv));

            if (authTag == null || authTag.Length != AES_TAG_SIZE_BYTES)
                throw new ArgumentException($"Invalid authentication tag size. Expected {AES_TAG_SIZE_BYTES} bytes", nameof(authTag));

            if (context == null)
                throw new ArgumentNullException(nameof(context));
        }
        #endregion

        #region Key Management
        public bool ValidateKey(byte[] key)
        {
            return key != null && key.Length == AES_KEY_SIZE_BYTES;
        }

        public byte[] GenerateIV()
        {
            var iv = new byte[AES_IV_SIZE_BYTES];
            _rng.GetBytes(iv);
            return iv;
        }

        public void SecureClear(byte[] sensitiveData)
        {
            if (sensitiveData != null)
            {
                // Overwrite with random data first
                _rng.GetBytes(sensitiveData);
                
                // Then overwrite with zeros
                Array.Clear(sensitiveData, 0, sensitiveData.Length);
            }
        }
        #endregion

        #region Statistics and Monitoring
        public EncryptionProviderStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                // Return a copy to prevent external modification
                return new EncryptionProviderStatistics
                {
                    TotalEncryptions = _statistics.TotalEncryptions,
                    TotalDecryptions = _statistics.TotalDecryptions,
                    SuccessfulEncryptions = _statistics.SuccessfulEncryptions,
                    SuccessfulDecryptions = _statistics.SuccessfulDecryptions,
                    FailedEncryptions = _statistics.FailedEncryptions,
                    FailedDecryptions = _statistics.FailedDecryptions,
                    TotalEncryptionTime = _statistics.TotalEncryptionTime,
                    TotalDecryptionTime = _statistics.TotalDecryptionTime,
                    TotalBytesEncrypted = _statistics.TotalBytesEncrypted,
                    TotalBytesDecrypted = _statistics.TotalBytesDecrypted,
                    LastEncryption = _statistics.LastEncryption,
                    LastDecryption = _statistics.LastDecryption
                };
            }
        }
        #endregion

        #region Security Audit Logging
        private void LogSecurityEvent(string message, bool isError = false)
        {
            var timestamp = DateTime.UtcNow;
            var logMessage = $"[SECURITY] {timestamp:yyyy-MM-dd HH:mm:ss UTC} - {message}";

            if (isError)
            {
                Debug.LogError(logMessage);
            }
            else
            {
                Debug.Log(logMessage);
            }

            // In production, this should be logged to a secure audit log file
            // For now, we use Unity's logging system
        }
        #endregion

        #region Helper Methods
        private void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("AES encryption provider is not initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AESEncryptionProvider));
        }
        #endregion

        #region Disposal
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _rng?.Dispose();
                _isDisposed = true;
                _isInitialized = false;

                if (_configuration?.EnableAuditLogging == true)
                {
                    LogSecurityEvent("AES encryption provider disposed");
                }
            }
        }
        #endregion
    }
}
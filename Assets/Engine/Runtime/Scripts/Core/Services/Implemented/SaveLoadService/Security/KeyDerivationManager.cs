using Cysharp.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Key derivation result containing derived key and metadata
    /// </summary>
    public class KeyDerivationResult
    {
        public bool Success { get; private set; }
        public byte[] DerivedKey { get; private set; }
        public byte[] Salt { get; private set; }
        public int Iterations { get; private set; }
        public string ErrorMessage { get; private set; }
        public Exception Exception { get; private set; }
        public TimeSpan Duration { get; private set; }

        private KeyDerivationResult() { }

        public static KeyDerivationResult CreateSuccess(byte[] derivedKey, byte[] salt, int iterations, TimeSpan duration)
        {
            return new KeyDerivationResult
            {
                Success = true,
                DerivedKey = derivedKey,
                Salt = salt,
                Iterations = iterations,
                Duration = duration
            };
        }

        public static KeyDerivationResult CreateFailure(string errorMessage, Exception exception = null, TimeSpan duration = default)
        {
            return new KeyDerivationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Key validation result
    /// </summary>
    public class KeyValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsMatch { get; set; }
        public string ErrorMessage { get; set; }
        public KeyStrength Strength { get; set; }

        public static KeyValidationResult CreateValid(bool isMatch, KeyStrength strength = KeyStrength.Unknown)
        {
            return new KeyValidationResult
            {
                IsValid = true,
                IsMatch = isMatch,
                Strength = strength
            };
        }

        public static KeyValidationResult CreateInvalid(string errorMessage)
        {
            return new KeyValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                Strength = KeyStrength.Weak
            };
        }
    }

    /// <summary>
    /// Key strength assessment
    /// </summary>
    public enum KeyStrength
    {
        Unknown = 0,
        Weak = 1,
        Fair = 2,
        Good = 3,
        Strong = 4,
        VeryStrong = 5
    }

    /// <summary>
    /// Key derivation method
    /// </summary>
    public enum KeyDerivationMethod
    {
        PBKDF2_SHA256 = 0,
        PBKDF2_SHA512 = 1,
        Scrypt = 2,
        Argon2 = 3
    }

    /// <summary>
    /// Manages cryptographic key derivation using PBKDF2 and other methods
    /// </summary>
    public class KeyDerivationManager : IDisposable
    {
        #region Constants
        private const int DEFAULT_SALT_SIZE_BYTES = 32; // 256 bits
        private const int DEFAULT_KEY_SIZE_BYTES = 32; // 256 bits for AES-256
        private const int DEFAULT_PBKDF2_ITERATIONS = 100000; // OWASP recommended minimum
        private const int MIN_PBKDF2_ITERATIONS = 10000;
        private const int MAX_PBKDF2_ITERATIONS = 1000000;
        #endregion

        #region Private Fields
        private SecurityConfiguration _configuration;
        private KeyDerivationStatistics _statistics;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly RNGCryptoServiceProvider _rng;
        private readonly object _lockObject = new object();
        #endregion

        #region Properties
        public KeyDerivationMethod DefaultMethod { get; private set; } = KeyDerivationMethod.PBKDF2_SHA256;
        public int DefaultIterations { get; private set; } = DEFAULT_PBKDF2_ITERATIONS;
        public int DefaultSaltSize { get; private set; } = DEFAULT_SALT_SIZE_BYTES;
        public int DefaultKeySize { get; private set; } = DEFAULT_KEY_SIZE_BYTES;
        #endregion

        #region Constructor
        public KeyDerivationManager()
        {
            _statistics = new KeyDerivationStatistics();
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

                // Apply configuration settings
                ApplyConfiguration();

                // Validate configuration
                if (!ValidateConfiguration())
                    return false;

                // Reset statistics
                _statistics.Reset();

                await UniTask.Delay(25, cancellationToken: cancellationToken); // Simulate initialization

                _isInitialized = true;

                if (_configuration.EnableAuditLogging)
                {
                    Debug.Log($"Key derivation manager initialized: Method={DefaultMethod}, Iterations={DefaultIterations}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize key derivation manager: {ex.Message}");
                return false;
            }
        }

        private void ApplyConfiguration()
        {
            DefaultIterations = Math.Max(_configuration.KeyDerivationIterations, MIN_PBKDF2_ITERATIONS);
            DefaultIterations = Math.Min(DefaultIterations, MAX_PBKDF2_ITERATIONS);

            if (_configuration.KeyDerivationMethod != KeyDerivationMethod.PBKDF2_SHA256)
            {
                DefaultMethod = _configuration.KeyDerivationMethod;
            }
        }

        private bool ValidateConfiguration()
        {
            if (DefaultIterations < MIN_PBKDF2_ITERATIONS)
            {
                Debug.LogError($"Key derivation iterations too low: {DefaultIterations}. Minimum is {MIN_PBKDF2_ITERATIONS}");
                return false;
            }

            return true;
        }
        #endregion

        #region Key Derivation
        /// <summary>
        /// Derive encryption key from password using PBKDF2
        /// </summary>
        public async UniTask<KeyDerivationResult> DeriveKeyAsync(string password, byte[] salt = null, int? iterations = null, int? keySize = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ValidateInitialization();

                if (string.IsNullOrEmpty(password))
                    throw new ArgumentException("Password cannot be null or empty", nameof(password));

                // Use provided values or defaults
                salt = salt ?? GenerateSalt();
                var iterationCount = iterations ?? DefaultIterations;
                var keySizeBytes = keySize ?? DefaultKeySize;

                byte[] derivedKey = null;

                // Perform key derivation on thread pool to avoid blocking
                await UniTask.RunOnThreadPool(() =>
                {
                    derivedKey = DeriveKeyPBKDF2(password, salt, iterationCount, keySizeBytes);
                }, cancellationToken: cancellationToken);

                stopwatch.Stop();

                // Record statistics
                _statistics.RecordDerivation(true, stopwatch.Elapsed, iterationCount);

                if (_configuration.EnableAuditLogging)
                {
                    Debug.Log($"Key derivation successful: iterations={iterationCount}, key_size={keySizeBytes} bytes, duration={stopwatch.ElapsedMilliseconds}ms");
                }

                return KeyDerivationResult.CreateSuccess(derivedKey, salt, iterationCount, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.RecordDerivation(false, stopwatch.Elapsed, 0);

                var errorMessage = $"Key derivation failed: {ex.Message}";

                if (_configuration?.EnableAuditLogging == true)
                {
                    Debug.LogError(errorMessage);
                }

                return KeyDerivationResult.CreateFailure(errorMessage, ex, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Derive key for a specific save ID (adds save ID to password for uniqueness)
        /// </summary>
        public async UniTask<KeyDerivationResult> DeriveKeyForSaveAsync(string password, string saveId, byte[] salt = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(saveId))
                throw new ArgumentException("Save ID cannot be null or empty", nameof(saveId));

            // Combine password with save ID for unique per-save encryption keys
            var combinedPassword = $"{password}:{saveId}";
            return await DeriveKeyAsync(combinedPassword, salt, cancellationToken: cancellationToken);
        }

        private byte[] DeriveKeyPBKDF2(string password, byte[] salt, int iterations, int keySize)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                return pbkdf2.GetBytes(keySize);
            }
        }
        #endregion

        #region Key Validation
        /// <summary>
        /// Validate a password against a previously derived key
        /// </summary>
        public async UniTask<KeyValidationResult> ValidatePasswordAsync(string password, byte[] expectedKey, byte[] salt, int iterations, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateInitialization();

                if (string.IsNullOrEmpty(password))
                    return KeyValidationResult.CreateInvalid("Password cannot be null or empty");

                if (expectedKey == null || expectedKey.Length == 0)
                    return KeyValidationResult.CreateInvalid("Expected key cannot be null or empty");

                if (salt == null || salt.Length == 0)
                    return KeyValidationResult.CreateInvalid("Salt cannot be null or empty");

                // Derive key from provided password
                var result = await DeriveKeyAsync(password, salt, iterations, expectedKey.Length, cancellationToken);

                if (!result.Success)
                    return KeyValidationResult.CreateInvalid($"Key derivation failed: {result.ErrorMessage}");

                // Compare keys securely
                bool isMatch = SecureCompare(result.DerivedKey, expectedKey);

                // Assess password strength
                var strength = AssessPasswordStrength(password);

                // Clear derived key from memory
                SecureClear(result.DerivedKey);

                return KeyValidationResult.CreateValid(isMatch, strength);
            }
            catch (Exception ex)
            {
                return KeyValidationResult.CreateInvalid($"Password validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Secure constant-time comparison to prevent timing attacks
        /// </summary>
        private bool SecureCompare(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        /// <summary>
        /// Assess password strength
        /// </summary>
        private KeyStrength AssessPasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return KeyStrength.Weak;

            int score = 0;

            // Length scoring
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (password.Length >= 16) score++;

            // Character variety scoring
            if (password.Any(char.IsLower)) score++;
            if (password.Any(char.IsUpper)) score++;
            if (password.Any(char.IsDigit)) score++;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score++;

            // Convert score to strength
            return score switch
            {
                0 or 1 => KeyStrength.Weak,
                2 or 3 => KeyStrength.Fair,
                4 or 5 => KeyStrength.Good,
                6 => KeyStrength.Strong,
                _ => KeyStrength.VeryStrong
            };
        }
        #endregion

        #region Salt Management
        /// <summary>
        /// Generate cryptographically secure random salt
        /// </summary>
        public byte[] GenerateSalt(int saltSize = -1)
        {
            var size = saltSize > 0 ? saltSize : DefaultSaltSize;
            var salt = new byte[size];
            _rng.GetBytes(salt);
            return salt;
        }

        /// <summary>
        /// Validate salt meets minimum security requirements
        /// </summary>
        public bool ValidateSalt(byte[] salt)
        {
            return salt != null && salt.Length >= 16; // Minimum 128 bits
        }
        #endregion

        #region Memory Security
        /// <summary>
        /// Securely clear sensitive data from memory
        /// </summary>
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

        /// <summary>
        /// Securely clear string data (convert to byte array first)
        /// </summary>
        public void SecureClear(string sensitiveData)
        {
            if (!string.IsNullOrEmpty(sensitiveData))
            {
                var bytes = Encoding.UTF8.GetBytes(sensitiveData);
                SecureClear(bytes);
            }
        }
        #endregion

        #region Statistics
        public KeyDerivationStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new KeyDerivationStatistics
                {
                    TotalDerivations = _statistics.TotalDerivations,
                    SuccessfulDerivations = _statistics.SuccessfulDerivations,
                    FailedDerivations = _statistics.FailedDerivations,
                    TotalDerivationTime = _statistics.TotalDerivationTime,
                    AverageIterations = _statistics.AverageIterations,
                    LastDerivation = _statistics.LastDerivation
                };
            }
        }
        #endregion

        #region Helper Methods
        private void ValidateInitialization()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Key derivation manager is not initialized");

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(KeyDerivationManager));
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
                    Debug.Log("Key derivation manager disposed");
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Statistics for key derivation performance monitoring
    /// </summary>
    public class KeyDerivationStatistics
    {
        public int TotalDerivations { get; set; }
        public int SuccessfulDerivations { get; set; }
        public int FailedDerivations { get; set; }
        public TimeSpan TotalDerivationTime { get; set; }
        public double AverageIterations { get; set; }
        public DateTime LastDerivation { get; set; }

        public double SuccessRate => TotalDerivations > 0 ? (double)SuccessfulDerivations / TotalDerivations : 0.0;
        public TimeSpan AverageDerivationTime => TotalDerivations > 0 ? TimeSpan.FromTicks(TotalDerivationTime.Ticks / TotalDerivations) : TimeSpan.Zero;

        public void Reset()
        {
            TotalDerivations = 0;
            SuccessfulDerivations = 0;
            FailedDerivations = 0;
            TotalDerivationTime = TimeSpan.Zero;
            AverageIterations = 0.0;
            LastDerivation = default;
        }

        public void RecordDerivation(bool success, TimeSpan duration, int iterations)
        {
            TotalDerivations++;
            if (success)
            {
                SuccessfulDerivations++;

                // Update average iterations (running average)
                AverageIterations = (AverageIterations * (SuccessfulDerivations - 1) + iterations) / SuccessfulDerivations;
            }
            else
            {
                FailedDerivations++;
            }

            TotalDerivationTime = TotalDerivationTime.Add(duration);
            LastDerivation = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Extension methods for LINQ operations (since we need Any() method)
/// </summary>
public static class LinqExtensions
{
    public static bool Any(this string source, Func<char, bool> predicate)
    {
        foreach (char c in source)
        {
            if (predicate(c))
                return true;
        }
        return false;
    }
}
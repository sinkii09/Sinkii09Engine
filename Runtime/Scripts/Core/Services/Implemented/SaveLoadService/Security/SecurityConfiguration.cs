using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Security configuration ScriptableObject for encryption and security settings
    /// </summary>
    [CreateAssetMenu(fileName = "SecurityConfiguration", menuName = "Engine/Save System/Security Configuration")]
    public class SecurityConfiguration : ScriptableObject
    {
        #region Encryption Settings
        [Header("Encryption Settings")]
        [SerializeField] private bool _enableEncryption = false;
        [SerializeField] private EncryptionAlgorithm _encryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM;
        [SerializeField] private bool _requireEncryptionForAllSaves = false;
        [SerializeField] private bool _enablePerSaveEncryption = true;

        [Header("Key Derivation Settings")]
        [SerializeField] private KeyDerivationMethod _keyDerivationMethod = KeyDerivationMethod.PBKDF2_SHA256;
        [SerializeField] private int _keyDerivationIterations = 100000;
        [SerializeField] private int _saltSize = 32;
        [SerializeField] private int _keySize = 32;

        [Header("Security Policies")]
        [SerializeField] private bool _enablePasswordStrengthValidation = true;
        [SerializeField] private KeyStrength _minimumPasswordStrength = KeyStrength.Good;
        [SerializeField] private bool _requirePasswordForEncryption = true;
        [SerializeField] private int _passwordMinLength = 8;
        [SerializeField] private int _passwordMaxLength = 128;
        #endregion

        #region Integrity and Validation
        [Header("Integrity and Validation")]
        [SerializeField] private bool _enableIntegrityValidation = true;
        [SerializeField] private bool _enableChecksumValidation = true;
        [SerializeField] private bool _enableAntiTamperProtection = true;
        [SerializeField] private bool _enableCorruptionDetection = true;
        #endregion

        #region Audit and Logging
        [Header("Audit and Logging")]
        [SerializeField] private bool _enableAuditLogging = true;
        [SerializeField] private bool _verboseAuditLogging = false;
        [SerializeField] private bool _logSecurityEvents = true;
        [SerializeField] private bool _logPerformanceMetrics = false;
        [SerializeField] private SecurityLogLevel _logLevel = SecurityLogLevel.Warning;
        #endregion

        #region Performance Settings
        [Header("Performance Settings")]
        [SerializeField] private bool _enableAsyncEncryption = true;
        [SerializeField] private bool _enableEncryptionCaching = false;
        [SerializeField] private int _encryptionTimeoutMs = 30000;
        [SerializeField] private int _maxConcurrentEncryptions = 2;
        #endregion

        #region Backup and Recovery
        [Header("Backup and Recovery")]
        [SerializeField] private bool _enableSecureBackups = true;
        [SerializeField] private bool _encryptBackups = true;
        [SerializeField] private int _maxBackupRetention = 5;
        [SerializeField] private bool _enableBackupVerification = true;
        #endregion

        #region Properties - Encryption Settings
        public bool EnableEncryption => _enableEncryption;
        public EncryptionAlgorithm EncryptionAlgorithm => _encryptionAlgorithm;
        public bool RequireEncryptionForAllSaves => _requireEncryptionForAllSaves;
        public bool EnablePerSaveEncryption => _enablePerSaveEncryption;

        public KeyDerivationMethod KeyDerivationMethod => _keyDerivationMethod;
        public int KeyDerivationIterations => _keyDerivationIterations;
        public int SaltSize => _saltSize;
        public int KeySize => _keySize;

        public bool EnablePasswordStrengthValidation => _enablePasswordStrengthValidation;
        public KeyStrength MinimumPasswordStrength => _minimumPasswordStrength;
        public bool RequirePasswordForEncryption => _requirePasswordForEncryption;
        public int PasswordMinLength => _passwordMinLength;
        public int PasswordMaxLength => _passwordMaxLength;
        #endregion

        #region Properties - Integrity and Validation
        public bool EnableIntegrityValidation => _enableIntegrityValidation;
        public bool EnableChecksumValidation => _enableChecksumValidation;
        public bool EnableAntiTamperProtection => _enableAntiTamperProtection;
        public bool EnableCorruptionDetection => _enableCorruptionDetection;
        #endregion

        #region Properties - Audit and Logging
        public bool EnableAuditLogging => _enableAuditLogging;
        public bool VerboseAuditLogging => _verboseAuditLogging;
        public bool LogSecurityEvents => _logSecurityEvents;
        public bool LogPerformanceMetrics => _logPerformanceMetrics;
        public SecurityLogLevel LogLevel => _logLevel;
        #endregion

        #region Properties - Performance Settings
        public bool EnableAsyncEncryption => _enableAsyncEncryption;
        public bool EnableEncryptionCaching => _enableEncryptionCaching;
        public int EncryptionTimeoutMs => _encryptionTimeoutMs;
        public int MaxConcurrentEncryptions => _maxConcurrentEncryptions;
        #endregion

        #region Properties - Backup and Recovery
        public bool EnableSecureBackups => _enableSecureBackups;
        public bool EncryptBackups => _encryptBackups;
        public int MaxBackupRetention => _maxBackupRetention;
        public bool EnableBackupVerification => _enableBackupVerification;
        #endregion

        #region Validation
        /// <summary>
        /// Validate security configuration settings
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            // Validate encryption settings
            if (_enableEncryption)
            {
                ValidateEncryptionSettings(errors);
                ValidateKeyDerivationSettings(errors);
                ValidatePasswordSettings(errors);
            }

            // Validate performance settings
            ValidatePerformanceSettings(errors);

            // Validate backup settings
            ValidateBackupSettings(errors);

            return errors.Count == 0;
        }

        private void ValidateEncryptionSettings(List<string> errors)
        {
            if (_encryptionAlgorithm == EncryptionAlgorithm.None)
            {
                errors.Add("Encryption algorithm cannot be None when encryption is enabled");
            }

            if (_requireEncryptionForAllSaves && !_enableEncryption)
            {
                errors.Add("Cannot require encryption for all saves when encryption is disabled");
            }
        }

        private void ValidateKeyDerivationSettings(List<string> errors)
        {
            if (_keyDerivationIterations < 10000)
            {
                errors.Add($"Key derivation iterations too low: {_keyDerivationIterations}. Minimum recommended: 10,000");
            }

            if (_keyDerivationIterations > 1000000)
            {
                errors.Add($"Key derivation iterations too high: {_keyDerivationIterations}. Maximum recommended: 1,000,000");
            }

            if (_saltSize < 16)
            {
                errors.Add($"Salt size too small: {_saltSize} bytes. Minimum recommended: 16 bytes");
            }

            if (_keySize < 16)
            {
                errors.Add($"Key size too small: {_keySize} bytes. Minimum recommended: 16 bytes");
            }

            if (_keySize > 64)
            {
                errors.Add($"Key size too large: {_keySize} bytes. Maximum recommended: 64 bytes");
            }
        }

        private void ValidatePasswordSettings(List<string> errors)
        {
            if (_requirePasswordForEncryption)
            {
                if (_passwordMinLength < 4)
                {
                    errors.Add($"Password minimum length too short: {_passwordMinLength}. Minimum recommended: 4");
                }

                if (_passwordMaxLength < _passwordMinLength)
                {
                    errors.Add("Password maximum length cannot be less than minimum length");
                }

                if (_passwordMaxLength > 1024)
                {
                    errors.Add($"Password maximum length too large: {_passwordMaxLength}. Maximum recommended: 1024");
                }
            }
        }

        private void ValidatePerformanceSettings(List<string> errors)
        {
            if (_encryptionTimeoutMs < 1000)
            {
                errors.Add($"Encryption timeout too short: {_encryptionTimeoutMs}ms. Minimum recommended: 1000ms");
            }

            if (_encryptionTimeoutMs > 300000) // 5 minutes
            {
                errors.Add($"Encryption timeout too long: {_encryptionTimeoutMs}ms. Maximum recommended: 300,000ms");
            }

            if (_maxConcurrentEncryptions < 1)
            {
                errors.Add("Maximum concurrent encryptions must be at least 1");
            }

            if (_maxConcurrentEncryptions > 10)
            {
                errors.Add($"Maximum concurrent encryptions too high: {_maxConcurrentEncryptions}. Maximum recommended: 10");
            }
        }

        private void ValidateBackupSettings(List<string> errors)
        {
            if (_enableSecureBackups)
            {
                if (_maxBackupRetention < 1)
                {
                    errors.Add("Maximum backup retention must be at least 1");
                }

                if (_maxBackupRetention > 100)
                {
                    errors.Add($"Maximum backup retention too high: {_maxBackupRetention}. Maximum recommended: 100");
                }
            }
        }
        #endregion

        #region Security Profiles
        /// <summary>
        /// Apply a predefined security profile
        /// </summary>
        public void ApplySecurityProfile(SecurityProfile profile)
        {
            switch (profile)
            {
                case SecurityProfile.Disabled:
                    ApplyDisabledProfile();
                    break;
                case SecurityProfile.Basic:
                    ApplyBasicProfile();
                    break;
                case SecurityProfile.Standard:
                    ApplyStandardProfile();
                    break;
                case SecurityProfile.High:
                    ApplyHighProfile();
                    break;
                case SecurityProfile.Maximum:
                    ApplyMaximumProfile();
                    break;
            }
        }

        private void ApplyDisabledProfile()
        {
            _enableEncryption = false;
            _enableIntegrityValidation = false;
            _enableAuditLogging = false;
            _enableSecureBackups = false;
        }

        private void ApplyBasicProfile()
        {
            _enableEncryption = true;
            _encryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM;
            _keyDerivationIterations = 50000;
            _enableIntegrityValidation = true;
            _enableAuditLogging = false;
            _minimumPasswordStrength = KeyStrength.Fair;
        }

        private void ApplyStandardProfile()
        {
            _enableEncryption = true;
            _encryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM;
            _keyDerivationIterations = 100000;
            _enableIntegrityValidation = true;
            _enableAuditLogging = true;
            _verboseAuditLogging = false;
            _minimumPasswordStrength = KeyStrength.Good;
            _enableSecureBackups = true;
        }

        private void ApplyHighProfile()
        {
            _enableEncryption = true;
            _encryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM;
            _keyDerivationIterations = 200000;
            _enableIntegrityValidation = true;
            _enableChecksumValidation = true;
            _enableAntiTamperProtection = true;
            _enableAuditLogging = true;
            _verboseAuditLogging = true;
            _minimumPasswordStrength = KeyStrength.Strong;
            _enableSecureBackups = true;
            _encryptBackups = true;
        }

        private void ApplyMaximumProfile()
        {
            _enableEncryption = true;
            _encryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM;
            _requireEncryptionForAllSaves = true;
            _keyDerivationIterations = 500000;
            _enableIntegrityValidation = true;
            _enableChecksumValidation = true;
            _enableAntiTamperProtection = true;
            _enableCorruptionDetection = true;
            _enableAuditLogging = true;
            _verboseAuditLogging = true;
            _logSecurityEvents = true;
            _logPerformanceMetrics = true;
            _minimumPasswordStrength = KeyStrength.VeryStrong;
            _passwordMinLength = 12;
            _enableSecureBackups = true;
            _encryptBackups = true;
            _enableBackupVerification = true;
        }
        #endregion

        #region Unity Editor Validation
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Clamp values to valid ranges
            _keyDerivationIterations = Mathf.Clamp(_keyDerivationIterations, 10000, 1000000);
            _saltSize = Mathf.Clamp(_saltSize, 16, 64);
            _keySize = Mathf.Clamp(_keySize, 16, 64);
            _passwordMinLength = Mathf.Clamp(_passwordMinLength, 4, 128);
            _passwordMaxLength = Mathf.Clamp(_passwordMaxLength, _passwordMinLength, 1024);
            _encryptionTimeoutMs = Mathf.Clamp(_encryptionTimeoutMs, 1000, 300000);
            _maxConcurrentEncryptions = Mathf.Clamp(_maxConcurrentEncryptions, 1, 10);
            _maxBackupRetention = Mathf.Clamp(_maxBackupRetention, 1, 100);

            // Auto-dependencies
            if (_requireEncryptionForAllSaves)
            {
                _enableEncryption = true;
            }

            if (_encryptBackups)
            {
                _enableSecureBackups = true;
            }
        }
#endif
        #endregion

        #region Default Configurations
        /// <summary>
        /// Create a default security configuration asset
        /// </summary>
        public static SecurityConfiguration CreateDefault()
        {
            var config = CreateInstance<SecurityConfiguration>();
            config.ApplySecurityProfile(SecurityProfile.Standard);
            return config;
        }
        #endregion
    }

    /// <summary>
    /// Predefined security profiles for common use cases
    /// </summary>
    public enum SecurityProfile
    {
        Disabled = 0,    // No security features
        Basic = 1,       // Basic encryption with minimal overhead
        Standard = 2,    // Recommended settings for most games
        High = 3,        // Enhanced security for sensitive data
        Maximum = 4      // Maximum security with all features enabled
    }

    /// <summary>
    /// Security audit log levels
    /// </summary>
    public enum SecurityLogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
        Verbose = 5
    }
}
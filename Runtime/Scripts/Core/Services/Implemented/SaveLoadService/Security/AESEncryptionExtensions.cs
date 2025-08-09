using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Extension methods for working with AES-GCM encryption results
    /// </summary>
    public static class AESEncryptionExtensions
    {
        /// <summary>
        /// Combine encrypted data, IV, and authentication tag into a single byte array
        /// Format: [IV][AuthTag][EncryptedData]
        /// </summary>
        public static byte[] ToPackedFormat(this EncryptionResult result)
        {
            if (!result.Success)
                throw new InvalidOperationException("Cannot pack failed encryption result");

            var packed = new byte[result.InitializationVector.Length + result.AuthenticationTag.Length + result.EncryptedData.Length];
            var offset = 0;

            // Copy IV
            Array.Copy(result.InitializationVector, 0, packed, offset, result.InitializationVector.Length);
            offset += result.InitializationVector.Length;

            // Copy Auth Tag
            Array.Copy(result.AuthenticationTag, 0, packed, offset, result.AuthenticationTag.Length);
            offset += result.AuthenticationTag.Length;

            // Copy Encrypted Data
            Array.Copy(result.EncryptedData, 0, packed, offset, result.EncryptedData.Length);

            return packed;
        }

        /// <summary>
        /// Extract IV, authentication tag, and encrypted data from packed format
        /// </summary>
        public static (byte[] iv, byte[] authTag, byte[] encryptedData) FromPackedFormat(byte[] packedData, int ivSize, int tagSize)
        {
            if (packedData == null || packedData.Length < ivSize + tagSize)
                throw new ArgumentException("Invalid packed data format");
            
            var iv = new byte[ivSize];
            var authTag = new byte[tagSize];
            var encryptedData = new byte[packedData.Length - ivSize - tagSize];

            var offset = 0;

            // Extract IV
            Array.Copy(packedData, offset, iv, 0, ivSize);
            offset += ivSize;

            // Extract Auth Tag
            Array.Copy(packedData, offset, authTag, 0, tagSize);
            offset += tagSize;

            // Extract Encrypted Data
            Array.Copy(packedData, offset, encryptedData, 0, encryptedData.Length);

            return (iv, authTag, encryptedData);
        }
    }
}
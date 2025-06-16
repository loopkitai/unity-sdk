using System;
using UnityEngine;

namespace LoopKit.Utils
{
    /// <summary>
    /// ID generator for creating unique identifiers
    /// Used for session IDs, anonymous IDs, and event IDs
    /// </summary>
    public class IdGenerator
    {
        /// <summary>
        /// Generate a new UUID/GUID
        /// </summary>
        /// <returns>New unique identifier</returns>
        public string GenerateId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Generate anonymous ID based on device
        /// Creates a persistent anonymous ID that survives app restarts
        /// </summary>
        /// <returns>Anonymous identifier</returns>
        public string GenerateAnonymousId()
        {
            // Use Unity's SystemInfo.deviceUniqueIdentifier as base for anonymous ID
            // This ensures the same anonymous ID across sessions for the same device
            var deviceId = UnityEngine.SystemInfo.deviceUniqueIdentifier;

            if (
                string.IsNullOrEmpty(deviceId)
                || deviceId == UnityEngine.SystemInfo.unsupportedIdentifier
            )
            {
                // Fallback to a random GUID if device ID is not available
                return GenerateId();
            }

            // Create a deterministic ID based on device identifier
            // This ensures the anonymous ID is consistent across app sessions
            return $"anon_{HashString(deviceId)}";
        }

        /// <summary>
        /// Generate session ID with timestamp
        /// </summary>
        /// <returns>Session identifier</returns>
        public string GenerateSessionId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return $"sess_{timestamp}_{GenerateShortId()}";
        }

        /// <summary>
        /// Generate a short random ID
        /// </summary>
        /// <returns>Short identifier</returns>
        public string GenerateShortId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Create a hash from a string for consistent ID generation
        /// </summary>
        /// <param name="input">Input string to hash</param>
        /// <returns>Hashed string</returns>
        private string HashString(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return BitConverter
                    .ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant()
                    .Substring(0, 16);
            }
        }
    }
}

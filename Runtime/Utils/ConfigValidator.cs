using System;
using UnityEngine;

namespace LoopKit.Utils
{
    /// <summary>
    /// Configuration validator for LoopKit SDK
    /// Ensures configuration parameters are valid and within acceptable ranges
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// Validate LoopKit configuration
        /// </summary>
        /// <param name="config">Configuration to validate</param>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
        public static void Validate(LoopKitConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "Configuration cannot be null");
            }

            ValidateApiKey(config.apiKey);
            ValidateBaseURL(config.baseURL);
            ValidateBatchSettings(config);
            ValidateNetworkSettings(config);
            ValidateSessionSettings(config);
        }

        /// <summary>
        /// Validate API key
        /// </summary>
        private static void ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException(
                    "API key is required and cannot be empty",
                    nameof(apiKey)
                );
            }

            if (apiKey.Length < 8)
            {
                throw new ArgumentException(
                    "API key must be at least 8 characters long",
                    nameof(apiKey)
                );
            }

            // Check for common placeholder values
            var lowerApiKey = apiKey.ToLowerInvariant();
            if (
                lowerApiKey.Contains("your-api-key")
                || lowerApiKey.Contains("placeholder")
                || lowerApiKey.Contains("test")
                || lowerApiKey == "api-key"
            )
            {
                Debug.LogWarning(
                    "[LoopKit] API key appears to be a placeholder. Make sure to use your actual API key."
                );
            }
        }

        /// <summary>
        /// Validate base URL
        /// </summary>
        private static void ValidateBaseURL(string baseURL)
        {
            if (string.IsNullOrEmpty(baseURL))
            {
                throw new ArgumentException("Base URL cannot be empty", nameof(baseURL));
            }

            if (!Uri.TryCreate(baseURL, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException(
                    "Base URL must be a valid absolute URL",
                    nameof(baseURL)
                );
            }

            if (uri.Scheme != "https" && uri.Scheme != "http")
            {
                throw new ArgumentException(
                    "Base URL must use HTTP or HTTPS protocol",
                    nameof(baseURL)
                );
            }

            // Warn about non-HTTPS URLs in production
            if (uri.Scheme != "https" && !Application.isEditor)
            {
                Debug.LogWarning(
                    "[LoopKit] Using non-HTTPS URL in production is not recommended for security reasons."
                );
            }
        }

        /// <summary>
        /// Validate batch settings
        /// </summary>
        private static void ValidateBatchSettings(LoopKitConfig config)
        {
            if (config.batchSize <= 0)
            {
                throw new ArgumentException(
                    "Batch size must be greater than 0",
                    nameof(config.batchSize)
                );
            }

            if (config.batchSize > 1000)
            {
                Debug.LogWarning(
                    "[LoopKit] Large batch size may impact performance. Consider using a smaller value."
                );
            }

            if (config.flushInterval <= 0)
            {
                throw new ArgumentException(
                    "Flush interval must be greater than 0",
                    nameof(config.flushInterval)
                );
            }

            if (config.maxQueueSize <= 0)
            {
                throw new ArgumentException(
                    "Max queue size must be greater than 0",
                    nameof(config.maxQueueSize)
                );
            }

            if (config.maxQueueSize < config.batchSize)
            {
                throw new ArgumentException(
                    "Max queue size must be greater than or equal to batch size",
                    nameof(config.maxQueueSize)
                );
            }
        }

        /// <summary>
        /// Validate network settings
        /// </summary>
        private static void ValidateNetworkSettings(LoopKitConfig config)
        {
            if (config.requestTimeout <= 0)
            {
                throw new ArgumentException(
                    "Request timeout must be greater than 0",
                    nameof(config.requestTimeout)
                );
            }

            if (config.requestTimeout > 60000) // 60 seconds
            {
                Debug.LogWarning(
                    "[LoopKit] Very long request timeout may cause poor user experience."
                );
            }

            if (config.maxRetries < 0)
            {
                throw new ArgumentException(
                    "Max retries cannot be negative",
                    nameof(config.maxRetries)
                );
            }

            if (config.maxRetries > 10)
            {
                Debug.LogWarning(
                    "[LoopKit] High retry count may cause delays. Consider using a lower value."
                );
            }
        }

        /// <summary>
        /// Validate session settings
        /// </summary>
        private static void ValidateSessionSettings(LoopKitConfig config)
        {
            if (config.sessionTimeout <= 0)
            {
                throw new ArgumentException(
                    "Session timeout must be greater than 0",
                    nameof(config.sessionTimeout)
                );
            }

            if (config.sessionTimeout < 60) // Less than 1 minute
            {
                Debug.LogWarning(
                    "[LoopKit] Very short session timeout may result in frequent session changes."
                );
            }

            if (config.sessionTimeout > 7200) // More than 2 hours
            {
                Debug.LogWarning(
                    "[LoopKit] Very long session timeout may reduce session analytics accuracy."
                );
            }
        }

        /// <summary>
        /// Sanitize configuration values
        /// Ensures configuration values are within safe ranges
        /// </summary>
        /// <param name="config">Configuration to sanitize</param>
        /// <returns>Sanitized configuration</returns>
        public static LoopKitConfig Sanitize(LoopKitConfig config)
        {
            if (config == null)
                return new LoopKitConfig();

            // Clamp values to safe ranges
            config.batchSize = Mathf.Clamp(config.batchSize, 1, 1000);
            config.flushInterval = Mathf.Clamp(config.flushInterval, 1f, 300f);
            config.maxQueueSize = Mathf.Max(config.maxQueueSize, config.batchSize);
            config.requestTimeout = Mathf.Clamp(config.requestTimeout, 1000, 60000);
            config.maxRetries = Mathf.Clamp(config.maxRetries, 0, 10);
            config.sessionTimeout = Mathf.Clamp(config.sessionTimeout, 60f, 7200f);

            return config;
        }
    }
}

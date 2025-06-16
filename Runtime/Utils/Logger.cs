using System;
using UnityEngine;

namespace LoopKit.Utils
{
    /// <summary>
    /// Logger implementation for LoopKit SDK
    /// Provides structured logging with configurable levels
    /// </summary>
    public class Logger : ILogger
    {
        private LoopKitConfig _config;
        private const string LOG_PREFIX = "[LoopKit]";

        public Logger(LoopKitConfig config)
        {
            _config = config ?? new LoopKitConfig();
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public void Error(string message, object context = null)
        {
            if (!ShouldLog(LogLevel.Error))
                return;

            var logMessage = FormatMessage("ERROR", message, context);
            UnityEngine.Debug.LogError(logMessage);
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public void Warn(string message, object context = null)
        {
            if (!ShouldLog(LogLevel.Warn))
                return;

            var logMessage = FormatMessage("WARN", message, context);
            UnityEngine.Debug.LogWarning(logMessage);
        }

        /// <summary>
        /// Log info message
        /// </summary>
        public void Info(string message, object context = null)
        {
            if (!ShouldLog(LogLevel.Info))
                return;

            var logMessage = FormatMessage("INFO", message, context);
            UnityEngine.Debug.Log(logMessage);
        }

        /// <summary>
        /// Log debug message
        /// </summary>
        public void Debug(string message, object context = null)
        {
            if (!ShouldLog(LogLevel.Debug))
                return;

            var logMessage = FormatMessage("DEBUG", message, context);
            UnityEngine.Debug.Log(logMessage);
        }

        /// <summary>
        /// Update logger configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            _config = config ?? _config;
        }

        /// <summary>
        /// Check if message should be logged based on current log level
        /// </summary>
        private bool ShouldLog(LogLevel level)
        {
            // Always log errors and warnings
            if (level == LogLevel.Error || level == LogLevel.Warn)
            {
                return true;
            }

            // For Info/Debug respect debug flag and log level
            if (!_config.debug)
                return false;

            return level <= _config.logLevel;
        }

        /// <summary>
        /// Format log message with timestamp and context
        /// </summary>
        private string FormatMessage(string level, string message, object context)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var baseMessage = $"{LOG_PREFIX} [{timestamp}] [{level}] {message}";

            if (context != null)
            {
                try
                {
                    var contextJson = JsonUtility.ToJson(context);
                    // JsonUtility cannot handle anonymous types or dictionaries and may return "{}"
                    if (string.IsNullOrEmpty(contextJson) || contextJson == "{}")
                    {
                        contextJson = SerializeContextFallback(context);
                    }
                    baseMessage += $" | Context: {contextJson}";
                }
                catch (Exception ex)
                {
                    // Fallback: stringify via reflection
                    var fallback = SerializeContextFallback(context);
                    baseMessage += $" | Context: {fallback} (Failed to serialize: {ex.Message})";
                }
            }

            return baseMessage;
        }

        /// <summary>
        /// Fallback serialization for anonymous objects/dictionaries
        /// </summary>
        private string SerializeContextFallback(object ctx)
        {
            try
            {
                if (ctx == null)
                    return "null";

                // Handle dictionaries explicitly
                if (ctx is System.Collections.IDictionary dict)
                {
                    var kvps = new System.Text.StringBuilder("{");
                    bool first = true;
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        if (!first)
                            kvps.Append(", ");
                        kvps.Append(entry.Key).Append(": ").Append(entry.Value);
                        first = false;
                    }
                    kvps.Append("}");
                    return kvps.ToString();
                }

                // Use reflection to read public properties/fields
                var type = ctx.GetType();
                var sb = new System.Text.StringBuilder("{");
                bool firstProp = true;
                foreach (var prop in type.GetProperties())
                {
                    if (!firstProp)
                        sb.Append(", ");
                    sb.Append(prop.Name).Append(": ").Append(prop.GetValue(ctx));
                    firstProp = false;
                }
                foreach (var field in type.GetFields())
                {
                    if (!firstProp)
                        sb.Append(", ");
                    sb.Append(field.Name).Append(": ").Append(field.GetValue(ctx));
                    firstProp = false;
                }
                sb.Append("}");
                return sb.ToString();
            }
            catch
            {
                return ctx.ToString();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using LoopKit.Utils;
using UnityEngine;

namespace LoopKit.Core
{
    /// <summary>
    /// Storage manager for persisting SDK data
    /// Uses Unity's PlayerPrefs for cross-platform persistence
    /// </summary>
    public class StorageManager : IStorageManager
    {
        private readonly LoopKitConfig _config;
        private readonly ILogger _logger;

        // Storage keys
        private const string QUEUE_KEY = "LoopKit_EventQueue";
        private const string ANONYMOUS_ID_KEY = "LoopKit_AnonymousId";
        private const string SESSION_ID_KEY = "LoopKit_SessionId";
        private const string LAST_ACTIVITY_KEY = "LoopKit_LastActivity";

        public StorageManager(LoopKitConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Persist event queue to storage
        /// </summary>
        public void PersistQueue(List<object> queue)
        {
            if (!_config.enableLocalStorage)
            {
                _logger.Debug("Local storage disabled, skipping queue persistence");
                return;
            }

            try
            {
                if (queue == null || queue.Count == 0)
                {
                    PlayerPrefs.DeleteKey(QUEUE_KEY);
                    _logger.Debug("Cleared empty event queue from storage");
                    return;
                }

                var queueJson = JsonUtility.ToJson(new SerializableQueue { events = queue });
                PlayerPrefs.SetString(QUEUE_KEY, queueJson);
                PlayerPrefs.Save();

                _logger.Debug($"Persisted {queue.Count} events to storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to persist event queue", ex);
            }
        }

        /// <summary>
        /// Load persisted event queue from storage
        /// </summary>
        public List<object> LoadQueue()
        {
            if (!_config.enableLocalStorage)
            {
                _logger.Debug("Local storage disabled, returning empty queue");
                return new List<object>();
            }

            try
            {
                var queueJson = PlayerPrefs.GetString(QUEUE_KEY, "");
                if (string.IsNullOrEmpty(queueJson))
                {
                    _logger.Debug("No persisted queue found");
                    return new List<object>();
                }

                var serializableQueue = JsonUtility.FromJson<SerializableQueue>(queueJson);
                _logger.Debug($"Loaded {serializableQueue.events.Count} events from storage");

                return serializableQueue.events ?? new List<object>();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load event queue", ex);
                // Clear corrupted data
                PlayerPrefs.DeleteKey(QUEUE_KEY);
                return new List<object>();
            }
        }

        /// <summary>
        /// Clear persisted event queue
        /// </summary>
        public void ClearQueue()
        {
            try
            {
                PlayerPrefs.DeleteKey(QUEUE_KEY);
                PlayerPrefs.Save();
                _logger.Debug("Cleared event queue from storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to clear event queue", ex);
            }
        }

        /// <summary>
        /// Load anonymous ID from storage
        /// </summary>
        public string LoadAnonymousId()
        {
            try
            {
                var anonymousId = PlayerPrefs.GetString(ANONYMOUS_ID_KEY, "");
                if (!string.IsNullOrEmpty(anonymousId))
                {
                    _logger.Debug("Loaded existing anonymous ID from storage");
                }
                return anonymousId;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load anonymous ID", ex);
                return "";
            }
        }

        /// <summary>
        /// Save anonymous ID to storage
        /// </summary>
        public void SaveAnonymousId(string anonymousId)
        {
            if (string.IsNullOrEmpty(anonymousId))
            {
                _logger.Warn("Attempted to save empty anonymous ID");
                return;
            }

            try
            {
                PlayerPrefs.SetString(ANONYMOUS_ID_KEY, anonymousId);
                PlayerPrefs.Save();
                _logger.Debug("Saved anonymous ID to storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save anonymous ID", ex);
            }
        }

        /// <summary>
        /// Clear anonymous ID from storage
        /// </summary>
        public void ClearAnonymousId()
        {
            try
            {
                PlayerPrefs.DeleteKey(ANONYMOUS_ID_KEY);
                PlayerPrefs.Save();
                _logger.Debug("Cleared anonymous ID from storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to clear anonymous ID", ex);
            }
        }

        /// <summary>
        /// Load session ID from storage
        /// </summary>
        public string LoadSessionId()
        {
            try
            {
                return PlayerPrefs.GetString(SESSION_ID_KEY, "");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load session ID", ex);
                return "";
            }
        }

        /// <summary>
        /// Save session ID to storage
        /// </summary>
        public void SaveSessionId(string sessionId)
        {
            try
            {
                PlayerPrefs.SetString(SESSION_ID_KEY, sessionId);
                PlayerPrefs.Save();
                _logger.Debug("Saved session ID to storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save session ID", ex);
            }
        }

        /// <summary>
        /// Load last activity timestamp
        /// </summary>
        public DateTime LoadLastActivity()
        {
            try
            {
                var ticksString = PlayerPrefs.GetString(LAST_ACTIVITY_KEY, "");
                if (string.IsNullOrEmpty(ticksString))
                {
                    return DateTime.MinValue;
                }

                if (long.TryParse(ticksString, out var ticks))
                {
                    return new DateTime(ticks);
                }

                return DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load last activity", ex);
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Save last activity timestamp
        /// </summary>
        public void SaveLastActivity(DateTime lastActivity)
        {
            try
            {
                PlayerPrefs.SetString(LAST_ACTIVITY_KEY, lastActivity.Ticks.ToString());
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save last activity", ex);
            }
        }

        /// <summary>
        /// Clear all stored data
        /// </summary>
        public void ClearAll()
        {
            try
            {
                PlayerPrefs.DeleteKey(QUEUE_KEY);
                PlayerPrefs.DeleteKey(ANONYMOUS_ID_KEY);
                PlayerPrefs.DeleteKey(SESSION_ID_KEY);
                PlayerPrefs.DeleteKey(LAST_ACTIVITY_KEY);
                PlayerPrefs.Save();

                _logger.Info("Cleared all LoopKit data from storage");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to clear all data", ex);
            }
        }

        /// <summary>
        /// Update storage manager configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            // Configuration updates are handled by the constructor reference
            _logger.Debug("Storage manager configuration updated");
        }

        /// <summary>
        /// Serializable wrapper for queue persistence
        /// </summary>
        [System.Serializable]
        private class SerializableQueue
        {
            public List<object> events = new List<object>();
        }
    }
}

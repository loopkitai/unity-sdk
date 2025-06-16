using System;
using System.Collections.Generic;
using LoopKit.Utils;
using UnityEngine;

namespace LoopKit.Core
{
    /// <summary>
    /// Session manager for tracking user sessions
    /// Handles session lifecycle, timeouts, and persistence
    /// </summary>
    public class SessionManager : ISessionManager
    {
        private readonly LoopKitConfig _config;
        private readonly ILogger _logger;
        private readonly IdGenerator _idGenerator;
        private readonly StorageManager _storageManager;

        private string _currentSessionId;
        private string _anonymousId;
        private DateTime _lastActivity;
        private DateTime _sessionStartTime;
        private Action<string, Dictionary<string, object>> _sessionEventCallback;

        public SessionManager(
            LoopKitConfig config,
            ILogger logger,
            IdGenerator idGenerator,
            StorageManager storageManager
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            _storageManager =
                storageManager ?? throw new ArgumentNullException(nameof(storageManager));

            InitializeSession();
        }

        /// <summary>
        /// Initialize session on startup
        /// </summary>
        private void InitializeSession()
        {
            // Load or generate anonymous ID
            _anonymousId = _storageManager.LoadAnonymousId();
            if (string.IsNullOrEmpty(_anonymousId))
            {
                _anonymousId = _idGenerator.GenerateAnonymousId();
                _storageManager.SaveAnonymousId(_anonymousId);
                _logger.Info("Generated new anonymous ID");
            }
            else
            {
                _logger.Debug("Loaded existing anonymous ID from storage");
            }

            // Check for existing session
            var existingSessionId = _storageManager.LoadSessionId();
            var lastActivity = _storageManager.LoadLastActivity();

            var now = DateTime.UtcNow;
            var timeSinceLastActivity = now - lastActivity;

            // Determine if we should continue existing session or start new one
            if (
                !string.IsNullOrEmpty(existingSessionId)
                && lastActivity != DateTime.MinValue
                && timeSinceLastActivity.TotalSeconds < _config.sessionTimeout
            )
            {
                // Continue existing session
                _currentSessionId = existingSessionId;
                _sessionStartTime = lastActivity; // Use last activity as approximate session start
                _lastActivity = now;

                _logger.Info($"Continuing existing session: {_currentSessionId}");
                UpdateActivity();
            }
            else
            {
                // Start new session
                StartNewSession();
            }
        }

        /// <summary>
        /// Get current session ID
        /// </summary>
        public string GetSessionId()
        {
            CheckSessionTimeout();
            return _currentSessionId;
        }

        /// <summary>
        /// Get anonymous ID
        /// </summary>
        public string GetAnonymousId()
        {
            return _anonymousId;
        }

        /// <summary>
        /// Start new session
        /// </summary>
        public void StartSession()
        {
            var previousSessionId = _currentSessionId;
            StartNewSession();

            // Fire session start event
            if (_config.enableSessionTracking && _sessionEventCallback != null)
            {
                var properties = new Dictionary<string, object>
                {
                    ["sessionId"] = _currentSessionId,
                };

                if (!string.IsNullOrEmpty(previousSessionId))
                {
                    properties["previousSessionId"] = previousSessionId;
                }

                _sessionEventCallback("session_start", properties);
            }
        }

        /// <summary>
        /// End current session
        /// </summary>
        public void EndSession()
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                _logger.Debug("No active session to end");
                return;
            }

            // Fire session end event
            if (_config.enableSessionTracking && _sessionEventCallback != null)
            {
                var sessionDuration = (DateTime.UtcNow - _sessionStartTime).TotalSeconds;
                var properties = new Dictionary<string, object>
                {
                    ["sessionId"] = _currentSessionId,
                    ["duration"] = sessionDuration,
                    ["reason"] = "manual",
                };

                _sessionEventCallback("session_end", properties);
            }

            _logger.Info($"Session ended: {_currentSessionId}");

            // Clear session data
            _currentSessionId = null;
            _storageManager.SaveSessionId("");
        }

        /// <summary>
        /// Check if session is active
        /// </summary>
        public bool IsSessionActive()
        {
            CheckSessionTimeout();
            return !string.IsNullOrEmpty(_currentSessionId);
        }

        /// <summary>
        /// Update activity timestamp
        /// </summary>
        public void UpdateActivity()
        {
            _lastActivity = DateTime.UtcNow;
            _storageManager.SaveLastActivity(_lastActivity);

            // Ensure we have an active session
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                StartSession();
            }
        }

        /// <summary>
        /// Set callback for session events
        /// </summary>
        public void SetSessionEventCallback(Action<string, Dictionary<string, object>> callback)
        {
            _sessionEventCallback = callback;
        }

        /// <summary>
        /// Update session manager configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            // Configuration is updated via the constructor reference
            _logger.Debug("Session manager configuration updated");
        }

        /// <summary>
        /// Start a completely new session
        /// </summary>
        private void StartNewSession()
        {
            _currentSessionId = _idGenerator.GenerateSessionId();
            _sessionStartTime = DateTime.UtcNow;
            _lastActivity = _sessionStartTime;

            // Persist session data
            _storageManager.SaveSessionId(_currentSessionId);
            _storageManager.SaveLastActivity(_lastActivity);

            _logger.Info($"Started new session: {_currentSessionId}");
        }

        /// <summary>
        /// Check if current session has timed out
        /// </summary>
        private void CheckSessionTimeout()
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                return;
            }

            var timeSinceLastActivity = (DateTime.UtcNow - _lastActivity).TotalSeconds;

            if (timeSinceLastActivity >= _config.sessionTimeout)
            {
                _logger.Info($"Session timed out after {timeSinceLastActivity} seconds");

                // Fire session end event for timeout
                if (_config.enableSessionTracking && _sessionEventCallback != null)
                {
                    var sessionDuration = (DateTime.UtcNow - _sessionStartTime).TotalSeconds;
                    var properties = new Dictionary<string, object>
                    {
                        ["sessionId"] = _currentSessionId,
                        ["duration"] = sessionDuration,
                        ["reason"] = "timeout",
                    };

                    _sessionEventCallback("session_end", properties);
                }

                // Start new session
                StartNewSession();

                // Fire session start event for new session
                if (_config.enableSessionTracking && _sessionEventCallback != null)
                {
                    var properties = new Dictionary<string, object>
                    {
                        ["sessionId"] = _currentSessionId,
                    };

                    _sessionEventCallback("session_start", properties);
                }
            }
        }

        /// <summary>
        /// Reset session manager state (for testing)
        /// </summary>
        internal void Reset()
        {
            _logger.Debug("Resetting session manager");

            // End current session if active
            if (!string.IsNullOrEmpty(_currentSessionId))
            {
                EndSession();
            }

            // Clear all stored data
            _storageManager.ClearAll();

            // Reinitialize
            InitializeSession();
        }
    }
}

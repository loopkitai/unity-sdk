using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LoopKit.Core;
using LoopKit.Utils;
using UnityEngine;
using Logger = LoopKit.Utils.Logger;

namespace LoopKit
{
    /// <summary>
    /// Main LoopKit Unity SDK class
    /// Provides analytics tracking, user identification, and session management
    /// </summary>
    public class LoopKit : ILoopKit
    {
        public const string VERSION = VersionInfo.VERSION;
        private const string TRACKING_ENABLED_PREF_KEY = "LoopKit_TrackingEnabled";

        // Core configuration
        private LoopKitConfig _config;
        private bool _initialized = false;
        private bool _trackingEnabled = true; // Default value, will be loaded from prefs

        // User context
        private string _userId;
        private Dictionary<string, object> _userProperties = new Dictionary<string, object>();
        private string _groupId;
        private Dictionary<string, object> _groupProperties = new Dictionary<string, object>();

        // Core components
        private Logger _logger;
        private IdGenerator _idGenerator;
        private StorageManager _storageManager;
        private SessionManager _sessionManager;
        private QueueManager _queueManager;
        private EventTracker _eventTracker;
        private NetworkManager _networkManager;
        private UnityFeatures _unityFeatures;

        // Singleton instance
        private static LoopKit _instance;
        public static LoopKit Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LoopKit();
                }
                return _instance;
            }
        }

        public string Version => VERSION;

        /// <summary>
        /// Initialize LoopKit with API key and configuration
        /// </summary>
        public ILoopKit Init(string apiKey, LoopKitConfig config = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException(
                    "API key is required and cannot be empty",
                    nameof(apiKey)
                );
            }

            // Create or update configuration
            _config = config ?? new LoopKitConfig();
            _config.apiKey = apiKey;

            // Validate and sanitize configuration
            ConfigValidator.Validate(_config);
            _config = ConfigValidator.Sanitize(_config);

            // Load tracking state from PlayerPrefs (defaults to enabled if not set)
            _trackingEnabled = PlayerPrefs.GetInt(TRACKING_ENABLED_PREF_KEY, 1) == 1;

            // Initialize core components
            _logger = new Logger(_config);
            _idGenerator = new IdGenerator();
            _storageManager = new StorageManager(_config, _logger);
            _sessionManager = new SessionManager(_config, _logger, _idGenerator, _storageManager);
            _queueManager = new QueueManager(_config, _logger, _storageManager);
            _eventTracker = new EventTracker(
                _config,
                _logger,
                _queueManager,
                _sessionManager,
                _idGenerator
            );
            _networkManager = new NetworkManager(_config, _logger);
            _unityFeatures = new UnityFeatures(
                _config,
                _logger,
                _eventTracker,
                _sessionManager,
                _queueManager
            );

            // Cross-wire dependencies
            _queueManager.SetNetworkManager(_networkManager);
            _queueManager.ScheduleFlush();

            // Setup session event tracking callback
            _sessionManager.SetSessionEventCallback(OnSessionEvent);

            // Setup Unity features
            _unityFeatures.SetNetworkManager(_networkManager);
            _unityFeatures.SetupFeatures();

            _initialized = true;

            _logger.Info(
                $"LoopKit Unity SDK initialized",
                new
                {
                    version = VERSION,
                    apiKey = _config.apiKey.Substring(0, Math.Min(8, _config.apiKey.Length))
                        + "...",
                    platform = Application.platform.ToString(),
                    trackingEnabled = _trackingEnabled,
                }
            );

            return this;
        }

        /// <summary>
        /// Configure the SDK with new settings
        /// </summary>
        public ILoopKit Configure(LoopKitConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "Configuration cannot be null");
            }

            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "LoopKit must be initialized before configuration"
                );
            }

            // Validate configuration
            ConfigValidator.Validate(config);

            // Merge with existing config
            var mergedConfig = new LoopKitConfig
            {
                apiKey = config.apiKey ?? _config.apiKey,
                baseURL = config.baseURL ?? _config.baseURL,
                batchSize = config.batchSize != 0 ? config.batchSize : _config.batchSize,
                flushInterval =
                    config.flushInterval != 0 ? config.flushInterval : _config.flushInterval,
                maxQueueSize =
                    config.maxQueueSize != 0 ? config.maxQueueSize : _config.maxQueueSize,
                enableCompression = config.enableCompression,
                requestTimeout =
                    config.requestTimeout != 0 ? config.requestTimeout : _config.requestTimeout,
                maxRetries = config.maxRetries != 0 ? config.maxRetries : _config.maxRetries,
                retryBackoff = config.retryBackoff,
                enableSessionTracking = config.enableSessionTracking,
                sessionTimeout =
                    config.sessionTimeout != 0 ? config.sessionTimeout : _config.sessionTimeout,
                enableErrorTracking = config.enableErrorTracking,
                enableSceneTracking = config.enableSceneTracking,
                enableFpsTracking = config.enableFpsTracking,
                fpsSampleInterval =
                    config.fpsSampleInterval != 0
                        ? config.fpsSampleInterval
                        : _config.fpsSampleInterval,
                fpsReportInterval =
                    config.fpsReportInterval != 0
                        ? config.fpsReportInterval
                        : _config.fpsReportInterval,
                enableMemoryTracking = config.enableMemoryTracking,
                enableNetworkTracking = config.enableNetworkTracking,
                enableLocalStorage = config.enableLocalStorage,
                respectDoNotTrack = config.respectDoNotTrack,
                debug = config.debug,
                logLevel = config.logLevel,
                onBeforeTrack = config.onBeforeTrack ?? _config.onBeforeTrack,
                onAfterTrack = config.onAfterTrack ?? _config.onAfterTrack,
                onError = config.onError ?? _config.onError,
            };

            _config = ConfigValidator.Sanitize(mergedConfig);

            // Update all components
            _logger?.UpdateConfig(_config);
            _storageManager?.UpdateConfig(_config);
            _sessionManager?.UpdateConfig(_config);
            _queueManager?.UpdateConfig(_config);
            _eventTracker?.UpdateConfig(_config);
            _networkManager?.UpdateConfig(_config);
            _unityFeatures?.UpdateConfig(_config);

            _logger.Info("LoopKit configuration updated");

            return this;
        }

        /// <summary>
        /// Get current configuration
        /// </summary>
        public LoopKitConfig GetConfig()
        {
            ThrowIfNotInitialized();
            return _config;
        }

        /// <summary>
        /// Enable event tracking
        /// </summary>
        public ILoopKit EnableTracking()
        {
            ThrowIfNotInitialized();

            _trackingEnabled = true;
            PlayerPrefs.SetInt(TRACKING_ENABLED_PREF_KEY, 1);
            PlayerPrefs.Save();

            _logger.Info("Event tracking enabled and saved to preferences");

            return this;
        }

        /// <summary>
        /// Disable event tracking
        /// </summary>
        public ILoopKit DisableTracking()
        {
            ThrowIfNotInitialized();

            _trackingEnabled = false;
            PlayerPrefs.SetInt(TRACKING_ENABLED_PREF_KEY, 0);
            PlayerPrefs.Save();

            _logger.Info("Event tracking disabled and saved to preferences");

            return this;
        }

        /// <summary>
        /// Check if event tracking is currently enabled
        /// </summary>
        public bool IsTrackingEnabled()
        {
            ThrowIfNotInitialized();
            return _trackingEnabled;
        }

        /// <summary>
        /// Track an event
        /// </summary>
        public ILoopKit Track(
            string eventName,
            Dictionary<string, object> properties = null,
            TrackOptions options = null
        )
        {
            ThrowIfNotInitialized();

            if (!_trackingEnabled)
            {
                _logger.Debug($"Tracking disabled, skipping event: {eventName}");
                return this;
            }

            if (string.IsNullOrEmpty(eventName))
            {
                _logger.Warn("Event name cannot be null or empty");
                return this;
            }

            var userContext = new
            {
                userId = _userId,
                userProperties = _userProperties,
                groupId = _groupId,
                groupProperties = _groupProperties,
            };

            _eventTracker.Track(eventName, properties, options, userContext);

            return this;
        }

        /// <summary>
        /// Track multiple events in batch
        /// </summary>
        public ILoopKit TrackBatch(List<BatchEventInput> events)
        {
            ThrowIfNotInitialized();

            if (!_trackingEnabled)
            {
                _logger.Debug("Tracking disabled, skipping event batch");
                return this;
            }

            if (events == null || events.Count == 0)
            {
                _logger.Warn("Event batch is null or empty");
                return this;
            }

            foreach (var eventInput in events)
            {
                if (eventInput != null && !string.IsNullOrEmpty(eventInput.name))
                {
                    Track(eventInput.name, eventInput.properties, eventInput.options);
                }
            }

            return this;
        }

        /// <summary>
        /// Identify a user
        /// </summary>
        public ILoopKit Identify(string userId, Dictionary<string, object> properties = null)
        {
            ThrowIfNotInitialized();

            if (!_trackingEnabled)
            {
                _logger.Debug($"Tracking disabled, skipping identify for user: {userId}");
                return this;
            }

            if (string.IsNullOrEmpty(userId))
            {
                _logger.Warn("User ID cannot be null or empty for identify");
                return this;
            }

            // Update user context
            _userId = userId;
            _userProperties = properties ?? new Dictionary<string, object>();

            var userContext = new
            {
                userId = _userId,
                userProperties = _userProperties,
                groupId = _groupId,
                groupProperties = _groupProperties,
            };

            _eventTracker.Identify(userId, properties, userContext);

            return this;
        }

        /// <summary>
        /// Associate user with a group
        /// </summary>
        public ILoopKit Group(
            string groupId,
            Dictionary<string, object> properties = null,
            string groupType = "organization"
        )
        {
            ThrowIfNotInitialized();

            if (!_trackingEnabled)
            {
                _logger.Debug($"Tracking disabled, skipping group association: {groupId}");
                return this;
            }

            if (string.IsNullOrEmpty(groupId))
            {
                _logger.Warn("Group ID cannot be null or empty for group");
                return this;
            }

            // Update group context
            _groupId = groupId;
            _groupProperties = properties ?? new Dictionary<string, object>();

            var userContext = new
            {
                userId = _userId,
                userProperties = _userProperties,
                groupId = _groupId,
                groupProperties = _groupProperties,
            };

            _eventTracker.Group(groupId, properties, groupType, userContext);

            return this;
        }

        /// <summary>
        /// Manually flush queued events
        /// </summary>
        public async Task FlushAsync()
        {
            ThrowIfNotInitialized();
            await _queueManager.FlushAsync(_networkManager);
        }

        /// <summary>
        /// Get current queue size
        /// </summary>
        public int GetQueueSize()
        {
            ThrowIfNotInitialized();
            return _queueManager.GetQueueSize();
        }

        /// <summary>
        /// Reset SDK state
        /// </summary>
        public void Reset()
        {
            ThrowIfNotInitialized();

            _logger.Info("Resetting LoopKit SDK state");

            // Clear user context
            _userId = null;
            _userProperties.Clear();
            _groupId = null;
            _groupProperties.Clear();

            // Reset tracking state to default and save to prefs
            _trackingEnabled = true;
            PlayerPrefs.SetInt(TRACKING_ENABLED_PREF_KEY, 1);
            PlayerPrefs.Save();

            // Reset components
            _queueManager?.Reset();
            _sessionManager?.Reset();

            _logger.Info("LoopKit SDK state reset complete");
        }

        /// <summary>
        /// Handle session events from session manager
        /// </summary>
        private void OnSessionEvent(string eventName, Dictionary<string, object> properties)
        {
            if (_config.enableSessionTracking)
            {
                Track(eventName, properties);
            }
        }

        /// <summary>
        /// Ensure SDK is initialized before operations
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "LoopKit must be initialized with Init() before use"
                );
            }
        }
    }

    /// <summary>
    /// Static convenience methods for easier access
    /// </summary>
    public static class LoopKitAPI
    {
        /// <summary>
        /// Initialize LoopKit with API key
        /// </summary>
        public static ILoopKit Init(string apiKey, LoopKitConfig config = null)
        {
            return LoopKit.Instance.Init(apiKey, config);
        }

        /// <summary>
        /// Enable event tracking
        /// </summary>
        public static ILoopKit EnableTracking()
        {
            return LoopKit.Instance.EnableTracking();
        }

        /// <summary>
        /// Disable event tracking
        /// </summary>
        public static ILoopKit DisableTracking()
        {
            return LoopKit.Instance.DisableTracking();
        }

        /// <summary>
        /// Check if event tracking is currently enabled
        /// </summary>
        public static bool IsTrackingEnabled()
        {
            return LoopKit.Instance.IsTrackingEnabled();
        }

        /// <summary>
        /// Track an event
        /// </summary>
        public static ILoopKit Track(
            string eventName,
            Dictionary<string, object> properties = null,
            TrackOptions options = null
        )
        {
            return LoopKit.Instance.Track(eventName, properties, options);
        }

        /// <summary>
        /// Track multiple events in batch
        /// </summary>
        public static ILoopKit TrackBatch(List<BatchEventInput> events)
        {
            return LoopKit.Instance.TrackBatch(events);
        }

        /// <summary>
        /// Identify a user
        /// </summary>
        public static ILoopKit Identify(string userId, Dictionary<string, object> properties = null)
        {
            return LoopKit.Instance.Identify(userId, properties);
        }

        /// <summary>
        /// Associate user with a group
        /// </summary>
        public static ILoopKit Group(
            string groupId,
            Dictionary<string, object> properties = null,
            string groupType = "organization"
        )
        {
            return LoopKit.Instance.Group(groupId, properties, groupType);
        }

        /// <summary>
        /// Flush events
        /// </summary>
        public static Task FlushAsync()
        {
            return LoopKit.Instance.FlushAsync();
        }

        /// <summary>
        /// Get queue size
        /// </summary>
        public static int GetQueueSize()
        {
            return LoopKit.Instance.GetQueueSize();
        }

        /// <summary>
        /// Reset SDK
        /// </summary>
        public static void Reset()
        {
            LoopKit.Instance.Reset();
        }
    }
}

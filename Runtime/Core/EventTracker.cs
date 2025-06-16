using System;
using System.Collections.Generic;
using LoopKit.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoopKit.Core
{
    /// <summary>
    /// Event tracker for creating and managing events
    /// Handles event creation, system context, and validation
    /// </summary>
    public class EventTracker : IEventTracker
    {
        private readonly LoopKitConfig _config;
        private readonly ILogger _logger;
        private readonly IQueueManager _queueManager;
        private readonly ISessionManager _sessionManager;
        private readonly IdGenerator _idGenerator;

        public EventTracker(
            LoopKitConfig config,
            ILogger logger,
            IQueueManager queueManager,
            ISessionManager sessionManager,
            IdGenerator idGenerator
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
            _sessionManager =
                sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        /// <summary>
        /// Track an event
        /// </summary>
        public void Track(
            string eventName,
            Dictionary<string, object> properties,
            TrackOptions options,
            object userContext
        )
        {
            if (string.IsNullOrEmpty(eventName))
            {
                _logger.Warn("Event name cannot be null or empty");
                return;
            }

            try
            {
                var trackEvent = CreateTrackEvent(eventName, properties, options, userContext);

                // Apply callback if configured
                if (_config.onBeforeTrack != null)
                {
                    try
                    {
                        var modifiedEvent = _config.onBeforeTrack(trackEvent);
                        if (modifiedEvent != null)
                        {
                            trackEvent = modifiedEvent;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error in onBeforeTrack callback", ex);
                    }
                }

                _queueManager.EnqueueEvent(trackEvent);
                _sessionManager.UpdateActivity();

                _logger.Debug($"Tracked event: {eventName}", properties);

                // Apply after-track callback if configured
                if (_config.onAfterTrack != null)
                {
                    try
                    {
                        _config.onAfterTrack(trackEvent, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error in onAfterTrack callback", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to track event: {eventName}", ex);

                // Call error callback if configured
                if (_config.onError != null)
                {
                    try
                    {
                        _config.onError(ex);
                    }
                    catch (Exception callbackEx)
                    {
                        _logger.Error("Error in onError callback", callbackEx);
                    }
                }
            }
        }

        /// <summary>
        /// Create identify event
        /// </summary>
        public void Identify(
            string userId,
            Dictionary<string, object> properties,
            object userContext
        )
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Warn("User ID cannot be null or empty for identify event");
                return;
            }

            try
            {
                var identifyEvent = CreateIdentifyEvent(userId, properties, userContext);

                _queueManager.EnqueueEvent(identifyEvent);
                _sessionManager.UpdateActivity();

                _logger.Info($"Identified user: {userId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to identify user: {userId}", ex);

                if (_config.onError != null)
                {
                    try
                    {
                        _config.onError(ex);
                    }
                    catch (Exception callbackEx)
                    {
                        _logger.Error("Error in onError callback", callbackEx);
                    }
                }
            }
        }

        /// <summary>
        /// Create group event
        /// </summary>
        public void Group(
            string groupId,
            Dictionary<string, object> properties,
            string groupType,
            object userContext
        )
        {
            if (string.IsNullOrEmpty(groupId))
            {
                _logger.Warn("Group ID cannot be null or empty for group event");
                return;
            }

            try
            {
                var groupEvent = CreateGroupEvent(groupId, properties, groupType, userContext);

                _queueManager.EnqueueEvent(groupEvent);
                _sessionManager.UpdateActivity();

                _logger.Info($"Associated with group: {groupId} (type: {groupType})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create group event: {groupId}", ex);

                if (_config.onError != null)
                {
                    try
                    {
                        _config.onError(ex);
                    }
                    catch (Exception callbackEx)
                    {
                        _logger.Error("Error in onError callback", callbackEx);
                    }
                }
            }
        }

        /// <summary>
        /// Update event tracker configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            _logger.Debug("Event tracker configuration updated");
        }

        /// <summary>
        /// Create a track event with full context
        /// </summary>
        private TrackEvent CreateTrackEvent(
            string eventName,
            Dictionary<string, object> properties,
            TrackOptions options,
            object userContext
        )
        {
            var trackEvent = new TrackEvent
            {
                name = eventName,
                properties = properties ?? new Dictionary<string, object>(),
                anonymousId = _sessionManager.GetAnonymousId(),
                timestamp = GetTimestamp(options),
                system = CreateSystemInfo(),
                userId = ExtractUserId(userContext),
            };

            // Add context properties if provided
            if (options?.context != null)
            {
                foreach (var kvp in options.context)
                {
                    trackEvent.properties[$"context_{kvp.Key}"] = kvp.Value;
                }
            }

            return trackEvent;
        }

        /// <summary>
        /// Create an identify event with full context
        /// </summary>
        private IdentifyEvent CreateIdentifyEvent(
            string userId,
            Dictionary<string, object> properties,
            object userContext
        )
        {
            return new IdentifyEvent
            {
                userId = userId,
                properties = properties ?? new Dictionary<string, object>(),
                anonymousId = _sessionManager.GetAnonymousId(),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                system = CreateSystemInfo(),
            };
        }

        /// <summary>
        /// Create a group event with full context
        /// </summary>
        private GroupEvent CreateGroupEvent(
            string groupId,
            Dictionary<string, object> properties,
            string groupType,
            object userContext
        )
        {
            return new GroupEvent
            {
                groupId = groupId,
                groupType = groupType ?? "organization",
                properties = properties ?? new Dictionary<string, object>(),
                anonymousId = _sessionManager.GetAnonymousId(),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                system = CreateSystemInfo(),
                userId = ExtractUserId(userContext),
            };
        }

        /// <summary>
        /// Create system information for events
        /// </summary>
        private SystemInfo CreateSystemInfo()
        {
            var currentScene = SceneManager.GetActiveScene();

            return new SystemInfo
            {
                sdk = new SDKInfo { name = "unity", version = Utils.VersionInfo.VERSION },
                sessionId = _sessionManager.GetSessionId(),
                context = new ContextInfo
                {
                    scene = new SceneInfo
                    {
                        name = currentScene.name,
                        buildIndex = currentScene.buildIndex,
                        path = currentScene.path,
                    },
                    platform = Application.platform.ToString(),
                    operatingSystem = UnityEngine.SystemInfo.operatingSystem,
                    device = new DeviceInfo
                    {
                        model = UnityEngine.SystemInfo.deviceModel,
                        type = UnityEngine.SystemInfo.deviceType.ToString(),
                        uniqueIdentifier = UnityEngine.SystemInfo.deviceUniqueIdentifier,
                        systemMemorySize = UnityEngine.SystemInfo.systemMemorySize,
                        graphicsDeviceName = UnityEngine.SystemInfo.graphicsDeviceName,
                        resolution = Screen.currentResolution,
                    },
                    application = new ApplicationInfo
                    {
                        version = Application.version,
                        unityVersion = Application.unityVersion,
                        companyName = Application.companyName,
                        productName = Application.productName,
                        buildGUID = Application.buildGUID,
                    },
                },
            };
        }

        /// <summary>
        /// Get timestamp for event, using provided options or current time
        /// </summary>
        private string GetTimestamp(TrackOptions options)
        {
            if (options != null && !string.IsNullOrEmpty(options.timestamp))
            {
                return options.timestamp;
            }

            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// Extract user ID from user context object
        /// </summary>
        private string ExtractUserId(object userContext)
        {
            if (userContext == null)
                return null;

            // Try to extract userId from different possible structures
            if (userContext is string userId)
            {
                return userId;
            }

            // Use reflection to try to find userId property
            try
            {
                var userIdProperty = userContext.GetType().GetProperty("userId");
                if (userIdProperty != null)
                {
                    return userIdProperty.GetValue(userContext)?.ToString();
                }

                var userIdField = userContext.GetType().GetField("userId");
                if (userIdField != null)
                {
                    return userIdField.GetValue(userContext)?.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("Failed to extract userId from context", ex);
            }

            return null;
        }
    }
}

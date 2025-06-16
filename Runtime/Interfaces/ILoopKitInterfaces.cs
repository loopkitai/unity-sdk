using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoopKit
{
    /// <summary>
    /// Main LoopKit SDK interface
    /// </summary>
    public interface ILoopKit
    {
        /// <summary>
        /// SDK version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Initialize the SDK
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="config">Optional configuration</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit Init(string apiKey, LoopKitConfig config = null);

        /// <summary>
        /// Configure the SDK
        /// </summary>
        /// <param name="config">Configuration to apply</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit Configure(LoopKitConfig config);

        /// <summary>
        /// Get current configuration
        /// </summary>
        /// <returns>Current configuration</returns>
        LoopKitConfig GetConfig();

        /// <summary>
        /// Track an event
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Event properties</param>
        /// <param name="options">Tracking options</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit Track(
            string eventName,
            Dictionary<string, object> properties = null,
            TrackOptions options = null
        );

        /// <summary>
        /// Track multiple events in batch
        /// </summary>
        /// <param name="events">Events to track</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit TrackBatch(List<BatchEventInput> events);

        /// <summary>
        /// Identify a user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="properties">User properties</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit Identify(string userId, Dictionary<string, object> properties = null);

        /// <summary>
        /// Associate user with a group
        /// </summary>
        /// <param name="groupId">Group identifier</param>
        /// <param name="properties">Group properties</param>
        /// <param name="groupType">Type of group</param>
        /// <returns>SDK instance for chaining</returns>
        ILoopKit Group(
            string groupId,
            Dictionary<string, object> properties = null,
            string groupType = "organization"
        );

        /// <summary>
        /// Manually flush queued events
        /// </summary>
        /// <returns>Task for async operation</returns>
        Task FlushAsync();

        /// <summary>
        /// Get current queue size
        /// </summary>
        /// <returns>Number of queued events</returns>
        int GetQueueSize();

        /// <summary>
        /// Reset SDK state
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Storage manager interface for persisting data
    /// </summary>
    public interface IStorageManager
    {
        /// <summary>
        /// Persist event queue
        /// </summary>
        /// <param name="queue">Events to persist</param>
        void PersistQueue(List<object> queue);

        /// <summary>
        /// Load persisted queue
        /// </summary>
        /// <returns>Persisted events</returns>
        List<object> LoadQueue();

        /// <summary>
        /// Clear persisted queue
        /// </summary>
        void ClearQueue();

        /// <summary>
        /// Load anonymous ID
        /// </summary>
        /// <returns>Anonymous ID or null</returns>
        string LoadAnonymousId();

        /// <summary>
        /// Save anonymous ID
        /// </summary>
        /// <param name="anonymousId">ID to save</param>
        void SaveAnonymousId(string anonymousId);

        /// <summary>
        /// Clear anonymous ID
        /// </summary>
        void ClearAnonymousId();

        /// <summary>
        /// Clear all stored data
        /// </summary>
        void ClearAll();
    }

    /// <summary>
    /// Session manager interface for tracking user sessions
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Get current session ID
        /// </summary>
        /// <returns>Session ID</returns>
        string GetSessionId();

        /// <summary>
        /// Get anonymous ID
        /// </summary>
        /// <returns>Anonymous ID</returns>
        string GetAnonymousId();

        /// <summary>
        /// Start new session
        /// </summary>
        void StartSession();

        /// <summary>
        /// End current session
        /// </summary>
        void EndSession();

        /// <summary>
        /// Check if session is active
        /// </summary>
        /// <returns>True if session is active</returns>
        bool IsSessionActive();

        /// <summary>
        /// Update activity timestamp
        /// </summary>
        void UpdateActivity();

        /// <summary>
        /// Set callback for session events
        /// </summary>
        /// <param name="callback">Callback function</param>
        void SetSessionEventCallback(Action<string, Dictionary<string, object>> callback);
    }

    /// <summary>
    /// Queue manager interface for managing event queue
    /// </summary>
    public interface IQueueManager
    {
        /// <summary>
        /// Add event to queue
        /// </summary>
        /// <param name="eventData">Event to queue</param>
        void EnqueueEvent(object eventData);

        /// <summary>
        /// Flush events to API
        /// </summary>
        /// <param name="networkManager">Network manager to use</param>
        /// <returns>Task for async operation</returns>
        Task FlushAsync(INetworkManager networkManager);

        /// <summary>
        /// Get current queue
        /// </summary>
        /// <returns>Current queue</returns>
        List<object> GetQueue();

        /// <summary>
        /// Get queue size
        /// </summary>
        /// <returns>Number of queued events</returns>
        int GetQueueSize();

        /// <summary>
        /// Clear queue
        /// </summary>
        void ClearQueue();

        /// <summary>
        /// Reset queue state
        /// </summary>
        void Reset();

        /// <summary>
        /// Schedule automatic flush
        /// </summary>
        void ScheduleFlush();

        /// <summary>
        /// Set network manager reference
        /// </summary>
        /// <param name="networkManager">Network manager</param>
        void SetNetworkManager(INetworkManager networkManager);
    }

    /// <summary>
    /// Network manager interface for API communication
    /// </summary>
    public interface INetworkManager
    {
        /// <summary>
        /// Send events to API
        /// </summary>
        /// <param name="endpoint">API endpoint</param>
        /// <param name="payload">Data to send</param>
        /// <param name="retryCount">Current retry attempt</param>
        /// <returns>Task with API response</returns>
        Task<ApiResponse> SendEventsAsync(string endpoint, object payload, int retryCount = 0);

        /// <summary>
        /// Update SDK configuration
        /// </summary>
        /// <param name="config">New configuration</param>
        void UpdateConfig(LoopKitConfig config);
    }

    /// <summary>
    /// Unity features interface for Unity-specific functionality
    /// </summary>
    public interface IUnityFeatures
    {
        /// <summary>
        /// Setup all Unity features
        /// </summary>
        void SetupFeatures();

        /// <summary>
        /// Setup scene change tracking
        /// </summary>
        void SetupSceneTracking();

        /// <summary>
        /// Setup error tracking
        /// </summary>
        void SetupErrorTracking();

        /// <summary>
        /// Setup application lifecycle tracking
        /// </summary>
        void SetupApplicationLifecycleTracking();

        /// <summary>
        /// Set network manager reference
        /// </summary>
        /// <param name="networkManager">Network manager</param>
        void SetNetworkManager(INetworkManager networkManager);
    }

    /// <summary>
    /// Event tracker interface for creating and tracking events
    /// </summary>
    public interface IEventTracker
    {
        /// <summary>
        /// Track an event
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="properties">Event properties</param>
        /// <param name="options">Tracking options</param>
        /// <param name="userContext">User context</param>
        void Track(
            string eventName,
            Dictionary<string, object> properties,
            TrackOptions options,
            object userContext
        );

        /// <summary>
        /// Create identify event
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="properties">User properties</param>
        /// <param name="userContext">User context</param>
        void Identify(string userId, Dictionary<string, object> properties, object userContext);

        /// <summary>
        /// Create group event
        /// </summary>
        /// <param name="groupId">Group ID</param>
        /// <param name="properties">Group properties</param>
        /// <param name="groupType">Group type</param>
        /// <param name="userContext">User context</param>
        void Group(
            string groupId,
            Dictionary<string, object> properties,
            string groupType,
            object userContext
        );
    }

    /// <summary>
    /// Logger interface for SDK logging
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log error message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="context">Additional context</param>
        void Error(string message, object context = null);

        /// <summary>
        /// Log warning message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="context">Additional context</param>
        void Warn(string message, object context = null);

        /// <summary>
        /// Log info message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="context">Additional context</param>
        void Info(string message, object context = null);

        /// <summary>
        /// Log debug message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="context">Additional context</param>
        void Debug(string message, object context = null);

        /// <summary>
        /// Update logger configuration
        /// </summary>
        /// <param name="config">New configuration</param>
        void UpdateConfig(LoopKitConfig config);
    }
}

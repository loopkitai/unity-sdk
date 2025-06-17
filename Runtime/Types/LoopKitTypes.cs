using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopKit
{
    /// <summary>
    /// Logging levels for the LoopKit SDK
    /// </summary>
    public enum LogLevel
    {
        Error,
        Warn,
        Info,
        Debug,
    }

    /// <summary>
    /// Retry backoff strategies
    /// </summary>
    public enum RetryBackoff
    {
        Exponential,
        Linear,
    }

    /// <summary>
    /// Configuration options for LoopKit SDK
    /// </summary>
    [System.Serializable]
    public class LoopKitConfig
    {
        [Header("API Configuration")]
        [Tooltip("API key for authentication")]
        public string apiKey = "";

        [Tooltip("Base URL for the LoopKit API")]
        public string baseURL = "https://drain.loopkit.ai/v1"; // TODO: Change to production URL

        [Header("Batching and Performance")]
        [Tooltip("Number of events to batch before auto-flushing")]
        [Range(1, 100)]
        public int batchSize = 50;

        [Tooltip("Interval in seconds to auto-flush events")]
        [Range(1, 300)]
        public float flushInterval = 5f;

        [Tooltip("Maximum number of events to store in queue")]
        [Range(100, 10000)]
        public int maxQueueSize = 1000;

        [Header("Network Settings")]
        [Tooltip("Enable gzip compression for requests")]
        public bool enableCompression = true;

        [Tooltip("Request timeout in milliseconds")]
        [Range(1000, 30000)]
        public int requestTimeout = 10000;

        [Tooltip("Maximum number of retry attempts")]
        [Range(0, 10)]
        public int maxRetries = 3;

        [Tooltip("Retry backoff strategy")]
        public RetryBackoff retryBackoff = RetryBackoff.Exponential;

        [Header("Tracking Features")]
        [Tooltip("Enable session tracking")]
        public bool enableSessionTracking = true;

        [Tooltip("Session timeout in seconds")]
        [Range(60, 7200)]
        public float sessionTimeout = 1800f; // 30 minutes

        [Tooltip("Enable automatic error tracking")]
        public bool enableErrorTracking = true;

        [Tooltip("Enable automatic scene change tracking")]
        public bool enableSceneTracking = true;

        [Tooltip("Enable automatic FPS tracking")]
        public bool enableFpsTracking = true;

        [Tooltip("How often to sample FPS (in seconds)")]
        [Range(0.1f, 5f)]
        public float fpsSampleInterval = 1f;

        [Tooltip("How often to report FPS data (in seconds)")]
        [Range(5f, 300f)]
        public float fpsReportInterval = 30f;

        [Header("Storage and Privacy")]
        [Tooltip("Enable local storage for event persistence")]
        public bool enableLocalStorage = true;

        [Tooltip("Respect Do Not Track setting")]
        public bool respectDoNotTrack = true;

        [Header("Debugging")]
        [Tooltip("Enable debug logging")]
        public bool debug = false;

        [Tooltip("Logging level")]
        public LogLevel logLevel = LogLevel.Debug;

        [Header("Event Callbacks")]
        [Tooltip("Callback before tracking events")]
        public System.Func<TrackEvent, TrackEvent> onBeforeTrack;

        [Tooltip("Callback after successfully flushing events")]
        public System.Action<TrackEvent, bool> onAfterTrack;

        [Tooltip("Error callback")]
        public System.Action<Exception> onError;
    }

    /// <summary>
    /// System information included with events
    /// </summary>
    [System.Serializable]
    public class SystemInfo
    {
        public SDKInfo sdk = new SDKInfo();
        public string sessionId;
        public ContextInfo context = new ContextInfo();
    }

    /// <summary>
    /// SDK information
    /// </summary>
    [System.Serializable]
    public class SDKInfo
    {
        public string name = "unity";
        public string version = Utils.VersionInfo.VERSION;
    }

    /// <summary>
    /// Unity environment context
    /// </summary>
    [System.Serializable]
    public class ContextInfo
    {
        public SceneInfo scene = new SceneInfo();
        public string platform;
        public string operatingSystem;
        public DeviceInfo device = new DeviceInfo();
        public ApplicationInfo application = new ApplicationInfo();
    }

    /// <summary>
    /// Current scene information
    /// </summary>
    [System.Serializable]
    public class SceneInfo
    {
        public string name;
        public int buildIndex;
        public string path;
    }

    /// <summary>
    /// Device information
    /// </summary>
    [System.Serializable]
    public class DeviceInfo
    {
        public string model;
        public string type;
        public string uniqueIdentifier;
        public int systemMemorySize;
        public string graphicsDeviceName;
        public Resolution resolution;
    }

    /// <summary>
    /// Application information
    /// </summary>
    [System.Serializable]
    public class ApplicationInfo
    {
        public string version;
        public string unityVersion;
        public string companyName;
        public string productName;
        public string buildGUID;
    }

    /// <summary>
    /// Base event properties
    /// </summary>
    [System.Serializable]
    public abstract class BaseEvent
    {
        public string anonymousId;
        public string timestamp;
        public string userId;
        public SystemInfo system = new SystemInfo();
    }

    /// <summary>
    /// Track event for analytics
    /// </summary>
    [System.Serializable]
    public class TrackEvent : BaseEvent
    {
        public string name;
        public Dictionary<string, object> properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// Identify event for user identification
    /// </summary>
    [System.Serializable]
    public class IdentifyEvent : BaseEvent
    {
        public Dictionary<string, object> properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// Group event for user-group association
    /// </summary>
    [System.Serializable]
    public class GroupEvent : BaseEvent
    {
        public string groupId;
        public string groupType;
        public Dictionary<string, object> properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// Batch event input for trackBatch method
    /// </summary>
    [System.Serializable]
    public class BatchEventInput
    {
        public string name;
        public Dictionary<string, object> properties = new Dictionary<string, object>();
        public TrackOptions options = new TrackOptions();
    }

    /// <summary>
    /// Options for tracking events
    /// </summary>
    [System.Serializable]
    public class TrackOptions
    {
        public string timestamp;
        public Dictionary<string, object> context = new Dictionary<string, object>();
    }

    /// <summary>
    /// API response structure
    /// </summary>
    [System.Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message;
        public object data;
    }

    /// <summary>
    /// API payload structure
    /// </summary>
    [System.Serializable]
    public class ApiPayload
    {
        public List<TrackEvent> tracks = new List<TrackEvent>();
        public List<IdentifyEvent> identifies = new List<IdentifyEvent>();
        public List<GroupEvent> groups = new List<GroupEvent>();
    }
}

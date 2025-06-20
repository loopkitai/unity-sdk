using System;
using System.Collections;
using System.Collections.Generic;
using LoopKit.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoopKit.Core
{
    /// <summary>
    /// Unity-specific features for automatic tracking
    /// Handles scene changes, errors, and application lifecycle events
    /// </summary>
    public class UnityFeatures : IUnityFeatures
    {
        private readonly LoopKitConfig _config;
        private readonly ILogger _logger;
        private readonly IEventTracker _eventTracker;
        private readonly ISessionManager _sessionManager;
        private readonly IQueueManager _queueManager;

        private INetworkManager _networkManager;
        private bool _isSetup = false;
        private string _previousSceneName;

        // FPS tracking
        private List<float> _fpssamples;
        private float _lastFpsSampleTime;
        private float _lastFpsReportTime;
        private Coroutine _fpsTrackingCoroutine;

        // Network tracking
        private NetworkReachability _lastNetworkReachability;
        private float _lastNetworkCheckTime;
        private const float NETWORK_CHECK_INTERVAL = 5f; // Check every 5 seconds

        public UnityFeatures(
            LoopKitConfig config,
            ILogger logger,
            IEventTracker eventTracker,
            ISessionManager sessionManager,
            IQueueManager queueManager
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventTracker = eventTracker ?? throw new ArgumentNullException(nameof(eventTracker));
            _sessionManager =
                sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));

            _previousSceneName = SceneManager.GetActiveScene().name;
            _fpssamples = new List<float>();
            _lastFpsSampleTime = Time.unscaledTime;
            _lastFpsReportTime = Time.unscaledTime;

            // Initialize network tracking
            _lastNetworkReachability = Application.internetReachability;
            _lastNetworkCheckTime = Time.unscaledTime;
        }

        /// <summary>
        /// Setup all Unity features
        /// </summary>
        public void SetupFeatures()
        {
            if (_isSetup)
            {
                _logger.Debug("Unity features already setup, skipping");
                return;
            }

            try
            {
                if (_config.enableSceneTracking)
                {
                    SetupSceneTracking();
                }

                if (_config.enableErrorTracking)
                {
                    SetupErrorTracking();
                }

                SetupApplicationLifecycleTracking();

                if (_config.enableFpsTracking)
                {
                    SetupFpsTracking();
                }

                if (_config.enableMemoryTracking)
                {
                    SetupMemoryTracking();
                }

                if (_config.enableNetworkTracking)
                {
                    SetupNetworkTracking();
                }

                _isSetup = true;
                _logger.Info("Unity features setup completed");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup Unity features", ex);
            }
        }

        /// <summary>
        /// Setup scene tracking
        /// </summary>
        public void SetupSceneTracking()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;

                // Track initial scene as loaded
                var currentScene = SceneManager.GetActiveScene();
                TrackSceneEvent("scene_loaded", currentScene);

                _logger.Debug("Scene tracking enabled");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup scene tracking", ex);
            }
        }

        /// <summary>
        /// Setup error tracking
        /// </summary>
        public void SetupErrorTracking()
        {
            try
            {
                Application.logMessageReceived += OnLogMessageReceived;

                _logger.Debug("Error tracking enabled");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup error tracking", ex);
            }
        }

        /// <summary>
        /// Setup application lifecycle tracking
        /// </summary>
        public void SetupApplicationLifecycleTracking()
        {
            try
            {
                Application.focusChanged += OnApplicationFocusChanged;
                Application.quitting += OnApplicationQuitting;

                _logger.Debug("Application lifecycle tracking enabled");

                // Track application start
                TrackApplicationEvent(
                    "application_start",
                    new Dictionary<string, object>
                    {
                        ["platform"] = Application.platform.ToString(),
                        ["version"] = Application.version,
                        ["unity_version"] = Application.unityVersion,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup application lifecycle tracking", ex);
            }
        }

        /// <summary>
        /// Setup FPS tracking
        /// </summary>
        public void SetupFpsTracking()
        {
            try
            {
                if (_fpsTrackingCoroutine != null)
                {
                    return; // Already started
                }

                // Start FPS tracking coroutine on a MonoBehaviour
                var gameObject = new GameObject("LoopKit_FPSTracker");
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);

                var fpsTracker = gameObject.AddComponent<FpsTracker>();
                fpsTracker.Initialize(this);

                _logger.Debug("FPS tracking enabled");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup FPS tracking", ex);
            }
        }

        /// <summary>
        /// Setup memory warning tracking
        /// </summary>
        public void SetupMemoryTracking()
        {
            try
            {
                Application.lowMemory += OnLowMemory;

                _logger.Debug("Memory tracking enabled");

                // Track initial memory status
                TrackMemoryEvent(
                    "memory_status",
                    new Dictionary<string, object>
                    {
                        ["system_memory_mb"] = UnityEngine.SystemInfo.systemMemorySize,
                        ["graphics_memory_mb"] = UnityEngine.SystemInfo.graphicsMemorySize,
                        ["available_memory_mb"] = GetAvailableMemory(),
                        ["device_model"] = UnityEngine.SystemInfo.deviceModel,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup memory tracking", ex);
            }
        }

        /// <summary>
        /// Setup network connectivity tracking
        /// </summary>
        public void SetupNetworkTracking()
        {
            try
            {
                // Track initial network status
                TrackNetworkEvent("network_status", Application.internetReachability);

                _logger.Debug("Network tracking enabled");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to setup network tracking", ex);
            }
        }

        /// <summary>
        /// Sample current FPS
        /// </summary>
        public void SampleFps()
        {
            var currentTime = Time.unscaledTime;

            // Check if it's time to sample
            if (currentTime - _lastFpsSampleTime >= _config.fpsSampleInterval)
            {
                var fps = 1f / Time.unscaledDeltaTime;
                _fpssamples.Add(fps);
                _lastFpsSampleTime = currentTime;

                // Check if it's time to report
                if (currentTime - _lastFpsReportTime >= _config.fpsReportInterval)
                {
                    ReportFpsData();
                    _lastFpsReportTime = currentTime;
                }
            }

            // Check network connectivity periodically
            if (
                _config.enableNetworkTracking
                && currentTime - _lastNetworkCheckTime >= NETWORK_CHECK_INTERVAL
            )
            {
                CheckNetworkConnectivity();
                _lastNetworkCheckTime = currentTime;
            }
        }

        /// <summary>
        /// Report collected FPS data
        /// </summary>
        private void ReportFpsData()
        {
            if (_fpssamples.Count == 0)
            {
                return;
            }

            try
            {
                // Calculate FPS statistics
                var sum = 0f;
                var min = float.MaxValue;
                var max = float.MinValue;

                foreach (var fps in _fpssamples)
                {
                    sum += fps;
                    if (fps < min)
                        min = fps;
                    if (fps > max)
                        max = fps;
                }

                var avg = sum / _fpssamples.Count;

                // Calculate median
                var sortedFps = new List<float>(_fpssamples);
                sortedFps.Sort();
                var median =
                    sortedFps.Count % 2 == 0
                        ? (sortedFps[sortedFps.Count / 2 - 1] + sortedFps[sortedFps.Count / 2]) / 2f
                        : sortedFps[sortedFps.Count / 2];

                // Count low FPS samples (below 30 FPS)
                var lowFpsCount = 0;
                foreach (var fps in _fpssamples)
                {
                    if (fps < 30f)
                        lowFpsCount++;
                }

                var lowFpsPercentage = (lowFpsCount / (float)_fpssamples.Count) * 100f;

                var properties = new Dictionary<string, object>
                {
                    ["fps_avg"] = Math.Round(avg, 2),
                    ["fps_min"] = Math.Round(min, 2),
                    ["fps_max"] = Math.Round(max, 2),
                    ["fps_median"] = Math.Round(median, 2),
                    ["fps_samples_count"] = _fpssamples.Count,
                    ["fps_low_percentage"] = Math.Round(lowFpsPercentage, 2),
                    ["sample_duration"] = _config.fpsReportInterval,
                    ["scene_name"] = SceneManager.GetActiveScene().name,
                    ["platform"] = Application.platform.ToString(),
                };

                _eventTracker.Track("fps_report", properties, null, null);

                _logger.Debug(
                    $"FPS report: avg={avg:F1}, min={min:F1}, max={max:F1}, samples={_fpssamples.Count}"
                );

                // Clear samples for next report
                _fpssamples.Clear();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to report FPS data", ex);
            }
        }

        /// <summary>
        /// Check for network connectivity changes
        /// </summary>
        private void CheckNetworkConnectivity()
        {
            try
            {
                var currentReachability = Application.internetReachability;

                if (currentReachability != _lastNetworkReachability)
                {
                    // Network status changed
                    var eventName =
                        currentReachability == NetworkReachability.NotReachable
                            ? "network_connection_lost"
                            : "network_connection_restored";

                    TrackNetworkEvent(eventName, currentReachability);

                    _lastNetworkReachability = currentReachability;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to check network connectivity", ex);
            }
        }

        /// <summary>
        /// Handle low memory warning
        /// </summary>
        private void OnLowMemory()
        {
            try
            {
                TrackMemoryEvent(
                    "low_memory_warning",
                    new Dictionary<string, object>
                    {
                        ["system_memory_mb"] = UnityEngine.SystemInfo.systemMemorySize,
                        ["graphics_memory_mb"] = UnityEngine.SystemInfo.graphicsMemorySize,
                        ["available_memory_mb"] = GetAvailableMemory(),
                        ["scene_name"] = SceneManager.GetActiveScene().name,
                    }
                );

                _logger.Warn("Low memory warning detected");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to track low memory warning", ex);
            }
        }

        /// <summary>
        /// Get available memory estimate (Unity doesn't provide direct access)
        /// </summary>
        private long GetAvailableMemory()
        {
            try
            {
                // Unity doesn't provide direct access to available memory
                // We can use GC.GetTotalMemory as a rough estimate of managed memory usage
                var managedMemoryBytes = System.GC.GetTotalMemory(false);
                var managedMemoryMB = managedMemoryBytes / (1024 * 1024);

                // Return system memory minus our estimated usage
                var systemMemoryMB = UnityEngine.SystemInfo.systemMemorySize;
                return Math.Max(0, systemMemoryMB - managedMemoryMB);
            }
            catch
            {
                return -1; // Unknown
            }
        }

        /// <summary>
        /// Track memory-related events
        /// </summary>
        private void TrackMemoryEvent(string eventName, Dictionary<string, object> properties)
        {
            try
            {
                var eventProperties = properties ?? new Dictionary<string, object>();

                // Add common memory context
                if (!eventProperties.ContainsKey("system_memory_mb"))
                {
                    eventProperties["system_memory_mb"] = UnityEngine.SystemInfo.systemMemorySize;
                }
                if (!eventProperties.ContainsKey("graphics_memory_mb"))
                {
                    eventProperties["graphics_memory_mb"] = UnityEngine
                        .SystemInfo
                        .graphicsMemorySize;
                }
                if (!eventProperties.ContainsKey("platform"))
                {
                    eventProperties["platform"] = Application.platform.ToString();
                }

                _eventTracker.Track(eventName, eventProperties, null, null);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to track memory event: {eventName}", ex);
            }
        }

        /// <summary>
        /// Track network-related events
        /// </summary>
        private void TrackNetworkEvent(string eventName, NetworkReachability reachability)
        {
            try
            {
                var properties = new Dictionary<string, object>
                {
                    ["network_reachability"] = reachability.ToString(),
                    ["is_connected"] = reachability != NetworkReachability.NotReachable,
                    ["connection_type"] = GetConnectionType(reachability),
                    ["platform"] = Application.platform.ToString(),
                    ["scene_name"] = SceneManager.GetActiveScene().name,
                };

                _eventTracker.Track(eventName, properties, null, null);

                _logger.Debug($"Network event: {eventName} ({reachability})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to track network event: {eventName}", ex);
            }
        }

        /// <summary>
        /// Get human-readable connection type
        /// </summary>
        private string GetConnectionType(NetworkReachability reachability)
        {
            switch (reachability)
            {
                case NetworkReachability.NotReachable:
                    return "none";
                case NetworkReachability.ReachableViaCarrierDataNetwork:
                    return "cellular";
                case NetworkReachability.ReachableViaLocalAreaNetwork:
                    return "wifi";
                default:
                    return "unknown";
            }
        }

        /// <summary>
        /// Set network manager reference
        /// </summary>
        public void SetNetworkManager(INetworkManager networkManager)
        {
            _networkManager = networkManager;
            _logger.Debug("Network manager set on Unity features");
        }

        /// <summary>
        /// Update Unity features configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            _logger.Debug("Unity features configuration updated");

            // Re-setup features if configuration changed
            if (_isSetup)
            {
                _isSetup = false;
                SetupFeatures();
            }
        }

        /// <summary>
        /// Handle scene loaded event
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                TrackSceneEvent(
                    "scene_loaded",
                    scene,
                    new Dictionary<string, object>
                    {
                        ["load_mode"] = mode.ToString(),
                        ["previous_scene"] = _previousSceneName,
                    }
                );

                _previousSceneName = scene.name;

                // Update session activity
                _sessionManager.UpdateActivity();
            }
            catch (Exception ex)
            {
                _logger.Error("Error handling scene loaded event", ex);
            }
        }

        /// <summary>
        /// Handle scene unloaded event
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            try
            {
                TrackSceneEvent("scene_unloaded", scene);
            }
            catch (Exception ex)
            {
                _logger.Error("Error handling scene unloaded event", ex);
            }
        }

        /// <summary>
        /// Handle application focus changed
        /// </summary>
        private void OnApplicationFocusChanged(bool hasFocus)
        {
            try
            {
                var eventName = hasFocus ? "application_focus_gained" : "application_focus_lost";

                TrackApplicationEvent(
                    eventName,
                    new Dictionary<string, object> { ["has_focus"] = hasFocus }
                );

                // Update session activity when app gains focus
                if (hasFocus)
                {
                    _sessionManager.UpdateActivity();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error handling application focus change", ex);
            }
        }

        /// <summary>
        /// Handle application quitting
        /// </summary>
        private void OnApplicationQuitting()
        {
            try
            {
                _logger.Info("Application quitting, flushing events");

                TrackApplicationEvent("application_quit");

                // End current session
                _sessionManager.EndSession();

                // Try to flush remaining events synchronously
                if (_networkManager != null && _queueManager.GetQueueSize() > 0)
                {
                    try
                    {
                        // Note: This is a best-effort attempt as Unity is quitting
                        _ = _queueManager.FlushAsync(_networkManager);
                    }
                    catch (Exception flushEx)
                    {
                        _logger.Error("Failed to flush events on quit", flushEx);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error handling application quit", ex);
            }
        }

        /// <summary>
        /// Handle log messages for error tracking
        /// </summary>
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            try
            {
                // Only track errors and exceptions
                if (type != LogType.Error && type != LogType.Exception)
                {
                    return;
                }

                // Don't track LoopKit's own log messages
                if (logString.Contains("[LoopKit]"))
                {
                    return;
                }

                var properties = new Dictionary<string, object>
                {
                    ["message"] = logString,
                    ["stack_trace"] = stackTrace,
                    ["log_type"] = type.ToString(),
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ["scene"] = SceneManager.GetActiveScene().name,
                };

                _eventTracker.Track("error", properties, null, null);

                _logger.Debug($"Tracked error: {type} - {logString}");
            }
            catch (Exception ex)
            {
                // Be careful not to create infinite loop
                Debug.LogError($"[LoopKit] Error in error tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Track scene-related events
        /// </summary>
        private void TrackSceneEvent(
            string eventName,
            Scene scene,
            Dictionary<string, object> additionalProperties = null
        )
        {
            try
            {
                var properties = new Dictionary<string, object>
                {
                    ["scene_name"] = scene.name,
                    ["scene_build_index"] = scene.buildIndex,
                    ["scene_path"] = scene.path,
                    ["scene_is_loaded"] = scene.isLoaded,
                    ["scene_is_valid"] = scene.IsValid(),
                };

                // Add additional properties if provided
                if (additionalProperties != null)
                {
                    foreach (var kvp in additionalProperties)
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }

                _eventTracker.Track(eventName, properties, null, null);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to track scene event: {eventName}", ex);
            }
        }

        /// <summary>
        /// Track application-related events
        /// </summary>
        private void TrackApplicationEvent(
            string eventName,
            Dictionary<string, object> properties = null
        )
        {
            try
            {
                var eventProperties = properties ?? new Dictionary<string, object>();

                // Add common application context
                eventProperties["application_version"] = Application.version;
                eventProperties["unity_version"] = Application.unityVersion;
                eventProperties["platform"] = Application.platform.ToString();
                eventProperties["is_editor"] = Application.isEditor;

                _eventTracker.Track(eventName, eventProperties, null, null);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to track application event: {eventName}", ex);
            }
        }

        /// <summary>
        /// Cleanup Unity features
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (_config.enableSceneTracking)
                {
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    SceneManager.sceneUnloaded -= OnSceneUnloaded;
                }

                if (_config.enableErrorTracking)
                {
                    Application.logMessageReceived -= OnLogMessageReceived;
                }

                if (_config.enableMemoryTracking)
                {
                    Application.lowMemory -= OnLowMemory;
                }

                Application.focusChanged -= OnApplicationFocusChanged;
                Application.quitting -= OnApplicationQuitting;

                _isSetup = false;
                _logger.Debug("Unity features cleaned up");
            }
            catch (Exception ex)
            {
                _logger.Error("Error during Unity features cleanup", ex);
            }
        }
    }

    /// <summary>
    /// MonoBehaviour component for FPS tracking
    /// Handles Update loop for FPS sampling
    /// </summary>
    internal class FpsTracker : MonoBehaviour
    {
        private UnityFeatures _unityFeatures;

        public void Initialize(UnityFeatures unityFeatures)
        {
            _unityFeatures = unityFeatures;
        }

        private void Update()
        {
            if (_unityFeatures != null)
            {
                _unityFeatures.SampleFps();
            }
        }

        private void OnDestroy()
        {
            _unityFeatures = null;
        }
    }
}

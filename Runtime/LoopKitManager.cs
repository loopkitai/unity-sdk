using System.Collections.Generic;
using UnityEngine;

namespace LoopKit
{
    /// <summary>
    /// LoopKit Manager component for easy setup and configuration
    /// Drop this prefab into your scene to automatically initialize LoopKit
    /// </summary>
    public class LoopKitManager : MonoBehaviour
    {
        [Header("API Configuration")]
        [SerializeField]
        [Tooltip("Your LoopKit API key - get this from your LoopKit dashboard")]
        private string apiKey = "";

        [Header("SDK Configuration")]
        [SerializeField]
        private LoopKitConfig config = new LoopKitConfig();

        [Header("Auto-Initialize")]
        [SerializeField]
        [Tooltip("Automatically initialize LoopKit on Awake")]
        private bool autoInitialize = true;

        [Header("Singleton Behavior")]
        [SerializeField]
        [Tooltip("Don't destroy this GameObject when loading new scenes")]
        private bool dontDestroyOnLoad = true;

        // Singleton instance
        private static LoopKitManager _instance;
        public static LoopKitManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = FindFirstObjectByType<LoopKitManager>();
#else
                    _instance = FindObjectOfType<LoopKitManager>();
#endif
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get the configured LoopKit instance
        /// </summary>
        public ILoopKit SDK => LoopKit.Instance;

        /// <summary>
        /// Check if LoopKit is properly configured
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        private void Awake()
        {
            // Ensure singleton behavior
            if (_instance == null)
            {
                _instance = this;

                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }

                if (autoInitialize)
                {
                    InitializeLoopKit();
                }
            }
            else if (_instance != this)
            {
                Debug.LogWarning(
                    "[LoopKit] Multiple LoopKitManager instances found. Destroying duplicate."
                );
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize LoopKit with the configured settings
        /// </summary>
        public void InitializeLoopKit()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError(
                    "[LoopKit] API key is not set! Please configure your API key in the LoopKitManager component."
                );
                return;
            }

            try
            {
                // Set the API key in config
                config.apiKey = apiKey;

                // Initialize LoopKit
                LoopKit.Instance.Init(apiKey, config);

                if (config.debug)
                {
                    Debug.Log("[LoopKit] Successfully initialized from LoopKitManager");
                }

                // Track initialization event
                Track(
                    "loopkit_initialized",
                    new Dictionary<string, object>
                    {
                        ["auto_initialize"] = autoInitialize,
                        ["scene"] = gameObject.scene.name,
                        ["platform"] = Application.platform.ToString(),
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoopKit] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Convenient method to track events through the manager
        /// </summary>
        public void Track(string eventName, Dictionary<string, object> properties = null)
        {
            if (IsConfigured)
            {
                LoopKitAPI.Track(eventName, properties);
            }
            else
            {
                if (config.debug)
                {
                    Debug.LogWarning("[LoopKit] Cannot track event - LoopKit not configured!");
                }
            }
        }

        /// <summary>
        /// Convenient method to identify users through the manager
        /// </summary>
        public void Identify(string userId, Dictionary<string, object> properties = null)
        {
            if (IsConfigured)
            {
                LoopKitAPI.Identify(userId, properties);
            }
            else
            {
                if (config.debug)
                {
                    Debug.LogWarning("[LoopKit] Cannot identify user - LoopKit not configured!");
                }
            }
        }

        /// <summary>
        /// Convenient method to group users through the manager
        /// </summary>
        public void Group(
            string groupId,
            Dictionary<string, object> properties = null,
            string groupType = "organization"
        )
        {
            if (IsConfigured)
            {
                LoopKitAPI.Group(groupId, properties, groupType);
            }
            else
            {
                if (config.debug)
                {
                    Debug.LogWarning("[LoopKit] Cannot group user - LoopKit not configured!");
                }
            }
        }

        /// <summary>
        /// Update configuration at runtime
        /// </summary>
        public void UpdateConfiguration(LoopKitConfig newConfig)
        {
            config = newConfig;
            config.apiKey = apiKey; // Preserve API key

            if (IsConfigured)
            {
                LoopKit.Instance.Configure(config);
                if (config.debug)
                {
                    Debug.Log("[LoopKit] Configuration updated");
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (IsConfigured)
            {
                if (pauseStatus)
                {
                    Track("app_paused");
                }
                else
                {
                    Track("app_resumed");
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (IsConfigured && !hasFocus)
            {
                // Flush events when losing focus
                _ = LoopKitAPI.FlushAsync();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validate configuration in editor
        /// </summary>
        private void OnValidate()
        {
            // Ensure config is not null
            if (config == null)
            {
                config = new LoopKitConfig();
            }

            // Validate API key format
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (apiKey.Length < 8)
                {
                    Debug.LogWarning(
                        "[LoopKit] API key seems too short. Please check your API key."
                    );
                }
                else if (
                    apiKey.ToLower().Contains("your-api-key")
                    || apiKey.ToLower().Contains("placeholder")
                )
                {
                    Debug.LogWarning(
                        "[LoopKit] Please replace the placeholder with your actual API key."
                    );
                }
            }
        }
#endif
    }
}

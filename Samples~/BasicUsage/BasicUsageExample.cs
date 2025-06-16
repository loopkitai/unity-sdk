using System.Collections.Generic;
using LoopKit;
using UnityEngine;

/*
    The provided LoopKitManager component is a singleton that can be used to track events and handles
    the initialization and configuration of the SDK for you. This example demonstrates how to
    initialize it directly though if you want to build your own manager.
    
    Learn more here: https://docs.loopkit.ai/docs/unity-quickstart.html
*/

namespace LoopKit.Samples
{
    /// <summary>
    /// Basic usage example for LoopKit Unity SDK
    /// Demonstrates core functionality including initialization, tracking, identification, and grouping
    /// </summary>
    public class BasicUsageExample : MonoBehaviour
    {
        [Header("LoopKit Configuration")]
        [SerializeField]
        private string apiKey = "your-api-key-here";

        [Header("Demo Settings")]
        [SerializeField]
        private bool enableDebugLogging = true;

        [SerializeField]
        private int batchSize = 10;

        [SerializeField]
        private float flushInterval = 5f;

        private void Start()
        {
            InitializeLoopKit();
            DemonstrateBasicUsage();
        }

        /// <summary>
        /// Initialize LoopKit with configuration
        /// </summary>
        private void InitializeLoopKit()
        {
            var config = new LoopKitConfig
            {
                debug = enableDebugLogging,
                batchSize = batchSize,
                flushInterval = flushInterval,
                enableSessionTracking = true,
                enableSceneTracking = true,
                enableErrorTracking = true,
            };

            // Initialize using static API
            LoopKitAPI.Init(apiKey, config);

            Debug.Log("[BasicUsageExample] LoopKit initialized successfully!");
        }

        /// <summary>
        /// Demonstrate basic SDK usage
        /// </summary>
        private void DemonstrateBasicUsage()
        {
            // Track a simple event
            LoopKitAPI.Track(
                "game_started",
                new Dictionary<string, object>
                {
                    ["level"] = 1,
                    ["difficulty"] = "normal",
                    ["timestamp"] = System.DateTime.UtcNow.ToString(),
                }
            );

            // Identify a user
            LoopKitAPI.Identify(
                "player_123",
                new Dictionary<string, object>
                {
                    ["name"] = "Demo Player",
                    ["level"] = 5,
                    ["score"] = 1250,
                    ["premium"] = false,
                }
            );

            // Associate user with a group (e.g., guild, team)
            LoopKitAPI.Group(
                "guild_456",
                new Dictionary<string, object>
                {
                    ["guild_name"] = "Demo Guild",
                    ["member_count"] = 25,
                    ["guild_level"] = 3,
                },
                "guild"
            );

            Debug.Log("[BasicUsageExample] Demo events tracked!");
        }

        /// <summary>
        /// Example: Track player action
        /// </summary>
        public void OnPlayerAction(string action, Dictionary<string, object> properties = null)
        {
            var eventProperties = properties ?? new Dictionary<string, object>();
            eventProperties["action_time"] = Time.time;
            eventProperties["scene"] = UnityEngine
                .SceneManagement.SceneManager.GetActiveScene()
                .name;

            LoopKitAPI.Track($"player_{action}", eventProperties);
        }

        /// <summary>
        /// Example: Track button click
        /// </summary>
        public void OnButtonClick(string buttonName)
        {
            LoopKitAPI.Track(
                "button_clicked",
                new Dictionary<string, object>
                {
                    ["button_name"] = buttonName,
                    ["screen"] = gameObject.scene.name,
                    ["timestamp"] = System.DateTime.UtcNow.ToString(),
                }
            );
        }

        /// <summary>
        /// Example: Track level completion
        /// </summary>
        public void OnLevelCompleted(int level, float completionTime, int score)
        {
            LoopKitAPI.Track(
                "level_completed",
                new Dictionary<string, object>
                {
                    ["level"] = level,
                    ["completion_time"] = completionTime,
                    ["score"] = score,
                    ["stars"] =
                        score > 1000 ? 3
                        : score > 500 ? 2
                        : 1,
                }
            );
        }

        /// <summary>
        /// Example: Track purchase
        /// </summary>
        public void OnPurchase(string itemId, string itemName, float price, string currency = "USD")
        {
            LoopKitAPI.Track(
                "purchase",
                new Dictionary<string, object>
                {
                    ["item_id"] = itemId,
                    ["item_name"] = itemName,
                    ["price"] = price,
                    ["currency"] = currency,
                    ["purchase_time"] = System.DateTime.UtcNow.ToString(),
                }
            );
        }

        /// <summary>
        /// Example: Manual flush for important events
        /// </summary>
        public async void FlushEvents()
        {
            try
            {
                await LoopKitAPI.FlushAsync();
                Debug.Log("[BasicUsageExample] Events flushed successfully!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BasicUsageExample] Failed to flush events: {ex.Message}");
            }
        }

        /// <summary>
        /// Example: Check queue status
        /// </summary>
        public void LogQueueStatus()
        {
            var queueSize = LoopKitAPI.GetQueueSize();
            Debug.Log($"[BasicUsageExample] Current queue size: {queueSize} events");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // Track when app is paused/backgrounded
                LoopKitAPI.Track("app_paused");
            }
            else
            {
                // Track when app is resumed
                LoopKitAPI.Track("app_resumed");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                // Flush events when losing focus to ensure data is sent
                _ = LoopKitAPI.FlushAsync();
            }
        }
    }
}

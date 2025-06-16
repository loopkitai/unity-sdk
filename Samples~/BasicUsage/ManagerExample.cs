using System.Collections.Generic;
using UnityEngine;

namespace LoopKit.Samples
{
    /// <summary>
    /// Example showing how to use LoopKitManager for easy setup
    /// Demonstrates integration with the prefab-based approach
    /// </summary>
    public class ManagerExample : MonoBehaviour
    {
        [Header("Demo Settings")]
        [SerializeField]
        private bool trackOnStart = true;

        private void Start()
        {
            if (trackOnStart)
            {
                DemonstrateManagerUsage();
            }
        }

        /// <summary>
        /// Demonstrate using LoopKit through the manager
        /// </summary>
        private void DemonstrateManagerUsage()
        {
            // The LoopKitManager prefab in the scene handles initialization automatically
            // We can access it through the singleton instance

            var manager = LoopKitManager.Instance;
            if (manager == null)
            {
                Debug.LogError(
                    "[ManagerExample] LoopKitManager not found! Please add the LoopKitManager prefab to your scene."
                );
                return;
            }

            if (!manager.IsConfigured)
            {
                Debug.LogWarning(
                    "[ManagerExample] LoopKit is not configured. Please set your API key in the LoopKitManager component."
                );
                return;
            }

            // Track events through the manager (recommended approach)
            manager.Track(
                "manager_example_started",
                new Dictionary<string, object>
                {
                    ["demo_mode"] = true,
                    ["unity_version"] = Application.unityVersion,
                    ["platform"] = Application.platform.ToString(),
                }
            );

            // Or use the static API (works the same way)
            LoopKitAPI.Track(
                "static_api_example",
                new Dictionary<string, object>
                {
                    ["method"] = "static_api",
                    ["scene"] = gameObject.scene.name,
                }
            );

            Debug.Log("[ManagerExample] Demo events tracked successfully!");
        }

        /// <summary>
        /// Example: Track player login through manager
        /// Call this when a player logs in
        /// </summary>
        public void OnPlayerLogin(string playerId, string playerName)
        {
            var manager = LoopKitManager.Instance;
            if (manager != null && manager.IsConfigured)
            {
                // Identify the user
                manager.Identify(
                    playerId,
                    new Dictionary<string, object>
                    {
                        ["name"] = playerName,
                        ["login_time"] = System.DateTime.UtcNow.ToString(),
                        ["session_type"] = "new_login",
                    }
                );

                // Track login event
                manager.Track(
                    "player_login",
                    new Dictionary<string, object>
                    {
                        ["player_id"] = playerId,
                        ["player_name"] = playerName,
                        ["login_method"] = "demo",
                    }
                );

                Debug.Log($"[ManagerExample] Player {playerName} logged in and tracked");
            }
        }

        /// <summary>
        /// Example: Track game event through manager
        /// </summary>
        public void OnGameEvent(string eventName, Dictionary<string, object> properties = null)
        {
            var manager = LoopKitManager.Instance;
            if (manager != null)
            {
                var eventProperties = properties ?? new Dictionary<string, object>();
                eventProperties["triggered_from"] = "manager_example";
                eventProperties["game_time"] = Time.time;

                manager.Track(eventName, eventProperties);
            }
        }

        /// <summary>
        /// Example: Handle level completion
        /// </summary>
        public void OnLevelComplete(int level, float completionTime, int score)
        {
            var manager = LoopKitManager.Instance;
            if (manager != null)
            {
                manager.Track(
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
                        ["difficulty"] = "normal",
                    }
                );
            }
        }

        /// <summary>
        /// Example: Handle in-app purchase
        /// </summary>
        public void OnPurchase(string productId, float price, string currency = "USD")
        {
            var manager = LoopKitManager.Instance;
            if (manager != null)
            {
                manager.Track(
                    "purchase",
                    new Dictionary<string, object>
                    {
                        ["product_id"] = productId,
                        ["price"] = price,
                        ["currency"] = currency,
                        ["purchase_method"] = "in_app",
                        ["timestamp"] = System.DateTime.UtcNow.ToString(),
                    }
                );
            }
        }

        /// <summary>
        /// Button handler for UI testing
        /// </summary>
        public void OnTestButtonClick()
        {
            OnGameEvent(
                "test_button_clicked",
                new Dictionary<string, object>
                {
                    ["button_name"] = "test_button",
                    ["click_count"] = 1,
                }
            );
        }

        private void OnGUI()
        {
            // Simple test UI for demonstration
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("LoopKit Manager Example", GUI.skin.box);

            var manager = LoopKitManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("❌ LoopKitManager not found!");
                GUILayout.Label("Add LoopKitManager prefab to scene");
            }
            else if (!manager.IsConfigured)
            {
                GUILayout.Label("⚠️ API Key not configured!");
                GUILayout.Label("Set API key in LoopKitManager");
            }
            else
            {
                GUILayout.Label("✅ LoopKit ready!");

                if (GUILayout.Button("Track Test Event"))
                {
                    OnTestButtonClick();
                }

                if (GUILayout.Button("Simulate Login"))
                {
                    OnPlayerLogin("demo_player_123", "Demo Player");
                }

                if (GUILayout.Button("Simulate Level Complete"))
                {
                    OnLevelComplete(
                        Random.Range(1, 10),
                        Random.Range(30f, 120f),
                        Random.Range(100, 2000)
                    );
                }
            }

            GUILayout.EndArea();
        }
    }
}

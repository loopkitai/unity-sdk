using UnityEditor;
using UnityEngine;

namespace LoopKit.Editor
{
    /// <summary>
    /// Custom editor for LoopKitManager to provide better configuration UX
    /// </summary>
    [CustomEditor(typeof(LoopKitManager))]
    public class LoopKitManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty apiKeyProperty;
        private SerializedProperty configProperty;
        private SerializedProperty autoInitializeProperty;
        private SerializedProperty dontDestroyOnLoadProperty;

        private bool showAdvancedConfig = false;

        private void OnEnable()
        {
            apiKeyProperty = serializedObject.FindProperty("apiKey");
            configProperty = serializedObject.FindProperty("config");
            autoInitializeProperty = serializedObject.FindProperty("autoInitialize");
            dontDestroyOnLoadProperty = serializedObject.FindProperty("dontDestroyOnLoad");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (LoopKitManager)target;

            // Header
            EditorGUILayout.Space();
            GUILayout.Label("LoopKit Unity SDK", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            // Welcome message (replaces old help box)
            EditorGUILayout.LabelField("loopkit.ai", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Welcome and thank you for choosing LoopKit! This Unity SDK makes it easy to integrate analytics and event tracking into your game. Enter your API key below to get started.",
                MessageType.Info
            );
            EditorGUILayout.Space();

            // API Key Section
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // API Key field with validation
            EditorGUILayout.PropertyField(
                apiKeyProperty,
                new GUIContent("API Key", "Your LoopKit API key from the dashboard")
            );

            // API Key validation feedback
            if (string.IsNullOrEmpty(apiKeyProperty.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "⚠️ API Key is required! Get yours from the LoopKit dashboard.",
                    MessageType.Warning
                );
            }
            else if (apiKeyProperty.stringValue.Length < 8)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ API Key seems too short. Please check your key.",
                    MessageType.Warning
                );
            }
            else if (
                apiKeyProperty.stringValue.ToLower().Contains("your-api-key")
                || apiKeyProperty.stringValue.ToLower().Contains("placeholder")
            )
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Please replace with your actual API key.",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox("✅ API Key configured!", MessageType.Info);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Basic Settings
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                autoInitializeProperty,
                new GUIContent(
                    "Auto Initialize",
                    "Initialize LoopKit automatically when this GameObject awakens"
                )
            );
            EditorGUILayout.PropertyField(
                dontDestroyOnLoadProperty,
                new GUIContent(
                    "Persist Across Scenes",
                    "Keep this GameObject alive when loading new scenes"
                )
            );

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Advanced Configuration
            showAdvancedConfig = EditorGUILayout.Foldout(
                showAdvancedConfig,
                "Advanced Configuration",
                true,
                EditorStyles.foldoutHeader
            );

            if (showAdvancedConfig)
            {
                EditorGUI.indentLevel++;

                // Config fields
                var config = configProperty;
                if (config != null)
                {
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("debug"),
                        new GUIContent("Debug Mode")
                    );
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("batchSize"),
                        new GUIContent("Batch Size")
                    );
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("flushInterval"),
                        new GUIContent("Flush Interval (s)")
                    );
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("enableSessionTracking"),
                        new GUIContent("Session Tracking")
                    );
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("enableSceneTracking"),
                        new GUIContent("Scene Tracking")
                    );
                    EditorGUILayout.PropertyField(
                        config.FindPropertyRelative("enableErrorTracking"),
                        new GUIContent("Error Tracking")
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Runtime Status
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                GUI.enabled = false;
                EditorGUILayout.Toggle("Is Configured", manager.IsConfigured);
                if (manager.IsConfigured)
                {
                    EditorGUILayout.IntField("Queue Size", LoopKitAPI.GetQueueSize());
                }
                GUI.enabled = true;

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();

                // Runtime Actions
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                if (manager.IsConfigured)
                {
                    if (GUILayout.Button("Flush Events Now"))
                    {
                        _ = LoopKitAPI.FlushAsync();
                        Debug.Log("[LoopKit] Manual flush triggered from editor");
                    }

                    if (GUILayout.Button("Test Track Event"))
                    {
                        manager.Track(
                            "editor_test_event",
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                ["source"] = "editor",
                                ["timestamp"] = System.DateTime.UtcNow.ToString(),
                            }
                        );
                        Debug.Log("[LoopKit] Test event tracked from editor");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Configure API key to enable runtime actions",
                        MessageType.Info
                    );
                }

                EditorGUI.indentLevel--;
            }

            // Quick Setup Buttons
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🔗 Get API Key"))
            {
                Application.OpenURL("https://app.loopkit.ai");
            }

            if (GUILayout.Button("📖 Documentation"))
            {
                Application.OpenURL("https://docs.loopkit.ai/docs/unity-quickstart.html");
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}

using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using LoopKitSDK = LoopKit.LoopKit;

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

        // Version checking
        private bool hasCheckedVersion = false;
        private bool isNewerVersionAvailable = false;
        private string latestVersion = "";
        private bool versionCheckFailed = false;
        private bool enableUpgradeDebugLog = true; // Default enabled

        [System.Serializable]
        private class SdkVersionsResponse
        {
            public string unity;
        }

        private void OnEnable()
        {
            apiKeyProperty = serializedObject.FindProperty("apiKey");
            configProperty = serializedObject.FindProperty("config");
            autoInitializeProperty = serializedObject.FindProperty("autoInitialize");
            dontDestroyOnLoadProperty = serializedObject.FindProperty("dontDestroyOnLoad");

            // Check for version updates when editor loads
            if (!hasCheckedVersion)
            {
                CheckForVersionUpdate();
            }
        }

        /// <summary>
        /// Check for SDK version updates
        /// </summary>
        private void CheckForVersionUpdate()
        {
            hasCheckedVersion = true;

            var manager = (LoopKitManager)target;
            if (manager == null)
                return;

            // Get the base URL from the manager's config
            var baseUrl = "https://drain.loopkit.ai/v1"; // Default URL
            if (manager.GetComponent<LoopKitManager>() != null)
            {
                // Try to get the actual configured base URL if available
                try
                {
                    var config = manager
                        .GetType()
                        .GetField(
                            "config",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );
                    if (config != null)
                    {
                        var configValue = config.GetValue(manager) as LoopKitConfig;
                        if (configValue != null && !string.IsNullOrEmpty(configValue.baseURL))
                        {
                            baseUrl = configValue.baseURL;
                        }
                    }
                }
                catch
                {
                    // Use default URL if we can't get the config
                }
            }

            StartVersionCheck(baseUrl);
        }

        /// <summary>
        /// Start the version check coroutine
        /// </summary>
        private void StartVersionCheck(string baseUrl)
        {
            EditorApplication.CallbackFunction coroutineRunner = null;
            var enumerator = CheckVersionCoroutine(baseUrl);

            coroutineRunner = () =>
            {
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        EditorApplication.update -= coroutineRunner;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoopKit] Version check failed: {ex.Message}");
                    versionCheckFailed = true;
                    EditorApplication.update -= coroutineRunner;
                }
            };

            EditorApplication.update += coroutineRunner;
        }

        /// <summary>
        /// Coroutine to check version from API
        /// </summary>
        private IEnumerator CheckVersionCoroutine(string baseUrl)
        {
            var url = $"{baseUrl.TrimEnd('/')}/sdk-versions";

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10; // 10 second timeout

                var operation = request.SendWebRequest();

                // Wait for request to complete
                while (!operation.isDone)
                {
                    yield return null;
                }

                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonUtility.FromJson<SdkVersionsResponse>(
                            request.downloadHandler.text
                        );

                        if (response != null && !string.IsNullOrEmpty(response.unity))
                        {
                            ProcessVersionResponse(response.unity);
                        }
                        else
                        {
                            Debug.LogWarning("[LoopKit] Version check: Invalid response format");
                            versionCheckFailed = true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LoopKit] Version check failed: {request.error}");
                        versionCheckFailed = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoopKit] Version check error: {ex.Message}");
                    versionCheckFailed = true;
                }
            }
        }

        /// <summary>
        /// Process the version response and compare with local version
        /// </summary>
        private void ProcessVersionResponse(string remoteVersion)
        {
            try
            {
                var localVersion = LoopKitSDK.VERSION;

                if (IsVersionNewer(remoteVersion, localVersion))
                {
                    isNewerVersionAvailable = true;
                    latestVersion = remoteVersion;

                    if (enableUpgradeDebugLog)
                    {
                        Debug.Log(
                            $"[LoopKit] A newer version ({remoteVersion}) is available! "
                                + $"You're currently using version {localVersion}. "
                                + $"Update via Package Manager or consider upgrading for the latest features and fixes."
                        );
                    }
                }
                else
                {
                    if (enableUpgradeDebugLog)
                    {
                        Debug.Log($"[LoopKit] You're using the latest version ({localVersion})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoopKit] Version comparison failed: {ex.Message}");
                versionCheckFailed = true;
            }
        }

        /// <summary>
        /// Compare two semantic version strings
        /// </summary>
        private bool IsVersionNewer(string remoteVersion, string localVersion)
        {
            try
            {
                var remote = ParseVersion(remoteVersion);
                var local = ParseVersion(localVersion);

                // Compare major.minor.patch
                if (remote.major > local.major)
                    return true;
                if (remote.major < local.major)
                    return false;

                if (remote.minor > local.minor)
                    return true;
                if (remote.minor < local.minor)
                    return false;

                return remote.patch > local.patch;
            }
            catch
            {
                // If parsing fails, assume newer version is available to be safe
                return true;
            }
        }

        /// <summary>
        /// Parse a semantic version string into components
        /// </summary>
        private (int major, int minor, int patch) ParseVersion(string version)
        {
            // Remove 'v' prefix if present
            version = version.TrimStart('v', 'V');

            var parts = version.Split('.');

            var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;

            return (major, minor, patch);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (LoopKitManager)target;

            // Custom styling
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
            };

            var sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            };

            var upgradeBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 12, 12),
                margin = new RectOffset(0, 0, 8, 8),
            };

            // Header Section
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("🔄", GUILayout.Width(24));
                GUILayout.Label("LoopKit Unity SDK", headerStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"v{LoopKitSDK.VERSION}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);

            // Draw separator line
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));

            EditorGUILayout.Space(8);

            // Prominent Version Update Notice
            if (isNewerVersionAvailable)
            {
                using (new EditorGUILayout.VerticalScope(upgradeBoxStyle))
                {
                    // Update header with icon
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("🚀", GUILayout.Width(20));
                        var updateHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            fontSize = 13,
                            normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
                        };
                        GUILayout.Label("UPDATE AVAILABLE", updateHeaderStyle);
                        GUILayout.FlexibleSpace();
                        var versionStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
                        };
                        GUILayout.Label($"{latestVersion}", versionStyle);
                    }

                    EditorGUILayout.Space(4);

                    // Update description
                    var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        fontSize = 11,
                        normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                    };
                    EditorGUILayout.LabelField(
                        $"A newer version is available! Update via Package Manager to get the latest features and fixes.",
                        descStyle
                    );

                    EditorGUILayout.Space(6);

                    // Update buttons
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var buttonStyle = new GUIStyle(GUI.skin.button)
                        {
                            fontSize = 11,
                            padding = new RectOffset(8, 8, 6, 6),
                        };

                        if (
                            GUILayout.Button(
                                "📦 Open Package Manager",
                                buttonStyle,
                                GUILayout.Height(24)
                            )
                        )
                        {
                            UnityEditor.PackageManager.UI.Window.Open("com.loopkit.sdk");
                        }

                        if (GUILayout.Button("📖 View Changes", buttonStyle, GUILayout.Height(24)))
                        {
                            Application.OpenURL("https://github.com/loopkitai/unity-sdk");
                        }
                    }
                }

                EditorGUILayout.Space(8);
            }

            // Welcome Section
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("💫", GUILayout.Width(20));
                    GUILayout.Label("loopkit.ai", sectionHeaderStyle);
                }

                EditorGUILayout.Space(2);

                var welcomeStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                };
                EditorGUILayout.LabelField(
                    "Welcome! This Unity SDK makes it easy to integrate analytics and event tracking into your game.",
                    welcomeStyle
                );
            }

            EditorGUILayout.Space(10);

            // API Configuration Section
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("🔑", GUILayout.Width(20));
                    GUILayout.Label("API Configuration", sectionHeaderStyle);
                }

                EditorGUILayout.Space(5);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // API Key field
                    EditorGUILayout.PropertyField(
                        apiKeyProperty,
                        new GUIContent("API Key", "Your LoopKit API key from the dashboard")
                    );

                    EditorGUILayout.Space(3);

                    // API Key validation with better styling
                    if (string.IsNullOrEmpty(apiKeyProperty.stringValue))
                    {
                        var warningStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            normal = { textColor = new Color(1f, 0.8f, 0.4f) },
                        };
                        using (new EditorGUILayout.HorizontalScope(warningStyle))
                        {
                            GUILayout.Label("⚠️", GUILayout.Width(16));
                            GUILayout.Label(
                                "API Key is required! Get yours from the LoopKit dashboard.",
                                EditorStyles.wordWrappedMiniLabel
                            );
                        }
                    }
                    else if (apiKeyProperty.stringValue.Length < 8)
                    {
                        var warningStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            normal = { textColor = new Color(1f, 0.8f, 0.4f) },
                        };
                        using (new EditorGUILayout.HorizontalScope(warningStyle))
                        {
                            GUILayout.Label("⚠️", GUILayout.Width(16));
                            GUILayout.Label(
                                "API Key seems too short. Please check your key.",
                                EditorStyles.wordWrappedMiniLabel
                            );
                        }
                    }
                    else if (
                        apiKeyProperty.stringValue.ToLower().Contains("your-api-key")
                        || apiKeyProperty.stringValue.ToLower().Contains("placeholder")
                    )
                    {
                        var warningStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            normal = { textColor = new Color(1f, 0.8f, 0.4f) },
                        };
                        using (new EditorGUILayout.HorizontalScope(warningStyle))
                        {
                            GUILayout.Label("⚠️", GUILayout.Width(16));
                            GUILayout.Label(
                                "Please replace with your actual API key.",
                                EditorStyles.wordWrappedMiniLabel
                            );
                        }
                    }
                    else
                    {
                        var successStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            normal = { textColor = new Color(0.4f, 0.8f, 0.4f) },
                        };
                        using (new EditorGUILayout.HorizontalScope(successStyle))
                        {
                            GUILayout.Label("✅", GUILayout.Width(16));
                            GUILayout.Label(
                                "API Key configured!",
                                EditorStyles.wordWrappedMiniLabel
                            );
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);

            // Basic Settings Section
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("⚙️", GUILayout.Width(20));
                    GUILayout.Label("Basic Settings", sectionHeaderStyle);
                }

                EditorGUILayout.Space(5);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(
                        autoInitializeProperty,
                        new GUIContent(
                            "Auto Initialize",
                            "Initialize LoopKit automatically when this GameObject awakens"
                        )
                    );

                    EditorGUILayout.Space(2);

                    EditorGUILayout.PropertyField(
                        dontDestroyOnLoadProperty,
                        new GUIContent(
                            "Persist Across Scenes",
                            "Keep this GameObject alive when loading new scenes"
                        )
                    );
                }
            }

            EditorGUILayout.Space(10);

            // Advanced Configuration
            var foldoutStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("🔧", GUILayout.Width(20));
                showAdvancedConfig = EditorGUILayout.Foldout(
                    showAdvancedConfig,
                    "Advanced Configuration",
                    true,
                    foldoutStyle
                );
            }

            if (showAdvancedConfig)
            {
                EditorGUILayout.Space(5);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var config = configProperty;
                    if (config != null)
                    {
                        // Tracking Features
                        var subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            fontSize = 11,
                            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                        };

                        GUILayout.Label("Tracking Features", subHeaderStyle);
                        EditorGUILayout.Space(3);

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
                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("enableFpsTracking"),
                            new GUIContent("FPS Tracking")
                        );
                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("enableMemoryTracking"),
                            new GUIContent("Memory Tracking")
                        );
                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("enableNetworkTracking"),
                            new GUIContent("Network Tracking")
                        );

                        EditorGUILayout.Space(8);

                        // Performance Settings
                        GUILayout.Label("Performance Settings", subHeaderStyle);
                        EditorGUILayout.Space(3);

                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("batchSize"),
                            new GUIContent("Batch Size")
                        );
                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("flushInterval"),
                            new GUIContent("Flush Interval (s)")
                        );

                        EditorGUILayout.Space(8);

                        // Debug Settings
                        GUILayout.Label("Debug Settings", subHeaderStyle);
                        EditorGUILayout.Space(3);

                        EditorGUILayout.PropertyField(
                            config.FindPropertyRelative("debug"),
                            new GUIContent("Debug Mode")
                        );
                    }
                }

                EditorGUILayout.Space(5);

                // Editor Settings
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                    };

                    GUILayout.Label("Editor Settings", subHeaderStyle);
                    EditorGUILayout.Space(3);

                    enableUpgradeDebugLog = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Upgrade Debug Logs",
                            "Show debug logs when checking for SDK updates"
                        ),
                        enableUpgradeDebugLog
                    );
                }
            }

            EditorGUILayout.Space(10);

            // Runtime Status (only show when playing)
            if (Application.isPlaying)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("📊", GUILayout.Width(20));
                        GUILayout.Label("Runtime Status", sectionHeaderStyle);
                    }

                    EditorGUILayout.Space(5);

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUI.enabled = false;
                        EditorGUILayout.Toggle("Is Configured", manager.IsConfigured);
                        if (manager.IsConfigured)
                        {
                            EditorGUILayout.IntField("Queue Size", LoopKitAPI.GetQueueSize());
                        }
                        GUI.enabled = true;

                        EditorGUILayout.Space(5);

                        // Runtime Actions
                        if (manager.IsConfigured)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Flush Events", GUILayout.Height(20)))
                                {
                                    _ = LoopKitAPI.FlushAsync();
                                    Debug.Log("[LoopKit] Manual flush triggered from editor");
                                }

                                if (GUILayout.Button("Test Event", GUILayout.Height(20)))
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
                        }
                        else
                        {
                            var infoStyle = new GUIStyle(EditorStyles.helpBox)
                            {
                                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                            };
                            using (new EditorGUILayout.HorizontalScope(infoStyle))
                            {
                                GUILayout.Label("ℹ️", GUILayout.Width(16));
                                GUILayout.Label(
                                    "Configure API key to enable runtime actions",
                                    EditorStyles.wordWrappedMiniLabel
                                );
                            }
                        }
                    }
                }

                EditorGUILayout.Space(10);
            }

            // Quick Actions Footer
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("🚀", GUILayout.Width(20));
                    GUILayout.Label("Quick Actions", sectionHeaderStyle);
                }

                EditorGUILayout.Space(5);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("🔗 Get API Key", GUILayout.Height(24)))
                        {
                            Application.OpenURL("https://app.loopkit.ai");
                        }

                        if (GUILayout.Button("📖 Documentation", GUILayout.Height(24)))
                        {
                            Application.OpenURL(
                                "https://docs.loopkit.ai/docs/unity-quickstart.html"
                            );
                        }
                    }

                    EditorGUILayout.Space(4);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("🔄 Check for Updates", GUILayout.Height(24)))
                        {
                            hasCheckedVersion = false;
                            isNewerVersionAvailable = false;
                            versionCheckFailed = false;
                            CheckForVersionUpdate();
                        }

                        // Version status
                        var versionLabel = $"v{LoopKitSDK.VERSION}";
                        if (versionCheckFailed)
                        {
                            versionLabel += " (Check failed)";
                        }
                        else if (hasCheckedVersion && !isNewerVersionAvailable)
                        {
                            versionLabel += " ✅";
                        }

                        var versionStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleRight,
                            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                        };
                        GUILayout.Label(versionLabel, versionStyle);
                    }
                }
            }

            EditorGUILayout.Space(5);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

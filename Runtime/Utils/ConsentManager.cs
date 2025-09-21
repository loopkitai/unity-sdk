using UnityEngine;

namespace LoopKit.Utils
{
    /// <summary>
    /// Centralized utility for reading and writing persisted consent flags.
    /// Uses PlayerPrefs to store user choices across sessions.
    /// </summary>
    public static class ConsentManager
    {
        private const string TRACKING_ENABLED_PREF_KEY = "LoopKit_TrackingEnabled";
        private const string CAMERA_SNAPSHOTS_ENABLED_PREF_KEY = "LoopKit_CameraSnapshotsEnabled";

        public static bool IsTrackingEnabled()
        {
            return PlayerPrefs.GetInt(TRACKING_ENABLED_PREF_KEY, 0) == 1;
        }

        public static void SetTrackingEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(TRACKING_ENABLED_PREF_KEY, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool IsCameraSnapshotsEnabled()
        {
            return PlayerPrefs.GetInt(CAMERA_SNAPSHOTS_ENABLED_PREF_KEY, 0) == 1;
        }

        public static void SetCameraSnapshotsEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(CAMERA_SNAPSHOTS_ENABLED_PREF_KEY, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

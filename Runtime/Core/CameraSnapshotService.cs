using System;
using System.Collections;
using LoopKit.Utils;
using UnityEngine;

namespace LoopKit.Core
{
    /// <summary>
    /// Service that periodically captures snapshots from the main camera
    /// and logs the Base64-encoded PNG to the console.
    /// </summary>
    public class CameraSnapshotService
    {
        private LoopKitConfig _config;
        private readonly ILogger _logger;
        private GameObject _runnerGO;
        private CameraSnapshotRunner _runner;

        public CameraSnapshotService(LoopKitConfig config, ILogger logger)
        {
            _config = config ?? new LoopKitConfig();
            _logger = logger ?? new Logger(_config);
        }

        public void StartIfEnabled()
        {
            if (!_config.enableCameraSnapshots)
                return;
            if (_runner != null)
                return;

            _runnerGO = new GameObject("LoopKit_CameraSnapshotService");
            _runnerGO.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_runnerGO);
            _runner = _runnerGO.AddComponent<CameraSnapshotRunner>();
            _runner.Initialize(this, _config.cameraSnapshotInterval);
            Application.quitting += OnApplicationQuitting;
            _logger.Info("Camera snapshot service started");
        }

        public void Stop()
        {
            Application.quitting -= OnApplicationQuitting;
            if (_runner != null)
            {
                _runner.StopSnapshots();
                UnityEngine.Object.Destroy(_runner);
                _runner = null;
            }
            if (_runnerGO != null)
            {
                UnityEngine.Object.Destroy(_runnerGO);
                _runnerGO = null;
            }

            _logger.Debug("Camera snapshot service stopped");
        }

        public void UpdateConfig(LoopKitConfig config)
        {
            _config = config ?? _config;
            if (_config.enableCameraSnapshots)
            {
                if (_runner == null)
                {
                    StartIfEnabled();
                }
                else
                {
                    _runner.SetInterval(_config.cameraSnapshotInterval);
                }
            }
            else
            {
                if (_runner != null)
                {
                    Stop();
                }
            }
        }

        private void OnApplicationQuitting()
        {
            try
            {
                Stop();
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        internal void CaptureAndLog()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                _logger.Warn("Camera snapshot: no main camera found");
                return;
            }

            var width = Screen.width;
            var height = Screen.height;

            var renderTexture = new RenderTexture(width, height, 24);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;

                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.Destroy(texture);

                var base64 = Convert.ToBase64String(bytes);
                Debug.Log($"[LoopKit] Camera snapshot (base64 PNG): {base64}");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to capture camera snapshot", ex);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }
    }

    internal class CameraSnapshotRunner : MonoBehaviour
    {
        private CameraSnapshotService _service;
        private float _interval;
        private Coroutine _coroutine;

        public void Initialize(CameraSnapshotService service, float interval)
        {
            _service = service;
            _interval = Mathf.Max(0.1f, interval);
            _coroutine = StartCoroutine(SnapshotLoop());
        }

        public void SetInterval(float interval)
        {
            _interval = Mathf.Max(0.1f, interval);
        }

        public void StopSnapshots()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
                _coroutine = null;
            }
        }

        private IEnumerator SnapshotLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_interval);
                if (_service != null)
                {
                    _service.CaptureAndLog();
                }
            }
        }

        private void OnDestroy()
        {
            _service = null;
        }
    }
}


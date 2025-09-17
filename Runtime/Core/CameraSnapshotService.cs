using System;
using System.Collections;
using LoopKit;
using LoopKit.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace LoopKit.Core
{
    /// <summary>
    /// Service that periodically captures snapshots from the main camera
    /// </summary>
    public class CameraSnapshotService
    {
        private const int MAX_SNAPSHOT_DIMENSION = 640; // Cross-platform compression via downscaling
        private LoopKitConfig _config;
        private readonly ILogger _logger;
        private GameObject _runnerGO;
        private CameraSnapshotRunner _runner;
        private INetworkManager _networkManager;
        private ISessionManager _sessionManager;

        // Frame buffer for uploads
        private readonly System.Collections.Generic.Queue<byte[]> _frameBuffer =
            new System.Collections.Generic.Queue<byte[]>();
        private float _lastUploadTime;
        private int _sequence;
        private bool _errorSnapshotTakenSinceLastTick;

        public CameraSnapshotService(LoopKitConfig config, ILogger logger)
        {
            _config = config ?? new LoopKitConfig();
            _logger = logger ?? new global::LoopKit.Utils.Logger(_config);
            _lastUploadTime = -9999f;
            _sequence = 0;
            _errorSnapshotTakenSinceLastTick = false;
        }

        public void SetDependencies(INetworkManager networkManager, ISessionManager sessionManager)
        {
            _networkManager = networkManager;
            _sessionManager = sessionManager;
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
            _runner.Initialize(this, 5f);
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
                    _runner.SetInterval(5f);
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

        internal void CaptureAndLog(bool isErrorTriggered = false)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                _logger.Warn("Camera snapshot: no main camera found");
                return;
            }

            var width = Screen.width;
            var height = Screen.height;

            // Compute downscaled target size to reduce bytes (platform-agnostic)
            var maxDim = Mathf.Max(width, height);
            var scale =
                maxDim > MAX_SNAPSHOT_DIMENSION ? (float)MAX_SNAPSHOT_DIMENSION / maxDim : 1f;
            var targetWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            var targetHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));

            var renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;

                var texture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                texture.Apply();

                var bytes = texture.EncodeToPNG();
                UnityEngine.Object.Destroy(texture);

                var base64 = Convert.ToBase64String(bytes);
                var dataUrl = $"data:image/png;base64,{base64}";

                // Enqueue and attempt upload if enabled
                if (_config.enableSnapshotUpload)
                {
                    EnqueueFrame(bytes);
                    TryUploadNext();
                }
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

            // Reset error snapshot allowance after a regular tick
            if (!isErrorTriggered)
            {
                _errorSnapshotTakenSinceLastTick = false;
            }
        }

        public void CaptureOnErrorIfAllowed()
        {
            if (!_config.enableCameraSnapshots)
                return;

            if (_errorSnapshotTakenSinceLastTick)
                return;

            _errorSnapshotTakenSinceLastTick = true;
            CaptureAndLog(true);
        }

        private void EnqueueFrame(byte[] imageBytes)
        {
            try
            {
                var maxSize = Mathf.Max(1, _config.cameraSnapshotBufferSize);
                while (_frameBuffer.Count >= maxSize)
                {
                    _frameBuffer.Dequeue();
                }
                _frameBuffer.Enqueue(imageBytes);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to enqueue snapshot frame", ex);
            }
        }

        private async void TryUploadNext()
        {
            if (_networkManager == null || _sessionManager == null)
                return;

            // Rate limit
            var minInterval = Mathf.Max(0.1f, _config.cameraSnapshotUploadMinInterval);
            if (Time.unscaledTime - _lastUploadTime < minInterval)
                return;

            if (_frameBuffer.Count == 0)
                return;

            var imageBytes = _frameBuffer.Dequeue();
            _lastUploadTime = Time.unscaledTime;

            try
            {
                var ext = (_config.cameraSnapshotUploadExt ?? "png").ToLowerInvariant();
                if (ext != "png" && ext != "webp")
                {
                    ext = "png"; // Unity encodes PNG natively
                }

                var sessionId = _sessionManager.GetSessionId();
                var seq = System.Threading.Interlocked.Increment(ref _sequence);
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // 1) Get presigned URL
                var presignPayload = new
                {
                    sessionId = sessionId,
                    ext = ext,
                    seq = seq,
                    timestamp = timestamp,
                };
                var presignResp = await PostJsonAsync("/uploads/frames/presign", presignPayload);
                if (!presignResp.success)
                {
                    _logger.Warn("Presign request failed", presignResp);
                    return;
                }

                var url = ExtractUrlFromApiResponse(presignResp.data as string);
                if (string.IsNullOrEmpty(url))
                {
                    _logger.Warn("Presign response missing URL", presignResp);
                    return;
                }

                // 2) Upload bytes to presigned URL
                var ok = await PutBytesAsync(url, imageBytes, ext);
                if (!ok)
                {
                    _logger.Warn("Snapshot upload failed for presigned URL");
                    return;
                }

                _logger.Debug("Snapshot uploaded", new { url });

                // Track event with uploaded file name
                try
                {
                    string fileName = null;
                    try
                    {
                        var uri = new Uri(url);
                        var path = uri.AbsolutePath;
                        if (!string.IsNullOrEmpty(path))
                        {
                            var idx = path.LastIndexOf('/') + 1;
                            if (idx >= 0 && idx < path.Length)
                                fileName = path.Substring(idx);
                        }
                    }
                    catch
                    {
                        // Fallback: naive extraction without Uri parsing
                        var q = url.Split('?')[0];
                        var parts = q.Split('/');
                        fileName = parts.Length > 0 ? parts[parts.Length - 1] : null;
                    }

                    var props = new System.Collections.Generic.Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        props["file_name"] = fileName;
                    }

                    LoopKitAPI.Track("system_frame_snapshot", props);
                }
                catch (Exception trackEx)
                {
                    _logger.Warn("Failed to track system_frame_snapshot", trackEx);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Snapshot upload error", ex);
            }
        }

        private async System.Threading.Tasks.Task<ApiResponse> PostJsonAsync(
            string endpoint,
            object payload
        )
        {
            return await _networkManager.SendEventsAsync(endpoint, payload, 0);
        }

        private async System.Threading.Tasks.Task<bool> PutBytesAsync(
            string url,
            byte[] bytes,
            string ext
        )
        {
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", ext == "png" ? "image/png" : "image/webp");
                request.timeout = Mathf.Max(1, _config.requestTimeout / 1000);

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

                return request.result == UnityWebRequest.Result.Success;
            }
        }

        [Serializable]
        private class PresignResponse
        {
            public string url;
        }

        private string ExtractUrlFromApiResponse(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                    return null;
                var obj = JsonUtility.FromJson<PresignResponse>(json);
                return obj != null ? obj.url : null;
            }
            catch
            {
                return null;
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LoopKit.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace LoopKit.Core
{
    /// <summary>
    /// Network manager for API communication
    /// Handles HTTP requests, retries, and error handling
    /// </summary>
    public class NetworkManager : INetworkManager
    {
        private LoopKitConfig _config;
        private readonly ILogger _logger;

        public NetworkManager(LoopKitConfig config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Send events to API asynchronously
        /// </summary>
        public async Task<ApiResponse> SendEventsAsync(
            string endpoint,
            object payload,
            int retryCount = 0
        )
        {
            try
            {
                var url = $"{_config.baseURL.TrimEnd('/')}/{endpoint.TrimStart('/')}";

                // Try to use Newtonsoft.Json if available, otherwise use JsonUtility
                var jsonData = SerializeToJson(payload);

                // Log full request details (debug level)
                _logger.Debug(
                    "Sending events request",
                    new
                    {
                        url,
                        retryCount,
                        payload = jsonData,
                    }
                );

                // Add detailed payload logging for debugging 400 errors
                _logger.Info($"PAYLOAD DEBUG - Endpoint: {endpoint}");
                _logger.Info($"PAYLOAD DEBUG - JSON: {jsonData}");
                Debug.Log($"[LoopKit] PAYLOAD DEBUG - Endpoint: {endpoint}");
                Debug.Log($"[LoopKit] PAYLOAD DEBUG - JSON: {jsonData}");

                using (var request = new UnityWebRequest(url, "POST"))
                {
                    // Set up request
                    var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();

                    // Set headers
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", _config.apiKey);
                    request.SetRequestHeader("User-Agent", "LoopKit-Unity");

                    if (_config.enableCompression)
                    {
                        request.SetRequestHeader("Accept-Encoding", "gzip");
                    }

                    // Set timeout
                    request.timeout = _config.requestTimeout / 1000; // Convert to seconds

                    // Send request
                    var operation = request.SendWebRequest();

                    // Wait for completion with proper async handling
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    // Handle response
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        _logger.Debug(
                            "API request succeeded",
                            new
                            {
                                url,
                                statusCode = request.responseCode,
                                response = request.downloadHandler.text,
                            }
                        );

                        var responseText = request.downloadHandler.text;
                        var response = new ApiResponse
                        {
                            success = true,
                            message = "Events sent successfully",
                            data = responseText,
                        };

                        return response;
                    }
                    else
                    {
                        var errorMessage = $"API request failed";
                        var errorContext = new
                        {
                            url,
                            statusCode = request.responseCode,
                            retryCount,
                            error = request.error,
                            response = request.downloadHandler?.text,
                        };
                        _logger.Warn(errorMessage, errorContext);

                        // Determine if we should retry
                        if (ShouldRetry(request, retryCount))
                        {
                            var delay = CalculateRetryDelay(retryCount);
                            _logger.Debug($"Retrying request after {delay}ms");

                            await Task.Delay(delay);
                            return await SendEventsAsync(endpoint, payload, retryCount + 1);
                        }

                        return new ApiResponse
                        {
                            success = false,
                            message = errorMessage,
                            data = request.error,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send events to API", ex);

                // Retry on exception if appropriate
                if (retryCount < _config.maxRetries)
                {
                    var delay = CalculateRetryDelay(retryCount);
                    _logger.Debug($"Retrying request after exception, delay: {delay}ms");

                    await Task.Delay(delay);
                    return await SendEventsAsync(endpoint, payload, retryCount + 1);
                }

                return new ApiResponse
                {
                    success = false,
                    message = ex.Message,
                    data = ex.StackTrace,
                };
            }
        }

        /// <summary>
        /// Update network manager configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            _config = config ?? _config;
            _logger.Debug("Network manager configuration updated");
        }

        /// <summary>
        /// Determine if request should be retried based on error type
        /// </summary>
        private bool ShouldRetry(UnityWebRequest request, int retryCount)
        {
            if (retryCount >= _config.maxRetries)
            {
                return false;
            }

            // Retry on network errors and server errors (5xx)
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    return true;

                case UnityWebRequest.Result.ProtocolError:
                    // Retry on 5xx server errors, but not on 4xx client errors
                    var statusCode = request.responseCode;
                    return statusCode >= 500 && statusCode < 600;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Calculate retry delay based on backoff strategy
        /// </summary>
        private int CalculateRetryDelay(int retryCount)
        {
            const int baseDelay = 1000; // 1 second base delay

            return _config.retryBackoff switch
            {
                RetryBackoff.Exponential => baseDelay * (int)Math.Pow(2, retryCount),
                RetryBackoff.Linear => baseDelay * (retryCount + 1),
                _ => baseDelay,
            };
        }

        /// <summary>
        /// Serialize object to JSON using Newtonsoft.Json if available, otherwise JsonUtility
        /// </summary>
        private string SerializeToJson(object payload)
        {
            try
            {
                // Try to use Newtonsoft.Json via reflection if available
                var newtonsoftType = System.Type.GetType(
                    "Newtonsoft.Json.JsonConvert, Newtonsoft.Json"
                );
                if (newtonsoftType != null)
                {
                    var serializeMethod = newtonsoftType.GetMethod(
                        "SerializeObject",
                        new[] { typeof(object) }
                    );
                    if (serializeMethod != null)
                    {
                        var result = serializeMethod.Invoke(null, new[] { payload });
                        return result as string;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(
                    $"Failed to use Newtonsoft.Json, falling back to JsonUtility: {ex.Message}"
                );
            }

            // Fallback to Unity's JsonUtility
            return JsonUtility.ToJson(payload);
        }

        /// <summary>
        /// Send events using Unity's coroutine system (alternative approach)
        /// </summary>
        public IEnumerator SendEventsCoroutine(
            string endpoint,
            object payload,
            Action<ApiResponse> callback,
            int retryCount = 0
        )
        {
            var url = $"{_config.baseURL.TrimEnd('/')}/{endpoint.TrimStart('/')}";

            // Try to use Newtonsoft.Json if available, otherwise use JsonUtility
            var jsonData = SerializeToJson(payload);

            _logger.Debug("Sending events via coroutine", new { url, payload = jsonData });

            using (var request = new UnityWebRequest(url, "POST"))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", _config.apiKey);
                request.SetRequestHeader("User-Agent", "LoopKit-Unity");

                if (_config.enableCompression)
                {
                    request.SetRequestHeader("Accept-Encoding", "gzip");
                }

                request.timeout = _config.requestTimeout / 1000;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    _logger.Debug(
                        "Coroutine request succeeded",
                        new { url, response = request.downloadHandler.text }
                    );

                    callback?.Invoke(
                        new ApiResponse
                        {
                            success = true,
                            message = "Events sent successfully",
                            data = request.downloadHandler.text,
                        }
                    );
                }
                else
                {
                    var errorContext = new
                    {
                        url,
                        statusCode = request.responseCode,
                        retryCount,
                        error = request.error,
                        response = request.downloadHandler?.text,
                    };
                    _logger.Warn("API request failed", errorContext);

                    if (ShouldRetry(request, retryCount))
                    {
                        var delay = CalculateRetryDelay(retryCount) / 1000f; // Convert to seconds
                        yield return new WaitForSeconds(delay);

                        // Retry
                        yield return SendEventsCoroutine(
                            endpoint,
                            payload,
                            callback,
                            retryCount + 1
                        );
                    }
                    else
                    {
                        callback?.Invoke(
                            new ApiResponse
                            {
                                success = false,
                                message = "API request failed",
                                data = request.error,
                            }
                        );
                    }
                }
            }
        }
    }
}

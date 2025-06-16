using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoopKit.Utils;
using UnityEngine;

namespace LoopKit.Core
{
    /// <summary>
    /// Queue manager for managing event queues and batch processing
    /// Handles automatic flushing, persistence, and queue size management
    /// </summary>
    public class QueueManager : IQueueManager
    {
        private readonly LoopKitConfig _config;
        private readonly ILogger _logger;
        private readonly StorageManager _storageManager;

        private readonly List<object> _eventQueue;
        private INetworkManager _networkManager;
        private Coroutine _flushCoroutine;
        private MonoBehaviour _coroutineRunner;
        private bool _isFlushInProgress;

        // Payload structures for each endpoint
        [Serializable]
        private class TracksPayload
        {
            public List<TrackEvent> tracks = new List<TrackEvent>();
        }

        [Serializable]
        private class IdentifiesPayload
        {
            public List<IdentifyEvent> identifies = new List<IdentifyEvent>();
        }

        [Serializable]
        private class GroupsPayload
        {
            public List<GroupEvent> groups = new List<GroupEvent>();
        }

        public QueueManager(LoopKitConfig config, ILogger logger, StorageManager storageManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageManager =
                storageManager ?? throw new ArgumentNullException(nameof(storageManager));

            _eventQueue = new List<object>();
            _isFlushInProgress = false;

            // Load persisted events
            LoadPersistedEvents();

            // Find or create coroutine runner
            FindCoroutineRunner();
        }

        /// <summary>
        /// Add event to queue
        /// </summary>
        public void EnqueueEvent(object eventData)
        {
            if (eventData == null)
            {
                _logger.Warn("Attempted to enqueue null event");
                return;
            }

            lock (_eventQueue)
            {
                // Check queue size limit
                if (_eventQueue.Count >= _config.maxQueueSize)
                {
                    _logger.Warn("Event queue is full, removing oldest events");

                    // Remove oldest events to make room (FIFO)
                    var eventsToRemove = _eventQueue.Count - _config.maxQueueSize + 1;
                    _eventQueue.RemoveRange(0, eventsToRemove);
                }

                _eventQueue.Add(eventData);
                _logger.Debug($"Enqueued event, queue size: {_eventQueue.Count}");

                // Persist updated queue
                _storageManager.PersistQueue(_eventQueue);

                // Check if we should auto-flush
                if (_eventQueue.Count >= _config.batchSize)
                {
                    _logger.Debug("Batch size reached, triggering flush");
                    _ = FlushAsync(_networkManager);
                }
            }
        }

        /// <summary>
        /// Flush events to API asynchronously
        /// </summary>
        public async Task FlushAsync(INetworkManager networkManager)
        {
            if (networkManager == null)
            {
                _logger.Debug("Network manager not available, skipping flush");
                return;
            }

            if (_isFlushInProgress)
            {
                _logger.Debug("Flush already in progress, skipping");
                return;
            }

            List<object> eventsToFlush;

            lock (_eventQueue)
            {
                if (_eventQueue.Count == 0)
                {
                    _logger.Debug("No events to flush");
                    return;
                }

                // Take events to flush
                var batchSize = Math.Min(_eventQueue.Count, _config.batchSize);
                eventsToFlush = _eventQueue.Take(batchSize).ToList();
            }

            _isFlushInProgress = true;

            try
            {
                _logger.Info($"Flushing {eventsToFlush.Count} events to API");

                // Prepare payload
                var fullPayload = CreateApiPayload(eventsToFlush);

                // Prepare payloads for each endpoint
                var payloadTracks = new TracksPayload { tracks = fullPayload.tracks };
                var payloadIdentifies = new IdentifiesPayload
                {
                    identifies = fullPayload.identifies,
                };
                var payloadGroups = new GroupsPayload { groups = fullPayload.groups };

                bool allSuccess = true;

                // Send tracks
                if (payloadTracks.tracks.Count > 0)
                {
                    var resp = await networkManager.SendEventsAsync("tracks", payloadTracks);
                    allSuccess &= resp.success;
                }

                // Send identifies
                if (payloadIdentifies.identifies.Count > 0)
                {
                    var resp = await networkManager.SendEventsAsync(
                        "identifies",
                        payloadIdentifies
                    );
                    allSuccess &= resp.success;
                }

                // Send groups
                if (payloadGroups.groups.Count > 0)
                {
                    var resp = await networkManager.SendEventsAsync("groups", payloadGroups);
                    allSuccess &= resp.success;
                }

                if (allSuccess)
                {
                    lock (_eventQueue)
                    {
                        // Remove successfully sent events
                        _eventQueue.RemoveRange(0, eventsToFlush.Count);

                        // Persist updated queue
                        _storageManager.PersistQueue(_eventQueue);
                    }

                    _logger.Info($"Successfully flushed {eventsToFlush.Count} events");
                }
                else
                {
                    _logger.Error($"Failed to flush events: one or more endpoint requests failed");

                    // Events remain in queue for retry
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Exception during event flush", ex);
            }
            finally
            {
                _isFlushInProgress = false;
            }
        }

        /// <summary>
        /// Get current queue
        /// </summary>
        public List<object> GetQueue()
        {
            lock (_eventQueue)
            {
                return new List<object>(_eventQueue);
            }
        }

        /// <summary>
        /// Get queue size
        /// </summary>
        public int GetQueueSize()
        {
            lock (_eventQueue)
            {
                return _eventQueue.Count;
            }
        }

        /// <summary>
        /// Clear queue
        /// </summary>
        public void ClearQueue()
        {
            lock (_eventQueue)
            {
                _eventQueue.Clear();
                _storageManager.ClearQueue();
                _logger.Debug("Event queue cleared");
            }
        }

        /// <summary>
        /// Reset queue state
        /// </summary>
        public void Reset()
        {
            _logger.Debug("Resetting queue manager");

            // Stop auto-flush
            StopAutoFlush();

            // Clear queue
            ClearQueue();

            // Restart auto-flush
            ScheduleFlush();
        }

        /// <summary>
        /// Schedule automatic flush
        /// </summary>
        public void ScheduleFlush()
        {
            if (_coroutineRunner == null)
            {
                FindCoroutineRunner();
            }

            if (_coroutineRunner != null && _config.flushInterval > 0)
            {
                StopAutoFlush();
                _flushCoroutine = _coroutineRunner.StartCoroutine(AutoFlushCoroutine());
                _logger.Debug($"Scheduled auto-flush every {_config.flushInterval} seconds");
            }
        }

        /// <summary>
        /// Set network manager reference
        /// </summary>
        public void SetNetworkManager(INetworkManager networkManager)
        {
            _networkManager = networkManager;
            _logger.Debug("Network manager set on queue manager");
        }

        /// <summary>
        /// Update queue manager configuration
        /// </summary>
        public void UpdateConfig(LoopKitConfig config)
        {
            _logger.Debug("Queue manager configuration updated");

            // Restart auto-flush with new interval
            ScheduleFlush();
        }

        /// <summary>
        /// Restart auto-flush with new configuration
        /// </summary>
        public void RestartAutoFlush()
        {
            ScheduleFlush();
        }

        /// <summary>
        /// Load persisted events from storage
        /// </summary>
        private void LoadPersistedEvents()
        {
            try
            {
                var persistedEvents = _storageManager.LoadQueue();
                if (persistedEvents != null && persistedEvents.Count > 0)
                {
                    lock (_eventQueue)
                    {
                        _eventQueue.AddRange(persistedEvents);
                    }

                    _logger.Info($"Loaded {persistedEvents.Count} persisted events");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load persisted events", ex);
            }
        }

        /// <summary>
        /// Create API payload from events
        /// </summary>
        private ApiPayload CreateApiPayload(List<object> events)
        {
            var payload = new ApiPayload();

            foreach (var eventObj in events)
            {
                try
                {
                    switch (eventObj)
                    {
                        case TrackEvent trackEvent:
                            payload.tracks.Add(trackEvent);
                            break;

                        case IdentifyEvent identifyEvent:
                            payload.identifies.Add(identifyEvent);
                            break;

                        case GroupEvent groupEvent:
                            payload.groups.Add(groupEvent);
                            break;

                        default:
                            _logger.Warn($"Unknown event type: {eventObj.GetType()}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to process event of type {eventObj.GetType()}", ex);
                }
            }

            return payload;
        }

        /// <summary>
        /// Auto-flush coroutine
        /// </summary>
        private IEnumerator AutoFlushCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_config.flushInterval);

                if (_eventQueue.Count > 0 && _networkManager != null)
                {
                    _logger.Debug("Auto-flush triggered");
                    _ = FlushAsync(_networkManager);
                }
            }
        }

        /// <summary>
        /// Stop auto-flush coroutine
        /// </summary>
        private void StopAutoFlush()
        {
            if (_flushCoroutine != null && _coroutineRunner != null)
            {
                _coroutineRunner.StopCoroutine(_flushCoroutine);
                _flushCoroutine = null;
                _logger.Debug("Stopped auto-flush coroutine");
            }
        }

        /// <summary>
        /// Find or create a MonoBehaviour to run coroutines
        /// </summary>
        private void FindCoroutineRunner()
        {
            // Try to find existing LoopKit GameObject
            var loopKitGO = GameObject.Find("LoopKit");
            if (loopKitGO == null)
            {
                // Create new GameObject for LoopKit
                loopKitGO = new GameObject("LoopKit");
                GameObject.DontDestroyOnLoad(loopKitGO);
            }

            _coroutineRunner = loopKitGO.GetComponent<LoopKitCoroutineRunner>();
            if (_coroutineRunner == null)
            {
                _coroutineRunner = loopKitGO.AddComponent<LoopKitCoroutineRunner>();
            }

            _logger.Debug("Coroutine runner initialized");
        }
    }

    /// <summary>
    /// MonoBehaviour component for running LoopKit coroutines
    /// </summary>
    internal class LoopKitCoroutineRunner : MonoBehaviour
    {
        private void Awake()
        {
            // Ensure this GameObject persists across scenes
            DontDestroyOnLoad(gameObject);
        }
    }
}

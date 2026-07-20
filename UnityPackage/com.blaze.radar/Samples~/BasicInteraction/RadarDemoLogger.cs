using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blaze.Radar.Samples
{
    /// <summary>
    /// Keeps the sample's live radar diagnostics and bounded on-screen history in one place.
    /// Continuous movement is throttled in history, while the live frame panel still updates every frame.
    /// </summary>
    public sealed class RadarDemoLogger : MonoBehaviour
    {
        private static readonly Color ConnectedColor = new Color32(82, 224, 179, 255);
        private static readonly Color WaitingColor = new Color32(247, 184, 89, 255);

        [Header("Radar Source")]
        [SerializeField] private RadarFrameDispatcher radarDispatcher;

        [Header("Diagnostics UI")]
        [SerializeField] private Image connectionStatusDot;
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text latestFrameText;
        [SerializeField] private Text eventLogText;
        [SerializeField] private Text interactionStatusText;
        [SerializeField] private ScrollRect eventLogScrollRect;

        [Header("History Limits")]
        [SerializeField, Min(20)] private int maxLogEntries = 160;
        [SerializeField, Range(0.05f, 1f)] private float frameHistoryIntervalSeconds = 0.25f;
        [SerializeField, Range(0.03f, 0.5f)] private float continuousEventIntervalSeconds = 0.12f;

        private readonly Queue<string> _entries = new Queue<string>();
        private readonly Dictionary<string, float> _nextContinuousEventTimes =
            new Dictionary<string, float>();
        private readonly StringBuilder _textBuilder = new StringBuilder(4096);

        private long _receivedFrameCount;
        private int _lastPointerCount = -1;
        private float _nextFrameHistoryTime;
        private bool? _lastConnectedState;
        private bool _sessionStarted;
        private bool _isAutoScrolling;
        private int _ignoreScrollCallbacksThroughFrame = -1;

        public bool IsAutoScrolling =>
            _isAutoScrolling || Time.frameCount <= _ignoreScrollCallbacksThroughFrame;

        private void OnEnable()
        {
            if (radarDispatcher == null)
            {
                SetConnectionPresentation(false);
                AppendLog("CONFIG", "RadarFrameDispatcher reference is missing.");
                return;
            }

            radarDispatcher.ConnectionChanged += OnConnectionChanged;
            radarDispatcher.ErrorReceived += OnRadarError;
            radarDispatcher.PointerFrameReceived += OnPointerFrameReceived;

            if (!_sessionStarted)
            {
                _sessionStarted = true;
                AppendLog(
                    "SESSION",
                    $"SDK {UnitySdkVersion.Value} | Unity {Application.unityVersion} | "
                    + $"screen {Screen.width}x{Screen.height}");
            }

            OnConnectionChanged(radarDispatcher.IsConnected);
            if (latestFrameText != null)
            {
                latestFrameText.text = "Waiting for the first radar pointer frame...";
            }
        }

        private void OnDisable()
        {
            if (radarDispatcher == null)
            {
                return;
            }

            radarDispatcher.ConnectionChanged -= OnConnectionChanged;
            radarDispatcher.ErrorReceived -= OnRadarError;
            radarDispatcher.PointerFrameReceived -= OnPointerFrameReceived;
        }

        private void OnValidate()
        {
            maxLogEntries = Mathf.Max(20, maxLogEntries);
            frameHistoryIntervalSeconds = Mathf.Max(0.05f, frameHistoryIntervalSeconds);
            continuousEventIntervalSeconds = Mathf.Max(0.03f, continuousEventIntervalSeconds);
        }

        public void ClearLog()
        {
            _entries.Clear();
            _nextContinuousEventTimes.Clear();
            AppendLog("SESSION", "History cleared. Live radar diagnostics remain active.");
        }

        public void ShowInteractionStatus(string message)
        {
            if (interactionStatusText != null)
            {
                interactionStatusText.text = message;
            }
        }

        public void LogUiEvent(string eventName, string details)
        {
            LogUiEvent(eventName, details, false);
        }

        public void LogContinuousUiEvent(string eventName, string details)
        {
            LogUiEvent(eventName, details, true);
        }

        public void LogPointerEvent(
            string source,
            string eventName,
            PointerEventData eventData,
            bool continuous = false)
        {
            if (eventData == null)
            {
                return;
            }

            string target = eventData.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.name
                : eventData.pointerEnter != null ? eventData.pointerEnter.name : "<none>";
            string summary = $"{source} | {eventName} | pointer {eventData.pointerId} | hit {target}";
            ShowInteractionStatus(summary);

            string throttleKey = $"POINTER:{source}:{eventName}:{eventData.pointerId}";
            if (continuous && !ShouldRecordContinuousEvent(throttleKey))
            {
                return;
            }

            AppendLog(
                "EVENT",
                $"{source}.{eventName} | id {eventData.pointerId} | "
                + $"pos ({eventData.position.x:0.0}, {eventData.position.y:0.0}) | "
                + $"delta ({eventData.delta.x:0.0}, {eventData.delta.y:0.0}) | "
                + $"press ({eventData.pressPosition.x:0.0}, {eventData.pressPosition.y:0.0}) | "
                + $"hit {target} | clicks {eventData.clickCount} | dragging {eventData.dragging}");
        }

        private void LogUiEvent(string eventName, string details, bool continuous)
        {
            string message = $"{eventName} | {details}";
            ShowInteractionStatus(message);

            if (continuous && !ShouldRecordContinuousEvent("UGUI:" + eventName))
            {
                return;
            }

            AppendLog("UGUI", message);
        }

        private bool ShouldRecordContinuousEvent(string key)
        {
            float now = Time.unscaledTime;
            if (_nextContinuousEventTimes.TryGetValue(key, out float nextTime) && now < nextTime)
            {
                return false;
            }

            _nextContinuousEventTimes[key] = now + continuousEventIntervalSeconds;
            return true;
        }

        private void OnConnectionChanged(bool connected)
        {
            SetConnectionPresentation(connected);
            if (_lastConnectedState.HasValue && _lastConnectedState.Value == connected)
            {
                return;
            }

            _lastConnectedState = connected;
            RadarPipeClient client = radarDispatcher != null ? radarDispatcher.Client : null;
            string bridgeVersion = ValueOrUnknown(client != null ? client.BridgeVersion : null);
            string deviceModel = ValueOrUnknown(client != null ? client.DeviceModel : null);
            AppendLog(
                "IPC",
                connected
                    ? $"CONNECTED | bridge {bridgeVersion} | device {deviceModel}"
                    : "WAITING | bridge disconnected; reconnect loop remains active");
            ShowInteractionStatus(
                connected
                    ? $"IPC connected | Bridge {bridgeVersion} | Device {deviceModel}"
                    : "IPC waiting | reconnect loop active");
        }

        private void SetConnectionPresentation(bool connected)
        {
            Color color = connected ? ConnectedColor : WaitingColor;
            if (connectionStatusDot != null)
            {
                connectionStatusDot.color = color;
            }

            if (connectionStatusText != null)
            {
                connectionStatusText.text = connected ? "IPC  CONNECTED" : "IPC  WAITING";
                connectionStatusText.color = color;
            }
        }

        private void OnRadarError(string message)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "Unknown RadarBridge error." : message;
            AppendLog("ERROR", safeMessage);
            ShowInteractionStatus("Radar error | " + safeMessage);
        }

        private void OnPointerFrameReceived(RadarPointerFrameMessage frame)
        {
            if (frame == null)
            {
                return;
            }

            _receivedFrameCount++;
            int pointerCount = frame.pointers != null ? frame.pointers.Count : 0;
            RenderLatestFrame(frame, pointerCount);

            bool containsEdgePhase = ContainsEdgePhase(frame);
            float now = Time.unscaledTime;
            if (containsEdgePhase || pointerCount != _lastPointerCount || now >= _nextFrameHistoryTime)
            {
                AppendFrameHistory(frame, pointerCount);
                _nextFrameHistoryTime = now + frameHistoryIntervalSeconds;
            }

            _lastPointerCount = pointerCount;
        }

        private void RenderLatestFrame(RadarPointerFrameMessage frame, int pointerCount)
        {
            if (latestFrameText == null)
            {
                return;
            }

            RadarPipeClient client = radarDispatcher != null ? radarDispatcher.Client : null;
            long droppedFrames = client != null ? client.DroppedFrameCount : 0L;
            _textBuilder.Clear();
            _textBuilder.Append("RX ").Append(_receivedFrameCount)
                .Append("  |  IPC SEQ ").Append(frame.sequence)
                .Append("  |  POINTERS ").Append(pointerCount)
                .Append("  |  DROPPED ").Append(droppedFrames).AppendLine();
            _textBuilder.Append("FRAME ").Append(FormatClock(frame.timestampUnixMilliseconds))
                .Append("  |  AGE ").Append(FormatAge(frame.timestampUnixMilliseconds))
                .Append("  |  SCREEN ").Append(Screen.width).Append('x').Append(Screen.height);

            if (pointerCount == 0)
            {
                _textBuilder.AppendLine().Append("No active radar pointers.");
            }
            else
            {
                for (int index = 0; index < pointerCount; index++)
                {
                    _textBuilder.AppendLine();
                    AppendPointerDetails(_textBuilder, frame.pointers[index]);
                }
            }

            latestFrameText.text = _textBuilder.ToString();
        }

        private void AppendFrameHistory(RadarPointerFrameMessage frame, int pointerCount)
        {
            RadarPipeClient client = radarDispatcher != null ? radarDispatcher.Client : null;
            long droppedFrames = client != null ? client.DroppedFrameCount : 0L;
            _textBuilder.Clear();
            _textBuilder.Append("seq ").Append(frame.sequence)
                .Append(" | pointers ").Append(pointerCount)
                .Append(" | dropped ").Append(droppedFrames)
                .Append(" | age ").Append(FormatAge(frame.timestampUnixMilliseconds));

            if (frame.pointers != null)
            {
                for (int index = 0; index < frame.pointers.Count; index++)
                {
                    _textBuilder.AppendLine();
                    AppendPointerDetails(_textBuilder, frame.pointers[index]);
                }
            }

            AppendLog("FRAME", _textBuilder.ToString());
        }

        private static void AppendPointerDetails(StringBuilder builder, RadarPointerMessage pointer)
        {
            float pixelX = Mathf.Clamp01(pointer.normalizedX) * Screen.width;
            float pixelY = Mathf.Clamp01(pointer.normalizedY) * Screen.height;
            builder.Append("  P").Append(pointer.pointerId)
                .Append(' ').Append(pointer.phase.ToString().ToUpperInvariant())
                .Append(" | N(").Append(pointer.normalizedX.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(", ").Append(pointer.normalizedY.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(") | PX(").Append(pixelX.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(", ").Append(pixelY.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(") | conf ").Append(pointer.confidence.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" | sample ").Append(FormatClock(pointer.timestampUnixMilliseconds));
        }

        private static bool ContainsEdgePhase(RadarPointerFrameMessage frame)
        {
            if (frame.pointers == null)
            {
                return false;
            }

            for (int index = 0; index < frame.pointers.Count; index++)
            {
                RadarPointerPhase phase = frame.pointers[index].phase;
                if (phase == RadarPointerPhase.Down || phase == RadarPointerPhase.Up)
                {
                    return true;
                }
            }

            return false;
        }

        private void AppendLog(string category, string message)
        {
            string entry = $"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}";
            _entries.Enqueue(entry);
            while (_entries.Count > maxLogEntries)
            {
                _entries.Dequeue();
            }

            if (eventLogText == null)
            {
                return;
            }

            _textBuilder.Clear();
            foreach (string line in _entries)
            {
                if (_textBuilder.Length > 0)
                {
                    _textBuilder.AppendLine().AppendLine();
                }

                _textBuilder.Append(line);
            }

            eventLogText.text = _textBuilder.ToString();
            ResizeAndScrollLog();
        }

        private void ResizeAndScrollLog()
        {
            if (eventLogScrollRect == null || eventLogScrollRect.content == null || eventLogScrollRect.viewport == null)
            {
                return;
            }

            float viewportHeight = eventLogScrollRect.viewport.rect.height;
            float contentHeight = Mathf.Max(viewportHeight, eventLogText.preferredHeight + 24f);
            eventLogScrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            eventLogText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight - 12f);

            _isAutoScrolling = true;
            _ignoreScrollCallbacksThroughFrame = Time.frameCount + 1;
            eventLogScrollRect.verticalNormalizedPosition = 0f;
            _isAutoScrolling = false;
        }

        private static string FormatClock(long unixMilliseconds)
        {
            if (unixMilliseconds <= 0)
            {
                return "n/a";
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
                    .ToLocalTime()
                    .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return "invalid";
            }
        }

        private static string FormatAge(long unixMilliseconds)
        {
            if (unixMilliseconds <= 0)
            {
                return "n/a";
            }

            long age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixMilliseconds;
            return age.ToString(CultureInfo.InvariantCulture) + " ms";
        }

        private static string ValueOrUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
    }
}

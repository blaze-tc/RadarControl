using System;
using System.Diagnostics;
using UnityEngine;

namespace Blaze.Radar
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RadarFrameDispatcher : MonoBehaviour
    {
        [SerializeField] private RadarRuntimeSettings settings;
        [SerializeField] private bool autoConnect = true;

        private RadarPipeClient _client;
        private long _receivedFrameCount;
        private int _lastLoggedPointerCount = -1;

        public RadarPointerFrameMessage LatestFrame { get; private set; }
        public RadarPipeClient Client => _client;
        public bool IsConnected => _client != null && _client.IsConnected;

        public event Action<RadarPointerFrameMessage> PointerFrameReceived;
        public event Action<bool> ConnectionChanged;
        public event Action<string> ErrorReceived;

        private void Awake()
        {
            if (settings == null)
            {
                settings = RadarRuntimeSettings.LoadOrCreateRuntimeDefaults();
            }

            _client = new RadarPipeClient(
                settings.PipeName,
                settings.ConnectTimeoutMilliseconds,
                settings.ReconnectDelayMilliseconds);
            _client.ConnectionChanged += connected =>
            {
                UnityEngine.Debug.Log($"[Blaze Radar] IPC {(connected ? "connected" : "disconnected")}.", this);
                InvokeSafely(ConnectionChanged, connected);
            };
            _client.ErrorReceived += message =>
            {
                UnityEngine.Debug.LogError($"[Blaze Radar] IPC error: {message}", this);
                InvokeSafely(ErrorReceived, message);
            };
        }

        private void Start()
        {
            if (!autoConnect)
            {
                return;
            }

            _client.Start(new RadarHelloPayload
            {
                unityProcessId = Process.GetCurrentProcess().Id,
                unityVersion = Application.unityVersion,
                screenWidth = Screen.width,
                screenHeight = Screen.height
            });
        }

        private void Update()
        {
            _client?.DrainMainThreadEvents();
            if (_client != null && _client.TryConsumeLatestFrame(out var frame))
            {
                LatestFrame = frame;
                _receivedFrameCount++;
                var pointerCount = frame.pointers != null ? frame.pointers.Count : 0;
                if (_receivedFrameCount == 1 || pointerCount != _lastLoggedPointerCount || _receivedFrameCount % 120 == 0)
                {
                    UnityEngine.Debug.Log(
                        $"[Blaze Radar] Pointer frame seq={frame.sequence}, pointers={pointerCount}, " +
                        $"received={_receivedFrameCount}, dropped={_client.DroppedFrameCount}.",
                        this);
                    _lastLoggedPointerCount = pointerCount;
                }
                InvokeSafely(PointerFrameReceived, frame);
            }
        }

        private async void OnDestroy()
        {
            if (_client != null)
            {
                try
                {
                    await _client.StopAsync();
                    _client.Dispose();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
                finally
                {
                    _client = null;
                }
            }
        }

        private void InvokeSafely<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }
    }
}

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
            _client.ConnectionChanged += connected => InvokeSafely(ConnectionChanged, connected);
            _client.ErrorReceived += message => InvokeSafely(ErrorReceived, message);
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

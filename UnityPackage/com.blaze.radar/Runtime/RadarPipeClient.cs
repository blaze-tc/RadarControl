using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Blaze.Radar.Internal;

namespace Blaze.Radar
{
    public sealed class RadarPipeClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly int _connectTimeoutMilliseconds;
        private readonly int _reconnectDelayMilliseconds;
        private readonly LatestValueBuffer<RadarPointerFrameMessage> _latestFrame =
            new LatestValueBuffer<RadarPointerFrameMessage>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadActions =
            new System.Collections.Concurrent.ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cancellation;
        private Task _runTask;
        private NamedPipeClientStream _activePipe;
        private long _sequence;
        private int _connected;
        private RadarHelloPayload _hello;

        public RadarPipeClient(string pipeName, int connectTimeoutMilliseconds, int reconnectDelayMilliseconds)
        {
            _pipeName = string.IsNullOrWhiteSpace(pipeName) ? "Yuexin.RadarBridge" : pipeName;
            _connectTimeoutMilliseconds = Math.Max(50, connectTimeoutMilliseconds);
            _reconnectDelayMilliseconds = Math.Max(50, reconnectDelayMilliseconds);
        }

        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public long DroppedFrameCount => _latestFrame.DroppedCount;
        public string BridgeVersion { get; private set; } = "";
        public string DeviceModel { get; private set; } = "";
        public string LastError { get; private set; } = "";

        public event Action<bool> ConnectionChanged;
        public event Action<string> ErrorReceived;

        public void Start(RadarHelloPayload hello)
        {
            if (_runTask != null)
            {
                return;
            }

            _hello = hello ?? throw new ArgumentNullException(nameof(hello));
            _cancellation = new CancellationTokenSource();
            _runTask = Task.Run(() => RunAsync(_cancellation.Token));
        }

        public bool TryConsumeLatestFrame(out RadarPointerFrameMessage frame)
        {
            return _latestFrame.TryConsume(out frame);
        }

        public void DrainMainThreadEvents()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action();
            }
        }

        public async Task StopAsync()
        {
            var cancellation = Interlocked.Exchange(ref _cancellation, null);
            var runTask = Interlocked.Exchange(ref _runTask, null);
            cancellation?.Cancel();
            _activePipe?.Dispose();
            if (runTask != null)
            {
                try
                {
                    await runTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cooperative cancellation is the normal stop path.
                }
                catch (ObjectDisposedException)
                {
                    // Disposing the pipe unblocks an in-flight read.
                }
            }

            cancellation?.Dispose();
            SetConnected(false);
            _latestFrame.Clear();
        }

        public async Task SendShutdownAsync()
        {
            var pipe = _activePipe;
            if (pipe == null || !pipe.IsConnected)
            {
                return;
            }

            await WriteEnvelopeAsync(
                pipe,
                RadarIpcProtocol.Create(RadarIpcMessageType.Shutdown, NextSequence(), new { }),
                CancellationToken.None).ConfigureAwait(false);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _writeLock.Dispose();
        }

        public static async Task<bool> CanConnectAsync(
            string pipeName,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            using (var pipe = new NamedPipeClientStream(
                       ".",
                       pipeName,
                       PipeDirection.InOut,
                       PipeOptions.Asynchronous))
            {
                try
                {
                    await pipe.ConnectAsync(timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                    return pipe.IsConnected;
                }
                catch (TimeoutException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var pipe = new NamedPipeClientStream(
                           ".",
                           _pipeName,
                           PipeDirection.InOut,
                           PipeOptions.Asynchronous))
                {
                    _activePipe = pipe;
                    try
                    {
                        await pipe.ConnectAsync(_connectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                        SetConnected(true);
                        LastError = "";
                        await WriteEnvelopeAsync(
                            pipe,
                            RadarIpcProtocol.Create(RadarIpcMessageType.Hello, NextSequence(), _hello),
                            cancellationToken).ConfigureAwait(false);

                        using (var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            var heartbeatTask = SendHeartbeatAsync(pipe, sessionCancellation.Token);
                            try
                            {
                                await ReadMessagesAsync(pipe, cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                sessionCancellation.Cancel();
                                try
                                {
                                    await heartbeatTask.ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Session cancellation stops the heartbeat loop.
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception) when (
                        exception is IOException || exception is TimeoutException || exception is UnauthorizedAccessException)
                    {
                        ReportError(exception.Message);
                    }
                    finally
                    {
                        _activePipe = null;
                        SetConnected(false);
                    }
                }

                await Task.Delay(_reconnectDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ReadMessagesAsync(Stream stream, CancellationToken cancellationToken)
        {
            var decoder = new LengthPrefixedFrameDecoder();
            var buffer = new byte[8192];
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("RadarBridge closed the Named Pipe connection.");
                }

                var payloads = decoder.Append(buffer, 0, count);
                for (var index = 0; index < payloads.Count; index++)
                {
                    var json = Encoding.UTF8.GetString(payloads[index]);
                    var envelope = JsonConvert.DeserializeObject<RadarIpcEnvelope>(json);
                    if (envelope == null)
                    {
                        throw new InvalidDataException("RadarBridge sent an empty IPC envelope.");
                    }

                    if (envelope.protocolVersion != RadarIpcProtocol.Version)
                    {
                        throw new InvalidDataException(
                            $"IPC protocol {envelope.protocolVersion} is incompatible with Unity SDK protocol {RadarIpcProtocol.Version}.");
                    }

                    HandleEnvelope(envelope);
                }
            }
        }

        private void HandleEnvelope(RadarIpcEnvelope envelope)
        {
            switch (envelope.messageType)
            {
                case RadarIpcMessageType.HelloAck:
                    var helloAck = envelope.payload.ToObject<RadarHelloAckPayload>();
                    if (helloAck != null)
                    {
                        BridgeVersion = helloAck.bridgeVersion ?? "";
                        DeviceModel = helloAck.deviceModel ?? "";
                    }
                    break;
                case RadarIpcMessageType.PointerFrame:
                    var frame = envelope.payload.ToObject<RadarPointerFrameMessage>();
                    if (frame != null)
                    {
                        _latestFrame.Publish(frame);
                    }
                    break;
                case RadarIpcMessageType.Error:
                    var error = envelope.payload.ToObject<RadarErrorPayload>();
                    ReportError(error?.message ?? "RadarBridge returned an unspecified error.");
                    break;
            }
        }

        private async Task SendHeartbeatAsync(Stream stream, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                await WriteEnvelopeAsync(
                    stream,
                    RadarIpcProtocol.Create(
                        RadarIpcMessageType.Ping,
                        NextSequence(),
                        new RadarPingPayload
                        {
                            clientTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        }),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task WriteEnvelopeAsync(
            Stream stream,
            RadarIpcEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
            var frame = new byte[payload.Length + 4];
            frame[0] = (byte)payload.Length;
            frame[1] = (byte)(payload.Length >> 8);
            frame[2] = (byte)(payload.Length >> 16);
            frame[3] = (byte)(payload.Length >> 24);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void SetConnected(bool connected)
        {
            var value = connected ? 1 : 0;
            if (Interlocked.Exchange(ref _connected, value) == value)
            {
                return;
            }

            _mainThreadActions.Enqueue(() => ConnectionChanged?.Invoke(connected));
        }

        private void ReportError(string message)
        {
            LastError = message ?? "Unknown IPC error.";
            _mainThreadActions.Enqueue(() => ErrorReceived?.Invoke(LastError));
        }

        private long NextSequence()
        {
            return Interlocked.Increment(ref _sequence);
        }
    }
}

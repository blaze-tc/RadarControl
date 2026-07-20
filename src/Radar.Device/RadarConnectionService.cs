using System.Diagnostics;
using System.Net.Sockets;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Protocol;

namespace Yuexin.Radar.Device;

public sealed class RadarConnectionService : IAsyncDisposable
{
    private readonly RadarConnectionOptions _options;
    private readonly RadarByteStreamDecoder _decoder = new();
    private readonly RadarScanFrameBuilder _frameBuilder;
    private int _isRunning;
    private RadarTcpClient? _activeClient;

    public RadarConnectionService(RadarConnectionOptions options, int minimumScanPointCount = 100)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.ReconnectDelays.Count == 0 || _options.ReconnectDelays.Any(delay => delay < TimeSpan.Zero))
        {
            throw new ArgumentException("ReconnectDelays must contain at least one non-negative delay.", nameof(options));
        }

        if (_options.DataWarningTimeout <= TimeSpan.Zero ||
            _options.DataDisconnectTimeout <= _options.DataWarningTimeout)
        {
            throw new ArgumentException("Data timeouts must be positive and the disconnect timeout must exceed the warning timeout.", nameof(options));
        }

        _frameBuilder = new RadarScanFrameBuilder(minimumScanPointCount);
    }

    public RadarConnectionState State { get; private set; } = RadarConnectionState.Disconnected;
    public string? LastError { get; private set; }
    public RadarConnectionMetrics Metrics { get; } = new();

    public event Action<RadarConnectionState>? StateChanged;
    public event Action<ReadOnlyMemory<byte>>? BytesReceived;
    public event Action<RadarPoint>? PointReceived;
    public event Action<RadarScanFrame>? ScanFrameReceived;
    public event Action<Exception>? ConnectionError;
    public event Action<TimeSpan>? DataWarning;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
        {
            throw new InvalidOperationException("The radar connection service is already running.");
        }

        var reconnectAttempt = 0;
        var hasConnected = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SetState(hasConnected ? RadarConnectionState.Reconnecting : RadarConnectionState.Connecting);
                await using var client = new RadarTcpClient();
                _activeClient = client;

                try
                {
                    await client.ConnectAsync(_options, cancellationToken).ConfigureAwait(false);
                    hasConnected = true;
                    reconnectAttempt = 0;
                    LastError = null;
                    _decoder.Reset(resetCounters: true);
                    _frameBuilder.Reset(resetSequence: true);
                    Metrics.ConnectionSucceeded();
                    SetState(RadarConnectionState.Connected);
                    await ReceiveLoopAsync(client, cancellationToken).ConfigureAwait(false);
                    throw new IOException("The radar closed the TCP connection.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or OperationCanceledException)
                {
                    LastError = exception.Message;
                    ConnectionError?.Invoke(exception);
                    if (!_options.AutoReconnect)
                    {
                        SetState(RadarConnectionState.Faulted);
                        break;
                    }

                    SetState(RadarConnectionState.Reconnecting);
                    var delay = _options.ReconnectDelays[Math.Min(reconnectAttempt, _options.ReconnectDelays.Count - 1)];
                    reconnectAttempt++;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _activeClient = null;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cooperative shutdown.
        }
        finally
        {
            SetState(RadarConnectionState.Disconnected);
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var client = _activeClient;
        if (client is not null)
        {
            await client.DisconnectAsync().ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(RadarTcpClient client, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.DataDisconnectTimeout);

            int count;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var readTask = client.ReadAsync(buffer, timeout.Token).AsTask();
                var warningTask = Task.Delay(_options.DataWarningTimeout, cancellationToken);
                if (await Task.WhenAny(readTask, warningTask).ConfigureAwait(false) == warningTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DataWarning?.Invoke(stopwatch.Elapsed);
                }

                count = await readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"No radar data was received for {_options.DataDisconnectTimeout.TotalMilliseconds:0} ms.");
            }

            if (count == 0)
            {
                return;
            }

            var ownedBytes = buffer.AsMemory(0, count).ToArray();
            Metrics.AddReceivedBytes(count);
            BytesReceived?.Invoke(ownedBytes);

            var crcBefore = _decoder.CrcErrorCount;
            var discardedBefore = _decoder.DiscardedByteCount;
            var points = _decoder.Append(ownedBytes);
            Metrics.AddCrcErrors(_decoder.CrcErrorCount - crcBefore);
            Metrics.AddDiscardedBytes(_decoder.DiscardedByteCount - discardedBefore);
            Metrics.AddParsedPoints(points.Count);

            foreach (var point in points)
            {
                PointReceived?.Invoke(point);
                var frame = _frameBuilder.AddPoint(point);
                if (frame is not null)
                {
                    ScanFrameReceived?.Invoke(frame);
                }
            }
        }
    }

    private void SetState(RadarConnectionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }
}

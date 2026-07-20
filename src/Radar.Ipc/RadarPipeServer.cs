using System.IO.Pipes;
using Yuexin.Radar.Contracts;

namespace Yuexin.Radar.Ipc;

public sealed class RadarPipeServerOptions
{
    public string PipeName { get; set; } = "Yuexin.RadarBridge";
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public Func<HelloAckPayload> HelloAckFactory { get; set; } =
        () => new HelloAckPayload("1.0.0", "F10", false);
}

public sealed class RadarPipeServer : IAsyncDisposable
{
    private readonly RadarPipeServerOptions _options;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private NamedPipeServerStream? _activePipe;
    private long _sequence;

    public RadarPipeServer(RadarPipeServerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.PipeName))
        {
            throw new ArgumentException("PipeName is required.", nameof(options));
        }

        if (_options.HeartbeatTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("HeartbeatTimeout must be positive.", nameof(options));
        }
    }

    public bool IsClientConnected => _activePipe?.IsConnected == true;

    public event Action<HelloPayload>? ClientConnected;
    public event Action? ClientDisconnected;
    public event Action<IpcEnvelope>? MessageReceived;
    public event Action<Exception>? ClientError;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);

        while (!linked.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _options.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            _activePipe = pipe;
            try
            {
                await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
                await HandleClientAsync(pipe, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                ClientError?.Invoke(exception);
            }
            finally
            {
                _activePipe = null;
                ClientDisconnected?.Invoke();
            }
        }
    }

    public async ValueTask<bool> SendAsync(
        IpcEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var pipe = _activePipe;
        if (pipe?.IsConnected != true)
        {
            return false;
        }

        await WriteLockedAsync(pipe, envelope, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _disposeCancellation.Cancel();
        _activePipe?.Dispose();
        _disposeCancellation.Dispose();
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var first = await ReadWithHeartbeatAsync(pipe, cancellationToken).ConfigureAwait(false);
        var version = IpcProtocolVersion.Validate(first);
        if (!version.IsCompatible)
        {
            await WriteLockedAsync(
                pipe,
                IpcEnvelope.Create(
                    IpcMessageType.Error,
                    NextSequence(),
                    new ErrorPayload("protocol_version_mismatch", version.Error!)),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (first.MessageType != IpcMessageType.Hello)
        {
            await WriteLockedAsync(
                pipe,
                IpcEnvelope.Create(
                    IpcMessageType.Error,
                    NextSequence(),
                    new ErrorPayload("hello_required", "The first IPC message must be Hello.")),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var hello = first.DeserializePayload<HelloPayload>();
        ClientConnected?.Invoke(hello);
        await WriteLockedAsync(
            pipe,
            IpcEnvelope.Create(IpcMessageType.HelloAck, NextSequence(), _options.HelloAckFactory()),
            cancellationToken).ConfigureAwait(false);

        while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReadWithHeartbeatAsync(pipe, cancellationToken).ConfigureAwait(false);
            var messageVersion = IpcProtocolVersion.Validate(message);
            if (!messageVersion.IsCompatible)
            {
                await WriteLockedAsync(
                    pipe,
                    IpcEnvelope.Create(
                        IpcMessageType.Error,
                        NextSequence(),
                        new ErrorPayload("protocol_version_mismatch", messageVersion.Error!)),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            switch (message.MessageType)
            {
                case IpcMessageType.Ping:
                    var ping = message.DeserializePayload<PingPayload>();
                    await WriteLockedAsync(
                        pipe,
                        IpcEnvelope.Create(
                            IpcMessageType.Pong,
                            NextSequence(),
                            new PongPayload(ping.ClientTimestampUnixMilliseconds)),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case IpcMessageType.Shutdown:
                    MessageReceived?.Invoke(message);
                    return;
                default:
                    MessageReceived?.Invoke(message);
                    break;
            }
        }
    }

    private async ValueTask<IpcEnvelope> ReadWithHeartbeatAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeat.CancelAfter(_options.HeartbeatTimeout);
        try
        {
            return await IpcStream.ReadAsync(stream, heartbeat.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Unity heartbeat timed out after {_options.HeartbeatTimeout.TotalSeconds:0.###} seconds.");
        }
    }

    private async ValueTask WriteLockedAsync(
        Stream stream,
        IpcEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await IpcStream.WriteAsync(stream, envelope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private long NextSequence() => Interlocked.Increment(ref _sequence);
}

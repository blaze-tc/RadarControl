using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using Blaze.Radar;
using Newtonsoft.Json;

namespace Radar.Unity.Compatibility.Tests;

public sealed class RadarPipeClientTests
{
    [Fact]
    public async Task Client_IsNotConnectedUntilHelloAckCompletesHandshake()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var client = new RadarPipeClient(pipeName, 500, 50);

        client.Start(Hello());
        await server.WaitForConnectionAsync(cancellation.Token);
        var hello = await ReadEnvelopeAsync(server, cancellation.Token);

        Assert.Equal(RadarIpcMessageType.Hello, hello.messageType);
        Assert.False(client.IsConnected);

        await WriteEnvelopeAsync(
            server,
            RadarIpcProtocol.Create(
                RadarIpcMessageType.HelloAck,
                1,
                new RadarHelloAckPayload { bridgeVersion = "1.1.1", deviceModel = "F10", connected = true }),
            cancellation.Token);
        await WaitUntilAsync(() =>
        {
            client.DrainMainThreadEvents();
            return client.IsConnected;
        }, cancellation.Token);

        Assert.True(client.IsConnected);
        Assert.Equal("1.1.1", client.BridgeVersion);
        await client.StopAsync();
    }

    [Fact]
    public async Task Client_FaultingConnectionSubscriber_DoesNotBlockOtherSubscribers()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var client = new RadarPipeClient(pipeName, 500, 50);
        var observed = false;
        client.ConnectionChanged += _ => throw new InvalidOperationException("observer failed");
        client.ConnectionChanged += connected => observed = connected;

        client.Start(Hello());
        await server.WaitForConnectionAsync(cancellation.Token);
        await ReadEnvelopeAsync(server, cancellation.Token);
        await WriteEnvelopeAsync(
            server,
            RadarIpcProtocol.Create(
                RadarIpcMessageType.HelloAck,
                1,
                new RadarHelloAckPayload { bridgeVersion = "1.1.1", deviceModel = "F10", connected = true }),
            cancellation.Token);
        await WaitUntilAsync(() => client.IsConnected, cancellation.Token);

        var exception = Record.Exception(client.DrainMainThreadEvents);

        Assert.Null(exception);
        Assert.True(observed);
        await client.StopAsync();
    }

    [Fact]
    public async Task Client_PreservesPointerFrameEnvelopeDiagnostics()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var client = new RadarPipeClient(pipeName, 500, 50);

        client.Start(Hello());
        await server.WaitForConnectionAsync(cancellation.Token);
        await ReadEnvelopeAsync(server, cancellation.Token);
        await WriteEnvelopeAsync(
            server,
            RadarIpcProtocol.Create(
                RadarIpcMessageType.HelloAck,
                1,
                new RadarHelloAckPayload { bridgeVersion = "1.1.1", deviceModel = "F10", connected = true }),
            cancellation.Token);
        await WaitUntilAsync(() => client.IsConnected, cancellation.Token);

        var pointerEnvelope = RadarIpcProtocol.Create(
            RadarIpcMessageType.PointerFrame,
            42,
            new RadarPointerFrameMessage
            {
                pointers =
                {
                    new RadarPointerMessage
                    {
                        pointerId = 7,
                        normalizedX = 0.25f,
                        normalizedY = 0.75f,
                        phase = RadarPointerPhase.Down,
                        confidence = 0.91f,
                        timestampUnixMilliseconds = 1_710_000_000_100
                    }
                }
            });
        pointerEnvelope.timestampUnixMilliseconds = 1_710_000_000_123;
        await WriteEnvelopeAsync(server, pointerEnvelope, cancellation.Token);

        RadarPointerFrameMessage? received = null;
        await WaitUntilAsync(() =>
        {
            if (!client.TryConsumeLatestFrame(out var frame))
            {
                return false;
            }

            received = frame;
            return true;
        }, cancellation.Token);

        Assert.NotNull(received);
        Assert.Equal(42, received.sequence);
        Assert.Equal(1_710_000_000_123, received.timestampUnixMilliseconds);
        Assert.Single(received.pointers);
        Assert.Equal(7, received.pointers[0].pointerId);
        await client.StopAsync();
    }

    [Fact]
    public async Task Client_StartDuringStop_DoesNotCreateAnOverlappingConnectionLoop()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new RadarPipeClient(pipeName, 500, 50);

        await using (var firstServer = new NamedPipeServerStream(
                         pipeName,
                         PipeDirection.InOut,
                         1,
                         PipeTransmissionMode.Byte,
                         PipeOptions.Asynchronous))
        {
            client.Start(Hello());
            await firstServer.WaitForConnectionAsync(cancellation.Token);
            await ReadEnvelopeAsync(firstServer, cancellation.Token);

            var stopTask = client.StopAsync();
            client.Start(Hello());
            await stopTask;
        }

        await using var probeServer = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var probeTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probeServer.WaitForConnectionAsync(probeTimeout.Token));
        Assert.False(client.IsConnected);
    }

    private static RadarHelloPayload Hello() => new()
    {
        unityProcessId = Environment.ProcessId,
        unityVersion = "2021.3",
        screenWidth = 1920,
        screenHeight = 1080
    };

    private static async Task<RadarIpcEnvelope> ReadEnvelopeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonConvert.DeserializeObject<RadarIpcEnvelope>(Encoding.UTF8.GetString(payload))!;
    }

    private static async Task WriteEnvelopeAsync(
        Stream stream,
        RadarIpcEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame.AsSpan(4));
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}

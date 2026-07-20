using System.IO.Pipes;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Ipc;

namespace Yuexin.Radar.Ipc.Tests;

public sealed class RadarPipeServerTests
{
    [Fact]
    public async Task Server_HandshakesAndRespondsToPing()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new RadarPipeServer(new RadarPipeServerOptions
        {
            PipeName = pipeName,
            HeartbeatTimeout = TimeSpan.FromSeconds(2),
            HelloAckFactory = () => new HelloAckPayload("1.0.0", "F10", true)
        });
        var runTask = server.RunAsync(cancellation.Token);

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellation.Token);
        await IpcStream.WriteAsync(
            client,
            IpcEnvelope.Create(IpcMessageType.Hello, 1, new HelloPayload(42, "2021.3", 1920, 1080)),
            cancellation.Token);

        var ack = await IpcStream.ReadAsync(client, cancellation.Token);
        Assert.Equal(IpcMessageType.HelloAck, ack.MessageType);
        Assert.Equal("F10", ack.DeserializePayload<HelloAckPayload>().DeviceModel);

        await IpcStream.WriteAsync(
            client,
            IpcEnvelope.Create(IpcMessageType.Ping, 2, new PingPayload(123)),
            cancellation.Token);
        var pong = await IpcStream.ReadAsync(client, cancellation.Token);

        Assert.Equal(IpcMessageType.Pong, pong.MessageType);
        Assert.Equal(123, pong.DeserializePayload<PongPayload>().ClientTimestampUnixMilliseconds);

        cancellation.Cancel();
        await runTask;
    }

    [Fact]
    public async Task Server_ReturnsErrorForIncompatibleProtocol()
    {
        var pipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var server = new RadarPipeServer(new RadarPipeServerOptions { PipeName = pipeName });
        var runTask = server.RunAsync(cancellation.Token);

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellation.Token);
        await IpcStream.WriteAsync(
            client,
            IpcEnvelope.Create(IpcMessageType.Hello, 1, new { }, protocolVersion: 99),
            cancellation.Token);

        var error = await IpcStream.ReadAsync(client, cancellation.Token);

        Assert.Equal(IpcMessageType.Error, error.MessageType);
        Assert.Contains("99", error.DeserializePayload<ErrorPayload>().Message);

        cancellation.Cancel();
        await runTask;
    }
}

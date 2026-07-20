using System.Buffers.Binary;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Ipc;

namespace Yuexin.Radar.Ipc.Tests;

public sealed class IpcFrameCodecTests
{
    [Fact]
    public void EncodeAndAppend_RoundTripsEnvelopeAcrossHalfPackets()
    {
        var envelope = IpcEnvelope.Create(
            IpcMessageType.Hello,
            sequence: 7,
            new HelloPayload(1234, "2021.3.45f1", 1920, 1080),
            timestampUnixMilliseconds: 1000);
        var bytes = IpcFrameCodec.Encode(envelope);
        var decoder = new IpcFrameDecoder();

        Assert.Equal(bytes.Length - 4, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Empty(decoder.Append(bytes.AsSpan(0, 3)));
        var decoded = Assert.Single(decoder.Append(bytes.AsSpan(3)));
        var hello = decoded.DeserializePayload<HelloPayload>();

        Assert.Equal(IpcProtocolVersion.Current, decoded.ProtocolVersion);
        Assert.Equal(IpcMessageType.Hello, decoded.MessageType);
        Assert.Equal(7, decoded.Sequence);
        Assert.Equal(1234, hello.UnityProcessId);
        Assert.Equal(0, decoder.BufferedByteCount);
    }

    [Fact]
    public void Append_DecodesMultipleStickyMessages()
    {
        var first = IpcFrameCodec.Encode(IpcEnvelope.Create(IpcMessageType.Ping, 1, new PingPayload(10)));
        var second = IpcFrameCodec.Encode(IpcEnvelope.Create(IpcMessageType.Pong, 2, new PongPayload(10)));
        var decoder = new IpcFrameDecoder();

        var decoded = decoder.Append(first.Concat(second).ToArray());

        Assert.Equal([IpcMessageType.Ping, IpcMessageType.Pong], decoded.Select(message => message.MessageType));
    }

    [Fact]
    public void Append_RejectsInvalidLengthBeforeAllocatingPayload()
    {
        var decoder = new IpcFrameDecoder(maximumPayloadLength: 1024);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, 1025);

        Assert.Throws<InvalidDataException>(() => decoder.Append(length));
    }

    [Fact]
    public void Append_RejectsMalformedJson()
    {
        var decoder = new IpcFrameDecoder();
        var bytes = new byte[7];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        bytes[4] = (byte)'n';
        bytes[5] = (byte)'o';
        bytes[6] = (byte)'!';

        Assert.Throws<InvalidDataException>(() => decoder.Append(bytes));
    }

    [Fact]
    public void ProtocolVersion_RejectsIncompatiblePeer()
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.Hello, 1, new { }, protocolVersion: 999);

        var result = IpcProtocolVersion.Validate(envelope);

        Assert.False(result.IsCompatible);
        Assert.Contains("999", result.Error);
    }
}

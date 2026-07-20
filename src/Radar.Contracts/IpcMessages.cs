using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yuexin.Radar.Contracts;

public enum IpcMessageType
{
    Hello = 1,
    HelloAck = 2,
    Status = 3,
    PointerFrame = 4,
    RawScanFrame = 5,
    ConfigurationChanged = 6,
    Error = 7,
    Ping = 8,
    Pong = 9,
    Shutdown = 10
}

public sealed record IpcEnvelope(
    int ProtocolVersion,
    IpcMessageType MessageType,
    long Sequence,
    long TimestampUnixMilliseconds,
    JsonElement Payload)
{
    public static IpcEnvelope Create<TPayload>(
        IpcMessageType messageType,
        long sequence,
        TPayload payload,
        long? timestampUnixMilliseconds = null,
        int protocolVersion = 1)
    {
        return new IpcEnvelope(
            protocolVersion,
            messageType,
            sequence,
            timestampUnixMilliseconds ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            JsonSerializer.SerializeToElement(payload, IpcContractJson.Options));
    }

    public TPayload DeserializePayload<TPayload>()
    {
        return Payload.Deserialize<TPayload>(IpcContractJson.Options)
            ?? throw new InvalidDataException($"The {MessageType} payload is empty or invalid.");
    }
}

public sealed record HelloPayload(
    int UnityProcessId,
    string UnityVersion,
    int ScreenWidth,
    int ScreenHeight);

public sealed record HelloAckPayload(
    string BridgeVersion,
    string DeviceModel,
    bool Connected);

public sealed record PointerFramePayload(IReadOnlyList<RadarPointer> Pointers);
public sealed record PingPayload(long ClientTimestampUnixMilliseconds);
public sealed record PongPayload(long ClientTimestampUnixMilliseconds);
public sealed record ErrorPayload(string Code, string Message);
public sealed record ConfigurationChangedPayload(int SchemaVersion, RadarModel DeviceModel);

internal static class IpcContractJson
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

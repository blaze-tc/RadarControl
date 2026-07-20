#nullable disable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Blaze.Radar
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RadarIpcMessageType
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

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RadarPointerPhase
    {
        Hover = 0,
        Down = 1,
        Move = 2,
        Up = 3
    }

    [Serializable]
    public sealed class RadarIpcEnvelope
    {
        public int protocolVersion = 1;
        public RadarIpcMessageType messageType;
        public long sequence;
        public long timestampUnixMilliseconds;
        public JToken payload;
    }

    [Serializable]
    public sealed class RadarHelloPayload
    {
        public int unityProcessId;
        public string unityVersion;
        public int screenWidth;
        public int screenHeight;
    }

    [Serializable]
    public sealed class RadarHelloAckPayload
    {
        public string bridgeVersion;
        public string deviceModel;
        public bool connected;
    }

    [Serializable]
    public sealed class RadarPointerMessage
    {
        public int pointerId;
        public float normalizedX;
        public float normalizedY;
        public RadarPointerPhase phase;
        public float confidence;
        public long timestampUnixMilliseconds;
    }

    [Serializable]
    public sealed class RadarPointerFrameMessage
    {
        public long sequence;
        public long timestampUnixMilliseconds;
        public List<RadarPointerMessage> pointers = new List<RadarPointerMessage>();
    }

    [Serializable]
    public sealed class RadarPingPayload
    {
        public long clientTimestampUnixMilliseconds;
    }

    [Serializable]
    public sealed class RadarErrorPayload
    {
        public string code;
        public string message;
    }

    public static class RadarIpcProtocol
    {
        public const int Version = 1;

        public static RadarIpcEnvelope Create(RadarIpcMessageType type, long sequence, object payload)
        {
            return new RadarIpcEnvelope
            {
                protocolVersion = Version,
                messageType = type,
                sequence = sequence,
                timestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload == null ? JValue.CreateNull() : JToken.FromObject(payload)
            };
        }
    }
}

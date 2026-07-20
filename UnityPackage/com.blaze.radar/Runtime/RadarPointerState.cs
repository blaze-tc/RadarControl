using System;
using UnityEngine;

namespace Blaze.Radar
{
    [Serializable]
    public sealed class RadarPointerState
    {
        public int PointerId;
        public Vector2 NormalizedPosition;
        public Vector2 ScreenPosition;
        public RadarPointerPhase Phase;
        public float Confidence;
        public long TimestampUnixMilliseconds;

        public void Apply(RadarPointerMessage message)
        {
            PointerId = message.pointerId;
            NormalizedPosition = new Vector2(message.normalizedX, message.normalizedY);
            ScreenPosition = new Vector2(message.normalizedX * Screen.width, message.normalizedY * Screen.height);
            Phase = message.phase;
            Confidence = message.confidence;
            TimestampUnixMilliseconds = message.timestampUnixMilliseconds;
        }
    }
}

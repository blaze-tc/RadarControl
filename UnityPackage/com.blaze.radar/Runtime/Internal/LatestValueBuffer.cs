#nullable disable

using System;

namespace Blaze.Radar.Internal
{
    public sealed class LatestValueBuffer<T>
    {
        private readonly object _gate = new object();
        private T _value;
        private bool _hasValue;

        public long DroppedCount { get; private set; }

        public void Publish(T value)
        {
            lock (_gate)
            {
                if (_hasValue)
                {
                    DroppedCount++;
                }

                _value = value;
                _hasValue = true;
            }
        }

        public bool TryConsume(out T value)
        {
            lock (_gate)
            {
                if (!_hasValue)
                {
                    value = default(T);
                    return false;
                }

                value = _value;
                _value = default(T);
                _hasValue = false;
                return true;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _value = default(T);
                _hasValue = false;
            }
        }
    }
}

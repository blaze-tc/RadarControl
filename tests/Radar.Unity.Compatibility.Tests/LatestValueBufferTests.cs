using Blaze.Radar.Internal;

namespace Blaze.Radar.Compatibility.Tests;

public sealed class LatestValueBufferTests
{
    [Fact]
    public void Publish_ReplacesUnreadFrameAndConsumeClearsIt()
    {
        var buffer = new LatestValueBuffer<int>();

        buffer.Publish(1);
        buffer.Publish(2);

        Assert.Equal(1, buffer.DroppedCount);
        Assert.True(buffer.TryConsume(out var value));
        Assert.Equal(2, value);
        Assert.False(buffer.TryConsume(out _));
    }
}

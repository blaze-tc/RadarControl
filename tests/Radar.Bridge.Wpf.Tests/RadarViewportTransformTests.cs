using System.Windows;
using Yuexin.Radar.Bridge.Wpf.Controls;
using Yuexin.Radar.Processing;

namespace Yuexin.Radar.Bridge.Wpf.Tests;

public sealed class RadarViewportTransformTests
{
    [Fact]
    public void WorldAndScreenConversions_AreInverseWithCenteredOrigin()
    {
        var screen = RadarViewportTransform.WorldToScreen(new Point2(1f, -0.5f), 200, 100, 2f);
        var world = RadarViewportTransform.ScreenToWorld(screen, 200, 100, 2f);

        Assert.Equal(new Point(125, 62.5), screen);
        Assert.Equal(1f, world.X, 5);
        Assert.Equal(-0.5f, world.Y, 5);
    }
}

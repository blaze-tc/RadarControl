using System.Windows;
using Yuexin.Radar.Processing;

namespace Yuexin.Radar.Bridge.Wpf.Controls;

public static class RadarViewportTransform
{
    public static Point WorldToScreen(Point2 world, double width, double height, float maximumRangeMeters)
    {
        var scale = CalculateScale(width, height, maximumRangeMeters);
        return new Point(
            width / 2d + world.X * scale,
            height / 2d - world.Y * scale);
    }

    public static Point2 ScreenToWorld(Point screen, double width, double height, float maximumRangeMeters)
    {
        var scale = CalculateScale(width, height, maximumRangeMeters);
        return new Point2(
            (float)((screen.X - width / 2d) / scale),
            (float)((height / 2d - screen.Y) / scale));
    }

    public static double CalculateScale(double width, double height, float maximumRangeMeters)
    {
        if (width <= 0d || height <= 0d || maximumRangeMeters <= 0f)
        {
            return 1d;
        }

        return Math.Min(width, height) / (2d * maximumRangeMeters);
    }
}

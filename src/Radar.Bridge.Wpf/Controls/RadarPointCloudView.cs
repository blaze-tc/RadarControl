using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Processing;

namespace Yuexin.Radar.Bridge.Wpf.Controls;

public sealed class RegionVertexMovedEventArgs : EventArgs
{
    public RegionVertexMovedEventArgs(int index, Point2 worldPosition)
    {
        Index = index;
        WorldPosition = worldPosition;
    }

    public int Index { get; }
    public Point2 WorldPosition { get; }
}

public sealed class RadarPointCloudView : FrameworkElement
{
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(
        nameof(Snapshot),
        typeof(RadarRuntimeSnapshot),
        typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumRangeMetersProperty = DependencyProperty.Register(
        nameof(MaximumRangeMeters),
        typeof(float),
        typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(5f, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RegionVerticesProperty = DependencyProperty.Register(
        nameof(RegionVertices),
        typeof(IReadOnlyList<Point2>),
        typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(null, OnRegionVerticesChanged));

    public static readonly DependencyProperty ShowRawPointsProperty = DependencyProperty.Register(
        nameof(ShowRawPoints), typeof(bool), typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaskedRegionsProperty = DependencyProperty.Register(
        nameof(MaskedRegions),
        typeof(IReadOnlyList<IReadOnlyList<Point2>>),
        typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowValidPointsProperty = DependencyProperty.Register(
        nameof(ShowValidPoints), typeof(bool), typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowClustersProperty = DependencyProperty.Register(
        nameof(ShowClusters), typeof(bool), typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowTargetsProperty = DependencyProperty.Register(
        nameof(ShowTargets), typeof(bool), typeof(RadarPointCloudView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush BackgroundBrush = FrozenBrush(Color.FromRgb(8, 15, 25));
    private static readonly Brush GridBrush = FrozenBrush(Color.FromArgb(72, 67, 91, 116));
    private static readonly Brush RawPointBrush = FrozenBrush(Color.FromArgb(120, 104, 139, 163));
    private static readonly Brush ValidPointBrush = FrozenBrush(Color.FromRgb(56, 211, 214));
    private static readonly Brush TargetBrush = FrozenBrush(Color.FromRgb(251, 168, 76));
    private static readonly Brush RegionBrush = FrozenBrush(Color.FromRgb(126, 231, 135));
    private static readonly Brush BlindBrush = FrozenBrush(Color.FromArgb(44, 245, 93, 91));
    private int _draggedVertex = -1;

    public RadarPointCloudView()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public RadarRuntimeSnapshot? Snapshot
    {
        get => (RadarRuntimeSnapshot?)GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public float MaximumRangeMeters
    {
        get => (float)GetValue(MaximumRangeMetersProperty);
        set => SetValue(MaximumRangeMetersProperty, value);
    }

    public IReadOnlyList<Point2>? RegionVertices
    {
        get => (IReadOnlyList<Point2>?)GetValue(RegionVerticesProperty);
        set => SetValue(RegionVerticesProperty, value);
    }

    public bool ShowRawPoints
    {
        get => (bool)GetValue(ShowRawPointsProperty);
        set => SetValue(ShowRawPointsProperty, value);
    }

    public IReadOnlyList<IReadOnlyList<Point2>>? MaskedRegions
    {
        get => (IReadOnlyList<IReadOnlyList<Point2>>?)GetValue(MaskedRegionsProperty);
        set => SetValue(MaskedRegionsProperty, value);
    }

    public bool ShowValidPoints
    {
        get => (bool)GetValue(ShowValidPointsProperty);
        set => SetValue(ShowValidPointsProperty, value);
    }

    public bool ShowClusters
    {
        get => (bool)GetValue(ShowClustersProperty);
        set => SetValue(ShowClustersProperty, value);
    }

    public bool ShowTargets
    {
        get => (bool)GetValue(ShowTargetsProperty);
        set => SetValue(ShowTargetsProperty, value);
    }

    public event EventHandler<RegionVertexMovedEventArgs>? RegionVertexMoved;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(RenderSize));
        if (ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        DrawGrid(drawingContext);
        DrawBlindZone(drawingContext);
        DrawRegion(drawingContext);
        DrawMaskedRegions(drawingContext);
        DrawSnapshot(drawingContext);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        Focus();
        var vertices = RegionVertices;
        if (vertices is null)
        {
            return;
        }

        var mouse = eventArgs.GetPosition(this);
        for (var index = 0; index < vertices.Count; index++)
        {
            var screen = ToScreen(vertices[index]);
            if ((screen - mouse).Length <= 14d)
            {
                _draggedVertex = index;
                CaptureMouse();
                eventArgs.Handled = true;
                return;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (_draggedVertex < 0 || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var world = RadarViewportTransform.ScreenToWorld(
            eventArgs.GetPosition(this),
            ActualWidth,
            ActualHeight,
            Math.Max(0.1f, MaximumRangeMeters));
        RegionVertexMoved?.Invoke(this, new RegionVertexMovedEventArgs(_draggedVertex, world));
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonUp(eventArgs);
        if (_draggedVertex >= 0)
        {
            _draggedVertex = -1;
            ReleaseMouseCapture();
            eventArgs.Handled = true;
        }
    }

    private void DrawGrid(DrawingContext drawingContext)
    {
        var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
        var maximumRange = Math.Max(0.1f, MaximumRangeMeters);
        var scale = RadarViewportTransform.CalculateScale(ActualWidth, ActualHeight, maximumRange);
        var interval = maximumRange <= 10f ? 1f : 5f;
        var gridPen = new Pen(GridBrush, 1d);
        for (var radius = interval; radius <= maximumRange + 0.001f; radius += interval)
        {
            drawingContext.DrawEllipse(null, gridPen, center, radius * scale, radius * scale);
        }

        drawingContext.DrawLine(gridPen, new Point(0d, center.Y), new Point(ActualWidth, center.Y));
        drawingContext.DrawLine(gridPen, new Point(center.X, 0d), new Point(center.X, ActualHeight));
        drawingContext.DrawEllipse(TargetBrush, null, center, 4d, 4d);

        DrawLabel(drawingContext, "0°", new Point(ActualWidth - 34d, center.Y + 5d), Brushes.LightSlateGray, 11d);
        DrawLabel(drawingContext, "90°", new Point(center.X + 6d, 6d), Brushes.LightSlateGray, 11d);
        DrawLabel(drawingContext, "180°", new Point(6d, center.Y + 5d), Brushes.LightSlateGray, 11d);
        DrawLabel(drawingContext, $"{maximumRange:0.#}m", new Point(center.X + 8d, 18d), Brushes.LightSlateGray, 11d);
    }

    private void DrawBlindZone(DrawingContext drawingContext)
    {
        var center = new Point(ActualWidth / 2d, ActualHeight / 2d);
        var radius = Math.Min(ActualWidth, ActualHeight) / 2d;
        var start = PolarScreenPoint(center, radius, 230f);
        var end = PolarScreenPoint(center, radius, 310f);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            context.LineTo(start, true, false);
            context.ArcTo(end, new Size(radius, radius), 0d, false, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(BlindBrush, new Pen(FrozenBrush(Color.FromArgb(90, 245, 93, 91)), 1d), geometry);
        DrawLabel(drawingContext, "230°-310° 盲区", new Point(center.X - 44d, ActualHeight - 28d), Brushes.IndianRed, 10d);
    }

    private void DrawRegion(DrawingContext drawingContext)
    {
        var vertices = RegionVertices;
        if (vertices is null || vertices.Count < 3)
        {
            return;
        }

        var pen = new Pen(RegionBrush, 1.5d) { DashStyle = DashStyles.Dash };
        for (var index = 0; index < vertices.Count; index++)
        {
            var current = ToScreen(vertices[index]);
            var next = ToScreen(vertices[(index + 1) % vertices.Count]);
            drawingContext.DrawLine(pen, current, next);
            drawingContext.DrawEllipse(BackgroundBrush, new Pen(RegionBrush, 2d), current, 7d, 7d);
            DrawLabel(drawingContext, (index + 1).ToString(), current + new Vector(9d, -12d), RegionBrush, 10d);
        }
    }

    private void DrawSnapshot(DrawingContext drawingContext)
    {
        var snapshot = Snapshot;
        if (snapshot is null)
        {
            DrawLabel(
                drawingContext,
                "等待雷达数据 · 可启动模拟模式",
                new Point(24d, 24d),
                Brushes.SlateGray,
                13d);
            return;
        }

        if (ShowRawPoints)
        {
            foreach (var point in snapshot.RawPoints)
            {
                drawingContext.DrawEllipse(RawPointBrush, null, ToScreen(new Point2(point.X, point.Y)), 1.5d, 1.5d);
            }
        }

        if (ShowValidPoints)
        {
            foreach (var point in snapshot.ValidPoints)
            {
                drawingContext.DrawEllipse(ValidPointBrush, null, ToScreen(new Point2(point.X, point.Y)), 2.6d, 2.6d);
            }
        }

        if (ShowClusters)
        {
            var scale = RadarViewportTransform.CalculateScale(ActualWidth, ActualHeight, Math.Max(0.1f, MaximumRangeMeters));
            foreach (var cluster in snapshot.Clusters)
            {
                var center = ToScreen(new Point2(cluster.CenterX, cluster.CenterY));
                var size = Math.Max(12d, cluster.WidthMeters * scale);
                drawingContext.DrawRectangle(null, new Pen(Brushes.MediumPurple, 1.2d),
                    new Rect(center.X - size / 2d, center.Y - size / 2d, size, size));
            }
        }

        if (ShowTargets)
        {
            foreach (var target in snapshot.Targets)
            {
                var position = ToScreen(new Point2(target.PhysicalX, target.PhysicalY));
                drawingContext.DrawEllipse(null, new Pen(TargetBrush, 2d), position, 9d, 9d);
                var normalized = float.IsFinite(target.NormalizedX)
                    ? $" · {target.NormalizedX:0.000}, {target.NormalizedY:0.000}"
                    : string.Empty;
                DrawLabel(drawingContext, $"T{target.TrackId}{normalized}", position + new Vector(12d, -16d), TargetBrush, 11d);
            }
        }
    }

    private void DrawMaskedRegions(DrawingContext drawingContext)
    {
        if (MaskedRegions is null)
        {
            return;
        }

        var fill = FrozenBrush(Color.FromArgb(38, 245, 93, 91));
        var pen = new Pen(FrozenBrush(Color.FromArgb(190, 245, 93, 91)), 1.3d);
        foreach (var polygon in MaskedRegions)
        {
            if (polygon.Count < 3)
            {
                continue;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(ToScreen(polygon[0]), true, true);
                for (var index = 1; index < polygon.Count; index++)
                {
                    context.LineTo(ToScreen(polygon[index]), true, false);
                }
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(fill, pen, geometry);
        }
    }

    private Point ToScreen(Point2 world)
    {
        return RadarViewportTransform.WorldToScreen(
            world,
            ActualWidth,
            ActualHeight,
            Math.Max(0.1f, MaximumRangeMeters));
    }

    private static Point PolarScreenPoint(Point center, double radius, float degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y - radius * Math.Sin(radians));
    }

    private static void DrawLabel(
        DrawingContext drawingContext,
        string text,
        Point origin,
        Brush brush,
        double fontSize)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            1.0);
        drawingContext.DrawText(formatted, origin);
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnRegionVerticesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (RadarPointCloudView)dependencyObject;
        if (eventArgs.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnRegionCollectionChanged;
        }

        if (eventArgs.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnRegionCollectionChanged;
        }

        control.InvalidateVisual();
    }

    private void OnRegionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        InvalidateVisual();
    }
}

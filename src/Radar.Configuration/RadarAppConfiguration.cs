using Yuexin.Radar.Contracts;

namespace Yuexin.Radar.Configuration;

public sealed class RadarAppConfiguration
{
    public int SchemaVersion { get; set; } = 1;
    public RadarDeviceConfiguration Device { get; set; } = new();
    public RadarTransformConfiguration Transform { get; set; } = new();
    public RadarRangeConfiguration Range { get; set; } = new();
    public RadarClusteringConfiguration Clustering { get; set; } = new();
    public RadarTrackingConfiguration Tracking { get; set; } = new();
    public RadarInteractionConfiguration Interaction { get; set; } = new();
    public RadarIpcConfiguration Ipc { get; set; } = new();
    public RadarCalibrationConfiguration Calibration { get; set; } = new();

    public static RadarAppConfiguration CreateDefault() => new();
}

public sealed class RadarDeviceConfiguration
{
    public RadarModel DeviceModel { get; set; } = RadarModel.F10;
    public string RadarIp { get; set; } = "192.168.0.100";
    public int Port { get; set; } = 8487;
    public string LocalIp { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
}

public sealed class RadarTransformConfiguration
{
    public float RotationDegrees { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public float OffsetXMeters { get; set; }
    public float OffsetYMeters { get; set; }
}

public sealed class RadarRangeConfiguration
{
    public float MinimumDistanceMeters { get; set; } = 0.1f;
    public float MaximumDistanceMeters { get; set; } = 5f;
    public float VisualizationRangeMeters { get; set; } = 4f;
    public float MinimumAngleDegrees { get; set; }
    public float MaximumAngleDegrees { get; set; } = 360f;
    public List<RadarPoint2> ActivePolygon { get; set; } = [];
    public List<List<RadarPoint2>> MaskedPolygons { get; set; } = [];
    public RadarEdgeDeadZoneConfiguration EdgeDeadZones { get; set; } = new();
}

public sealed class RadarEdgeDeadZoneConfiguration
{
    public float LeftMeters { get; set; }
    public float RightMeters { get; set; }
    public float TopMeters { get; set; }
    public float BottomMeters { get; set; }
}

public readonly record struct RadarPoint2(float X, float Y);

public sealed class RadarClusteringConfiguration
{
    public float BaseGapMeters { get; set; } = 0.08f;
    public float DistanceScale { get; set; } = 0.015f;
    public int MinimumClusterPointCount { get; set; } = 2;
    public float MaximumClusterWidthMeters { get; set; } = 0.8f;
}

public sealed class RadarTrackingConfiguration
{
    public int ConfirmFrames { get; set; } = 2;
    public int LostFrames { get; set; } = 3;
    public float MaximumAssociationDistanceMeters { get; set; } = 0.35f;
    public float SmoothingAlpha { get; set; } = 0.5f;
}

public sealed class RadarInteractionConfiguration
{
    public RadarInteractionMode Mode { get; set; } = RadarInteractionMode.Touch;
    public int DwellMilliseconds { get; set; } = 800;
    public float DwellRadiusNormalized { get; set; } = 0.03f;
    public float DragThresholdNormalized { get; set; } = 0.015f;
    public int MinimumPressMilliseconds { get; set; } = 30;
    public float MaximumClickMovementNormalized { get; set; } = 0.03f;
}

public sealed class RadarIpcConfiguration
{
    public string PipeName { get; set; } = "Yuexin.RadarBridge";
    public bool SendRawPoints { get; set; }
    public bool SendClusters { get; set; }
}

public sealed class RadarCalibrationConfiguration
{
    public bool IsValid { get; set; }
    public RadarModel DeviceModel { get; set; } = RadarModel.F10;
    public List<RadarPoint2> PhysicalCorners { get; set; } = [];
    public List<double> HomographyMatrix { get; set; } = [];
    public DateTimeOffset? CreatedAt { get; set; }
    public RadarTransformConfiguration TransformSnapshot { get; set; } = new();
    public double MaximumCornerError { get; set; }
}

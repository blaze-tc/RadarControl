using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Bridge.Wpf.ViewModels;
using Yuexin.Radar.Configuration;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Processing;

namespace Yuexin.Radar.Bridge.Wpf.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Constructor_SelectsF10AndShowsDocumentedProfile()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        Assert.Equal(RadarModel.F10, viewModel.SelectedModel);
        Assert.Equal("FaseLase F10", viewModel.ModelDisplayName);
        Assert.Equal("10-25 Hz / 默认 15 Hz", viewModel.ScanFrequencyDescription);
        Assert.Equal("0.27°", viewModel.AngularResolutionDescription);
        Assert.Equal(10f, viewModel.ModelMaximumDistanceMeters);
    }

    [Fact]
    public void SelectingF20_RefreshesProfileAndConfiguration()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        viewModel.SelectedModel = RadarModel.F20;

        Assert.Equal(RadarModel.F20, configuration.Device.DeviceModel);
        Assert.Equal("FaseLase F20", viewModel.ModelDisplayName);
        Assert.Equal("10-30 Hz / 默认 25 Hz", viewModel.ScanFrequencyDescription);
        Assert.Equal(40f, viewModel.ModelMaximumDistanceMeters);
    }

    [Fact]
    public void SwitchingBackToF10_ClampsSoftwareRangeWithoutDeletingCalibration()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Device.DeviceModel = RadarModel.F20;
        configuration.Range.MaximumDistanceMeters = 20f;
        configuration.Calibration.IsValid = true;
        configuration.Calibration.DeviceModel = RadarModel.F20;
        configuration.Calibration.PhysicalCorners = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];
        configuration.Calibration.HomographyMatrix = [1, 0, 0, 0, 1, 0, 0, 0, 1];
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        viewModel.SelectedModel = RadarModel.F10;

        Assert.Equal(10f, configuration.Range.MaximumDistanceMeters);
        Assert.True(configuration.Calibration.IsValid);
        Assert.Contains("重新标定", viewModel.CalibrationStatus);
    }

    [Fact]
    public void RuntimeSnapshot_UpdatesMetricsAndKeepsOnlyLastFiveHundredLogs()
    {
        var runtime = new TestRuntime();
        var viewModel = new MainViewModel(RadarAppConfiguration.CreateDefault(), runtime);

        runtime.PublishSnapshot(new RadarRuntimeSnapshot(
            Sequence: 9,
            Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(1000),
            RawPoints: [],
            ValidPoints: [],
            Clusters: [],
            Targets: [],
            Pointers: [],
            ScanFrequencyHz: 15.2,
            ReceivedBytesPerSecond: 4096));
        for (var index = 0; index < 550; index++)
        {
            runtime.PublishLog($"log-{index}");
        }

        Assert.Equal(9, viewModel.LastFrameSequence);
        Assert.Equal("15.2 Hz", viewModel.ActualScanFrequency);
        Assert.Equal("4.0 KB/s", viewModel.ReceiveRate);
        Assert.Equal(500, viewModel.LogEntries.Count);
        Assert.Equal("log-549", viewModel.LogEntries[^1]);
    }

    [Fact]
    public void UpdateRegionVertex_PersistsDraggedVertex()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        viewModel.UpdateRegionVertex(0, new Point2(-1.25f, 2.5f));

        Assert.Equal(-1.25f, viewModel.RegionVertices[0].X);
        Assert.Equal(2.5f, configuration.Range.ActivePolygon[0].Y);
    }

    [Fact]
    public void Calibration_CapturesFourCornersAndSavesValidHomography()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        viewModel.BeginCalibration();
        viewModel.CaptureCalibrationPoint(new Point2(-1f, 1f));
        viewModel.CaptureCalibrationPoint(new Point2(1f, 1f));
        viewModel.CaptureCalibrationPoint(new Point2(1f, -1f));
        viewModel.CaptureCalibrationPoint(new Point2(-1f, -1f));
        var saved = viewModel.SaveCalibration();

        Assert.True(saved);
        Assert.True(configuration.Calibration.IsValid);
        Assert.Equal(RadarModel.F10, configuration.Calibration.DeviceModel);
        Assert.Equal(4, configuration.Calibration.PhysicalCorners.Count);
        Assert.Contains("已标定", viewModel.CalibrationStatus);
    }

    [Fact]
    public void MaskRegion_CanBeAddedAndDeletedAroundSelectedPoint()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        var viewModel = new MainViewModel(configuration, new TestRuntime());

        viewModel.AddMaskedRegion(new Point2(1f, 2f), halfSizeMeters: 0.2f);

        Assert.Equal(1, viewModel.MaskedRegionCount);
        Assert.Equal(4, configuration.Range.MaskedPolygons[0].Count);
        Assert.True(viewModel.DeleteLastMaskedRegion());
        Assert.Equal(0, viewModel.MaskedRegionCount);
    }

    private sealed class TestRuntime : IRadarBridgeRuntime
    {
        public event Action<RadarRuntimeSnapshot>? SnapshotUpdated;
        public event Action<string>? LogReceived;
        public event Action<RadarConnectionState>? ConnectionStateChanged;
        public event Action<UnityClientStatus>? UnityStatusChanged;

        public RadarConnectionState ConnectionState => RadarConnectionState.Disconnected;
        public UnityClientStatus UnityStatus { get; } = UnityClientStatus.Disconnected;

        public Task StartInfrastructureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task StartSimulationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopSimulationAsync() => Task.CompletedTask;
        public Task StartRecordingAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopRecordingAsync() => Task.CompletedTask;
        public Task ReplayAsync(string path, double speed, bool loop, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PauseReplay() { }
        public void ResumeReplay() { }
        public void StepReplay() { }
        public Task StopReplayAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void PublishSnapshot(RadarRuntimeSnapshot snapshot) => SnapshotUpdated?.Invoke(snapshot);
        public void PublishLog(string message) => LogReceived?.Invoke(message);
        public void PublishConnectionState(RadarConnectionState state) => ConnectionStateChanged?.Invoke(state);
        public void PublishUnityStatus(UnityClientStatus status) => UnityStatusChanged?.Invoke(status);
    }
}

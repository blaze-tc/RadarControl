using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Input;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Configuration;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Device;
using Yuexin.Radar.Processing;

namespace Yuexin.Radar.Bridge.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int MaximumVisibleLogEntries = 500;
    private readonly RadarAppConfiguration _configuration;
    private readonly IRadarBridgeRuntime _runtime;
    private readonly SynchronizationContext? _synchronizationContext;
    private IRadarModelProfile _modelProfile;
    private RadarModel _selectedModel;
    private string _connectionStatus = "未连接";
    private string _calibrationStatus = "未标定";
    private string _actualScanFrequency = "0.0 Hz";
    private string _receiveRate = "0 B/s";
    private long _lastFrameSequence;
    private RadarRuntimeSnapshot? _latestSnapshot;
    private UnityClientStatus _unityStatus;
    private readonly List<Point2> _capturedCalibrationPoints = [];
    private string _calibrationStep = "未开始";

    public MainViewModel(RadarAppConfiguration configuration, IRadarBridgeRuntime runtime)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _synchronizationContext = SynchronizationContext.Current;
        _selectedModel = configuration.Device.DeviceModel;
        _modelProfile = RadarModelProfileFactory.Create(_selectedModel);
        _unityStatus = runtime.UnityStatus;
        InitializeRegionVertices();
        RefreshCalibrationStatus(modelChanged: false);

        ConnectCommand = CreateAsyncCommand(() => _runtime.ConnectAsync());
        DisconnectCommand = CreateAsyncCommand(_runtime.DisconnectAsync);
        StartSimulationCommand = CreateAsyncCommand(() => _runtime.StartSimulationAsync());
        StopSimulationCommand = CreateAsyncCommand(_runtime.StopSimulationAsync);
        SaveConfigurationCommand = CreateAsyncCommand(SaveConfigurationAsync);
        ResetRegionCommand = new RelayCommand(ResetRegion);
        BeginCalibrationCommand = new RelayCommand(BeginCalibration);
        CaptureCalibrationPointCommand = new RelayCommand(
            () => CaptureCurrentTargetForCalibration(),
            () => LatestSnapshot?.Targets.Count > 0);
        UndoCalibrationPointCommand = new RelayCommand(UndoCalibrationPoint);
        SaveCalibrationCommand = new RelayCommand(() => SaveCalibration());
        ClearCalibrationCommand = new RelayCommand(ClearCalibration);
        AddMaskedRegionCommand = new RelayCommand(
            () => AddMaskedRegionAtCurrentTarget(),
            () => LatestSnapshot?.Targets.Count > 0);
        DeleteMaskedRegionCommand = new RelayCommand(() => DeleteLastMaskedRegion(), () => MaskedRegionCount > 0);

        _runtime.SnapshotUpdated += OnSnapshotUpdated;
        _runtime.LogReceived += OnLogReceived;
        _runtime.ConnectionStateChanged += OnConnectionStateChanged;
        _runtime.UnityStatusChanged += OnUnityStatusChanged;
    }

    public IReadOnlyList<RadarModel> AvailableModels { get; } = [RadarModel.F10, RadarModel.F20];
    public IReadOnlyList<RadarInteractionMode> AvailableInteractionModes { get; } =
        Enum.GetValues<RadarInteractionMode>();
    public IReadOnlyList<string> AvailableLocalIps { get; } = DiscoverLocalIps();
    public ObservableCollection<string> LogEntries { get; } = [];
    public ObservableCollection<Point2> RegionVertices { get; } = [];

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand StartSimulationCommand { get; }
    public ICommand StopSimulationCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand ResetRegionCommand { get; }
    public ICommand BeginCalibrationCommand { get; }
    public ICommand CaptureCalibrationPointCommand { get; }
    public ICommand UndoCalibrationPointCommand { get; }
    public ICommand SaveCalibrationCommand { get; }
    public ICommand ClearCalibrationCommand { get; }
    public ICommand AddMaskedRegionCommand { get; }
    public ICommand DeleteMaskedRegionCommand { get; }

    public RadarModel SelectedModel
    {
        get => _selectedModel;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : RadarModel.F10;
            if (!SetProperty(ref _selectedModel, normalized))
            {
                return;
            }

            _configuration.Device.DeviceModel = normalized;
            _modelProfile = RadarModelProfileFactory.Create(normalized);
            ConfigurationValidator.ValidateAndNormalize(_configuration);
            OnPropertyChanged(nameof(ModelDisplayName));
            OnPropertyChanged(nameof(ModelMaximumDistanceMeters));
            OnPropertyChanged(nameof(ScanFrequencyDescription));
            OnPropertyChanged(nameof(AngularResolutionDescription));
            OnPropertyChanged(nameof(MaximumDistanceMeters));
            RefreshCalibrationStatus(modelChanged: true);
            AddLog($"雷达型号已切换为 {_modelProfile.DisplayName}；协议解析公式保持不变。");
        }
    }

    public string ModelDisplayName => _modelProfile.DisplayName;
    public float ModelMaximumDistanceMeters => _modelProfile.MaximumDistanceMeters;
    public string ScanFrequencyDescription =>
        $"{_modelProfile.MinimumScanFrequencyHz}-{_modelProfile.MaximumScanFrequencyHz} Hz / 默认 {_modelProfile.DefaultScanFrequencyHz} Hz";
    public string AngularResolutionDescription => $"{_modelProfile.DefaultAngularResolutionDegrees:0.##}°";

    public string RadarIp
    {
        get => _configuration.Device.RadarIp;
        set
        {
            if (_configuration.Device.RadarIp == value)
            {
                return;
            }

            _configuration.Device.RadarIp = value;
            OnPropertyChanged();
        }
    }

    public int Port
    {
        get => _configuration.Device.Port;
        set
        {
            if (_configuration.Device.Port == value)
            {
                return;
            }

            _configuration.Device.Port = value;
            OnPropertyChanged();
        }
    }

    public string LocalIp
    {
        get => _configuration.Device.LocalIp;
        set
        {
            if (_configuration.Device.LocalIp == value)
            {
                return;
            }

            _configuration.Device.LocalIp = value;
            OnPropertyChanged();
        }
    }

    public bool AutoReconnect
    {
        get => _configuration.Device.AutoReconnect;
        set
        {
            if (_configuration.Device.AutoReconnect == value)
            {
                return;
            }

            _configuration.Device.AutoReconnect = value;
            OnPropertyChanged();
        }
    }

    public float MinimumDistanceMeters
    {
        get => _configuration.Range.MinimumDistanceMeters;
        set
        {
            if (Math.Abs(_configuration.Range.MinimumDistanceMeters - value) < float.Epsilon)
            {
                return;
            }

            _configuration.Range.MinimumDistanceMeters = value;
            OnPropertyChanged();
        }
    }

    public float MaximumDistanceMeters
    {
        get => _configuration.Range.MaximumDistanceMeters;
        set
        {
            var clamped = Math.Clamp(value, _configuration.Range.MinimumDistanceMeters, _modelProfile.MaximumDistanceMeters);
            if (Math.Abs(_configuration.Range.MaximumDistanceMeters - clamped) < float.Epsilon)
            {
                return;
            }

            _configuration.Range.MaximumDistanceMeters = clamped;
            OnPropertyChanged();
        }
    }

    public float RotationDegrees
    {
        get => _configuration.Transform.RotationDegrees;
        set
        {
            if (Math.Abs(_configuration.Transform.RotationDegrees - value) < float.Epsilon) return;
            _configuration.Transform.RotationDegrees = value;
            OnPropertyChanged();
        }
    }

    public bool FlipX
    {
        get => _configuration.Transform.FlipX;
        set
        {
            if (_configuration.Transform.FlipX == value) return;
            _configuration.Transform.FlipX = value;
            OnPropertyChanged();
        }
    }

    public bool FlipY
    {
        get => _configuration.Transform.FlipY;
        set
        {
            if (_configuration.Transform.FlipY == value) return;
            _configuration.Transform.FlipY = value;
            OnPropertyChanged();
        }
    }

    public float OffsetXMeters
    {
        get => _configuration.Transform.OffsetXMeters;
        set
        {
            if (Math.Abs(_configuration.Transform.OffsetXMeters - value) < float.Epsilon) return;
            _configuration.Transform.OffsetXMeters = value;
            OnPropertyChanged();
        }
    }

    public float OffsetYMeters
    {
        get => _configuration.Transform.OffsetYMeters;
        set
        {
            if (Math.Abs(_configuration.Transform.OffsetYMeters - value) < float.Epsilon) return;
            _configuration.Transform.OffsetYMeters = value;
            OnPropertyChanged();
        }
    }

    public RadarInteractionMode InteractionMode
    {
        get => _configuration.Interaction.Mode;
        set
        {
            if (_configuration.Interaction.Mode == value) return;
            _configuration.Interaction.Mode = value;
            OnPropertyChanged();
        }
    }

    public int DwellMilliseconds
    {
        get => _configuration.Interaction.DwellMilliseconds;
        set
        {
            var normalized = Math.Max(0, value);
            if (_configuration.Interaction.DwellMilliseconds == normalized) return;
            _configuration.Interaction.DwellMilliseconds = normalized;
            OnPropertyChanged();
        }
    }

    public float BaseGapMeters
    {
        get => _configuration.Clustering.BaseGapMeters;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_configuration.Clustering.BaseGapMeters - normalized) < float.Epsilon) return;
            _configuration.Clustering.BaseGapMeters = normalized;
            OnPropertyChanged();
        }
    }

    public float DistanceScale
    {
        get => _configuration.Clustering.DistanceScale;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_configuration.Clustering.DistanceScale - normalized) < float.Epsilon) return;
            _configuration.Clustering.DistanceScale = normalized;
            OnPropertyChanged();
        }
    }

    public float MinimumAngleDegrees
    {
        get => _configuration.Range.MinimumAngleDegrees;
        set
        {
            var normalized = Math.Clamp(value, 0f, 360f);
            if (Math.Abs(_configuration.Range.MinimumAngleDegrees - normalized) < float.Epsilon) return;
            _configuration.Range.MinimumAngleDegrees = normalized;
            OnPropertyChanged();
        }
    }

    public float MaximumAngleDegrees
    {
        get => _configuration.Range.MaximumAngleDegrees;
        set
        {
            var normalized = Math.Clamp(value, 0f, 360f);
            if (Math.Abs(_configuration.Range.MaximumAngleDegrees - normalized) < float.Epsilon) return;
            _configuration.Range.MaximumAngleDegrees = normalized;
            OnPropertyChanged();
        }
    }

    public float LeftEdgeDeadZoneMeters
    {
        get => _configuration.Range.EdgeDeadZones.LeftMeters;
        set => SetEdgeDeadZone(value, _configuration.Range.EdgeDeadZones.LeftMeters, normalized => _configuration.Range.EdgeDeadZones.LeftMeters = normalized);
    }

    public float RightEdgeDeadZoneMeters
    {
        get => _configuration.Range.EdgeDeadZones.RightMeters;
        set => SetEdgeDeadZone(value, _configuration.Range.EdgeDeadZones.RightMeters, normalized => _configuration.Range.EdgeDeadZones.RightMeters = normalized);
    }

    public float TopEdgeDeadZoneMeters
    {
        get => _configuration.Range.EdgeDeadZones.TopMeters;
        set => SetEdgeDeadZone(value, _configuration.Range.EdgeDeadZones.TopMeters, normalized => _configuration.Range.EdgeDeadZones.TopMeters = normalized);
    }

    public float BottomEdgeDeadZoneMeters
    {
        get => _configuration.Range.EdgeDeadZones.BottomMeters;
        set => SetEdgeDeadZone(value, _configuration.Range.EdgeDeadZones.BottomMeters, normalized => _configuration.Range.EdgeDeadZones.BottomMeters = normalized);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string CalibrationStatus
    {
        get => _calibrationStatus;
        private set => SetProperty(ref _calibrationStatus, value);
    }

    public string ActualScanFrequency
    {
        get => _actualScanFrequency;
        private set => SetProperty(ref _actualScanFrequency, value);
    }

    public string ReceiveRate
    {
        get => _receiveRate;
        private set => SetProperty(ref _receiveRate, value);
    }

    public long LastFrameSequence
    {
        get => _lastFrameSequence;
        private set => SetProperty(ref _lastFrameSequence, value);
    }

    public RadarRuntimeSnapshot? LatestSnapshot
    {
        get => _latestSnapshot;
        private set => SetProperty(ref _latestSnapshot, value);
    }

    public UnityClientStatus UnityStatus
    {
        get => _unityStatus;
        private set => SetProperty(ref _unityStatus, value);
    }

    public int RawPointCount => LatestSnapshot?.RawPoints.Count ?? 0;
    public int ValidPointCount => LatestSnapshot?.ValidPoints.Count ?? 0;
    public int TargetCount => LatestSnapshot?.Targets.Count ?? 0;
    public long CrcErrorCount => LatestSnapshot?.CrcErrorCount ?? 0;
    public long DiscardedByteCount => LatestSnapshot?.DiscardedByteCount ?? 0;
    public string UnityConnectionText => UnityStatus.IsConnected ? "Unity 已连接" : "Unity 未连接";
    public string UnityResolution => UnityStatus.IsConnected
        ? $"{UnityStatus.ScreenWidth} × {UnityStatus.ScreenHeight}"
        : "—";
    public int MaskedRegionCount => _configuration.Range.MaskedPolygons.Count;
    public IReadOnlyList<IReadOnlyList<Point2>> MaskedRegions => _configuration.Range.MaskedPolygons
        .Select(mask => (IReadOnlyList<Point2>)mask.Select(point => new Point2(point.X, point.Y)).ToArray())
        .ToArray();

    public void AddMaskedRegion(Point2 center, float halfSizeMeters = 0.2f)
    {
        if (halfSizeMeters <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(halfSizeMeters));
        }

        _configuration.Range.MaskedPolygons.Add(
        [
            new RadarPoint2(center.X - halfSizeMeters, center.Y + halfSizeMeters),
            new RadarPoint2(center.X + halfSizeMeters, center.Y + halfSizeMeters),
            new RadarPoint2(center.X + halfSizeMeters, center.Y - halfSizeMeters),
            new RadarPoint2(center.X - halfSizeMeters, center.Y - halfSizeMeters)
        ]);
        OnPropertyChanged(nameof(MaskedRegionCount));
        OnPropertyChanged(nameof(MaskedRegions));
        (DeleteMaskedRegionCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    public bool AddMaskedRegionAtCurrentTarget()
    {
        var target = LatestSnapshot?.Targets.FirstOrDefault();
        if (target is null)
        {
            return false;
        }

        AddMaskedRegion(new Point2(target.PhysicalX, target.PhysicalY));
        return true;
    }

    public bool DeleteLastMaskedRegion()
    {
        if (_configuration.Range.MaskedPolygons.Count == 0)
        {
            return false;
        }

        _configuration.Range.MaskedPolygons.RemoveAt(_configuration.Range.MaskedPolygons.Count - 1);
        OnPropertyChanged(nameof(MaskedRegionCount));
        OnPropertyChanged(nameof(MaskedRegions));
        (DeleteMaskedRegionCommand as RelayCommand)?.NotifyCanExecuteChanged();
        return true;
    }

    public string CalibrationStep
    {
        get => _calibrationStep;
        private set => SetProperty(ref _calibrationStep, value);
    }

    public void UpdateRegionVertex(int index, Point2 value)
    {
        if (index < 0 || index >= RegionVertices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        RegionVertices[index] = value;
        SyncRegionToConfiguration();
    }

    public void ResetRegion()
    {
        var extent = MathF.Min(5f, _modelProfile.MaximumDistanceMeters) / 2f;
        RegionVertices.Clear();
        RegionVertices.Add(new Point2(-extent, extent));
        RegionVertices.Add(new Point2(extent, extent));
        RegionVertices.Add(new Point2(extent, -extent));
        RegionVertices.Add(new Point2(-extent, -extent));
        SyncRegionToConfiguration();
    }

    public void BeginCalibration()
    {
        _capturedCalibrationPoints.Clear();
        CalibrationStep = "请采集左上角";
        CalibrationStatus = "标定进行中 0/4";
    }

    public bool CaptureCalibrationPoint(Point2 point)
    {
        if (_capturedCalibrationPoints.Count >= 4)
        {
            return false;
        }

        _capturedCalibrationPoints.Add(point);
        var names = new[] { "左上", "右上", "右下", "左下" };
        CalibrationStatus = $"标定进行中 {_capturedCalibrationPoints.Count}/4";
        CalibrationStep = _capturedCalibrationPoints.Count == 4
            ? "四点已采集，可保存标定"
            : $"请采集{names[_capturedCalibrationPoints.Count]}角";
        return true;
    }

    public bool CaptureCurrentTargetForCalibration()
    {
        var target = LatestSnapshot?.Targets.FirstOrDefault(target => target.IsConfirmed)
            ?? LatestSnapshot?.Targets.FirstOrDefault();
        return target is not null && CaptureCalibrationPoint(new Point2(target.PhysicalX, target.PhysicalY));
    }

    public void UndoCalibrationPoint()
    {
        if (_capturedCalibrationPoints.Count == 0)
        {
            return;
        }

        _capturedCalibrationPoints.RemoveAt(_capturedCalibrationPoints.Count - 1);
        CalibrationStatus = $"标定进行中 {_capturedCalibrationPoints.Count}/4";
        var names = new[] { "左上", "右上", "右下", "左下" };
        CalibrationStep = $"请采集{names[_capturedCalibrationPoints.Count]}角";
    }

    public bool SaveCalibration()
    {
        if (!HomographyCalibration.TryCreate(_capturedCalibrationPoints, out var calibration, out var error))
        {
            CalibrationStatus = error ?? "标定失败";
            return false;
        }

        _configuration.Calibration = new RadarCalibrationConfiguration
        {
            IsValid = true,
            DeviceModel = SelectedModel,
            PhysicalCorners = _capturedCalibrationPoints
                .Select(point => new RadarPoint2(point.X, point.Y))
                .ToList(),
            HomographyMatrix = calibration!.Matrix.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
            MaximumCornerError = calibration.MaximumCornerError,
            TransformSnapshot = new RadarTransformConfiguration
            {
                RotationDegrees = _configuration.Transform.RotationDegrees,
                FlipX = _configuration.Transform.FlipX,
                FlipY = _configuration.Transform.FlipY,
                OffsetXMeters = _configuration.Transform.OffsetXMeters,
                OffsetYMeters = _configuration.Transform.OffsetYMeters
            }
        };
        CalibrationStep = "标定完成";
        RefreshCalibrationStatus(modelChanged: false);
        AddLog($"四点标定已保存，最大角点误差 {calibration.MaximumCornerError:0.000000}。");
        return true;
    }

    public void ClearCalibration()
    {
        _capturedCalibrationPoints.Clear();
        _configuration.Calibration = new RadarCalibrationConfiguration();
        CalibrationStep = "未开始";
        CalibrationStatus = "未标定";
    }

    private AsyncRelayCommand CreateAsyncCommand(Func<Task> action)
    {
        var command = new AsyncRelayCommand(action);
        command.ExecutionFailed += exception => AddLog($"操作失败：{exception.Message}");
        return command;
    }

    private async Task SaveConfigurationAsync()
    {
        var path = RadarConfigurationStore.GetDefaultUserConfigurationPath();
        await RadarConfigurationStore.SaveAsync(path, _configuration).ConfigureAwait(true);
        AddLog($"配置已保存：{path}");
    }

    private void OnSnapshotUpdated(RadarRuntimeSnapshot snapshot)
    {
        Dispatch(() =>
        {
            LatestSnapshot = snapshot;
            LastFrameSequence = snapshot.Sequence;
            ActualScanFrequency = $"{snapshot.ScanFrequencyHz:0.0} Hz";
            ReceiveRate = FormatRate(snapshot.ReceivedBytesPerSecond);
            OnPropertyChanged(nameof(RawPointCount));
            OnPropertyChanged(nameof(ValidPointCount));
            OnPropertyChanged(nameof(TargetCount));
            OnPropertyChanged(nameof(CrcErrorCount));
            OnPropertyChanged(nameof(DiscardedByteCount));
            if (CaptureCalibrationPointCommand is RelayCommand captureCommand)
            {
                captureCommand.NotifyCanExecuteChanged();
            }
        });
    }

    private void OnLogReceived(string message) => Dispatch(() => AddLog(message));

    private void OnConnectionStateChanged(RadarConnectionState state)
    {
        Dispatch(() => ConnectionStatus = state switch
        {
            RadarConnectionState.Connecting => "正在连接",
            RadarConnectionState.Connected => "已连接",
            RadarConnectionState.Reconnecting => "正在重连",
            RadarConnectionState.Faulted => "连接故障",
            _ => "未连接"
        });
    }

    private void OnUnityStatusChanged(UnityClientStatus status) => Dispatch(() =>
    {
        UnityStatus = status;
        OnPropertyChanged(nameof(UnityConnectionText));
        OnPropertyChanged(nameof(UnityResolution));
    });

    private void RefreshCalibrationStatus(bool modelChanged)
    {
        if (!_configuration.Calibration.IsValid)
        {
            CalibrationStatus = "未标定";
            return;
        }

        CalibrationStatus = _configuration.Calibration.DeviceModel == _selectedModel
            ? $"已标定 / 最大误差 {_configuration.Calibration.MaximumCornerError:0.0000}"
            : modelChanged
                ? "型号已变化：保留原标定，请现场确认或重新标定"
                : "标定来自另一型号，请现场确认或重新标定";
    }

    private void InitializeRegionVertices()
    {
        if (_configuration.Range.ActivePolygon.Count == 4)
        {
            foreach (var point in _configuration.Range.ActivePolygon)
            {
                RegionVertices.Add(new Point2(point.X, point.Y));
            }
        }
        else
        {
            ResetRegion();
        }
    }

    private void SyncRegionToConfiguration()
    {
        _configuration.Range.ActivePolygon = RegionVertices
            .Select(point => new RadarPoint2(point.X, point.Y))
            .ToList();
    }

    private void AddLog(string message)
    {
        LogEntries.Add(message);
        while (LogEntries.Count > MaximumVisibleLogEntries)
        {
            LogEntries.RemoveAt(0);
        }
    }

    private void Dispatch(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), null);
    }

    private static string FormatRate(double bytesPerSecond)
    {
        return bytesPerSecond >= 1024d
            ? $"{bytesPerSecond / 1024d:0.0} KB/s"
            : $"{bytesPerSecond:0} B/s";
    }

    private static IReadOnlyList<string> DiscoverLocalIps()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        addresses.Insert(0, string.Empty);
        return addresses;
    }

    private void SetEdgeDeadZone(float value, float current, Action<float> setter, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        var normalized = Math.Max(0f, value);
        if (Math.Abs(current - normalized) < float.Epsilon)
        {
            return;
        }

        setter(normalized);
        OnPropertyChanged(propertyName);
    }
}

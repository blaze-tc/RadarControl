using Yuexin.Radar.Contracts;

namespace Yuexin.Radar.Bridge.Wpf.Services;

public sealed record RadarRuntimeSnapshot(
    long Sequence,
    DateTimeOffset Timestamp,
    IReadOnlyList<RadarPoint> RawPoints,
    IReadOnlyList<RadarPoint> ValidPoints,
    IReadOnlyList<RadarCluster> Clusters,
    IReadOnlyList<RadarTarget> Targets,
    IReadOnlyList<RadarPointer> Pointers,
    double ScanFrequencyHz,
    double ReceivedBytesPerSecond,
    long CrcErrorCount = 0,
    long DiscardedByteCount = 0);

public sealed record UnityClientStatus(
    bool IsConnected,
    int ProcessId,
    string UnityVersion,
    int ScreenWidth,
    int ScreenHeight,
    DateTimeOffset? LastFrameSentAt,
    int PointerCount,
    string? LastError)
{
    public static UnityClientStatus Disconnected { get; } = new(
        false,
        0,
        string.Empty,
        0,
        0,
        null,
        0,
        null);
}

public interface IRadarBridgeRuntime : IAsyncDisposable
{
    event Action<RadarRuntimeSnapshot>? SnapshotUpdated;
    event Action<string>? LogReceived;
    event Action<RadarConnectionState>? ConnectionStateChanged;
    event Action<UnityClientStatus>? UnityStatusChanged;

    RadarConnectionState ConnectionState { get; }
    UnityClientStatus UnityStatus { get; }

    Task StartInfrastructureAsync(CancellationToken cancellationToken = default);
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task StartSimulationAsync(CancellationToken cancellationToken = default);
    Task StopSimulationAsync();
    Task StartRecordingAsync(string path, CancellationToken cancellationToken = default);
    Task StopRecordingAsync();
    Task ReplayAsync(string path, double speed, bool loop, CancellationToken cancellationToken = default);
    void PauseReplay();
    void ResumeReplay();
    void StepReplay();
    Task StopReplayAsync();
}

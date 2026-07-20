using Microsoft.Extensions.Logging.Abstractions;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Configuration;

namespace Yuexin.Radar.Bridge.Wpf.Tests;

public sealed class RadarBridgeRuntimeTests
{
    [Fact]
    public async Task StartInfrastructureAsync_CanceledFirstAttemptCanBeRetried()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        await using var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runtime.StartInfrastructureAsync(canceled.Token));
        await runtime.StartInfrastructureAsync();

        var pipeTaskField = typeof(RadarBridgeRuntime).GetField(
            "_pipeTask",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(pipeTaskField?.GetValue(runtime));
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMoreThanOnce()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);

        await runtime.StartInfrastructureAsync();
        await runtime.DisposeAsync();
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Simulation_ProducesProcessedSnapshotsAndStopsCooperatively()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        configuration.Range.ActivePolygon =
        [
            new(-5f, -5f),
            new(5f, -5f),
            new(5f, 5f),
            new(-5f, 5f)
        ];
        await using var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new TaskCompletionSource<RadarRuntimeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.SnapshotUpdated += snapshot => received.TrySetResult(snapshot);

        await runtime.StartInfrastructureAsync(cancellation.Token);
        await runtime.StartSimulationAsync(cancellation.Token);
        var snapshot = await received.Task.WaitAsync(cancellation.Token);
        await runtime.StopSimulationAsync();

        Assert.NotEmpty(snapshot.RawPoints);
        Assert.NotEmpty(snapshot.ValidPoints);
        Assert.True(snapshot.Sequence > 0);
        Assert.InRange(snapshot.ScanFrequencyHz, 1, 30);
    }

    [Fact]
    public async Task Simulation_FaultingSnapshotSubscriber_DoesNotStopOtherSubscribers()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        configuration.Range.ActivePolygon =
        [
            new(-5f, -5f),
            new(5f, -5f),
            new(5f, 5f),
            new(-5f, 5f)
        ];
        await using var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new TaskCompletionSource<RadarRuntimeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.SnapshotUpdated += _ => throw new InvalidOperationException("observer failed");
        runtime.SnapshotUpdated += snapshot => received.TrySetResult(snapshot);

        await runtime.StartInfrastructureAsync(cancellation.Token);
        await runtime.StartSimulationAsync(cancellation.Token);
        var completed = await Task.WhenAny(received.Task, Task.Delay(500, cancellation.Token));
        await runtime.StopSimulationAsync();

        Assert.Same(received.Task, completed);
    }

    [Fact]
    public async Task ConcurrentSimulationStarts_StopWithoutLeavingOrphanProducers()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        configuration.Range.ActivePolygon =
        [
            new(-5f, -5f),
            new(5f, -5f),
            new(5f, 5f),
            new(-5f, 5f)
        ];
        await using var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var snapshotCount = 0;
        runtime.SnapshotUpdated += _ => Interlocked.Increment(ref snapshotCount);

        await runtime.StartInfrastructureAsync(cancellation.Token);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(
            _ => Task.Run(() => runtime.StartSimulationAsync(cancellation.Token), cancellation.Token)));
        await runtime.StopSimulationAsync();
        await Task.Delay(150, cancellation.Token);
        var stoppedCount = Volatile.Read(ref snapshotCount);
        await Task.Delay(250, cancellation.Token);

        Assert.Equal(stoppedCount, Volatile.Read(ref snapshotCount));
    }

    [Fact]
    public async Task ReplayAsync_MissingFileFailsAtOperationBoundary()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Ipc.PipeName = "RadarControl.Tests." + Guid.NewGuid().ToString("N");
        await using var runtime = new RadarBridgeRuntime(
            configuration,
            NullLogger<RadarBridgeRuntime>.Instance);
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".radarrec");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => runtime.ReplayAsync(missingPath, 1d, loop: false));
    }
}

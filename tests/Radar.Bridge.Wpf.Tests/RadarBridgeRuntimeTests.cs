using Microsoft.Extensions.Logging.Abstractions;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Configuration;

namespace Yuexin.Radar.Bridge.Wpf.Tests;

public sealed class RadarBridgeRuntimeTests
{
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
}

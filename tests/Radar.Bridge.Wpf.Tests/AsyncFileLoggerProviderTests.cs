using Microsoft.Extensions.Logging;
using Yuexin.Radar.Bridge.Wpf.Logging;

namespace Radar.Bridge.Wpf.Tests;

public sealed class AsyncFileLoggerProviderTests
{
    [Fact]
    public async Task DisposeAsync_FlushesQueuedEntriesToFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"radar-log-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "radar-bridge.log");

        try
        {
            var provider = new AsyncFileLoggerProvider(path, capacity: 8);
            var logger = provider.CreateLogger("Radar.Tests");

            logger.LogInformation("connected to {Endpoint}", "192.168.0.100:8487");
            await provider.DisposeAsync();

            var contents = await File.ReadAllTextAsync(path);
            Assert.Contains("Information", contents);
            Assert.Contains("Radar.Tests", contents);
            Assert.Contains("connected to 192.168.0.100:8487", contents);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

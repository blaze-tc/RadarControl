namespace Radar.Unity.Compatibility.Tests;

public sealed class EmbeddedBridgePayloadTests
{
    [Fact]
    public void PackageContainsSelfContainedBridgeAndProfiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var publishDirectory = Path.Combine(
            repositoryRoot,
            "UnityPackage",
            "com.yuexin.radar",
            "Bridge~",
            "win-x64");

        AssertFileExists(publishDirectory, "RadarBridge.exe");
        AssertFileExists(publishDirectory, "RadarBridge.deps.json");
        AssertFileExists(publishDirectory, "RadarBridge.runtimeconfig.json");
        AssertFileExists(publishDirectory, "coreclr.dll");
        AssertFileExists(publishDirectory, "profiles", "default-profile.json");
        AssertFileExists(publishDirectory, "profiles", "f20-profile.json");
    }

    private static void AssertFileExists(params string[] pathParts)
    {
        var path = Path.Combine(pathParts);
        Assert.True(File.Exists(path), $"Expected embedded Bridge file: {path}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RadarControl.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate RadarControl.sln from the test output directory.");
    }
}

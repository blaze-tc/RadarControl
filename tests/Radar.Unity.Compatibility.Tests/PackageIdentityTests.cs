using System.Text.Json;

namespace Radar.Unity.Compatibility.Tests;

public sealed class PackageIdentityTests
{
    [Fact]
    public void Package_UsesBlazeIdentityAndExplicitSampleAssemblyReference()
    {
        var packageRoot = Path.Combine(FindRepositoryRoot(), "UnityPackage", "com.blaze.radar");
        Assert.True(Directory.Exists(packageRoot), $"Expected renamed package directory: {packageRoot}");

        using var packageJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "package.json")));
        Assert.Equal("com.blaze.radar", packageJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("Blaze Radar SDK", packageJson.RootElement.GetProperty("displayName").GetString());

        using var runtimeAssembly = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(packageRoot, "Runtime", "Blaze.Radar.Runtime.asmdef")));
        Assert.Equal("Blaze.Radar.Runtime", runtimeAssembly.RootElement.GetProperty("name").GetString());
        Assert.Equal("Blaze.Radar", runtimeAssembly.RootElement.GetProperty("rootNamespace").GetString());

        using var sampleAssembly = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(packageRoot, "Samples~", "BasicInteraction", "Blaze.Radar.Sample.BasicInteraction.asmdef")));
        var references = sampleAssembly.RootElement
            .GetProperty("references")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("Blaze.Radar.Runtime", references);
        Assert.Contains("Unity.ugui", references);

        var sampleSource = File.ReadAllText(
            Path.Combine(packageRoot, "Samples~", "BasicInteraction", "BasicInteractionBootstrap.cs"));
        Assert.Contains("using Blaze.Radar;", sampleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Yuexin.Radar.Unity", sampleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageCSharpSources_DoNotUseLegacyUnityNamespace()
    {
        var packageRoot = Path.Combine(FindRepositoryRoot(), "UnityPackage", "com.blaze.radar");
        Assert.True(Directory.Exists(packageRoot), $"Expected renamed package directory: {packageRoot}");

        var legacySources = Directory
            .EnumerateFiles(packageRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("Yuexin.Radar.Unity", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(packageRoot, path))
            .ToArray();

        Assert.Empty(legacySources);
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

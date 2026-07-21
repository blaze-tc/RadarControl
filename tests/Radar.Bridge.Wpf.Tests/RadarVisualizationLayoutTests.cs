using System.Xml.Linq;

namespace Yuexin.Radar.Bridge.Wpf.Tests;

public sealed class RadarVisualizationLayoutTests
{
    [Fact]
    public void MainWindow_SeparatesRawObservationFromUnityOutput()
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "Radar.Bridge.Wpf", "MainWindow.xaml"));
        var presentation = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var controls = XNamespace.Get("clr-namespace:Yuexin.Radar.Bridge.Wpf.Controls");
        var xaml = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var views = document.Descendants(controls + "RadarPointCloudView").ToArray();

        Assert.Equal(2, views.Length);
        var raw = views.Single(element => (string?)element.Attribute(xaml + "Name") == "RawRadarView");
        var output = views.Single(element => (string?)element.Attribute(xaml + "Name") == "UnityOutputRadarView");

        Assert.Equal("True", (string?)raw.Attribute("ShowRawPoints"));
        Assert.Equal("False", (string?)raw.Attribute("ShowValidPoints"));
        Assert.Equal("False", (string?)raw.Attribute("ShowBlindZone"));
        Assert.Equal("False", (string?)raw.Attribute("IsRegionEditable"));
        Assert.Null(raw.Attribute("RegionVertices"));

        Assert.Equal("False", (string?)output.Attribute("ShowRawPoints"));
        Assert.Equal("True", (string?)output.Attribute("ShowValidPoints"));
        Assert.Equal("False", (string?)output.Attribute("ShowBlindZone"));
        Assert.Equal("True", (string?)output.Attribute("IsRegionEditable"));
        Assert.Equal("{Binding RegionVertices}", (string?)output.Attribute("RegionVertices"));

        var labels = document.Descendants(presentation + "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .Where(text => text is not null)
            .ToArray();
        Assert.Contains("区域 1 · 雷达原始数据", labels);
        Assert.Contains("区域 2 · Unity 输出数据", labels);
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

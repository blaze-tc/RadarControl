using Yuexin.Radar.Configuration;
using Yuexin.Radar.Contracts;

namespace Yuexin.Radar.Configuration.Tests;

public sealed class RadarConfigurationTests
{
    [Fact]
    public void NewConfiguration_DefaultsToF10AndDocumentedEndpoint()
    {
        var configuration = RadarAppConfiguration.CreateDefault();

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal(RadarModel.F10, configuration.Device.DeviceModel);
        Assert.Equal("192.168.0.100", configuration.Device.RadarIp);
        Assert.Equal(8487, configuration.Device.Port);
        Assert.Equal(5f, configuration.Range.MaximumDistanceMeters);
        Assert.Equal(4f, configuration.Range.VisualizationRangeMeters);
        Assert.Equal("Yuexin.RadarBridge", configuration.Ipc.PipeName);
    }

    [Fact]
    public void Validator_ClampsMaximumDistanceToSelectedModel()
    {
        var f10 = RadarAppConfiguration.CreateDefault();
        f10.Range.MaximumDistanceMeters = 99f;

        var f10Result = ConfigurationValidator.ValidateAndNormalize(f10);

        Assert.True(f10Result.IsValid);
        Assert.Equal(10f, f10.Range.MaximumDistanceMeters);

        var f20 = RadarAppConfiguration.CreateDefault();
        f20.Device.DeviceModel = RadarModel.F20;
        f20.Range.MaximumDistanceMeters = 99f;

        var f20Result = ConfigurationValidator.ValidateAndNormalize(f20);

        Assert.True(f20Result.IsValid);
        Assert.Equal(40f, f20.Range.MaximumDistanceMeters);
    }

    [Fact]
    public void LoadFromJson_UnknownModelFallsBackToF10()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "device": {
                "deviceModel": "F99",
                "radarIp": "192.168.0.100",
                "port": 8487
              }
            }
            """;

        var configuration = RadarConfigurationStore.LoadFromJson(json);

        Assert.Equal(RadarModel.F10, configuration.Device.DeviceModel);
    }

    [Fact]
    public async Task SaveAndLoad_RetainsF20Selection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarControl.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "config.json");
        try
        {
            var configuration = RadarAppConfiguration.CreateDefault();
            configuration.Device.DeviceModel = RadarModel.F20;
            configuration.Range.MaximumDistanceMeters = 20f;
            configuration.Range.VisualizationRangeMeters = 7.5f;

            await RadarConfigurationStore.SaveAsync(path, configuration);
            var loaded = await RadarConfigurationStore.LoadAsync(path);

            Assert.Equal(RadarModel.F20, loaded.Device.DeviceModel);
            Assert.Equal(20f, loaded.Range.MaximumDistanceMeters);
            Assert.Equal(7.5f, loaded.Range.VisualizationRangeMeters);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Validator_RejectsInvalidEndpointAndRanges()
    {
        var configuration = RadarAppConfiguration.CreateDefault();
        configuration.Device.RadarIp = "not-an-ip";
        configuration.Device.Port = 0;
        configuration.Range.MinimumDistanceMeters = -1f;

        var result = ConfigurationValidator.ValidateAndNormalize(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("radarIp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("port", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("minimumDistanceMeters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAndLoad_RetainsCalibrationWhenModelChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarControl.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "config.json");
        try
        {
            var configuration = RadarAppConfiguration.CreateDefault();
            configuration.Calibration = new RadarCalibrationConfiguration
            {
                IsValid = true,
                DeviceModel = RadarModel.F10,
                PhysicalCorners = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)],
                HomographyMatrix = [1, 0, 0, 0, 1, 0, 0, 0, 1],
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(1000),
                MaximumCornerError = 0.001
            };
            configuration.Device.DeviceModel = RadarModel.F20;

            await RadarConfigurationStore.SaveAsync(path, configuration);
            var loaded = await RadarConfigurationStore.LoadAsync(path);

            Assert.Equal(RadarModel.F20, loaded.Device.DeviceModel);
            Assert.True(loaded.Calibration.IsValid);
            Assert.Equal(RadarModel.F10, loaded.Calibration.DeviceModel);
            Assert.Equal(4, loaded.Calibration.PhysicalCorners.Count);
            Assert.Equal(9, loaded.Calibration.HomographyMatrix.Count);
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

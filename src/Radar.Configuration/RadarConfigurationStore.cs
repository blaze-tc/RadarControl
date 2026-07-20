using System.Text.Json;
using System.Text.Json.Serialization;
using Yuexin.Radar.Contracts;

namespace Yuexin.Radar.Configuration;

public static class RadarConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static RadarAppConfiguration LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return RadarAppConfiguration.CreateDefault();
        }

        try
        {
            var configuration = JsonSerializer.Deserialize<RadarAppConfiguration>(json, JsonOptions)
                ?? RadarAppConfiguration.CreateDefault();
            EnsureSections(configuration);
            ConfigurationValidator.ValidateAndNormalize(configuration);
            return configuration;
        }
        catch (JsonException)
        {
            return RadarAppConfiguration.CreateDefault();
        }
    }

    public static async Task<RadarAppConfiguration> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return RadarAppConfiguration.CreateDefault();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return LoadFromJson(json);
    }

    public static async Task SaveAsync(
        string path,
        RadarAppConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(configuration);

        var validation = ConfigurationValidator.ValidateAndNormalize(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public static string GetDefaultUserConfigurationPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Yuexin",
            "RadarBridge",
            "config.json");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new LenientRadarModelJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void EnsureSections(RadarAppConfiguration configuration)
    {
        configuration.Device ??= new RadarDeviceConfiguration();
        configuration.Transform ??= new RadarTransformConfiguration();
        configuration.Range ??= new RadarRangeConfiguration();
        configuration.Range.ActivePolygon ??= [];
        configuration.Range.MaskedPolygons ??= [];
        configuration.Range.EdgeDeadZones ??= new RadarEdgeDeadZoneConfiguration();
        configuration.Clustering ??= new RadarClusteringConfiguration();
        configuration.Tracking ??= new RadarTrackingConfiguration();
        configuration.Interaction ??= new RadarInteractionConfiguration();
        configuration.Ipc ??= new RadarIpcConfiguration();
        configuration.Calibration ??= new RadarCalibrationConfiguration();
        configuration.Calibration.PhysicalCorners ??= [];
        configuration.Calibration.HomographyMatrix ??= [];
        configuration.Calibration.TransformSnapshot ??= new RadarTransformConfiguration();
    }

    private sealed class LenientRadarModelJsonConverter : JsonConverter<RadarModel>
    {
        public override RadarModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String &&
                Enum.TryParse<RadarModel>(reader.GetString(), ignoreCase: true, out var model) &&
                Enum.IsDefined(model))
            {
                return model;
            }

            if (reader.TokenType == JsonTokenType.Number &&
                reader.TryGetInt32(out var numeric) &&
                Enum.IsDefined(typeof(RadarModel), numeric))
            {
                return (RadarModel)numeric;
            }

            return RadarModel.F10;
        }

        public override void Write(Utf8JsonWriter writer, RadarModel value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Enum.IsDefined(value) ? value.ToString() : RadarModel.F10.ToString());
        }
    }
}

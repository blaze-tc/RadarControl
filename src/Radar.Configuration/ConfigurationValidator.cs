using System.Net;
using Yuexin.Radar.Contracts;
using Yuexin.Radar.Device;

namespace Yuexin.Radar.Configuration;

public sealed record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public static class ConfigurationValidator
{
    public static ConfigurationValidationResult ValidateAndNormalize(RadarAppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Enum.IsDefined(configuration.Device.DeviceModel))
        {
            configuration.Device.DeviceModel = RadarModel.F10;
            warnings.Add("deviceModel is unknown and was reset to F10.");
        }

        var profile = RadarModelProfileFactory.Create(configuration.Device.DeviceModel);
        if (configuration.Range.MaximumDistanceMeters > profile.MaximumDistanceMeters)
        {
            configuration.Range.MaximumDistanceMeters = profile.MaximumDistanceMeters;
            warnings.Add($"maximumDistanceMeters was clamped to {profile.MaximumDistanceMeters} for {profile.Model}.");
        }

        if (!IPAddress.TryParse(configuration.Device.RadarIp, out _))
        {
            errors.Add("device.radarIp must be a valid IP address.");
        }

        if (configuration.Device.Port is < 1 or > 65535)
        {
            errors.Add("device.port must be between 1 and 65535.");
        }

        if (configuration.Range.MinimumDistanceMeters < 0f)
        {
            errors.Add("range.minimumDistanceMeters cannot be negative.");
        }

        if (configuration.Range.MaximumDistanceMeters <= configuration.Range.MinimumDistanceMeters)
        {
            errors.Add("range.maximumDistanceMeters must be greater than minimumDistanceMeters.");
        }

        if (configuration.Range.EdgeDeadZones.LeftMeters < 0f ||
            configuration.Range.EdgeDeadZones.RightMeters < 0f ||
            configuration.Range.EdgeDeadZones.TopMeters < 0f ||
            configuration.Range.EdgeDeadZones.BottomMeters < 0f)
        {
            errors.Add("range edge dead zones cannot be negative.");
        }

        if (configuration.Range.MinimumAngleDegrees is < 0f or > 360f ||
            configuration.Range.MaximumAngleDegrees is < 0f or > 360f)
        {
            errors.Add("range angles must be between 0 and 360 degrees.");
        }

        if (configuration.Clustering.MinimumClusterPointCount < 1)
        {
            errors.Add("clustering.minimumClusterPointCount must be at least 1.");
        }

        if (configuration.Tracking.ConfirmFrames < 1 || configuration.Tracking.LostFrames < 1)
        {
            errors.Add("tracking frame counts must be at least 1.");
        }

        if (configuration.Tracking.SmoothingAlpha is <= 0f or > 1f)
        {
            errors.Add("tracking.smoothingAlpha must be greater than 0 and at most 1.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Ipc.PipeName))
        {
            errors.Add("ipc.pipeName is required.");
        }

        if (configuration.Calibration.IsValid)
        {
            if (configuration.Calibration.PhysicalCorners.Count != 4 ||
                configuration.Calibration.HomographyMatrix.Count != 9)
            {
                errors.Add("calibration requires four physical corners and a 3x3 homography matrix.");
            }

            if (configuration.Calibration.DeviceModel != configuration.Device.DeviceModel)
            {
                warnings.Add("The saved calibration was created for another radar model; review or recalibrate before use.");
            }
        }

        return new ConfigurationValidationResult(errors.Count == 0, errors, warnings);
    }
}

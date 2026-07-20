using System;
using System.IO;
using UnityEditor.PackageManager;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Yuexin.Radar.Unity.Internal;

namespace Yuexin.Radar.Unity.Editor
{
    public sealed class RadarBuildProcessor : IPostprocessBuildWithReport
    {
        private const string BridgeSourceEditorPreference = "Yuexin.Radar.BridgePublishDirectory";

        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            var sourceDirectory = ResolveBridgeSourceDirectory();

            var sourceExecutable = Path.Combine(sourceDirectory, "RadarBridge.exe");
            if (!File.Exists(sourceExecutable))
            {
                throw new BuildFailedException(
                    $"RadarBridge.exe is missing. Reinstall the Yuexin Radar package or set Tools > Yuexin Radar > Bridge Publish Directory. Expected: {sourceExecutable}");
            }

            var playerDirectory = Path.GetDirectoryName(report.summary.outputPath)
                ?? throw new BuildFailedException("Unable to resolve the player output directory.");
            var destinationDirectory = Path.Combine(playerDirectory, "RadarBridge");
            CopyDirectory(sourceDirectory, destinationDirectory);

            if (!File.Exists(Path.Combine(destinationDirectory, "RadarBridge.exe")))
            {
                throw new BuildFailedException("RadarBridge copy completed without RadarBridge.exe.");
            }
        }

        [UnityEditor.MenuItem("Tools/Yuexin Radar/Bridge Publish Directory")]
        private static void ChooseBridgePublishDirectory()
        {
            var current = UnityEditor.EditorPrefs.GetString(BridgeSourceEditorPreference, "");
            if (string.IsNullOrWhiteSpace(current))
            {
                current = ResolveBridgeSourceDirectory();
            }

            var selected = UnityEditor.EditorUtility.OpenFolderPanel("Select RadarBridge publish directory", current, "");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                UnityEditor.EditorPrefs.SetString(BridgeSourceEditorPreference, selected);
            }
        }

        private static string ResolveBridgeSourceDirectory()
        {
            var configured = UnityEditor.EditorPrefs.GetString(BridgeSourceEditorPreference, "");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            var packageInfo = PackageInfo.FindForAssembly(typeof(RadarBridgeLauncher).Assembly);
            if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                var embedded = BridgePathResolver.GetEmbeddedPublishDirectory(packageInfo.resolvedPath);
                if (File.Exists(Path.Combine(embedded, BridgePathResolver.ExecutableName)))
                {
                    return embedded;
                }
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "artifacts",
                "publish",
                "RadarBridge",
                BridgePathResolver.RuntimeIdentifier));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}

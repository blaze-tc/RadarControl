using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yuexin.Radar.Unity.Editor
{
    public static class RadarSettingsProvider
    {
        private const string SettingsAssetPath = "Assets/Resources/RadarRuntimeSettings.asset";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider("Project/Yuexin Radar", SettingsScope.Project)
            {
                label = "Yuexin Radar",
                guiHandler = _ => DrawSettings()
            };
            return provider;
        }

        [MenuItem("Tools/Yuexin Radar/Create or Select Settings")]
        public static void SelectSettings()
        {
            var settings = LoadOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void DrawSettings()
        {
            var settings = LoadOrCreateSettings();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "RadarBridge owns TCP/protocol/calibration. Unity receives normalized pointer frames over Named Pipe.",
                MessageType.Info);
            var serialized = new SerializedObject(settings);
            serialized.Update();
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, includeChildren: true);
            }

            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settings);
            }
        }

        internal static RadarRuntimeSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RadarRuntimeSettings>(SettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath) ?? "Assets/Resources");
            settings = ScriptableObject.CreateInstance<RadarRuntimeSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}

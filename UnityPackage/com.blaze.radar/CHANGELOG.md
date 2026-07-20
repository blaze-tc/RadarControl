# Changelog

## 1.1.0 - 2026-07-20

- Rename the UPM package to `com.blaze.radar` and its public Unity namespace to `Blaze.Radar`.
- Add an explicit Basic Interaction sample assembly definition that references the runtime and Unity UI assemblies.
- Add Unity 2021.3 compatibility for EventSystem selection handling and validate Runtime, Editor and Sample compilation in Unity 2021.3.45f1.

## 1.0.1 - 2026-07-20

- Embed the complete self-contained win-x64 RadarBridge publish directory in the UPM package.
- Resolve the embedded Bridge automatically in the Unity Editor and copy it beside Windows players.
- Fix WPF startup bindings for read-only CRC metrics and make Unity parent-process shutdown clean.

## 1.0.0 - 2026-07-20

- Initial F10-default/F20-compatible RadarBridge launcher and Named Pipe client.
- Multi-pointer UGUI, 3D and 2D EventSystem input.
- Radar-only and integrated mouse-debug modes.
- Build-time Bridge copy validation and Basic Interaction sample.

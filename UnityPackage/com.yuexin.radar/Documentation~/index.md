# Yuexin Radar SDK 1.0.0

The package connects Unity to the external `RadarBridge.exe` over the `Yuexin.RadarBridge` Named Pipe. Unity never opens the radar TCP socket.

## Quick start

1. Import the Basic Interaction sample from Package Manager.
2. Use **Tools > Yuexin Radar > Create or Select Settings** and set the Editor Bridge executable.
3. Use **GameObject > Yuexin Radar > Create Runtime** in an existing scene.
4. Ensure each screen-space Canvas has a `GraphicRaycaster`.
5. Add `PhysicsRaycaster` and/or `Physics2DRaycaster` to the relevant camera.
6. Disable `StandaloneInputModule`; use `RadarInputModule.InputMode = RadarAndMouseDebug` when mouse testing is needed.

The Bridge sends bottom-left-origin normalized coordinates. The SDK maps them directly to `Screen.width` and `Screen.height` without flipping Y.

## Build output

Publish the Bridge first. `RadarBuildProcessor` copies the complete publish directory beside the player executable:

```text
Game.exe
RadarBridge/
  RadarBridge.exe
  *.dll
Game_Data/
```

The build fails with an explicit path if `RadarBridge.exe` is missing.

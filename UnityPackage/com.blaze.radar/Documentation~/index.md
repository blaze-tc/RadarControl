# Blaze Radar SDK 1.1.2

The package connects Unity to the external `RadarBridge.exe` over the `Yuexin.RadarBridge` Named Pipe. Unity never opens the radar TCP socket.

## Quick start

1. Import the Basic Interaction sample from Package Manager. Its right-hand diagnostics panel reports IPC, frame timing, dropped frames, pointer samples and EventSystem callbacks during real-device testing.
2. Use **Tools > Blaze Radar > Create or Select Settings**. Leave the Editor Bridge executable empty to use the embedded self-contained Bridge.
3. Use **GameObject > Blaze Radar > Create Runtime** in an existing scene.
4. Ensure each screen-space Canvas has a `GraphicRaycaster`.
5. Add `PhysicsRaycaster` and/or `Physics2DRaycaster` to the relevant camera.
6. Disable `StandaloneInputModule`; use `RadarInputModule.InputMode = RadarAndMouseDebug` when mouse testing is needed.

The Bridge sends bottom-left-origin normalized coordinates. The SDK maps them directly to `Screen.width` and `Screen.height` without flipping Y.

## Build output

`RadarBuildProcessor` copies the embedded `Bridge~/win-x64` publish directory beside the player executable:

```text
Game.exe
RadarBridge/
  RadarBridge.exe
  *.dll
Game_Data/
```

The build fails with an explicit path if `RadarBridge.exe` is missing. Package developers can regenerate both the repository artifact and embedded payload with `scripts/publish-bridge.ps1`.

# Changelog

## 1.1.2 - 2026-07-20

- Add an in-scene diagnostics panel for IPC state, Bridge/device identity, frame sequence, timing, dropped frames and per-pointer samples.
- Log standard UGUI, Physics and Physics2D EventSystem callbacks with pointer ID, pixel position, delta, press position and raycast target.
- Keep drag/scroll probes limited to controls that already implement those interactions, preserving native Button and Toggle behavior.
- Keep history bounded and throttle continuous frame/drag updates so long-running device tests remain readable and memory-safe.
- Preserve IPC envelope sequence and timestamp metadata on Unity pointer frames for end-to-end fault isolation.

## 1.1.1 - 2026-07-20

- Improve contrast across the RadarBridge WPF theme and Basic Interaction sample controls.
- Require a completed Hello/HelloAck handshake before Unity reports IPC connected and add response timeouts.
- Recover from malformed or short-lived Named Pipe clients without stopping the Bridge accept loop.
- Validate radar endpoints eagerly, isolate event subscriber failures and cancel active Unity pointers on IPC loss.
- Make launcher failures visible in the Unity Console and reject missing replay files at the API boundary.
- Replace the runtime-generated demo with an authored UGUI scene wired through persistent UnityEvents.
- Route radar pointers through standard Graphic, Physics and Physics2D raycasters, including pointer-over queries and clean module deactivation.
- Prevent overlapping Unity IPC loops during stop/restart and make pointer cancellation safe against reentrant event handlers.

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

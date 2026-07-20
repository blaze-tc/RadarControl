# Basic Interaction

Open `BasicInteraction.unity` and enter Play Mode. The scene is authored with ordinary Unity objects and explicit Inspector references; it does not build its UI from code.

The sample demonstrates the native EventSystem path end to end:

- `RadarInputModule` is the active `BaseInputModule` on the scene EventSystem.
- `GraphicRaycaster` handles Button, Toggle, Slider and ScrollRect.
- `PhysicsRaycaster` and `Physics2DRaycaster` handle the 3D and 2D targets.
- Persistent UnityEvent listeners connect the controls to `BasicInteractionPresenter`.
- `RadarPointerEventProbe` exposes standard UGUI pointer callbacks; `RadarDragEventProbe` is added only to controls that already support drag/scroll, so logging does not change control semantics.
- `SamplePointerTarget` implements the same pointer and drag interfaces used by normal mouse or touch input.
- `RadarDemoLogger` owns the diagnostic UI and all subscriptions; scene references are assigned explicitly in the Inspector.

## Device-test diagnostics

The right-hand panel is intended to stay open during a real radar test. It shows:

- IPC connection state, Bridge version and radar model.
- Received-frame count, IPC sequence, active pointer count and dropped-frame count.
- Frame clock/age plus each pointer's ID, phase, normalized position, pixel position, confidence and sample time.
- UGUI, Physics and Physics2D EventSystem callbacks with pointer ID, position, delta, press position and raycast hit.
- Radar/IPC errors with millisecond local timestamps.

The live frame block updates for every consumed frame. Historical frame movement is sampled every 0.25 seconds, continuous drag/scroll events every 0.12 seconds, and Down/Up/connection/error events are recorded immediately. History is capped at 160 entries so an extended field test cannot grow the UI log without limit. Use **CLEAR LOG** before reproducing a specific issue.

Use `RadarAndMouseDebug` while learning the sample, then switch to `RadarOnly` for production scenes.

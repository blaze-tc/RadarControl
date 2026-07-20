# Basic Interaction

Open `BasicInteraction.unity` and enter Play Mode. The scene is authored with ordinary Unity objects and explicit Inspector references; it does not build its UI from code.

The sample demonstrates the native EventSystem path end to end:

- `RadarInputModule` is the active `BaseInputModule` on the scene EventSystem.
- `GraphicRaycaster` handles Button, Toggle, Slider and ScrollRect.
- `PhysicsRaycaster` and `Physics2DRaycaster` handle the 3D and 2D targets.
- Persistent UnityEvent listeners connect the controls to `BasicInteractionPresenter`.
- `SamplePointerTarget` implements the same pointer and drag interfaces used by normal mouse or touch input.

Use `RadarAndMouseDebug` while learning the sample, then switch to `RadarOnly` for production scenes.

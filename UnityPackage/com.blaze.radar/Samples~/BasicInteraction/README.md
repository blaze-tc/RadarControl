# Basic Interaction

Open `BasicInteraction.unity` and enter Play Mode. The bootstrap creates:

- a UGUI Button, Toggle, Slider and ScrollRect;
- a 3D Cube with `PhysicsRaycaster` interaction;
- a 2D Collider with `Physics2DRaycaster` interaction;
- `RadarInputModule` in `RadarAndMouseDebug` mode;
- Bridge launcher, Named Pipe dispatcher and debug overlay.

Mouse input is synthesized by `RadarInputModule` itself, so `StandaloneInputModule` is not needed and duplicate clicks are avoided. For deployment switch the input mode to `RadarOnly`.

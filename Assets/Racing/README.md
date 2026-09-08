# Circuit 01 - Dual-View Racing

Open `Assets/Racing/Scenes/DualViewPractice.unity`, enter Play mode, and focus the Game view.

- W / Up: accelerate.
- S / Down: brake; reverse after stopping.
- A / D or Left / Right: steer.
- Space: handbrake.
- C: switch between cockpit and third-person chase views.
- R: restart the trial and return the car to its starting position.
- Escape: pause or resume.

Vehicle simulation uses Unity WheelCollider and Rigidbody. Both views follow the same car and camera; switching does not reset physics. The cockpit camera is fixed to the vehicle's rendered pose. The chase camera smooths movement and checks for walls.

This version contains a Porsche 911 Turbo S with a detailed interior, a closed circuit, a three-lap solo time trial, best-lap storage, a digital speedometer, and a pause menu. Timing begins when the car crosses the start line. All four checkpoints must be crossed in order and in the correct direction to count a lap. The pause menu includes Resume, Restart, Switch Camera and Quit.

The vehicle is Porsche 911 with interior by n.brizitskaya:
https://sketchfab.com/3d-models/porsche-911-with-interior-877b1bc1739f4a2bb65d62fd7ffd9f75

The model was downloaded by the user and imported from the original FBX archive. Its materials have been rebuilt for URP, the driver's door closed, and the merged wheels and steering mesh separated for animation. Source files are preserved. Keep `Art/Porsche911/ATTRIBUTION.md` with distributions; the asset uses CC BY 4.0.

The asphalt and landscape use Poly Haven CC0 2K textures; see `Art/PolyHaven/SOURCES.md`. The road and terrain geometry are generated locally. The original red test car and simple environment shapes remain disabled in the scene for reference.

Local background verification results are in `Captures/driving-check.json` and `Captures/lap-check.json`; this generated directory is excluded from Git. Automated input was sent to a temporary Unity virtual keyboard. No operating-system input was generated, and normal input settings were restored afterward.

This is a playable test version. Driving uses an accessible WheelCollider setup, not a validated vehicle simulator. Opponent AI, engine audio, real-time mirrors, and a vehicle-selection garage are not included.

The scene generator refuses to overwrite an existing scene or discard unsaved scene changes. The original SampleScene is preserved.

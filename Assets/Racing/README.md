# Long Road - Dual-View Racing

Open `Assets/Racing/Scenes/LongRoadPlains.unity`, enter Play mode, choose a region and time of day, then select Start Race. The four long-road scenes are first in the enabled scene list. `DualViewPractice.unity` remains available as the original three-lap practice mode.

| Region | Route | Landscape |
| --- | --- | --- |
| Green Plains | 6.25 km | Rolling meadows, groves and broad sweeping bends |
| Pine Forest | 5.97 km | Dense fir woodland and undulating roads |
| Alpine Pass | 6.68 km | Climbing mountain roads, rock outcrops and snowy ridges |
| Red Desert | 7.25 km | Sand dunes, boulders and long straights |

Every region is an open point-to-point race against three Porsche opponents. AI uses Unity NavMesh navigation over the road and drives the same WheelCollider physics as the player. Corner lookahead controls braking and steering. Checkpoint gates must be crossed in order; leaving the route or getting stuck returns a racer to the last safe checkpoint. Rank, remaining distance, elapsed time and a local route map are shown during racing; the finish screen shows the standings and supports restarting or returning to region selection.

Opponents target 174, 184 and 194 km/h on clear straights, brake ahead of bends, steer toward available pickups and actively spend energy on boost when the road permits. Boost raises their target by 35 km/h. Actual speed depends on bends, traffic and available energy. Recovering resets their movement timer so returning to a checkpoint does not trigger an immediate second recovery.

Race rank uses the vehicle's current projected distance along the road. Hidden checkpoints cover the asphalt, four-metre shoulders and a vehicle-width margin, and allow short crest hops. Driving more than 30 m past an unvalidated checkpoint recovers the car to the last checkpoint; it cannot silently freeze the displayed progress. Teleporting past checkpoints cannot award a finish, and reversing or recovering updates the ranking distance immediately.

The finish marker retains the original race distance. Road, shoulders, terrain and regional scenery continue another 2.4 km along world Z beyond the original road endpoint, placing the end beyond the driving camera's 1800 m far plane when approaching or crossing the finish. The continuation is outside the timed course.

Random time selects day, sunset or night for each new race. A time can also be selected explicitly. Night races enable headlights, cooler ambient light and emissive roadside markers. Region selection uses previews rendered from the actual Unity scenes.

- W / Up: accelerate.
- S / Down: brake; reverse after stopping.
- A / D or Left / Right: steer.
- Space while steering: controlled handbrake drift; actual tyre slip earns boost energy above 30 km/h.
- Shift: consume energy for acceleration and glowing exhaust jets.
- C: switch between cockpit and third-person chase views.
- R: recover to the most recently crossed checkpoint (practice mode restarts the trial).
- Escape: pause or resume.

Vehicle simulation uses Unity WheelCollider and Rigidbody. Both views follow the same car and camera; switching does not reset physics. The cockpit camera is fixed to the vehicle's rendered pose. The chase camera smooths movement and checks for walls.

The upper-right minimap renders the actual road, ground, vegetation and vehicles from above. It stays north-up, follows the player with a forward offset, and adds heading markers for the player and nearby opponents, a north indicator and a 50 m scale. A 536 x 448 orthographic view covers 280 m vertically and refreshes at up to 15 Hz. Its independent camera exposure keeps the terrain readable at night without changing the driving view. Rendering pauses with the race; its camera, texture and volume profile are released when the scene unloads.

The PC URP asset uses the standard SRP Batcher with GPU Resident Drawer disabled. Unity 6000.6.0f1 repeatedly crashed in GPU Resident Drawer's native ObjectDispatcher while unloading the forest and loading the next region during multi-camera validation. This compatibility setting avoids that renderer path; material instancing and vegetation LODs remain configured.

A live rear-view mirror at the top centre shows following traffic in both cockpit and chase modes, with race time and remaining distance below it. The mirror follows the vehicle, reverses the image horizontally like a mirror, and uses a dedicated 864 x 224 camera texture. Its camera stops rendering while paused or outside the race HUD and releases its texture when the scene unloads.

Steering is smoothed in the physics step and limited by speed-dependent curvature. High-speed full steering requests a wider turn, so braking before tight corners remains necessary. Lower chassis mass placement, a suspension anti-roll force on each axle, and contact-normal downforce reduce rollover during steering without locking body rotation.

Forward drive torque is 1325, with a 1.9 multiplier during boost; speed limits are 220 km/h normally and 275 km/h while boosting. Boost also adds forward acceleration along the ground while at least two wheels are grounded, the car is upright and the driver is accelerating without braking.

The car starts with 35 energy. Drifting replenishes energy based on sustained slip, and boost consumes 16 energy per second. Green Energy pickups add 35 energy; blue Turbo provides five seconds of automatic boost without consuming energy; amber Grip increases grip for eight seconds. Pickups can be taken by either the player or opponents and reset with each race. Recovery costs 15 energy and clears Turbo. The pause menu includes Resume, Restart Race, Regions, Camera, SFX volume and Quit.

The original practice scene contains the same Porsche with a detailed interior, a closed circuit, a three-lap solo time trial, best-lap storage and a pause menu. In practice mode, timing begins at the start line and all four checkpoints must be crossed in order to count a lap.

The vehicle is Porsche 911 with interior by n.brizitskaya:
https://sketchfab.com/3d-models/porsche-911-with-interior-877b1bc1739f4a2bb65d62fd7ffd9f75

The model was downloaded by the user and imported from the original FBX archive. Its materials have been rebuilt for URP, the driver's door closed, and the merged wheels and steering mesh separated for animation. Source files are preserved. Keep `Art/Porsche911/ATTRIBUTION.md` with distributions; the asset uses CC BY 4.0.

The asphalt and landscape use Poly Haven CC0 textures and scanned rock and fir models; see `Art/PolyHaven/SOURCES.md` and `Art/LongRoad/SOURCES.md`. Long-road terrain and roads use locally generated chunked meshes. Each roadside tree uses one centred scanned variant with separate wood and foliage material slots, switching to the existing simplified fir meshes at a distance. The original red test car and simple environment shapes remain disabled in the practice scene for reference.

Pine Forest distributes trees along both sides of the full route, with several rows extending into the landscape. Placement checks the tree canopy against nearby road segments and leaves the asphalt and shoulders clear. The generated forest contains 7865 trees, including the continuation beyond the finish.

Local background verification results are in `Captures/driving-check.json` and `Captures/lap-check.json`; this generated directory is excluded from Git. Automated input was sent to a temporary Unity virtual keyboard. No operating-system input was generated, and normal input settings were restored afterward.

Vehicle audio includes an engine loop with throttle/rev response and gear changes, tyre-slip sound, road/wind noise and collision impacts. Cockpit mode applies a low-pass filter and reduced exterior noise. Pausing suspends playback. The pause menu includes a persistent SFX volume slider. See `Audio/ATTRIBUTION.md` for the CC0 engine recording and synthesized supporting effects.

`Tools/generate_vehicle_audio.py` regenerates the four WAV assets using Python's standard library. `Racing/Install Vehicle Audio` assigns them to the current scene. `CircuitRacing.Editor.VehicleAudioSetup.RunBatch` is the Unity batch entry point for installation, playback-state verification and building `Builds/Circuit01-Audio`. Its report is `Captures/audio-check.json`. A batch runner may report that mixer capture is unavailable; this is recorded separately from clip and playback-state validation, and does not claim speaker playback was heard.

This is a playable development version. Driving uses an accessible WheelCollider setup, not a validated vehicle simulator. A vehicle-selection garage is not included.

The scene generator refuses to overwrite an existing scene or discard unsaved scene changes. The original SampleScene is preserved.

Development workflow: only generate executable builds or release archives when explicitly requested. `CircuitRacing.Editor.VehicleStabilityChecks.RunBatch` runs background handling measurements and writes `Captures/stability-check.json`; it does not build the game. The audio batch entry point above includes a build step and should only be invoked when a package is requested.

When a package is requested, `Racing/Build Long Road Windows` or `CircuitRacing.Editor.LongRoadBuild.RunBatch` builds the four current regions for Windows x64. It creates a timestamped `Builds/LongRoad-Windows-x64-...` folder and ZIP, includes player instructions and asset credits, and writes `Captures/long-road-build-result.json`. Previous packages are preserved. Unity's optional debug-backup directories are excluded from the ZIP.

Long-road editor commands do not package the game:

- `CircuitRacing.Editor.CreateLongRoadScenes.RunBatch`: regenerate the four long-road scenes and their generated assets from the preserved practice Porsche. Existing generated long-road scenes are replaced; save custom scene work elsewhere before regeneration.
- `CircuitRacing.Editor.LongRoadChecks.RunBatch`: background, muted ability, checkpoint, camera, lighting and complete AI-race checks. Writes `Captures/long-road-check.json`. With `-nographics`, minimap and mirror render textures are not created; camera checks cover pose and switching, not rendered output.
- `CircuitRacing.Editor.RoadProgressChecks.RunBatch`: deterministic replay of ranking, shoulder and crest-hop checkpoint passages, real overtakes, reversal, recovery and finish validation on all four routes. Writes `Captures/road-progress-check.json`; does not build the player.
- `CircuitRacing.Editor.FinishSceneryChecks.RunBatch`: verify original finish distances, road and terrain coverage beyond the finish, scenery continuation, road clearance from tree colliders and forest density. Writes `Captures/finish-scenery-check.json`; with a graphics device, also captures day/night finish views in both camera modes and three forest locations.
- `CircuitRacing.Editor.LongRoadChecks.CaptureBatch`: render region day previews and night captures offscreen. Requires a graphics device; omit `-nographics`.

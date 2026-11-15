# BODY-ANIM v1: evidence frames (Claude body writer)

Rendered by `Assets/_OutOfReach/Tests/BodyAnim/BodyFrameCaptureTests.cs` in Unity 6000.3.28f1 batch mode with a real GPU.
Setup: the real FitV02 WORK and EVA bodies (`I06BodyAuthoring.Create`). The presenter is driven by `AdvancePresentation`, using synthetic feet motion without physics.
The berth frames use the real habitat model `HB_Habitat_Placed.fbx` and its authored anchors from `HabitatLayout.json`.
These frames are a test harness, not real playback. They are also not visual acceptance.

| File | What it shows |
|---|---|
| `01_walk_work_side.png`, `02_walk_eva_side.png` | One stride at 2.5 m/s, side view. Heel strike, toe-off and opposing arm swing. The EVA profile takes shorter, wider and stiffer steps. |
| `03_turn_in_place_lookdown.png` | First-person look-down at 70 deg during a 180 deg turn in place. The feet step instead of rotating. |
| `04_stop_settle_side.png` | A stop from 2.5 m/s. The foot in the air lands, then a settle step brings the feet side by side. |
| `05_ramp_side.png` | Walking up a 12 deg ramp. The feet are placed on the ramp surface. |
| `06_float_legpush_side.png` | Floating legs in the neutral posture, then the leg push: plant, tuck, extension, release. |
| `07_berth_enter_strap_aisle.png` | Berth rest seen from the aisle. The body docks in the bag at the camera cut, the limbs settle, then the right hand draws the chest strap and the hip strap. |
| `08_berth_enter_strap_firstperson.png` | The same sequence from the real SleepCamera anchor, pitched 50 deg down. The bag top hides the chest; the strapping hand is visible. |
| `09_berth_release_aisle.png` | An ordinary wake: unclip the chest, then the hips. At 0.9 s the camera cuts back to the gameplay eye and the body goes with it. |
| `10_berth_held_aisle.png` | Held: both straps buckled around the actual skin of the torso and arms. |
| `11_lookdown80_walking_work.png`, `12_lookdown80_walking_eva.png` | First-person look-down at 80 deg while walking. |
| `15`–`18_real_scene_lookdown*_before_after.png` | **P1 #7 6895779523 item 4, the bulging belly.** Real `Bootstrap.unity`, real input. The mission is started through `I01MissionComposition.StartTimedPlanning()`. A virtual mouse drives the actual `LookPointer` action until the actual controller pitch reaches 70 and 80 deg. Frames come from the actual gameplay camera. BEFORE = the presenter as deployed at c772511 (Alex's tested build had no body-anim package). AFTER = this branch. Test: `Tests/RealScene/BodyLookDownRealSceneTests.cs` (PlayMode). EVA: the suit is only reachable in the Repair phase, so the test switches on the session's own suit flag in the same scene. |
| `13_seat_harness_side.png`, `14_seat_harness_firstperson.png` | Prologue seat harness on the real `TransportLaunch.fbx` chair. The frame matches root's `LeadDepartureAuthoring`: seat body frame at (0, .12, 1.46), yaw 180, seated eye at (0, 1.397, 1.40). Shown on the departure playhead from 0 to 5 s, with root's glance toward the buckle replayed. |

Measured in the same run (`TestResults/rest3.log`):

- **Planted feet:** the flat-foot ankle path stays at or under 0.65 mm at 1.5 / 2.5 / 3.75 m/s and in a 60 deg/s curve.
- **Turn in place, 180 deg at 120 deg/s:** 10 steps.
- **Stop:** the feet settle in 0.70–0.78 s.
- **Ramp:** the ankle height on the ramp is within 4.1 mm of the flat-ground height.
- **Berth fit:**
  - The eye offset is (0, 1.6514, 0.0926) m from the feet. This puts the berth body frame at (-2.2326, 0.3086, 13.03) in habitat space, yaw 90 deg.
  - The nearest skin vertex to SleepCamera is 0.132 m away.
  - 78 skin vertices lie up to 15 mm behind the berth back plane x = -2.380. They are hidden behind the bag.
- **Release timing:** an ordinary wake takes 1.38 s. A hazard wake (CO2) takes 0.27 s.
- **Real-scene look-down** (`TestResults/real_before.log`, `real_after.log`). Belly and lower-chest skin (0.95–1.40 m above the feet) as a share of the screen, and the nearest skin to the camera:

  | Outfit, pitch | Before | After |
  |---|---|---|
  | WORK, 70 deg | 0.3 %, 0.265 m | 0.0 %, 0.354 m |
  | WORK, 80 deg | 6.9 %, 0.265 m | 1.0 %, 0.374 m |
  | EVA, 70 deg | 0.4 %, 0.264 m | 0.0 %, 0.359 m |
  | EVA, 80 deg | 7.2 %, 0.264 m | 1.6 %, 0.375 m |

- **Seat harness** (`TestResults/seat4.log`):
  - It latches at 2.83 s on the playhead.
  - The nearest skin to the seat eye is 0.077 m away.
  - Pause holds the pose. A suit on, or `EndDepartureSeat`, releases it at once.
  - Shoulder-belt webbing sank up to 11 mm into the chest; it now sits 0 mm into the skin.
  - Not changed: the seated body is upright, while the chair back is reclined about 34 deg. The gap from the back to the chair is 112 mm at the pelvis and 371 mm at the upper back. Reclining the body needs root's SeatCamera to move with the eye.

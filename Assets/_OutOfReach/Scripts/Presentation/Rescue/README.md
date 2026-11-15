# RESCUE-ENDING-01: first-person rescue arrival (backlog R42)

Owner: Claude / Apoliak7777, lane `rescue-ending`. Assembly `OutOfReach.Presentation.Rescue` (Contracts and Input
System only). New files only. Core, Contracts, Bootstrap, the existing cinematic presenter, the audio assets and the
music are unchanged.

## What the player sees

Core already ends a survived mission with `Recovered` and requests `RecoveryArrival` → `MarsDeparture` → Report.
`RescueEndingPresenter` answers only `RecoveryArrival`, with a 37.5 s first-person sequence (see `RescueEndingTimeline`):

1. **Cabin window (0–14 s).** A short dip, then a cut to the starboard porthole; if the eye is already near it, a glide
   instead. The original craft (`RescueCraftV01`) approaches and its braking thrusters fire. Its floodlight pours into
   the cabin, then it slides aside towards the aft hatch. **VO24a** starts at 1.6 s: "Aurora Seven, rescue crew. …
   Stand by for docking."
2. **Aft docking camera (14–25 s).** The centreline feed at the hatch, with a reticle and a live range readout:
   1. The craft closes and makes **visible contact** at 19.4 s, with a contact clunk and shake.
   2. **VO24b** starts at 20.0 s, after visible contact, as the Story02 manifest requires: "Contact. Capture
      confirmed. Hard dock. …"
   3. Capture at 21.35 s: the collar lights turn cyan.
   4. Hard dock at 23.25 s: the latches pull in the last centimetres, with a big clunk and shake.
3. **Airlock (25–37.5 s).** Three latch lamps turn cyan and the pressure hiss plays. A seam of light appears, then the
   hatch opens and warm light floods the airlock as "...you're not alone anymore." is spoken. Then a fade to black.

When the sequence ends, the presenter restores every value it touched and publishes exactly one
`CinematicFinished(RecoveryArrival)`. Core then requests the **existing** `MarsDeparture`, followed by Report.

The subtitles follow the approved transcripts word for word, using the measured speech segments of the exact WAVs.
They go through the existing flight-board subtitle path, with the speaker "RESCUE CREW · RADIO". Neither voice line
says a day number, so the open Core day-30 vs VO23 day-31 question does not touch this sequence.

## Rules kept

- **Pause:** the authoritative clock pauses the playhead and both audio sources.
- **Skip:** holding Space or Enter for 0.6 s, or calling `SkipActive()`, acknowledges the sequence as skipped once.
- **Reduced effects:** no shake, glide, push or handheld sway; it cuts under black only and the lights stay steady.
- **Loss or change:** Core only requests this sequence for `Recovered`. A lost outcome, a phase change, a new
  `MissionEnded`, reconfiguration, disabling or `Release()` all end it at once, acknowledged as skipped exactly once.
- **Story and sound:** the craft was already in transit, so no Earth launch is shown. Space is silent: thrusters make
  no sound, and only the radio and the contact-transmitted structure noises are heard.
- **Fail-open:** if the presenter is missing, unconfigured or has invalid views, the request passes through the filter
  and the existing tableau plays.

## Stars in the ending (backlog R20)

Bootstrap's saved space skybox is already the NASA sky: `Visuals/Environment/NasaDeepSkyV01/NasaDeepSky.mat`
(NASA SVS Deep Star Maps 2020, `starmap_2020_4k.exr`; Gaia DR2: ESA/Gaia/DPAC). It is saved at the bright-ship
exposure 0.15, and at that exposure the stars are effectively invisible (`CELESTIAL_FRAME.md` section 6), so the
ending played against a black sky.

`RescueEndingSky` shows a **runtime copy of that same saved material** at the existing deep-view exposure **0.45**
(the value of `NasaDeepSky_DeepView.mat` / `I06NasaDeepSkyAuthoring.DeepViewExposure`):

- It goes up on the first frame of the ending and eases from 0.15 to 0.45 over the first 1.5 s of the playhead. With
  reduced effects it is at 0.45 at once. The ramp follows the playhead, so pause holds it.
- It stays up while Core is in the recovered `Outcome`, which covers the `MarsDeparture` shot that Core requests next
  (a skip also leaves the full deep view for it). It comes down when the phase changes (the report), on any other
  cinematic request, a lost or changed mission, `Configure()`, `Release()`, disabling or destruction. A per-frame
  guard lowers it if none of those events arrived.
- Only `RenderSettings.skybox` is swapped. No asset, scene, light or ambient probe is changed, and nothing is added to
  Bootstrap: **the installer does not need to be re-run for this.**
- Texture, mapping, tint and rotation are the saved ones. This adds no claim about the sky's orientation beyond
  `CELESTIAL_FRAME.md`; the exposure is artistic, as `NasaDeepSkyV01/NOTICE.txt` already states.
- Fail-open: if the active skybox is not a material using `starmap_2020_4k` with an `_Exposure` property (or there
  is no skybox), nothing is touched and the ending plays as before.

The airlock shot is an interior with no opening to space, so no sky is in frame there.

## Stars in every view of space (SPACE-SKY)

The same saved sky is behind the Earth-orbit shot of the departure (`I06MarineBindings.orbitalSky` is
`NasaDeepSky.mat`) and behind the whole EVA, so both looked out on plain black. `SpaceSkyKeeper` is the one owner of
the deep view in the mission scene. Its rule has no game state: while the active skybox is the saved NASA sky
(exposure 0.15), it shows a runtime copy at the existing deep-view exposure **0.45**; any other skybox (the launch
site's `MAT_LaunchSky_RockyRidge`, the upper-atmosphere transit sky) or none is left alone.

- It installs itself in the Bootstrap scene (`RuntimeInitializeOnLoadMethod`, the R18 pattern): no installer, no
  scene edit, no composition change. Test scenes do not get one.
- Only `RenderSettings.skybox` is swapped. Bootstrap's ambient is flat colour, and no light, fog, ambient or
  reflection setting is written; `NasaDeepSky.mat` still reads 0.15.
- There is no transition, so pause and reduced effects need nothing. A restart needs nothing either: the launch
  site has its own sky.
- One owner: `RescueEndingPresenter.TryBegin` calls `SpaceSkyKeeper.HoldNow()` before its own `Raise()`, which then
  finds the sky already at 0.45 and creates no copy. In the mission the ending's own 1.5 s ramp therefore no longer
  runs (the stars are already there) and the sky is not lowered at the report. Without a keeper (tests, another
  scene) the ending raises and lowers the sky exactly as described above.
- `SpaceSkyKeeper.Enabled = false` is the kill switch: the saved sky is back on the next frame.
- This is wider than the two shots it was written for: every view of space in the mission (cabin windows, the
  exterior cinematics, the EVA) now shows the star field.

## Assembler steps (sole integrator)

1. Merge `feature/rescue-ending-01` (new files only).
2. Apply the composition hooks:
   - `git apply --3way Assets/_OutOfReach/Scripts/Presentation/Rescue/Integration~/composition-rescue.patch`, or
   - `python -I Assets/_OutOfReach/Scripts/Presentation/Rescue/Integration~/apply_composition_rescue.py <repo root>`
     (anchored and idempotent; it writes nothing if any anchor is ambiguous).
   - Either way it touches only `I01MissionComposition.cs` (10 hunks) and the Composition asmdef (+1 reference).
3. Install into Bootstrap, using either:
   - the menu **OUT OF REACH → Integration → Install rescue ending (RESCUE-ENDING-01)** with Bootstrap open, then save;
   - or batch: `-executeMethod OutOfReach.Presentation.Rescue.Editor.RescueEndingInstaller.InstallBootstrapBatch`.
   - The report line must read `RESCUE_ENDING_INSTALL_PASS … validViews=True … composition=bound`.

The installer reads the existing camera chain from `MissionCinematicPresenter`: `cameraOffsetPivot`,
`presentationCamera` and `blackoutOverlay`. It reads the ship anchors from `ShipVisualAnchors`, binds VO24a and VO24b
from `Audio/Voices/Story02/`, and creates the materials in `Visuals/Props/RescueCraftV01/Materials/` when they are
missing. It adds one object tree, `Rescue ending (RESCUE-ENDING-01)`, beside the cinematic presenter. Its visuals stay
inactive outside the ending. Running it again replaces that tree.

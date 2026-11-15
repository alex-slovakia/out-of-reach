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

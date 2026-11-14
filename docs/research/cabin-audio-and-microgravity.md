# Cabin movement and audio — NASA research, 14 November 2026

Purpose: respond to Alex's request for footsteps, audible music and more physically plausible movement. This is an implementation brief, not a claim to have NASA footstep recordings or a measured AURORA-7 acoustic model.

## Primary sources

- [NASA: Sound Monitoring and Control for NASA's Spacecrafts](https://www.nasa.gov/wp-content/uploads/2024/09/1-sound-monitoring-and-acoustic-control-spec-sheet-508.pdf). Describes cabin machinery and maintenance noise, acoustic monitoring, crew hearing and alarm/communication audibility. Supports audible cabin mechanisms; it does not specify our game's boot sound or volume.
- [NASA-STD-3001 Volume 2, Architecture, restraints and mobility aids](https://www.nasa.gov/reference/8-0-architecture-vol-2/). Describes movement with handholds and foot restraints in microgravity. Ordinary Earth-like walking in a free-falling cabin is not a faithful ISS locomotion model.
- [NASA: Spaceflight Q&A](https://robotics.nasa.gov/spaceflight-qa/). Describes ISS microgravity and methodical use of tethers, restraints and handholds during EVA.
- [ESA: How loud is space?](https://www.esa.int/ESA_Multimedia/Videos/2020/04/How_loud_is_space/%28lang%29). Supporting agency explanation: cabin sound can be loud, while sound does not propagate through the external vacuum.

## Game implementation

1. On the sea platform, ordinary boot contacts and movement bob are appropriate. Trigger contacts from actual grounded displacement, never from a held movement key while blocked.
2. Existing ship walking is a gameplay simplification. A proposed optional microgravity mode will use the single existing player authority, bounded inertial translation, controlled braking and handrails; no independent movement script may fight the controller. No walking bob or boot steps during floating. This mode is not yet implemented or approved for the saved playable scene.
3. Exterior sounds are local suit/radio or explicitly contact-transmitted effects. Do not place conventional airborne footfalls, engines or impact noise in empty space. Music is non-diegetic score.
4. Music and footstep assets authored for this project are original synthesized effects/composition, not NASA recordings. Preserve audibility of speech and warning cues. Loudness measurements in dBFS/LUFS are digital signal measurements, not a claim about cabin dBA or astronaut exposure.

## Audit finding

The I05 score is wired but measures approximately -24 LUFS in the assets, -29 LUFS after its music gain, and -43 LUFS during dialogue ducking. It is bass-heavy. These are offline signal and gain estimates; actual output and device audibility require a separate Unity playback check. The next mix candidate raises score assets by 4 dB, changes music gain from .55 to .8 and dialogue ducking from -14 to -8 dB, with peak/seam checks and listening before promotion.

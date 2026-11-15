# Rest/wake audio V01

Original 3.2-second wake accent: rounded low impact, restrained open-fifth harmonic bloom and a softened cabin-contact transient. This is a non-diegetic transition cue, not a claim that a belt has been animated or that sound propagates through vacuum.

The contact component reuses the project's original `departure-seatbelt-click.wav`, whose committed provenance records mathematical synthesis without external samples. The new low impact, harmonic bloom, noise and short stereo reflection tail are authored mathematically using the Python standard library. No external audio, paid generation, new dependency, or third-party music license is involved.

Target asset: `Assets/_OutOfReach/Audio/Production/RestWakeV01/rest-wake-cinematic.wav`.

Root should wire the existing `SurvivalChapterPresenter.WakePresentationStarted(int day, RestStopReason reason)` to `MissionAudioPresenter.PlaySfx(wakeClip, .7f)`. The existing SFX path supplies master/SFX volume, routing and pause/mute policy. Do not replay from Update or from every survival Changed event. No additional AudioSource or component is required. The UI event fires once for an observed actual rest exit when its presentation gate is available; no day advancement is authored by this cue.

Format is stereo 48,000 Hz PCM16; 153,600 frames; 614,444 bytes. Measured sample peak is -13.00 dBFS, whole-clip RMS -28.76 dBFS, with zero clipped samples and zero-valued first/last frames. At the suggested .7 playback multiplier, the isolated sample peak is approximately -16.10 dBFS before existing master/SFX gains. These are PCM measurements, not integrated loudness or a live listener test.

SHA-256: `56f0fd8967e6ddb0852109ade4403a3f0994220540bb2962352d3a8204a7124c`.

Importer is PCM, preload on, normalization off and 2D. The exact source hash, generator hash and output measurements are in `audio-provenance.json` beside the WAV. The reproducible generator is this folder's `generate_rest_wake.py`; pass explicit `--project` and `--output` paths. It verifies the original contact source hash before rendering.

Verification performed: successful offline generation, independent WAV decode/format check, clipping check and endpoint check. No Unity import, native listener/mixer capture, human listening approval, live gameplay test or delivery to the user's open editor is claimed by this handoff. Stage files stay outside the live Assets tree until root finishes its current import.

Root acceptance: verify one cue for manual Wake and one for a natural rest exit, no cue on ordinary HUD refresh/rebind, no bypass of pause/mute, and restrained level against the actual score and cabin bed. Confirm the cue does not mask critical warnings or voiced guidance before increasing gain.

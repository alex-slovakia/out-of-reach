# Prologue music and SFX auditions — 15 November 2026

Private development previews for Alex. No runtime audio, mixer, scene or settings have been replaced. These are new edits/sound-design variants, not three newly recorded orchestral compositions. Listening approval and in-game balance remain pending.

| File | Length | Direction |
|---|---:|---|
| `01-titan-deep-launch.mp3` | 80 s | Most cinematic: approved Titan phrase with a heavier, continuous low engine-pressure bed. |
| `02-cobalt-pressure.mp3` | 80 s | Darker and more restrained: approved Cobalt theme over wide, slowly moving rumble. |
| `03-threshold-original.mp3` | 80 s | Original minor-chord tension, metallic air and low pressure. No percussion. |
| `sfx01-ignition-engine.mp3` | 18 s | Turbopump rise, ignition and sustained atmospheric engine roar. |
| `sfx02-opening-shockwave.mp3` | 7 s | Low impact front, short crack and decaying structural rumble. |
| `sfx03-thruster-contact.mp3` | 6 s | Three short valve/jet pulses of increasing duration. |

MP3 padding adds approximately 0.024–0.040 s. Music previews continue through the full 60-second loading period and another 20 seconds. Their final three seconds fade only to finish the audition. There is no added drum ending and no switch to another track at 60 seconds. Titan uses only its already approved 10.9–35.9 s phrase, with a 5-second crossfade into a repeatable 20-second passage; it does not introduce the later Titan ending. Resonance is not used.

The generator also creates **120-second PCM music masters without audition fades** in ignored `.local/prologue-audio-20261115/`. Root can continue the selected bed into the cutscene, then fade/duck it deliberately. These are loop candidates: waveform boundary measurements are recorded, but final musical-seam listening and Unity playback remain required. Do not loop the faded MP3 audition files.

## Authorship and attribution

- **'Titan' by Scott Buckley**, released under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). [Official track](https://www.scottbuckley.com.au/library/titan/), www.scottbuckley.com.au. Adaptation: excerpt 10.9–35.9 s, 5 s crossfade/20 s repeated passage, original synthesized rumble, gain/loudness processing and audition fades. Original source SHA-256: `ce6d890556829b17152deb6216ceffa314469c09d858f07d0d302d81f2b48313`.
- **'Cobalt' by Scott Buckley**, released under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). [Official track](https://www.scottbuckley.com.au/library/cobalt/), www.scottbuckley.com.au. Adaptation: the previously approved 120 s game loop plus original synthesized rumble, loudness processing and audition fades. Approved input WAV SHA-256: `c9ad3c3db8446e3535f7f3a08a1c8cc230d29f37d0070f51ea50b0b75b048e0b`. Original MP3 source hash/provenance remain in the existing Cobalt notice and earlier audition manifest.
- **Threshold and all added rumble/SFX:** original deterministic synthesis authored for OUT OF REACH with Codex. No external samples, musicians' recordings or new dependencies. These are stylized sound designs, not recordings or calibrated simulations of a real rocket.

The prior verified composer license/permission record is in `../music-audition-20261115/README.md` and the shipped `Assets/StreamingAssets/ThirdParty/{Titan,Cobalt}/` notices. Retain both attribution notices in any selected adaptation's game credits. No composer endorsement is implied. These edits are game-development auditions, not a standalone music release.

## Scientific and integration boundary

The ignition/roar and shockwave can be atmospheric sounds during the Earth launch. In external space, any audible impact or thruster machinery must be **cabin/suit/contact-transmitted sound**, or clearly non-diegetic score. Do not play these as sound propagating across vacuum. The thruster cue does not add player thrusters, lateral force or any gameplay mechanic. Bind effects only to the authoritative vehicle/cinematic events; the overload/failure path is not implemented by this audio package.

## Reproduce and verify

Run from the integration checkout with existing Python/NumPy and FFmpeg:

```powershell
python docs/art-direction/prologue-audio-20261115/build_previews.py --titan-source 'C:/Users/alexk/Documents/Projects/OUT-OF-REACH-NASA-HACKATHON/.local/music-staging/sb_titan.mp3'
```

The generator verifies both approved input hashes, writes only this new audition folder and its ignored staging folder, uses 48 kHz stereo, loudness targets of -16 LUFS for music and -18 LUFS for SFX with -1.5 dBTP target, and measures the final encoded files. `manifest.json` records exact durations, SHA-256, file sizes, measured loudness/true peaks, decoded full-scale sample counts and PCM master locations. No subjective listening or Unity acceptance is claimed by numerical verification.

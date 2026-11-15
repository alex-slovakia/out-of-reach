# SFX v03 — recorded machinery auditions

Six **unapproved auditions**, 15 November 2026. Replaces the rejected v02 synthesis approach; no game assets or runtime wiring changed. Opening Titan v02 is untouched. No movie sound, soundtrack, generated white noise or oscillator was used. The reference is cinematic weight and contrast, not imitation of a specific copyrighted film recording.

Start with [the 41.5-second comparison](00-comparison-six-variants.mp3). It contains no voice labels or music; half-second gaps separate the clips.

| Reel time | Audition | Intended distinction |
|---|---|---|
| 0:00–0:06.5 | [01 — engine A](01-engine-A-turbine-spool.mp3) | J79 ignition/spool texture with a lower pitched turbine layer; mechanical onset. |
| 0:07–0:13.5 | [02 — engine B](02-engine-B-hull-mass.mp3) | NASA launch mass and a lower turbine bed; deeper cockpit/hull perspective. First engine candidate. |
| 0:14–0:20.5 | [03 — shock A](03-shock-A-structural-slam.mp3) | Short reverse mechanical intake, real metal impact, stretched shed resonance and a low launch tail. First impact candidate. |
| 0:21–0:27.5 | [04 — shock B](04-shock-B-pressure-collapse.mp3) | Lower pressure body, offset metal snap and a secondary structural rattle. |
| 0:28–0:34.5 | [05 — thruster A](05-thruster-A-valve-bursts.mp3) | Three distinct burn commands, recorded jet body and short valve-like metal attack. |
| 0:35–0:41.5 | [06 — thruster B](06-thruster-B-deep-contact.mp3) | Three deeper structural pulses, gentle left/right contact perspective. First thruster candidate. |

The impact begins about one second into each shock audition; the opening is intentional anticipation. Thruster clips demonstrate three pulse lengths, not a single 6.5-second burn. Engine clips are direction auditions, not seamless runtime loops.

## Sources and rights

Original files downloaded to `.local/sfx-cinematic-v03/sources/`; SHA-256 and byte sizes are recorded in [manifest.json](manifest.json). Freesound originals require login, so these use the publisher's public **HQ MP3 preview** downloads. This is a real source-quality limitation; conversion to a WAV master does not restore lost detail. No login, account creation, payment or generated-audio credit was used.

| Local source | Recording and author | Permission verified on source page | Direct downloaded media |
|---|---|---|---|
| `j79.mp3` | [Jet Engine Start Cart — C-V](https://freesound.org/people/C-V/sounds/704945/), described by its recorder as a military J79 start-cart field recording | CC0 1.0 | [HQ MP3](https://cdn.freesound.org/previews/704/704945_8956746-hq.mp3) |
| `jet.mp3` | [Jet Engine — m_cel](https://freesound.org/people/m_cel/sounds/534856/), jet static test | CC0 1.0 | [HQ MP3](https://cdn.freesound.org/previews/534/534856_7108357-hq.mp3) |
| `metal.mp3` | [Metal Hit 2 — dslrguide](https://freesound.org/people/dslrguide/sounds/321482/), wheelbarrow struck with an axe, Rode VideoMic Pro/Zoom H5 | CC0 1.0 | [HQ MP3](https://cdn.freesound.org/previews/321/321482_5485024-hq.mp3) |
| `shed.mp3` | [Metal Impact 1 — Yoyodaman234](https://freesound.org/people/Yoyodaman234/sounds/334680/), metal shed wall struck, Zoom H6 | CC0 1.0 | [HQ MP3](https://cdn.freesound.org/previews/334/334680_2792951-hq.mp3) |
| `launch.mp3` | NASA, Discovery STS-131 Sound of Launch, [Historical Sounds](https://www.nasa.gov/historical-sounds/) | NASA-created recording, public-domain status in the US under NASA media guidelines; NASA source acknowledged, no endorsement implied | [NASA MP3](https://www.nasa.gov/wp-content/uploads/2015/01/590189main_ringtone_131_launchNats.mp3) |
| `sofia.mp3`, `gear.mp3` | NASA SOFIA takeoff and STS-132 landing gear drop | Downloaded for source selection only; **not used** in the auditions | [SOFIA](https://www.nasa.gov/wp-content/uploads/2015/01/sofia_takeoff_audio.mp3), [gear](https://www.nasa.gov/wp-content/uploads/2015/01/590327main_ringtone_landingGearDrop.mp3) |

License references: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) permits adaptation and commercial reuse; [NASA media usage](https://www.nasa.gov/nasa-brand-center/images-and-media/) distinguishes NASA-created material from separately protected third-party content and prohibits implying endorsement. Source pages and these policy pages were saved locally beside the sources as verification evidence. The historical NASA source page did not identify a third-party rights holder for the selected launch recording. Credit for the edited result: **Sound design for OUT OF REACH; recordings: NASA, C-V, m_cel, dslrguide, Yoyodaman234.**

## Processing and verification

- Every sound layer comes from those recordings. Processing: excerpt selection, resampling/pitch shifts, high/low-pass filtering, level envelopes, layering and limited stereo placement. The low-end motion is the recorded motor's modulation, not generated noise.
- Six 48 kHz stereo, 24-bit WAV masters remain in `.local/sfx-cinematic-v03/`. Review MP3s use 320 kb/s. Rebuild with installed Blender Python/NumPy and FFmpeg using [build_auditions.py](build_auditions.py); no new dependency is required. The script states exact source excerpts, pitches, filters and gains.
- All seven final MP3s decoded successfully. FFmpeg `ebur128=peak=true` measured **−3.5 dBTP** for each; no clipped decoded samples in any individual audition. SHA-256 identifies the exact review revision. Logs remain in `.local/sfx-cinematic-v03/*-levels.txt`.
- [Decoded quality report](decoded-quality.json) and [spectral comparison](spectral-comparison.svg) quantify the actual output. Energy above 4 kHz is below 0.03% in every individual audition, reducing high-frequency hiss. This does not prove absence of audible turbulence, speech contamination or codec artifacts; those need listening. Strong low-mid energy is intentional, but may obscure dialogue or disappear on small speakers; a final game mix must be assessed at normal headphone/speaker volume.
- Source audio is about 4.94 MB; a timed-out duplicate request transferred another ~1.55 MB. Total selected audio transfer remained below 7 MB, within the 40 MB limit. No packs were downloaded.

**Quality limit:** these have source verification, waveform/spectral inspection and codec/level checks, but no human listening approval. Do not label them AAA quality based on metrics or silently replace the game's effects. The first candidates above are editorial directions to audition, not a claim that a listening test selected the winners. The pressure and metal transients deliberately remain dynamic rather than flattened to constant loudness.

For the game, rocket sound is appropriate in the atmospheric launch scene. In exterior vacuum, thruster/impact layers must be presented through suit/hull contact, radio or an explicitly non-diegetic cinematic mix; this audition does not claim that sound propagates through vacuum. Integrate only after Alex selects the direction, then verify onset, pause/restart, layering under dialogue and actual playback in Unity.

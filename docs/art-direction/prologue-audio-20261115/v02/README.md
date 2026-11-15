# Prologue audition v02

This revision follows Alex's rejection of noisy engine/shockwave/thruster effects and request for a rising, dramatic opening with deep booms and drums. V01 is preserved. These are listening candidates, not a claim of final mix approval or runtime integration.

## Playable previews

- **opening-titan-ascent-v02.mp3** — 84 seconds. A continuous developing orchestral excerpt of Scott Buckley's *Titan*, with original low membrane accents that grow into a restrained drum pulse. The score continues across the 60-second loading boundary; it does not restart there. Suggested opening candidate.
- **sfx01-engine-structure-v02.mp3** — 14 seconds. Mechanical ignition, ramp, sustained vibration and rundown. Use from inside the cabin or as contact-transmitted engine vibration.
- **sfx02-shockwave-structure-v02.mp3** — 7 seconds. Short impact followed by low structural resonance. There is no sustained broadband blast tail.
- **sfx03-thruster-contact-v02.mp3** — 7 seconds. Three valve/pressure impulses with increasing duration. These are an audition of cabin/contact-transmitted spacecraft audio, not an implementation of player movement thrusters.

The SFX use deterministic original synthesis: membrane modes, decaying metal resonances, valve clicks and low engine harmonics. Engine texture has a small noise component restricted to the low band; shockwave and pulse effects contain no noise generator. None use recordings from films or games.

## Musical development

The selected source passage runs from 48 to 132 seconds without cuts or loops. Its own orchestration rises through the preview. Added sparse low accents begin around 24 seconds, become a measured pulse around 42–60 seconds, and gain tom responses around 63–79 seconds. The final 2.5-second fade is for audition only. The PCM master has no audition fade, but this excerpt is **not a seamless runtime loop**; transition timing and voice ducking remain integration work.

The original local MP3 header reports 175.804 seconds but the actual decoded stream contains 132.520 seconds. This preview uses only valid decoded audio. No missing ending was invented or described as listened to. Source dynamics and transient timing were inspected programmatically; subjective listening approval is still required.

## Attribution and reproducibility

“Titan” by Scott Buckley — released under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). [Official track page](https://www.scottbuckley.com.au/library/titan/).

Changes: continuous excerpt selection, gradual gain staging, original percussion and quiet tonal vibration, mastering and an audition fade. Preserve this attribution in any distribution of the adapted music. Exact source and output SHA-256 hashes are in `manifest.json`. All three SFX are original project synthesis with no third-party audio source.

Run `python docs/art-direction/prologue-audio-20261115/v02/build_previews.py` from the integration checkout. Requires the already installed NumPy and FFmpeg, and the previously approved source MP3 in the original project's `.local/music-staging/`. No new packages or downloads are required. PCM intermediates and masters stay in the ignored `.local/prologue-audio-20261115/v02/` folder; only compressed previews, this document, the generator and manifest belong in Git.

The manifest records decoded duration, post-MP3 integrated loudness, true peak, clipped-sample count and source provenance. These checks confirm technical signal properties, not artistic quality. No Unity assets, runtime code or scenes were changed, and no Unity playback is claimed.

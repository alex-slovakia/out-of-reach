# Original instrumental score V2

These original project compositions supersede the V1 drone loops. No external samples, copied melody or NASA recordings are used. Human listening approval is pending.

- Isolation / mx01: **Cabin Radio - Small Blue Light**, 80 BPM, 48 s. Warm electric-piano harmony, original lead motif, bass and restrained brushed rhythm.
- Tension / mx02: **Signal Break - Hold the Line**, 100 BPM, 38.4 s. Minor harmony, pitched ostinato and a firmer pulse.
- Emergency / mx03: the same original crisis arrangement, retaining the existing Emergency meaning and canonical GUID.
- Recovery / mx04: **Another Dawn - Recovery**, 90 BPM, 42.667 s. A distinct hopeful motif and brighter harmony; only for actual recovery, never mere radio contact.

All 48 kHz stereo PCM16 loops measure about -20 LUFS with true peaks below -7 dBFS. Music gain .8 and speech duck -8 dB are listening candidates. Full mix, stems, 15 s previews and exact numerical evidence are delivered in AudioMixV02 staging. Existing music GUIDs are preserved; Recovery has a new GUID.

Rebuild using the existing numpy and ffmpeg installation, with an explicit isolated output directory:

`python Editor/build_music_v2.py --output-root <isolated-output>`

The source contains the actual note events, voicings, original instrument synthesis, deterministic percussion and circular room tails. It writes Assets/_OutOfReach/Audio/Music plus Previews, Stems and a provenance manifest under that output. The older build_music.py is historical V1 and will regenerate the superseded drone if run.

## Opening score: `titan-departure.wav` (AUDIO-OPENING, 15 Nov 2026)

Since `feature/audio-opening` this file is the **approved opening track**: a byte-identical copy of
`docs/art-direction/prologue-audio-20261115/v02/opening-titan-ascent-v02-master.wav` (84.000 s, 48 kHz stereo PCM16,
SHA-256 `926e994502373d4df7ec499404172c4c67d52a8f2f5c7631a3794ca938fa717c`). The old 34.5 s Titan cut is replaced;
path and GUID `827915f725a152f396bf51b2b2ecf184` are unchanged, so Bootstrap's `departureScore` and
`I01BootstrapBuilder` already point at it.

Attribution: "Titan" by Scott Buckley, CC BY 4.0, <https://www.scottbuckley.com.au/library/titan/>. Changes:
continuous excerpt 48-132 s, gain staging, original low percussion and quiet tonal vibration, mastering (see the v02
README and `Assets/StreamingAssets/ThirdParty/Titan/NOTICE.txt`).

Runtime use (`MissionAudioPresenter` opening program, driven by `I01MissionComposition`):

- **Menu:** silent. No mood track plays before a mode is chosen.
- **Loading** (Practice or Timed chosen): the score plays from 0 s with a 2 s fade-in. If the crew has not boarded
  when it reaches 47.4 s it fades out by 49.9 s, so the launch section is never used up while loading.
- **Departure** (seat, `TransportDepartureCue.Boarding`): the score enters at **49.90 s**, the downbeat that opens its
  final section, and from then on follows the departure playhead: liftoff 10.5 s = 60.4 s (the score's level step),
  climb cut 13.25 s = 63.15 s, docking 27.5 s = 77.4 s, departure end 34 s = 83.9 s. It fades out over the last 3.5 s.
  A loading score that is still audible crossfades out over 1.5 s.
- **After the departure:** no mood track returns until the mission leaves the Intro phase (the old "elevator music"
  jump at the end of the departure is gone). Skip fades the score out in 0.6 s; restart and cancel stop it at once.
- The score ducks -5 dB under the NASA launch radio (`Audio/LaunchRadio`) and pauses with the game.

The mood tracks above are unchanged and are not used during the opening.

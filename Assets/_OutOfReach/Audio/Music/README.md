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

# Original OUT OF REACH score

These three original compositions are production source assets ready for presentation integration. Human listening approval is still pending; no subjective quality or Unity playback claim is made.

| Asset | Mood | Tempo / length | Measured integrated loudness / true peak |
|---|---|---|---|
| `mx01_isolation.wav` | Quiet isolation; open minor pads and sparse low pulse | 60 BPM, 8 bars, 32 seconds | -24.0 LUFS / -15.2 dBFS |
| `mx02_tension.wav` | Low ostinato, cyclic swells, restrained ticks | 75 BPM, 10 bars, 32 seconds | -24.0 LUFS / -15.2 dBFS |
| `mx03_emergency.wav` | Syncopation and narrow dissonance without trailer impacts | 90 BPM, 12 bars, 32 seconds | -24.0 LUFS / -11.9 dBFS |

All files are native 48 kHz stereo PCM16, exactly 1,536,000 frames, with no loop-end silence. Import settings preserve PCM, stereo and sample rate. Their bytes match the reviewed staging hashes in `music-manifest.json`. Numerical wrap-step/slope checks and a byte-identical rebuild passed. These are signal checks, not an auditory loop review.

Composition, voicings, rhythms, synthesis patches and noise were originally authored for OUT OF REACH. No third-party audio samples, recorded performances, copied melodies, downloaded libraries or music-generation APIs were used. The score is explicitly non-diegetic; it is not airborne sound in vacuum.

`Editor/build_music.py` preserves the exact deterministic authoring script. To rebuild without writing generated intermediates into Assets, copy that file to an isolated output folder and run it with existing Python/numpy/ffmpeg. It writes only beside itself. Compare every output SHA-256 with the manifest before replacing production files. Keep existing `.meta` GUIDs.

Runtime playback uses `OutOfReach.Presentation.Audio.MissionAudioPresenter`. It starts silent and uses explicit caller-selected moods. Music attenuation during dialogue and mix levels are presentation settings, not scientific quantities or mission-state authority. See the owning `Audio/MUSIC_HANDOFF.md` for APIs and pending integration checks.

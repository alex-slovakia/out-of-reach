# English cinematic audio production checkpoint

Owner: Codex. Branch: `feature/visuals-cinematics`. Task: V02 audio preparation. No gameplay playback wiring is included in this checkpoint.

## Delivered assets

- `Audio/Voices/Clean/`: twelve dry, normalized voice masters.
- `Audio/Voices/Radio/`: twelve finished radio variants. Mission Control uses a stronger receiver filter; onboard speech uses a wider, clearer cabin band.
- `Audio/SFX/`: seven original procedural effects: selection, confirmation, planning tick, planning urgency, communications dropout, equipment power-down and warning pulse.
- Each of the 31 clips has an English `.txt` subtitle/caption. `Production/voice-cues.en.json` supplies exact text, speaker, context label and authoritative playback gate.
- `Production/audio-manifest.json`: SHA-256, duration, format, source, processing and peak measurements. Paths are relative to `Assets/_OutOfReach/Audio`.
- `Production/Editor/SourceTakes/`: twelve untouched generated source recordings and generation metadata, archived under an Editor directory for production reproducibility. Do not reference these production takes from the release scene.

All audio is native 24 kHz mono PCM16. Total finished WAV size is 9,894,164 bytes. No live TTS connection or API credential belongs in the game.

## Provenance and permitted-use basis

Original English dialogue was generated for this project using Google's `gemini-3.8-flash-tts`, with stock voices **Orus** (Mission Control) and **Kore** (ship). No real person was cloned and no third-party film/game recording was sampled. Generated voice outputs are governed by [Gemini API Additional Terms](https://aistudio.google.com/docs/terms), reviewed 14 November 2026; their generated-content section says Google does not claim ownership of outputs. These outputs are not labeled CC0 or exclusively owned recordings. They are prerecorded production assets, not a player-facing Gemini API client. See also [speech-generation documentation](https://aistudio.google.com/docs/speech-generation).

The seven SFX are original deterministic oscillator/noise synthesis in `finish_audio.py`; no external sound library, music or melody was used. Radio processing adds restrained band-pass filtering, compression, mild saturation and quiet opening/closing squelch. Credits can say: "Original dialogue and sound design: OUT OF REACH. Synthetic voices: Google Gemini TTS (Orus, Kore)."

## Rebuild and verification

With the already-installed Python/numpy and ffmpeg, run from the repository root:

```powershell
python Assets/_OutOfReach/Audio/Production/finish_audio.py
```

This makes no network requests and writes only to ignored `.local/audio-rebuild`. It preserves source recordings and does not overwrite committed clips. Compare rebuilt WAV SHA-256 values with `audio-manifest.json` before deliberately replacing production assets. The processor records its original and rebuild source hashes; byte-exact waveform reproduction is verified on the recorded ffmpeg version, not promised for every future tool version.

Automated source review checked all twelve takes with a locally cached speech-recognition model. Eleven matched normalized words exactly. The remaining take was transcribed as `PowerFault` instead of `Power fault` by two local models, with all following words matching. Both Mission Control radio variants independently matched every normalized word, including "Rescue is not yet confirmed." Evidence is in `asr-review.json`. This is not a claim of human listening approval, tone quality or perfect pronunciation.

All 31 finished WAVs have zero clipped samples, finite samples and a maximum sample peak of -2.0 dBFS. This is not a certified true-peak or broadcast-loudness measurement. Rebuilding from archived takes reproduces all 31 WAVs byte-for-byte in the checked environment.

Unity authoring verification: `OutOfReach.Presentation.Editor.AudioProductionChecks.ConfigureAndValidate` ran in Unity **6000.3.28f1** in `.local/unity-baseline-check` on 14 November 2026. Exit **0**; `AUDIO_PRODUCTION_PASS clips=31 nativeRate=24000 channels=1 sha256=matched decodedSamples=finite maxSamplePeak=0.794312`. The log is `.local/unity-baseline-check/audio-import.log`. Import settings and metadata were copied back with source WAVs unchanged. This verifies the audio assets and editor checker in this checkpoint, not later unrelated edits or a gameplay build.

## Integration requirements

Use one managed dialogue source and the game's existing single audio listener. Bind clips explicitly; do not create a second resource or mission authority. Respect the gates in `voice-cues.en.json`; snapshots must not replay completion lines. Stop obsolete queued lines on mission loss, restart, phase cancellation and scene unload. Subtitles must follow actual playback and be usable with audio muted.

- Briefing is labeled **PREVIOUSLY RECEIVED — MISSION CONTROL**.
- Optional false-signal clips are labeled **LOCAL BUFFER — OLD OUTBOUND RECORDING**. They never mean Earth has answered.
- Final Earth reply plays only after authoritative `CommunicationChanged.State == ReplyReceived` and when the mission is not lost. Presentation timers never unlock this line.
- Airlock speech does not open a gate. Exterior sounds are suit, radio or contact-transmitted effects.
- Planning beeps are available, but the requested 60-second planning deadline needs coordinated gameplay ownership and timeout behavior. Audio does not implement that clock.
- Captions next to SFX are production descriptions; the integrator should use short player-facing non-speech captions such as `[Radio static.]` rather than exposing implementation notes.

Remaining acceptance: human listening of every take, in-game voice/SFX mixing and ducking, subtitle timing, interruption/restart tests, reduced-effects/mute behavior and a complete playable success/failure run.

# English cinematic audio production checkpoint

Owner: Codex. Branch: `feature/visuals-cinematics`. Task: V02 audio preparation. No gameplay playback wiring is included in this checkpoint.

## Current selected assets - provenance correction, 14 November 2026

The production checkpoint below is historical. Clean `vo01`-`vo10` were subsequently replaced by approved **Orus Clean-v3** masters. Their local generation receipts and finished-master hashes identify **`gemini-3.8-flash-lite-tts`**; `clean-runtime-selection.json` now records that evidence. These are different raw takes from the original `Editor/SourceTakes` recordings. The ten replacement raw WAVs/receipts remain local production artifacts (`.local/audio-generation-orus-v3/`), not a committed source archive. The corrected per-file entries in `audio-manifest.json` identify the actual current files; its top-level original processor/import/ASR results do not certify the replacements.

The observed saved Bootstrap binds seven Clean-v3 cues (`vo01`, `vo03`, `vo04`, `vo06`, `vo07`, `vo09`, `vo10`) and three approved Dramatic Orus clips: `02_natural_crisis`, `03_exhaustion_relief`, `05_houston_to_earth`. Their own `Voices/Dramatic/voice-manifest.json` records matching generation evidence for the same flash-lite model. The retained Clean `vo11` is an older Orus master (`gemini-3.8-flash-tts`); it is available but the current reply binding uses Dramatic `03`. Clean `vo12` is an unselected older Kore report take. Radio variants and original archived takes retain their earlier provenance. Exact scene field/GUID/hash mapping is in `clean-runtime-selection.json`; it is static binding evidence, not a new listening test.

Voice model names here are the recorded generation-request identifiers, not an independent provider/version certification. All current scene-bound dialogue is synthetic Orus speech, not NASA mission audio, Retellai or Charon. Credits for that selection can say: **"Original dialogue and sound design: OUT OF REACH. Synthetic dialogue: Google Gemini TTS, Orus."** Include Kore when crediting the older available production library. Preserve the permitted-use basis below; no CC0 claim or expanded license is made by this correction.

## Original delivered assets

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

This makes no network requests and writes only to ignored `.local/audio-rebuild`. It preserves source recordings and does not overwrite committed clips. The original rebuild reproduces the original production pass, not the later Clean-v3 replacements. Compare each clip with its matching processor/source record in `audio-manifest.json` before deliberately replacing production assets. The processor records its original and rebuild source hashes; byte-exact waveform reproduction is verified on the recorded ffmpeg version, not promised for every future tool version.

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

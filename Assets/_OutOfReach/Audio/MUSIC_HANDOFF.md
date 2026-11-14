# Music and audio presenter handoff

- Task: V02 audio presentation foundation; original score asset production.
- Branch: `feature/visuals-cinematics`; source base `0be6d6a0f4272088ca0acd2956eb03a6be279fbd`. This handoff is an uncommitted working-tree checkpoint, not a tested commit.
- Writer: delegated audio agent; exclusive paths `Audio/Music/**`, `Audio/Runtime/**`, their folder metadata, and this handoff. Voices, SFX, Production, shared contracts, Bootstrap and settings were not edited by this task.
- Editor target: Unity 6000.3.28f1. No Unity process, compile, playtest or build was run by the audio writer. Root owns isolated Unity verification.

## Deliverables and provenance

The three WAVs under `Audio/Music/` are byte-for-byte copies of original 32-second staged compositions. `music-manifest.json` includes hashes, native format, tempo, tonal center, LUFS, peak, seam values/slopes and authorship. `signal-verification.json` records a byte-identical offline rebuild, zero clipped samples and numerical loop-seam checks. `Music/Editor/build_music.py` preserves the exact original generator. No third-party samples, melodies or API-generated music are used.

Music human-listening review remains pending. All three measured -24.0 LUFS; true peaks are -15.2, -15.2 and -11.9 dBFS. Music files total 18,432,132 bytes. PCM import preserves the original stereo/rate and avoids codec padding at the loop seam. Runtime memory/performance and audible looping remain unmeasured.

## Runtime API

Assembly: `OutOfReach.Audio`. Namespace: `OutOfReach.Presentation.Audio`.
Component: `MissionAudioPresenter`. It has no compile-time dependency on Core or Contracts because the designated presentation coordinator calls it explicitly after consuming authoritative events.

```csharp
bool PlayDialogue(AudioClip clip, string subtitleText, string cueId);
bool TryPlayDialogue(string cueId);
void StopDialogue();
bool SetMood(MusicMood mood, float fadeSeconds = -1f);
bool PlaySfx(AudioClip clip, float volume = 1f);
bool TryPlaySfx(string cueId);
void SetPresentationPaused(bool paused);
void StopAllPresentation();
void SetLevels(float master, float music, float dialogue, float sfx);
void ConfigureClips(AudioClip isolation, AudioClip tension, AudioClip emergency,
    DialogueCueBinding[] cleanDialogues, SfxCueBinding[] effects);
bool ValidateConfiguration(out string reason);
event Action<DialogueSubtitle> SubtitleChanged;
```

`MusicMood` is `Silent`, `Isolation`, `Tension`, `Emergency`. `DialogueSubtitle` exposes `CueId`, `Text`, `IsVisible`. Read `CurrentSubtitle` when binding a UI, then subscribe; unsubscribe on UI disable/disposal. Read-only presenter properties include `IsDialoguePlaying`, `ActiveCueId`, `IsPresentationPaused`, `CurrentMood`, and `LastRejection`. `CurrentMood` is the requested presentation mood even during crossfade; it is not a game phase.

Bindings have constructors `DialogueCueBinding(string cueId, AudioClip cleanClip, string subtitleText)` and `SfxCueBinding(string cueId, AudioClip clip, float volume = 1f)`. Catalogs and all three music references are serialized. Clean dialogue is the default: there is no radio effect, fallback search, path lookup or automatic voice selection. The integrator must supply the accepted take explicitly; this task does not choose between existing takes.

Playback returns false for unavailable/unloaded clips, inactive or paused presentation, duplicate active dialogue, ambiguous catalog IDs or invalid parameters. Inspect `LastRejection`; rejected playback publishes no domain success. A currently playing line is replaced by a different explicit cue; its subtitle is hidden before the replacement appears. A repeated active clip/id is rejected. Once finished, the same cue can be intentionally replayed; authoritative event deduplication remains the coordinator's responsibility.

## Lifecycle, mix and integration anchors

- Starts silent; no autoplay, listener, camera, singleton, event bus subscription, domain mutation, timing threshold or success/failure decision.
- Creates five owned 2D sources at runtime: one dialogue, one one-shot SFX, three looping score channels. Three score channels permit interrupted crossfades without abruptly replacing a still-audible loop. Silence fades all channels out.
- Default crossfade 1.25 seconds. Dialogue reduces music by 14 dB, with 80 ms attack and 650 ms release. These are adjustable presentation choices, not simulation constants. Only music ducks; the clean voice is unchanged.
- Default levels: master 1, dialogue 1, music 0.55, SFX 0.8. Optional existing `AudioMixerGroup` references allow later routing; no mixer package or new dependency is required.
- Explicit pause freezes owned playback, crossfades and ducking. It does not set `Time.timeScale`, `AudioListener.pause` or the simulation clock. The coordinator must call it alongside authoritative pause/resume. A global listener pause is respected without treating the paused line as finished.
- Fades otherwise use unscaled presentation time. Muting dialogue keeps subtitle timing. Disable stops all owned audio and hides the current caption. Re-enable remains silent. Destruction removes the owned channel objects. `StopAllPresentation` preserves the explicit pause flag; restart orchestration should also set pause false.
- Music is non-diegetic. SFX are limited in intent to interface, suit, radio or contact transmission; never imply external airborne sound. Overlapping one-shot SFX and final mix headroom require a real playback check.
- Earth-reply voice is called only after the authoritative received-reply event; neither audio duration nor mood generates `CinematicFinished`, phase transitions, rescue, repairs or mission outcomes.

## Optional owned prefab authoring

After compiling in the isolated Unity project, root may invoke:

```text
OutOfReach.Presentation.Audio.Editor.MissionAudioAuthoring.BuildMusicAndSfxPrefab
OutOfReach.Presentation.Audio.Editor.MissionAudioAuthoring.ValidateSavedPrefab
```

The authoring method saves only `Assets/_OutOfReach/Audio/Runtime/MissionAudioPresenter.prefab`, binds the three original loops and seven existing SFX clips, and intentionally leaves dialogue bindings empty pending take selection. It does not modify the referenced clips, Bootstrap or saved settings. Running it again resets the authored prefab dialogue catalog to empty, so do not regenerate over later integrator voice bindings without reviewing that change.

The prefab contains no authored AudioSources or AudioListener. Runtime channels start silent. The validator checks references, duplicate IDs, listener/source exclusions and rejects rejected-radio-folder defaults. It is a serialized-asset check, not playback evidence.

## Verification and remaining checks

Actually run before handoff: exact SHA-256 comparison of all three imported WAVs against staging and the exact generator script hash; original generator/rebuild evidence reviewed; native 48 kHz stereo PCM16, 32-second durations, original metadata and source-only review. `python tools/verify_repository.py` passed with 238 asset GUIDs, valid JSON, editor/serialization settings and generated-file exclusions. `python tools/nasa/horizons_snapshot.py` passed all 32 samples, source/CSV hashes, vector norms, light-time checks and exact offline regeneration. These checks are separate from Unity compile/playback evidence, which remains pending.

Root must verify exact-revision compilation, prefab generation, dialogue/subtitle start/replacement/end, repeated cue rejection, pause/resume during a line and crossfade, rapid three-mood changes, silence fade, music attack/release ducking, disabled/re-enabled cleanup, missing/async-loading clip rejection, and clean-voice selection. Confirm one gameplay listener and both overall mix/audible loop seams in actual playback. No claim of these passes is made here.

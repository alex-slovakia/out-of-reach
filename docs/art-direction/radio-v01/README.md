# STORY-RADIO-01 — handheld radio (vysielačka), first person

**Task:** the lead's STORY-RADIO-01 for Alex's story (#7 6896895693), R12 on the live board, plus the voiced
Day-14 announcement (VO23, part of R42).
**Branch:** `feature/apx-radio-2315`, on `codex/nasa-2330-staging`. New files only, plus one Bootstrap install commit.

## What it is

- **Prop:** `Visuals/Props/RadioV01/**`, an original handheld radio built from code by `RadioV01Authoring`.
  - Graphite body with rubber side grips, dark screen, speaker grille and an amber stripe.
  - Amber PTT on the left side, amber LED, knob, rubber antenna.
  - 23 parts, 4,504 triangles, URP Lit. No downloads, no logos.
- **Presenter:** `Scripts/Presentation/Radio/Runtime/HandheldRadioPresenter.cs`, its own asmdef
  `OutOfReach.Presentation.Radio`. Presentation only.
  - **First-person scene:** the radio rises into the lower right of the player's view and the PTT presses on
    the player's lines. The LED is amber for transmit and pulses cyan for receive.
  - **Sound:** original procedural crackle bed, key-up blip, squelch tail and incoming-call chirp.
  - **Subtitles:** every line goes through the existing flight-board subtitle path, with speaker labels. It
    never talks over the ship computer or Earth dialogue; it waits for them.
  - **Pause, skip, reduced motion:** pause freezes it (timeline and audio). Enter skips it, lowering from
    wherever the radio is. Reduced motion (the Settings option) removes the sway and shortens raise and lower.
  - **Control:** it never takes control from the player.
  - **When something takes the view:** if a cutscene or departure takes the view, the radio lowers out of the way.
  - **Self-binding:** it reads `MissionRuntime` (phase, communications, chapter dispatch), so no composition or
    Core change is needed.
- **Beats** (`RadioBeats.cs`); each plays once per session. They bind only to EXISTING events: no schedule or
  Core change.
  1. **Boarding Aurora** (default `AuroraArrival`): after the transfer, once the player floats aboard with
     control. Incoming chirp → "Aurora Seven, Mission Control. We show you aboard. Radio check, how do you read?"
     → the player keys: "Loud and clear, Control. Aboard and floating. Starting cockpit checks." About 12 s. It is
     inserted; the opening and the liftoff cutscene are untouched.
     - Alternative `PadHatch`: a pad radio check at the transport hatch before seat E. Set by the one field
       `boardingPlacement`.
  2. **Day 1, distress call:** on Core's `CommunicationState.AwaitingReply` edge (after the antenna repair), the
     player keys a mayday.
  3. **Day 1, Earth reply:** on `ReplyReceived`, which Core sends only after the real light time. "...Expect rescue
     on day fourteen..."
  4. **Day 14 call:** on the existing Core dispatch `dispatch.rescue-1-failed`. Chirp → "Aurora, Earth. Priority
     traffic. Go to the console, Aurora."
  5. **Day 14 voiced announcement** (Alex 6896919554: actual audible voice, synced subtitles, once).
     - **Trigger:** the next use of the cockpit console after the Day-14 call.
     - **Voice:** the day-neutral cut of Codex's approved recording, through a radio band (300–3400 Hz, light
       grit). See "VO23 day-neutral cut" below.
     - **Subtitles:** exactly the transcript of that cut, in readable chunks timed to the 18.0 s clip.
     - **If the console view takes the camera:** the voice and subtitles continue; the radio model hides.

## VO23 day-neutral cut (pending Alex's approval)

- **Why:** the approved take says "New rendezvous: mission day thirty-one." while Core has the second rescue on
  day 30. No schedule or Core change is authorized, so the game must not speak that date.
- **What:** `Scripts/Presentation/Radio/Voices/vo23_mc_rescue_lost_dayneutral.wav` is the approved take with that
  ONE sentence removed. No new generated audio. The source WAV and `approved-voices.json` are unchanged.
- **How:** ffmpeg `atrim` + a 40 ms crossfade, both cut points in the silence between sentences. 24 kHz mono
  16-bit kept; 21.08 s → 17.98 s.
- **Checked sample by sample:** outside the crossfade the clip is bit-identical to the source. The crossfade holds
  room tone only (peak −58.9 dBFS). The pause across the cut is 1.00 s; the source's own sentence pauses are
  0.85–1.47 s.
- **Full record:** `Voices/PROVENANCE.md` (hashes, cut times, exact command, measurements).
- **If Alex rejects the cut:** delete the WAV (and its `.meta`) and re-run the installer, or clear `voiceClips`
  on "Handheld radio V01". The announcement then plays as subtitles with the radio sound only. The subtitle
  text stays day-neutral either way.

## Integrator

Either cherry-pick all four commits (the last one is the Bootstrap install), or cherry-pick the three code
commits and run the installer yourself:

```
Unity.exe -batchmode -quit -projectPath <checkout> -executeMethod OutOfReach.Presentation.Radio.Editor.RadioSceneInstaller.InstallIntoBootstrap -logFile <log>
```

- **What it does:** adds ONE root object, `Handheld radio V01`, with the presenter, the prefab, the transport's
  `BoardingThreshold` anchor, the Day-14 dispatch mapping and the voice clip, then saves Bootstrap.
- **Safe:** idempotent; it throws if the collider count would change.
- **No other edits:** no composition, Core, collider, audio-manifest or asmdef change elsewhere.
- **Expected log line:** `STORY_RADIO_01_INSTALLED ... voice=vo23_mc_rescue_lost_dayneutral bound (18,0 s) colliders=354 (unchanged)`.
- **Scene diff:** 7181 → 7184 serialized objects (GameObject + Transform + presenter), none removed. Compared
  object by object, every existing object is byte-identical except `SceneRoots` (+1 root) and two MonoBehaviours
  whose empty `m_Name` gained Unity's trailing space. The rest of the textual diff is Unity re-sorting existing
  objects on save. The tether and the rescue-ending objects are untouched.
- **Without batch mode:** menu **Tools > OUT OF REACH > Radio V01 - add to open scene (no save)**.

## Verified (own worktree, Unity 6000.3.28f1, batch mode, RTX 4070 Direct3D 12)

- **EditMode `OutOfReach.Tests.Radio`: 13/13.** Line order, PTT and LED, pause, skip, reduced motion, incoming
  chirp, abort, boarding length, the day-neutral announcement (names no day, equals the transcript file),
  subtitle chunks, stale boarding check, announcement window.
- **Native run through the REAL route on the saved Bootstrap with the radio installed** (`native-route-*.txt`,
  `captures/route-*.jpg`). Real virtual keyboard and mouse: startup cards → Practice → walk to the transport →
  seat E → departure → transfer to Aurora → cockpit check → impact → repairs → EVA → transmission → routine →
  Day 14. The driver is a copy of the QA-E2E driver with radio hooks (temporary, never committed).
  - **Boarding Aurora:** the beat starts by itself after the transfer, once the arrival dialogue has finished.
    The player keeps control. The real Escape key freezes it; it resumes, ends by itself, the radio hides and the
    subtitle clears.
  - **Skip:** the real Enter key lowers the radio and ends a beat in about 0.2 s (distress call).
  - **Day 14:** Core delivers `dispatch.rescue-1-failed` on mission day 14; the binding queues the call; after the
    chapter cards are answered the call plays. The first E on the cockpit console starts the voiced announcement
    at once (the console itself answers "Contact with Earth is already restored").
  - **Announcement:** the derived clip plays on the radio voice source for 18.33 s; the HUD shows three chunks
    that, joined, equal the transcript; no date. Escape pauses the voice and the timeline; resume continues;
    a second console use replays nothing.
  - **Reduced motion:** switched on for Day 14; the call and the announcement report no sway.
- See `native-route-checks.txt` for every radio check, `native-route-trace.txt` for what the radio did frame by
  frame, and `native-route-notes.txt` for the run facts and every disclosed shortcut.
- **Earlier in-memory QA** (old base, original 21.1 s clip, pad-hatch trigger): `native-checks.txt`,
  `native-notes.txt`, driver `RadioQa.cs.txt`, captures `02`–`09` and `zoom-radio-ptt`.

## What the real route taught (fixed in `STORY-RADIO-01d` and `01e`)

- **A boarding check that misses its moment is dropped.** The Aurora check waits about 10 s for the arrival
  dialogue. A player who reaches the cockpit check first would have heard "we show you aboard" after the impact.
- **Chapter cards come in a row.** The Day-14 call started in the short gap of control between two cards and
  was cut after 1.7 s. A beat now waits while a chapter decision is pending.
- **No slipping between two ship-computer lines.** Other dialogue must have been quiet for 1.5 s.
- **No old news.** A beat still queued when the routine (R) starts is dropped, and the announcement plays only
  within one simulated day of the message.

## Honest limits

- **Earth's reply beat is easy to miss.** It waits behind the CONTACT card. If the player presses R right after
  closing the card, the routine starts and the beat is dropped (by design: no old news days later).
- **The Day-14 call can be missed the same way.** The announcement then still plays on a console use within one
  simulated day, but nothing else sends the player to the console.
- **Navigation shortcuts of the QA driver:** legs that real float pushes did not reach in their budget fall back
  to a QA teleport next to the target (listed in `native-route-notes.txt`). Every E, Escape and Enter is real
  input.
- **Reduced motion** was switched through `I01MissionComposition.SetReducedEffects`, the method the Settings
  toggle calls, not by clicking the toggle.
- **Voice:** only the Day-14 announcement has a recorded voice. The other radio lines are subtitles plus radio
  sound. `vo24a`/`vo24b` belong to the rescue ending (not this lane).
- **No hand:** no glove holds the radio. It is framed so the hand stays below the screen edge. The radio body
  overlaps the right end of long subtitles; the text stays readable.

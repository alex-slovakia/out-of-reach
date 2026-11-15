# STORY-RADIO-01 — handheld radio (vysielačka), first person

**Task:** the lead's STORY-RADIO-01 for Alex's story (#7 6896895693). Claim 6896965174.
**Branch:** `feature/story-radio-01`. Only NEW files.
**Base:** `claude/integration@d901fc5`. The commit adds new paths only, so it cherry-picks cleanly onto
`codex/nasa-2330-candidate@ae6380f`: none of its paths exist there, and every type it uses exists there.

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
    wherever the radio is. Reduced motion (the pause-menu option) removes the sway and shortens raise and lower.
  - **When something takes the view:** if a cutscene or departure takes the view, the radio lowers out of the way.
  - **Self-binding:** it reads `MissionRuntime` (phase, communications, chapter dispatch), so no composition or
    Core change is needed.
- **Beats** (`RadioBeats.cs`); each plays once per session:
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
     - **Voice:** Codex's approved recording `Audio/Voices/Story02/vo23_mc_rescue_lost.wav` through a radio band
       (300–3400 Hz, light grit). Its exact transcript is shown in two readable chunks timed to the 21.1 s
       recording.
     - **If the console view takes the camera:** the voice and subtitles continue; the radio model hides.

## Integrator (one step)

```
Unity.exe -batchmode -quit -projectPath <checkout> -executeMethod OutOfReach.Presentation.Radio.Editor.RadioSceneInstaller.InstallIntoBootstrap -logFile <log>
```

- **What it does:** adds ONE root object, `Handheld radio V01`, with the presenter, the prefab, the transport's
  `BoardingThreshold` anchor and the Day-14 dispatch mapping, then saves Bootstrap.
- **Safe:** idempotent; it throws if the collider count would change.
- **No other edits:** no composition, Core, collider, audio-manifest or asmdef change elsewhere.
- **Without batch mode:** menu **Tools > OUT OF REACH > Radio V01 - add to open scene (no save)**.
- **Voice:** the installer binds `vo23` when `Audio/Voices/Story02/vo23_mc_rescue_lost.wav` exists in the checkout.
  It is in `codex/nasa-2330-candidate`. Its log says `voice=vo23_mc_rescue_lost bound (21.1 s)`.

## Verified (own worktree, Unity 6000.3.28f1, batch mode, real GPU)

- **EditMode `OutOfReach.Tests.Radio`: 11/11.** They cover:
  - line order;
  - PTT and LED for transmit and receive;
  - pause freezes everything;
  - skip lowers from the current height and speaks nothing more;
  - reduced motion;
  - incoming chirp before the raise;
  - abort;
  - both boarding variants stay in 8–12.5 s;
  - the Day-14 announcement uses cue `vo23` and its length;
  - long-line subtitle chunks.
- **Native, real saved Bootstrap with the installer applied in memory: 16/16 PASS** (`native-checks.txt`).
  - The run uses the real Planning bind, real Practice through the UI and the player's own camera with the HUD
    composited.
  - **Pad trigger:** the pad-hatch proximity trigger fires at the real `BoardingThreshold`.
  - **Transmit:** the transmit line keys the PTT with the amber LED.
  - **Pause:** the real runtime pause freezes the beat (5.60 s → 5.60 s).
  - **After the beat:** the radio is hidden.
  - **Reduced motion and skip:** reduced motion gives no sway; skip ends the Day-14 call.
  - **No repeats:** each beat plays once.
  - **Voice:** `vo23` is bound and plays on the radio voice source. A real pause pauses it, and it stops with the
    beat.
- **Captures** (`captures/`): the pad call with subtitle, the player transmitting (PTT, amber LED), paused, the
  Day-14 call, the distress call, and a close-up of the radio.

## Honest limits

- **Arrival trigger:** the default `AuroraArrival` trigger was not driven natively through the full route
  (departure → transfer). Beats 2–4 were started directly in QA. Their Core edges are the existing public states.
- **Voice:** only `vo23` is wired. The other radio lines are subtitles plus radio sound. `vo24a`/`vo24b` (rescue
  approach and docked) belong to the Day-31 sequence; its owner can call `TryPlay` or play them directly.
- **Console trigger:** the voiced announcement's console trigger (InteractionResolver.Interacted on
  `cockpit-console`) is code-reviewed. QA started the beat directly.
- **No hand:** no glove holds the radio. It is framed so the hand stays below the screen edge.
- **The P0 startup error on `d901fc5`** ("interior contact rails could not bind") appears in this clone at session
  start. It is not caused by the radio; the QA still passes.

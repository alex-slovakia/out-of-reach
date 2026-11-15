# CHAPTER-UI-01 — installer for the Claude integration assembler

Owner of these files: the `chapter-ui` session. Owner of the scene and `Scripts/Composition/**`: the one Claude
integration assembler (`claude/integration`). This folder is ignored by Unity (`~`).

The chapter UI reads and commands the approved day 1-30 loop through `IMissionChapterFlow` only (plus the existing
`IGameState`, `ISimulationClock`, `IResourceSystem`, `ISurvivalRecovery`, `ISurvivalExtension` contracts). It keeps no
clock, ledger, day counter or story state of its own.

## Steps (in this order)

1. **Merge** `feature/chapter-ui`. It contains the Core loop merge (`feature/chapter-loop-core@c85bd33`), the new
   `Assets/_OutOfReach/UI/Chapter/**` and a minimal edit of `UI/Runtime/SurvivalChapterPresenter.cs` (claimed on #7):
   needs/rest UI also in the chapter loop's cabin phases before contact, and `ConfigureRestPresentationOwner(...)` so the
   routine montage owns rest presentation (no rest overlay, eyelids or `DAY N` wake title flashing every routine night).
2. **Apply the composition patch** (also adds `OutOfReach.UI.Chapter` to `OutOfReach.Composition.asmdef`):
   ```bash
   git apply --3way Assets/_OutOfReach/UI/Chapter/Integration~/I01MissionComposition.chapter-ui.patch
   ```
   Hunks: one `using`, two serialized fields (`chapterUi`, `chapterStyle`), `ConfigureChapterUi(...)`, one line in
   `CanUsePlanningControls` (`!(chapterUi && chapterUi.BlocksPlayerControl)` frees the cursor while a decision card is
   open), the `chapterUi.Configure(...)` + `survivalUi.ConfigureRestPresentationOwner(...)` block right after the
   survival UI block in `BindSession`, and `chapterUi.ClearBinding()` in `UnbindSession`. No music/countdown line is
   touched (those belong to `audio-opening`).
3. **Apply the habitat patch** — required for the loop to be playable in the real scene:
   ```bash
   git apply --3way Assets/_OutOfReach/UI/Chapter/Integration~/I06HabitatBindings.chapter-mode.patch
   ```
   Without it the galley water, food locker and berth are only exposed in `GamePhase.Survival`, so in the chapter loop
   (days 1-9 are Emergency/Repair/Transmission) Core's day-1 prerequisites "eat the first stored ration" and "complete
   one rest" can never be met and the routine never starts. The patch exposes them in those cabin phases only when
   `runtime.ChapterFlow != null` and the needs are active; EVA stays excluded; nothing changes without the chapter loop.
4. **Install into Bootstrap** (Unity batch, or the menu `OUT OF REACH/Chapter UI/Install into the open scene`):
   ```
   Unity.exe -batchmode -projectPath <clone> -executeMethod OutOfReach.Presentation.UI.Chapter.Editor.ChapterUiAuthoring.InstallIntoBootstrapAndSave -quit
   ```
   Idempotent. It adds `I01Composition/ChapterUi` with `MissionChapterPresenter` (document = the flight-board UIDocument
   that the survival UI uses, style = `UI/Chapter/Styles/ChapterUi.uss`), assigns the composition's `chapterUi` /
   `chapterStyle`, and sets `MissionRuntime.chapterLoop = true` (`survivalExtension` is already on in Bootstrap). The log
   prints a report; a WARNING line means a step above is missing. Restore Unity side effects before committing
   (`Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/SceneTemplateSettings.json`, PolyHaven `.mat`).

## What the player gets

| When (Core state) | UI |
|---|---|
| Survival active after the impact (`Chapter == Accident`) | Beat card **MISSION DAYS 1–2 · THE ACCIDENT** |
| No routine running | Bottom-right strip: next beat and its day, `R Continue the routine`, or Core's own refusal text (`CheckContinueRoutine`) |
| `IsRoutineRunning` | Full-screen montage: running mission day and clock, `DAY a → DAY b · n CALENDAR DAYS OF ORDINARY LIFE`, day ruler, Core's ration/water/night tally, live reserves with deltas, crew log (meals, lights out, wake + systems check), `R Stop the routine` (`TryCancelRoutine`) |
| Routine stopped | Report card with Core's actual stop time (`RoutineStoppedAtSimulationSeconds`), reason (`LastRoutineStopText`), tone by `RoutineStopReason`; when it reached a beat, the beat card carries that summary instead |
| `Chapter` changes | Beat cards: CONSEQUENCES (5), CONTACT (9), THE MESSAGE (14), LAST RESERVES (20), RENDEZVOUS (30) |
| `PendingDecision` | Modal decision card; options show Core's `CheckChoose` text and issue `TryChoose`; the physical options (salvage at the payload rack, build the grow module) close the card and point at the existing interaction |
| `Rescue → FirstRescuePlanned` | Message "Help is coming" with the scheduled report day |
| `LatestDispatch` (new `Id`) | Message with sent/received times and light time, `1 Acknowledge` (`TryChoose(AcknowledgeRescueFailure)`) |
| `Rescue → InTransitCraftCommitted` | Message "A craft already in transit" with the docking day |
| `RecoveryStage.BeaconRequested` / `Delayed` | Beacon card with Core's window and `CheckPowerBeacon` text / missed-window card |

Keys: `R` continue/stop the routine (reopens a deferred decision), `1`/`2` options, `Enter` confirm or dismiss. Mouse
works on every button (the montage and the decision cards free the cursor). Pause, menus, cinematics and the Earth
terminal hide everything (the composition's existing `CanPresentSurvivalUi` gate) and freeze card timers; restart
rebinds and clears; a decided mission clears every queued card.

# MEAL-PRES-01 — the physical galley meal (presentation only)

Approved chapter loop, "Physical food and sleep" (`docs/APPROVED-CHAPTER-LOOP-2026-11-15.md`): take a real stored food
pouch, prepare it by type, open it and eat; Core consumes one authoritative ration.

## What happens on E

E on the existing `survival-ration` target (the galley ration drawer) starts a meal instead of the instant command.

| Stage | Real seconds | What the player sees and hears |
|---|---|---|
| Take | 1.4 | The right WORK hand reaches into the drawer, clasps a sealed pouch (foil rustle) and brings it up into the middle of the view |
| Prepare | 0.8–2.8 | **Warmed** (MEAL 04 vegetable stew, thermostabilised): held towards the food warmer and back · **Rehydrated** (MEAL 11 rice and beans): towards the water dispenser needle, the dry pouch fills and is kneaded · **Ready to eat** (SNACK 03 fruit and nut mix): a squeeze and a look at the label. At the end the left hand comes up beside the top corner |
| Open | 1.0 (1.7 on a repeated menu) | **Both hands:** the left thumb and index finger pinch the top corner and tear the top strip off across the pouch (tear sound); the strip peels to the fingers and the left hand takes it out of the view. From the fourth meal a short, restrained look at the familiar label first |
| Eat | 2.8 | **First bite = the one real `TryConsumeRation`**, then the opened pouch is tipped to the mouth below the eye for three squeezed sips (squeeze and swallow sounds, a small head nod); it flattens as it empties |
| Stow | 1.0 | The hand lowers the flat pouch into the wet-trash bag (foil rustle) and lets go |

- **Preparation rotation:** the first packaged meal is warmed, then rehydrated, then ready to eat, repeating. Each
  preparation shows its own label on the same pouch mesh (`GC_Labels`, `GC_Labels_Rehydratable`, `GC_Labels_ReadyToEat`).
- **Station positions** (needle, warmer, wet trash) are derived from `WaterInteractionSurface` with the habitat kit's own
  offsets (`MealPresenter.*InWaterFrame`). The movement lock freezes the view where E was pressed, and the warmer and
  needle sit below it and beyond arm's reach from the drawer. The pouch is therefore presented **towards** the real
  station: on its line of sight when the station is in view, otherwise at the edge of the view in its direction
  (`MealChoreography.TowardsInView`). The pouch never leaves the hand.
- **Too far:** beyond 1.05 m from the drawer the target answers "Too far from the galley food drawer. Move closer and
  press E to eat." and nothing starts.

## R35 — the readable first-person meal (post-jam)

Playtest: "the eating animation is weak". On the post-jam build the pouch sat low and right, behind the chapter panel
and the subtitles, opened with an invisible swap and was "eaten" as two small bobs at the bottom edge, in silence.
R35 re-stages the same meal inside the **unchanged** stage lengths (`MealSequence.cs` is not touched):

- **In the middle of the view.** The hold, open and eat keys moved out of the HUD's corners (`MealChoreography.*Key`).
- **Both hands tear it open.** `MealOffHand` leases the visible WORK left arm like the right one (two-bone reach,
  rotations only, given back on release and on `PoseWillReset`) and builds a thumb-and-index pinch from the rig's own
  finger joints. `MealTearStrip` is the pouch's own top band as one foil ribbon (procedural mesh, the pouch's foil
  material and texture coordinates, no new asset): it stands on the cut where it still holds, peels to the fingers
  where it has torn (`MealStripShape`), and is carried out of the view in the left hand. Sealed → Opened swaps when the
  tear starts, hidden by the strip. Without a visible left arm the strip never peels or floats: it goes when the tear ends.
- **Three sips at the mouth.** The opened pouch tips its opening to the assumed mouth below the eye, the far end rises,
  the fingers press on each squeeze and the pouch flattens to 58 % as it empties.
- **A small head nod.** `MealViewNod` pitches the camera's own transform by at most 2° down and 1.4° back per sip, on
  top of whatever pose it has, and gives the rest pose back exactly. **Reduced motion: no nod at all.** The meal follows
  the mission's setting through `FirstPersonMotionPresenter.ReducedMotion` (the composition already feeds it from
  "Reduced effects"); `MealPresenter.ReducedMotionQuery` overrides it. A look pivot (no Camera component) is never nodded.
- **Foley.** `MealFoley` plays four original clips through the mission's `MissionAudioPresenter.PlaySfx` (its SFX level,
  pause and mute apply): foil in the hand, the tear, the squeeze, the swallow. They are synthesised by
  `Audio/Original/MealFoley/generate_meal_foley.py` (Python standard library, seeded; provenance in
  `audio-provenance.json` beside it) and loaded by name from `Resources/MealFoley`, so the saved scene needs no new
  reference. **No scene, composition or installer change is needed for R35.**
- **Beats.** `MealBeats.For(kind, repeatedMenu)` is the one schedule for motion and sound, from the choreography's own
  constants. `MealPresenter.BeatPlayed` reports each beat (and whether its sound played).

| Beat | When (Warmed, s from E) | Sound |
|---|---|---|
| Grasp | 0.50 | `meal-pouch-grab` |
| Knead (ready to eat, rehydrated only) | in Prepare | `meal-pouch-squeeze` |
| Pinch | 4.26 | — |
| Tear | 4.36 (the strip is off at 4.70; first bite at 5.00) | `meal-pouch-tear` |
| Sip 1 / 2 / 3 | 5.58 / 6.25 / 6.92 | `meal-pouch-squeeze` |
| Swallow 1 / 2 / 3 | 5.96 / 6.63 / 7.30 | `meal-swallow` |
| Stow | 8.50 | `meal-pouch-grab` |

A sip is only ever heard after the real first bite (the schedule orders it so, and the presenter checks `HasEaten`).
Verification, frames and the native QA tool: `docs/verification/meal-r35/`.

## Authority and safety (tested)

- **One ration per meal.** `MealSequence` calls the captured real interactable exactly once, at the first bite, after
  a fresh `CanInteract` and policy check. Take, prepare and open grant nothing. Repeated E is refused ("Meal in
  progress"). Oversized, replayed or re-entrant frames and stage events cannot call it again: there is no other path.
  The R35 beats, hands, strip, nod and sounds only read the sequence's timer.
- **Core decides.** If Core refuses at the first bite (no food, rest, cinematic), the meal ends with Core's own reason and
  nothing spent. No water or power cost is added: Core defines none for preparation.
- **Pause** freezes the meal (and its beats). **Restart / new session, phase change, cinematic, hazard** (a ship system
  losing condition), **suit on or outside, mission end, focus loss or a revoked root policy** end it: before the first
  bite nothing is spent, after it the ration stays eaten (no refund, no second ration). The pouch and the strip are
  hidden, every bone of both arms is restored and the view is given back.
- **Hands:** `MealHandClasp` leases the visible WORK right arm only (never EVA): two-bone reach, half the wrist roll into
  the forearm, and the measured edge clasp (`WORK_Clasp_Meal_R`). `MealOffHand` leases the left arm the same way for the
  tear. Both write rotations at execution order 260 (after the body 200 and contacts 250), restore every touched bone
  when they let go and on `PoseWillReset`. No movement, input, physics or domain writes.

## Install (integration assembler, `claude/integration`)

1. Merge `feature/meal-pres` (new files only).
2. Apply `Integration~/composition-meal.patch` (I01MissionComposition + one asmdef reference):
   `git apply --3way Assets/_OutOfReach/Scripts/Presentation/Meal/Integration~/composition-meal.patch` (generated against
   `claude/integration@4853b05`; if its context has drifted, `Integration~/apply_composition_meal.py` makes the same edit by exact anchors).
   It adds the `meal` field and `ConfigureMeal`, binds the meal after the habitat and the body in `BindSession`, clears it
   before the habitat in `UnbindSession`, adds `meal.BlocksMovement` to the player's control gate, keeps the opening-menu
   sway and the Earth viewer off during a meal, releases rail grips when a meal starts and routes the stage lines and
   results to the existing interaction prompt.
3. With Bootstrap open (not playing): **OUT OF REACH → Integration → Install meal presentation (MEAL-PRES-01)**, or batch
   `-executeMethod OutOfReach.Presentation.Meal.Editor.MealPresentationInstaller.InstallBootstrapBatch`. It adds
   `Meal presentation (MEAL-PRES-01)` under the habitat root, wires the ration target, the two surfaces, the pouch and the
   clasp, assigns the composition's `meal` field and saves the scene. Idempotent.
4. Tests: `OutOfReach.Tests.Meal.EditMode` and `OutOfReach.Tests.Meal.PlayMode` (own assemblies under `Tests/Meal`).

(Already done on `main` and `post-jam-polish`. R35 adds one asmdef reference, `OutOfReach.Audio`, and nothing to install.)

## Limits

- One meal-pouch mesh for all three preparations. Only the label text differs: the two extra atlases are redrawn by the
  kit's own label generator with the same layout (`Visuals/Props/GalleyConsumablesV01/Source~/gc_label_variants.py`).
  No new food model was made. The opened pouch shows the kit's stew surface for every menu.
- Legacy C06 activates rations only after contact; in chapter mode (`feature/chapter-loop-core`) the same Core command
  is active from day 1 and this presentation needs no change, but the habitat must expose the ration target then.
- The camera stays where the player looked when E was pressed (the movement lock freezes the view; R35 adds only the
  small nod): the preparation is shown towards a station that is usually below the view, not touching it; the prompt
  line names the station.
- R35: the tear strip is the pouch's top band as a smooth foil ribbon (the pouch's crinkle is in its texture, not in the
  ribbon's shape). At the lips the pouch is close to the eye and large, and the eye sees its back, not its label. The
  left-hand pinch is built from the joints, not measured like the right clasp. The sounds are synthesised and were not
  checked by ear in the authoring session. Before power is restored the galley is lit only by the red emergency light:
  the meal reads, but dimly (the scene's lighting is not changed here).

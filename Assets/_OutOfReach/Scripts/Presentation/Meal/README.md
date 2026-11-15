# MEAL-PRES-01 — the physical galley meal (presentation only)

Approved chapter loop, "Physical food and sleep" (`docs/APPROVED-CHAPTER-LOOP-2026-11-15.md`): take a real stored food
pouch, prepare it by type, open it and eat; Core consumes one authoritative ration.

## What happens on E

E on the existing `survival-ration` target (the galley ration drawer) starts a meal instead of the instant command.

| Stage | Real seconds | What the player sees |
|---|---|---|
| Take | 1.4 | The right WORK hand reaches into the drawer, clasps a sealed pouch and brings it low into view |
| Prepare | 0.8–2.8 | **Warmed** (MEAL 04 vegetable stew, thermostabilised): held towards the food warmer and back · **Rehydrated** (MEAL 11 rice and beans): towards the water dispenser needle, the dry pouch fills and is kneaded · **Ready to eat** (SNACK 03 fruit and nut mix): a squeeze and a look at the label |
| Open | 1.0 (1.7 on a repeated menu) | The pouch is cut open (Sealed → Opened in place); from the fourth meal a short, restrained look at the familiar label first |
| Eat | 2.8 | **First bite = the one real `TryConsumeRation`**, then two bites near the mouth |
| Stow | 1.0 | Into the wet-trash bag; the hand lets go |

- **Preparation rotation:** the first packaged meal is warmed, then rehydrated, then ready to eat, repeating. Each
  preparation shows its own label on the same pouch mesh (`GC_Labels`, `GC_Labels_Rehydratable`, `GC_Labels_ReadyToEat`).
- **Station positions** (needle, warmer, wet trash) are derived from `WaterInteractionSurface` with the habitat kit's own
  offsets (`MealPresenter.*InWaterFrame`). The movement lock freezes the view where E was pressed, and the warmer and
  needle sit below it and beyond arm's reach from the drawer. The pouch is therefore presented **towards** the real
  station: on its line of sight when the station is in view, otherwise at the edge of the view in its direction
  (`MealChoreography.TowardsInView`). The pouch never leaves the hand.
- **Too far:** beyond 1.05 m from the drawer the target answers "Too far from the galley food drawer. Move closer and
  press E to eat." and nothing starts.

## Authority and safety (tested)

- **One ration per meal.** `MealSequence` calls the captured real interactable exactly once, at the first bite, after
  a fresh `CanInteract` and policy check. Take, prepare and open grant nothing. Repeated E is refused ("Meal in
  progress"). Oversized, replayed or re-entrant frames and stage events cannot call it again: there is no other path.
- **Core decides.** If Core refuses at the first bite (no food, rest, cinematic), the meal ends with Core's own reason and
  nothing spent. No water or power cost is added: Core defines none for preparation.
- **Pause** freezes the meal. **Restart / new session, phase change, cinematic, hazard** (a ship system losing condition),
  **suit on or outside, mission end, focus loss or a revoked root policy** end it: before the first bite nothing is
  spent, after it the ration stays eaten (no refund, no second ration). The pouch is hidden and every bone is restored.
- **Hand:** `MealHandClasp` leases the visible WORK right arm only (never EVA): two-bone reach, half the wrist roll into
  the forearm, and the measured edge clasp (`WORK_Clasp_Meal_R`). It writes rotations at execution order 260 (after the
  body 200 and contacts 250), restores every touched bone when the meal ends and on `PoseWillReset`. No movement,
  input, physics or domain writes.

## Install (integration assembler, `claude/integration`)

1. Merge `feature/meal-pres` (new files only).
2. Apply `Integration~/composition-meal.patch` (I01MissionComposition + one asmdef reference):
   `git apply --3way Assets/_OutOfReach/Scripts/Presentation/Meal/Integration~/composition-meal.patch`.
   It adds the `meal` field and `ConfigureMeal`, binds the meal after the habitat and the body in `BindSession`, clears it
   before the habitat in `UnbindSession`, adds `meal.BlocksMovement` to the player's control gate, keeps the opening-menu
   sway and the Earth viewer off during a meal, releases rail grips when a meal starts and routes the stage lines and
   results to the existing interaction prompt.
3. With Bootstrap open (not playing): **OUT OF REACH → Integration → Install meal presentation (MEAL-PRES-01)**, or batch
   `-executeMethod OutOfReach.Presentation.Meal.Editor.MealPresentationInstaller.InstallBootstrapBatch`. It adds
   `Meal presentation (MEAL-PRES-01)` under the habitat root, wires the ration target, the two surfaces, the pouch and the
   clasp, assigns the composition's `meal` field and saves the scene. Idempotent.
4. Tests: `OutOfReach.Tests.Meal.EditMode` and `OutOfReach.Tests.Meal.PlayMode` (own assemblies under `Tests/Meal`).

## Limits

- One meal-pouch mesh for all three preparations. Only the label text differs: the two extra atlases are redrawn by the
  kit's own label generator with the same layout (`Visuals/Props/GalleyConsumablesV01/Source~/gc_label_variants.py`).
  No new food model was made. The opened pouch shows the kit's stew surface for every menu.
- Legacy C06 activates rations only after contact; in chapter mode (`feature/chapter-loop-core`) the same Core command
  is active from day 1 and this presentation needs no change, but the habitat must expose the ration target then.
- The camera stays where the player looked when E was pressed (the movement lock freezes the view): the preparation is
  shown towards a station that is usually below the view, not touching it; the prompt line names the station.

# R35 — the readable first-person meal (CLAUDE-MAX meal lane, post-jam)

Branch `feature/claude-max-meal`, base `post-jam-polish@5ff300f`. Playtest: "the eating animation is weak" (#11 R35:
grasp and open the food pouch with matched foley, reusing the existing meal presentation).

## What was wrong (seen in the BEFORE frames)

On the post-jam build the pouch was held low and to the right. That is where the chapter panel, the resource bars and the
subtitles are, so most of the meal was behind the HUD. The pouch "opened" by an instant swap that looked the same,
"eating" was two small bobs at the bottom edge, one hand did everything, and there was no sound.

## What changed

Presentation only, inside the unchanged stage lengths (`MealSequence.cs` is not touched; the one real ration command
still runs once, at the first bite). Details: `Assets/_OutOfReach/Scripts/Presentation/Meal/README.md`.

- The pouch is held, opened and eaten in the middle of the view.
- Both hands: the left thumb and index finger pinch the top corner and tear the top strip off; the strip is the pouch's
  own foil, peels to the fingers and leaves the view in the left hand.
- The opened pouch is tipped to the mouth below the eye three times; the fingers press, the pouch flattens.
- A head nod of at most 2° per sip; none with the game's Reduced effects setting.
- Four original synthesised sounds: foil in the hand, tear, squeeze, swallow.
- No scene, composition or installer change: cherry-pick and play.

## Frames (own Unity 6000.3.28f1 Editor, the actual saved Bootstrap, 1280×720)

| File | What |
|---|---|
| `frames/before-after-open-hud.png` | The first meal as the player meets it (emergency light, HUD on): BEFORE above, AFTER below, same meal times |
| `frames/before-after-eat-hud.png` | The same for the eating |
| `frames/before-eat-hud-emergency-light.png`, `frames/after-sip-hud-emergency-light.png` | One eat moment, before and after, full size |
| `frames/after-open-clean.png` | AFTER, cabin light on, HUD off, middle of the view: the left hand arrives, pinches, tears, takes the strip away |
| `frames/after-eat-clean.png` | AFTER: raise, three sips at the mouth, lower |
| `frames/after-take-prepare-clean.png` | AFTER: reach into the drawer, clasp, carry |
| `frames/after-tear-hud.png`, `frames/after-sip-hud.png` | AFTER, the player's view with the HUD at the tear and at a sip |

The BEFORE frames were taken on `post-jam-polish@5ff300f` itself, with the same tool, before any change.

## Tool

`native-qa/MealBeatsNativeQa.cs` (+ its asmdef; `MealBeatsNativeQaAfter.cs` only with the reworked presenter). Copy to
`Assets/_MealQa/Editor/`, set `MEAL_QA_OUT`, run `-batchmode -executeMethod MealBeatsNativeQa.Run` without `-nographics`,
then delete `Assets/_MealQa`. It opens the saved Bootstrap, chooses Practice through the real UI and starts every meal
with a real E on the real `survival-ration` target. Logged shortcuts: a teleport to the cockpit console (real E there),
hold-Space skip of the departure, the domain cockpit check, a teleport to the galley aisle, simulated waits until a ration
is due, domain repairs before meal 2 (cabin light on). Meals 1 and 3 are stepped with the scripted presentation clock
(`MealPresenter.Advance`) so each frame is at an exact meal time; meals 2 and 4 run on the game's own real-time update.

## Results of the final revision

All in an own Unity 6000.3.28f1 Editor on this branch's worktree (no other Editor running), after the last code change.

| Check | Result |
|---|---|
| Native run, 4 meals (`evidence/after-checks.txt`, `after-observations.txt`) | PASS, 47 of 47 checks, 144 frames |
| EditMode `OutOfReach.Tests.Meal.EditMode` | 23 of 23 passed (17 existing, 6 new in `MealBeatTests`) |
| PlayMode `OutOfReach.Tests.Meal.PlayMode` | 9 of 9 passed (7 existing, 2 new) |
| One ration per meal, at the first bite; take, prepare and open grant nothing | checked in all 4 meals |
| Real time: meal 2 (rehydrated) | 9.03 s for the 9.00 s timeline; 11 beats in order, every audible one played |
| Left hand at the pinch and through the tear | reach shortfall 0 mm, thumb and index pads meet (gap 0 mm), strip in the fingers (0 mm) |
| Right hand while opening and eating | reach shortfall 0 mm |
| Head nod, meal 2 | largest deviation 2.00°, back at the rest pose within 0.000° |
| The game's own Reduced effects setting (`I01MissionComposition.SetReducedEffects(true)`), meal 4 | the meal reports reduced motion, largest view deviation 0.00°; beats and sounds still play |
| Repeated menu with the label look, meal 4 | 9.50 s for the 9.50 s timeline, beats in order |

New tests: the beat order around the one real command (and the unchanged pacing), both hands on the tear, three
squeezed sips with a small nod, no jumps anywhere in the meal, the strip as one ribbon that ends in the fingers, the
delivered foley; in PlayMode the beats through the presenter and the nod lease with reduced motion.

## Not run, and limits

- Only the two Meal test assemblies were run, not the other suites; no player build; no full mission playtest.
- The sounds were not checked by ear (no listening in the authoring session): their lengths, levels and spectra are in
  `Audio/Original/MealFoley/audio-provenance.json`, and the game's audio presenter accepted every one in the native run.
- BEFORE has frames of the first meal only (emergency light). Its tool run stopped at its third meal: the game ended
  that meal itself and the first version of the tool treated this as a failure (`evidence/before-checks.txt`).
- Before power is restored the galley is lit only by the red emergency light: the meal now happens in the clear middle
  of the view and reads, but dimly (`frames/after-sip-hud-emergency-light.png`). The scene's lighting is not changed here.
- At the lips the pouch is close to the eye and large, and the eye sees its foil back, not its label.
- The left-hand pinch is built from the rig's finger joints, not measured like the right hand's clasp.
- The real-time shortfall of the right hand while stowing (0.87 m) is the wet-trash bag being out of reach from the
  drawer, as before: the hand lowers the pouch out of the view towards it.

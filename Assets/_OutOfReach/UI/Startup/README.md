# Startup cards (R01)

Self-contained UI Toolkit presenter for the opening startup cards. Lane: `claude-max-startup`.

## Card 1: goat + Slovak double cross, sketched then coloured

- Gold vector art on black, imported from SVG at runtime (`Runtime/VectorArt.cs`). **Document order is stroke order.**
  Each shape is traced like a pencil sketch (stroke-by-stroke `Painter2D` progress, a thin second hand-held pass),
  then all fills fade in, then the team name "Polish Alex Goats" fades in beneath the pictogram.
- A classic pencil-scratch sound plays in sync with the pen. It is **original and procedural** (`Runtime/PencilScratchSynth.cs`,
  `PENCIL-SOUND-PROVENANCE.md`), rendered from the same stroke schedule as the picture, so pen lifts are silent and
  every stroke has its own pressure envelope. No recordings or downloads; nothing audio is committed.
- Reduced effects: shorter, no sketch, no fill animation, no scratch sound. Any key, mouse button or gamepad button skips
  (after a 0.35 s grace) with a short fade of picture and sound.

## Dropping in the final artwork (made in ChatGPT)

Replace the contents of `Resources/StartupCards/startup-art.txt` with the final SVG (keep the file name; `.txt` is
deliberate so Unity imports it as a `TextAsset` without the Vector Graphics package). No code, scene or prefab change.

- Supported: `path` (M L H V C S Q T A Z, absolute and relative), `rect`, `circle`, `ellipse`, `line`, `polyline`,
  `polygon`, `g`, `transform`, `viewBox`, and `fill` / `stroke` / `stroke-width` / `opacity` / `fill-opacity` /
  `stroke-opacity` / `fill-rule` (attributes or `style`).
- A shape with a stroke is traced with that stroke. A fill-only shape is traced in pencil gold, then filled.
- Not rendered: gradients (a gradient fill falls back to pencil gold), clip paths, masks, filters, text, `<use>`, images.
  Ask for flat colours, plain paths and no text when generating the art, and export outlines (convert text to paths).
- Order the shapes in the SVG in the order they should be drawn (large outline first, details last).
- If the art arrives as a PNG instead, it needs an ordered stroke-path SVG alongside it; the PNG alone cannot be sketched.
- The shipped file is a **placeholder**: an original goat head with a double cross.

## Card 2: event card

Text only: `NASA Space Apps Challenge · Nov 14–15 2026` in Barlow Condensed Bold between gold hairlines (fonts from
`UI/Typography` via `Resources/StartupCards/startup-cards.uss`, SIL OFL). **No NASA logo, meatball or other insignia.**

## Behaviour

Plays once per launch (`StartupCardPresenter.HasPlayedThisLaunch`), so Restart does not replay it. While visible it
swallows key, navigation and pointer input so the skipping press cannot activate the menu underneath. It reads no
mission state and owns only its overlay element (and a hidden AudioSource it destroys).

## Composition hook

`Integration~/startup-cards-hook.patch` (not applied; shared files belong to the integrator). Call is
`StartupCardPresenter.PlayOnce(host, reducedEffects)`; the font argument is only a fallback if the shipped style sheet
fails to load.

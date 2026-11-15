# Startup cards (R01)

Self-contained UI Toolkit presenter for the opening startup cards. Lane: `claude-max-startup`.

## Card 1: the logo ident

- Gold (#F5BC74) line art on pure black, 1:1 composition: a right-facing mountain goat with two ridged swept-back horns,
  pointed ear, almond eye, nose/mouth line, wispy beard and neck lines; below the chin a Slovak double cross with flared
  ends standing on three hill arcs. The shipped file is **my own SVG trace of the written spec** (`gen`-built, 23 paths,
  see `startup-art.txt`), not Alex's PNG. It is the swap-in target: `Art/goat-logo-source.png` was not present.
- Imported from SVG at runtime (`Runtime/VectorArt.cs`). **Document order is draw order** (horns, head, beard, cross,
  hills). Each stroke is a tapered, calligraphic ribbon (`Runtime/CalligraphicStroke.cs`: ends taper to a fine point,
  width breathes with direction like a broad nib) revealed along its length, with a glowing pen tip.
- A classic pencil-scratch sound plays in sync with the pen. It is **original and procedural** (`Runtime/PencilScratchSynth.cs`,
  `PENCIL-SOUND-PROVENANCE.md`), rendered from the same stroke schedule as the picture. No recordings or downloads.
- When the last stroke lands a hot-gold glow sweeps diagonally across the mark and leaves a soft bloom; "POLISH ALEX
  GOATS" (tracked-out capitals, Source Sans 3 Semibold) fades in beneath with its tracking settling.
- Hold about 5 s (1.5 s sweep + 3.6 s), then card 1 cross-dissolves into card 2. A very subtle film grain (procedural
  texture, 18 fps jitter, 5 % opacity) lies over both cards. About 15 s in total; any key skips.
- Reduced effects: shorter (about 8 s), finished logo straight away, no sketch, sweep, grain or scratch sound.

## Dropping in the final artwork

Replace the contents of `Resources/StartupCards/startup-art.txt` with the final SVG (keep the file name; `.txt` is
deliberate so Unity imports it as a `TextAsset` without the Vector Graphics package). No code, scene or prefab change.

- A PNG cannot be sketched stroke by stroke. The final art must be (or be traced to) an SVG whose path order is the
  desired draw order: large shapes first, details last. Strokes are re-rendered as tapered ribbons using each path's
  `stroke` colour and `stroke-width`, so outline-only line art is ideal. A fill-only shape is traced in gold, then drawn.
- Optional `data-taper="0..0.5"` on a path sets the fraction tapered at each end (default 0.28, closed outlines never taper).
- Supported: `path` (M L H V C S Q T A Z, absolute and relative), `rect`, `circle`, `ellipse`, `line`, `polyline`,
  `polygon`, `g`, `transform`, `viewBox`, and `fill` / `stroke` / `stroke-width` / `opacity` / `fill-opacity` /
  `stroke-opacity` / `fill-rule` (attributes or `style`). Not rendered: gradients (fall back to pencil gold), clip paths,
  masks, filters, text, `<use>`, images. Export text as outlines.

## Card 2: event card

Text only: `NASA Space Apps Challenge · Nov 14–15 2026` in tracked Barlow Condensed Bold between gold hairlines (fonts from
`UI/Typography` via `Resources/StartupCards/startup-cards.uss`, SIL OFL). **No NASA logo, meatball or other insignia.**

## Behaviour

Plays once per launch (`StartupCardPresenter.HasPlayedThisLaunch`), so Restart does not replay it. While visible it
swallows key, navigation and pointer input so the skipping press cannot activate the menu underneath. It reads no
mission state and owns only its overlay element (and a hidden AudioSource it destroys).

## Composition hook

`Integration~/startup-cards-hook.patch` (not applied; shared files belong to the integrator). Call is
`StartupCardPresenter.PlayOnce(host, reducedEffects)`; the font argument is only a fallback if the shipped style sheet
fails to load.

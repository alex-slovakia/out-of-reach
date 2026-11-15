# CHAPTER-UI-01 native evidence at `479560f`

The native run used the actual saved `Bootstrap.unity` in Unity 6000.3.28f1 with a GPU, in batch mode with no window. It
ran in the chapter-ui session's own worktree, with `UI/Chapter/Integration~` composition and habitat patches applied
locally. The chapter UI installer (`ChapterUiAuthoring.Install`) was applied **in memory only**: the scene was never saved,
and every `Assets` file hash was verified unchanged at the end. The result is `PASS.txt`, with 47/47 checks and 0 soft
failures. There were no runtime errors.

## Route

1. Real Practice UI, W boarding, E seat, the real departure and the arrival.
2. The accident.
3. Days 1 → 5 → 9 → 14 → 20 → 30.
4. Docking, with outcome `Recovered` on day 30.

## Input and shortcuts

The chapter UI was driven only by real Input System keys: R, 1, 2, Enter and Esc.

The run also used these disclosed shortcuts, which are the stations' own Core commands:

- the cockpit check,
- the two interior repairs,
- the first ration and the first rest,
- the day-9 EVA, antenna, link and transmit commands,
- the beacon power-up.

## Files

- `contact-sheet.png`: 16 key screens.
- `NN-*.png`: every capture at 1280×720.
- `checks.txt`, `observations.txt`, `installer-report.txt`, `scope.txt`: the run's own log.

# I04 playable checkpoint

Game source/assets: `c7cfb8681cb3ecc30a188b1d6d9120cac4d0c434`. Unity 6000.3.28f1, URP, Windows x64 Development build, Bootstrap only. Root verified in the isolated integration checkout, then fast-forwarded the original project and opened Bootstrap for Alex. Both remote branches were pushed. No second-computer launch is confirmed.

Actually run on the game content committed in this revision:
- Scene authoring/import/capture: exit 0.
- Physical cargo acceptance: 187 checks passed, including actual 60-active-second deadline, 601 kg rejection, pause, valid fallback, real E pickup/stow, weighted travel, two-hand contact and HUD bounds.
- Saved Bootstrap full mission: 274 checks passed, success/loss/restart, actual repair debits, EVA travel and return, delayed communications and report. QA uses setup teleports; it is not a human walkthrough.
- Windows Development build: exit 0, 369,266,361 bytes total reported build size. Local executable: `Builds/I04/OutOfReach.exe` in the isolated checkout. SHA-256 is adjacent. Executable manual playthrough is not yet verified.
- Repository and offline NASA dataset verification: passed.

The captures here are actual Unity captures. They are distinct from `docs/art-direction/review-03`, which contains generated concepts. An existing Unity Editor Search startup exception was separately observed in earlier runs; full-mission game runtime error checks passed. The build performs ordinary Unity serialization of rendering/player settings; the exact automatic worktree settings delta is retained next to this file rather than silently presenting a pristine whole-tree build. No package or engine update was made. Unity scene serialization has trailing whitespace; the whitespace-only git check is not reported as passing.

Included: physical cargo and two-hand grip, white/red deadline, compact segmented oxygen/food/battery bars, restrained grade, countdown audio, airlock digital timing, delivered ship/hands/stars and corrected EVA controller.

Not included: sprint/exertion, food/growing/sleep survival extension, touchable Earth terminal, seated radio interaction, new rescue ending. Those tasks are now Codex-owned per `docs/DEVELOPMENT-OWNERSHIP-C06.md`.

Latest user playtest feedback for the next version: retain current lighting/head motion; improve menu transitions; fix cargo text depth and backwards airlock plaque; add cargo destination guidance; retain frozen hands during pause; allow walking through the first cockpit disturbance; show normal work gloves and left-click/left-hand, right-click/right-hand gestures. Original checkout must stay unchanged while that playtest runs.

# CARRY-HANDS-V1 on the 23:30 candidate base

`feature/carry-hands-v1-on-2330` is `codex/nasa-2330-candidate@ae6380f` plus the CARRY-HANDS commit cherry-picked
without conflicts (`9c80e53` → `54fe8a9`). Nothing else changed: no Composition, scene, prefab, Core or test change.

Native run in my own clone of that exact tree: Unity 6000.3.28f1, RTX 4070, actual saved Bootstrap, real virtual
keyboard and mouse. Steps:

1. Startup: the R01 startup cards are shown and skipped with Space, like a player would.
2. Practice through the real UI.
3. Walk to the water-tank pallet and press E.
4. Before/after comparison in the same run.
5. Look 0/15/30/50° down and walk.
6. **R06:** put the crate back on the pallet with E, then pick it up again with E.
7. Repeat the look and walk samples with the EVA outfit. For this presentation check only, the suit-on flag is set by
   reflection.

`checks.txt` passes:

- The crate is shown, the separate gloves are hidden and both body arms are visible.
- Both wrists sit on their handle targets, with a shortfall of 0.000 m, in WORK, in EVA and after the regrab.
- After the drop, the crate, the gloves and the arm lease are all released, so no floating parts are left.
- There are no runtime errors.

The tool is `../native-qa/CarryHandsNativeQa.cs`, updated for the R01 startup cards and the drop/regrab check.

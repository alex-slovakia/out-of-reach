# Testing and release checks

## Exact editor

Use **Unity 6000.3.28f1** for both development and verification. The original machine has it at `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`; another machine may use a different install path. Do not run a second editor against a project already open in Unity. Use a separate verification checkout or close the correct editor through its normal UI.

## Automated tests

Run the repository checks before handoff:

```powershell
python tools/verify_repository.py
python tools/nasa/horizons_snapshot.py
```

The NASA command verifies the committed snapshot offline. Fetching new ephemeris data is an explicit coordinated data change, not part of normal test execution.

Claude owns `Assets/_OutOfReach/Tests/`. Write tests around observable rules: valid/invalid mission configuration, resource boundaries, atomic repairs, phase order, pause/restart, airlock gating, signal delay and outcomes. Avoid tests that simply repeat implementation statements. Separate pure domain EditMode tests from scene/component PlayMode tests.

Example PowerShell in a verification checkout (paths may be adjusted):

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Force TestResults | Out-Null
& $unityEditor -batchmode -nographics -projectPath $projectPath -runTests -testPlatform EditMode -testResults "$projectPath/TestResults/editmode.xml" -logFile "$projectPath/TestResults/editmode.log"
```

Run PlayMode tests similarly with `-testPlatform PlayMode`. Do not add `-quit` to the test command without verifying runner behavior. Wait for completion, inspect the test XML and log, and report pass/fail counts and exit status. An editor process starting is not a successful test. If no tests exist yet, report that explicitly.

## Acceptance matrix

| ID | Scenario | Expected behavior |
|---|---|---|
| A01 | Open a clean clone | Correct editor, package restore, zero compilation errors, no missing GUIDs/scripts |
| A02 | Choose an invalid loadout | Launch disabled with a specific violated constraint and units |
| A03 | Compare two viable loadouts | Later repair/survival/science trade-offs differ and report explains why |
| A04 | Trigger the accident twice or reload during it | No duplicate damage; no stuck camera, blackout or input lock |
| A05 | Attempt repair without enough parts/power | No partial consumption; failure reason shown |
| A06 | Complete and repeat a repair | Cost charged once; system state and visual agree |
| A07 | Pause and resume | Resource and signal timers pause; no large catch-up drain |
| A08 | Exit airlock unsuited or unsafe | Exit blocked; correct prompt; doors cannot defeat gameplay gate |
| A09 | Complete EVA and return | Constrained route usable, antenna repair registered, safe re-entry possible |
| A10 | Deplete resources during EVA | Deterministic failure; no negative quantities or softlock |
| A11 | Send distress signal | Correct dataset/epoch/distance shown; reply waits for outbound + inbound + processing time |
| A12 | Use time compression/chapter transition | Visible label; resources and signal time follow documented rules |
| A13 | Win, fail, restart | Both outcomes reachable; report correct; fresh state and no duplicate event listeners |
| A14 | Build and launch offline | No dependency on live NASA/API requests; controls, credits and sources accessible |
| A15 | Reduced-effects mode | Objectives remain clear with shake/flashing reduced and audio muted |

## Build and visual verification

In Unity 6: **File → Build Profiles**, select/create **Windows**, choose x86_64, add the integrated Bootstrap scene and inspect included scenes. Use **Build** to an ignored `Builds/Windows` folder. Only the coordinated integrator changes saved build/project settings. Development builds are appropriate for diagnosis; test the intended submission build separately.

Run the executable from a fresh extraction, not only from the editor. Play a complete success and failure path. Check camera/audio listener count, collisions, HUD at common resolutions, subtitles, pause, restart, restored mouse cursor and closing the app. Capture warnings/exceptions and measured performance with machine details. A second-machine launch remains unverified unless someone actually performs it.

## Release evidence

Record commit SHA, exact editor, build configuration, test counts/log paths, manual scenarios and tester/machine. Include controls, NASA provenance, assumptions and asset credits. Record a 60–90 second demo: planning trade-off (15s), accident/interior repair (20s), EVA (20s), delayed communications/outcome/report (20s). Editing may condense time, but caption simulation time and do not misrepresent the playable flow.

Keep the final two hours before the confirmed submission cutoff for packaging, upload and verifying links. No passing build or playthrough is claimed by this document.

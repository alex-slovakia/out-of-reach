# I03 presentation checkpoint

Runtime source: `fb9703b9882654c35cc5a88237d6e2f90afc4ad3`, Unity 6000.3.28f1, Windows/D3D12, RTX 3070. Executed November 14, 2026 around 13:48–13:52 UTC in the isolated integration checkout.

Includes Claude's R01–R05 ship through `ba8b87f`, gloves from `f72224c`, camera-only grounded walk motion, sparse V03 HUD, GMAT-derived angular star sky, independent authored sunlight, and removal of four unresolved unused legacy volume components. Sun direction/exposure is artistic, not sampled ephemeris.

- Saved Bootstrap rebuilt and compiled, exit 0: one camera/listener/player/runtime, eight reachable mission targets, two airlock gates.
- Full automated mission smoke: **240 checks PASS**, actual input/UI through success and loss, reports/restarts, suit/gloves visibility, pause and reduced motion, walking/collisions, real-input EVA round trip, delayed reply. `checks.txt` records observations. Cabin QA positioning uses disclosed teleports: this is not a human walkthrough or standalone playtest.
- Actual grounded motion peak in this run: 2.354 mm. Interaction ray stays above the camera-only offset. Reduced-effects movement and EVA pivots remained neutral.
- Sky probe: six real GPU views; exact texture hash/GUID/material binding checks; camera translation by (37.25,-12.5,91.75) with fixed orientation changes **zero pixels**, repeat control also zero. Source observation epoch remains unspecified; J2000 alignment inferred. This does not validate precise star astrometry, spacecraft attitude, planets, or a common Sun/planet ephemeris.
- Repository verification passed; static Unity checker found zero errors, one expected warning for the temporary dynamically-added QA driver in a multi-class file.
- Game runtime errors empty. Known Unity Editor Search startup exception is separately recorded in the ignored editor log; not hidden as a game success claim.

No new Windows build or second-machine/manual performance pass has been run for this source revision. The internal I02 ZIP remains an older frozen build. C05 physical cargo/crafting, sprint, full body, interaction hand animations, moving opening/pause menus, latest requested lighting/audio polish and planets are **not included here**.

The two images are actual 1280×720 game camera + UIDocument render captures, not concepts. ScreenSpaceOverlay UGUI is excluded by this offscreen capture mechanism; impact/blackout visual overlap is not verified by them.

Reproduction: copy `I03PlayQa.cs` as `Assets/Editor/I02PlayQa/I02PlayQa.cs` and its asmdef into a separate ignored clone; preserve the filename required by the runner. Invoke `I02PlayQa.Run` with graphics and without `-quit`. `GmatSkyProbe.cs` is temporary default Editor assembly code; invoke `OutOfReach.Verification.Editor.GmatSkyProbe.Run` with graphics and `-quit`. Never install these temporary runners in the production Assets tree. Root's raw editor logs remain ignored because they include machine-specific environment details.

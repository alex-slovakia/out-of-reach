# I02 internal checkpoint — 14 November 2026

Runtime revision: `7e41699008f269982808a803eeb8cde50886e9d9`. Unity 6000.3.28f1, URP, Windows x64 development build. Private team test, not an organizer submission.

## Evidence actually run

- Scripted saved-Bootstrap mission harness: **198 checks passed**, process exit0. Both recovery-possible and mission-lost/report/restart paths. Input System movement/collision, planner submissions, repeat-safe repairs, suit/airlock, actual-input EVA round trip, antenna, old recording and causally delayed Earth reply.
- Interior positioning uses documented QA teleports. Resource stepping is suspended around exact accounting/captures. This is not a human playthrough.
- Runtime error collection is empty. A separate UnityEditor.Search startup ArgumentOutOfRangeException was recorded and is included; no zero-editor-exceptions claim.
- Windows build reported success, exit0. Unity-generated build-time serialization/URP prefilter cache changes were retained locally as a patch and not promoted as authored project settings.
- Archive was read back: all314 packaged file hashes match the manifest. ZIP SHA-256: `5ac03cdeb2bf2afe1e8bdd0b2003f27ef5adac8f084fd0aa8db9a8cfb8665744`.
- Original Alex project updated by merge `4784913`. Its runtime/Bootstrap Git blobs match7e41699; it adds only two unused optional C05 interfaces. Its own Unity import/compilation exited0. Local voice-script metadata edits were preserved byte-for-byte.
- Original repository verification:571 GUIDs PASS. NASA Horizons offline verification:32 samples PASS.

## Visual evidence and fixes

Captures use the actual player camera and actual UI document at1280x720 after ScreenCapture returned no image in batch mode. They are not generated concepts. Screen-space overlay UGUI is excluded; see scope.txt. UI is the rejected interim layout, not the upcoming redesign.

The scene clears BatchingStatic only on the five model instances to resolve reproducible torn geometry; source model prefab unchanged. CharacterController minimum movement is zero on the scene instance to prevent tiny frame steps being discarded. EVA approach now ends beside the antenna service area instead of within the dish.

## Reproduce the scene harness

Use a separate ignored `.local/` verification checkout at7e41699 with its own Unity Library, never a second editor on an open project. Copy `tools/verification/I02PlayQa.cs` from this evidence commit into that checkout's temporary `Assets/Editor/`. Run Unity with `-batchmode -force-d3d12 -projectPath <verification-checkout> -executeMethod I02PlayQa.Run -logFile <local-log>`. The runner exits itself; do not add `-quit`. Inspect PASS/FAIL, runtime errors, scope and images under `.local/i02-play-qa`. The harness rejects non-verification project paths. Do not install it in the player's project or ship it.

## Remaining acceptance

Manual standalone success/failure runs, second-machine launch and measured performance remain unverified. C05 cargo/crafting, new UI, first-person hands/body, catalogue star rendering and Earth/Mars renderer are not included. Current distant stars are procedural. This is a testable checkpoint, not the finished proposed game.

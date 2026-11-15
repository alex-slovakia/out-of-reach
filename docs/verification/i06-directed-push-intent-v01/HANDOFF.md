# DIRECTED-PUSH-INTENT-01

Staging only. One production candidate: `Assets/_OutOfReach/Scripts/Composition/Runtime/I06MicrogravityBridge.cs`; preserve its existing meta/GUID64bcbff3d7f5582aacc52fb37d0641c8. Import exact manifest or apply `presenter.patch`. Baseline bridge SHA-2565ab7c8b49e6ce654271b31e3f9820b9aa3c28c0de4dc4946a9c4b72ec22a6e91. No Player/Core/input/hand/scene changes, no new public API, no Unity process. Root integrates only after the active native run ends.

## Corrected request law

Let `n` be the actual normalized surface normal, `i` the normalized one-shot travel intent, and `away=Clamp(Max(.25, Player.MinimumAwayDot+.025),0,1)`, exactly the existing cone bound.

1. If `dot(i,n) >= away`, retain `i`. An already-away45° request therefore remains45°, instead of becoming the old cone edge75.52° from the normal.
2. Otherwise let `t=i−n*dot(i,n)`. When `|t| >= .25`, retain the original cone formula `n*away + normalize(t)*sqrt(1−away²)`. Ordinary tangent W input is unchanged.
3. For smaller tangents, `r=Clamp01(|t|/.25)`, `angle=SmoothStep(0,1,r)*acos(away)`, and output `n*cos(angle)+normalize(t)*sin(angle)`. Exact zero tangent returns `n`.

This follows the root's explicit correction to [Claude's review](https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6886586788). Claude proposed a hard normal fallback below25% tangent; that would retain a75.52° jump at the default threshold. The staged version blends the angle through that interval. Direction is continuous at zero tangent, the25% tangent join and the already-away boundary. The already-away boundary can retain a derivative kink; no claim of globally identical sensitivity or physical astronaut tuning is made.

All outputs are unit directions inside the existing outward cone. The original configured force strength, momentum addition/cap, reach, wrist/skin, target, occlusion, cooldown and authoritative Player law remain unchanged. Direction is still captured in the contacted surface's local frame and used only after the real visible contact callback. Plain no-intent push and Grip retain their existing paths. Invalid/nonfinite vectors, zero/deadzone vectors and squared-length overflow fail closed; the public original deadzone threshold is preserved, including its exact-boundary ordinary-normal behavior.

## Evidence

`compile_and_test.ps1` compiles the **full candidate bridge**, then compiles that same source into a standalone test executable. Tests obtain the private pure method from that actual compiled candidate; there is no second implementation of the new law. Existing installed Unity assemblies/.NET8 are used. No new dependency.

- Full candidate reference compile passed with warnings as errors.
-40,908 actual-source assertions passed:45° outward preservation, near-opposite/zero tangents, unchanged tangential cone, both joins, invalid vectors, scale invariance,1,200 rotated/random cases, unit length/minimum-away bounds and unchanged actual Player pure push magnitude.
- A5,200-step inward sweep (tangent increments0.0001) measured maximum successive output-vector difference0.00079114584; no hard threshold jump. This samples continuity and complements the analytic limit; it is not native movement evidence.
-35 source guards verify exact unchanged contact/lease/geometry/lifecycle methods, original dispatch/magnitude, preserved meta, baseline and candidate manifest, and all frozen V04 files.

The initial test harness compile found an ambiguous `Random` name; it was qualified as `System.Random`, then the full source compile/tests passed. Native animation/contact/physics/input and complete route acceptance remain unrun for this candidate.

## Integration warning for QA

**V04 is untouched. Its QA steering helper still predicts the old cone for directed input.** After this production correction, those predictions no longer match already-away or near-inward requests. Do not interpret a later V04 route result as a calibrated policy proof or silently update it to manufacture a pass. Keep the existing run as old-law evidence. Root can separately authorize a clearly versioned driver using the corrected actual source law, followed by native real-contact and full-route regression.

Reproduce: `prepare.py`, `source_check.py`, `compile_and_test.ps1`. Check the baseline hash before import; the prepare script intentionally refuses a different live bridge. Existing script metadata is copied unchanged, not regenerated.

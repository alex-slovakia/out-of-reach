# Compact open-sea direction — proposed 2026-11-14

## Latest correction: the transport must be visible and boardable

**User-approved visual direction:** the user approved the boardable transport ("way better this makes sense") and then the launch storyboard ("love it"). Approval is for the target appearance/sequence, not a claim that those features exist in the game.

![Actual transport and direct cargo-loading route, proposed reference](02-boardable-transport-direction.png)

The user rejected the standalone platform/cabin as an incomplete depiction of the opening. The essential subject is the actual spacecraft: carry supplies through its open hatch, secure cargo inside it, board before departure, secure the hatch, then experience ignition/ascent and later rendezvous with AURORA-7. AURORA-7 is already in orbit; the loading vehicle is the smaller Earth-to-orbit transport.

Keep the existing deck kit as a reusable staging module. Connect its transfer gate to the transport via a protected boarding walkway. The proposal depicts an elevated crew-access deck next to a capsule on a rocket, with a marine launch-support barge below. Exact dimensions, launch safety clearances and flight performance are not validated by the generated picture; authoring must keep the interior and external hull geometrically consistent. No ignition with the access bridge connected or the cabin unsecured. Avoid building an unrelated hut and calling it the spacecraft.

Gameplay intention: a 60-second loading window, an explicit boarding/hatch transition, launch cinematic, and orbital arrival. Cargo becomes aboard only when physically stowed inside the transport; supplies carry forward to the mission ledger. Expiry and failure-to-board behavior still need explicit implementation/verification.

This image is an approved generated art reference, not an implemented scene or an approved engineering design. Its exact prompt is `02-transport-prompt.txt`.

## Approved launch storyboard

![Approved four-beat launch storyboard](03-launch-storyboard.png)

1. **Cabin secure:** retain the actual selected cargo in strapped racks, sit/recline and secure the restraint, shut and latch the hatch. Door motors, latch contacts, breath and a restrained radio call create tension. Camera retains a small permitted look range.
2. **Liftoff:** only after the player is aboard, hatch secured and access bridge clear. Engine startup, structure vibration and sea-level steam/spray; cut to the external launch view. Reduce-motion mode substitutes restrained motion and avoids strong flashing.
3. **Ascent:** return to the seated first-person view. Physical fittings and hands carry vibration, daylight changes through the window; no full-screen random shake. Cinematic compression must be disclosed as a time transition, not instant orbit.
4. **Later, orbital arrival:** booster no longer attached, capsule/service section approach AURORA-7. Use the actual existing AURORA-7 model rather than remodeling it from the provisional storyboard silhouette. Exterior space shots use score; cabin/structure/radio audio remains physically motivated. Dock and transition into the existing ship.

Target presentation length is approximately 40–60 seconds, subject to integration/playtest. This is edited narrative time, not a validated flight simulation. The later calm cruise and accident remain separate beats after boarding AURORA-7. This storyboard is not a video and has not been implemented.

User instructed us to abandon Krea if it remains blocked. The 3D trial required a paid plan, so this pipeline is stopped: no more Krea retries or paid upgrade. Continue with existing Blender work and built-in reference generation. Live connected Krea homepage reported 200 compute units and free-plan status, but no recent generation outputs were visible; the separately mentioned rock was not retrieved or imported.

Exact storyboard prompt: `03-launch-prompt.txt`.

## Earlier platform-only image — superseded as a complete opening

![Two views of the proposed compact offshore platform](01-open-sea-compact-direction.png)

This earlier generated visual target is retained for material/deck detail only. It is not a complete prologue and not an in-game capture. User requested an achievable open-sea location, detailed ropes, wet surfaces and water effects. The generated hands are pose/material references only; existing functional cargo grips remain authoritative.

## Build boundary

- One approximately 8 x 6 m offshore staging deck; one small cabin and one cargo rack.
- A protected 2.2 m carrying route, safe rails and gated boarding/transfer points.
- Maintainable steel panels, drainage, cleats, secured ropes, rubber fenders and a few practical lamps. Keep loose props out of the route.
- Wave, foam and restrained splash effects provide atmosphere. Water is a separate runtime system, not a mesh baked into every prop.
- No cliff compound, island, city, giant gantry or launch rocket on this loading deck. This is staging before transfer; the cabin is not itself a launch vehicle.
- Physical carrying HUD: carried kilograms only in the lower left; timer and the current objective remain separate. Full supply details are optional inspection.

## Ownership / state

- Codex model agent: original Blender platform kit in `.local/verification/MarineLoadingDockV02`, including scale/anchor/collision evidence; no live Unity edits.
- Codex integrator: scene placement, cargo route, runtime water/effects and gameplay validation.
- Claude: retains assigned habitat, radio-desk and rest/recovery work; no duplicate marine-deck task.
- This board has been shown to the user but the final model has not yet been accepted or integrated.

## External assets and Krea trial

- Poly Haven Rock Moss Set 01: https://polyhaven.com/a/rock_moss_set_01 — released 3D set, CC0 under https://polyhaven.com/license. Optional fallback for a location that actually needs rocks; no rock field is required for this open-sea platform. Not downloaded or integrated at this checkpoint.
- Poly Haven River Small Rocks: https://polyhaven.com/a/river_small_rocks — a surface texture, not a 3D rock set. Not integrated.
- User connected Krea. One text-to-3D rope request using `tencent/hunyuan3d-3.1-rapid` returned HTTP 402, `required_plan: Basic`, before producing any model. No upgrade was made. User separately reports an active rock job; output not yet available through the connected Files listing.
- Krea public pricing lists 100 free units per day, but that does not establish entitlement for each 3D model. The direct account response governs this trial. https://www.krea.ai/pricing

Generated with the built-in image tool. Exact prompt is in `prompt.txt`.

# CLAUDE-GAIT-03 work in progress (paused for CLAUDE-EVA-ROPE-04)

Status: diagnosis and design only. No runtime gait code was written before the pause.

## Diagnosis of `EmbodiedBodyPresenter` (base `0d64487`)
- Cadence: full walk pace uses a 0.72 m step at 2.5 m/s, about 3.5 steps/s (208 per minute);
  sprint uses 0.92 m at 3.75 m/s, about 4.1 steps/s. This reads as marching or toy-like legs.
- Clearance: lift `(4s(1-s))^2` is peaky; the preflight gait lifts 0.025–0.063 m, which reads as shuffling.
- Knee pole `(side*.35, 0, 1)` outside preflight splays the knees by about 19 degrees.
- Pelvis: vertical drop only. There is no lateral sway over the stance foot.
- Foot: flat through stance. Swing pitch is ±7–13 degrees, with no heel strike or toe-off rocker.
- Arms swing only in the preflight gait (12–19 degrees). Arms are never prepared outside a preflight policy.
- Every distance is a world-unit constant fitted to one body (step, lift, 0.092 m half-width, 0.021 m drop).
- Body-to-eye: the standing visual body follows `feet + yaw*offset`, using the caller's PreflightStandingFit
  `(0,0,-.285)` and InFlightStandingFit `(0,0,-.197)`. In flight it sits about 9 cm further forward,
  so at a normal downward look the abdomen fills the lower view (Alex cockpit screenshot).

## Planned design (resume from here)
- New `ModelGait.cs` with pure math, plus a testable rig built from semantic roles. Distances are fractions of
  the measured leg length L (thigh plus shin) and the hip half-width W.
- Cadence model from Froude speed `v/sqrt(gL)`: step/L = 1.15*v^0.6, clamped to [0.35, 1.7].
  Stance runs from 0.62 (slow) to 0.40 (v=1.0) and 0.30 (v>=1.6), keeping the reach stance*step at or below about 0.47 L.
  The speed range of about 1.2–5 m/s stays plausible.
- Clearance runs from 0.07 L to 0.25 L with speed (carrying x0.6, which stays at or below the old carry knee lift).
  The lift is a single skewed bump.
- Heel strike of 6–12 degrees, rolling to a toe-off of 16–24 degrees, with exact rocker offsets about the heel and ball
  (ball lever measured from the foot's descendants). The contact point does not slip.
- Pelvis sway 0.022 L walk / 0.008 L run toward the stance foot; crouch 0.015–0.045 L; SmoothHigher reach drop.
- Knee pole `(side*.08, 0, 1)`. Minimum stance half-width 0.9 W.
- Opposing arm swing (14–30 degrees, elbow flexing on the forward swing) whenever the WORK arms are owned (mask Both).
  Never for EVA.
- Pitch-dependent posture: the pelvis moves back by up to about 0.13 L as the camera looks down 12–70 degrees, with the feet
  planted by leg IK when idle. It blends into a whole-body stride shift while walking. The camera is untouched.
- Integrator ask: decide whether the model-derived fit supersedes the composition's hard-coded standing fits
  (do not compensate twice). Cargo: keep body arms visible and lease the WORK arm chains to the cargo presenter
  (two-bone IK to the handle grips, run after order 200, restore on `PoseWillReset`) instead of separate sleeves.

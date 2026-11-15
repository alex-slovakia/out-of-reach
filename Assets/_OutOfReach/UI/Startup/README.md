# Startup cards (R01)

Self-contained UI Toolkit presenter for the opening startup cards. Lane: `claude-max-startup`.

1. Black card with an original gold pencil-drawn Slovakia pictogram (procedural, `SlovakiaPictogram.cs`: about forty
   approximate border points redrawn as three jittered outline passes plus diagonal hatching and a Bratislava mark).
   The outline is a simplified illustration, not a survey and not a NASA dataset.
2. Second card, text only: `NASA Space Apps Challenge · Nov 14–15 2026` and the team name `Polish Alex Goats`.
   No NASA logo or other third-party artwork is used. The line is the permitted event wording from issue #11 R01.

Fades in and out. Any key, mouse button or gamepad button skips (after a 0.35 s grace) with a short fade into the menu.
Reduced effects: shorter, fade only, no pencil draw-on. Plays once per launch (`StartupCardPresenter.HasPlayedThisLaunch`),
so Restart does not replay it. While visible it swallows key, navigation and pointer input so the skipping press cannot
activate the menu underneath. It reads no mission state and owns only its own overlay element.

Fonts: pass the existing UI font (`I01SessionControls` already holds one) or a StyleSheet; both are optional.

Composition hook: `Integration~/startup-cards-hook.patch` (not applied; shared files belong to the integrator).

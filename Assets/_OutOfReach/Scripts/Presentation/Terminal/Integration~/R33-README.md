# R33 Earth terminal: integration notes (held for the owner; nothing here is applied)

Default behaviour after R33 (presenter only, no composition change needed):
- The MissionTerminal screen is lit and readable when idle: the dated EPIC globe (2026-11-11 11:01:17 UTC), mission UTC, image age, and a dated MODIS Terra Slovakia inset (2026-11-14 08:15-08:20 UTC). The inset stays dark until mission time passes 08:20 UTC.
- E enters the existing focus. If the root's body-reach frame and the existing glove hooks (`separateGloveInstance`, `rigJson`, `TerminalGlovePose`) all succeed, the gloved hand presses the screen and drags the globe, as in the i06-earth-terminal-native-v05 evidence.
- If reach or the glove hooks are unavailable, entry no longer fails with "Actual body reach is unavailable". The on-screen cursor becomes the hand: a ring while hovering, a filled grab disc while held, and dragging over the globe rotates it. Escape closes in both modes.
- `staticImageMode` (off by default) is the fallback: a look-only Slovakia image with a NASA-derived weather readout and no touch or drag.

`R33-I06EarthTerminalBindings-static.patch` changes `Composition/Runtime/I06EarthTerminalBindings.cs` only when `staticImageMode` is on. In that mode it keeps the cursor locked and stops feeding pointer input. With the default interactive mode, it has no effect.

    git apply "Assets/_OutOfReach/Scripts/Presentation/Terminal/Integration~/R33-I06EarthTerminalBindings-static.patch"

Caller impact: none. The patch reads only the new `PhysicalEarthTerminalPresenter.IsStaticImage`. `UsesCursorHand` is also available if root wants to hide the OS cursor in cursor-hand mode.
Credit lines for the credits/about screen are proposed in `R33-credits-proposal.md`. They are not applied.

Not verified: shader compile, in-Editor look, actual entry with Apoliak's current rig. These need the Unity Editor.

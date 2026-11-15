# R33 static Earth terminal: composition hook (optional, integrator-owned)

The presenter change works on its own. With `staticImageMode` on (prefab default), `TryEnter` takes only the root camera lease, `SubmitPointer` is ignored, and the screen shows the Slovakia image whether or not the player is focused. No glove, reach, touch or drag remains.

`R33-I06EarthTerminalBindings-static.patch` is a small polish for `Composition/Runtime/I06EarthTerminalBindings.cs`. In static mode it keeps the cursor locked and hidden, because nothing can be clicked, and stops feeding mouse input. Escape still closes. Apply it from the repository root:

    git apply "Assets/_OutOfReach/Scripts/Presentation/Terminal/Integration~/R33-I06EarthTerminalBindings-static.patch"

Caller impact: none. The patch reads only the new `PhysicalEarthTerminalPresenter.IsStaticImage` property.
Credit lines for the credits/about screen are proposed in `R33-credits-proposal.md`. They are not applied.
Held for the owner: do not apply this patch or the credit lines without the integrator.

Rollback: set `staticImageMode` to false on the MissionTerminal prefab presenter. That restores the EPIC globe path unchanged.

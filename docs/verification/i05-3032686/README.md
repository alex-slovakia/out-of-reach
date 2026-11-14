# I05 playable feedback checkpoint — 3032686

Unity 6000.3.28f1, URP, Windows x64 Development build. Runtime and saved Bootstrap at 3032686 match the focused/full scene runs; this commit adds an Editor-only I05 build entry point after those runs.

## Delivered in this checkpoint

- Held Shift sprint and controller-owned exertion, forward walking bob, actual movement during the opted-in impact cinematic.
- Centered carried cargo with both visible work gloves, one 25 kg label for the spare kit, detailed loadout only while holding Tab, exact cargo-bay direction marker.
- Cargo source/bay label visibility follows actual stock and phase; depth-tested text. Visible/frozen hands during pause, proper resume/restart.
- Transparent subtitle backing, neutral interaction text, restrained pressure display, menu hover/confirm audio and pause/menu presentation motion.
- Two authored solid cockpit/engineering shoulder closures. Six boundary rays and nine central-clear rays pass; the reported player escape is not independently reproduced, so this is not a full containment proof.

## Verification

- Focused run 20261114T171611390Z: **107/107** actual UI/controller/scene assertions. Hidden Unity process exit **0**.
- Full mission run 20261114T171731547Z: **274/274**, covering success, objective loss, actual repair costs, EVA outward/return, delayed Earth contact, pause and restart. Hidden Unity process exit **0**.
- Runs use explicit QA positioning, synthetic controller focus regain for the hidden editor, and actual commands/input. They are not a human desktop walkthrough. Full run suspends automatic simulation around exact accounting/captures, as documented in its scope.
- Earlier focused run 20261114T171116326Z passed 107 assertions but had a native Editor background-Ticker crash after Mono shutdown. No production-code change was made to hide it. The unchanged repeat and full run exited cleanly. Cause unconfirmed; preserve the earlier log locally.
- Fresh Windows build from 3032686: **succeeded**, 369,312,768 reported bytes, process exit **0**. Output `.local/i01-integration/Builds/I05/OutOfReach.exe`. EXE SHA-256 `0e74ad636ce1132a6165c348645008943dc1a5201978378827adf1d52de0312e`; this hashes the launcher only, not the whole build folder. Standalone human playthrough not yet established.
- Repository verifier passed: 733 asset GUIDs, version/serialization/JSON/exclusion checks. Source whitespace check passed; Unity's generated scene has ordinary serialized trailing spaces.
- Temporary QA scripts, generated Editor settings and rejected/unreviewed concepts are excluded from production commits. Local voice-production edits in the original project must be preserved during promotion.

## Not delivered

This remains the connected contact-restored mission with the I05 feedback improvements. The full open-sea boarding/ascent, post-contact survival/rest/recovery chapter, seated radio/terminal interactions, continuous first-person body, tool-repair choreography and new rescue/Mars ending are not playable here. C06 extension remains disabled until its complete UI/interaction/outcome wiring is ready. Cyan-view bug B24 remains open/unreproduced. No claim that every user bug or addition is complete.

Claude habitat 36211e8 was already delivered as art; the earlier 18:15 status incorrectly retained its old estimated delivery time. It still needs Unity import/integration. Claude rest/recovery 33de03c is delivered source under separate review, outside this checkpoint.

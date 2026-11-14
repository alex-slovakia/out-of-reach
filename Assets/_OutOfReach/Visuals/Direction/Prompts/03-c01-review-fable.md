# OUT OF REACH — independent C01 review

Use the available Fable model in a SEPARATE read-only Claude session on Apoliak's checkout, after the implementing Claude reports C01 ready. The filename does not select a model. Do not interrupt or duplicate the implementation session.

Read AGENTS.md, PRODUCT.md, ARCHITECTURE.md, INTEGRATION.md, NASA-DATA.md, TESTING.md and canonical Assets/_OutOfReach/Scripts/Contracts/*.cs. Record the exact commit and working-tree state. Scope: C01 implementations in Core, Simulation, Inventory and their Tests. Do not modify, commit, merge, switch branches, install packages or regenerate scenes. Do not expose secrets. If C01 does not yet exist, report that fact and stop; do not write a replacement.

Review these observable requirements:
- Planner has at least two valid loadouts, real mass/budget/power tradeoffs, valid ID/count handling, and freezes a defensive copy exactly once on launch.
- Resources reject negative, NaN/infinite, invalid integer-part costs and time; duplicate resource IDs aggregate; failed requests debit nothing; quantities remain finite and nonnegative.
- One simulation clock; explicit pause/restart; timestep consistency and correct W*s/3,600,000 kWh conversion.
- Guarded mission phases and one authoritative state. No rendering dependencies or duplicate success events. Cinematic acknowledgements reject stale/duplicate IDs and missing presentation cannot strand Intro.
- Event subscriptions clean up, exceptions do not prevent later subscribers, and tests exercise actual failure boundaries rather than mirror implementation.
- Ownership and actual contract signatures are respected. Missing shared members become precise change requests, not private substitutes.

Run only safe read-only analysis. If test execution would conflict with an already-open Unity checkout, do not launch another editor; cite provided exact-revision test evidence or report it missing. Never claim a passing compile/test from source inspection.

Deliver at most five actionable findings, ranked by player impact, each with exact file/line, reproduction or reasoning, and minimal fix. Then list verified evidence, missing evidence, and integration-ready class names/construction requirements. Include a compact handoff the user can paste into the main Codex chat. No general praise, large rewrite or new systems.

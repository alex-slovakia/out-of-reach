# Read-only review — Claude Fable 5.2

Open a separate review chat using **Claude Fable 5.2**. Give it a clean read-only checkout or the relevant files/commit diff. Do not run it as a second writer in the implementation checkout. Model selection is manual.

Review OUT OF REACH, Unity **6000.3.28f1**, for integration and scientific credibility. Read PRODUCT.md, ARCHITECTURE.md, AGENTS.md, INTEGRATION.md, NASA-DATA.md, TESTING.md, current Contracts, the NASA manifest/CSV and the proposed implementation commits.

Do not modify files, branches, settings, permissions or remote issues. Do not merge. If no precise revision/diff is supplied, inspect current HEAD read-only and state its SHA.

Find actionable defects, prioritizing:

1. Competing domain authorities, mismatched interface signatures, cross-owner edits, mutable snapshots, lifecycle/event leaks, and cinematics that can strand the mission.
2. Resource units, invalid values, atomic costs, duplicate actions, frame-rate dependence, paused time and time-skip thresholds.
3. Whether NASA data actually influences gameplay rather than only appearing in credits. Verify provenance, epoch, target/centre, geometric-range proxy, offline packaging and known-row calculations. A Mars reference is not an exact vessel trajectory.
4. One-way versus round-trip delays, processing time, compressed display, premature replies, impossible rescue, fictional echo, instant hydroponics or numerical precision unsupported by the model.
5. Planner choices that fail to affect later play/report, inaccessible objectives, broken success/failure/restart paths, or missing evidence for test/build claims.

Return only a short ranked list of concrete findings with file/line, trigger, impact, suggested owner and a focused validation case. Distinguish blockers from optional polish. Do not redesign the whole game or expand scope. If no defect is found, state what was checked and what remains unverified. The implementation owner fixes issues after review; this chat stays read-only.

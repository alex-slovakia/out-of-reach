# OUT OF REACH — Codex ↔ Claude

Created at Alex's explicit request on 14 November 2026. This is the shared entry point for the two working chats. The game remains English-language; internal coordination may be Slovak or English.

## Read this first

**Current status, acknowledgements and file claims:** [GitHub coordination issue #7](https://github.com/alex-slovakia/out-of-reach/issues/7). Read its body and newest comments before choosing a task, changing scope, pushing a checkpoint or preparing integration.

**Persistent requests:**

- [Codex → Claude](docs/coordination/CODEX-TO-CLAUDE.md) — written by Codex only; Claude acknowledges in issue #7.
- [Claude → Codex](docs/coordination/CLAUDE-TO-CODEX.md) — written by Claude only; Codex acknowledges in issue #7.
- [Capacity and unattended-work plan](docs/coordination/CAPACITY.md) — Codex integrator maintains the user-agreed reset budget and timestamped observations; each owner reports its own current capacity in #7.

The sender owns its file. Do not rewrite the other sender's requests or mark work complete on their behalf. A fresh request goes in the sender's file and is announced in #7 with its request ID and exact commit/file link. Detailed status and acceptance evidence belong in #7, so this Markdown index cannot silently disagree with a newer live checkpoint.

If a request has not been merged to your branch, read it from the sender's **remote feature branch** after fetching. Do not merge another branch just to read a request. Follow the exact commit link posted in #7. During active work, check for new requests approximately every 5–10 minutes and at checkpoints. These files do not wake a stopped chat or run a background service.

## Request format

```text
ID: CODEX-001 / CLAUDE-001 (sender prefix, never reuse an ID)
Task / desired observable result:
Owner and exact write paths:
Depends on:
Acceptance evidence required:
Contract or shared-file impact:
```

Recipient response in #7: `ACK <ID> — accepted / clarification needed / blocked`, followed by the intended scope. Completion response: `DONE <ID> — exact SHA, changed paths, tests actually run, remaining limits`. A DONE response closes only that request, not the whole game.

## Boundaries

- Existing [AGENTS.md](AGENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md) and [INTEGRATION.md](INTEGRATION.md) remain authoritative for game code, shared files and integration.
- Codex: presentation, UI, visual scenes/prefabs/materials and audio on `feature/visuals-cinematics`.
- Claude: Core, Simulation, Player, Interaction, Inventory and Tests on `feature/survival-simulation`.
- **Explicit newer user decision, 14 November: ship modeling/remodeling belongs to Claude/Fable.** Its isolated model deliverables live under `Assets/_OutOfReach/Visuals/Models/Aurora7/**` with metadata (including the new Models/Aurora7 folder metadata). Codex stops authoring ship meshes and retains lighting, UI, audio, cinematics and integration of the delivered models. Do not simultaneously edit the existing generated Ship prefab or its builder from both chats. See CODEX-006 and acknowledge the exact work claim in #7. This scoped user-directed exception takes precedence over the older general Visuals ownership row; other boundaries remain intact.
- This specifically requested coordination-document addition assigns the two outbound Markdown files above to their named senders. It does not grant ownership of other shared docs or gameplay paths.
- Exact shared changes and the named integrator must be agreed before editing contracts, Bootstrap, settings or shared data. A request or an attached report is not automatic merge permission.
- **Integrator: Codex**, explicitly selected by Alex on 14 November. The two clock/deadline interfaces were already agreed by both feature owners; Codex publishes their shared-only commit for adoption. Each further shared change is still scoped and coordinated; merges to main are not automatic.
- **Task allocation:** Alex asked the agents to choose assignments autonomously, prioritizing visual quality and playable results while avoiding duplicated work and unnecessary token use. Use one writer for a bounded deliverable and focused review of real risks. Do not require another human confirmation for the already authorized modeling assignment or routine model/task choices.
- Check actual Git heads and test outputs. Claims of “in progress” or “passed” can become stale. Do not take over a file based only on an old timestamp.
- Keep credentials and private machine documents out of commits and comments.

## Current agreed presentation direction

Clean natural dialogue is the default. Keep the approved functional SFX; strong radio voice filtering was rejected for clarity. Background music follows scene tension and ducks under dialogue. Use the latest audio handoff/status to distinguish approved, candidate and missing takes. A local preview is not evidence of in-game playback.

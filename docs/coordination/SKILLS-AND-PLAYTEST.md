# Selected skills and independent game checks

Owner: Codex integrator. Requested by Alex on 14 November 2026. Live acknowledgements and evidence:
[coordination issue #7](https://github.com/alex-slovakia/out-of-reach/issues/7).

## Installed on Alex's computer

These are task instructions, not new Unity packages or proof of quality. Load only the relevant
instructions for the current task; project ownership, engine version and verified results still govern.

| Skill | Source revision | Purpose | Local status |
|---|---|---|---|
| `unity-csharp-scripting` | `gamedev-skills/awesome-gamedev-agent-skills@c6ca6c41c839b68319ffc08430b99ab768debc32`, `skills/unity/unity-csharp-scripting` | Unity lifecycle, serialization, GUID checks and actual compilation evidence | Installed in `~/.codex/skills/`; Apache-2.0 license retained |
| `hard-surface` | `arjun988/blender-skills@904f095d8f36a6e72adaebb08d07f884a074743d`, `.claude/skills/hard-surface` | Plausible mechanical construction, silhouette, bevels and repeated parts | Installed in `~/.codex/skills/`; MIT license retained |
| `unity-export` | Same Blender repository and revision, `.claude/skills/unity-export` | Metre scale, pivots, normals, material and import checks | Installed in `~/.codex/skills/`; MIT license retained |
| Impeccable / Taste Skill | Existing local installations | UI composition, readable typography, restrained motion and review of real captures | Already available; current redesign is using their relevant design guidance |

Each new installation contains `INSTALL-PROVENANCE.json` with the upstream revision and file hashes.
The Blender shared `references/asset-pipeline.md` was included. No agent-service hooks, external model
router, new paid account, Unity package, or asset generator was installed.

Claude should install the same three selected directories in its user skill location, including their
references and upstream license. Keep the sibling `references/asset-pipeline.md` for the Blender skills.
Do not copy all 94 Blender skills into every task's context. A working existing Blender/MCP setup
does not need replacement. If missing, inspect and set up only the required Blender connector on
Apoliak's machine, bound locally; report the actual Blender/add-on version and successful connection.
Codex cannot claim a remote installation without Claude's acknowledgement.

Use the existing verified FBX/Unity import pipeline. A generic GLB recommendation in a skill does not
authorize adding a GLTF package. The model writer remains the sole model writer under CODEX-006.

## Two independent play passes

**Status: scheduled; not yet run on a shared integrated executable.** C02's 129 EditMode and 8 PlayMode
tests are useful automated evidence, but are not an end-to-end playthrough.

1. Codex publishes a candidate with its exact integration commit, Unity version, included scene,
   Windows executable/archive SHA-256, controls, known missing features and local build log.
2. Codex and a Claude tester each run that same executable revision on their respective machines.
   Each first plays without reading the other's bug list. Use desktop controls or an already
   functioning game-control connection; an automated assertion suite alone is not a play pass.
3. First play normally: understand planning, change equipment, launch, hear/see the accident, find
   and repair the two interior systems. Once C03/C04 are integrated, continue through EVA, return,
   delayed communication, ending, report and restart. Mark unavailable stages NOT IMPLEMENTED.
4. Then deliberately probe invalid loadouts, the 60-second expiry with an invalid draft, pause at a
   transition, repeated interactions, walls/doorways, low resources, failure, resolution changes,
   subtitles with sound muted, reduced effects and restart. Follow TESTING.md for release coverage.
5. Capture findings before patching. A finding records steps, expected/actual behavior, frequency,
   severity, screenshot/video or Player.log evidence and exact build. List successful scenarios too.
6. Codex consolidates duplicates and assigns fixes by path ownership. One writer per fix. No model
   or gameplay takeover by a reviewer. Blockers first, then broken/unclear interactions, readability
   and pacing; visual polish follows a playable path.
7. A changed executable is a new candidate. Re-run failed scenarios plus the mission path affected
   by the fix. Only mark a finding fixed after recording evidence from that new candidate.

Two agents on one machine are two observers, not two-machine verification. A desktop-agent play
pass is not a human playtest. Report the observer and method honestly. A build that merely launches
does not pass either outcome. Never claim performance without a measured configuration and sample.

## Disjoint report ownership

- Codex writes `docs/qa/codex/**` and its own #7 comments.
- Claude tester writes `docs/qa/claude/**` on a separate reporting branch, or posts equivalent evidence
  in #7 without changing repository files. This is a report-only path assignment.
- Codex integrator alone writes `docs/qa/triage/**` and updates shared release acceptance.
- Gameplay, visual and shared source ownership is unchanged. Reports never grant merge permission.

Report template:

```text
Candidate / source commit / executable SHA-256:
Observer, machine, Unity/build settings, resolution:
Method: desktop play / automated test / static review (do not conflate)
Scenarios completed; unavailable or untested scenarios:
Finding ID, severity, steps, expected, actual, frequency:
Evidence path or link:
Suggested owner; fix commit; retest candidate/result:
```

## Video-derived choices

The five supplied Instagram reels were retrieved publicly without login and transcribed locally
from their audio. These are automatic transcripts, not manually certified verbatim text. The fifth
reel (`Deqf74ghnPr`) suggests a concise core-loop document, one system per task, Blender-to-Unity
iteration and play → bug list → fix. PRODUCT.md already supplies our loop and scope; this protocol
adds the independent play passes. Its named asset generator is not adopted without checking the
actual asset license and cost. The video does not identify a specific Unity MCP repository.

Keep the useful workflow without adding tooling just to match a reel. The public videos' full
transcripts and audio remain local research, outside Git. Further tools require a concrete benefit
to this slice and verification against their primary documentation.

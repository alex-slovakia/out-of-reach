# V01 — GPT-6 Astra High — modular spacecraft

Use the Codex visual chat in Alex's separate checkout. Select **GPT-6 Astra**, reasoning **High**, if available in the client. This prompt does not change the selected model automatically.

Read AGENTS.md, PRODUCT.md, ARCHITECTURE.md, INTEGRATION.md, ASSETS.md, TESTING.md and actual Contracts. Verify the published baseline, starting SHA and exact Unity **6000.3.28f1**. Work on `feature/visuals-cinematics` only after the main baseline is verified.

Implement **V01 only**. Allowed paths are the six Codex-owned visual/presentation/UI/audio paths in ARCHITECTURE.md. Do not edit gameplay Core/Simulation/Interaction/Player/Inventory/Tests, shared Contracts, Bootstrap, Packages, ProjectSettings or root documentation. Request coordination for a shared change.

Create a compact coherent AURORA-7 interior with cockpit, engineering, storage and airlock, plus a separate visual preview scene. Build original modular geometry/materials first; no large asset imports. Use graphite panels, structural ribs, restrained cyan/amber practical light and clear landmarks. Preserve a usable first-person route, simple colliders, 1 Unity unit = 1 metre, readable doors and service access.

Expose the integration anchors listed in ARCHITECTURE.md. Create independent visual prefabs so the integrator can attach Claude components without both developers editing the same scene. The preview may have its own camera/light setup; document how it is excluded from Bootstrap gameplay. Do not simulate oxygen, repairs, mission success or player authority inside visual scripts. Preview values must be clearly labeled as preview, not a working game.

Inspect the scene in Unity, verify references/compilation and capture representative cockpit/engineering/airlock views where available. Do not claim screenshot inspection or a build that was not performed. Keep geometry/material counts modest; record measured performance only when measured.

Commit only owned files. Hand off exact prefab/scene paths, anchor names/transforms, collider assumptions, camera/listener exclusions, test/inspection evidence and known gaps. Stop at V01 before adding cinematics. Shared TASKS.md updates go through the integrator.

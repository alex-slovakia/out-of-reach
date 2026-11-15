# Asset sheet: partial approval only

Alex approved the props/environment direction but explicitly rejected character cells 01 and 02. Do not model the random coverall character or EVA suit from this new sheet.

Character source of truth remains the previously approved [cabin front/back](../astronaut-reference-v01/cabin-front-back.png) and [EVA front/back](../astronaut-reference-v01/eva-front-back.png). Cabin: male crew member, navy long-sleeve shirt, charcoal cargo trousers, low-profile shoes, bare hands. EVA: the existing off-white layered suit reference with helmet lights, chest controls and life-support pack. Hand/cuff proportions must match these rigs.

The new sheet is a generated reference, not delivered meshes. Props/environment cells are art direction, not authorization to change mission layout, collision dimensions or anchors. Claude owns the requested modeling; exact disjoint asset claims must precede parallel work. Movement remains first priority. Each image-to-3D attempt would need a separate clean asset view; do not feed the whole contact sheet as one object.

## Budget3D feasibility: not adopted for this deadline

Checked 15 November 2026 against the [official repository](https://github.com/BudgetAny/Budget3D) and [Turbo model card](https://huggingface.co/BudgetAny/Budget3D-Idol-Turbo); downloaded model-card text is preserved alongside. Code is MIT; weights are Apache-2.0. DINOv3 and FLUX.2 VAE have separate licenses, so this is not an assertion of complete pipeline license clearance.

Output is 3D Gaussian splats in PLY, not a directly rigged polygon mesh with game collision. Integration would need additional rendering/conversion, mesh cleanup and rigging work. It is therefore unsuitable as an immediate replacement for the game's existing mesh/URP workflow. No model install, weight download, new Unity dependency or generation benchmark was performed.

Publisher benchmarks: RTX PRO 6000 Blackwell Server Edition, batch 8, optimized warmed inference; 0.15–0.48 seconds sampling per object, roughly 3.15–3.64 seconds including Gaussian decode and orbit render. Peak sampling memory 6–8 GB, not just the 1.63 GB denoiser download. These are publisher measurements, not this machine's speed. Local nvidia-smi reports RTX3070 with 8192 MiB VRAM and 4772 MiB free at observation while Unity was running. Do not assume comparable performance or memory headroom.

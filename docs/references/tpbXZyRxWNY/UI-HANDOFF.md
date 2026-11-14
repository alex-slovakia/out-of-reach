# UI reference handoff — for the single Claude/Fable UI writer

Research only, 14 November 2026. Alex transferred all `Assets/_OutOfReach/UI/**` to your single coordinated writer. Root Codex owns composition/camera/audio; this reference chat implemented no UI and did not publish or message anyone. Root can forward this brief plus selected images to private issue #7.

Source: Brendan Jowett, https://www.youtube.com/watch?v=tpbXZyRxWNY. Genuine 640×360 frames; exact timestamps and hashes in frames.json. These are references, not reusable game assets.

**Start with these actual files:**
- `selected/F03-100h-sparse-menu.png` — 15:42.000. Scene occupies most of the frame; one title and short menu column. Borrow hierarchy and breathing room, not its art/title/font assets. A menu is not sufficient evidence for gameplay HUD design.
- `selected/F05-100h-context-prompt.png` — 16:56.400. Small contextual action adjacent to the focused object; no large interaction panel. In our game use the authoritative prompt/availability from InteractionResolver/IInteractable.
- `selected/F01-10h-blackout-to-play.png` — 09:52.000. After blackout, the consequence and direction are readable in the world. Avoid three competing text banners at this handover.
- `selected/F02-10h-visible-body.png` — 10:21.200; `selected/F07-100h-ladder-grip.png` — 20:03.000; `selected/F08-100h-swim-hands.png` — 20:30.000. Keep lower center free for original hands, legs and object contact. Body/ladder/swim presentation does not imply a suit-changing mechanic was shown.

**Concrete AURORA-7 layout proposal for your mock:**
- Upper left: one short current objective, e.g. `Restore power`; avoid duplicate objective prose elsewhere.
- Center: tiny reticle only when useful. Near focus, one action key and authoritative verb; rejected action replaces that prompt briefly with its reason. Do not duplicate the same failure in a toast and banner.
- Lower left: at most two or three compact contextual readings. Cabin: relevant oxygen reserve (kg), battery (kWh), CO2 (ppm) on danger or inspection; EVA: suit reserve/remaining simulation time and return objective, replacing irrelevant cabin detail. Preserve access to every required resource through existing details/planner/report surfaces.
- Lower center: one subtitle area with speaker, at most two lines, clear of hands; arbitrate with transient feedback rather than stacking three text layers. Existing composition produces voice subtitles, cinematic captions and interaction feedback: request a precise integration change from root if source arbitration cannot be resolved inside your scope.
- Communications console: epoch, Earth-distance proxy, one-way delay, phase and reply wait; `TRANSMITTING`, `AWAITING REPLY`, `REPLY RECEIVED` follow authoritative state. Read ISimulationClock for visible acceleration; UI never runs a second countdown.
- On Impact: restrained objective/subtitle, no full-screen diagnostic dashboard. Blackout artwork and emergency overlays remain explicitly coordinated with root cinematic binding; one owner per property.

Use graphite/amber/cyan/red; distinguish state with text/symbols as well as color. White or softly warm primary text, quiet secondary units. Assess real readability at 1280×720 and 1920×1080 rather than inheriting the tiny reference text. The red debug warnings and webcam in the reference are not game UI to copy.

**First reviewable deliverable:** one genuine in-engine screenshot or clearly labeled mock for cabin, one for EVA, plus an Impact subtitle/interaction overlap case. Root will review before integration. Then wire existing authoritative state, preserve pause/restart/report and one interaction action. No new timers, inventory, simulation or competing HUD writer. Suit/body art comes from the coordinated model writer; equipped visuals must read IEvaStatus.IsSuitOn/IsSuitSealed.

Reference limitations: source subtitles returned HTTP 429; no audio analysis. Vehicle entry is a fast camera/black transition, not verified hand/foot mounting animation. Unreal is the creator's visible credit/claim, not a requirement for our Unity project.

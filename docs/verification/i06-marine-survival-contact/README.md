# I06 checkpoint — current native evidence

This release contains **nine unchanged native screenshots**, original source/check manifests and verified process receipts from four Unity **6000.3.28f1** GPU runs. It describes the isolated integration candidate. The original user project has not received this checkpoint.

| Exact run | Result | Verified scope and boundary |
|---|---|---|
| Marine Bootstrap `20261114T211433082Z` | 177 assertions; exit0 | Actual saved scene, real60-second loading deadline, continuous loaded walk, boarding,22-second departure, pause, transfer and restart. Daylight-water V01. |
| Survival V02 `20261114T212340632Z` | 1740 assertions; exit0 | Actual C06 services/HUD, refreshed water bar, real Tab disclosure, received-reply subtitle separation at1280×720 and960×540, request/rest/beacon/pause/restart. Disclosed deterministic time inputs. |
| Micro bridge `20261114T210359557Z` | 398 assertions; exit0 | Real Player/contact presenters and delayed commands in a bounded wall/rail fixture. **Direct dispatch, not physical input/full-game acceptance.** |
| Marine FX native2 `20261114T212656683Z` | 157 assertions; exit0 | Two real departures, actual nozzle/deck anchors, pause/property blocks, disable/clear/rebind, restart/reduced effects and native shader checks. Current water V02 appears in these images. |

All recorded assertions passed. **Assertion totals are not bug counts, independent scenarios or human playthrough counts. No numeric candidate bug count is inferred.** Passing scopes are not combined into a claim that all systems work together.

## Selected actual images

| Native image | Exact state / limitation |
|---|---|
| [Dock and boarding route](runs/marine/01-actual-dock.png) | Actual marine loading scene and open transport route; older daylight-water V01. |
| [Loaded transport cabin](runs/marine/04-cabin-launch-loaded-cases.png) | Real loaded cases inside the craft during departure, practical light and actual caption. |
| [Marine4 exterior](runs/marine/06-actual-exterior-ascent.png) | Actual player-camera shot at approximately18.1324s. Corrected visible exterior is continuous in this view; small exhaust is visible. |
| [V02 water/needs and subtitle —1280×720](runs/survival/01-survival-primary-water-needs.png) | Actual water bar/pictogram and received ground reply. Subtitle is separated from the needs HUD. |
| [V02 layout —960×540](runs/survival/01b-survival-subtitle-960x540.png) | Independently rendered at960×540, not a resized/composited image. |
| [Bare left push](runs/micro/work-Left-Push-session1.png) | Real event/render frame36 after request frame9; one push, measured velocity `(0,0,-0.1)`m/s. Fixture wall view. |
| [EVA left grip](runs/micro/eva-Left-Grip-session1.png) | Real event/render frame951 after request frame904; one grab, actual suited/float/held state, visible registered rail. |
| [Ignition nozzle diagnostic](runs/fx/01-nozzle-ignition-9_8-diagnostic.png) | Explicit temporary diagnostic camera; actual corrected nozzle/deck/FX, not the shipping camera. Initial plume is constrained by the physical deck clearance. |
| [Current exterior and water V02](runs/fx/03-actual-exterior-18_5.png) | Same actual player camera,18.500864s. The prior water grid is absent in this inspected view. Plume geometry reports8m, but the bright visible plume reads short; further visual diagnosis remains open. |

These are native captures of project assets, not concept art. No external reference pictures, crops, retouching or synthetic composites are included. Appearance is evidence to inspect, not a premium-fidelity or physical-realism claim.

## Scope that must travel with the pictures

**Marine:** real Timed/Practice/Restart UI and Input System E/W/Escape. Setup teleports positioned initial cargo probes and the later Aurora console probe; the loaded W path crossed dock/access arm/hatch continuously. Normal runtime Update drove the full deadline and departure. Overlay UGUI fade is excluded from PNGs; its real alpha was checked. Batch focus delivery is disclosed. The former isolated pale lamp quad is absent from the retained dock view; no all-angle watertightness or imported FBX triangle recount is claimed.

**Scientific framing:** the sea-based launch setting has an elevated boarding deck. Its **22-second,90m authored ascent → “later aboard AURORA-7” orbital ship is cinematic compression**. It does not calculate launch trajectory, orbital insertion or rendezvous, or claim orbit is reached in22seconds. The chapter transition omits the intervening flight; the spacecraft is fictional.

**Survival V02:** real services/commands/resource costs with explicitly supplied simulation intervals; automatic ticking was disabled for deterministic setup. Actual departure/Impact/FalseSignal presenters were skipped, and one framing teleport was used. Real bounded rests reached the request boundary without reserve grants or outcome assignment. The beacon action passed while state remained `Survival`, `Outcome=None`; this is not rescue completion. These1740 assertions replace the older1165-assertion UI evidence. Exact quantity behind real Tab and actual subtitle geometry were checked in both retained resolutions.

**Micro bridge:** real suit/repair/launch commands and real Player contact events, but direct bridge dispatch with zero device intent. Setup Teleport/ClearMotion occurred between cases; no result was fabricated. The run rebaked264 bare-skin rendered frames: minimum reported clearance−0.0075mm, maximum correction34.10mm. This establishes bounded hand/plane and wrist-path behavior, not whole-arm collision certification. Bare grip and physical input/lease/full-Bootstrap float entry are not established by this run.

**FX native2:** actual Practice/Planner launch are disclosed setup shortcuts, not cargo/input acceptance. The production22-second playhead was not shortened or replaced. The probe checked pause-held time/property blocks/world poses, exact material state, clear/disable/rebind, reduced restart, stationary deck steam and plume clearance. Diagnostic cameras were removed; no extra production camera/light/listener was created. Shader messages are empty. Root corrected the first run's test-only MaterialPropertyBlock constructor allocation before native2; that startup failure was not a product-FX failure. The short-looking exterior flame remains a visual limitation despite the successful lifecycle checks.

## Exact revision and provenance

Marine4 and Survival V02 saved Bootstrap SHA-256: `a84d66f2ac340a08a0eab6b003db6c17b7b192e8b136766d6fc5d4003c4e5ce5`. Corrected transport FBX: `e21cfd832ffb50a190eb317be848b2da53624987bb6bd9b1152c50eece060e2d`. FX presenter: `190daa3049c1f80ddfe07bbca710fd566864058544a8c60fb4851c3f7eaa29e2`. Every run retains its own original source manifest; later changes are not covered merely because another component is unchanged.

`copied-evidence-manifest.json` pins66 byte-identical source artifacts. `run-receipts.json` connects each PASS marker to the native log hash and original exit0 file. Each run retains its separately recorded known UnityEditor.Search startup exception; these passes do not mean there was no editor-environment exception.

At the read-only original-checkout observation, Bootstrap hash was `c0579a652eed1eade998b9eb93f2a2c5fc4fc633ea64ab50079897bce796107b`; integrated MarinePrologue FBX and FX presenter paths were absent. See `original-checkout-observation.json`. **Do not present the isolated screenshots as the user's currently updated original project.**

`provenance/SOURCE.native-validation.patch` proposes only `/transport_correction/native_validation` wording in MarinePrologue/SOURCE.json. Its base and exact hash are retained. Root owns applying/publishing it. No attribution, GUID, asset hash or unmeasured FBX count is changed. Physical micro-input acceptance and any later flame tuning need their own completed evidence.

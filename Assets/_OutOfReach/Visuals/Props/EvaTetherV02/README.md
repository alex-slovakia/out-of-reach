# EVA tether V02 — the rope the crew climbs hand over hand (EVA-TETHER-01)

Original asset by Claude / Apoliak7777 for OUT OF REACH, 15 Nov 2026. No external source, scan, photo or third-party
asset: the textures come from `Source~/make_rope_textures.py` (procedural numpy), the meshes are built in code
(`Scripts/Presentation/Hands/EvaTetherMeshes.cs`, `EvaTetherArmSleeves.cs`) from the actual route waypoints when the
scene starts.

## What it is

- **Rope:** a 30 mm braided safety line (16-carrier plain braid, off-white sheath, two amber and one graphite tracer).
  - It follows the real EVA route line (the same waypoint transforms as the gameplay `TetheredRoute`) at shoulder height, 1.40 m above the feet line.
  - It sits 0.36 m to the hull side, with mitred corners, so each straight run is exactly parallel to its segment.
  - It sags slightly between anchors (1.4 % of the span, at most 5 cm).
  - Beyond the work site only an 8 cm tail runs to the end anchor; gloves never hold past the route end.
- **Anchors:** at both rope ends, every route corner, and at most 2.2 m apart. Each anchor is:
  - an amber carabiner around the rope,
  - a graphite eye bracket, and a post to the nearest visible surface on the hull side, with a bolted base plate.
  - The installer bakes the post feet against the hull meshes, using temporary mesh colliders that it removes again (12 of 13 on the delivered route). An anchor with no surface in reach gets a short capped stub.
- **Climb:** the gloves hold the rope hand over hand.
  - The leading glove closes on a grip station ("rung", every 0.5 m along the line) and stays fixed on the rope while the body moves.
  - The other glove opens, swings forward and closes on the next rung.
  - Gameplay moves the body only by these pulls (`FirstPersonController.MoveAlongRoute`).
- **Arms:** while climbing, padded upper sleeves and elbow covers join the delivered rigid glove forearms to the suit shoulders (the actual EVA `upperarm01.L/R` bones when the scene has them).
- **Presentation only:** no collider and no physics. The old amber route guide line is turned off by the installer.

## Files

| Path | Purpose |
|---|---|
| `Textures/EvaTetherV02_Rope_BaseColor.png` | sheath colour, sRGB, 512², one tile = once round × 94 mm along |
| `Textures/EvaTetherV02_Rope_Normal.png` | tangent-space normal map of the braid (import type Normal map) |
| `Materials/EvaTetherV02_Rope.mat` | URP Lit, base + normal, smoothness 0.3 |
| `Materials/EvaTetherV02_Bracket.mat` | URP Lit graphite metal (metallic 0.8, smoothness 0.5) |
| `Materials/EvaTetherV02_Clip.mat` | URP Lit anodized amber (metallic 0.55, smoothness 0.62) |
| `Materials/EvaTetherV02_Sleeve.mat` | URP Lit matte off-white suit cloth for the upper sleeves |
| `Source~/make_rope_textures.py` | regenerates both textures (`python -I make_rope_textures.py`) |

## Integration (root)

The installer is idempotent, edits only the scene that holds the route and hands, and leaves the route, waypoints,
gates, player and mission composition unchanged:

```csharp
// I01BootstrapBuilder, after ValidateAntennaApproach(route, player) (and after the tool sleeve exists):
EvaTetherAuthoring.Install(route, hands);
```

The same thing on the saved scene:
- **Menu:** **OUT OF REACH / EVA Tether V02 / Install into open scene**, then save.
- **Batch:** `-executeMethod OutOfReach.Composition.Editor.EvaTetherAuthoring.InstallIntoBootstrapBatch` opens, installs and saves `Assets/_OutOfReach/Scenes/Bootstrap.unity`.

It adds:
- the child `EVA tether rope V02` under the route object (`EvaTetherRope`, with the baked bracket feet);
- the child `EVA tether arm sleeves V02` under the hands presenter (`EvaTetherArmSleeves`);
- the `tetherRope` reference on `AstronautHandsPresenter`;
- and it disables the old route guide `LineRenderer`.

The climb tuning lives on the route (`TetheredRoute`: stroke 0.5 m, reach 0.14–0.64 m, surge 0.3, grip ramp 0.18 s,
brake 0.24 s). The rope and anchor placement lives on `EvaTetherRope`. The installer copies the stroke lattice from the
route to the rope.

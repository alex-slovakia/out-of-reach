# Launch landscape V02 — integration handoff

Task: SURFACE-02. Branch: codex/i01-integration; uncommitted worker files. Existing photographic soil/gravel and six placed rock props are retained. The active ground screenshot had a flat skyline and nearly bare margins; old coarse scrub was disabled by the preceding review.

## Delivered

- `Assets/_OutOfReach/Scripts/Presentation/LaunchLandscapeV02/Editor/LaunchLandscapeAuthoring.cs`: independent editor-only deterministic authoring.
- `Assets/_OutOfReach/Visuals/LaunchLandscapeV02/LandscapeSurface.shader`: URP PBR, vertex-colored opaque surface, shadow receiving/casting, depth and depth-normal passes, existing atmospheric fog.
- Unique metadata for all new files and folders. No changes to existing files, gameplay colliders, materials, settings, packages or Bootstrap.

The tool creates eight contiguous low-hill mesh sectors, 55,296 triangles total, with two irregular eroded ridge profiles. Height amplitude differs strongly around the site to avoid an enclosing crater silhouette. Adjacent sectors use matching derivative normals. Foot and outer skirts sit below existing terrain; the generated relief starts 1,120 metres from the launch site and ends at 4,750 metres. Maximum allowed local elevation is 180 metres, with a -49 m skirt relative to the existing -36.6 m ground datum. This is fictional landscape, not a NASA elevation dataset.

Dry grass and small sage scrub grow in irregular clusters on the ACTUAL active terrain mesh triangles. A spatial index samples upward triangles; no temporary or permanent collider is created. Blade ribbons have curved tips and darker roots; scrub has individual branching stems and leaves, not opaque hemispheres. The tool excludes the apron, crawler/facility corridor and flame-trench/service outlet with a 9 m margin. Seed 150026. Detail geometry is capped at 190,000 triangles and at most 24 vegetation renderers; actual counts depend on source terrain and are logged at application.

Reuses already imported ambientCG Ground110 color and normal textures. License/source/hash evidence remains `docs/art-direction/ground-finish-20261115/source-manifest.json`. No downloads, new dependencies, copied game assets or generated image textures. Geometry/shader code are original project work.

## Root integration

After the current ground-finish authoring, call in Edit mode:

```csharp
GameObject landscape = OutOfReach.Presentation.LaunchLandscape.Editor.LaunchLandscapeAuthoring.Apply(site);
Debug.Log(OutOfReach.Presentation.LaunchLandscape.Editor.LaunchLandscapeAuthoring.LastReport);
AssetDatabase.SaveAssets();
// Root then validates and saves its currently open Bootstrap scene.
```

`site` must be the actual stationary `Launch site - stationary` Transform. The active `Coastal relief and launch pad detail` group must already exist. This function does not open or save a scene. The independent `OutOfReach.LaunchLandscape.Editor` assembly has no project assembly dependency; a compiled caller asmdef needs an explicit reference to it. A temporary Assembly-CSharp-Editor runner can call it directly.

Generated assets are persisted ONLY under `Visuals/LaunchLandscapeV02/Generated/`. The authoring replaces only its own named child group on successful validation. Existing terrain/ground shader/props remain untouched. Include the generated assets and metadata in the final commit after native acceptance.

`LastReport` is populated only after actual authoring. Its fields: ridgeTriangles, detailTriangles, grassTussocks, scrubPlants, renderers, collidersChanged=0, protectedFootprintPlants=0, seed. Root should copy the measured report here with native evidence rather than treating predicted triangle counts as execution evidence.

## Verification and remaining gates

- Reference C# compile against Unity 6000.3.28f1 assemblies: PASS after final asset edits. Reproducer `.local/launch-landscape-v02/compile-reference.ps1`, output DLL and response file alongside. This is not native Unity import or shader compilation.
- Asset metadata/project checker was launched; result is recorded separately when complete.
- No Unity process, scene save, native render or gameplay test was run by this worker. Root is currently running the isolated editor and owns those checks.
- Native authoring must validate actual plant counts, unchanged colliders, shader compilation and saved asset references. Inspect from loading deck, ground detail view and exterior ascent; confirm no obvious repetition, visible seams, floating vegetation or route obstruction.
- Existing `ApplyMarineEnvironment` sets far clip 10,500 m (sufficient), sunlight 28-degree pitch and exponential-squared fog density .00045. Distant ridge contrast must be assessed with that actual environment; no lighting was changed by this worker. Outer ridges may intentionally be very hazy.
- No LOD system or performance claim. Maximum 32 added mesh renderers, ~245k triangles before terrain-based rejections. Measure draw-call/GPU cost in the actual scene before release.
- Approved concept also has delicate cloud wisps and softer long shadows; those need root lighting/sky direction, not invented terrain assets. No sea or road was added.

All worker Assets frozen after final reference compile for root native verification.

Static metadata check completed: 2,631 files checked, 0 errors, 47 existing warnings outside owned paths, 0 LaunchLandscapeV02 warnings. Process exit 0. Compact evidence: static-checks.json.

Visual correction after actual native review: repeated angular channels produced a fluted/sausage-like ridge profile. RidgeHeight now uses broad asymmetric low shoulders and world-space erosion noise; vertex strata colors are quieter. Shared edge normals and triangle count remain unchanged. Reference compile PASS. This changes generated landscape shape only after root reapplies; it does not move or remove any existing site buildings, huts, ship or platform.

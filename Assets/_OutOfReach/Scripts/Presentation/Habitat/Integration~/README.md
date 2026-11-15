# R36 stored ration portions: integration hook

`HabitatStatusPresenter.ConfigureResources(IResourceSystem)` gives the habitat presenter a read-only view of
Core's `FoodKg`. Nothing calls it yet, so the stored-portion row stays hidden until the integrator applies one
two-line change to the shared composition file `Composition/Runtime/I06HabitatBindings.cs`.

## Apply (sole composition integrator)

```bash
git apply --3way Assets/_OutOfReach/Scripts/Presentation/Habitat/Integration~/r36-habitat-resources.patch
```

If the context has drifted, make the same edit by hand. In `I06HabitatBindings.ConfigureSession`, directly after
`if (status) status.ConfigureSession(survival, recovery);`, add:

```csharp
if (status) status.ConfigureResources(runtime.Resources);
```

The order matters: `ConfigureSession` clears the resource view.

No scene or prefab change is needed. At runtime the presenter clones the meal presentation's own
`GC_MealPouch_Sealed` mesh and materials, after `MealPresenter` instantiates its pouch under the habitat root. It
then places eight pouches at y 0.42 on the starboard blanket bay (x 2.3265, z 13.25), below the existing
RATIONS · MEALS / DRINKS cords.

## What it shows

- Each pouch stands for an equal share of the largest `FoodKg` seen since binding, which is the launch stores.
- Pouches disappear one by one as Core's `FoodKg` falls, whether from a meal, rest or the routine.
- The last pouch disappears only when the store is empty.
- The mapping is `HabitatStoredRations.VisiblePortions`. It reads `FoodKg` and nothing else: no consumption,
  no prediction.

## Verified / not verified

- **Verified:** Unity 6000.3.28f1 Roslyn compile of `OutOfReach.Presentation.Habitat`, and of
  `OutOfReach.Composition` with this patch applied against that build. Both exit 0.
- **Not verified:** Editor import, Play mode or visual placement. Confirm in Play mode that the row sits flush
  on the quilt below the ration cords.

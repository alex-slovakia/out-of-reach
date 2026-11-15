"""MEAL-PRES-01 label variants: the GALLEY-PROPS-01 atlas redrawn with the original generator (gc_textures.ration) and
only the meal_front text and band changed, so the same pouch mesh can show its preparation honestly. Same layout, same
UVs, same fonts (Inter from the Blender install, pixels only).
Needs the kit's a7_textures.py and gc_textures.py beside this file (copy them unchanged from
feature/galley-consumables-v1@12100dd docs/art-direction/galley-consumables-v01/source/; they are not duplicated here).
Run: python -I gc_label_variants.py <out dir>
"""
import os
import sys

sys.dont_write_bytecode = True
here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)
out = os.path.abspath(sys.argv[1])
os.makedirs(out, exist_ok=True)
sys.argv = [sys.argv[0], out]
import a7_textures as tx  # noqa: E402
import gc_textures as gc  # noqa: E402  (module only defines helpers; build() runs under __main__)

VARIANTS = {
    "GC_Labels_Rehydratable": ("MEAL 11", "RICE AND BEANS", "REHYDRATABLE · ADD 120 ml HOT", "LOT 26-034-11 · NET 64 g DRY", gc.AMBER),
    "GC_Labels_ReadyToEat": ("SNACK 03", "FRUIT AND NUT MIX", "READY TO EAT · NO PREPARATION", "LOT 26-027-03 · NET 85 g", (92, 112, 70, 255)),
}
for name, (title, line1, line2, code, band) in VARIANTS.items():
    atlas = tx.Atlas(1024, gc.PPM)
    atlas.add("meal_front", 0.100, 0.072, gc.ration(title, line1, line2, code, band))
    atlas.add("meal_cut", 0.110, 0.010, gc.cut_line)
    atlas.add("drink_front", 0.078, 0.064, gc.ration("DRINK 07", "CITRUS", "ADD 240 ml AMB", "LOT 26-029-07", gc.CYAN))
    atlas.add("fill", 0.026, 0.009, tx.plate(gc.IVORY, gc.INK, "FILL", "Bold", None, 0.25, 0.70))
    atlas.add("velcro", 0.040, 0.028, tx.velcro((205, 202, 192, 255)))
    atlas.add("scale_100", 0.100, 0.012, gc.scale_bar)
    atlas.save(name)
    print("wrote", name)

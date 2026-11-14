"""Re-import a terminal FBX into an empty Blender scene and report what a consumer receives.

    blender -b --factory-startup --python verify_terminal_fbx.py -- <file.fbx> <report.json>
Positions are given in Unity metres (Unity = (-x, z, -y) of Blender), as in the TERM-V1 handoff."""
import json, re, sys
import bpy
from mathutils import Vector
fbx, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
def unity(v):
    return [round(-v.x, 6), round(v.z, 6), round(-v.y, 6)]
dg = bpy.context.evaluated_depsgraph_get()
objs, tris, lo, hi = {}, 0, [9.0] * 3, [-9.0] * 3
for o in bpy.context.scene.objects:
    e = {"type": o.type, "parent": o.parent.name if o.parent else None, "location_unity": unity(o.matrix_world.translation),
         "scale": [round(s, 6) for s in o.matrix_world.to_scale()]}
    if o.type == "MESH":
        m = o.evaluated_get(dg).to_mesh()
        m.calc_loop_triangles()
        e["triangles"] = len(m.loop_triangles)
        e["uv_layers"] = [layer.name for layer in m.uv_layers]
        e["materials"] = [s.material.name if s.material else None for s in o.material_slots]
        tris += len(m.loop_triangles)
        for v in m.vertices:
            p = unity(o.matrix_world @ v.co)
            for i in range(3):
                lo[i] = min(lo[i], p[i]); hi[i] = max(hi[i], p[i])
        if o.name == "ActiveDisplay":
            pts = [unity(o.matrix_world @ v.co) for v in m.vertices]
            normal = (o.matrix_world.to_3x3() @ m.polygons[0].normal).normalized()
            uv = m.uv_layers.active.data
            e["display"] = {"corners_unity": pts, "normal_unity": unity(normal),
                            "size": [round(max(p[0] for p in pts) - min(p[0] for p in pts), 6), round(max(p[1] for p in pts) - min(p[1] for p in pts), 6)],
                            "uv_by_corner": [[list(map(lambda c: round(c, 4), uv[l.index].uv)), unity(o.matrix_world @ m.vertices[l.vertex_index].co)] for l in m.loops]}
        o.evaluated_get(dg).to_mesh_clear()
    objs[o.name] = e
raw = open(fbx, "rb").read()
paths = sorted(set(re.findall(rb"[A-Za-z]:[\/][ -~]{3,}", raw)))
report = {"file": fbx.replace("\\", "/").split("/")[-1], "objects": objs, "triangles": tris,
          "bounds_unity": {"min": lo, "max": hi}, "drive_letter_paths": [p.decode("ascii", "replace") for p in paths][:10],
          "cameras_lights": [o.name for o in bpy.context.scene.objects if o.type in ("CAMERA", "LIGHT")]}
json.dump(report, open(out, "w"), indent=1)
print("VERIFY_DONE", report["file"], "objects", len(objs), "tris", tris, "paths", len(paths))

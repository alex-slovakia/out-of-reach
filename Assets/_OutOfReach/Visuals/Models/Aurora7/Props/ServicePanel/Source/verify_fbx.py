"""Round-trip check: import the exported FBX into an empty scene and report what a consumer receives.

    blender -b --factory-startup --python verify_fbx.py -- ServicePanel.fbx
"""
import json
import os
import sys

import bpy
from mathutils import Vector

path = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
importer = "wm.fbx_import"
try:
    bpy.ops.wm.fbx_import(filepath=path)
except (AttributeError, RuntimeError):
    importer = "import_scene.fbx"
    bpy.ops.import_scene.fbx(filepath=path)

lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
objects = {}
for obj in sorted(bpy.data.objects, key=lambda o: o.name):
    if obj.type != "MESH":
        continue
    mesh = obj.data
    mesh.calc_loop_triangles()
    for vertex in mesh.vertices:
        p = obj.matrix_world @ vertex.co
        lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
        hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
    objects[obj.name] = {
        "triangles": len(mesh.loop_triangles),
        "polygons_not_triangles": sum(1 for p in mesh.polygons if len(p.vertices) != 3),
        "location": [round(v, 4) for v in obj.location],
        "rotation_deg": [round(v * 57.29578, 2) for v in obj.rotation_euler],
        "scale": [round(v, 4) for v in obj.scale],
        "materials": [m.name if m else None for m in mesh.materials],
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "has_custom_normals": bool(getattr(mesh, "has_custom_normals", False)),
        "parent": obj.parent.name if obj.parent else None,
    }

images = sorted({node.image.filepath for mat in bpy.data.materials if mat.node_tree for node in mat.node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image})
print("FBX_REPORT " + json.dumps({
    "file": os.path.basename(path),
    "bytes": os.path.getsize(path),
    "importer": importer,
    "blender": bpy.app.version_string,
    "size_m": {"x": round(hi.x - lo.x, 4), "y": round(hi.y - lo.y, 4), "z": round(hi.z - lo.z, 4)},
    "bounds_min": [round(v, 4) for v in lo],
    "bounds_max": [round(v, 4) for v in hi],
    "triangles_total": sum(o["triangles"] for o in objects.values()),
    "objects": objects,
    "texture_references": images,
}, indent=1))

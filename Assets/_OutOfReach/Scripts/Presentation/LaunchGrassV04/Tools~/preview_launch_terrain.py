"""LAUNCH-TERRAIN-V04: offline preview of the generated landscape (no Unity needed).

    blender -b -P preview_launch_terrain.py -- <terrain.bin> <output prefix>

<terrain.bin> is the dump written by the offline harness that runs LaunchTerrainV04Builder.Build outside Unity:
int32 part count, then per part int32 vertex count, int32 index count, float32 xyz positions, float32 xyz normals,
int32 indices (Unity site-local coordinates, Y up).
Colours are an approximation of the ground shader's rules (grass / scrub / rock by height and slope, baked sun
light and aerial haze as seen from the boarding platform). It shows landforms and composition, not the final look.
"""
import math
import sys

import bpy
import numpy as np
from mathutils import Vector

DATUM = -36.6
EYE = np.array([30.0, -7.6, -20.0])                 # boarding platform, 25 m above the pad (site-local)
SUN = np.array([0.672, 0.309, -0.605])              # towards the sun (launch environment: Euler(18, -48, 0))


def load(path):
    raw = open(path, 'rb').read()
    off = 0
    parts = int(np.frombuffer(raw, '<i4', 1, off)[0]); off += 4
    pos, nrm, tri = [], [], []
    base = 0
    for _ in range(parts):
        nv, ni = np.frombuffer(raw, '<i4', 2, off); off += 8
        p = np.frombuffer(raw, '<f4', nv * 3, off).reshape(nv, 3); off += nv * 12
        n = np.frombuffer(raw, '<f4', nv * 3, off).reshape(nv, 3); off += nv * 12
        t = np.frombuffer(raw, '<i4', ni, off).reshape(-1, 3); off += ni * 4
        pos.append(p); nrm.append(n); tri.append(t + base); base += nv
    return np.concatenate(pos), np.concatenate(nrm), np.concatenate(tri)


def smooth(lo, hi, v):
    t = np.clip((v - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def vnoise(x, z):
    ix = np.floor(x); iz = np.floor(z); fx = x - ix; fz = z - iz
    fx = fx * fx * (3 - 2 * fx); fz = fz * fz * (3 - 2 * fz)

    def h(a, b):
        v = np.sin(a * 127.1 + b * 311.7) * 43758.5453
        return v - np.floor(v)
    return (h(ix, iz) * (1 - fx) + h(ix + 1, iz) * fx) * (1 - fz) + (h(ix, iz + 1) * (1 - fx) + h(ix + 1, iz + 1) * fx) * fz


def colours(pos, nrm, haze):
    h = pos[:, 1] - DATUM
    n = nrm / np.linalg.norm(nrm, axis=1, keepdims=True)
    slope = np.clip(1 - n[:, 1], 0, 1)
    broad = (vnoise(pos[:, 0] / 820, pos[:, 2] / 820) + vnoise(pos[:, 0] / 190 + 5, pos[:, 2] / 190 + 3) * .5) / 1.5
    swale = np.clip((2.5 - h) * .22, 0, 1)
    green = smooth(.34, .66, broad + swale * .25)
    dry, olive, lush = np.array([.63, .56, .36]), np.array([.47, .49, .28]), np.array([.37, .42, .22])
    grass = dry + (olive - dry) * green[:, None]
    grass = grass + (lush - grass) * (smooth(.6, 1, green) * np.clip(.4 + swale, 0, 1) * .6)[:, None]
    relief = smooth(35, 240, h)
    scrub_w = np.clip(smooth(.05, .26, slope) * .8 + relief * .45, 0, 1)
    rock_w = np.clip(smooth(.24, .55, slope) + smooth(420, 900, h) * .7, 0, 1)
    strata = vnoise(pos[:, 0] * .0019 + pos[:, 2] * .0011 + 3, h * .05)
    rock = np.array([.39, .33, .29]) + (np.array([.62, .52, .42]) - np.array([.39, .33, .29])) * smooth(.2, .8, strata)[:, None]
    scrub = np.array([.33, .34, .22])
    alb = grass + (scrub - grass) * (scrub_w * (1 - rock_w))[:, None]
    alb = alb + (rock - alb) * rock_w[:, None]
    soft = (.4 * (1 - smooth(25, 120, h)))[:, None]
    ns = n * (1 - soft) + np.array([0, 1, 0]) * soft
    ns /= np.linalg.norm(ns, axis=1, keepdims=True)
    ndl = np.clip(ns @ SUN, 0, 1)
    lit = alb * (np.array([1, .87, .69]) * 2.1 * ndl[:, None] + np.array([.36, .42, .48]))
    lit = lit * .62 / (1 + lit * .62) * 1.75                         # soft shoulder instead of a tone mapper
    if haze:
        d = np.linalg.norm(pos - EYE, axis=1)
        view = (pos - EYE) / np.maximum(d, 1)[:, None]
        f1 = 1 - np.exp(-(d * .00009) ** 2)
        k = np.maximum(h, 1) / 500.0
        f2 = (1 - np.exp(-.16e-3 * np.maximum(d - 70, 0) * (1 - np.exp(-k)) / k)) * .88 * .85
        f = 1 - (1 - f1) * (1 - f2)
        sunward = np.clip(view @ SUN * .5 + .5, 0, 1) ** 4
        hz = (np.array([.80, .79, .76]) + (np.array([1.0, .86, .66]) - np.array([.80, .79, .76])) * sunward[:, None]) * .95
        lit = lit + (hz - lit) * f[:, None]
    return np.concatenate([np.clip(lit, 0, 1), np.ones((len(pos), 1))], axis=1).astype(np.float32)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    pos, nrm, tri = load(args[0])
    prefix = args[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh = bpy.data.meshes.new("LaunchTerrainV04")
    mesh.vertices.add(len(pos)); mesh.loops.add(tri.size); mesh.polygons.add(len(tri))
    co = np.stack([pos[:, 0], pos[:, 2], pos[:, 1]], axis=1).astype(np.float32)        # Unity (x, y, z) -> Blender (x, z, y)
    mesh.vertices.foreach_set("co", co.ravel())
    mesh.loops.foreach_set("vertex_index", tri.ravel().astype(np.int32))
    mesh.polygons.foreach_set("loop_start", np.arange(0, tri.size, 3, dtype=np.int32))
    mesh.polygons.foreach_set("loop_total", np.full(len(tri), 3, dtype=np.int32))
    mesh.update()
    for name, haze in (("Platform", True), ("Clear", False)):
        attr = mesh.color_attributes.new(name=name, type='FLOAT_COLOR', domain='POINT')
        attr.data.foreach_set("color", colours(pos, nrm, haze).ravel())
    obj = bpy.data.objects.new("LaunchTerrainV04", mesh)
    scene = bpy.context.scene
    scene.collection.objects.link(obj)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'FLAT'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_backface_culling = False
    scene.view_settings.view_transform = 'Standard'
    scene.world = bpy.data.worlds.new("Sky")
    scene.world.color = (.50, .62, .80)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.image_settings.file_format = 'JPEG'; scene.render.image_settings.quality = 88
    cam_data = bpy.data.cameras.new("Preview"); cam_data.clip_start = 1; cam_data.clip_end = 40000
    cam_data.sensor_fit = 'VERTICAL'; cam_data.angle_y = math.radians(55)
    cam = bpy.data.objects.new("Preview", cam_data); scene.collection.objects.link(cam); scene.camera = cam

    def shot(name, eye, target, attribute, ortho=None):
        mesh.color_attributes.active_color = mesh.color_attributes[attribute]
        mesh.color_attributes.render_color_index = mesh.color_attributes.find(attribute)
        cam.location = (eye[0], eye[2], eye[1])
        look = Vector((target[0] - eye[0], target[2] - eye[2], target[1] - eye[1]))
        cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
        cam_data.type = 'ORTHO' if ortho else 'PERSP'
        if ortho: cam_data.ortho_scale = ortho
        scene.render.filepath = prefix + name + ".jpg"
        bpy.ops.render.render(write_still=True)

    e = EYE
    sun_h = np.array([SUN[0], 0, SUN[2]]) / np.hypot(SUN[0], SUN[2])
    shot("platform-towards-sun", e, e + sun_h * 1000 + np.array([0, -30, 0]), "Platform")
    shot("platform-east-river", e, e + np.array([1000, -40, 150]), "Platform")
    shot("platform-north", e, e + np.array([150, -30, 1000]), "Platform")
    shot("platform-west-massif", e, e + np.array([-1000, -20, 100]), "Platform")
    shot("platform-south-crawlerway", e, e + np.array([0, -40, -1000]), "Platform")
    shot("ascent-600m-looking-down-east", np.array([0, 560, 0]), np.array([1800, -36, 600]), "Clear")
    shot("aerial-oblique", np.array([-3200, 2300, -3800]), np.array([700, -36, 900]), "Clear")
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1200
    shot("map-8km", np.array([0, 9000, 5]), np.array([0, 0, 5.01]), "Clear", ortho=8000)
    shot("map-21km", np.array([0, 9000, 5]), np.array([0, 0, 5.01]), "Clear", ortho=21000)


main()

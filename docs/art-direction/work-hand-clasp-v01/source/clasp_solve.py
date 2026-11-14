"""clasp_solve - WORK-HAND-CLASP-01 (Blender 5.3): finger-local Clasp poses for the bare hands of the published WORK
body (A7_BodyFit02_WORK.fbx at 85b0994) against the GALLEY-PROPS-01 meal and drink pouches, with the whole arm skin
measured against each pouch through the closing and the hold.

The body file is read only: bone lengths, bind pose and meshes stay as published. Only finger bones get local
rotations (the wrist and every arm bone stay at bind), so the clasp is independent of the arm pose root chooses.

Grip: the thumb pad presses the pouch's front (label) face opposite the index and middle pads on its back face; the
aperture between the pads is the pouch's measured thickness at the contact. Ring and little follow the middle
finger's flexion behind the pouch.

Method (Blender armature space = the published bind pose):
  1. Flexion axes from the rest geometry: palm normal n from the index direction and the knuckle line (sign: the
     thumb lies on the palm side); each finger bone bends about unit(cross(bone dir, n)). The thumb's MP and IP bend
     in the plane in which the published thumb already curves (that plane points at the thumb's pulp: checked
     against the flat nail side of the skin, 0.996).
  2. Pads: on each distal segment the skin vertex furthest along its pad normal (30-80 % of the segment).
  3. Nelder-Mead over 9 finger-local angles (index and middle MCP, PIP with DIP = PIP / 2; thumb CMC rotation
     vector, MP, IP) puts the thumb pad opposite the index-middle pads at the aperture, pads facing each other.
  4. The pouch frame follows from the pads (z from the finger pads to the thumb pad, x along the distal fingers,
     y = the index side); the aperture target is corrected with the true skin gaps on the evaluated mesh.
  5. Whole-skin checks: world-space BVH of the evaluated arm mesh (every vertex, sleeve included) against every
     pouch variant; if anything intersects, the clasp point slides towards the hand-side edge in 5 mm steps.

Run: blender -b --factory-startup --python clasp_solve.py -- --fbx <A7_BodyFit02_WORK.fbx> --gc <folder with
     textures/ holding the pouch and ship textures> --out <dir> [--diag R,L | --final [--render --samples 48]]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import build_habitat as bh  # noqa: E402
import gc_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


FBX = os.path.abspath(arg("--fbx"))
GC = os.path.abspath(arg("--gc"))
OUT = os.path.abspath(arg("--out"))
RENDER = bool(arg("--render", False))
SAMPLES = int(arg("--samples", 48))
EPS = 0.0005                     # target skin-to-pouch gap at a contact
PIP_DEG = float(arg("--pip", 0.0))   # flattened fingers: PIP and DIP may extend slightly (negative)
DIP_DEG = float(arg("--dip", 0.0))
TILT_DEG = float(arg("--tilt", 0.0))   # pouch tilt about its y axis, far side away from the fingertips
DIGITS = {"thumb": ("finger1-1", "finger1-2", "finger1-3"), "index": ("finger2-1", "finger2-2", "finger2-3"),
          "middle": ("finger3-1", "finger3-2", "finger3-3"), "ring": ("finger4-1", "finger4-2", "finger4-3"),
          "little": ("finger5-1", "finger5-2", "finger5-3")}
POUCHES = {"drink": ("GC_DrinkPouch", "DrinkGrip", ("GC_DrinkPouch_Closed", "GC_DrinkPouch_Open")),
           "meal": ("GC_MealPouch", "MealGrip", ("GC_MealPouch_Opened", "GC_MealPouch_Sealed"))}
NOMINAL = np.radians([45.0, 50.0, 45.0, 50.0, 0.0, 0.0, 0.0, 20.0, 20.0])   # i_mcp i_pip m_mcp m_pip cmc(3) mp ip
LIMITS = (np.radians([0, 0, 0, 0, -180, -180, -180, -10, -10]), np.radians([75, 90, 75, 90, 180, 180, 180, 60, 80]))


def unit(v):
    v = np.asarray(v, dtype=float)
    return v / np.linalg.norm(v)


def rotvec(v):
    """3x3 rotation from a rotation vector (axis * angle)."""
    a = float(np.linalg.norm(v))
    if a < 1e-12:
        return np.eye(3)
    k = np.asarray(v) / a
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(a) * K + (1 - math.cos(a)) * K @ K


def quat(R):
    """(w, x, y, z) of a 3x3 rotation, w >= 0."""
    w = math.sqrt(max(0.0, 1.0 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
    x = math.copysign(math.sqrt(max(0.0, 1.0 + R[0, 0] - R[1, 1] - R[2, 2])) / 2, R[2, 1] - R[1, 2])
    y = math.copysign(math.sqrt(max(0.0, 1.0 - R[0, 0] + R[1, 1] - R[2, 2])) / 2, R[0, 2] - R[2, 0])
    z = math.copysign(math.sqrt(max(0.0, 1.0 - R[0, 0] - R[1, 1] + R[2, 2])) / 2, R[1, 0] - R[0, 1])
    q = np.array([w, x, y, z])
    return q / np.linalg.norm(q)


def nelder_mead(f, x0, step, iters=1500, tol=1e-12):
    pts = [np.array(x0, dtype=float)]
    for i in range(len(x0)):
        p = np.array(x0, dtype=float)
        p[i] += step[i]
        pts.append(p)
    vals = [f(p) for p in pts]
    for _ in range(iters):
        order = np.argsort(vals)
        pts, vals = [pts[i] for i in order], [vals[i] for i in order]
        if abs(vals[-1] - vals[0]) < tol:
            break
        c = np.mean(pts[:-1], axis=0)
        xr = c + (c - pts[-1])
        fr = f(xr)
        if fr < vals[0]:
            xe = c + 2 * (c - pts[-1])
            fe = f(xe)
            pts[-1], vals[-1] = (xe, fe) if fe < fr else (xr, fr)
        elif fr < vals[-2]:
            pts[-1], vals[-1] = xr, fr
        else:
            xc = c + 0.5 * (pts[-1] - c)
            fc = f(xc)
            if fc < vals[-1]:
                pts[-1], vals[-1] = xc, fc
            else:
                for i in range(1, len(pts)):
                    pts[i] = pts[0] + 0.5 * (pts[i] - pts[0])
                    vals[i] = f(pts[i])
    i = int(np.argmin(vals))
    return pts[i], vals[i]


# ----------------------------------------------------------------------------- body
class Body:
    def __init__(self):
        bh.clear_scene()
        bpy.ops.import_scene.fbx(filepath=FBX)
        self.rig = bpy.data.objects["A7_ContinuousBody_Rig"]
        self.rig.animation_data_clear()
        self.arms = {"R": bpy.data.objects["A7_WORK_Arm_R"], "L": bpy.data.objects["A7_WORK_Arm_L"]}
        self.torso = bpy.data.objects["A7_WORK_TorsoLegs"]
        for pb in self.rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)
        bpy.context.view_layer.update()
        self.rest = {b.name: np.array(b.matrix_local) for b in self.rig.data.bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in self.rig.data.bones}
        self.length = {b.name: b.length for b in self.rig.data.bones}
        self.region, self.tris = {}, {}
        for side, obj in self.arms.items():
            names = [g.name for g in obj.vertex_groups]
            self.region[side] = np.array([names[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""
                                          for v in obj.data.vertices])
            obj.data.calc_loop_triangles()
            t = np.zeros(len(obj.data.loop_triangles) * 3, dtype=np.int32)
            obj.data.loop_triangles.foreach_get("vertices", t)
            self.tris[side] = t.reshape(-1, 3)

    def path(self, bone):
        chain = []
        while bone:
            chain.append(bone)
            bone = self.parent[bone]
        return "/".join([self.rig.name] + list(reversed(chain)))

    def frame(self, side):
        s = "." + side
        R = self.rest
        d = unit(R["finger2-1" + s][:3, 3] - R["wrist" + s][:3, 3])
        across = unit(R["finger5-1" + s][:3, 3] - R["finger2-1" + s][:3, 3])
        n = unit(np.cross(d, across))
        tip = lambda b: R[b + s][:3, 3] + R[b + s][:3, 1] * self.length[b + s]
        if np.dot(tip("finger1-3") - tip("finger2-3"), n) < 0:
            n = -n
        d1, d3 = R["finger1-1" + s][:3, 1], R["finger1-3" + s][:3, 1]
        bend = unit((d3 - d1) - np.dot(d3 - d1, d3) * d3)
        return n, bend

    def flex_local(self, bone, normal):
        """Unit axis in the bone's rest frame about which a positive angle bends the bone towards `normal`."""
        B = self.rest[bone][:3, :3]
        return B.T @ unit(np.cross(B[:, 1], normal))

    def fk(self, deltas, names):
        """Posed armature-space 4x4 of the given bones; deltas = {bone: 3x3 local rotation}."""
        posed = {}

        def get(b):
            if b in posed:
                return posed[b]
            D = np.eye(4)
            if b in deltas:
                D[:3, :3] = deltas[b]
            p = self.parent[b]
            m = self.rest[b] @ D if p is None else get(p) @ np.linalg.inv(self.rest[p]) @ self.rest[b] @ D
            posed[b] = m
            return m
        return {b: get(b) for b in names}

    def apply(self, deltas):
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = quat(deltas[pb.name]) if pb.name in deltas else (1, 0, 0, 0)
        bpy.context.view_layer.update()

    def skin(self, side):
        """Evaluated arm vertices (armature = world space: the rig object sits at identity)."""
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.arms[side].evaluated_get(dg)
        me = ev.to_mesh()
        co = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        ev.to_mesh_clear()
        return co.reshape(-1, 3)


# ----------------------------------------------------------------------------- pouches
def build_pouches():
    tex = os.path.join(GC, "textures")
    atlas = json.load(open(os.path.join(tex, "gc_labels.json"), encoding="utf-8"))
    specs = {}
    for f in ("hb_materials.json", "gc_materials.json"):
        specs.update({e["name"]: e for e in json.load(open(os.path.join(HERE, f), encoding="utf-8"))["materials"]})
    kit = gc_kit.build_kit(atlas)
    materials = {n: bh.build_material(n, s, tex) for n, s in specs.items()}
    objs, local = {}, {}
    for prop in kit.values():
        for name, model in prop["objects"].items():
            obj, _ = bh.make_object(name, model, materials, bpy.context.scene.collection)
            objs[name] = obj
            me = obj.data
            me.calc_loop_triangles()
            co = np.zeros(len(me.vertices) * 3)
            me.vertices.foreach_get("co", co)
            t = np.zeros(len(me.loop_triangles) * 3, dtype=np.int32)
            me.loop_triangles.foreach_get("vertices", t)
            local[name] = (co.reshape(-1, 3), t.reshape(-1, 3))
    return kit, objs, local


def thickness(local_mesh, point_unity):
    """Outer-to-outer thickness through a prop-space point along prop z (Blender local y); (t, front, back)."""
    co, tris = local_mesh
    tree = BVHTree.FromPolygons([tuple(v) for v in co], [tuple(t) for t in tris])
    x, y_u = point_unity[0], point_unity[1]
    front = tree.ray_cast((x, 0.2, y_u), (0, -1, 0))[0]
    back = tree.ray_cast((x, -0.2, y_u), (0, 1, 0))[0]
    return front.y - back.y, front.y, back.y


def pouch_matrix(x_c, y_c, z_c, clasp_point, point_unity):
    """Blender world matrix of a gc_kit pouch (local axes: prop x, prop z, prop y) whose prop-space point lands on
    the clasp point with prop x, y, z along x_c, y_c, z_c."""
    a = np.asarray(point_unity)
    origin = clasp_point - (x_c * a[0] + y_c * a[1] + z_c * a[2])
    m = np.eye(4)
    m[:3, 0], m[:3, 1], m[:3, 2], m[:3, 3] = x_c, z_c, y_c, origin
    return m


def world_tree(local_mesh, m):
    co, tris = local_mesh
    w = (m[:3, :3] @ co.T).T + m[:3, 3]
    return BVHTree.FromPolygons([tuple(v) for v in w], [tuple(t) for t in tris])


def skin_tree(verts, tris, keep=None):
    if keep is not None:
        tris = tris[np.all(keep[tris], axis=1)]
    return BVHTree.FromPolygons([tuple(v) for v in verts], [tuple(t) for t in tris])


def min_dist(tree, verts, mask=None):
    idx = np.arange(len(verts)) if mask is None else np.nonzero(mask)[0]
    best, where = 1.0, None
    for i in idx:
        hit = tree.find_nearest(tuple(verts[i]), 0.05)
        if hit[0] is not None and hit[3] < best:
            best, where = hit[3], i
    return best, where


# ----------------------------------------------------------------------------- solve
class Hand:
    """Finger-local IK for one bare hand: index/middle (MCP, PIP; DIP = PIP / 2) and thumb (CMC rotation vector,
    MP, IP) so the thumb pad faces the index-middle pads across a given aperture."""

    def __init__(self, body, side):
        self.body, self.side, s = body, side, "." + side
        self.n, self.bend = body.frame(side)
        R = body.rest
        self.fb = {d: [b + s for b in DIGITS[d]] for d in DIGITS}
        self.axis = {}
        for d in ("index", "middle", "ring", "little"):
            for b in self.fb[d]:
                self.axis[b] = body.flex_local(b, self.n)
        for b in self.fb["thumb"][1:]:
            self.axis[b] = body.flex_local(b, self.bend)
        self.B1 = R[self.fb["thumb"][0]][:3, :3]
        obj = body.arms[side]
        co = np.zeros(len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        self.pad = {}
        for d, normal in (("index", self.n), ("middle", self.n), ("thumb", self.bend)):
            b = self.fb[d][2]
            head, ax = R[b][:3, 3], R[b][:3, 1]
            pn = unit(normal - np.dot(normal, ax) * ax)
            v = co[body.region[side] == b]
            t = (v - head) @ ax
            v = v[(t > 0.3 * body.length[b]) & (t < 0.8 * body.length[b])]
            p = v[np.argmax((v - head) @ pn)]
            self.pad[d] = ((np.linalg.inv(R[b]) @ np.append(p, 1.0))[:3], R[b][:3, :3].T @ pn)

    def deltas(self, q, rl=None):
        """rl = {"ring": scale, "little": scale}: their flexion as a fraction of the middle finger's."""
        d = {}
        rl = rl or {"ring": 1.0, "little": 1.0}
        for name, (mcp, pip) in (("index", (q[0], q[1])), ("middle", (q[2], q[3])),
                                 ("ring", (rl["ring"] * q[2], rl["ring"] * q[3])),
                                 ("little", (rl["little"] * q[2], rl["little"] * q[3]))):
            for b, a in zip(self.fb[name], (mcp, pip, 0.5 * pip)):
                d[b] = rotvec(self.axis[b] * a)
        t = self.fb["thumb"]
        d[t[0]] = self.B1.T @ rotvec(q[4:7]) @ self.B1
        d[t[1]] = rotvec(self.axis[t[1]] * q[7])
        d[t[2]] = rotvec(self.axis[t[2]] * q[8])
        return d

    def pads(self, d):
        bones = [self.fb[x][2] for x in ("index", "middle", "thumb")]
        P = self.body.fk(d, bones)
        out = {}
        for x, b in zip(("index", "middle", "thumb"), bones):
            p_local, n_local = self.pad[x]
            out[x] = ((P[b] @ np.append(p_local, 1.0))[:3], P[b][:3, :3] @ n_local)
        return out

    def cost(self, q, aperture):
        p = self.pads(self.deltas(q))
        (ip, ipn), (mp, mpn), (tp, tpn) = p["index"], p["middle"], p["thumb"]
        F = (ip + mp) / 2
        nav = unit(ipn + mpn)
        e = np.sum((tp - (F + aperture * nav)) ** 2) / 1e-6            # thumb pad opposite, at the aperture
        e += 30.0 * (np.dot(ip - mp, nav) ** 2) / 1e-6                  # both finger pads on one face
        e += 200.0 * (1.0 + np.dot(tpn, nav)) ** 2 + 100.0 * (1.0 - np.dot(ipn, mpn))
        e += 1e6 * max(0.0, 0.015 - np.linalg.norm(ip - mp)) ** 2
        e += 60.0 * float(np.sum((q[:4] - NOMINAL[:4]) ** 2)) + 6.0 * float(np.sum((q[4:] - NOMINAL[4:]) ** 2))
        e += 1e4 * float(np.sum(np.maximum(0, LIMITS[0] - q) ** 2 + np.maximum(0, q - LIMITS[1]) ** 2))
        return e

    def solve(self, aperture, q0=None):
        starts = [q0] if q0 is not None else [NOMINAL + np.radians([0, 0, 0, 0, a, b, c, 0, 0])
                                                for a, b, c in ((0, 0, 0), (40, 0, 0), (0, 40, 0), (0, 0, 40),
                                                                (-40, 0, 0), (0, -40, 0), (0, 0, -40))]
        best = None
        for x0 in starts:
            q, e = nelder_mead(lambda q: self.cost(q, aperture), x0, [0.2] * 9, iters=4000)
            q, e = nelder_mead(lambda q: self.cost(q, aperture), q, [0.05] * 9, iters=4000)
            if best is None or e < best[1]:
                best = (q, e)
        return best

    def frame(self, d):
        """Pouch frame from the pads: z from the finger pads to the thumb, x along the distal fingers, y = index side."""
        p = self.pads(d)
        (ip, ipn), (mp, mpn), (tp, tpn) = p["index"], p["middle"], p["thumb"]
        F = (ip + mp) / 2
        z_c = unit(tp - F)
        P = self.body.fk(d, [self.fb["index"][2], self.fb["middle"][2]])
        f = unit(P[self.fb["index"][2]][:3, 1] + P[self.fb["middle"][2]][:3, 1])
        x_c = unit(f - np.dot(f, z_c) * z_c)
        y_c = np.cross(x_c, z_c)
        edge = "+x"
        if np.dot(y_c, ip - mp) < 0:
            x_c, y_c, edge = -x_c, -y_c, "-x"
        return x_c, y_c, z_c, F, tp, edge


def overlap_by_region(verts, tris, reg, trees, groups):
    out = {}
    for name, bones in groups.items():
        keep = np.isin(reg, bones)
        if not keep.any():
            continue
        st = skin_tree(verts, tris, keep)
        n = sum(len(tr.overlap(st)) for tr in trees)
        if n:
            out[name] = n
    return out


def solve(body, side, kind, kit, local, log, shifts_mm=(0, 5, 10, 15, 20, 25, 30, 35, 40)):
    """Clasp for one hand and one pouch. The clasp point starts at the pouch's grip anchor and slides towards the
    hand-side edge (5 mm steps) only while the arm skin would intersect the pouch; ring and little then flex only
    as far as the pouch's back face allows (10 % steps of the middle finger's flexion)."""
    hand = Hand(body, side)
    s = "." + side
    reg, tris = body.region[side], body.tris[side]
    prop, anchor_name, variants = POUCHES[kind]
    anchor = np.array(kit[prop]["anchors"][anchor_name][0])
    finger_pads = np.isin(reg, [hand.fb["index"][2], hand.fb["middle"][2]])
    thumb_pad = reg == hand.fb["thumb"][2]
    groups = {d: hand.fb[d] for d in DIGITS}
    groups["palm_and_wrist"] = ["wrist" + s] + ["metacarpal%d%s" % (i, s) for i in range(1, 5)]
    groups["forearm_and_sleeve"] = [g for g in set(reg) if g and not any(g in b for b in groups.values())]
    result, q = None, None
    for mm in shifts_mm:
        t, front, back = thickness(local[variants[0]], anchor)
        target = t + 2 * EPS
        rl = {"ring": 1.0, "little": 1.0}
        for it in range(6):
            q, err = hand.solve(target, q)
            d = hand.deltas(q, rl)
            x_c, y_c, z_c, F, tp, edge = hand.frame(d)
            wrist = body.rest["wrist" + s][:3, 3]
            h = 1.0 if np.dot(wrist - F, x_c) > 0 else -1.0       # which prop-x side the hand is on
            point = anchor + np.array([h * mm / 1000.0, 0.0, 0.0])
            t, front, back = thickness(local[variants[0]], point)
            C = F + (-back + EPS) * z_c                            # the prop point lands on the pouch's mid-plane
            m = pouch_matrix(x_c, y_c, z_c, C, point)
            trees = [world_tree(local[v], m) for v in variants]
            # ring and little: as much of the middle finger's flexion as the back face allows
            for name in ("ring", "little"):
                for k in np.arange(1.0, -0.01, -0.1):
                    rl[name] = float(k)
                    body.apply(hand.deltas(q, rl))
                    v = body.skin(side)
                    if not overlap_by_region(v, tris, reg, trees, {name: hand.fb[name]}):
                        break
            d = hand.deltas(q, rl)
            body.apply(d)
            verts = body.skin(side)
            hits = overlap_by_region(verts, tris, reg, trees, groups)
            g_f = min(min_dist(tr, verts, finger_pads)[0] for tr in trees)
            g_t = min(min_dist(tr, verts, thumb_pad)[0] for tr in trees)
            log("  %s %s shift %d mm it %d: cost %.1f target %.2f thick %.2f gaps fingers %.2f thumb %.2f ring/little %.1f/%.1f hits %s" % (
                side, kind, mm, it, err, target * 1000, t * 1000, g_f * 1000, g_t * 1000, rl["ring"], rl["little"], hits))
            if not hits and abs(g_f - EPS) < 0.0004 and abs(g_t - EPS) < 0.0004:
                break
            if set(hits) - {"thumb", "index", "middle"}:
                break                                              # palm/web or sleeve: needs a bigger shift
            target += 0.0005 if hits else (EPS - g_t) + (EPS - g_f)
        result = {"pose": d, "q": q, "rl": dict(rl), "x_c": x_c, "y_c": y_c, "z_c": z_c, "C": C, "point": point,
                  "shift_mm": mm, "hand_side": h, "thickness": t, "front": front, "back": back, "edge": edge,
                  "verts": verts, "variants": variants, "hits": hits, "aperture_target": target, "hand": hand}
        if not hits:
            break
    return result


def thumb_ik(hand, target, z_c, p0=None):
    """Thumb only (CMC rotation vector, MP, IP): pad on the target, facing -z_c, distal segment along the face."""
    body, tb = hand.body, hand.fb["thumb"]
    p_local, n_local = hand.pad["thumb"]

    def deltas(p):
        return {tb[0]: hand.B1.T @ rotvec(p[:3]) @ hand.B1, tb[1]: rotvec(hand.axis[tb[1]] * p[3]),
                tb[2]: rotvec(hand.axis[tb[2]] * p[4])}

    def cost(p):
        m3 = body.fk(deltas(p), tb)[tb[2]]
        pt = (m3 @ np.append(p_local, 1.0))[:3]
        e = np.sum((pt - target) ** 2) / 1e-6
        e += 200.0 * (1.0 + np.dot(m3[:3, :3] @ n_local, z_c)) ** 2 + 50.0 * np.dot(m3[:3, 1], z_c) ** 2
        e += 6.0 * float(np.dot(p[:3], p[:3])) + 6.0 * ((p[3] - 0.35) ** 2 + (p[4] - 0.35) ** 2)
        lo, hi = np.radians([-10.0, -10.0]), np.radians([60.0, 80.0])
        e += 1e4 * float(np.sum(np.maximum(0, lo - p[3:]) ** 2 + np.maximum(0, p[3:] - hi) ** 2))
        return e

    starts = [p0] if p0 is not None else [np.array([a, b, c, 0.35, 0.35]) for a, b, c in
                                          ((0, 0, 0), (0.7, 0, 0), (0, 0.7, 0), (0, 0, 0.7), (-0.7, 0, 0), (0, -0.7, 0), (0, 0, -0.7))]
    best = None
    for x0 in starts:
        p, e = nelder_mead(cost, x0, [0.25] * 5, iters=3000)
        p, e = nelder_mead(cost, p, [0.05] * 5, iters=3000)
        if best is None or e < best[1]:
            best = (p, e)
    m3 = body.fk(deltas(best[0]), tb)[tb[2]]
    miss = float(np.linalg.norm((m3 @ np.append(p_local, 1.0))[:3] - target))
    return best[0], best[1], deltas(best[0]), miss


def solve_edge(body, side, kind, kit, local, log, mcp_deg=(22.0, 22.0, 26.0, 30.0), shifts_mm=tuple(range(0, 60, 5))):
    """Edge clasp: index..little straight and flexed only at the knuckle, so the pouch's back face rests on them; the
    pouch's near edge sits in the thumb web; the thumb pad presses the front face opposite the middle phalanges of
    index and middle. The clasp point starts at the grip anchor and slides towards the held edge in 5 mm steps until
    no skin intersects (the pouch then sticks out further beyond the fingers)."""
    hand = Hand(body, side)
    s = "." + side
    reg, tris = body.region[side], body.tris[side]
    prop, anchor_name, variants = POUCHES[kind]
    anchor = np.array(kit[prop]["anchors"][anchor_name][0])
    groups = {d: hand.fb[d] for d in DIGITS}
    groups["palm_and_wrist"] = ["wrist" + s] + ["metacarpal%d%s" % (i, s) for i in range(1, 5)]
    groups["forearm_and_sleeve"] = [g for g in set(reg) if g and not any(g in b for b in groups.values())]
    fingers = {}
    for digit, deg in zip(("index", "middle", "ring", "little"), mcp_deg):
        b = hand.fb[digit][0]
        fingers[b] = rotvec(hand.axis[b] * math.radians(deg))
        for bone, a in ((hand.fb[digit][1], PIP_DEG), (hand.fb[digit][2], DIP_DEG)):
            if a:
                fingers[bone] = rotvec(hand.axis[bone] * math.radians(a))
    i2, m2 = hand.fb["index"][1], hand.fb["middle"][1]
    Pm = body.fk(fingers, [i2, m2])
    palmar = lambda b: Pm[b][:3, :3] @ (body.rest[b][:3, :3].T @ hand.n)
    z_c = unit(palmar(i2) + palmar(m2))
    f = unit(Pm[i2][:3, 1] + Pm[m2][:3, 1])
    x_c = unit(f - np.dot(f, z_c) * z_c)
    y_c = np.cross(x_c, z_c)
    if TILT_DEG:            # lift the pouch's far side off the fingertips so the fingers touch under the thumb
        t_ = math.radians(TILT_DEG)
        x_c, z_c = math.cos(t_) * x_c + math.sin(t_) * z_c, math.cos(t_) * z_c - math.sin(t_) * x_c
        y_c = np.cross(x_c, z_c)
    edge = "+x"
    if np.dot(y_c, body.rest[hand.fb["index"][0]][:3, 3] - body.rest[hand.fb["middle"][0]][:3, 3]) < 0:
        x_c, y_c, edge = -x_c, -y_c, "-x"
    mid = lambda b: Pm[b][:3, 3] + Pm[b][:3, 1] * body.length[b] * 0.5
    ref = (mid(i2) + mid(m2)) / 2
    h = 1.0 if np.dot(body.rest["wrist" + s][:3, 3] - ref, x_c) > 0 else -1.0
    body.apply(fingers)
    v0 = body.skin(side)
    nonthumb = skin_tree(v0, tris, ~np.isin(reg, hand.fb["thumb"]))

    def clear(m, tree):
        return not any(len(world_tree(local[v], m).overlap(tree)) for v in variants)

    result, p = None, None
    for mm in shifts_mm:
        point = anchor + np.array([h * mm / 1000.0, 0.0, 0.0])
        t, front, back = thickness(local[variants[0]], point)
        # 1. back face down onto the fingers (bisection, 0.01 mm)
        lo, hi = -0.015, 0.040                           # lo intersects, hi is free
        for _ in range(13):
            mid_d = (lo + hi) / 2
            if clear(pouch_matrix(x_c, y_c, z_c, ref + (mid_d - back) * z_c, point), nonthumb):
                hi = mid_d
            else:
                lo = mid_d
        C = ref + (hi - back) * z_c
        m = pouch_matrix(x_c, y_c, z_c, C, point)
        trees = [world_tree(local[v], m) for v in variants]
        # 2. every finger flexes at its knuckle until it rests on the back face (bisection, 0.1 degree)
        flex = dict(zip(("index", "middle", "ring", "little"), mcp_deg))
        for digit in ("index", "middle", "ring", "little"):
            b = hand.fb[digit][0]
            a_lo, a_hi = flex[digit] - 10.0, flex[digit] + 25.0
            keep = np.isin(reg, hand.fb[digit])

            def free_at(a):
                trial = dict(fingers)
                trial[b] = rotvec(hand.axis[b] * math.radians(a))
                body.apply(trial)
                return not overlap_by_region(body.skin(side), tris, reg, trees, {digit: hand.fb[digit]})
            if free_at(a_hi):
                flex[digit] = a_hi
            else:
                for _ in range(9):
                    a = (a_lo + a_hi) / 2
                    if free_at(a):
                        a_lo = a
                    else:
                        a_hi = a
                flex[digit] = a_lo
            fingers[b] = rotvec(hand.axis[b] * math.radians(flex[digit]))
        # 3. thumb pad onto the front face, opposite the index-middle middle phalanges as far as it reaches
        target = C + (front + EPS) * z_c
        hits = {"thumb": 1}
        for it in range(6):
            p, err, td, miss = thumb_ik(hand, target, z_c, p)
            d = dict(fingers)
            d.update(td)
            body.apply(d)
            verts = body.skin(side)
            hits = overlap_by_region(verts, tris, reg, trees, groups)
            g_t = min(min_dist(tr, verts, reg == hand.fb["thumb"][2])[0] for tr in trees)
            log("  %s %s shift %d mm it %d: thick %.2f flex %s thumb miss %.1f mm gap %.2f mm hits %s" % (
                side, kind, mm, it, t * 1000, {k: round(v, 1) for k, v in flex.items()}, miss * 1000, g_t * 1000, hits))
            if not hits and abs(g_t - EPS) < 0.0004:
                break
            if set(hits) - {"thumb"} or hits.get("thumb", 0) > 60:
                break                                    # not a pad gap: the pouch's edge needs a bigger shift
            target = target + (0.0005 if hits else (EPS - g_t)) * z_c
        result = {"pose": d, "q": p, "x_c": x_c, "y_c": y_c, "z_c": z_c, "C": C, "point": point, "shift_mm": mm,
                  "hand_side": h, "thickness": t, "front": front, "back": back, "edge": edge, "verts": verts,
                  "variants": variants, "hits": hits, "flex_deg": flex, "rl": {}, "hand": hand, "thumb_miss": miss}
        if not hits:
            break
    return result


def contacts(body, side, res, local):
    """Measured contacts on the evaluated skin: the thumb pad, the back-face point straight across the pouch from it,
    the finger skin there, and the clasp point (middle of that aperture)."""
    s = "." + side
    reg, verts = body.region[side], res["verts"]
    m = pouch_matrix(res["x_c"], res["y_c"], res["z_c"], res["C"], res["point"])
    tree = world_tree(local[res["variants"][0]], m)
    z_c = res["z_c"]
    d_t, i_t = min_dist(tree, verts, reg == "finger1-3" + s)
    thumb = verts[i_t]
    surf = tree.find_nearest(tuple(thumb))[0]
    # through the pouch along -z: the back face under the thumb contact
    hit = tree.ray_cast(tuple(np.array(surf) - z_c * 0.0002), tuple(-z_c))
    back_pt = np.array(hit[0]) if hit[0] is not None else np.array(surf) - z_c * res["thickness"]
    fingers = np.isin(reg, [b + s for d in ("index", "middle", "ring", "little") for b in DIGITS[d]])
    idx = np.nonzero(fingers)[0]
    dist = np.linalg.norm(verts[idx] - back_pt, axis=1)
    j = idx[np.argmin(dist)]
    across = float(np.dot(thumb - verts[j], z_c))
    out = {"thumb_contact": thumb, "thumb_gap_mm": round(d_t * 1000, 2), "back_point": back_pt,
           "opposite_bone": str(reg[j]), "opposite_skin": verts[j], "opposite_skin_to_back_point_mm": round(float(dist.min()) * 1000, 2),
           "aperture_mm": round(across * 1000, 2), "clasp_point": (thumb + verts[j]) / 2}
    for digit in ("index", "middle", "ring", "little"):
        dd, ii = min_dist(tree, verts, np.isin(reg, [b + s for b in DIGITS[digit]]))
        out[digit + "_contact_bone"] = str(reg[ii]) if ii is not None else None
        out[digit + "_gap_mm"] = round(dd * 1000, 2)
        if ii is not None:      # opening between the thumb contact and this finger's contact, along the clasp normal
            out[digit + "_contact"] = verts[ii]
            out[digit + "_aperture_along_normal_mm"] = round(float(np.dot(thumb - verts[ii], z_c)) * 1000, 2)
            out[digit + "_contact_offset_from_thumb_mm"] = round(float(np.linalg.norm(
                (verts[ii] - thumb) - np.dot(verts[ii] - thumb, z_c) * z_c)) * 1000, 1)
    return out


# ----------------------------------------------------------------------------- measurement
def regions(body, side):
    s = "." + side
    reg = body.region[side]
    g = {"thumb_pad_segment": reg == "finger1-3" + s, "index_pad_segment": reg == "finger2-3" + s,
         "middle_pad_segment": reg == "finger3-3" + s}
    for digit, bones in DIGITS.items():
        g[digit] = np.isin(reg, [b + s for b in bones])
    g["palm_and_wrist"] = np.isin(reg, ["wrist" + s] + ["metacarpal%d%s" % (i, s) for i in range(1, 5)])
    g["whole_arm_skin"] = np.ones(len(reg), dtype=bool)
    return g


def measure(body, side, res, local):
    verts, tris = res["verts"], body.tris[side]
    whole = skin_tree(verts, tris)
    m = pouch_matrix(res["x_c"], res["y_c"], res["z_c"], res["C"], res["point"])
    out, contacts = {}, {}
    for v in res["variants"]:
        tree = world_tree(local[v], m)
        row = {"intersecting_pairs": len(tree.overlap(whole))}
        for g, mask in regions(body, side).items():
            d, i = min_dist(tree, verts, mask)
            row[g + "_mm"] = round(d * 1000, 2) if i is not None else None
            if v == res["variants"][0] and g.endswith("_pad_segment") and i is not None:
                contacts[g.split("_")[0]] = verts[i]
        out[v] = row
    return out, contacts


def log(*a):
    print("[clasp]", *a, flush=True)


def setup_render(samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    try:
        prefs.compute_device_type = "OPTIX"
        prefs.get_devices()
        for dev in prefs.devices:
            dev.use = dev.type == "OPTIX"
        scene.cycles.device = "GPU"
    except (TypeError, RuntimeError):
        pass
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "AgX"
    if not scene.world:
        scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.30, 0.32, 0.35, 1)


def still(body, side, res, objs, path, view, size=(800, 600)):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = size
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = not (o == body.arms[side] or o.name == res["variants"][0])
    objs[res["variants"][0]].matrix_world = Matrix(pouch_matrix(res["x_c"], res["y_c"], res["z_c"], res["C"], res["point"]).tolist())
    body.apply(res["pose"])
    C, x_c, y_c, z_c = res["C"], res["x_c"], res["y_c"], res["z_c"]
    pos = {"top": C + 0.24 * y_c + 0.10 * x_c + 0.03 * z_c, "front": C + 0.30 * z_c + 0.06 * y_c + 0.04 * x_c,
           "back": C - 0.30 * z_c + 0.06 * y_c + 0.04 * x_c, "edge": C + 0.30 * x_c + 0.05 * y_c}[view]
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.angle, cam.data.clip_start = math.radians(34), 0.01
    scene.collection.objects.link(cam)
    cam.location = Vector(pos)
    up = Vector(y_c)
    look = (Vector(C) - cam.location).normalized()
    right = look.cross(up).normalized()
    cam.matrix_world = Matrix(((right.x, right.cross(look).x, -look.x, pos[0]), (right.y, right.cross(look).y, -look.y, pos[1]),
                               (right.z, right.cross(look).z, -look.z, pos[2]), (0, 0, 0, 1)))
    light = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    light.data.energy, light.data.size = 30.0, 0.5
    light.location = cam.location + Vector(y_c) * 0.25
    light.rotation_euler = (Vector(C) - light.location).to_track_quat("-Z", "Z").to_euler()
    scene.collection.objects.link(light)
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(light, do_unlink=True)


if __name__ == "__main__" and "--diag" in ARGS:
    os.makedirs(OUT, exist_ok=True)
    body = Body()
    kit, objs, local = build_pouches()
    setup_render(24)
    for side in ARGS[ARGS.index("--diag") + 1].split(","):
        n, bend = body.frame(side)
        log("side", side, "palm normal", np.round(n, 3).tolist(), "thumb bend", np.round(bend, 3).tolist())
        for kind in arg("--kinds", "drink,meal").split(","):
            res = solve_edge(body, side, kind, kit, local, log)
            meas, _ = measure(body, side, res, local)
            log(side, kind, "edge", res["edge"], "hand side %+d" % res["hand_side"], "shift %d mm" % res["shift_mm"],
                "thickness %.2f mm" % (res["thickness"] * 1000), "q deg", np.round(np.degrees(res["q"]), 1).tolist(), "rl", res["rl"])
            for v, row in meas.items():
                log("   ", v, json.dumps(row))
            c = contacts(body, side, res, local)
            log("    contacts", json.dumps({k: v for k, v in c.items() if not isinstance(v, np.ndarray)}))
            for view in ("top", "front", "back"):
                still(body, side, res, objs, os.path.join(OUT, "diag_%s_%s_%s.png" % (side, kind, view)), view)


# ----------------------------------------------------------------------------- final outputs
def wxyz(R):
    return [round(float(c), 6) for c in quat(R)]


def unity_xyzw(R):
    """Expected Unity localRotation for a Blender bone-local rotation: the FBX importer's handedness change mirrors
    every node's local X: (x, y, z, w) = (qx, -qy, -qz, qw). Expected, not verified in Unity by this kit."""
    w, x, y, z = quat(R)
    return [round(float(x), 6), round(float(-y), 6), round(float(-z), 6), round(float(w), 6)]


def unity_from_matrix(R):
    """(x, y, z, w) of a proper rotation matrix given in Unity coordinates."""
    w, x, y, z = quat(R)
    return [round(float(x), 6), round(float(y), 6), round(float(z), 6), round(float(w), 6)]


def to_wrist(body, side, p):
    return (np.linalg.inv(body.rest["wrist." + side]) @ np.append(p, 1.0))[:3]


def mirror(v):
    return [round(float(-v[0]), 5), round(float(v[1]), 5), round(float(v[2]), 5)]


def closing(body, side, res, local, steps=(0.0, 0.25, 0.5, 0.75, 1.0)):
    """Intersections while every rotated finger bone slerps from bind to the clasp, the pouch already in place."""
    s = "." + side
    reg, tris = body.region[side], body.tris[side]
    m = pouch_matrix(res["x_c"], res["y_c"], res["z_c"], res["C"], res["point"])
    trees = [world_tree(local[v], m) for v in res["variants"]]
    groups = {d: [b + s for b in DIGITS[d]] for d in DIGITS}
    groups["palm_and_wrist"] = ["wrist" + s] + ["metacarpal%d%s" % (i, s) for i in range(1, 5)]
    out = []
    for k in steps:
        d = {}
        for b, D in res["pose"].items():
            q = quat(D)
            a = 2 * math.acos(max(-1.0, min(1.0, q[0])))
            axis = q[1:] / (np.linalg.norm(q[1:]) or 1.0)
            d[b] = rotvec(axis * a * k)
        body.apply(d)
        out.append({"fraction": k, "intersecting_pairs_by_region": overlap_by_region(body.skin(side), tris, reg, trees, groups)})
    body.apply(res["pose"])
    return out


M_U = np.array([[-1.0, 0.0, 0.0], [0.0, 0.0, 1.0], [0.0, -1.0, 0.0]])   # Blender armature -> Unity model basis (-x, z, -y)


def u_vec(v):
    return [round(float(c), 5) for c in M_U @ np.asarray(v, dtype=float)]


def u_rot(R):
    """A rotation given in Blender armature axes, re-expressed in the Unity model basis, as Unity (x, y, z, w)."""
    return unity_from_matrix(M_U @ R @ M_U.T)


def pose_record(body, side, kind, res, local, log):
    s = "." + side
    meas, _ = measure(body, side, res, local)
    c = contacts(body, side, res, local)
    head = body.rest["wrist" + s][:3, 3]
    rotated = sorted(b for b, D in res["pose"].items() if not np.allclose(D, np.eye(3), atol=1e-9))
    P = body.fk(res["pose"], rotated)
    bones = {}
    for b in rotated:
        D = res["pose"][b]
        W = body.rest[b]
        dW = P[b][:3, :3] @ W[:3, :3].T
        bones[b] = {"unity_path": body.path(b),
                    "angle_deg": round(math.degrees(2 * math.acos(min(1.0, abs(quat(D)[0])))), 2),
                    "model_delta_at_bind_unity_xyzw": u_rot(dW),
                    "head_from_wrist_unity_m": u_vec(P[b][:3, 3] - head),
                    "tail_from_wrist_unity_m": u_vec(P[b][:3, 3] + P[b][:3, 1] * body.length[b] - head),
                    "blender_bone_local_delta_wxyz": wxyz(D)}
    origin = res["C"] - (res["x_c"] * res["point"][0] + res["y_c"] * res["point"][1] + res["z_c"] * res["point"][2])
    rot_u = M_U @ np.column_stack([res["x_c"], res["y_c"], res["z_c"]])     # prop x, y, z in the Unity model basis
    pt = lambda v: u_vec(np.asarray(v) - head)
    rec = {
        "hand": side, "pouch": POUCHES[kind][0], "variants_checked": list(res["variants"]),
        "grip": "edge clasp: index to little straight and flexed at the knuckle under the back face, the near edge "
                "in the thumb web, the thumb pad on the front (label) face",
        "aperture_mm": c["aperture_mm"], "pouch_thickness_at_clasp_mm": round(res["thickness"] * 1000, 2),
        "thumb_opposes_bone": c["opposite_bone"], "thumb_pad_gap_mm": c["thumb_gap_mm"],
        "opposing_skin_to_back_face_mm": c["opposite_skin_to_back_point_mm"],
        "finger_contacts": {d: {"bone": c[d + "_contact_bone"], "gap_mm": c[d + "_gap_mm"],
                                "aperture_from_thumb_contact_along_normal_mm": c.get(d + "_aperture_along_normal_mm"),
                                "offset_from_thumb_contact_along_face_mm": c.get(d + "_contact_offset_from_thumb_mm"),
                                "contact_from_wrist_unity_m": u_vec(c[d + "_contact"] - head) if d + "_contact" in c else None}
                            for d in ("index", "middle", "ring", "little")},
        "knuckle_flexion_deg": {k: round(v, 2) for k, v in res["flex_deg"].items()},
        "thumb": {"cmc_rotation_vector_deg_armature_space": [round(float(math.degrees(x)), 2) for x in res["q"][:3]],
                  "mp_flexion_deg": round(float(math.degrees(res["q"][3])), 2),
                  "ip_flexion_deg": round(float(math.degrees(res["q"][4])), 2),
                  "miss_from_ideal_target_mm": round(res["thumb_miss"] * 1000, 1)},
        "clasp_point_prop_space_unity": [round(float(x), 5) for x in res["point"]],
        "clasp_point_from_grip_anchor_towards_held_edge_mm": res["shift_mm"],
        "anchors_from_wrist_unity_m": {"ThumbPad": pt(c["thumb_contact"]), "OpposingFingerSkin": pt(c["opposite_skin"]),
                                       "ClaspPoint": pt(c["clasp_point"])},
        "pouch_from_wrist_unity": {"origin_m": pt(origin), "rotation_xyzw": unity_from_matrix(rot_u),
                                   "note": "prop origin and prop axes (x, y up the pouch, z label) at the clasp"},
        "bones": bones, "clearance_by_region": meas, "closing_from_bind": closing(body, side, res, local)}
    log(side, kind, "aperture %.2f mm, opposes %s, shift %d mm, pairs %s" % (
        c["aperture_mm"], c["opposite_bone"], res["shift_mm"], json.dumps({v: r["intersecting_pairs"] for v, r in meas.items()})))
    return rec


if __name__ == "__main__" and "--final" in ARGS:
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    body = Body()
    kit, objs, local = build_pouches()
    setup_render(SAMPLES)
    report = {"task": "WORK-HAND-CLASP-01",
              "rig": {"fbx": "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Meshes/A7_BodyFit02_WORK.fbx",
                      "revision": "85b0994", "lfs_sha256": "46b3051a592f2e2fe3a0e7579ad281411415a6eb71ad4546c2899e47cf5eec90",
                      "armature": body.rig.name, "bones": len(body.rest),
                      "untouched": "bone lengths, bind pose, meshes and weights as published"},
              "conventions": {
                  "blender": "armature space = Blender world of the imported FBX (Z up, character forward -Y, anatomical left +X); "
                             "quaternions (w, x, y, z); bone-local frames as imported (bone Y along the bone)",
                  "unity_model_basis": "Unity model axes = (-x, z, -y) of Blender armature axes, the basis root verified for this "
                                       "rig's imported bone positions (docs/verification/i06-body-float-v04, 1.06 um). All *_unity values "
                                       "are offsets and rotations in that basis with the body at its bind pose, measured from the "
                                       "wrist head; nothing here depends on the importer's per-bone axes. Local-rotation values for "
                                       "Unity are NOT given: they depend on importer axes this kit has not verified.",
                  "apply_in_unity": "all rotations in model space (relative to the imported model root). Capture at bind (the "
                                    "model's default state): Wb = wrist rotation, Bb = each listed bone's rotation. At runtime, with "
                                    "the wrist's current model-space rotation Wr and position Wp: bone rotation = Wr * Inverse(Wb) * "
                                    "model_delta_at_bind * Bb (parent before child); a point p_from_wrist lies at Wp + Wr * "
                                    "Inverse(Wb) * p. Check: the listed head and tail points then land within 2 mm.",
                  "unity_path": "relative to the imported model's root; the armature node is A7_ContinuousBody_Rig",
                  "prop_space": "GALLEY-PROPS-01 prop space (Unity metres, y up the pouch, +z label side)"},
              "poses": {}}
    for side in ("R", "L"):
        for kind in ("drink", "meal"):
            res = solve_edge(body, side, kind, kit, local, log)
            name = "WORK_Clasp_%s_%s" % (kind.capitalize(), side)
            report["poses"][name] = pose_record(body, side, kind, res, local, log)
            if RENDER:
                for view in ("front", "back", "top"):
                    still(body, side, res, objs, os.path.join(OUT, "renders", "%s_%s.png" % (name, view)), view, (960, 720))
    with open(os.path.join(OUT, "work_hand_clasp.json"), "w", encoding="utf-8", newline="\n") as h:
        json.dump(report, h, indent=1)
        h.write("\n")
    log("written", os.path.join(OUT, "work_hand_clasp.json"))

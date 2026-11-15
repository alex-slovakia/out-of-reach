"""MakeHuman CC0 system assets (clothes, hair, eyebrows, eyelashes, eyes, skin) fitted onto the skeleton-compensated
male base mesh of CHAR-REF-01, skinned with MakeHuman's default weights (bone names = FitV02's 164-bone rig).
Proxy fitting follows MakeHuman's .mhclo definition: each proxy vertex = sum(w_i * base[r_i]) + offset * axis scale,
axis scale = |base[a].axis - base[b].axis| / den  (x_scale / y_scale / z_scale lines)."""
import os, re, json
import bpy, bmesh
from mathutils import Vector

S = 0.1065109505563943
TZ = 0.875583749


def mh2b(p):
    return Vector((S * p[0], -S * p[2], S * p[1] + TZ))


class MaleBase:
    """base.obj vertices in MH units with the skeleton-compensated male delta applied; default weights per vertex."""

    def __init__(self, mh_dir, delta_npy=None):
        import numpy as np
        self.V = np.array([[float(t) for t in l.split()[1:4]] for l in open(os.path.join(mh_dir, "base.obj")) if l.startswith("v ")])
        if delta_npy:
            dB = np.load(delta_npy)                    # Blender-axis metres -> MH units
            self.V = self.V + np.stack([dB[:, 0] / S, dB[:, 2] / S, -dB[:, 1] / S], 1)
        W = json.load(open(os.path.join(mh_dir, "default_weights.mhw")))["weights"]
        self.w = {}
        for bone, lst in W.items():
            for vi, wt in lst:
                self.w.setdefault(vi, {})[bone] = wt
        # base topology (body group) with UVs for the skin
        self.VT, self.F = [], []
        g = None
        for l in open(os.path.join(mh_dir, "base.obj")):
            if l.startswith("vt "):
                self.VT.append(tuple(float(t) for t in l.split()[1:3]))
            elif l.startswith("g "):
                g = l.split()[1]
            elif l.startswith("f "):
                idx = []
                for tok in l.split()[1:]:
                    p = tok.split("/")
                    idx.append((int(p[0]) - 1, int(p[1]) - 1 if len(p) > 1 and p[1] else -1))
                self.F.append((g, idx))


def parse_mhclo(path):
    d = {"refs": [], "scale": {}, "delete": set(), "obj": None, "material": None, "z_depth": None}
    mode = None
    dtoks = []
    for raw in open(path, encoding="utf-8", errors="replace"):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        t = line.split()
        key = t[0]
        if mode == "verts" and re.match(r"^-?\d", key):
            if len(t) == 1:
                d["refs"].append(((int(t[0]),), (1.0,), (0.0, 0.0, 0.0)))
            else:
                d["refs"].append(((int(t[0]), int(t[1]), int(t[2])), (float(t[3]), float(t[4]), float(t[5])),
                                  (float(t[6]), float(t[7]), float(t[8]))))
            continue
        if mode == "delete" and (re.match(r"^\d", key) or key == "-"):
            dtoks.extend(t)
            continue
        if key == "verts":
            mode = "verts"
        elif key == "delete_verts":
            mode = "delete"
            dtoks.extend(t[1:])
        elif key == "obj_file":
            d["obj"] = t[1]
        elif key == "material":
            d["material"] = t[1]
        elif key in ("x_scale", "y_scale", "z_scale"):
            d["scale"][key[0]] = (int(t[1]), int(t[2]), float(t[3]))
        elif key == "z_depth":
            d["z_depth"] = int(t[1])
        elif key == "name":
            d["name"] = t[1]
    # delete_verts: integers and "a - b" ranges (also "a-b")
    flat = []
    for tok in dtoks:
        if tok != "-" and "-" in tok:
            a, b = tok.split("-", 1)
            flat += [a, "-", b]
        else:
            flat.append(tok)
    i = 0
    while i < len(flat):
        if i + 2 < len(flat) and flat[i + 1] == "-":
            d["delete"].update(range(int(flat[i]), int(flat[i + 2]) + 1))
            i += 3
        else:
            if flat[i] != "-":
                d["delete"].add(int(flat[i]))
            i += 1
    return d


def parse_obj(path):
    V, VT, F = [], [], []
    for l in open(path, encoding="utf-8", errors="replace"):
        if l.startswith("v "):
            V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith("vt "):
            VT.append(tuple(float(x) for x in l.split()[1:3]))
        elif l.startswith("f "):
            idx = []
            for tok in l.split()[1:]:
                p = tok.split("/")
                idx.append((int(p[0]) - 1, int(p[1]) - 1 if len(p) > 1 and p[1] else -1))
            F.append(idx)
    return V, VT, F


def parse_mhmat(path):
    m = {}
    for l in open(path, encoding="utf-8", errors="replace"):
        t = l.strip().split(None, 1)
        if len(t) == 2 and not t[0].startswith("#"):
            m[t[0]] = t[1].strip()
    return m


def fitted_positions(mb, clo):
    import numpy as np
    V = mb.V
    sc = {}
    for ax, i in (("x", 0), ("y", 1), ("z", 2)):
        if ax in clo["scale"]:
            a, b, den = clo["scale"][ax]
            sc[i] = abs(V[a][i] - V[b][i]) / den
        else:
            sc[i] = 1.0
    out = []
    for refs, ws, off in clo["refs"]:
        p = np.zeros(3)
        for r, w in zip(refs, ws):
            p += w * V[r]
        p += np.array([off[0] * sc[0], off[1] * sc[1], off[2] * sc[2]])
        out.append(p)
    return out


def proxy_weights(mb, clo):
    out = []
    for refs, ws, _ in clo["refs"]:
        acc = {}
        for r, w in zip(refs, ws):
            for bone, bw in mb.w.get(r, {}).items():
                acc[bone] = acc.get(bone, 0.0) + w * bw
        out.append(acc)
    return out


def make_material(name, folder, mhmat, tint=None, alpha_clip=False, rough=None, normal_strength=1.0, gray_tint=False):
    """Principled material from an .mhmat (diffuse/normal textures). tint multiplies the diffuse (grayscale first if gray_tint)."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    b = nt.nodes["Principled BSDF"]
    info = {"diffuse": None, "normal": None}
    if mhmat:
        dt = mhmat.get("diffuseTexture")
        if dt:
            p = os.path.normpath(os.path.join(folder, dt))
            if os.path.exists(p):
                img = bpy.data.images.load(p, check_existing=True)
                tn = nt.nodes.new("ShaderNodeTexImage")
                tn.image = img
                info["diffuse"] = p
                col = tn.outputs["Color"]
                if gray_tint and tint:
                    bw = nt.nodes.new("ShaderNodeRGBToBW")
                    nt.links.new(col, bw.inputs["Color"])
                    col = bw.outputs["Val"]
                if tint:
                    mix = nt.nodes.new("ShaderNodeMix")
                    mix.data_type = "RGBA"
                    mix.blend_type = "MULTIPLY"
                    mix.inputs["Factor"].default_value = 1.0
                    nt.links.new(col, mix.inputs[6])
                    mix.inputs[7].default_value = tint
                    col = mix.outputs[2]
                nt.links.new(col, b.inputs["Base Color"])
                if alpha_clip:
                    nt.links.new(tn.outputs["Alpha"], b.inputs["Alpha"])
                    try:
                        mat.surface_render_method = "DITHERED"
                    except Exception:
                        pass
        nm = mhmat.get("normalmapTexture")
        if nm:
            p = os.path.normpath(os.path.join(folder, nm))
            if os.path.exists(p):
                img = bpy.data.images.load(p, check_existing=True)
                img.colorspace_settings.name = "Non-Color"
                tn = nt.nodes.new("ShaderNodeTexImage")
                tn.image = img
                nmn = nt.nodes.new("ShaderNodeNormalMap")
                nmn.inputs["Strength"].default_value = normal_strength
                nt.links.new(tn.outputs["Color"], nmn.inputs["Color"])
                nt.links.new(nmn.outputs["Normal"], b.inputs["Normal"])
                info["normal"] = p
    if info["diffuse"] is None and tint:
        b.inputs["Base Color"].default_value = tint
    b.inputs["Roughness"].default_value = rough if rough is not None else 0.8
    mat["v03_info"] = json.dumps(info)
    return mat, info


def build_proxy(name, arm, mb, folder, mhclo_name=None, mat=None, to_blender=True):
    """Fit one proxy asset folder onto the male base; returns (object, clo)."""
    clos = [f for f in os.listdir(folder) if f.endswith(".mhclo")]
    clo_path = os.path.join(folder, mhclo_name or clos[0])
    clo = parse_mhclo(clo_path)
    V, VT, F = parse_obj(os.path.join(folder, clo["obj"]))
    pos = fitted_positions(mb, clo)
    wts = proxy_weights(mb, clo)
    assert len(pos) == len(V), (name, len(pos), len(V))
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = arm
    ob.modifiers.new("Armature", "ARMATURE").object = arm
    bones = set(b.name for b in arm.data.bones)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UV0")
    dl = bm.verts.layers.deform.verify()
    gidx = {}
    verts = []
    for p, w in zip(pos, wts):
        v = bm.verts.new(mh2b(p))
        ws = {k: x for k, x in w.items() if k in bones and x > 1e-4}
        tot = sum(ws.values()) or 1.0
        for k, x in sorted(ws.items(), key=lambda kv: -kv[1])[:4]:
            if k not in gidx:
                gidx[k] = ob.vertex_groups.new(name=k).index
            v[dl][gidx[k]] = x / tot
        verts.append(v)
    for f in F:
        try:
            nf = bm.faces.new([verts[i] for i, _ in f])
        except ValueError:
            continue
        for lp, (_, ti) in zip(nf.loops, f):
            if 0 <= ti < len(VT):
                lp[uvl].uv = VT[ti]
        nf.smooth = True
    if mat is not None:
        me.materials.append(mat)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    return ob, clo


def build_body(name, arm, mb, delete, mat, keep=None):
    """the male base 'body' group (skin) minus vertices hidden by clothes (union of their delete_verts)."""
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = arm
    ob.modifiers.new("Armature", "ARMATURE").object = arm
    bones = set(b.name for b in arm.data.bones)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UV0")
    dl = bm.verts.layers.deform.verify()
    vmap, gidx = {}, {}

    def vert(i):
        if i in vmap:
            return vmap[i]
        v = bm.verts.new(mh2b(mb.V[i]))
        ws = {k: x for k, x in mb.w.get(i, {}).items() if k in bones and x > 1e-4}
        tot = sum(ws.values()) or 1.0
        for k, x in sorted(ws.items(), key=lambda kv: -kv[1])[:4]:
            if k not in gidx:
                gidx[k] = ob.vertex_groups.new(name=k).index
            v[dl][gidx[k]] = x / tot
        vmap[i] = v
        return v

    for g, idx in mb.F:
        if g != "body":
            continue
        if any(i in delete for i, _ in idx):
            continue
        if keep is not None and not keep(idx):
            continue
        try:
            nf = bm.faces.new([vert(i) for i, _ in idx])
        except ValueError:
            continue
        for lp, (_, ti) in zip(nf.loops, idx):
            if ti >= 0:
                lp[uvl].uv = mb.VT[ti]
        nf.smooth = True
    me.materials.append(mat)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    return ob

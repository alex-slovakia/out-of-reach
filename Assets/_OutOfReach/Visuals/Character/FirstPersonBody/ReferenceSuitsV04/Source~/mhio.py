"""MakeHuman CC0 file readers and fitting math (numpy). Units: MakeHuman decimetres, y up."""
import json
import numpy as np


def read_obj(path):
    vs, vts, faces = [], [], []
    group = None
    for line in open(path, encoding='utf-8', errors='ignore'):
        if line.startswith('v '):
            vs.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('vt '):
            p = line.split()
            vts.append([float(p[1]), float(p[2])])
        elif line.startswith('g '):
            group = line.split(None, 1)[1].strip()
        elif line.startswith('f '):
            vi, ti = [], []
            for tok in line.split()[1:]:
                parts = tok.split('/')
                vi.append(int(parts[0]) - 1)
                ti.append(int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1)
            faces.append((vi, ti, group))
    return np.array(vs, float), np.array(vts, float), faces


def read_target(path):
    idx, d = [], []
    for line in open(path, encoding='utf-8', errors='ignore'):
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        p = line.split()
        idx.append(int(p[0]))
        d.append([float(p[1]), float(p[2]), float(p[3])])
    return np.array(idx, int), np.array(d, float)


def read_mhclo(path):
    out = dict(scales={}, refs=[], delete=set(), obj=None)
    mode = None
    for raw in open(path, encoding='utf-8', errors='ignore'):
        line = raw.strip()
        if not line or line.startswith('#'):
            continue
        p = line.split()
        key = p[0]
        if key in ('x_scale', 'y_scale', 'z_scale'):
            out['scales'][key[0]] = (int(p[1]), int(p[2]), float(p[3]))
            continue
        if key == 'obj_file':
            out['obj'] = p[1]
            continue
        if key == 'verts':
            mode = 'verts'
            continue
        if key == 'delete_verts':
            mode = 'delete'
            continue
        if mode == 'verts':
            try:
                nums = [float(x) for x in p]
            except ValueError:
                continue    # e.g. 'material', 'vertexboneweights_file' lines inside the verts block
            if len(nums) == 1:
                i = int(nums[0])
                out['refs'].append((i, i, i, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0))
            elif len(nums) >= 9:
                out['refs'].append((int(nums[0]), int(nums[1]), int(nums[2]), nums[3], nums[4], nums[5], nums[6], nums[7], nums[8]))
        elif mode == 'delete':
            toks = line.replace('-', ' - ').split()
            i = 0
            while i < len(toks):
                try:
                    a = int(toks[i])
                except ValueError:
                    i += 1
                    continue
                if i + 2 < len(toks) and toks[i + 1] == '-':
                    b = int(toks[i + 2])
                    out['delete'].update(range(a, b + 1))
                    i += 3
                else:
                    out['delete'].add(a)
                    i += 1
    return out


def read_mhw(path):
    d = json.load(open(path))
    w = d.get('weights', d)
    out = {}
    for bone, pairs in w.items():
        if not pairs:
            continue
        a = np.array(pairs, float)
        out[bone] = (a[:, 0].astype(int), a[:, 1])
    return out


def weight_matrix(mhw, n, bones):
    """dense (n, len(bones)) normalised weights."""
    W = np.zeros((n, len(bones)), float)
    col = {b: i for i, b in enumerate(bones)}
    for b, (idx, w) in mhw.items():
        if b in col:
            W[idx, col[b]] += w
    s = W.sum(1, keepdims=True)
    s[s == 0] = 1
    return W / s


def joints(V, skel):
    return {k: V[np.array(v, int)].mean(0) for k, v in skel['joints'].items()}


def fit_clothes(V, clo, offset_scale=1.0):
    """MakeHuman proxy fitting: weighted reference triangle + per-axis scaled offset."""
    R = np.array(clo['refs'], float)
    i1, i2, i3 = R[:, 0].astype(int), R[:, 1].astype(int), R[:, 2].astype(int)
    w = R[:, 3:6]
    off = R[:, 6:9].copy()
    sc = np.ones(3)
    for ax, k in (('x', 0), ('y', 1), ('z', 2)):
        if ax in clo['scales']:
            a, b, dist = clo['scales'][ax]
            sc[k] = abs(V[a, k] - V[b, k]) / dist if dist else 1.0
    P = V[i1] * w[:, :1] + V[i2] * w[:, 1:2] + V[i3] * w[:, 2:3] + off * sc * offset_scale
    return P, (i1, i2, i3, w)


def mh_to_blender(P, s, ty):
    return np.stack([P[:, 0] * s, -P[:, 2] * s, P[:, 1] * s + ty], 1)

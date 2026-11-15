"""Male body shape for CHAR-REF-01 without changing the skeleton.
MakeHuman CC0 targets (caucasian/african/asian-male-young + muscle) turn the neutral base mesh into a male, but they
also move every joint (the male is 8.5 cm taller). Here the male mesh is re-fitted onto the ORIGINAL joints bone by
bone (blended by MakeHuman's default weights): each bone's male segment is rotated onto the base segment and scaled
along the bone only, so soft-tissue thickness (shoulders, chest, lats, flat belly, narrower pelvis tissue) stays male
while joint positions and bone lengths stay exactly those of FitV02.
python male_shape.py <mh_cc0 dir> <out npy> [report json]"""
import json, sys
import numpy as np

S = 0.1065109505563943
TZ = 0.875583749
MH, OUT = sys.argv[1], sys.argv[2]
REP = sys.argv[3] if len(sys.argv) > 3 else None
WEIGHTS = {"caucasian-male-young": 0.6, "african-male-young": 0.2, "asian-male-young": 0.2,
           "universal-male-young-maxmuscle-averageweight": 0.25, "universal-male-young-maxmuscle-minweight": 0.20}


def toB(p):
    return np.stack([S * p[..., 0], -S * p[..., 2], S * p[..., 1] + TZ], -1)


V = np.array([[float(t) for t in l.split()[1:4]] for l in open(MH + "/base.obj") if l.startswith("v ")])
D = np.zeros_like(V)
for name, w in WEIGHTS.items():
    for line in open("%s/targets/%s.target" % (MH, name)):
        if line.strip() and not line.startswith("#"):
            t = line.split()
            D[int(t[0])] += w * np.array([float(x) for x in t[1:4]])
B0, B1 = toB(V), toB(V + D)
sk = json.load(open(MH + "/default.mhskel"))
J0 = {j: B0[idx].mean(0) for j, idx in sk["joints"].items()}
J1 = {j: B1[idx].mean(0) for j, idx in sk["joints"].items()}
W = json.load(open(MH + "/default_weights.mhw"))["weights"]

acc = np.zeros_like(B1)
wsum = np.zeros(len(B1))


def rot_between(a, b):
    a, b = a / np.linalg.norm(a), b / np.linalg.norm(b)
    v = np.cross(a, b)
    c = float(np.dot(a, b))
    if np.linalg.norm(v) < 1e-12:
        return np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1.0 / (1.0 + c))


def under_head(bn):
    while bn:
        if bn == "head":
            return True
        bn = sk["bones"][bn].get("parent")
    return False


for bname, lst in W.items():
    rigid = under_head(bname)          # face bones are tiny: map the whole head rigidly with the head bone (no squash)
    b = sk["bones"]["head" if rigid else bname]
    h0, t0, h1, t1 = J0[b["head"]], J0[b["tail"]], J1[b["head"]], J1[b["tail"]]
    L0, L1 = np.linalg.norm(t0 - h0), np.linalg.norm(t1 - h1)
    u1 = (t1 - h1) / L1
    sax = 1.0 if rigid else L0 / L1
    R = rot_between(t1 - h1, t0 - h0)
    idx = np.array([i for i, _ in lst])
    ww = np.array([w for _, w in lst])
    r = B1[idx] - h1
    ax = (r @ u1)[:, None] * u1[None, :]
    q = h0 + (R @ (ax * sax + (r - ax)).T).T
    acc[idx] += ww[:, None] * q
    wsum[idx] += ww
has = wsum > 1e-6
comp = B0.copy()
comp[has] = acc[has] / wsum[has][:, None]
delta = comp - B0
np.save(OUT, delta)

# residual joint drift after compensation + silhouette widths on the body group only
Jc = {j: comp[idx].mean(0) for j, idx in sk["joints"].items()}
body = set()
g = None
for l in open(MH + "/base.obj"):
    if l.startswith("g "):
        g = l.split()[1]
    elif l.startswith("f ") and g == "body":
        for tok in l.split()[1:]:
            body.add(int(tok.split("/")[0]) - 1)
body = np.array(sorted(body))
rep = {"weights": WEIGHTS, "joint_drift_mm": {}, "widths_m": {}}
for bn in ("root", "spine03", "spine01", "neck01", "head", "clavicle.L", "upperarm01.L", "lowerarm01.L", "wrist.L",
           "upperleg01.L", "lowerleg01.L", "foot.L"):
    j = sk["bones"][bn]["head"]
    rep["joint_drift_mm"][bn] = round(float(np.linalg.norm(Jc[j] - J0[j]) * 1000), 1)


def width(P, z0, z1, xmax=0.30):
    m = (P[:, 2] > z0) & (P[:, 2] < z1) & (np.abs(P[:, 0]) < xmax)
    return round(float(P[m, 0].max() - P[m, 0].min()), 3), round(float(P[m, 1].max() - P[m, 1].min()), 3)


for name, z0, z1, xm in (("shoulders", 1.40, 1.46, 0.30), ("chest", 1.26, 1.32, 0.19), ("waist", 1.04, 1.10, 0.19),
                         ("hips", 0.86, 0.92, 0.30), ("thighs", 0.70, 0.74, 0.30)):
    rep["widths_m"][name] = {"base_w_d": width(B0[body], z0, z1, xm), "male_comp_w_d": width(comp[body], z0, z1, xm)}
rep["max_delta_mm"] = round(float(np.linalg.norm(delta[body], axis=1).max() * 1000), 1)
print(json.dumps(rep, indent=1))
if REP:
    json.dump(rep, open(REP, "w"), indent=1)

"""motion-samples.json -> motion-samples.csv (one row per object and frame; Unity metres, delta quaternions x,y,z,w)."""
import json, sys
src, dst = sys.argv[1:3]
s = json.load(open(src, encoding="utf-8"))
rows = ["object,frame,t,px,py,pz,qx,qy,qz,qw,weight,tipx,tipy,tipz"]
for name in ("ServiceWrench", "DamagedNut", "DamagedPlugBody", "DamagedCable"):
    for r in s["objects"][name]:
        p = r.get("position", [0.0, 0.0, 0.0])
        q = r.get("delta_rotation_xyzw", [0.0, 0.0, 0.0, 1.0])
        tip = r.get("WrenchTip", [0.0, 0.0, 0.0])
        vals = [name, r["f"], r["t"]] + list(p) + list(q) + [r.get("weight_0_1", 0.0)] + list(tip)
        rows.append(",".join(str(v) for v in vals))
open(dst, "w", encoding="utf-8", newline="\n").write("\n".join(rows) + "\n")
print("csv rows", len(rows) - 1)

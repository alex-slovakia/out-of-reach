import json, sys
a, b = (json.load(open(p)) for p in sys.argv[1:3])
names = list(a["bones"].keys())
print("bones", len(a["bones"]), len(b["bones"]), "same names", names == list(b["bones"].keys()))
mh = ml = mm = 0.0; par = 0
for n in names:
    x, y = a["bones"][n], b["bones"][n]
    par += x["parent"] != y["parent"]
    mh = max(mh, max(abs(p - q) for p, q in zip(x["head"], y["head"])), max(abs(p - q) for p, q in zip(x["tail"], y["tail"])))
    ml = max(ml, abs(x["len"] - y["len"]))
    mm = max(mm, max(abs(p - q) for r1, r2 in zip(x["mat"], y["mat"]) for p, q in zip(r1, r2)))
print("parent mismatches", par, "max head/tail err m", mh, "max len err", ml, "max bone matrix err", mm)
print("scale", a["scale"], b["scale"])

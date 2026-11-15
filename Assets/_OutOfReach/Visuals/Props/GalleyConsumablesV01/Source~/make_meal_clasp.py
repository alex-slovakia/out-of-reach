"""Extract WORK_Clasp_Meal_R from WORK-HAND-CLASP-01 into the compact runtime file used by MealPresenter.

Input: docs/art-direction/work-hand-clasp-v01/work_hand_clasp.json on feature/work-hand-clasp-v1@7e9ee5c (read only).
Output: WORK_Clasp_Meal_R.json (JsonUtility-friendly arrays, values unchanged, parent-before-child bone order).
Run: python -I make_meal_clasp.py <work_hand_clasp.json> <out WORK_Clasp_Meal_R.json>
"""
import json
import sys

src, out = sys.argv[1], sys.argv[2]
data = json.load(open(src, encoding="utf-8"))
pose = data["poses"]["WORK_Clasp_Meal_R"]
bones = []
for name, bone in pose["bones"].items():  # the source lists thumb CMC/MP/IP, then the knuckles: parent before child
    bones.append({"name": name, "delta": bone["model_delta_at_bind_unity_xyzw"], "head": bone["head_from_wrist_unity_m"]})
result = {
    "pose": "WORK_Clasp_Meal_R",
    "source": "feature/work-hand-clasp-v1@7e9ee5c docs/art-direction/work-hand-clasp-v01/work_hand_clasp.json "
              "(rig %s @%s)" % (data["rig"]["fbx"].split("/")[-1], data["rig"]["revision"]),
    "armature": data["rig"]["armature"],
    "pouchOrigin": pose["pouch_from_wrist_unity"]["origin_m"],
    "pouchRotation": pose["pouch_from_wrist_unity"]["rotation_xyzw"],
    "bones": bones,
}
json.dump(result, open(out, "w", encoding="utf-8", newline="\n"), indent=1)
print("bones:", [b["name"] for b in bones])

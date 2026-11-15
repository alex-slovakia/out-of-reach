"""make_metas - writes the Unity .meta files of the SHIP-DETAIL-02 package (original tooling).

GUIDs are derived from the asset path (md5 of "aurora7-dp02/<path>"), so a rebuild always gives the same GUIDs.
FBX metas copy the importer block of the delivered habitat FBX (Bake Axis Conversion on, file units and scale,
global scale 1, no cameras or lights) and remap every FBX material by name onto the existing AURORA-7 / habitat
material asset (externalObjects), so the detail pass shares the ship's materials instead of embedding copies.
CKP-REFINE adds a small material set: texture metas copy the matching AURORA-7 texture importer (A7_Paint_BaseColor /
_MODS / _Normal), material metas copy the habitat's NativeFormatImporter meta; both only change the GUID.
Folders ending in "~" are ignored by Unity and get no metas. Existing non-FBX metas are left untouched.

Run: python -B make_metas.py      (reads ../Source~/reports/dp02_build_report.json for the FBX materials)
"""
import glob
import hashlib
import json
import os
import re
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                                     # .../Aurora7/DetailPass02
AURORA7 = os.path.dirname(ROOT)
HABITAT = os.path.join(os.path.dirname(AURORA7), "Habitat")
TEMPLATE = os.path.join(HABITAT, "Meshes", "HB_Habitat_Placed.fbx.meta")

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
MAT = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(path):
    rel = os.path.relpath(path, AURORA7).replace("\\", "/")
    return hashlib.md5(("aurora7-dp02/" + rel).encode("utf-8")).hexdigest()


def material_guids():
    guids = {}
    for folder in (os.path.join(AURORA7, "Materials"), os.path.join(HABITAT, "Materials"), os.path.join(ROOT, "Materials")):
        for meta in glob.glob(os.path.join(folder, "*.mat.meta")):
            name = os.path.basename(meta)[:-len(".mat.meta")]
            match = re.search(r"^guid: ([0-9a-f]{32})", open(meta, encoding="utf-8").read(), re.M)
            guids[name] = match.group(1)
    for mat in glob.glob(os.path.join(ROOT, "Materials", "*.mat")):     # CKP-REFINE set: metas may not exist yet
        guids.setdefault(os.path.basename(mat)[:-len(".mat")], guid_for(mat))
    return guids


def fbx_meta(path, materials, guids):
    text = open(TEMPLATE, encoding="utf-8").read()
    text = re.sub(r"^guid: [0-9a-f]{32}", "guid: " + guid_for(path), text, count=1, flags=re.M)
    lines = ["  externalObjects:"]
    for name in sorted(materials):
        if name not in guids:
            raise SystemExit("no material asset named %s" % name)
        lines += ["  - first:", "      type: UnityEngine:Material", "      assembly: UnityEngine.CoreModule",
                  "      name: " + name, "    second: {fileID: 2100000, guid: %s, type: 2}" % guids[name]]
    start = text.index("  externalObjects:")
    end = text.index("  materials:\n")
    return text[:start] + "\n".join(lines) + "\n" + text[end:]


def texture_meta(path):
    """Importer of the matching AURORA-7 painted-metal texture (sRGB colour, linear MODS, normal map)."""
    stem = os.path.splitext(os.path.basename(path))[0]
    kind = stem.rsplit("_", 1)[1]
    if kind not in ("BaseColor", "MODS", "Normal"):
        raise SystemExit("no texture meta rule for " + path)
    text = open(os.path.join(AURORA7, "Textures", "A7_Paint_%s.png.meta" % kind), encoding="utf-8").read()
    return re.sub(r"^guid: [0-9a-f]{32}", "guid: " + guid_for(path), text, count=1, flags=re.M)


def write(path, text, overwrite=False):
    meta = path + ".meta"
    if os.path.exists(meta) and not overwrite:
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    return True


def main():
    report_path = os.path.join(ROOT, "Source~", "reports", "dp02_build_report.json")
    report = json.load(open(report_path, encoding="utf-8"))
    fbx_materials = {os.path.normpath(os.path.join(ROOT, unit["fbx"])): unit["materials"] for unit in report["units"].values()}
    guids = material_guids()
    written = []
    if write(ROOT, FOLDER.format(guid=guid_for(ROOT))):
        written.append(ROOT + ".meta")
    for base, dirs, files in os.walk(ROOT):
        dirs[:] = sorted(d for d in dirs if not d.endswith("~") and d != "__pycache__")
        for d in dirs:
            path = os.path.join(base, d)
            if write(path, FOLDER.format(guid=guid_for(path))):
                written.append(path + ".meta")
        for f in sorted(files):
            if f.endswith(".meta"):
                continue
            path = os.path.join(base, f)
            ext = os.path.splitext(f)[1].lower()
            if ext == ".fbx":
                text = fbx_meta(path, fbx_materials[os.path.normpath(path)], guids)
                if write(path, text, overwrite=True):
                    written.append(path + ".meta")
            elif ext in (".json", ".md", ".txt"):
                if write(path, TEXT.format(guid=guid_for(path))):
                    written.append(path + ".meta")
            elif ext == ".py":
                if write(path, DEFAULT.format(guid=guid_for(path))):
                    written.append(path + ".meta")
            elif ext == ".png":
                if write(path, texture_meta(path)):
                    written.append(path + ".meta")
            elif ext == ".mat":
                if write(path, MAT.format(guid=guid_for(path))):
                    written.append(path + ".meta")
            else:
                raise SystemExit("no meta rule for " + path)
    for path in written:
        print("meta", os.path.relpath(path, AURORA7).replace("\\", "/"))


if __name__ == "__main__":
    main()

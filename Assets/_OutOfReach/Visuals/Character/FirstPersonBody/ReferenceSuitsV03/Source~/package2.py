"""Package the CC0-proxy characters into ReferenceSuitsV03: FBX + metas, URP Lit materials with textures, textures.
python package2.py <worktree> <work fbx> <eva fbx> <materials json written by the builds> [<display png>]
materials json: {matname: {"base": hex, "rough": r, "metal": m, "diffuse": path|null, "normal": path|null,
                 "gray": bool, "alpha_clip": bool, "transparent": alpha|null, "emission": [hex, strength]|null,
                 "tiling": n, "normal_strength": s}}"""
import sys, os, json, hashlib, shutil, re
from PIL import Image, ImageOps

WT, WORK, EVA, MJ = sys.argv[1:5]
DISPLAY = sys.argv[5] if len(sys.argv) > 5 else None
ROOT = os.path.join(WT, "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/ReferenceSuitsV03")
FIT = os.path.join(WT, "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02")
URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"
WEAVE_GUID = "adeb5024fa44af24ea42237e851fae78"


def guid(name):
    return hashlib.md5(("OOR-ReferenceSuitsV03/" + name).encode()).hexdigest()


def lin(h):
    h = h.lstrip("#")
    f = lambda c: (c / 255.0 / 12.92) if c / 255.0 <= 0.04045 else ((c / 255.0 + 0.055) / 1.055) ** 2.4
    return tuple(f(int(h[i:i + 2], 16)) for i in (0, 2, 4))


def folder_meta(path, key):
    with open(path + ".meta", "w", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(key))


TEX_META_TPL = open(os.path.join(FIT, "Textures/Body_Weave_Normal.png.meta"), newline="").read()


def texture(src, name, kind, gray=False, maxsize=2048):
    """copy (optionally grayscale-normalised) texture into Textures/ with a TextureImporter meta. kind: color|normal|alpha"""
    dst = os.path.join(ROOT, "Textures", name + ".png")
    im = Image.open(src)
    if max(im.size) > maxsize:
        im = im.resize((maxsize * im.size[0] // max(im.size), maxsize * im.size[1] // max(im.size)), Image.LANCZOS)
    if gray:
        a = im.getchannel("A") if im.mode in ("RGBA", "LA") else None
        g = ImageOps.grayscale(im.convert("RGB"))
        # normalise so the mean is ~0.82: the material colour then sets the real tone
        import statistics
        hist = g.histogram()
        tot = sum(hist)
        mean = sum(i * c for i, c in enumerate(hist)) / max(1, tot)
        k = 209.0 / max(1.0, mean)
        g = g.point(lambda v: min(255, int(v * k)))
        im = Image.merge("RGBA", (g, g, g, a)) if a is not None else g.convert("RGB")
    im.save(dst)
    tm = re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid("tex/" + name), TEX_META_TPL, count=1)
    tm = re.sub(r"textureType: \d+", "textureType: %d" % (1 if kind == "normal" else 0), tm)
    tm = re.sub(r"sRGBTexture: \d+", "sRGBTexture: %d" % (0 if kind == "normal" else 1), tm)
    tm = re.sub(r"alphaIsTransparency: \d+", "alphaIsTransparency: %d" % (1 if kind == "alpha" else 0), tm)
    tm = re.sub(r"maxTextureSize: \d+", "maxTextureSize: %d" % maxsize, tm)
    open(dst + ".meta", "w", newline="\n").write(tm)
    return guid("tex/" + name)


def mat_text(name, spec, base_guid, normal_guid):
    r, g, b = lin(spec.get("base", "#FFFFFF"))
    rough = spec.get("rough", 0.8)
    metal = spec.get("metal", 0.0)
    alpha = spec.get("transparent") or 1.0
    transp = spec.get("transparent") is not None
    clip = bool(spec.get("alpha_clip"))
    em = spec.get("emission")
    er, eg, eb = (0, 0, 0)
    if em:
        er, eg, eb = (c * em[1] for c in lin(em[0]))
    tiling = spec.get("tiling", 1)
    bump = ("{fileID: 2800000, guid: %s, type: 3}" % normal_guid) if normal_guid else "{fileID: 0}"
    basem = ("{fileID: 2800000, guid: %s, type: 3}" % base_guid) if base_guid else "{fileID: 0}"
    emap = basem if (em and base_guid) else "{fileID: 0}"
    kws = []
    if normal_guid:
        kws.append("_NORMALMAP")
    if em:
        kws.append("_EMISSION")
    if transp:
        kws.append("_SURFACE_TYPE_TRANSPARENT")
    if clip:
        kws.append("_ALPHATEST_ON")
    kwtxt = ("  m_ValidKeywords:\n" + "".join("  - %s\n" % k for k in kws)) if kws else "  m_ValidKeywords: []\n"
    queue = 3000 if transp else (2450 if clip else 2000)
    rtype = "Transparent" if transp else ("TransparentCutout" if clip else "Opaque")
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {URP_LIT}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
{kwtxt}  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {rtype}
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {basem}
        m_Scale: {{x: {tiling}, y: {tiling}}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {bump}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {emap}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {basem}
        m_Scale: {{x: {tiling}, y: {tiling}}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: {1 if clip else 0}
    - _Blend: 0
    - _BumpScale: {spec.get("normal_strength", 1.0)}
    - _Cull: {0 if clip else 2}
    - _Cutoff: 0.5
    - _DstBlend: {10 if transp else 0}
    - _EnvironmentReflections: 1
    - _Metallic: {metal}
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {round(1 - rough, 4)}
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: {5 if transp else 1}
    - _Surface: {1 if transp else 0}
    - _WorkflowMode: 1
    - _ZWrite: {0 if transp else 1}
    m_Colors:
    - _BaseColor: {{r: {r:.6f}, g: {g:.6f}, b: {b:.6f}, a: {alpha}}}
    - _Color: {{r: {r:.6f}, g: {g:.6f}, b: {b:.6f}, a: {alpha}}}
    - _EmissionColor: {{r: {er:.6f}, g: {eg:.6f}, b: {eb:.6f}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""


os.makedirs(ROOT, exist_ok=True)
for sub in ("Meshes", "Materials", "Provenance", "Textures"):
    os.makedirs(os.path.join(ROOT, sub), exist_ok=True)
    folder_meta(os.path.join(ROOT, sub), sub)
folder_meta(ROOT, "root")
# fresh Materials/Textures content (only files this build references)
for sub in ("Materials", "Textures"):
    for f in os.listdir(os.path.join(ROOT, sub)):
        os.remove(os.path.join(ROOT, sub, f))

mats = json.load(open(MJ))
tex_done = {}
for name, spec in sorted(mats.items()):
    bg = ng = None
    if spec.get("diffuse"):
        key = (spec["diffuse"], bool(spec.get("gray")))
        if key not in tex_done:
            tname = "T_V03_" + os.path.splitext(os.path.basename(spec["diffuse"]))[0] + ("_gray" if spec.get("gray") else "")
            tex_done[key] = texture(spec["diffuse"], tname, "alpha" if spec.get("alpha_clip") else "color", gray=bool(spec.get("gray")))
        bg = tex_done[key]
    if spec.get("normal"):
        key = (spec["normal"], "n")
        if key not in tex_done:
            tname = "T_V03_" + os.path.splitext(os.path.basename(spec["normal"]))[0]
            tex_done[key] = texture(spec["normal"], tname, "normal")
        ng = tex_done[key]
    elif spec.get("weave"):
        ng = WEAVE_GUID
    p = os.path.join(ROOT, "Materials", name + ".mat")
    open(p, "w", newline="\n").write(mat_text(name, spec, bg, ng))
    open(p + ".meta", "w", newline="\n").write("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(name))

tpl = open(os.path.join(FIT, "Meshes/A7_BodyFit02_WORK.fbx.meta"), newline="").read()
a = tpl.index("    clipAnimations:")
b = tpl.index("    isReadable: 1")
tpl = tpl[:a] + "    clipAnimations: []\n" + tpl[b:]
for src, name in ((WORK, "A7_SuitV03_WORK"), (EVA, "A7_SuitV03_EVA")):
    dst = os.path.join(ROOT, "Meshes", name + ".fbx")
    shutil.copyfile(src, dst)
    open(dst + ".meta", "w", newline="\n").write(re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid(name), tpl, count=1))
print("packaged", len(mats), "materials,", len(tex_done), "textures")

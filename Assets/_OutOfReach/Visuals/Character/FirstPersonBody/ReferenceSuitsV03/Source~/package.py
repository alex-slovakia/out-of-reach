"""Assemble Assets/_OutOfReach/Visuals/Character/FirstPersonBody/ReferenceSuitsV03 (FBX, materials, metas).
python package.py <worktree> <work_fbx> <eva_fbx> [project_for_unity_check]"""
import sys, os, hashlib, shutil, re

WT, WORK, EVA = sys.argv[1:4]
ROOT = os.path.join(WT, "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/ReferenceSuitsV03")
FIT = os.path.join(WT, "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02")
WEAVE_GUID = "adeb5024fa44af24ea42237e851fae78"
URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"


def guid(name):
    return hashlib.md5(("OOR-ReferenceSuitsV03/" + name).encode()).hexdigest()


def lin(h):
    h = h.lstrip("#")
    f = lambda c: (c / 255.0 / 12.92) if c / 255.0 <= 0.04045 else ((c / 255.0 + 0.055) / 1.055) ** 2.4
    return tuple(f(int(h[i:i + 2], 16)) for i in (0, 2, 4))


# name: (hex, rough, metal, emission hex|None, emission strength, cloth)
MATS = {
    "MAT_V03_Shirt": ("#26324F", 0.88, 0, None, 0, True),
    "MAT_V03_Trousers": ("#3A3B3D", 0.9, 0, None, 0, True),
    "MAT_V03_TrousersPanel": ("#333437", 0.92, 0, None, 0, True),
    "MAT_V03_Shoe": ("#2A2C2F", 0.8, 0, None, 0, False),
    "MAT_V03_Sole": ("#16181A", 0.9, 0, None, 0, False),
    "MAT_V03_Skin": ("#A67A64", 0.65, 0, None, 0, False),
    "MAT_V03_Trim": ("#1B1C1E", 0.7, 0, None, 0, False),
    "MAT_V03_EvaSuit": ("#D6D2C8", 0.85, 0, None, 0, True),
    "MAT_V03_EvaPanel": ("#C7C2B6", 0.88, 0, None, 0, True),
    "MAT_V03_EvaBoot": ("#CBC6BA", 0.8, 0, None, 0, True),
    "MAT_V03_EvaBootGrey": ("#585C60", 0.85, 0, None, 0, False),
    "MAT_V03_EvaSole": ("#25272A", 0.9, 0, None, 0, False),
    "MAT_V03_EvaGlove": ("#CFCABF", 0.8, 0, None, 0, True),
    "MAT_V03_EvaGloveGrip": ("#4A4E52", 0.85, 0, None, 0, True),
    "MAT_V03_EvaStrap": ("#47494C", 0.88, 0, None, 0, True),
    "MAT_V03_EvaMetal": ("#9AA2A8", 0.4, 0.8, None, 0, False),
    "MAT_V03_EvaRed": ("#B5382C", 0.75, 0, None, 0, False),
    "MAT_V03_EvaDisplay": ("#0B1620", 0.3, 0, "#2DA8C8", 1.2, False),
    "MAT_V03_EvaVisorGold": ("#C9A24A", 0.12, 1.0, None, 0, False),
    "MAT_V03_EvaLight": ("#FFF2D0", 0.3, 0, "#FFE9B0", 6.0, False),
    "MAT_V03_EvaYellow": ("#E0B020", 0.5, 0, None, 0, False),
    "MAT_V03_EvaDark": ("#1B1C1E", 0.7, 0, None, 0, False),
    "MAT_V03_Hair": ("#2B211B", 0.75, 0, None, 0, False),
    "MAT_V03_EyeWhite": ("#D8D3CA", 0.3, 0, None, 0, False),
    "MAT_V03_EyeIris": ("#3A2B22", 0.2, 0, None, 0, False),
    "MAT_V03_ShirtSeam": ("#1E2843", 0.9, 0, None, 0, True),
    "MAT_V03_ShoeMid": ("#4A4D52", 0.85, 0, None, 0, False),
    "MAT_V03_EvaVisorClear": ("#C8D6DE", 0.05, 0, None, 0, False),
}
TRANSPARENT = {"MAT_V03_EvaVisorClear": 0.12}
TEXTURED = {"MAT_V03_EvaDisplay": ("T_V03_EvaDisplay", 1.5)}

TEX = """    - {n}:
        m_Texture: {{fileID: {fid}, guid: {g}, type: 3}}
        m_Scale: {{x: {s}, y: {s}}}
        m_Offset: {{x: 0, y: 0}}
"""


def mat_text(name, hexc, rough, metal, em, ems, cloth):
    r, g, b = lin(hexc)
    alpha = TRANSPARENT.get(name, 1)
    tex = TEXTURED.get(name)
    if tex:
        r = g = b = 1.0
        em, ems = '#FFFFFF', tex[1]
    er, eg, eb = lin(em) if em else (0, 0, 0)
    er, eg, eb = er * ems, eg * ems, eb * ems
    texref = ("{fileID: 2800000, guid: %s, type: 3}" % guid(tex[0])) if tex else "{fileID: 0}"
    transp = alpha < 1
    bump = ("{fileID: 2800000, guid: %s, type: 3}" % WEAVE_GUID) if cloth else "{fileID: 0}"
    kw = "  - _NORMALMAP\n" if cloth else ""
    emk = "  - _EMISSION\n" if em else ""
    trk = "  - _SURFACE_TYPE_TRANSPARENT\n" if transp else ""
    kws = ("  m_ValidKeywords:\n" + kw + emk + trk) if (kw or emk or trk) else "  m_ValidKeywords: []\n"
    flags = 0 if em else 4
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
{kws}  m_InvalidKeywords: []
  m_LightmapFlags: {flags}
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {3000 if transp else 2000}
  stringTagMap:
    RenderType: {'Transparent' if transp else 'Opaque'}
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {texref}
        m_Scale: {{x: {14 if cloth else 1}, y: {14 if cloth else 1}}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {bump}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {texref}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {texref}
        m_Scale: {{x: {14 if cloth else 1}, y: {14 if cloth else 1}}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0

    - _BumpScale: {0.18 if cloth else 1}
    - _Cull: 2
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


def folder_meta(path, key):
    with open(path + ".meta", "w", newline="\n") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid(key)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs(ROOT, exist_ok=True)
for sub in ("Meshes", "Materials", "Provenance", "Textures"):
    os.makedirs(os.path.join(ROOT, sub), exist_ok=True)
    folder_meta(os.path.join(ROOT, sub), sub)
folder_meta(ROOT, "root")

for name, spec in MATS.items():
    p = os.path.join(ROOT, "Materials", name + ".mat")
    open(p, "w", newline="\n").write(mat_text(name, *spec))
    with open(p + ".meta", "w", newline="\n") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid(name)}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

DISPLAY = sys.argv[4] if len(sys.argv) > 4 else None
if DISPLAY:
    dst = os.path.join(ROOT, "Textures", "T_V03_EvaDisplay.png")
    shutil.copyfile(DISPLAY, dst)
    tm = open(os.path.join(FIT, "Textures/Body_Weave_Normal.png.meta"), newline="").read()
    tm = re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid("T_V03_EvaDisplay"), tm, count=1)
    tm = re.sub(r"textureType: \d+", "textureType: 0", tm)
    tm = re.sub(r"sRGBTexture: \d+", "sRGBTexture: 1", tm)
    open(dst + ".meta", "w", newline=chr(10)).write(tm)
tpl = open(os.path.join(FIT, "Meshes/A7_BodyFit02_WORK.fbx.meta"), newline="").read()
a = tpl.index("    clipAnimations:")
b = tpl.index("    isReadable: 1")
tpl = tpl[:a] + "    clipAnimations: []\n" + tpl[b:]
for src, name in ((WORK, "A7_SuitV03_WORK"), (EVA, "A7_SuitV03_EVA")):
    dst = os.path.join(ROOT, "Meshes", name + ".fbx")
    shutil.copyfile(src, dst)
    m = re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid(name), tpl, count=1)
    open(dst + ".meta", "w", newline="\n").write(m)
print("packaged", ROOT)

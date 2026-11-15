"""
dp02_cockpit - SHIP-DETAIL-02 cockpit cluster (original, procedural work).

Reuses the kit: the crew-notes board on a cockpit blanket bay is built from the habitat pieces (strap, notebook,
headphones, photo-style card); the cockpit frame looms and aft access panels are instanced from dp02_kit.
Front-view mount frame, host surface z = 0, every piece GAP above it.
"""
import sys

sys.dont_write_bytecode = True
from a7kit import Model  # noqa: E402
from dp02_habitat import cam_buckle, headphones, notebook, strap, strap_anchor  # noqa: E402
from dp02_kit import ALU, ANO, DECALS, GAP, PLASTIC, STRAP  # noqa: E402


def checklist_card(m, atlas, key, x, y, width, frame=None):
    """Laminated procedure card from the ship's decal atlas, held by an elastic strap between two rivets."""
    entry = atlas["entries"][key]
    h = width * entry["h"] / entry["w"]
    m.box(PLASTIC, (x, y, GAP + 0.0008), (width + 0.014, h + 0.014, 0.0016), 0.0005, frame)
    m.decal(atlas, key, (x, y, GAP + 0.0016), frame, width=width, lift=0.0005, material=DECALS)
    sy = y + h * 0.36
    m.box(STRAP, (x, sy, GAP + 0.0026), (width + 0.05, 0.012, 0.0012), 0.0, frame)
    for s in (-1, 1):
        m.cyl(ALU, (x + s * (width / 2 + 0.019), sy, GAP), (x + s * (width / 2 + 0.019), sy, GAP + 0.0045), 0.0045, 8,
              0.0008, frame)


def crew_notes_cluster(atlas, hb_atlas):
    """Cockpit blanket bay behind the commander's seat: the flight log (a larger hardcover notebook with a tethered
    pen) held by a strap with a cam buckle between two tie-down points, a laminated repair procedure card under an
    elastic strap, and a comm headset hung from a hook-and-loop strap. All above the bay handrail."""
    m = Model("CrewNotes")
    notebook(m, -0.30, 1.56, None, 0.17, 0.23, 0.018)
    y = 1.56
    strap(m, [(-0.47, y, GAP + 0.0065), (-0.415, y, GAP + 0.0215), (-0.20, y, GAP + 0.0215), (-0.13, y, GAP + 0.0065)],
          0.022, 0.0022, (0, 1, 0))
    cam_buckle(m, (-0.27, y, GAP + 0.0215 + 0.0011), (1, 0, 0), (0, 0, 1), 0.022)
    for x in (-0.47, -0.13):
        strap_anchor(m, x, y, GAP)
    checklist_card(m, atlas, "p_repair", 0.06, 1.62, 0.15)
    headphones(m, 0.40, 1.58)
    return m

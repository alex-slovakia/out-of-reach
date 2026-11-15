#!/usr/bin/env python3
"""Derive the OUT OF REACH celestial-sky orientation table from committed NASA/JPL Horizons data.

Python 3.10+, standard library only, offline. Run from the repository root:

    python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/celestial_sky_table.py          # verify
    python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/celestial_sky_table.py --write  # regenerate

Inputs (never modified):
  docs/data/reference-orbits/normalized.json   Sun-centred Earth/Mars, ICRF + mean ecliptic of J2000, km
  docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv   Earth-centred Mars, ICRF equatorial, km
Output:
  Assets/_OutOfReach/Scripts/Presentation/CelestialSky/CelestialEphemeris2026.g.cs
"""
import csv
import hashlib
import json
import math
import sys
from pathlib import Path

ROOT = Path.cwd()
ORBITS = ROOT / "docs/data/reference-orbits/normalized.json"
GEO = ROOT / "docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv"
OUT = ROOT / "Assets/_OutOfReach/Scripts/Presentation/CelestialSky/CelestialEphemeris2026.g.cs"
ORBITS_SHA = "f3f3e5bdff720a4b277b933f0b31d675d8f49a7429e1a42f12feb5704aecaffc"
GEO_SHA = "694f3606e7abc1d94941b428dc1f7a9364bb07d1810327b6343a77986a79e5af"
SAMPLES = 32                                   # 2026-11-14 08:00 UTC .. 2026-12-15 08:00 UTC, daily
EPS = math.radians(84381.448 / 3600.0)         # IAU 1976 obliquity of the J2000 ecliptic used by Horizons
# ICRS -> Galactic rotation (ESA 1997, Hipparcos Vol. 1 sec. 1.5.3, matrix A_G'); rows are galactic axes in ICRS.
A_G = ((-0.0548755604162154, -0.8734370902348850, -0.4838350155487132),
       (+0.4941094278755837, -0.4448296299600112, +0.7469822444972189),
       (-0.8676661490190047, -0.1980763734312015, +0.4559837761750669))
DEFAULT_LIGHT_EULER = (34.0, -55.0, 0.0)       # Bootstrap.unity "Sunlight - authored direction"
AU_KM = 149597870.7                            # IAU 2012 astronomical unit
EARTH_H = -3.99                                # NASA Earth fact sheet, V-band magnitude V(1,0)
SIRIUS_V = -1.46                               # Sirius Johnson V (Hipparcos-era catalogues)
# Sirius in starmap_2020_8k.exr: background-subtracted luminance summed within 16 texels, times texel solid
# angle (texture value x sr). Measured by Tools~/verify_sky_chain_blender.py, which re-checks it within 2 %.
SIRIUS_MAP_FLUX_8K = 2.326103e-05


def lambert_phase(alpha):
    """Lambert-sphere phase integral; Earth's real phase curve differs by a few tenths of a magnitude."""
    return (math.sin(alpha) + (math.pi - alpha) * math.cos(alpha)) / math.pi


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def norm(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v)


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def ecl_to_eq(v):
    """Rotate about +x by +epsilon: mean ecliptic of J2000 -> ICRF equatorial."""
    x, y, z = v
    c, s = math.cos(EPS), math.sin(EPS)
    return (x, c * y - s * z, s * y + c * z)


def eq_to_sky(v):
    """ICRF (x RA 0h, y RA 6h, z north) -> panoramic sky frame numbers (+X RA 0h, +Y north, +Z RA 6h)."""
    return (v[0], v[2], v[1])


def sky_to_eq(v):
    return (v[0], v[2], v[1])


def radec(ra, dec):
    ra, dec = math.radians(ra), math.radians(dec)
    return (math.cos(dec) * math.cos(ra), math.cos(dec) * math.sin(ra), math.sin(dec))


def to_radec(v):
    return math.degrees(math.atan2(v[1], v[0])) % 360, math.degrees(math.asin(max(-1.0, min(1.0, v[2]))))


def gal_to_eq(l, b):
    g = radec(l, b)
    return tuple(sum(A_G[r][c] * g[r] for r in range(3)) for c in range(3))  # A_G^T g


# Unity conventions reproduced numerically (left-handed display, standard Hamilton quaternion algebra).
def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def quat_rot(q, v):
    u, w = q[:3], q[3]
    t = tuple(2 * c for c in cross(u, v))
    ut = cross(u, t)
    return tuple(v[i] + w * t[i] + ut[i] for i in range(3))


def quat_inv(q):
    return (-q[0], -q[1], -q[2], q[3])


def euler(px, py, pz):
    """Unity Quaternion.Euler: rotate about Z, then X, then Y (q = qy * qx * qz)."""
    def axis(a, deg):
        h = math.radians(deg) / 2
        return tuple(math.sin(h) * c for c in a) + (math.cos(h),)
    return quat_mul(quat_mul(axis((0, 1, 0), py), axis((1, 0, 0), px)), axis((0, 0, 1), pz))


def look_rotation(f, up):
    """Unity Quaternion.LookRotation: maps +Z to f and +Y into the (f, up) half-plane."""
    f = norm(f)
    r = norm(cross(up, f))
    u = cross(f, r)
    m = ((r[0], u[0], f[0]), (r[1], u[1], f[1]), (r[2], u[2], f[2]))
    tr = m[0][0] + m[1][1] + m[2][2]
    if tr > 0:
        s = math.sqrt(tr + 1) * 2
        return ((m[2][1] - m[1][2]) / s, (m[0][2] - m[2][0]) / s, (m[1][0] - m[0][1]) / s, s / 4)
    if m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        s = math.sqrt(1 + m[0][0] - m[1][1] - m[2][2]) * 2
        return (s / 4, (m[0][1] + m[1][0]) / s, (m[0][2] + m[2][0]) / s, (m[2][1] - m[1][2]) / s)
    if m[1][1] > m[2][2]:
        s = math.sqrt(1 + m[1][1] - m[0][0] - m[2][2]) * 2
        return ((m[0][1] + m[1][0]) / s, s / 4, (m[1][2] + m[2][1]) / s, (m[0][2] - m[2][0]) / s)
    s = math.sqrt(1 + m[2][2] - m[0][0] - m[1][1]) * 2
    return ((m[0][2] + m[2][0]) / s, (m[1][2] + m[2][1]) / s, s / 4, (m[1][0] - m[0][1]) / s)


def sky_from_world(sun_world, sun_sky, up_world, up_sky):
    """TRIAD attitude: Sun matched exactly, world up placed as close as possible to up_sky."""
    return quat_mul(look_rotation(sun_sky, up_sky), quat_inv(look_rotation(sun_world, up_world)))


def main(write):
    for p, h in ((ORBITS, ORBITS_SHA), (GEO, GEO_SHA)):
        if sha(p) != h:
            sys.exit(f"SHA-256 mismatch for {p}: {sha(p)}")
    orbits = json.loads(ORBITS.read_text(encoding="utf-8"))
    assert orbits["referenceFrame"] == "ICRF / mean ecliptic of J2000"
    assert orbits["epochUtc"] == "2026-11-14 08:00 UTC" and orbits["sampleStepDays"] == 1
    body = {b["name"]: b["path"] for b in orbits["bodies"]}
    rows = list(csv.DictReader(GEO.open(encoding="utf-8")))[:SAMPLES]
    assert len(rows) == SAMPLES and rows[0]["epoch_utc"] == "2026-11-14T08:00:00Z"
    assert rows[-1]["epoch_utc"] == "2026-12-15T08:00:00Z"

    radius = {b["name"]: b["radiusKm"] for b in orbits["bodies"]}
    sun_dir, earth_dir, worst = [], [], 0.0
    sun_radius, earth_radius, earth_v, earth_flux = [], [], [], []
    for i in range(SAMPLES):
        e = tuple(body["Earth"][i][k] for k in "xyz")
        m = tuple(body["Mars"][i][k] for k in "xyz")
        csv_vec = tuple(float(rows[i][k]) for k in ("x_km", "y_km", "z_km"))
        worst = max(worst, math.dist(ecl_to_eq(sub(m, e)), csv_vec))       # frame + obliquity cross-check
        sun_dir.append(eq_to_sky(norm(ecl_to_eq(tuple(-c for c in m)))))   # observer = Mars-centre proxy
        earth_dir.append(eq_to_sky(norm(ecl_to_eq(sub(e, m)))))
        r_mars, r_earth, delta = math.dist(m, (0, 0, 0)), math.dist(e, (0, 0, 0)), math.dist(e, m)
        sun_radius.append(math.asin(radius["Sun"] / r_mars))               # angular radius, rad
        earth_radius.append(math.asin(radius["Earth"] / delta))
        alpha = math.acos((r_earth ** 2 + delta ** 2 - r_mars ** 2) / (2 * r_earth * delta))  # Sun-Earth-observer
        v = EARTH_H + 5 * math.log10(r_earth / AU_KM * delta / AU_KM) - 2.5 * math.log10(lambert_phase(alpha))
        earth_v.append(v)
        earth_flux.append(SIRIUS_MAP_FLUX_8K * 10 ** (-0.4 * (v - SIRIUS_V)))
    if worst > 0.05:
        sys.exit(f"Ecliptic->ICRF cross-check failed: {worst:.6f} km")
    ecl_north = eq_to_sky(ecl_to_eq((0.0, 0.0, 1.0)))
    gal_north = eq_to_sky(gal_to_eq(0, 90))
    gal_centre = eq_to_sky(gal_to_eq(0, 0))

    print(f"cross-check: rotated heliocentric (Mars - Earth) vs geocentric ICRF CSV, "
          f"max |diff| = {worst:.6f} km over {SAMPLES} days")
    for i in (0, 13, 30, 31):
        ra, dec = to_radec(sky_to_eq(sun_dir[i]))
        er, ed = to_radec(sky_to_eq(earth_dir[i]))
        elong = math.degrees(math.acos(dot(sun_dir[i], earth_dir[i])))
        print(f"day {i:2d}: Sun from Mars RA {ra:8.3f} Dec {dec:+7.3f} | Earth from Mars RA {er:8.3f} "
              f"Dec {ed:+7.3f} | Sun-Earth elongation {elong:6.2f} deg")
    total = math.degrees(math.acos(dot(sun_dir[0], sun_dir[-1])))
    print(f"Sun drift against the stars, day 0 -> day 31: {total:.3f} deg")
    for i in (0, 30):
        print(f"day {i:2d}: Sun disc diameter {2 * math.degrees(sun_radius[i]):.4f} deg | Earth disc "
              f"{2 * math.degrees(earth_radius[i]) * 3600:.1f} arcsec | Earth V {earth_v[i]:+.2f} "
              f"({10 ** (-0.4 * (earth_v[i] - SIRIUS_V)):.2f} x Sirius) | map flux {earth_flux[i]:.4e} sr")

    light = euler(*DEFAULT_LIGHT_EULER)
    sun_world = tuple(-c for c in quat_rot(light, (0, 0, 1)))
    q0 = sky_from_world(sun_world, sun_dir[0], (0, 1, 0), ecl_north)
    print(f"default light quaternion (x,y,z,w) {tuple(round(c, 7) for c in light)}")
    print(f"Sun world direction = -light.forward = {tuple(round(c, 6) for c in sun_world)}")
    print(f"day-0 sky_from_world quaternion (x,y,z,w) = {tuple(round(c, 9) for c in q0)}")
    assert math.dist(quat_rot(q0, sun_world), sun_dir[0]) < 1e-9
    inv = quat_inv(q0)

    def describe(name, v):
        w = quat_rot(inv, v)
        el = math.degrees(math.asin(max(-1.0, min(1.0, w[1]))))
        hd = math.degrees(math.atan2(w[0], w[2]))
        print(f"  {name:30s} world ({w[0]:+.4f}, {w[1]:+.4f}, {w[2]:+.4f})  elevation {el:+6.1f}  heading {hd:+7.1f}")

    print("day-0 world directions (elevation above world XZ plane; heading = atan2(x, z), Unity yaw):")
    describe("Sun (equals -light.forward)", sun_dir[0])
    describe("Earth (from Mars proxy)", earth_dir[0])
    describe("ecliptic north pole", ecl_north)
    describe("galactic centre (Sgr A*)", gal_centre)
    describe("north galactic pole", gal_north)
    for n, ra, dec in (("Sirius", 101.2872, -16.7161), ("Betelgeuse", 88.7929, 7.4071),
                       ("Rigel", 78.6345, -8.2016), ("Polaris", 37.9529, 89.2641),
                       ("Vega", 279.2347, 38.7837), ("Canopus", 95.9880, -52.6957)):
        describe(n, eq_to_sky(radec(ra, dec)))
    tilt = math.degrees(math.acos(dot(quat_rot(inv, ecl_north), (0, 1, 0))))
    sun_ecl = math.degrees(math.asin(dot(sun_dir[0], ecl_north)))
    print(f"Sun ecliptic latitude seen from Mars, day 0: {sun_ecl:+.3f} deg")
    print(f"angle between world +Y and ecliptic north (ship tilt forced by the authored light) = {tilt:.3f} deg")

    def vec(v):
        return "new Vector3(" + ", ".join(f"{c:.9f}f" for c in v) + ")"

    lines = [
        "// <auto-generated> by Tools~/celestial_sky_table.py from committed NASA/JPL Horizons data. Do not edit by hand.",
        "using UnityEngine;",
        "",
        "namespace OutOfReach.Presentation.CelestialSky",
        "{",
        "    /// <summary>Daily geometric unit directions seen from the Mars-centre proxy observer, 2026-11-14..2026-12-15",
        "    /// 08:00 UTC, in the panoramic sky frame (+X = RA 0h, +Y = celestial north, +Z = RA 6h; ICRF/J2000 axes).</summary>",
        "    public static class CelestialEphemeris2026",
        "    {",
        '        public const string FirstEpochUtc = "2026-11-14T08:00:00Z";',
        f'        public const string OrbitsSha256 = "{ORBITS_SHA}";',
        f'        public const string GeocentricSha256 = "{GEO_SHA}";',
        "        public const double ObliquityArcsec = 84381.448;",
        f"        public static readonly Vector3 EclipticNorth = {vec(ecl_north)};",
        f"        public static readonly Vector3 GalacticNorth = {vec(gal_north)};",
        f"        public static readonly Vector3 GalacticCentre = {vec(gal_centre)};",
        "        /// <summary>Day-0 rotation for the default authored light Euler(34,-55,0); the material's static fallback.</summary>",
        f"        public static readonly Quaternion DefaultSkyFromWorld = new Quaternion({q0[0]:.9f}f, {q0[1]:.9f}f, "
        f"{q0[2]:.9f}f, {q0[3]:.9f}f);",
        "        public static readonly Vector3[] SunFromObserver =",
        "        {",
    ]
    lines += [f"            {vec(v)}, // day {i}" for i, v in enumerate(sun_dir)]
    lines += ["        };", "        public static readonly Vector3[] EarthFromObserver =", "        {"]
    lines += [f"            {vec(v)}, // day {i}" for i, v in enumerate(earth_dir)]
    lines += ["        };",
              "        /// <summary>Sun angular radius, radians: asin(695700 km / Sun-observer distance).</summary>",
              "        public static readonly float[] SunAngularRadius =", "        {"]
    lines += [f"            {r:.9e}f, // day {i}: {2 * math.degrees(r):.4f} deg across" for i, r in enumerate(sun_radius)]
    lines += ["        };",
              "        /// <summary>Earth V magnitude: NASA V(1,0) -3.99, Horizons distances, Lambert phase law.</summary>",
              "        public static readonly float[] EarthMagnitudeV =", "        {"]
    lines += [f"            {v:.4f}f, // day {i}" for i, v in enumerate(earth_v)]
    lines += ["        };",
              "        /// <summary>Earth flux in starmap_2020_8k texture units x steradian (Sirius-calibrated).</summary>",
              "        public static readonly float[] EarthMapFlux =", "        {"]
    lines += [f"            {f:.9e}f, // day {i}" for i, f in enumerate(earth_flux)]
    lines += ["        };", "    }", "}", ""]
    text = "\n".join(lines)
    if write:
        OUT.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {OUT.relative_to(ROOT).as_posix()}")
    elif not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
        sys.exit(f"{OUT.relative_to(ROOT).as_posix()} is missing or stale; run with --write")
    print("CELESTIAL_SKY_TABLE_PASS")


if __name__ == "__main__":
    main("--write" in sys.argv)

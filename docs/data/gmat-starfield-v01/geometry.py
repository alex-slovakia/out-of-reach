"""Offline observer geometry; fictional sample positions, never NASA ship telemetry."""
import csv
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
C = 299792.458
RADII_KM = {"Earth": 6371.0084, "Mars": 3389.50}  # JPL mean radii; spherical approximation.


def norm(v):
    return math.sqrt(sum(x*x for x in v))


def unit(v):
    n = norm(v)
    if not math.isfinite(n) or n == 0:
        raise ValueError("Direction requires a finite, non-zero displacement")
    return [x/n for x in v]


def dot(a, b):
    return sum(x*y for x,y in zip(a,b))


def mapped(v):
    return [v[1], v[2], v[0]]


def target(body, body_icrf_km, observer_icrf_km):
    v = [a-b for a,b in zip(body_icrf_km, observer_icrf_km)]
    d = norm(v)
    radius = RADII_KM[body]
    if d <= radius:
        raise ValueError("Observer is on/inside body; external sky-disc model invalid")
    n = unit(v)
    diameter = 2*math.asin(radius/d)
    return {"distance_km": d, "direction_icrf": n, "direction_unity_celestial": mapped(n),
            "ra_deg": math.degrees(math.atan2(n[1],n[0])) % 360,
            "dec_deg": math.degrees(math.atan2(n[2], math.hypot(n[0], n[1]))),
            "angular_diameter_deg": math.degrees(diameter),
            "angular_diameter_arcsec": math.degrees(diameter)*3600,
            "disc_diameter_at_1000_unity_units": 2000*math.tan(diameter/2)}


def products():
    rows = list(csv.DictReader((ROOT / "earth-mars.reference.csv").open(newline="")))
    results=[]
    for idx in (0, 30, 31):
        row=rows[idx]
        m=[float(row[k]) for k in ("x_km","y_km","z_km")]
        r=norm(m); u=unit(m)
        north=[0.,0.,1.]
        tangent=unit([north[i]-dot(north,u)*u[i] for i in range(3)])
        # A deliberately authored position with EXACT C04 Earth distance, 50,000 km from Mars.
        theta=2*math.asin(50000/(2*r))
        near=[r*(math.cos(theta)*u[i]+math.sin(theta)*tangent[i]) for i in range(3)]
        positions={"near_mars_same_earth_range_50000km":near,
                   "geometric_midpoint_comparison_only":[0.5*x for x in m],
                   "earth_departure_20000km_comparison_only":[20000*x for x in u]}
        for name,s in positions.items():
            earth=target("Earth",[0,0,0],s); mars=target("Mars",m,s)
            sep=math.degrees(math.acos(max(-1,min(1,dot(earth["direction_icrf"],mars["direction_icrf"])))))
            results.append({"sample_index":idx,"epoch_utc":row["epoch_utc"],"scenario":name,
                            "observer_earth_relative_icrf_km":s,"Earth":earth,"Mars":mars,
                            "earth_mars_separation_deg":sep,"earth_one_way_seconds":earth["distance_km"]/C,
                            "earth_minimum_frozen_round_trip_seconds":2*earth["distance_km"]/C,
                            "existing_c04_one_way_seconds":float(row["one_way_seconds"]),
                            "compatible_with_current_c04_range":name.startswith("near_mars")})
    max_daily_sky_step=0
    for a,b in zip(rows,rows[1:]):
        va=unit([float(a[k]) for k in ("x_km","y_km","z_km")])
        vb=unit([float(b[k]) for k in ("x_km","y_km","z_km")])
        max_daily_sky_step=max(max_daily_sky_step,math.degrees(math.acos(max(-1,min(1,dot(va,vb))))))
    return {"reference":"jpl-horizons-earth-mars-2026-v1","radii_mean_km":RADII_KM,
            "physical_model":"simultaneous geometric centres, spherical discs, no apparent-place corrections",
            "ship_model":"authored static sample positions, not a dynamically propagated trajectory",
            "max_adjacent_reference_direction_change_deg":max_daily_sky_step,"samples":results}


def verify(data):
    for row in data["samples"]:
        for body in ("Earth","Mars"):
            t=row[body]
            assert abs(norm(t["direction_unity_celestial"])-1)<1e-12
            reconstructed=math.sin(math.radians(t["angular_diameter_deg"])/2)*t["distance_km"]
            assert abs(reconstructed-RADII_KM[body])<1e-8
        if row["compatible_with_current_c04_range"]:
            assert abs(row["earth_one_way_seconds"]-row["existing_c04_one_way_seconds"])<1e-9
            assert abs(row["Mars"]["distance_km"]-50000)<1e-6
        if row["scenario"].startswith("geometric_midpoint"):
            assert abs(row["earth_mars_separation_deg"]-180)<2e-6
    for observer in ([0,0,0],[100,0,0],[RADII_KM["Earth"],0,0]):
        try:
            target("Earth",[0,0,0],observer)
        except ValueError:
            pass
        else:
            raise AssertionError("Should reject observer on/inside Earth")
    # Ship-local inverse yaw: celestial +X becomes ship +Z for a +90-degree yaw.
    direction=[1,0,0]
    yaw=math.pi/2
    local=[math.cos(yaw)*direction[0]-math.sin(yaw)*direction[2],direction[1],
           math.sin(yaw)*direction[0]+math.cos(yaw)*direction[2]]
    assert math.dist(local,[0,0,1])<1e-12
    return {"result":"PASS","sample_count":len(data["samples"]),
            "checks":["unit vectors","spherical angular diameters","same-range C04 compatibility",
                      "50,000 km Mars separation","midpoint antipodes","invalid inside-body observer","inverse ship yaw"]}


if __name__ == "__main__":
    import argparse
    p=argparse.ArgumentParser(); p.add_argument("--build",action="store_true"); args=p.parse_args()
    data=products(); encoded=(json.dumps(data,indent=2,allow_nan=False)+"\n").encode()
    if args.build:
        (ROOT/'planet-samples.json').write_bytes(encoded)
    else:
        assert (ROOT/'planet-samples.json').read_bytes()==encoded
    print(json.dumps(verify(data),indent=2))

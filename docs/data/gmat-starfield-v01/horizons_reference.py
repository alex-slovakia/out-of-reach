"""Offline-only verification of the bundled NASA/JPL Horizons reference.

Adapted from tools/nasa/horizons_snapshot.py; parser and normalized bytes retained.
Python 3.10+, standard library only. No network or file writes.
"""

from __future__ import annotations

import csv
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import re
from urllib.parse import urlencode


DATA = Path(__file__).resolve().parent
RAW = DATA / "earth-mars.reference.raw.json"
CSV = DATA / "earth-mars.reference.csv"
MANIFEST = DATA / "earth-mars.reference.manifest.json"
SPEED_OF_LIGHT_KM_S = 299792.458
ENDPOINT = "https://ssd.jpl.nasa.gov/api/horizons.api"
PARAMETERS = {
    "format": "json",
    "COMMAND": "'499'",
    "CENTER": "'500@399'",
    "MAKE_EPHEM": "'YES'",
    "EPHEM_TYPE": "'VECTORS'",
    "START_TIME": "'2026-11-14 08:00'",
    "STOP_TIME": "'2026-12-15 08:00'",
    "STEP_SIZE": "'1 d'",
    "TIME_TYPE": "'UT'",
    "OUT_UNITS": "'KM-S'",
    "REF_SYSTEM": "'ICRF'",
    "REF_PLANE": "'FRAME'",
    "VEC_CORR": "'NONE'",
    "VEC_TABLE": "'3'",
    "CSV_FORMAT": "'YES'",
    "OBJ_DATA": "'YES'",
}
FIELDS = [
    "epoch_utc", "julian_day_ut", "x_km", "y_km", "z_km",
    "earth_mars_distance_km", "one_way_seconds", "minimum_round_trip_seconds",
]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def extract(raw: bytes) -> tuple[list[dict], dict]:
    payload = json.loads(raw)
    require("error" not in payload, f"Horizons API error: {payload.get('error')}")
    result = payload.get("result", "")
    checks = {
        r"Target body name:\s+Mars \(499\)": "Mars target",
        r"Center body name:\s+Earth \(399\)": "Earth origin",
        r"Center-site name:\s+BODY CENTER": "geocentric origin",
        r"Output units\s*:\s*KM-S": "kilometres and seconds",
        r"Reference frame\s*:\s*ICRF": "ICRF reference frame",
        r"Geometric state vectors have NO corrections": "uncorrected geometric vectors",
        r"JDUT\s*,\s*Calendar Date \(UT\s*\),\s*X,\s*Y,\s*Z,\s*VX,\s*VY,\s*VZ,\s*LT,\s*RG,\s*RR": "expected CSV column order",
    }
    for pattern, description in checks.items():
        require(re.search(pattern, result) is not None, f"Unexpected source metadata: {description}")
    require(result.count("$$SOE") == 1 and result.count("$$EOE") == 1, "Missing/ambiguous ephemeris table")
    table = result.split("$$SOE", 1)[1].split("$$EOE", 1)[0].strip()
    rows = []
    start = datetime(2026, 11, 14, 8, tzinfo=timezone.utc)
    for index, values in enumerate(csv.reader(io.StringIO(table))):
        values = [value.strip() for value in values]
        require(len(values) == 12 and values[-1] == "", "Unexpected vector column count")
        epoch = datetime.strptime(values[1], "A.D. %Y-%b-%d %H:%M:%S.%f").replace(tzinfo=timezone.utc)
        require(epoch == start + timedelta(days=index), "Unexpected epoch or daily spacing")
        jd, x, y, z = map(float, [values[0], *values[2:5]])
        source_light_time, source_range = map(float, values[8:10])
        require(all(math.isfinite(float(values[i])) for i in [0, *range(2, 11)]), "Non-finite source value")
        distance = math.sqrt(x * x + y * y + z * z)
        one_way = distance / SPEED_OF_LIGHT_KM_S
        require(distance > 0, "Non-positive distance")
        require(math.isclose(distance, source_range, rel_tol=1e-12), "Vector norm disagrees with Horizons RG")
        require(math.isclose(one_way, source_light_time, rel_tol=1e-12), "Derived delay disagrees with Horizons LT")
        require(abs(jd - (2461358.833333333 + index)) < 1e-7, "Unexpected Julian date")
        rows.append({
            "epoch_utc": epoch.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "julian_day_ut": f"{jd:.9f}",
            "x_km": f"{x:.9f}",
            "y_km": f"{y:.9f}",
            "z_km": f"{z:.9f}",
            "earth_mars_distance_km": f"{distance:.9f}",
            "one_way_seconds": f"{one_way:.9f}",
            "minimum_round_trip_seconds": f"{2 * one_way:.9f}",
        })
    require(len(rows) == 32, f"Expected 32 inclusive daily samples; got {len(rows)}")
    return rows, payload.get("signature", {})


def csv_bytes(rows: list[dict]) -> bytes:
    output = io.StringIO(newline="")
    writer = csv.DictWriter(output, fieldnames=FIELDS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return output.getvalue().encode("utf-8")


def verify() -> list[dict]:
    raw, normalized = RAW.read_bytes(), CSV.read_bytes()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    require(sha256(raw) == manifest["raw_sha256"], "Raw response SHA-256 mismatch")
    require(sha256(normalized) == manifest["normalized_sha256"], "Normalized CSV SHA-256 mismatch")
    require(manifest["request_parameters"] == PARAMETERS, "Manifest parameters changed")
    require(manifest["request_url"] == ENDPOINT + "?" + urlencode(PARAMETERS), "Manifest URL changed")
    require(manifest["speed_of_light_km_per_second"] == SPEED_OF_LIGHT_KM_S, "Wrong speed of light")
    rows, signature = extract(raw)
    require(signature == manifest["api_signature"], "API signature mismatch")
    require(normalized == csv_bytes(rows), "Normalized CSV differs from raw-derived data")
    require(manifest["sample_count"] == len(rows), "Manifest sample count mismatch")
    require(manifest["first_epoch_utc"] == rows[0]["epoch_utc"], "Manifest first epoch mismatch")
    require(manifest["last_epoch_utc"] == rows[-1]["epoch_utc"], "Manifest last epoch mismatch")
    return rows


def main() -> None:
    rows = verify()
    print(f"PASS: {len(rows)} samples, source metadata, raw/CSV SHA-256, vector norms, light times, and exact offline regeneration.")
    for row in [rows[0], rows[-1]]:
        print(f"{row['epoch_utc']}: {float(row['earth_mars_distance_km']):,.3f} km; one-way {float(row['one_way_seconds']):.6f} s; minimum RTT {float(row['minimum_round_trip_seconds']):.6f} s")


if __name__ == "__main__":
    main()

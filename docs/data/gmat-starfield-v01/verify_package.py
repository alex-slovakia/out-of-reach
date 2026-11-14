"""Offline, read-only verification of the standalone GMAT and Horizons package."""
import sys
sys.dont_write_bytecode = True
if sys.flags.optimize:
    raise RuntimeError("Verification requires assertions; do not run Python with -O.")

import hashlib
import json
import math
from pathlib import Path
import gmat_catalogue
import geometry
import horizons_reference

ROOT = Path(__file__).resolve().parent
PAYLOAD_FILES = {
    '.gitattributes', 'README.md', 'THIRD-PARTY-NOTICE.md',
    'gmat-stars.raw.txt', 'gmat-stars.csv', 'gmat-stars.json',
    'gmat-LICENSE.txt', 'gmat-README.txt', 'gmat-GLStars.cpp',
    'gmat-retrieval.json', 'gmat_catalogue.py',
    'earth-mars.reference.raw.json', 'earth-mars.reference.csv',
    'earth-mars.reference.manifest.json', 'horizons_reference.py',
    'geometry.py', 'planet-samples.json', 'planet-radii-source.html',
    'CelestialDirectionsExample.cs', 'source-audit.json',
    'challenge-resources.evidence.json', 'verify_package.py',
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify_payload():
    manifest_bytes = (ROOT / 'manifest.json').read_bytes()
    manifest = json.loads(manifest_bytes)
    assert set(manifest['files']) == PAYLOAD_FILES, 'Unexpected manifest payload'
    for name, expected in manifest['files'].items():
        data = (ROOT / name).read_bytes()
        assert len(data) == expected['bytes'], name
        assert sha(data) == expected['sha256'], name

    rows, files = gmat_catalogue.products()
    for name, data in files.items():
        assert (ROOT / name).read_bytes() == data, name
    gmat = gmat_catalogue.verify(rows)
    for ra, dec, expected in [(0, 0, (0, 0, 1)), (90, 0, (1, 0, 0)),
                              (180, 0, (0, 0, -1)), (270, 0, (-1, 0, 0)),
                              (0, 90, (0, 1, 0)), (240, -90, (0, -1, 0))]:
        assert math.dist(gmat_catalogue.direction(ra, dec), expected) < 1e-14
    assert math.dist(gmat_catalogue.direction(359.999999, 10),
                     gmat_catalogue.direction(0.000001, 10)) < 4e-8
    assert abs(10**(-0.4*0) / 10**(-0.4*5) - 100) < 1e-12
    for row in rows:
        x, y, z = (row['unity_' + axis] for axis in 'xyz')
        ra = math.degrees(math.atan2(x, z)) % 360
        dec = math.degrees(math.atan2(y, math.hypot(x, z)))
        assert abs((ra-row['ra_deg']+180) % 360-180) < 1e-7
        assert abs(dec-row['dec_deg']) < 1e-8
        assert row['bv_color'] is None
        assert row['source_visual_magnitude'] <= 5.5 or row['source_line'] == 1
    gmat['additional_checks'] = ['cardinal axes and poles', 'RA seam',
                                'all inverse RA/Dec', 'five magnitudes = 100x flux',
                                'selection threshold and absent color']

    reference = horizons_reference.verify()
    raw = horizons_reference.RAW.read_bytes()
    for damaged in (raw[:len(raw)//2],
                    raw.replace(b'Target body name: Mars (499)', b'Target body name: Mars (498)'),
                    raw.replace(b'Output units    : KM-S', b'Output units    : AU-D')):
        assert damaged != raw, 'Malformed-source test did not change raw bytes'
        try:
            horizons_reference.extract(damaged)
        except (ValueError, KeyError):
            pass
        else:
            raise AssertionError('Invalid Horizons source accepted')

    planets = geometry.products()
    encoded = (json.dumps(planets, indent=2, allow_nan=False) + '\n').encode()
    assert (ROOT / 'planet-samples.json').read_bytes() == encoded
    planet_report = geometry.verify(planets)

    audit = json.loads((ROOT / 'source-audit.json').read_bytes())
    radii_source = (ROOT / 'planet-radii-source.html').read_bytes()
    assert sha(radii_source) == audit['radii_retrieval']['sha256']
    assert len(radii_source) == audit['radii_retrieval']['bytes']
    assert sha((ROOT / 'gmat-LICENSE.txt').read_bytes()) == audit['application_license_sha256']
    return {
        'result': 'PASS',
        'package_id': 'nasa-gmat-starfield-v01-standalone',
        'manifest_sha256': sha(manifest_bytes),
        'payload_files_verified': len(PAYLOAD_FILES),
        'gmat': gmat,
        'horizons': {'result': 'PASS', 'sample_count': len(reference),
                     'checks': ['raw/CSV hashes', 'exact CSV regeneration',
                                'target/origin/frame/units', '32 exact daily UTC epochs',
                                'vector norm versus source RG', 'range/c versus source LT',
                                'truncated response, wrong target and wrong unit rejected']},
        'planet_geometry': planet_report,
        'BSC_data_included': False,
        'Unity_compile_render_build_playtest': 'NOT RUN',
    }


def verify():
    actual = {path.name for path in ROOT.iterdir() if path.is_file()}
    expected = PAYLOAD_FILES | {'manifest.json', 'SHA256SUMS.txt', 'verification.json'}
    assert actual == expected, 'Unexpected or missing package files: ' + str(actual ^ expected)
    report = verify_payload()
    checksum_entries = {}
    for line in (ROOT / 'SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
        digest, name = line.split('  ', 1)
        assert name in expected and name not in checksum_entries, name
        assert sha((ROOT / name).read_bytes()) == digest, name
        checksum_entries[name] = digest
    assert set(checksum_entries) == expected - {'SHA256SUMS.txt'}
    evidence = json.loads((ROOT / 'verification.json').read_bytes())
    assert evidence['report'] == report, 'Saved evidence does not describe this payload'
    report['checksum_entries_verified'] = len(checksum_entries)
    report['saved_evidence_matches_payload'] = True
    print(json.dumps(report, indent=2))
    return report


if __name__ == '__main__':
    verify()

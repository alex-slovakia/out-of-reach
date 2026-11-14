"""Deterministic numerical GMAT projection. Default verifies; --build writes this staging folder only."""
import sys
sys.dont_write_bytecode = True
if sys.flags.optimize:
    raise RuntimeError('Run without -O; numerical verification uses assertions.')
import argparse
from datetime import datetime, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version
from PIL.PngImagePlugin import PngInfo

ROOT = Path(__file__).resolve().parent
SIZE = 1024
SIGMA_RAD = 0.0009
SUPPORT_SIGMA = 5.0
EXPOSURE = 4.0
# Normal, image-right, image-up. Unity's Left texture is +X, Right is -X.
BASES = {
    'Front': ((0, 0, 1), (1, 0, 0), (0, 1, 0)),
    'Back': ((0, 0, -1), (-1, 0, 0), (0, 1, 0)),
    'Left': ((1, 0, 0), (0, 0, -1), (0, 1, 0)),
    'Right': ((-1, 0, 0), (0, 0, 1), (0, 1, 0)),
    'Up': ((0, 1, 0), (1, 0, 0), (0, 0, -1)),
    'Down': ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def load_stars():
    provenance = json.loads((ROOT / 'input-provenance.json').read_bytes())
    for name, expected in provenance['source_input_files'].items():
        data = (ROOT / 'source' / name).read_bytes()
        assert len(data) == expected['bytes'] and sha(data) == expected['sha256'], name
    data = json.loads((ROOT / 'source/gmat-stars.json').read_bytes())
    assert data['source_commit'] == provenance['gmat_commit']
    rows = [dict(zip(data['columns'], values)) for values in data['stars']]
    assert len(rows) == 2797
    unique = {}
    for row in rows:
        key = (row['ra_deg'], row['dec_deg'])
        if key not in unique or row['source_visual_magnitude'] < unique[key]['source_visual_magnitude']:
            unique[key] = row
    stars = sorted(unique.values(), key=lambda row: row['source_line'])
    assert len(stars) == 2785
    for row in stars:
        a, d = math.radians(row['ra_deg']), math.radians(row['dec_deg'])
        expected = (math.cos(d)*math.sin(a), math.sin(d), math.cos(d)*math.cos(a))
        v = np.array([row['unity_' + axis] for axis in 'xyz'], dtype=np.float64)
        assert np.max(np.abs(v - expected)) < 1e-11
        assert abs(np.linalg.norm(v) - 1) < 1e-11
        row['direction'] = v / np.linalg.norm(v)
        row['flux'] = 10**(-0.4*row['source_visual_magnitude'])
        assert row['bv_color'] is None
    polaris = next(row for row in stars if row['source_line'] == 1)
    assert polaris['source_visual_magnitude'] == 6.0
    return stars


def ray(face, s, t):
    n, right, up = np.array(BASES[face], dtype=np.float64)
    v = n + np.asarray(s)[..., None]*right + np.asarray(t)[..., None]*up
    return v / np.linalg.norm(v, axis=-1, keepdims=True)


def project(face, direction):
    n, right, up = np.array(BASES[face], dtype=np.float64)
    depth = np.dot(direction, n)
    if depth <= 0:
        return None
    return float(np.dot(direction, right)/depth), float(np.dot(direction, up)/depth)


def coordinates(indices):
    # Usual pixel centers, with the boundary centers fixed to the exact face edge.
    # This lets clamp/bilinear six-sided skyboxes share identical edge samples.
    result = 2*(np.asarray(indices, dtype=np.float64) + 0.5)/SIZE - 1
    return np.where(np.asarray(indices) == 0, -1.0,
                    np.where(np.asarray(indices) == SIZE-1, 1.0, result))


def png_bytes(pixels):
    output = io.BytesIO()
    notice = PngInfo()
    notice.add_text('Source', 'NASA GMAT inp_StarCatalog.txt; commit b8aec552a8f34a9212063adc3f249bd2315397a3')
    notice.add_text('License', 'Apache-2.0; retain full GMAT license and OUT OF REACH modification notice')
    notice.add_text('Copyright', 'Copyright (c) 2002-2026 United States Government as represented by the Administrator of NASA. All Rights Reserved.')
    notice.add_text('Modifications', 'OUT OF REACH 2026-11-14: subset; brightest exact-coordinate duplicate retained; six-sided projection; neutral artistic point spread/exposure; stellar epoch unspecified.')
    Image.fromarray(pixels, 'RGB').save(output, format='PNG', compress_level=9, optimize=False, pnginfo=notice)
    return output.getvalue()


def render(stars):
    images, report = {}, {}
    support = SIGMA_RAD * SUPPORT_SIGMA
    patch_radius = math.ceil(SIZE*support*2.5) + 2
    for face in BASES:
        light = np.zeros((SIZE, SIZE), dtype=np.float64)
        included = []
        for star in stars:
            uv = project(face, star['direction'])
            if uv is None or max(abs(uv[0]), abs(uv[1])) > 1 + 4*support:
                continue
            s, t = uv
            cx, cy = (s+1)*SIZE/2-0.5, (1-t)*SIZE/2-0.5
            x0, x1 = max(0, math.floor(cx)-patch_radius), min(SIZE, math.ceil(cx)+patch_radius+1)
            y0, y1 = max(0, math.floor(cy)-patch_radius), min(SIZE, math.ceil(cy)+patch_radius+1)
            if x0 >= x1 or y0 >= y1:
                continue
            xs, ys = np.meshgrid(coordinates(np.arange(x0, x1)), -coordinates(np.arange(y0, y1)))
            rays = ray(face, xs, ys)
            chord_squared = np.maximum(0, 2*(1 - rays @ star['direction']))
            values = EXPOSURE*star['flux']*np.exp(-chord_squared/(2*SIGMA_RAD**2))
            values[chord_squared > support**2] = 0
            light[y0:y1, x0:x1] += values
            included.append(star['source_line'])
        # Fixed artistic exposure and soft response, then standard sRGB encoding.
        linear = -np.expm1(-light)
        srgb = np.where(linear <= 0.0031308, 12.92*linear, 1.055*linear**(1/2.4)-0.055)
        gray = np.clip(np.floor(srgb*255 + 0.5), 0, 255).astype(np.uint8)
        images[face] = np.repeat(gray[..., None], 3, axis=-1)
        report[face] = {'source_rows_touching_face': len(included),
                        'nonblack_pixels': int(np.count_nonzero(gray)),
                        'pixels_at_least_128': int(np.count_nonzero(gray >= 128)),
                        'max_srgb_value': int(gray.max()),
                        'mean_srgb_value': float(gray.mean())}
    return images, report


def edges(face, images):
    q = coordinates(np.arange(SIZE))
    return [
        ('left', ray(face, np.full(SIZE, -1.), -q), images[face][:, 0]),
        ('right', ray(face, np.full(SIZE, 1.), -q), images[face][:, -1]),
        ('top', ray(face, q, np.full(SIZE, 1.)), images[face][0, :]),
        ('bottom', ray(face, q, np.full(SIZE, -1.)), images[face][-1, :]),
    ]


def verify_math(stars, images):
    # These expected axis mappings are independent of the projection implementation.
    for ra, dec, face in [(0, 0, 'Front'), (90, 0, 'Left'), (180, 0, 'Back'),
                           (270, 0, 'Right'), (0, 90, 'Up'), (0, -90, 'Down')]:
        a, d = math.radians(ra), math.radians(dec)
        v = np.array([math.cos(d)*math.sin(a), math.sin(d), math.cos(d)*math.cos(a)])
        uv = project(face, v)
        assert uv is not None and max(abs(uv[0]), abs(uv[1])) < 1e-14
        assert np.max(np.abs(ray(face, 0., 0.)-v)) < 1e-14
    evidence = json.loads((ROOT / 'unity-orientation-evidence.json').read_bytes())
    for face, vertices in evidence['vertices_by_face'].items():
        for x, y, z, u, v in vertices:
            expected = np.array([x, y, z])/math.sqrt(3)
            assert np.max(np.abs(ray(face, 2*u-1, 2*v-1)-expected)) < 1e-14
    max_error = 0.
    ownership = {face: 0 for face in BASES}
    for star in stars:
        face = max(BASES, key=lambda f: np.dot(star['direction'], BASES[f][0]))
        uv = project(face, star['direction'])
        assert uv is not None and max(abs(uv[0]), abs(uv[1])) <= 1+1e-12
        max_error = max(max_error, float(np.max(np.abs(ray(face, *uv)-star['direction']))))
        ownership[face] += 1
    assert max_error < 1e-14
    all_edges = [(face, *item) for face in BASES for item in edges(face, images)]
    matched = set()
    for i, (face, edge, directions, pixels) in enumerate(all_edges):
        if i in matched:
            continue
        candidates = []
        for j, (other_face, other_edge, other_directions, other_pixels) in enumerate(all_edges):
            if i == j or other_face == face:
                continue
            for reverse in (False, True):
                d = other_directions[::-1] if reverse else other_directions
                if np.max(np.abs(d-directions)) < 1e-14:
                    p = other_pixels[::-1] if reverse else other_pixels
                    assert np.array_equal(p, pixels), f'Visible edge mismatch: {face}/{edge} {other_face}/{other_edge}'
                    candidates.append(j)
        assert len(candidates) == 1, (face, edge, candidates)
        matched.add(i); matched.add(candidates[0])
    assert len(matched) == 24
    for pixels in images.values():
        assert pixels.shape == (SIZE, SIZE, 3)
        assert np.array_equal(pixels[..., 0], pixels[..., 1]) and np.array_equal(pixels[..., 1], pixels[..., 2])
        assert np.count_nonzero(pixels[..., 0]) / SIZE**2 < 0.03
    return {'result': 'PASS', 'catalogue_rows': 2797, 'unique_directions': len(stars),
            'discarded_exact_position_duplicates': 12, 'source_rows_per_primary_face': ownership,
            'double_precision_max_round_trip_component_error': max_error,
            'unity_mesh_vertices_verified': 36, 'axis_pole_checks': 6,
            'exact_matching_face_edge_pairs': 12, 'matching_edge_samples_per_pair': SIZE,
            'all_rgb_channels_neutral': True,
            'translation_parallax': 'Direction-only skybox data; no camera position input. Actual Unity walk test pending.',
            'Unity_render': 'NOT RUN'}


def contact_sheet(images):
    sheet = Image.new('RGB', (1560, 1180), (12, 15, 19))
    draw = ImageDraw.Draw(sheet)
    font_path = 'C:/Windows/Fonts/segoeui.ttf'
    heading = ImageFont.truetype(font_path, 27)
    small = ImageFont.truetype(font_path, 18)
    draw.text((24, 14), 'Calculated star projection - NASA GMAT catalogue', fill=(229, 233, 237), font=heading)
    draw.text((24, 52), '2,785 unique directions | 1024 px faces | artistic exposure | NOT a Unity capture', fill=(163, 175, 186), font=small)
    for i, (face, pixels) in enumerate(images.items()):
        x, y = 16 + (i % 3)*520, 96 + (i // 3)*540
        thumb = Image.fromarray(pixels).resize((504, 504), Image.Resampling.LANCZOS)
        sheet.paste(thumb, (x, y+24))
        draw.text((x, y), f'{face}  {BASES[face][0]}', fill=(201, 210, 218), font=small)
    output = io.BytesIO()
    sheet.save(output, 'PNG', compress_level=9)
    return output.getvalue()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--build', action='store_true')
    args = p.parse_args()
    stars = load_stars()
    images, metrics = render(stars)
    report = verify_math(stars, images)
    products = {'textures/Stars'+face+'.png': png_bytes(pixels) for face, pixels in images.items()}
    products['calculated-star-projection.png'] = contact_sheet(images)
    for name, data in products.items():
        if args.build:
            (ROOT / name).parent.mkdir(parents=True, exist_ok=True)
            (ROOT / name).write_bytes(data)
        else:
            assert (ROOT / name).read_bytes() == data, name
    report['face_metrics'] = metrics
    report['png_byte_identical_regeneration'] = True
    report['Pillow'] = pillow_version
    report['NumPy'] = np.__version__
    if args.build:
        evidence = {'executed_at_utc': datetime.now(timezone.utc).isoformat(),
                    'command': 'python -B generate_sky.py --build', 'report': report}
        (ROOT / 'verification.json').write_bytes((json.dumps(evidence, indent=2)+'\n').encode())
        names = sorted(str(path.relative_to(ROOT)).replace('\\', '/') for path in ROOT.rglob('*')
                       if path.is_file() and path.name not in ('manifest.json', 'SHA256SUMS.txt'))
        manifest = {'package_id': 'GmatSkyboxV01', 'status': 'Staged; Unity render and integration pending',
                    'resolution': [SIZE, SIZE], 'format': 'RGB8 PNG, sRGB, neutral stars, black background',
                    'artistic_parameters': {'sigma_radians': SIGMA_RAD, 'support_sigma': SUPPORT_SIGMA,
                                            'exposure': EXPOSURE, 'response': '1-exp(-linear flux); sRGB encoding',
                                            'seams': 'Clamp edge texels sample exact cube boundaries'},
                    'BSC_data_used': False, 'Earth_Mars_added': False,
                    'files': {name: {'bytes': (ROOT/name).stat().st_size, 'sha256': sha((ROOT/name).read_bytes())}
                              for name in names}}
        (ROOT / 'manifest.json').write_bytes((json.dumps(manifest, indent=2)+'\n').encode())
        names.append('manifest.json')
        (ROOT / 'SHA256SUMS.txt').write_bytes(('\n'.join(sha((ROOT/name).read_bytes())+'  '+name for name in sorted(names))+'\n').encode())
    else:
        manifest = json.loads((ROOT/'manifest.json').read_bytes())
        for name, expected in manifest['files'].items():
            data = (ROOT/name).read_bytes()
            assert len(data) == expected['bytes'] and sha(data) == expected['sha256'], name
        for line in (ROOT/'SHA256SUMS.txt').read_text().splitlines():
            digest, name = line.split('  ', 1)
            assert sha((ROOT/name).read_bytes()) == digest, name
        assert json.loads((ROOT/'verification.json').read_bytes())['report'] == report
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()

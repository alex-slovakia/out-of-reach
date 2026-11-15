"""R33 weather readout derivation. Standard library only; run with `python -I derive_slovakia_weather.py <dir>`.

Inputs (all NASA GIBS WMS, EPSG:4326, BBOX 46.9,16.3,50.45,23.1, 1280x1012, TIME=2026-11-14), stored next to this file:
  MODIS_Terra_Cloud_Fraction_Day.png     MOD06 cloud fraction, colormap MODIS_Cloud_Fraction.xml (percent)
  MODIS_Terra_Land_Surface_Temp_Day.png  MOD11 land surface temperature, colormap MODIS_Land_Surface_Temp.xml (K)
Prints JSON with the numbers shown on the terminal.
"""
import json, math, os, re, struct, sys, zlib

W, H = 1280, 1012
SOUTH, WEST, NORTH, EAST = 46.9, 16.3, 50.45, 23.1
# Approximate city-centre coordinates (lat, lon), rounded to 0.01 deg. Readouts use a 5x5-pixel window (~2.6 x 2.0 km).
POINTS = [('BRATISLAVA', 48.15, 17.11), ('ZILINA', 49.22, 18.74), ('BANSKA BYSTRICA', 48.74, 19.15),
          ('POPRAD / TATRAS', 49.06, 20.30), ('KOSICE', 48.72, 21.26)]


def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', path
    pos, idat, ihdr = 8, b'', None
    while pos < len(data):
        length, kind = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b'IHDR': ihdr = struct.unpack('>IIBBBBB', body)
        elif kind == b'IDAT': idat += body
        pos += 12 + length
    w, h, depth, ctype, _, _, interlace = ihdr
    assert depth == 8 and ctype == 6 and interlace == 0, (path, ihdr)
    raw, bpp, stride = zlib.decompress(idat), 4, w * 4
    out, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]; line = bytearray(raw[i + 1:i + 1 + stride]); i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        out.append(bytes(line)); prev = line
    return w, h, out


def colormap(path):
    s = open(path, encoding='utf-8').read()
    table = {}
    for rgb, attrs in re.findall(r'<ColorMapEntry rgb="([^"]+)"([^>]*)/>', s):
        if 'nodata="true"' in attrs: continue
        v = re.search(r'value="([^"]+)"', attrs).group(1)
        nums = [float(n) for n in re.findall(r'-?\d+(?:\.\d+)?', v)]
        table[tuple(int(c) for c in rgb.split(','))] = sum(nums) / len(nums)  # bin centre
    return table


def pixel_area_km2(row):
    lat = NORTH - (row + .5) * (NORTH - SOUTH) / H
    return ((EAST - WEST) / W * 111.32 * math.cos(math.radians(lat))) * ((NORTH - SOUTH) / H * 110.57)


def main(folder):
    layers = {'cloud_fraction_pct': ('MODIS_Terra_Cloud_Fraction_Day.png', 'MODIS_Cloud_Fraction.xml'),
              'land_surface_temperature_k': ('MODIS_Terra_Land_Surface_Temp_Day.png', 'MODIS_Land_Surface_Temp.xml')}
    decoded = {}
    for key, (png, cmap) in layers.items():
        _, _, img = read_png(os.path.join(folder, png)); table = colormap(os.path.join(folder, cmap))
        def value(r, c, img=img, table=table):
            p = img[r]; rgb = (p[c * 4], p[c * 4 + 1], p[c * 4 + 2])
            if p[c * 4 + 3] == 0: return 'nodata'
            return table.get(rgb, 'unmatched')
        decoded[key] = value
    result = {'points': [], 'crop': {}}
    for name, lat, lon in POINTS:
        r0 = int((NORTH - lat) / (NORTH - SOUTH) * H); c0 = int((lon - WEST) / (EAST - WEST) * W)
        entry = {'name': name, 'lat': lat, 'lon': lon, 'row': r0, 'col': c0}
        for key, value in decoded.items():
            vals = [value(r, c) for r in range(r0 - 2, r0 + 3) for c in range(c0 - 2, c0 + 3)]
            nums = sorted(v for v in vals if isinstance(v, float))
            entry[key] = {'valid': len(nums), 'of': 25, 'nodata': vals.count('nodata'), 'unmatched': vals.count('unmatched'),
                          'median': nums[len(nums) // 2] if nums else None}
        lst = entry['land_surface_temperature_k']['median']
        entry['land_surface_temperature_c'] = round(lst - 273.15, 1) if lst is not None else None
        result['points'].append(entry)
    for key, value in decoded.items():
        tot = valid = 0.0; acc = 0.0; unmatched = 0
        for r in range(H):
            w = pixel_area_km2(r)
            for c in range(W):
                v = value(r, c); tot += w
                if isinstance(v, float): valid += w; acc += v * w
                elif v == 'unmatched': unmatched += 1
        result['crop'][key] = {'valid_area_pct': round(100 * valid / tot, 1), 'area_weighted_mean': round(acc / valid, 2) if valid else None,
                               'unmatched_pixels': unmatched}
    print(json.dumps(result, indent=2)); return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))))

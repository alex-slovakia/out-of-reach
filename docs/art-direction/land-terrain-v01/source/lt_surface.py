"""lt_surface - the LAND-TERRAIN-V01 ground as it is actually rendered: the piecewise-linear surface of the delivered
terrain meshes (LT_TerrainInner + LT_TerrainFar, rebuilt from lt_kit with the same fixed seeds), sampled at any (x, z).

The analytic h(x, z) and the mesh agree at the mesh vertices; between them the mesh is linear, so objects seated on the
analytic h could float or sink by up to tens of centimetres on dunes. LAND-SURROUNDINGS v02 is seated on this sampler.
Inside the footprint hole it returns the datum (y -36.6).
"""
import numpy as np

import lt_height as H
import lt_kit


class Surface:
    def __init__(self):
        rings, _ = lt_kit.inner_rings()
        inner, _ = lt_kit._surface_model("LT_TerrainInner", lt_kit.TERRAIN_MAT, rings)
        frings, _ = lt_kit.far_rings(rings[-1])
        far, _ = lt_kit._surface_model("LT_TerrainFar", lt_kit.FAR_MAT, frings)
        self.grids = [self._index(inner, 40.0), self._index(far, 400.0)]

    @staticmethod
    def _index(model, cell):
        (mat,) = model.parts.keys()
        v, t, _ = model.arrays(mat)
        a = v[t[:, 0]]
        b = v[t[:, 1]]
        c = v[t[:, 2]]
        e1 = (b - a)[:, [0, 2]]
        e2 = (c - a)[:, [0, 2]]
        det = e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]
        lo = np.floor(np.minimum(np.minimum(a, b), c)[:, [0, 2]] / cell).astype(int)
        hi = np.floor(np.maximum(np.maximum(a, b), c)[:, [0, 2]] / cell).astype(int)
        cells = {}
        for i in range(len(t)):
            for gx in range(lo[i, 0], hi[i, 0] + 1):
                for gz in range(lo[i, 1], hi[i, 1] + 1):
                    cells.setdefault((gx, gz), []).append(i)
        cells = {k: np.asarray(val) for k, val in cells.items()}
        return {"cell": cell, "cells": cells, "a": a, "e1": e1, "e2": e2, "det": det,
                "ya": a[:, 1], "yb": b[:, 1], "yc": c[:, 1]}

    def _one(self, x, z):
        for g in self.grids:
            cand = g["cells"].get((int(np.floor(x / g["cell"])), int(np.floor(z / g["cell"]))))
            if cand is None:
                continue
            px = x - g["a"][cand, 0]
            pz = z - g["a"][cand, 2]
            e1, e2, det = g["e1"][cand], g["e2"][cand], g["det"][cand]
            u = (px * e2[:, 1] - pz * e2[:, 0]) / det
            w = (e1[:, 0] * pz - e1[:, 1] * px) / det
            ok = (u >= -1e-9) & (w >= -1e-9) & (u + w <= 1.0 + 1e-9)
            if ok.any():
                k = int(np.argmax(ok))
                i = cand[k]
                return float(g["ya"][i] + u[k] * (g["yb"][i] - g["ya"][i]) + w[k] * (g["yc"][i] - g["ya"][i]))
        return H.DATUM

    def y(self, x, z):
        """Ground y of the terrain mesh at (x, z); scalars or arrays."""
        xs, zs = np.broadcast_arrays(np.asarray(x, float), np.asarray(z, float))
        out = np.array([self._one(a, b) for a, b in zip(xs.ravel(), zs.ravel())])
        return out.reshape(xs.shape) if xs.shape else float(out[0])

    def seat(self, x, z, radius):
        """Lowest mesh ground under a round footprint (centre and 8 rim points)."""
        ang = np.arange(8) * np.pi / 4.0
        xs = np.concatenate([[x], x + radius * np.cos(ang)])
        zs = np.concatenate([[z], z + radius * np.sin(ang)])
        return float(self.y(xs, zs).min())


_SURFACE = None


def surface():
    global _SURFACE
    if _SURFACE is None:
        _SURFACE = Surface()
    return _SURFACE


if __name__ == "__main__":
    import time
    t0 = time.time()
    s = surface()
    print("index %.1fs" % (time.time() - t0))
    rng = np.random.default_rng(5)
    r = 80 + 2800 * rng.random(3000)
    a = rng.random(3000) * 2 * np.pi
    x, z = r * np.cos(a), r * np.sin(a)
    t1 = time.time()
    ys = s.y(x, z)
    dy = ys - H.ground_y(x, z)
    print("3000 queries %.2fs; mesh - analytic: p1 %.3f p50 %.3f p99 %.3f max |%.3f|" % (
        time.time() - t1, np.percentile(dy, 1), np.percentile(dy, 50), np.percentile(dy, 99), np.abs(dy).max()))
    print("far", s.y(np.array([5000.0, -8000.0]), np.array([100.0, 2000.0])), "hole", s.y(0.0, 0.0))

# NASA GMAT derived star asset

Source: NASA General Mission Analysis Tool, `application/data/graphics/stars/inp_StarCatalog.txt`, commit `b8aec552a8f34a9212063adc3f249bd2315397a3` in [nasa/GMAT](https://github.com/nasa/GMAT). Exact source URLs and hashes are recorded in `gmat-retrieval.json`.

Copyright (c) 2002-2026 United States Government as represented by the Administrator of the National Aeronautics and Space Administration. All Rights Reserved.

GMAT is supplied under the Apache License, Version 2.0. The full supplied licence is `gmat-LICENSE.txt`. Preserve that licence and this notice with redistributed original or derived GMAT data. The complete original `gmat-README.txt` and source evidence `gmat-GLStars.cpp` retain their upstream notices.

Modifications by OUT OF REACH, 2026-11-14: selected source rows with visual magnitude <=5.5 and the first Polaris marker row; converted RA/Dec to Unity unit directions; calculated relative flux from the source magnitude; normalized into CSV/JSON. Original row numbers, coordinates and magnitudes are retained. Upstream's special `mag=-2` Polaris rendering override is not used. The standalone converter replaces its former research-helper import with local mathematical/hash functions, without changing generated data. No GMAT source code is linked into the game by this package.

The selected raw star file has no separate copyright, stellar epoch or measurement-provenance header. Licensing relies on the source repository's Apache-2.0 licence and the application README. The application licence was checked against the root licence and is byte-identical. No additional licence or notice was present along the star asset's directory path at this pinned commit. This finding applies to the selected files; it does not grant rights to unrelated GMAT dependencies, textures or third-party assets.

This is an independent educational application. Attribution does not imply NASA endorsement. Keep source limitations visible: inferred celestial frame alignment, coarse visual magnitudes, missing color and repeated coordinate pairs.

## NASA/JPL reference data

Planet centers: [NASA/JPL Horizons](https://ssd-api.jpl.nasa.gov/doc/horizons.html), dataset `jpl-horizons-earth-mars-2026-v1`. `earth-mars.reference.manifest.json` contains the complete request, retrieval timestamp, units, frame and hashes; the exact raw response retains NASA/JPL metadata and caveats. Source files were copied from the already committed project snapshot without a new ephemeris request.

Mean radii: [NASA/JPL Solar System Dynamics, Planetary Physical Parameters](https://ssd.jpl.nasa.gov/planets/phys_par.html), Earth 6371.0084 km and Mars 3389.50 km. `planet-radii-source.html` preserves the retrieved source; `source-audit.json` records its retrieval metadata. These values support a spherical visual-disc approximation. The observer positions are fictional authored examples, not NASA measurements or propagated trajectories.

HEASARC BSC5P data and derived comparisons are excluded from this package. [HEASARC's source-specific licensing note](https://heasarc.gsfc.nasa.gov/W3Browse/star-catalog/bsc5p.html) does not grant or clarify commercial rights to that catalogue; no such rights are inferred here.

Suggested short credit, only after actual game integration:

> Star directions: NASA GMAT star graphics data, Apache-2.0; subset converted for OUT OF REACH. Magnitudes are approximate; exposure is artistic. Planet centers: NASA/JPL Horizons; observer position is fictional.

# Image attribution and reuse basis

Image: **NASA EPIC Team / NOAA DSCOVR**.

Natural-color Earth observation. API observation metadata: **2026-11-11 11:01:17 UTC**. Image identifier: `epic_1b_20261111110606`. Retained JPEG bytes are unmodified.

- Image source: https://epic.gsfc.nasa.gov/archive/natural/2026/11/11/jpg/epic_1b_20261111110606.jpg
- Observation metadata: https://epic.gsfc.nasa.gov/api/natural/date/2026-11-11
- Specific image-use policy: https://epic.gsfc.nasa.gov/about
- NASA media guidelines: https://www.nasa.gov/nasa-brand-center/images-and-media/

The NASA EPIC Team makes its materials available for reproduction/reuse, including commercial purposes, and requests credit. This package relies on that explicit source policy; it does not assign an invented Creative Commons licence. NASA is credited as a source, with no endorsement claim. Preserve the observation date and archived status. This is not a forecast, a live feed, or an image captured by the fictional AURORA-7.

# Slovakia static image (R33, default terminal display)

File: `modis_terra_slovakia_20261114_0815utc.jpg` (1280 x 1012, SHA-256 `7e1d762d2daef4c63aeb954d58266849ebe769bd27190aae11c2e0c3a84ae700`). Bytes are exactly as returned by GIBS; nothing is drawn on them.

Image: **NASA GIBS / ESDIS, MODIS on NASA Terra**, layer `MODIS_Terra_CorrectedReflectance_TrueColor`, date 2026-11-14. Slovakia lies entirely inside the Terra pass **08:15:00-08:20:00 UTC (09:15-09:20 CET)**, granule `MOD03.A2026318.0815.061.2026318092449.NRT.hdf`. Slovakia itself was crossed at about 08:16-08:17 UTC (estimate). The far-eastern strip of the crop, east of about 21.8 E, is also touched by the 06:35 UTC pass, and which pass supplied those pixels in the GIBS daily mosaic is not verified. The request was for 08:00 UTC; no NASA frame exists at that exact time (EPIC had no 14 Nov images when this was retrieved), so the actual pass time is shown instead.

Crop: EPSG:4326 lat/lon grid, 46.90-50.45 N, 16.30-23.10 E, about 390 m per pixel.

- Image request: https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS=MODIS_Terra_CorrectedReflectance_TrueColor&STYLES=&CRS=EPSG:4326&BBOX=46.9,16.3,50.45,23.1&WIDTH=1280&HEIGHT=1012&FORMAT=image/jpeg&TIME=2026-11-14
- Pass times: https://cmr.earthdata.nasa.gov/search/granules.json?short_name=MOD03&provider=LANCEMODIS&temporal=2026-11-14T06:00:00Z,2026-11-14T09:00:00Z&bounding_box=16,47,23.5,50.5
- GIBS acknowledgement and usage: https://nasa-gibs.github.io/gibs-api-docs/
- NASA Earth science data use guidance (CC0 unless marked otherwise, citation requested): https://www.earthdata.nasa.gov/engage/open-data-services-software-policies/data-use-guidance

Required acknowledgement: "We acknowledge the use of imagery provided by services from NASA's Global Imagery Browse Services (GIBS), part of NASA's Earth Science Data and Information System (ESDIS)."

This is an archived Earth-orbit observation. It is not live, not a forecast, and not taken by the fictional AURORA-7. No NASA endorsement is stated or implied. Full record: `slovakia-observation-manifest.json`.

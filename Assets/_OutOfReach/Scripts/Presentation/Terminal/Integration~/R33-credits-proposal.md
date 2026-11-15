# R33 in-game credit lines: proposal for the owner of the credits/about UI (not applied)

The terminal already credits its own imagery on the screen:
- Static mode (default): the Credit label reads "Imagery: NASA GIBS / ESDIS, MODIS Terra (LANCE NRT)" with worldview.earthdata.nasa.gov / 2026-11-14.
- EPIC globe mode (`staticImageMode` off): the prefab Credit label reads "Image: NASA EPIC Team / NOAA DSCOVR" with epic.gsfc.nasa.gov / 2026-11-11. In static mode the EPIC image is not displayed.

These lines are proposed for the end credits or the about screen, so the credit survives outside the terminal view:

    Earth imagery: NASA EPIC Team / NOAA DSCOVR (EPIC, 2026-11-11 11:01:17 UTC). epic.gsfc.nasa.gov
    Slovakia imagery and weather readout: NASA Global Imagery Browse Services (GIBS), part of NASA's
    Earth Science Data and Information System (ESDIS). MODIS Terra true colour, land surface temperature
    and cloud fraction, 2026-11-14 (Terra pass 08:15-08:20 UTC). worldview.earthdata.nasa.gov
    We acknowledge the use of imagery provided by services from NASA's Global Imagery Browse Services
    (GIBS), part of NASA's Earth Science Data and Information System (ESDIS).
    NASA does not endorse this game.

Sources and licence basis: `Visuals/Props/MissionTerminal/Data/ATTRIBUTION.md`, `earth-observation-manifest.json` and `slovakia-observation-manifest.json`.

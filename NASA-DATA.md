# NASA data and scientific assumptions

## Archived orbit research — 15 November 2026

Alex explicitly dropped the planet/orbit diagram from Sources. The menu retains the mission's NASA source links and full dataset disclosure; no orbit viewer or planet data is loaded by this UI.

The unused heliocentric research is archived in [reference-orbits](docs/data/reference-orbits/README.md), including raw responses, hashes and `normalized.json`. `python tools/nasa/reference_orbits.py` verifies that archive offline. It does not replace the communications data. The Mars cinematic still uses NASA/JPL Viking imagery on an authored globe.

## Marine prologue presentation boundary

The fictional marine launch has an elevated boarding deck. Its 22-second, 90-metre authored ascent ends with a clearly separate “LATER · ABOARD AURORA-7” chapter. This is cinematic compression, not a calculated launch trajectory, orbital insertion or rendezvous. Orbit is not reached in 22 seconds; the intervening flight is omitted. The real-time loading deadline and loaded equipment remain authoritative before this presentation transition. The deep-space communications model below remains the separately documented Mars-vicinity reference approximation.

## Status

The committed NASA/JPL Horizons dataset now drives C04 communications in the integrated I02 mission. At runtime revision `7e41699`, the 198-check scene harness exercised actual outbound/return delay, processing, reception and reporting. Original-project merge `4784913` contains the same runtime plus two unused optional C05 interfaces and passed Unity import/compilation. This is scripted scene evidence, not a completed manual standalone playthrough. Stars were procedural at that historical checkpoint. The current source audit confirms that all six committed star textures match the GMAT projection manifest and their saved material/scene GUIDs; see the current star provenance below. This audit is not a new GPU or standalone playthrough claim.

The objective is an understandable educational simulation. OUT OF REACH is not NASA-certified flight software, and including a dataset does not by itself guarantee award eligibility.

## Sources

| ID | Source | What it supports | Use/status |
| --- | --- | --- | --- |
| NASA-01 | [NASA/JPL Horizons API](https://ssd-api.jpl.nasa.gov/doc/horizons.html) and the exact query in [manifest.json](docs/data/horizons/manifest.json) | Computed Earth-to-Mars geometric positions and range for specified epochs | Integrated into C04/I02 communications; exact revision and evidence above |
| NASA-02 | [NASA Environmental Control and Life Support Systems](https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/) | Life support has distinct oxygen generation, air revitalization and water recovery functions. Removing CO2 and supplying oxygen are different jobs. | Scientific background; does not calibrate our damage rates or danger thresholds |
| NASA-03 | [NASA Mars Communications Disruption and Delay](https://www.nasa.gov/wp-content/uploads/2024/01/mars-communications-disruption-and-delay.pdf) | Signal delay depends on distance; communication availability and latency are different problems. | Basis for delayed contact; no instantaneous reply or rescue |
| NASA-GMAT | [NASA GMAT star graphics data](https://github.com/nasa/GMAT), pinned commit `b8aec552a8f34a9212063adc3f249bd2315397a3` | Source RA/declination and coarse visual magnitudes for the space background | Six texture hashes and saved bindings verified; Apache-2.0 notice and license ship with the build. Stellar observation epoch/underlying measurement catalogue are unspecified; no Hipparcos attribution or GMAT trajectory claim |
| NASA-VEGGIE | [NASA Veggie fact sheet](https://science.nasa.gov/wp-content/uploads/2023/04/veggie_fact_sheet_508.pdf) | Reference lettuce growth duration and approximately 70 W equipment load | Background for C06; assumed pre-grown seedlings, recipe, yield and survival thresholds are gameplay tuning |
| NASA-REST | [NASA Medical Operations reference](https://www.nasa.gov/reference/6-0-medical-operations/) | 8.5-hour sleep opportunity reference | Maximum simulated rest opportunity; not a guarantee of restful sleep or a clinical sleep model |
| CHALLENGE | [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) | Mission design, constraints, operational consequences and evaluation belong in the game | Planner and debrief must connect engineering choices to outcomes |

Source review and dataset retrieval: November 14, 2026. The raw response and manifest preserve the precise retrieval timestamp and API version. These are ephemeris predictions/computations, not a live spacecraft telemetry feed.

## Reproducible dataset

- [Raw API response](docs/data/horizons/earth-mars-2026-11-14--2026-12-15.raw.json): exact bytes returned by Horizons, including metadata and caveats.
- [Normalized CSV](docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv): 32 daily samples, inclusive, November 14 through December 15, 2026 at 08:00 UTC (09:00 Europe/Bratislava).
- [Manifest](docs/data/horizons/manifest.json): complete encoded request URL, parameters, file hashes, reference frame, units and derivation.
- [Fetch/verification script](tools/nasa/horizons_snapshot.py): Python 3.10+ standard library; no dependencies.

```powershell
# Offline verification: no network, no file mutation.
python tools/nasa/horizons_snapshot.py

# Deliberate dataset refresh only; inspect resulting diff and hashes before committing.
python tools/nasa/horizons_snapshot.py --fetch
```

The request uses target Mars body center `499`, origin Earth body center `500@399`, `VECTORS`, `KM-S`, `TIME_TYPE=UT`, `REF_SYSTEM=ICRF`, `REF_PLANE=FRAME`, and `VEC_CORR=NONE`. The source identifies Mars solution `mar099` and Earth solution `DE441`. For these dates, Horizons UT denotes UTC; preserve its future leap-second caveat. No light-time or aberration corrections are applied to the geometric state vectors.

The CSV columns are `epoch_utc`, `julian_day_ut`, `x_km`, `y_km`, `z_km`, `earth_mars_distance_km`, `one_way_seconds`, and `minimum_round_trip_seconds`. Numeric fields use an invariant decimal point. Positions are Mars relative to Earth's center in ICRF, not Unity scene coordinates.

```text
distance_km = sqrt(x_km² + y_km² + z_km²)
one_way_seconds = distance_km / 299792.458
minimum_round_trip_seconds = 2 × one_way_seconds
```

`299792.458 km/s` is the vacuum speed of light. The verification script independently compares the vector norm against Horizons `RG` and the derived one-way time against Horizons `LT`, with relative tolerance `1e-12`. It checks the exact 32 timestamps, daily spacing, expected target/origin/frame/units, finite numbers, raw and normalized SHA-256 hashes, and byte-for-byte offline CSV regeneration.

| Epoch (UTC) | Distance (km, rounded for display) | One-way delay | Frozen-geometry minimum round trip |
| --- | ---: | ---: | ---: |
| 2026-11-14 08:00 | 197,707,976 | 10 min 59.483 s | 21 min 58.966 s |
| 2026-12-15 08:00 | 157,475,149 | 8 min 45.281 s | 17 min 30.561 s |

More digits in the CSV preserve reproducibility; they do not imply that the fictional spacecraft position is known to that accuracy. UI distance should normally round to the nearest thousand kilometres and delay to whole seconds.

## Scenario assumptions and narrative corrections

| Subject | Decision / limit |
| --- | --- |
| Spacecraft location | For the slice, use Mars-center range as an explicitly labeled **Mars-vicinity distance proxy**. This dataset does not calculate AURORA-7's transfer trajectory or orbit. A point on an Earth–Mars transfer is not automatically at Mars' distance. |
| Story timing | Sample index 0 is elapsed day 0, November 14. If the UI calls that “Day 1,” “Day 31” uses index 30 (December 14). Index 31 is an extra boundary sample. Preserve a single epoch/day mapping. |
| Receding Earth | Earth–Mars distance **decreases** across this dataset. Use “off course,” “isolated,” or “recovery window uncertain.” Do not present increasing range as an observation from these data. A separately modeled fictional vessel trajectory would need a separate label. |
| Fixed 18-minute signal delay | The supplied story's 18 minutes is a placeholder. Replace it with the selected sample's value. Do not tune the speed of light to fit dialogue. |
| Reply timing | `2 × range/c` is a frozen-epoch educational approximation. Add ground processing time separately. Actual signal propagation also involves moving endpoints; link availability, occultation, acquisition and transmission time are not modeled by this range alone. |
| Chapter compression | Display elapsed simulation time explicitly. During a time jump, resource accounting and communication clocks must advance consistently. Camera animation and UI timing can use unscaled presentation time. Never label 20 real seconds as 20 simulated minutes without disclosing the scale. |
| Earth imagery | At this range, Earth is a distant point/small disk. A huge nearby Earth belongs only in a clearly separated launch memory or illustrative planner, not the deep-space exterior. |
| False hope | A damaged local buffer replays the ship's earlier transmission. Do not claim a simple echo returning from empty space is a valid NASA communications model. |
| Finale | A received reply permits a recovery plan. It does not mean a rescue craft instantly arrives. Real recovery feasibility is not established by this simplified mission. |
| Damage and navigation | The impact, faulty correction burn and antenna damage are authored events. Navigation warnings are scenario state, not a propagated orbit solution. |

## Life-support model boundaries

Track stored oxygen in kilograms, water in litres, battery energy in kWh, load/generation in watts, food in kilograms, spare parts as integer counts, and cabin CO2 concentration in ppm. Stored oxygen reserve is **not** atmospheric oxygen percentage; reserve depletion and breathable cabin conditions must not be conflated. Define the compartment volume and gas assumptions before deriving concentration from mass.

NASA-02 supports the relationships between subsystems, not arbitrary per-second depletion numbers. The first slice may use documented gameplay tuning for consumption, leak severity and repair time, but label every such rate as a design assumption until sourced. Do not present unsourced CO2 limits or suit survival timers as medically validated thresholds.

Power conversion must preserve units: `energy_kWh = power_W × duration_s / 3,600,000`. Resource quantities must remain finite and nonnegative; consumption cannot create resources. A blocked action must not partially consume materials. Hydroponics is a long-term investment/display, not instant mature food. Water recycling and oxygen generation require energy; avoid unlimited closed-loop recovery.

## Required gameplay integration and evidence

1. Package an offline runtime copy of the reviewed CSV under an integration-approved Unity asset path. Preserve the dataset ID and SHA-256 in the mission report. No network requirement during judging.
2. Load with invariant culture, reject invalid/non-finite rows, select the last sample at or before the mission epoch, and reject epochs outside the snapshot interval. Any interpolation is a later explicitly documented change.
3. Show mission date, approximate Earth distance, estimated one-way delay, and separate “transmitting,” “awaiting reply,” and “reply received” states.
4. Make the delay consequential: remaining energy and life support must cover the wait. Mission configuration and power decisions should alter available reserves, not light speed.
5. Do not emit `reply received` before modeled outbound delay, ground processing and return delay have elapsed. An unpowered receiver cannot receive a reply; document retry/queued-message behavior.
6. In the report, distinguish **NASA/JPL data**, **derived physics**, **fictional scenario**, and **gameplay time compression**. Link NASA-01 through NASA-03.
7. Claude must add boundary and timing tests, then the integrator must verify the displayed numbers in an actual complete playthrough. The data-verification script is not a Unity gameplay test.

Suggested report attribution: “Earth–Mars reference ephemeris: NASA/JPL Horizons. AURORA-7 uses a fictional Mars-vicinity distance approximation. Life-support behavior is simplified for education. Mission timing may be compressed.”

## Current star and C06 provenance boundaries

The current star asset is documented in `Assets/_OutOfReach/Scripts/Presentation/Space/STARFIELD_PROVENANCE.md`. Raw GMAT star bytes have SHA-256 `d6b66913176142dd12610fa5c2220f6c0f85e1ed2bd817230cf5619ab8f569f5`; the numerical subset has SHA-256 `7077c2df92e8b5e53772ceef200dce3f6b9a5e0c3665ff83c4ea44d7bae5ce20`. Per-face hashes, directional mapping and artistic exposure are in `docs/data/gmat-skybox-v01/`. RA/declination use degrees; stellar epoch is unspecified and J2000 alignment is inferred from GMAT's renderer. This background does not establish AURORA-7's orbit or attitude.

C06 uses **fictional 25-day-old pre-grown seedlings**, followed by 8 further simulated growing days to a nominal age of 33 days. The reference Veggie experiment informs that nominal duration and approximately 70 W load; it does not prescribe the game's 25-day starting plants or guarantee growth. The 1 spare part/0.2 kWh build, 1 L planting cost, 0.05 L/day, 0.05 kg ration-equivalent harvest, 6-hour portions and hunger/thirst failure rules are authored game assumptions. The harvest credit is not measured leafy biomass or a complete dietary replacement. Resource consumption and growth advance through the same simulation; no instant crop.

Recovery uses an already-in-transit fictional nearby craft. Current constants in `SurvivalRecovery.cs`: rendezvous 12 days after contact; beacon request 6 days after contact; an 8-hour response window; 0.4 kWh charged once; a missed window delays rendezvous by 3 days (15 days after contact). These are relative scenario timings, not fixed mission-day numbers, an Earth-to-Mars flight duration, or an orbit propagated by GMAT/Horizons. Mars departure is a separately disclosed later narrative passage. Rest lasts at most 8.5 simulated hours per opportunity and advances the same supplies/calendar model; gameplay time acceleration does not manufacture supplies or bypass danger.

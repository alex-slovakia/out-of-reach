# DSN visibility calculator (data only, no UI yet)

`Runtime/DsnVisibility.cs` answers one question for a UTC instant: **which Deep Space Network complex
(Goldstone, Madrid, Canberra) has Mars above its horizon?** It is pure `System` C# (no `UnityEngine`, no file or
network access) and lives in the existing `OutOfReach.UI.Chapter` assembly, namespace
`OutOfReach.Presentation.UI.Chapter`.

## What is NASA/JPL data and what is derived

| Part | Origin |
| --- | --- |
| 32 daily Mars-relative-to-Earth vectors (ICRF, km) | NASA/JPL Horizons, copied verbatim from the committed `docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv` (dataset NASA-01). `DsnVisibilityTests` compares all 32 rows with the CSV. |
| Latitude / east longitude of each complex | NASA/JPL Horizons observer-site records of the 70 m antennas, ITRF93 geodetic, retrieved 2026-11-15 (`CENTER='-14@399'`, `'-63@399'`, `'-43@399'`): DSS-14 Goldstone `243.1104618 E, 35.4259009`, DSS-63 Madrid `355.7519915 E, 40.4312097`, DSS-43 Canberra `148.9812673 E, -35.4024242`. Raw records: `dsn-horizons-check/site-dss*.txt`. |
| Right ascension, declination, sidereal time, hour angle, elevation, the 10° mask, the wording | **Derived** here with textbook spherical astronomy (below). |

## Formulas

1. Unit vector of Mars: the two neighbouring daily samples are normalised, interpolated linearly in time and
   renormalised. No extrapolation: before 2026-11-14 08:00 UTC or after 2026-12-15 08:00 UTC there is no data.
2. ICRF direction: `RA = atan2(y, x)`, `Dec = asin(z)`.
3. Precession to the mean equator and equinox of date (IAU 1976 angles zeta, z, theta; Meeus, *Astronomical
   Algorithms*, ch. 21). For this epoch that is +0.37° in right ascension and -0.13° in declination, so it is applied.
4. Greenwich mean sidereal time from the UT Julian date (IAU 1982; Meeus eq. 12.4):
   `GMST = 280.46061837 + 360.98564736629 (JD - 2451545.0) + 0.000387933 T² - T³ / 38710000` degrees.
5. Hour angle `H = GMST + east longitude - RA`.
6. Elevation `el = asin(sin(lat) sin(Dec) + cos(lat) cos(Dec) cos(H))`.
7. A complex "has Mars in view" when `el >= 10°` (`DsnVisibility.ElevationMaskDeg`). The 10° mask is an authored,
   conservative threshold; real antennas have site-specific terrain masks and elevation limits that are not modelled.

## Accuracy: checked against JPL Horizons

Reference: Horizons API 1.2, `EPHEM_TYPE='OBSERVER'`, `COMMAND='499'`, `CENTER='coord@399'`, `COORD_TYPE='GEODETIC'`,
`SITE_COORD='<east lon>,<lat>,<alt km>'` of each complex, `QUANTITIES='2,4'`, `APPARENT='AIRLESS'` (no refraction),
eight instants between 2026-11-14 and 2026-12-15 at different hours. Retrieved 2026-11-15 20:59 UTC. Exact request
URLs are in `dsn-horizons-check/queries.txt`, raw responses beside it. Elevation in degrees, JPL Horizons / this code:

| UTC | Goldstone | Madrid | Canberra |
| --- | ---: | ---: | ---: |
| 2026-11-14 08:00 | 7.490 / 7.489 | 58.528 / 58.530 | -68.970 / -68.967 |
| 2026-11-17 14:30 | 67.952 / 67.952 | -12.198 / -12.195 | -5.394 / -5.393 |
| 2026-11-21 21:00 | -5.797 / -5.795 | -23.919 / -23.919 | 38.929 / 38.931 |
| 2026-11-26 03:15 | -36.061 / -36.061 | 43.587 / 43.587 | -23.865 / -23.862 |
| 2026-12-01 10:45 | 47.341 / 47.341 | 21.681 / 21.684 | -43.254 / -43.254 |
| 2026-12-06 17:20 | 29.972 / 29.974 | -36.965 / -36.964 | 33.308 / 33.310 |
| 2026-12-11 23:50 | -39.998 / -39.996 | 11.858 / 11.860 | 9.755 / 9.758 |
| 2026-12-15 06:00 | -3.053 / -3.051 | 59.795 / 59.796 | -61.954 / -61.953 |

Worst absolute difference over the 24 values: **0.0035°** (required: 1.5°; the test asserts 0.05°). The remainder is
what the simple model leaves out: light-time, aberration, nutation, UT1-UTC and topocentric parallax.

Stepping through the whole snapshot minute by minute (44,641 instants): exactly one complex has Mars in view 65.3 %
of the time, two complexes 34.7 % (handover overlap), all three never, and none never. So in this window the line
"Mars is below 10° at all three complexes" exists for completeness but does not occur.

## Honest limits (please keep in any player-facing text)

- It is the **geometric visibility of Mars' centre** from each complex, computed from Horizons vectors and Earth's
  rotation. It is **not the real DSN tracking schedule**: which antenna actually tracks which spacecraft is an
  operational decision this does not know.
- It is **not the position of the fictional AURORA-7**. Like the delay model, it uses Mars as the Mars-vicinity proxy.
- No refraction, terrain mask, antenna slew limits, weather, occultation or link budget.
- Station coordinates are those of each complex's 70 m antenna, standing in for the whole complex.

## Public API

```csharp
namespace OutOfReach.Presentation.UI.Chapter
{
    public enum DsnComplex { Goldstone = 0, Madrid = 1, Canberra = 2 }

    public static class DsnVisibility
    {
        public const double ElevationMaskDeg = 10d;
        public const int SampleCount = 32;
        public const string NoDataLine = "DSN: no JPL Horizons sample for this date";
        public static readonly DateTime FirstSampleUtc; // 2026-11-14 08:00 UTC
        public static readonly DateTime LastSampleUtc;  // 2026-12-15 08:00 UTC

        public static string Name(DsnComplex complex);
        public static double LatitudeDeg(DsnComplex complex);
        public static double EastLongitudeDeg(DsnComplex complex);
        public static bool HasData(DateTime utc);
        public static DateTime SampleEpochUtc(int index);
        public static void SampleVectorKm(int index, out double xKm, out double yKm, out double zKm);
        public static double JulianDayUt(DateTime utc);
        public static double GreenwichMeanSiderealTimeDeg(DateTime utc);
        public static bool TryMarsDirectionIcrf(DateTime utc, out double rightAscensionDeg, out double declinationDeg);
        public static bool TryMarsDirectionOfDate(DateTime utc, out double rightAscensionDeg, out double declinationDeg);
        public static double ElevationDeg(DsnComplex complex, DateTime utc);            // NaN outside the snapshot
        public static bool TryElevationDeg(DsnComplex complex, DateTime utc, out double elevationDeg);
        public static bool TryElevations(DateTime utc, out double goldstoneDeg, out double madridDeg, out double canberraDeg);
        public static DsnComplex[] ComplexesInView(DateTime utc);                       // highest first; empty if none / no data
        public static string Describe(DateTime utc);                                    // the UI line
        public static bool TryDescribe(DateTime utc, out string line);                  // false + NoDataLine outside the snapshot
        public static string DescribeElevations(double goldstoneDeg, double madridDeg, double canberraDeg);
    }
}
```

`DateTime` arguments are UTC; `DateTimeKind.Local` is converted, `Unspecified` is taken as UTC.

One-line hook-up next to the Earth distance / signal delay row (same UTC epoch that selects the Horizons sample):

```csharp
string dsnLine = DsnVisibility.Describe(epochUtc);
// or, to hide the row outside the snapshot:
if (DsnVisibility.TryDescribe(epochUtc, out string dsnLine)) { /* show dsnLine */ }
```

Examples (real output):

```text
2026-11-14 08:00 UTC  DSN: Madrid has Mars in view · el. 59°
2026-11-14 10:00 UTC  DSN: Madrid + Goldstone have Mars in view
2026-11-14 14:00 UTC  DSN: Goldstone has Mars in view · el. 69°
2026-11-14 16:00 UTC  DSN: Goldstone + Canberra have Mars in view
2026-11-14 20:00 UTC  DSN: Canberra has Mars in view · el. 39°
2026-12-16 08:00 UTC  DSN: no JPL Horizons sample for this date
```

## Proposed documentation rows (integrator-owned files, not edited here)

Row for the Sources table in `NASA-DATA.md`:

```markdown
| NASA-DSN | [NASA/JPL Horizons API](https://ssd-api.jpl.nasa.gov/doc/horizons.html) observer-site records of DSS-14 (Goldstone), DSS-63 (Madrid) and DSS-43 (Canberra), combined with the NASA-01 vectors; background: [NASA Deep Space Network](https://www.nasa.gov/communicating-with-missions/dsn/) | Which DSN complex has Mars at least 10° above its horizon at the mission's UTC epoch | Derived geometry in `DsnVisibility.cs`; 24 values checked against Horizons topocentric elevations (worst difference 0.0035°). Geometric visibility of Mars only: not the real DSN tracking schedule and not AURORA-7's position. |
```

Line for the in-game Sources screen:

```text
Deep Space Network view of Mars — derived from NASA/JPL Horizons vectors and DSN antenna coordinates (Goldstone DSS-14, Madrid DSS-63, Canberra DSS-43). Geometric visibility above 10°, not the real DSN tracking schedule.
```

## Verification without Unity

`DsnVisibility.cs` and `Tests/EditMode/DsnVisibilityTests.cs` were compiled with the Roslyn shipped in Unity
6000.3.28f1 (`-nostdlib`, `netstandard.dll` 2.1 only for the calculator, plus `nunit.framework.dll` for the tests)
and run with a reflection runner outside the editor: 14 tests, 14 passed. Unity itself was not started for this
change, so an EditMode run inside the editor is still the integrator's check. `Integration~` is ignored by Unity
(trailing `~`), so it needs no `.meta`.

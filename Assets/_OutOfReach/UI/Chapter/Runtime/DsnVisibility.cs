using System;
using System.Globalization;

namespace OutOfReach.Presentation.UI.Chapter
{
    /// <summary>The three NASA Deep Space Network complexes, placed roughly 120 degrees of longitude apart.</summary>
    public enum DsnComplex
    {
        Goldstone = 0,
        Madrid = 1,
        Canberra = 2
    }

    /// <summary>
    /// DERIVED GEOMETRY from NASA/JPL data: which Deep Space Network complex has Mars above its horizon at a UTC
    /// instant. Pure <c>System</c> code, no engine types, no file or network access.
    ///
    /// Inputs. (1) The 32 daily NASA/JPL Horizons vectors of Mars relative to Earth's centre (ICRF, geometric),
    /// copied verbatim from <c>docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv</c> (dataset NASA-01).
    /// (2) Geodetic latitude and east longitude of each complex's 70 m antenna (DSS-14 Goldstone, DSS-63 Madrid,
    /// DSS-43 Canberra) as listed in the NASA/JPL Horizons observer-site records (ITRF93), retrieved 2026-11-15 from
    /// https://ssd.jpl.nasa.gov/api/horizons.api with CENTER='-14@399', '-63@399' and '-43@399'.
    ///
    /// Method. Unit vectors are interpolated linearly between the two neighbouring daily samples and renormalised;
    /// the ICRF direction is precessed to the mean equator and equinox of date (IAU 1976); Greenwich mean sidereal
    /// time comes from the UT Julian date (IAU 1982 expression); the hour angle is GMST + east longitude - right
    /// ascension and the elevation is asin(sin(lat) sin(dec) + cos(lat) cos(dec) cos(H)).
    ///
    /// Limits. This is the geometric visibility of Mars' centre from each complex. It is NOT the real DSN tracking
    /// schedule and NOT the position of the fictional AURORA-7. No refraction, terrain mask, light-time, aberration,
    /// nutation or parallax is modelled; against 24 JPL Horizons topocentric apparent elevations the result agreed
    /// to within 0.004 degree (see Integration~/DSN-README.md). Outside the snapshot interval there is no data and
    /// nothing is extrapolated.
    /// </summary>
    public static class DsnVisibility
    {
        /// <summary>A complex "has Mars in view" at or above this geometric elevation, degrees.</summary>
        public const double ElevationMaskDeg = 10d;

        /// <summary>Number of daily samples copied from the committed snapshot.</summary>
        public const int SampleCount = 32;

        /// <summary>Line returned outside the snapshot interval.</summary>
        public const string NoDataLine = "DSN: no JPL Horizons sample for this date";

        /// <summary>Epoch of sample 0: 2026-11-14 08:00 UTC (Julian day 2461358.833333333).</summary>
        public static readonly DateTime FirstSampleUtc = new DateTime(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);

        /// <summary>Epoch of sample 31: 2026-12-15 08:00 UTC (Julian day 2461389.833333333).</summary>
        public static readonly DateTime LastSampleUtc = new DateTime(2026, 12, 15, 8, 0, 0, DateTimeKind.Utc);

        private const double DegToRad = Math.PI / 180d;
        private const double RadToDeg = 180d / Math.PI;
        private const double ArcsecToRad = Math.PI / (180d * 3600d);
        private const double J2000JulianDay = 2451545.0d;
        private const double DaysPerJulianCentury = 36525d;

        private static readonly DateTime J2000Utc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static readonly string[] NameByComplex = { "Goldstone", "Madrid", "Canberra" };

        // NASA/JPL Horizons observer-site records, "Center geodetic" line {E-lon(deg), Lat(deg), Alt(km)}, ITRF93:
        //   DSS-14 (70-m) Goldstone, California : 243.1104618,  35.4259009, 1.00139
        //   DSS-63 (70-m) Robledo, near Madrid  : 355.7519915,  40.4312097, 0.86482
        //   DSS-43 (70-m) Tidbinbilla, Canberra : 148.9812673, -35.4024242, 0.688867
        // The altitude is not used: at Mars' range it changes the elevation by less than 0.001 degree.
        private static readonly double[] LatitudeDegByComplex = { 35.4259009d, 40.4312097d, -35.4024242d };
        private static readonly double[] EastLongitudeDegByComplex = { 243.1104618d, 355.7519915d, 148.9812673d };

        // x_km, y_km, z_km of Mars' centre relative to Earth's centre, ICRF, one row per day at 08:00 UTC.
        // Copied from docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv; DsnVisibilityTests compares them.
        private static readonly double[] MarsKm =
        {
            -161160097.612842590d, 102305448.047787502d, 51472924.995187446d, // 2026-11-14
            -161009956.951080203d, 100451702.482303798d, 50710921.448357768d, // 2026-11-15
            -160827395.697875112d, 98616308.532722026d, 49956373.222073384d, // 2026-11-16
            -160612950.626043499d, 96799756.091088519d, 49209486.558135703d, // 2026-11-17
            -160367164.530109793d, 95002518.740491107d, 48470460.271966673d, // 2026-11-18
            -160090584.629726201d, 93225053.452686369d, 47739485.698460311d, // 2026-11-19
            -159783760.413274407d, 91467800.432097062d, 47016746.746442549d, // 2026-11-20
            -159447240.854467213d, 89731183.469107494d, 46302420.254958414d, // 2026-11-21
            -159081571.184554011d, 88015611.278701276d, 45596676.888903283d, // 2026-11-22
            -158687289.789629996d, 86321480.279419944d, 44899682.777649887d, // 2026-11-23
            -158264926.217845112d, 84649178.976717323d, 44211601.924134716d, // 2026-11-24
            -157815001.450884610d, 82999093.502261803d, 43532599.082447827d, // 2026-11-25
            -157338031.195274502d, 81371613.126499355d, 42862842.442448162d, // 2026-11-26
            -156834531.939996392d, 79767134.175963029d, 42202505.317893907d, // 2026-11-27
            -156305028.393396586d, 78186061.156190112d, 41551766.293818839d, // 2026-11-28
            -155750060.393270403d, 76628804.894170299d, 40910807.847986497d, // 2026-11-29
            -155170187.803674787d, 75095778.540707096d, 40279813.972360983d, // 2026-11-30
            -154565992.888405204d, 73587392.723979563d, 39658967.498637833d, // 2026-12-01
            -153938080.522097498d, 72104050.944734573d, 39048447.675923303d, // 2026-12-02
            -153287076.992779702d, 70646145.788953260d, 38448428.257014647d, // 2026-12-03
            -152613628.108711094d, 69214056.066865474d, 37859076.108561970d, // 2026-12-04
            -151918397.079887688d, 67808144.728735775d, 37280550.239878401d, // 2026-12-05
            -151202062.396317989d, 66428757.341316879d, 36713001.125013307d, // 2026-12-06
            -150465315.760729194d, 65076220.952035837d, 36156570.224646792d, // 2026-12-07
            -149708860.062974006d, 63750843.243903123d, 35611389.657925382d, // 2026-12-08
            -148933407.381160796d, 62452911.946607217d, 35077582.007018499d, // 2026-12-09
            -148139677.024080008d, 61182694.497886449d, 34555260.250390269d, // 2026-12-10
            -147328393.658877403d, 59940437.944334552d, 34044527.816415668d, // 2026-12-11
            -146500285.575532585d, 58726369.044234797d, 33545478.734580841d, // 2026-12-12
            -145656083.118192405d, 57540694.505306438d, 33058197.847299960d, // 2026-12-13
            -144796517.267707288d, 56383601.274667233d, 32582761.040149789d, // 2026-12-14
            -143922318.302467495d, 55255256.808625430d, 32119235.457321201d, // 2026-12-15
        };

        /// <summary>Display name: "Goldstone", "Madrid" or "Canberra".</summary>
        public static string Name(DsnComplex complex)
        {
            return NameByComplex[IndexOf(complex)];
        }

        /// <summary>Geodetic latitude of the complex's 70 m antenna, degrees, north positive.</summary>
        public static double LatitudeDeg(DsnComplex complex)
        {
            return LatitudeDegByComplex[IndexOf(complex)];
        }

        /// <summary>Longitude of the complex's 70 m antenna, degrees east of Greenwich, 0 to 360.</summary>
        public static double EastLongitudeDeg(DsnComplex complex)
        {
            return EastLongitudeDegByComplex[IndexOf(complex)];
        }

        /// <summary>True from the first to the last sample epoch, both inclusive.</summary>
        public static bool HasData(DateTime utc)
        {
            DateTime instant = ToUtc(utc);
            return instant >= FirstSampleUtc && instant <= LastSampleUtc;
        }

        /// <summary>Epoch of one embedded sample, 0 to <see cref="SampleCount"/> - 1.</summary>
        public static DateTime SampleEpochUtc(int index)
        {
            CheckSampleIndex(index);
            return FirstSampleUtc.AddDays(index);
        }

        /// <summary>The embedded NASA/JPL vector of one sample, kilometres, ICRF, Mars relative to Earth's centre.</summary>
        public static void SampleVectorKm(int index, out double xKm, out double yKm, out double zKm)
        {
            CheckSampleIndex(index);
            xKm = MarsKm[index * 3];
            yKm = MarsKm[(index * 3) + 1];
            zKm = MarsKm[(index * 3) + 2];
        }

        /// <summary>Julian day number of a UTC instant (UT is taken as UTC, as in the snapshot).</summary>
        public static double JulianDayUt(DateTime utc)
        {
            return J2000JulianDay + DaysSinceJ2000(utc);
        }

        /// <summary>Greenwich mean sidereal time, degrees in [0, 360): IAU 1982 expression in the UT Julian date.</summary>
        public static double GreenwichMeanSiderealTimeDeg(DateTime utc)
        {
            double days = DaysSinceJ2000(utc);
            double centuries = days / DaysPerJulianCentury;
            double degrees = 280.46061837d
                + (360.98564736629d * days)
                + (0.000387933d * centuries * centuries)
                - (centuries * centuries * centuries / 38710000d);
            return Wrap360(degrees);
        }

        /// <summary>
        /// Direction of Mars from Earth's centre in the ICRF, exactly as the snapshot gives it: right ascension
        /// atan2(y, x) and declination asin(z / r) of the linearly interpolated, renormalised unit vector.
        /// Returns false (NaN outputs) outside the snapshot interval.
        /// </summary>
        public static bool TryMarsDirectionIcrf(DateTime utc, out double rightAscensionDeg, out double declinationDeg)
        {
            if (!TryUnitVector(utc, out double x, out double y, out double z))
            {
                rightAscensionDeg = double.NaN;
                declinationDeg = double.NaN;
                return false;
            }

            rightAscensionDeg = Wrap360(Math.Atan2(y, x) * RadToDeg);
            declinationDeg = Math.Asin(Clamp(z)) * RadToDeg;
            return true;
        }

        /// <summary>
        /// The same direction referred to the mean equator and equinox of date (IAU 1976 precession from J2000),
        /// the frame Greenwich sidereal time is measured in. Returns false (NaN outputs) outside the snapshot.
        /// </summary>
        public static bool TryMarsDirectionOfDate(DateTime utc, out double rightAscensionDeg, out double declinationDeg)
        {
            if (!TryMarsDirectionIcrf(utc, out double icrfRightAscension, out double icrfDeclination))
            {
                rightAscensionDeg = double.NaN;
                declinationDeg = double.NaN;
                return false;
            }

            double t = DaysSinceJ2000(utc) / DaysPerJulianCentury;
            double zeta = ((2306.2181d * t) + (0.30188d * t * t) + (0.017998d * t * t * t)) * ArcsecToRad;
            double z = ((2306.2181d * t) + (1.09468d * t * t) + (0.018203d * t * t * t)) * ArcsecToRad;
            double theta = ((2004.3109d * t) - (0.42665d * t * t) - (0.041833d * t * t * t)) * ArcsecToRad;

            double alpha = (icrfRightAscension * DegToRad) + zeta;
            double delta = icrfDeclination * DegToRad;
            double a = Math.Cos(delta) * Math.Sin(alpha);
            double b = (Math.Cos(theta) * Math.Cos(delta) * Math.Cos(alpha)) - (Math.Sin(theta) * Math.Sin(delta));
            double c = (Math.Sin(theta) * Math.Cos(delta) * Math.Cos(alpha)) + (Math.Cos(theta) * Math.Sin(delta));

            rightAscensionDeg = Wrap360((Math.Atan2(a, b) + z) * RadToDeg);
            declinationDeg = Math.Asin(Clamp(c)) * RadToDeg;
            return true;
        }

        /// <summary>
        /// Geometric elevation of Mars' centre above the horizon of a complex, degrees (negative below the horizon).
        /// Returns <see cref="double.NaN"/> outside the snapshot interval; use <see cref="TryElevationDeg"/> to branch.
        /// </summary>
        public static double ElevationDeg(DsnComplex complex, DateTime utc)
        {
            return TryElevationDeg(complex, utc, out double elevationDeg) ? elevationDeg : double.NaN;
        }

        /// <summary>Elevation as in <see cref="ElevationDeg"/>; false (NaN) outside the snapshot interval.</summary>
        public static bool TryElevationDeg(DsnComplex complex, DateTime utc, out double elevationDeg)
        {
            int index = IndexOf(complex);
            if (!TryMarsDirectionOfDate(utc, out double rightAscensionDeg, out double declinationDeg))
            {
                elevationDeg = double.NaN;
                return false;
            }

            double hourAngle = (GreenwichMeanSiderealTimeDeg(utc) + EastLongitudeDegByComplex[index] - rightAscensionDeg) * DegToRad;
            double latitude = LatitudeDegByComplex[index] * DegToRad;
            double declination = declinationDeg * DegToRad;
            double sine = (Math.Sin(latitude) * Math.Sin(declination))
                + (Math.Cos(latitude) * Math.Cos(declination) * Math.Cos(hourAngle));
            elevationDeg = Math.Asin(Clamp(sine)) * RadToDeg;
            return true;
        }

        /// <summary>Elevations at all three complexes at once; false (NaN outputs) outside the snapshot interval.</summary>
        public static bool TryElevations(DateTime utc, out double goldstoneDeg, out double madridDeg, out double canberraDeg)
        {
            bool known = TryElevationDeg(DsnComplex.Goldstone, utc, out goldstoneDeg);
            known &= TryElevationDeg(DsnComplex.Madrid, utc, out madridDeg);
            known &= TryElevationDeg(DsnComplex.Canberra, utc, out canberraDeg);
            return known;
        }

        /// <summary>
        /// Complexes with Mars at or above <see cref="ElevationMaskDeg"/>, highest elevation first.
        /// Empty when none is, and also outside the snapshot interval (check <see cref="HasData"/> to tell them apart).
        /// </summary>
        public static DsnComplex[] ComplexesInView(DateTime utc)
        {
            if (!TryElevations(utc, out double goldstoneDeg, out double madridDeg, out double canberraDeg))
            {
                return Array.Empty<DsnComplex>();
            }

            return InView(goldstoneDeg, madridDeg, canberraDeg);
        }

        /// <summary>
        /// One short UI line for a UTC instant, for example "DSN: Madrid has Mars in view · el. 59°",
        /// "DSN: Madrid + Goldstone have Mars in view" or "DSN: Mars is below 10° at all three complexes".
        /// Outside the snapshot interval it returns <see cref="NoDataLine"/>.
        /// </summary>
        public static string Describe(DateTime utc)
        {
            TryDescribe(utc, out string line);
            return line;
        }

        /// <summary>As <see cref="Describe"/>; false (and <see cref="NoDataLine"/>) outside the snapshot interval.</summary>
        public static bool TryDescribe(DateTime utc, out string line)
        {
            if (!TryElevations(utc, out double goldstoneDeg, out double madridDeg, out double canberraDeg))
            {
                line = NoDataLine;
                return false;
            }

            line = DescribeElevations(goldstoneDeg, madridDeg, canberraDeg);
            return true;
        }

        /// <summary>The wording rule on its own: the line for three given elevations in degrees.</summary>
        public static string DescribeElevations(double goldstoneDeg, double madridDeg, double canberraDeg)
        {
            if (double.IsNaN(goldstoneDeg) || double.IsNaN(madridDeg) || double.IsNaN(canberraDeg))
            {
                return NoDataLine;
            }

            DsnComplex[] inView = InView(goldstoneDeg, madridDeg, canberraDeg);
            if (inView.Length == 0)
            {
                return "DSN: Mars is below " + ElevationMaskDeg.ToString("0", CultureInfo.InvariantCulture)
                    + "° at all three complexes";
            }

            if (inView.Length == 1)
            {
                double elevation = ElevationOf(inView[0], goldstoneDeg, madridDeg, canberraDeg);
                return "DSN: " + Name(inView[0]) + " has Mars in view · el. "
                    + Math.Round(elevation, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "°";
            }

            string names = Name(inView[0]);
            for (int i = 1; i < inView.Length; i++)
            {
                names += " + " + Name(inView[i]);
            }

            return "DSN: " + names + " have Mars in view";
        }

        private static DsnComplex[] InView(double goldstoneDeg, double madridDeg, double canberraDeg)
        {
            var order = new[] { DsnComplex.Goldstone, DsnComplex.Madrid, DsnComplex.Canberra };
            var elevations = new[] { goldstoneDeg, madridDeg, canberraDeg };

            // Three items: a stable insertion sort, highest elevation first.
            for (int i = 1; i < order.Length; i++)
            {
                for (int j = i; j > 0 && elevations[j] > elevations[j - 1]; j--)
                {
                    double swappedElevation = elevations[j];
                    elevations[j] = elevations[j - 1];
                    elevations[j - 1] = swappedElevation;
                    DsnComplex swappedComplex = order[j];
                    order[j] = order[j - 1];
                    order[j - 1] = swappedComplex;
                }
            }

            int count = 0;
            while (count < order.Length && elevations[count] >= ElevationMaskDeg)
            {
                count++;
            }

            var inView = new DsnComplex[count];
            Array.Copy(order, inView, count);
            return inView;
        }

        private static double ElevationOf(DsnComplex complex, double goldstoneDeg, double madridDeg, double canberraDeg)
        {
            switch (complex)
            {
                case DsnComplex.Goldstone:
                    return goldstoneDeg;
                case DsnComplex.Madrid:
                    return madridDeg;
                default:
                    return canberraDeg;
            }
        }

        private static bool TryUnitVector(DateTime utc, out double x, out double y, out double z)
        {
            DateTime instant = ToUtc(utc);
            if (instant < FirstSampleUtc || instant > LastSampleUtc)
            {
                x = double.NaN;
                y = double.NaN;
                z = double.NaN;
                return false;
            }

            double days = (instant - FirstSampleUtc).TotalDays;
            int lower = (int)Math.Floor(days);
            if (lower > SampleCount - 2)
            {
                lower = SampleCount - 2;
            }

            double fraction = days - lower;
            UnitSample(lower, out double ax, out double ay, out double az);
            UnitSample(lower + 1, out double bx, out double by, out double bz);
            x = ax + ((bx - ax) * fraction);
            y = ay + ((by - ay) * fraction);
            z = az + ((bz - az) * fraction);
            double length = Math.Sqrt((x * x) + (y * y) + (z * z));
            x /= length;
            y /= length;
            z /= length;
            return true;
        }

        private static void UnitSample(int index, out double x, out double y, out double z)
        {
            x = MarsKm[index * 3];
            y = MarsKm[(index * 3) + 1];
            z = MarsKm[(index * 3) + 2];
            double length = Math.Sqrt((x * x) + (y * y) + (z * z));
            x /= length;
            y /= length;
            z /= length;
        }

        private static double DaysSinceJ2000(DateTime utc)
        {
            return (ToUtc(utc) - J2000Utc).TotalDays;
        }

        /// <summary>Local times are converted; unspecified kinds are taken as UTC already.</summary>
        private static DateTime ToUtc(DateTime time)
        {
            return time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        }

        private static int IndexOf(DsnComplex complex)
        {
            int index = (int)complex;
            if (index < 0 || index >= NameByComplex.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(complex), complex, "Unknown Deep Space Network complex.");
            }

            return index;
        }

        private static void CheckSampleIndex(int index)
        {
            if (index < 0 || index >= SampleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The snapshot has samples 0 to 31.");
            }
        }

        private static double Wrap360(double degrees)
        {
            double wrapped = degrees % 360d;
            return wrapped < 0d ? wrapped + 360d : wrapped;
        }

        private static double Clamp(double value)
        {
            return Math.Max(-1d, Math.Min(1d, value));
        }
    }
}

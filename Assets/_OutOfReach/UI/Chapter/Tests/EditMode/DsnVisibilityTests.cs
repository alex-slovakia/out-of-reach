using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using OutOfReach.Presentation.UI.Chapter;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// DSN visibility: the embedded vectors are the committed NASA/JPL Horizons snapshot, the geometry agrees with
    /// JPL Horizons topocentric elevations, the 10 degree mask and the snapshot bounds hold, nothing is extrapolated.
    /// </summary>
    public sealed class DsnVisibilityTests
    {
        private const string CsvRelativePath = "docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv";

        /// <summary>The snapshot keeps nine decimals of a kilometre; a double keeps about 3e-8 km at this magnitude.</summary>
        private const double VectorToleranceKm = 1e-6d;

        /// <summary>Asserted agreement with JPL Horizons. Measured worst case over the 24 references: 0.0035 degree.</summary>
        private const double HorizonsToleranceDeg = 0.05d;

        private static readonly DsnComplex[] AllComplexes = { DsnComplex.Goldstone, DsnComplex.Madrid, DsnComplex.Canberra };

        // JPL Horizons reference: https://ssd.jpl.nasa.gov/api/horizons.api, API version 1.2, retrieved 2026-11-15 20:59 UTC.
        // EPHEM_TYPE='OBSERVER', COMMAND='499' (Mars), CENTER='coord@399', COORD_TYPE='GEODETIC',
        // SITE_COORD='<east longitude deg>,<latitude deg>,<altitude km>' of DSS-14 / DSS-63 / DSS-43,
        // TLIST = the eight Julian dates below (TLIST_TYPE='JD', TIME_TYPE='UT'), QUANTITIES='2,4',
        // APPARENT='AIRLESS' (no refraction), ANG_FORMAT='DEG', EXTRA_PREC='YES', CSV_FORMAT='YES'.
        // The values are the "Elevation_(a-app)" column. Raw responses and exact request URLs:
        // Assets/_OutOfReach/UI/Chapter/Integration~/dsn-horizons-check/.
        private static readonly DateTime[] HorizonsEpochsUtc =
        {
            Utc(2026, 11, 14, 8, 0),   // JD 2461358.833333333
            Utc(2026, 11, 17, 14, 30), // JD 2461362.104166667
            Utc(2026, 11, 21, 21, 0),  // JD 2461366.375000000
            Utc(2026, 11, 26, 3, 15),  // JD 2461370.635416667
            Utc(2026, 12, 1, 10, 45),  // JD 2461375.947916667
            Utc(2026, 12, 6, 17, 20),  // JD 2461381.222222222
            Utc(2026, 12, 11, 23, 50), // JD 2461386.493055556
            Utc(2026, 12, 15, 6, 0)    // JD 2461389.750000000
        };

        // SITE_COORD='243.1104618,35.4259009,1.00139' (DSS-14, Goldstone)
        private static readonly double[] HorizonsGoldstoneElevationDeg =
        {
            7.489623937d, 67.951677831d, -5.797445451d, -36.061121299d, 47.341079080d, 29.971544241d, -39.997909329d, -3.053244249d
        };

        // SITE_COORD='355.7519915,40.4312097,0.86482' (DSS-63, Madrid)
        private static readonly double[] HorizonsMadridElevationDeg =
        {
            58.528439662d, -12.197822765d, -23.918891105d, 43.586674373d, 21.681194091d, -36.965290652d, 11.858160068d, 59.795133001d
        };

        // SITE_COORD='148.9812673,-35.4024242,0.688867' (DSS-43, Canberra)
        private static readonly double[] HorizonsCanberraElevationDeg =
        {
            -68.969639393d, -5.393814490d, 38.928705648d, -23.865202018d, -43.254293558d, 33.308367737d, 9.755329125d, -61.954141390d
        };

        private static DateTime Utc(int year, int month, int day, int hour, int minute)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        }

        [Test]
        public void EmbeddedVectors_AreTheCommittedSnapshot_FirstMiddleAndLastDay()
        {
            Assert.That(DsnVisibility.SampleCount, Is.EqualTo(32));

            // Rows copied from docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv (x_km, y_km, z_km).
            AssertSample(0, Utc(2026, 11, 14, 8, 0), -161160097.612842590d, 102305448.047787502d, 51472924.995187446d);
            AssertSample(15, Utc(2026, 11, 29, 8, 0), -155750060.393270403d, 76628804.894170299d, 40910807.847986497d);
            AssertSample(16, Utc(2026, 11, 30, 8, 0), -155170187.803674787d, 75095778.540707096d, 40279813.972360983d);
            AssertSample(31, Utc(2026, 12, 15, 8, 0), -143922318.302467495d, 55255256.808625430d, 32119235.457321201d);

            // earth_mars_distance_km of the first and the last row is the length of the embedded vector.
            DsnVisibility.SampleVectorKm(0, out double x, out double y, out double z);
            Assert.That(Math.Sqrt((x * x) + (y * y) + (z * z)), Is.EqualTo(197707975.990852982d).Within(1e-3d));
            DsnVisibility.SampleVectorKm(31, out x, out y, out z);
            Assert.That(Math.Sqrt((x * x) + (y * y) + (z * z)), Is.EqualTo(157475148.505746007d).Within(1e-3d));

            Assert.Throws<ArgumentOutOfRangeException>(() => DsnVisibility.SampleVectorKm(-1, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => DsnVisibility.SampleVectorKm(32, out _, out _, out _));
        }

        [Test]
        public void EmbeddedVectors_MatchEveryRowOfTheCommittedCsv()
        {
            string path = FindRepositoryFile(CsvRelativePath);
            if (path == null)
            {
                Assert.Ignore("The repository file " + CsvRelativePath + " is not reachable from " + Directory.GetCurrentDirectory() + ".");
            }

            string[] lines = File.ReadAllText(path).Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines[0], Does.StartWith("epoch_utc,julian_day_ut,x_km,y_km,z_km,"));
            Assert.That(lines.Length - 1, Is.EqualTo(DsnVisibility.SampleCount), "one embedded sample per CSV row");

            for (int index = 0; index < DsnVisibility.SampleCount; index++)
            {
                string[] fields = lines[index + 1].Split(',');
                DateTime epoch = DateTime.ParseExact(fields[0], "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                Assert.That(DsnVisibility.SampleEpochUtc(index), Is.EqualTo(epoch), "epoch of row " + index);
                Assert.That(DsnVisibility.JulianDayUt(epoch), Is.EqualTo(Number(fields[1])).Within(1e-8d), "julian_day_ut of row " + index);
                AssertSample(index, epoch, Number(fields[2]), Number(fields[3]), Number(fields[4]));
            }
        }

        [Test]
        public void SnapshotBounds_AreDailySamplesAt0800Utc()
        {
            Assert.That(DsnVisibility.FirstSampleUtc, Is.EqualTo(Utc(2026, 11, 14, 8, 0)));
            Assert.That(DsnVisibility.LastSampleUtc, Is.EqualTo(Utc(2026, 12, 15, 8, 0)));
            Assert.That(DsnVisibility.FirstSampleUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(DsnVisibility.SampleEpochUtc(31), Is.EqualTo(DsnVisibility.LastSampleUtc));
            Assert.That(DsnVisibility.JulianDayUt(DsnVisibility.FirstSampleUtc), Is.EqualTo(2461358.833333333d).Within(1e-8d));
            Assert.That(DsnVisibility.JulianDayUt(DsnVisibility.LastSampleUtc), Is.EqualTo(2461389.833333333d).Within(1e-8d));
        }

        [Test]
        public void Complexes_UseTheHorizonsSiteCoordinatesOfThe70mAntennas()
        {
            Assert.That(DsnVisibility.Name(DsnComplex.Goldstone), Is.EqualTo("Goldstone"));
            Assert.That(DsnVisibility.Name(DsnComplex.Madrid), Is.EqualTo("Madrid"));
            Assert.That(DsnVisibility.Name(DsnComplex.Canberra), Is.EqualTo("Canberra"));
            Assert.That(DsnVisibility.LatitudeDeg(DsnComplex.Goldstone), Is.EqualTo(35.4259009d));
            Assert.That(DsnVisibility.EastLongitudeDeg(DsnComplex.Goldstone), Is.EqualTo(243.1104618d));
            Assert.That(DsnVisibility.LatitudeDeg(DsnComplex.Madrid), Is.EqualTo(40.4312097d));
            Assert.That(DsnVisibility.EastLongitudeDeg(DsnComplex.Madrid), Is.EqualTo(355.7519915d));
            Assert.That(DsnVisibility.LatitudeDeg(DsnComplex.Canberra), Is.EqualTo(-35.4024242d));
            Assert.That(DsnVisibility.EastLongitudeDeg(DsnComplex.Canberra), Is.EqualTo(148.9812673d));
            Assert.Throws<ArgumentOutOfRangeException>(() => DsnVisibility.Name((DsnComplex)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => DsnVisibility.ElevationDeg((DsnComplex)(-1), DsnVisibility.FirstSampleUtc));
        }

        [Test]
        public void GreenwichMeanSiderealTime_MatchesPublishedValues()
        {
            // The constant term of the IAU 1982 expression is GMST at J2000.0 (2000-01-01 12:00 UT).
            Assert.That(DsnVisibility.JulianDayUt(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo(2451545.0d).Within(1e-9d));
            Assert.That(DsnVisibility.GreenwichMeanSiderealTimeDeg(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
                Is.EqualTo(280.46061837d).Within(1e-9d));

            // Meeus, Astronomical Algorithms, example 12.b: 1987 April 10, 19h21m00s UT -> 8h34m57.0896s = 128.7378734 degrees.
            Assert.That(DsnVisibility.GreenwichMeanSiderealTimeDeg(new DateTime(1987, 4, 10, 19, 21, 0, DateTimeKind.Utc)),
                Is.EqualTo(128.7378734d).Within(1e-6d));

            // One sidereal day later the same meridian angle returns: 86164.0905 s of UT.
            double start = DsnVisibility.GreenwichMeanSiderealTimeDeg(DsnVisibility.FirstSampleUtc);
            double later = DsnVisibility.GreenwichMeanSiderealTimeDeg(DsnVisibility.FirstSampleUtc.AddSeconds(86164.0905d));
            Assert.That(Math.Abs(later - start), Is.LessThan(1e-4d));
        }

        [Test]
        public void MarsDirection_AtASampleEpoch_IsThatVectorsRightAscensionAndDeclination()
        {
            // First row: RA = atan2(y, x), Dec = asin(z / r) in the ICRF.
            const double x = -161160097.612842590d;
            const double y = 102305448.047787502d;
            const double z = 51472924.995187446d;
            double expectedRightAscension = Math.Atan2(y, x) * 180d / Math.PI;
            double expectedDeclination = Math.Asin(z / Math.Sqrt((x * x) + (y * y) + (z * z))) * 180d / Math.PI;

            Assert.That(DsnVisibility.TryMarsDirectionIcrf(DsnVisibility.FirstSampleUtc, out double rightAscension, out double declination), Is.True);
            Assert.That(rightAscension, Is.EqualTo(expectedRightAscension).Within(1e-9d));
            Assert.That(declination, Is.EqualTo(expectedDeclination).Within(1e-9d));
            Assert.That(rightAscension, Is.EqualTo(147.592d).Within(0.001d));
            Assert.That(declination, Is.EqualTo(15.090d).Within(0.001d));

            // Precession to the equator and equinox of date moves it by about 0.37 degree in RA and -0.13 in Dec (26.9 years).
            Assert.That(DsnVisibility.TryMarsDirectionOfDate(DsnVisibility.FirstSampleUtc, out double dateRightAscension, out double dateDeclination), Is.True);
            Assert.That(dateRightAscension - rightAscension, Is.EqualTo(0.366d).Within(0.005d));
            Assert.That(dateDeclination - declination, Is.EqualTo(-0.126d).Within(0.005d));
        }

        [Test]
        public void MarsDirection_BetweenSamples_IsInterpolatedAndStaysBetweenItsNeighbours()
        {
            DateTime before = DsnVisibility.SampleEpochUtc(10);
            DateTime after = DsnVisibility.SampleEpochUtc(11);
            DsnVisibility.TryMarsDirectionIcrf(before, out double beforeRightAscension, out double beforeDeclination);
            DsnVisibility.TryMarsDirectionIcrf(after, out double afterRightAscension, out double afterDeclination);

            Assert.That(DsnVisibility.TryMarsDirectionIcrf(before.AddHours(12), out double middleRightAscension, out double middleDeclination), Is.True);
            Assert.That(middleRightAscension, Is.EqualTo((beforeRightAscension + afterRightAscension) / 2d).Within(1e-3d));
            Assert.That(middleDeclination, Is.EqualTo((beforeDeclination + afterDeclination) / 2d).Within(1e-3d));

            // Continuous across a sample: one second either side of sample 11 differs by far less than a daily step.
            DsnVisibility.TryMarsDirectionIcrf(after.AddSeconds(-1), out double justBefore, out _);
            DsnVisibility.TryMarsDirectionIcrf(after.AddSeconds(1), out double justAfter, out _);
            Assert.That(Math.Abs(justAfter - justBefore), Is.LessThan(1e-4d));
            Assert.That(Math.Abs(afterRightAscension - beforeRightAscension), Is.GreaterThan(0.1d), "Mars moves between daily samples");
        }

        [Test]
        public void Elevation_AgreesWithJplHorizons_AtAllThreeComplexes()
        {
            double worst = 0d;
            for (int i = 0; i < HorizonsEpochsUtc.Length; i++)
            {
                worst = Math.Max(worst, AssertHorizons(DsnComplex.Goldstone, i, HorizonsGoldstoneElevationDeg[i]));
                worst = Math.Max(worst, AssertHorizons(DsnComplex.Madrid, i, HorizonsMadridElevationDeg[i]));
                worst = Math.Max(worst, AssertHorizons(DsnComplex.Canberra, i, HorizonsCanberraElevationDeg[i]));
            }

            Assert.That(worst, Is.LessThan(HorizonsToleranceDeg));
        }

        [Test]
        public void ComplexesInView_MatchTheHorizonsElevations_AtTheTenDegreeMask()
        {
            for (int i = 0; i < HorizonsEpochsUtc.Length; i++)
            {
                DsnComplex[] inView = DsnVisibility.ComplexesInView(HorizonsEpochsUtc[i]);
                Assert.That(Array.IndexOf(inView, DsnComplex.Goldstone) >= 0, Is.EqualTo(HorizonsGoldstoneElevationDeg[i] >= DsnVisibility.ElevationMaskDeg), "Goldstone at reference " + i);
                Assert.That(Array.IndexOf(inView, DsnComplex.Madrid) >= 0, Is.EqualTo(HorizonsMadridElevationDeg[i] >= DsnVisibility.ElevationMaskDeg), "Madrid at reference " + i);
                Assert.That(Array.IndexOf(inView, DsnComplex.Canberra) >= 0, Is.EqualTo(HorizonsCanberraElevationDeg[i] >= DsnVisibility.ElevationMaskDeg), "Canberra at reference " + i);
            }

            // 2026-11-14 08:00 UTC: Madrid 58.5 degrees, Goldstone 7.5 (under the mask), Canberra -69.
            Assert.That(DsnVisibility.ComplexesInView(DsnVisibility.FirstSampleUtc), Is.EqualTo(new[] { DsnComplex.Madrid }));
            Assert.That(DsnVisibility.Describe(DsnVisibility.FirstSampleUtc), Is.EqualTo("DSN: Madrid has Mars in view · el. 59°"));

            // 2026-12-06 17:20 UTC: Canberra 33.3 and Goldstone 30.0 share Mars, highest first.
            Assert.That(DsnVisibility.ComplexesInView(HorizonsEpochsUtc[5]), Is.EqualTo(new[] { DsnComplex.Canberra, DsnComplex.Goldstone }));
            Assert.That(DsnVisibility.Describe(HorizonsEpochsUtc[5]), Is.EqualTo("DSN: Canberra + Goldstone have Mars in view"));

            // 2026-12-11 23:50 UTC: Madrid 11.9 is in view, Canberra 9.76 is just under the mask.
            Assert.That(DsnVisibility.ComplexesInView(HorizonsEpochsUtc[6]), Is.EqualTo(new[] { DsnComplex.Madrid }));
        }

        [Test]
        public void Wording_FollowsTheMask_ForOneTwoAndNoComplex()
        {
            Assert.That(DsnVisibility.ElevationMaskDeg, Is.EqualTo(10d));
            Assert.That(DsnVisibility.DescribeElevations(7.49d, 43.2d, -68.97d), Is.EqualTo("DSN: Madrid has Mars in view · el. 43°"));
            Assert.That(DsnVisibility.DescribeElevations(25d, 43d, -50d), Is.EqualTo("DSN: Madrid + Goldstone have Mars in view"));
            Assert.That(DsnVisibility.DescribeElevations(12d, -20d, 30d), Is.EqualTo("DSN: Canberra + Goldstone have Mars in view"));
            Assert.That(DsnVisibility.DescribeElevations(9.999d, -5d, 9.5d), Is.EqualTo("DSN: Mars is below 10° at all three complexes"));

            // Exactly at the mask counts as in view; a hair under it does not.
            Assert.That(DsnVisibility.DescribeElevations(10d, -5d, -30d), Is.EqualTo("DSN: Goldstone has Mars in view · el. 10°"));
            Assert.That(DsnVisibility.DescribeElevations(-5d, 9.9999d, 10.4d), Is.EqualTo("DSN: Canberra has Mars in view · el. 10°"));
            Assert.That(DsnVisibility.DescribeElevations(-5d, 89.6d, -30d), Is.EqualTo("DSN: Madrid has Mars in view · el. 90°"));

            // Not-a-number is never reported as a view or as "below the mask".
            Assert.That(DsnVisibility.DescribeElevations(double.NaN, 40d, 40d), Is.EqualTo(DsnVisibility.NoDataLine));
        }

        [Test]
        public void OutsideTheSnapshot_ThereIsNoData_AndNothingIsExtrapolated()
        {
            DateTime tooEarly = DsnVisibility.FirstSampleUtc.AddTicks(-1);
            DateTime tooLate = DsnVisibility.LastSampleUtc.AddTicks(1);

            foreach (DateTime outside in new[] { tooEarly, tooLate, Utc(2026, 11, 13, 8, 0), Utc(2026, 12, 16, 8, 0), Utc(2031, 1, 1, 0, 0), DateTime.MinValue, DateTime.MaxValue })
            {
                Assert.That(DsnVisibility.HasData(outside), Is.False);
                Assert.That(DsnVisibility.TryMarsDirectionIcrf(outside, out double rightAscension, out double declination), Is.False);
                Assert.That(double.IsNaN(rightAscension) && double.IsNaN(declination), Is.True);
                Assert.That(DsnVisibility.TryMarsDirectionOfDate(outside, out _, out _), Is.False);
                foreach (DsnComplex complex in AllComplexes)
                {
                    Assert.That(double.IsNaN(DsnVisibility.ElevationDeg(complex, outside)), Is.True);
                    Assert.That(DsnVisibility.TryElevationDeg(complex, outside, out double elevation), Is.False);
                    Assert.That(double.IsNaN(elevation), Is.True);
                }

                Assert.That(DsnVisibility.TryElevations(outside, out _, out _, out _), Is.False);
                Assert.That(DsnVisibility.ComplexesInView(outside), Is.Empty);
                Assert.That(DsnVisibility.Describe(outside), Is.EqualTo(DsnVisibility.NoDataLine));
                Assert.That(DsnVisibility.TryDescribe(outside, out string line), Is.False);
                Assert.That(line, Is.EqualTo(DsnVisibility.NoDataLine));
            }

            // Both end samples are inside, and the last instant uses the last vector itself.
            foreach (DateTime inside in new[] { DsnVisibility.FirstSampleUtc, DsnVisibility.LastSampleUtc })
            {
                Assert.That(DsnVisibility.HasData(inside), Is.True);
                Assert.That(DsnVisibility.TryDescribe(inside, out string line), Is.True);
                Assert.That(line, Does.StartWith("DSN: "));
                Assert.That(line, Is.Not.EqualTo(DsnVisibility.NoDataLine));
                foreach (DsnComplex complex in AllComplexes)
                {
                    Assert.That(DsnVisibility.ElevationDeg(complex, inside), Is.InRange(-90d, 90d));
                }
            }

            const double lastX = -143922318.302467495d;
            const double lastY = 55255256.808625430d;
            DsnVisibility.TryMarsDirectionIcrf(DsnVisibility.LastSampleUtc, out double lastRightAscension, out _);
            Assert.That(lastRightAscension, Is.EqualTo(Math.Atan2(lastY, lastX) * 180d / Math.PI).Within(1e-9d));
        }

        [Test]
        public void TimeKinds_LocalIsConverted_UnspecifiedIsTakenAsUtc()
        {
            DateTime utc = Utc(2026, 11, 20, 13, 30);
            double expected = DsnVisibility.ElevationDeg(DsnComplex.Canberra, utc);
            Assert.That(DsnVisibility.ElevationDeg(DsnComplex.Canberra, DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)), Is.EqualTo(expected));
            Assert.That(DsnVisibility.ElevationDeg(DsnComplex.Canberra, utc.ToLocalTime()), Is.EqualTo(expected).Within(1e-9d));
        }

        [Test]
        public void Elevation_PeaksAtTheCulminationAltitude_AndIsSymmetricAboutTransit()
        {
            DateTime dayStart = Utc(2026, 11, 20, 8, 0);
            foreach (DsnComplex complex in AllComplexes)
            {
                DateTime transit = dayStart;
                double peak = double.NegativeInfinity;
                double lowest = double.PositiveInfinity;
                for (int seconds = 0; seconds < 86400; seconds += 10)
                {
                    DateTime instant = dayStart.AddSeconds(seconds);
                    double elevation = DsnVisibility.ElevationDeg(complex, instant);
                    lowest = Math.Min(lowest, elevation);
                    if (elevation > peak)
                    {
                        peak = elevation;
                        transit = instant;
                    }
                }

                // Upper culmination: 90 - |latitude - declination|. Lower culmination: |latitude + declination| - 90.
                Assert.That(DsnVisibility.TryMarsDirectionOfDate(transit, out _, out double declination), Is.True);
                double latitude = DsnVisibility.LatitudeDeg(complex);
                Assert.That(peak, Is.EqualTo(90d - Math.Abs(latitude - declination)).Within(0.01d), complex + " upper culmination");
                Assert.That(lowest, Is.EqualTo(Math.Abs(latitude + declination) - 90d).Within(0.1d), complex + " lower culmination");

                // Equal hour angles east and west of the meridian give equal elevations (Mars barely moves in 4 hours).
                double rising = DsnVisibility.ElevationDeg(complex, transit.AddHours(-2));
                double setting = DsnVisibility.ElevationDeg(complex, transit.AddHours(2));
                Assert.That(rising, Is.EqualTo(setting).Within(0.1d), complex + " two hours either side of transit");
                Assert.That(rising, Is.LessThan(peak - 5d));
            }

            // Mirror latitudes: Goldstone (35.43 N) and Canberra (35.40 S) see a northern Mars very differently.
            DsnVisibility.TryMarsDirectionOfDate(dayStart, out _, out double marsDeclination);
            Assert.That(marsDeclination, Is.GreaterThan(10d), "Mars is north of the equator throughout the snapshot");
        }

        [Test]
        public void AcrossTheSnapshot_SomeComplexAlwaysHasMars_NeverAllThree_AndHandoversOverlap()
        {
            int none = 0;
            int one = 0;
            int two = 0;
            int three = 0;
            for (DateTime instant = DsnVisibility.FirstSampleUtc; instant <= DsnVisibility.LastSampleUtc; instant = instant.AddMinutes(5))
            {
                switch (DsnVisibility.ComplexesInView(instant).Length)
                {
                    case 0:
                        none++;
                        break;
                    case 1:
                        one++;
                        break;
                    case 2:
                        two++;
                        break;
                    default:
                        three++;
                        break;
                }
            }

            Assert.That(none, Is.EqualTo(0), "at a 10 degree mask one complex always has Mars in view in this snapshot");
            Assert.That(three, Is.EqualTo(0), "three sites 120 degrees apart never share one planet above 10 degrees");
            Assert.That(one, Is.GreaterThan(0));
            Assert.That(two, Is.GreaterThan(0), "neighbouring complexes overlap during handover");
            Assert.That(none + one + two, Is.EqualTo((31 * 288) + 1));
        }

        private static double AssertHorizons(DsnComplex complex, int reference, double horizonsElevationDeg)
        {
            Assert.That(DsnVisibility.TryElevationDeg(complex, HorizonsEpochsUtc[reference], out double elevation), Is.True);
            double difference = Math.Abs(elevation - horizonsElevationDeg);
            Assert.That(difference, Is.LessThan(HorizonsToleranceDeg),
                complex + " at " + HorizonsEpochsUtc[reference].ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                + " UTC: computed " + elevation.ToString("0.000", CultureInfo.InvariantCulture)
                + ", JPL Horizons " + horizonsElevationDeg.ToString("0.000", CultureInfo.InvariantCulture));
            return difference;
        }

        private static void AssertSample(int index, DateTime epochUtc, double xKm, double yKm, double zKm)
        {
            Assert.That(DsnVisibility.SampleEpochUtc(index), Is.EqualTo(epochUtc), "epoch of sample " + index);
            DsnVisibility.SampleVectorKm(index, out double x, out double y, out double z);
            Assert.That(x, Is.EqualTo(xKm).Within(VectorToleranceKm), "x_km of sample " + index);
            Assert.That(y, Is.EqualTo(yKm).Within(VectorToleranceKm), "y_km of sample " + index);
            Assert.That(z, Is.EqualTo(zKm).Within(VectorToleranceKm), "z_km of sample " + index);
        }

        private static double Number(string text)
        {
            return double.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        /// <summary>Looks for a repository file from the current directory upwards (Unity runs tests in the project root).</summary>
        private static string FindRepositoryFile(string relativePath)
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}

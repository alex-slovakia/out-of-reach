using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OutOfReach.Simulation
{
    /// <summary>One epoch of the reference ephemeris: where Mars' centre is relative to Earth's centre.</summary>
    public readonly struct EarthMarsSample
    {
        public EarthMarsSample(int index, DateTime epochUtc, double julianDayUt, double xKm, double yKm, double zKm, double distanceKm)
        {
            Index = index;
            EpochUtc = epochUtc;
            JulianDayUt = julianDayUt;
            XKm = xKm;
            YKm = yKm;
            ZKm = zKm;
            DistanceKm = distanceKm;
            OneWayLightSeconds = SignalDelay.OneWaySeconds(distanceKm);
        }

        /// <summary>Zero-based row in the snapshot. Row 0 is elapsed day 0; a UI that calls it "Day 1" adds one.</summary>
        public int Index { get; }

        public DateTime EpochUtc { get; }

        public double JulianDayUt { get; }

        public double XKm { get; }

        public double YKm { get; }

        public double ZKm { get; }

        /// <summary>Geometric Earth-centre to Mars-centre range, km (NASA/JPL data).</summary>
        public double DistanceKm { get; }

        /// <summary>DERIVED: DistanceKm / speed of light, seconds.</summary>
        public double OneWayLightSeconds { get; }

        /// <summary>DERIVED: twice the one-way light time at this frozen epoch, seconds. No processing time included.</summary>
        public double MinimumRoundTripSeconds => 2d * OneWayLightSeconds;
    }

    /// <summary>
    /// The validated, immutable Earth-Mars reference table the communication delay is computed from.
    /// Parsing uses the invariant culture and rejects anything that is not exactly the reviewed format: a wrong
    /// header, a wrong field count, non-finite or non-positive numbers, epochs that are not strictly increasing, a
    /// distance that does not match its own vector, a light time that does not match distance / c, or text whose
    /// SHA-256 differs from the expected hash. Selection returns the last sample at or before a mission epoch and
    /// rejects epochs outside the snapshot interval; nothing is interpolated or extrapolated.
    /// </summary>
    public sealed class EarthMarsReference
    {
        public const string ExpectedHeader = "epoch_utc,julian_day_ut,x_km,y_km,z_km,earth_mars_distance_km,one_way_seconds,minimum_round_trip_seconds";

        private const string EpochFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        /// <summary>Relative slack between the distance column and the length of its vector (same as the offline verifier).</summary>
        private const double RelativeTolerance = 1e-12;

        /// <summary>The light-time columns are rounded to nine decimals in the snapshot.</summary>
        private const double RoundedSecondsTolerance = 1e-9;

        private static readonly object EmbeddedLock = new object();
        private static EarthMarsReference embedded;

        private readonly EarthMarsSample[] samples;

        private EarthMarsReference(string datasetId, string sha256, EarthMarsSample[] samples)
        {
            DatasetId = datasetId;
            Sha256 = sha256;
            this.samples = samples;
            Samples = Array.AsReadOnly(samples);
        }

        /// <summary>
        /// The snapshot compiled into the game (<see cref="HorizonsEarthMars2026Snapshot"/>), verified against its
        /// recorded SHA-256 on first use. Throws <see cref="FormatException"/> if the embedded text was altered.
        /// </summary>
        public static EarthMarsReference Embedded
        {
            get
            {
                lock (EmbeddedLock)
                {
                    if (embedded == null)
                    {
                        embedded = Parse(
                            HorizonsEarthMars2026Snapshot.NormalizedCsv,
                            HorizonsEarthMars2026Snapshot.DatasetId,
                            HorizonsEarthMars2026Snapshot.NormalizedSha256);
                    }

                    return embedded;
                }
            }
        }

        /// <summary>dataset_id of the snapshot manifest.</summary>
        public string DatasetId { get; }

        /// <summary>Lower-case hexadecimal SHA-256 of the normalized CSV text this table was parsed from.</summary>
        public string Sha256 { get; }

        public IReadOnlyList<EarthMarsSample> Samples { get; }

        public int Count => samples.Length;

        public DateTime FirstEpochUtc => samples[0].EpochUtc;

        public DateTime LastEpochUtc => samples[samples.Length - 1].EpochUtc;

        /// <summary>
        /// Parses normalized CSV text. Throws <see cref="FormatException"/> with the line number and the reason when
        /// anything is invalid, so a damaged table can never be half loaded.
        /// </summary>
        /// <param name="normalizedCsv">Header plus one line per sample, LF line endings.</param>
        /// <param name="datasetId">Reference ID reported to the mission configuration and the report.</param>
        /// <param name="expectedSha256">When given, the text's SHA-256 must equal it (hexadecimal, any case).</param>
        public static EarthMarsReference Parse(string normalizedCsv, string datasetId, string expectedSha256 = null)
        {
            if (normalizedCsv == null)
            {
                throw new ArgumentNullException(nameof(normalizedCsv));
            }

            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("A dataset ID is required.", nameof(datasetId));
            }

            string sha256 = ComputeSha256(normalizedCsv);
            if (expectedSha256 != null && !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"Reference data integrity check failed for '{datasetId}': SHA-256 is {sha256}, expected {expectedSha256.ToLowerInvariant()}.");
            }

            string[] lines = normalizedCsv.Split('\n');
            int lineCount = lines.Length;
            if (lineCount > 0 && lines[lineCount - 1].Length == 0)
            {
                lineCount--;
            }

            if (lineCount < 2)
            {
                throw new FormatException("Reference data needs a header and at least one sample.");
            }

            if (!string.Equals(lines[0], ExpectedHeader, StringComparison.Ordinal))
            {
                throw new FormatException("Reference data line 1: unexpected header '" + lines[0] + "'.");
            }

            var parsed = new EarthMarsSample[lineCount - 1];
            for (int i = 1; i < lineCount; i++)
            {
                parsed[i - 1] = ParseLine(lines[i], i + 1, i - 1);
                if (i > 1 && parsed[i - 1].EpochUtc <= parsed[i - 2].EpochUtc)
                {
                    throw new FormatException($"Reference data line {i + 1}: epochs must be strictly increasing.");
                }
            }

            return new EarthMarsReference(datasetId, sha256, parsed);
        }

        /// <summary>
        /// Selects the last sample at or before <paramref name="epochUtc"/>. An epoch before the first sample or
        /// after the last one is outside the reviewed snapshot and is rejected with an explanation.
        /// </summary>
        public bool TrySelect(DateTime epochUtc, out EarthMarsSample sample, out string problem)
        {
            if (epochUtc < FirstEpochUtc)
            {
                sample = default(EarthMarsSample);
                problem = "No Earth-Mars reference sample exists before " + FormatEpoch(FirstEpochUtc) + ".";
                return false;
            }

            if (epochUtc > LastEpochUtc)
            {
                sample = default(EarthMarsSample);
                problem = "The Earth-Mars reference snapshot ends on " + FormatEpoch(LastEpochUtc) + "; there is no reviewed sample for " + FormatEpoch(epochUtc) + ".";
                return false;
            }

            int low = 0;
            int high = samples.Length - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (samples[middle].EpochUtc <= epochUtc)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            sample = samples[low];
            problem = null;
            return true;
        }

        /// <summary>Lower-case hexadecimal SHA-256 of the text's UTF-8 bytes (the snapshot is plain ASCII).</summary>
        public static string ComputeSha256(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(text));
            }

            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        /// <summary>"2026-11-14 08:00 UTC".</summary>
        public static string FormatEpoch(DateTime epochUtc)
        {
            return epochUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        }

        private static EarthMarsSample ParseLine(string line, int lineNumber, int index)
        {
            string[] fields = line.Split(',');
            if (fields.Length != 8)
            {
                throw new FormatException($"Reference data line {lineNumber}: expected 8 fields, found {fields.Length}.");
            }

            if (!DateTime.TryParseExact(fields[0], EpochFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime epoch))
            {
                throw new FormatException($"Reference data line {lineNumber}: '{fields[0]}' is not a UTC epoch like 2026-11-14T08:00:00Z.");
            }

            double julianDay = ParseNumber(fields[1], lineNumber, "julian_day_ut");
            double x = ParseNumber(fields[2], lineNumber, "x_km");
            double y = ParseNumber(fields[3], lineNumber, "y_km");
            double z = ParseNumber(fields[4], lineNumber, "z_km");
            double distance = ParseNumber(fields[5], lineNumber, "earth_mars_distance_km");
            double oneWay = ParseNumber(fields[6], lineNumber, "one_way_seconds");
            double roundTrip = ParseNumber(fields[7], lineNumber, "minimum_round_trip_seconds");

            if (distance <= 0d)
            {
                throw new FormatException($"Reference data line {lineNumber}: the distance must be greater than zero.");
            }

            double norm = Math.Sqrt((x * x) + (y * y) + (z * z));
            if (Math.Abs(norm - distance) > distance * RelativeTolerance)
            {
                throw new FormatException($"Reference data line {lineNumber}: the distance does not match the length of its position vector.");
            }

            double derivedOneWay = SignalDelay.OneWaySeconds(distance);
            if (Math.Abs(derivedOneWay - oneWay) > RoundedSecondsTolerance)
            {
                throw new FormatException($"Reference data line {lineNumber}: one_way_seconds does not equal distance / speed of light.");
            }

            if (Math.Abs((2d * derivedOneWay) - roundTrip) > RoundedSecondsTolerance)
            {
                throw new FormatException($"Reference data line {lineNumber}: minimum_round_trip_seconds does not equal twice the one-way light time.");
            }

            return new EarthMarsSample(index, DateTime.SpecifyKind(epoch, DateTimeKind.Utc), julianDay, x, y, z, distance);
        }

        private static double ParseNumber(string text, int lineNumber, string column)
        {
            if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new FormatException($"Reference data line {lineNumber}: {column} '{text}' is not a finite invariant-culture number.");
            }

            return value;
        }
    }
}

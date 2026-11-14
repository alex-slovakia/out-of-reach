using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>The packaged NASA/JPL Horizons snapshot: identity, known rows, units, selection and rejection of bad data.</summary>
    public sealed class EarthMarsReferenceTests
    {
        public const string ManifestSha256 = "694f3606e7abc1d94941b428dc1f7a9364bb07d1810327b6343a77986a79e5af";
        public const string DatasetId = "jpl-horizons-earth-mars-2026-v1";

        private const string Row0 = "2026-11-14T08:00:00Z,2461358.833333333,-161160097.612842590,102305448.047787502,51472924.995187446,197707975.990852982,659.482821248,1318.965642497";
        private const string Row1 = "2026-11-15T08:00:00Z,2461359.833333333,-161009956.951080203,100451702.482303798,50710921.448357768,196434081.368594199,655.233566178,1310.467132356";

        private static DateTime Utc(int year, int month, int day, int hour = 8, int minute = 0, int second = 0)
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        private static string Table(params string[] rows)
        {
            return EarthMarsReference.ExpectedHeader + "\n" + string.Join("\n", rows) + "\n";
        }

        [Test]
        public void EmbeddedSnapshot_PassesItsIntegrityCheck_AndKeepsTheManifestIdentity()
        {
            EarthMarsReference reference = EarthMarsReference.Embedded;

            Assert.That(reference.DatasetId, Is.EqualTo(DatasetId));
            Assert.That(AuroraMission.AstronomicalReferenceId, Is.EqualTo(DatasetId), "the mission configuration names the packaged dataset");
            Assert.That(reference.Sha256, Is.EqualTo(ManifestSha256));
            Assert.That(HorizonsEarthMars2026Snapshot.NormalizedSha256, Is.EqualTo(ManifestSha256));
            Assert.That(reference.Count, Is.EqualTo(32));
            Assert.That(reference.FirstEpochUtc, Is.EqualTo(Utc(2026, 11, 14)));
            Assert.That(reference.LastEpochUtc, Is.EqualTo(Utc(2026, 12, 15)));
            for (int i = 0; i < reference.Count; i++)
            {
                EarthMarsSample sample = reference.Samples[i];
                Assert.That(sample.Index, Is.EqualTo(i));
                Assert.That(sample.EpochUtc, Is.EqualTo(Utc(2026, 11, 14).AddDays(i)), "daily cadence at 08:00 UTC");
                Assert.That(sample.EpochUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                if (i > 0)
                {
                    Assert.That(sample.DistanceKm, Is.LessThan(reference.Samples[i - 1].DistanceKm), "the range decreases across this snapshot");
                }
            }
        }

        [Test]
        public void EmbeddedSnapshot_IsByteIdenticalToTheCanonicalFile_AndMatchesTheManifest()
        {
            string directory = Directory.GetCurrentDirectory();
            string data = null;
            for (int i = 0; i < 6 && directory != null; i++)
            {
                string candidate = Path.Combine(directory, "docs", "data", "horizons");
                if (Directory.Exists(candidate))
                {
                    data = candidate;
                    break;
                }

                directory = Path.GetDirectoryName(directory);
            }

            if (data == null)
            {
                Assert.Ignore("docs/data/horizons was not found above the working directory; the embedded hash check still ran in the other tests.");
            }

            byte[] canonical = File.ReadAllBytes(Path.Combine(data, "earth-mars-2026-11-14--2026-12-15.csv"));
            byte[] embedded = Encoding.ASCII.GetBytes(HorizonsEarthMars2026Snapshot.NormalizedCsv);
            Assert.That(embedded, Is.EqualTo(canonical), "the packaged copy must equal the committed CSV byte for byte");

            string manifest = File.ReadAllText(Path.Combine(data, "manifest.json"));
            Assert.That(manifest, Does.Contain("\"dataset_id\": \"" + HorizonsEarthMars2026Snapshot.DatasetId + "\""));
            Assert.That(manifest, Does.Contain("\"normalized_sha256\": \"" + HorizonsEarthMars2026Snapshot.NormalizedSha256 + "\""));
            Assert.That(manifest, Does.Contain("\"raw_sha256\": \"" + HorizonsEarthMars2026Snapshot.RawSha256 + "\""));
            Assert.That(manifest, Does.Contain("\"sample_count\": " + HorizonsEarthMars2026Snapshot.SampleCount));
        }

        [TestCase(0, 14, 11, 197707975.990852982d, 659.482821248d, 1318.965642497d)]
        [TestCase(30, 14, 12, 158766426.622751743d, 529.587794443d, 1059.175588885d)]
        [TestCase(31, 15, 12, 157475148.505746007d, 525.280554275d, 1050.561108550d)]
        public void KnownRows_HaveTheReviewedDistance_AndALightTimeDerivedFromIt(int index, int day, int month, double distanceKm, double oneWaySeconds, double roundTripSeconds)
        {
            EarthMarsSample sample = EarthMarsReference.Embedded.Samples[index];

            Assert.That(sample.EpochUtc, Is.EqualTo(Utc(2026, month, day)));
            Assert.That(sample.DistanceKm, Is.EqualTo(distanceKm).Within(1e-6));
            Assert.That(sample.OneWayLightSeconds, Is.EqualTo(sample.DistanceKm / 299792.458d).Within(1e-12), "derived, not read from the file");
            Assert.That(sample.OneWayLightSeconds, Is.EqualTo(oneWaySeconds).Within(1e-9));
            Assert.That(sample.MinimumRoundTripSeconds, Is.EqualTo(roundTripSeconds).Within(2e-9));
        }

        [Test]
        public void SignalDelay_UsesKilometresAndSeconds_AndIncludesBothLegsAndProcessing()
        {
            Assert.That(SignalDelay.OneWaySeconds(299792.458d), Is.EqualTo(1d));
            Assert.That(SignalDelay.OneWaySeconds(197707975.990852982d), Is.EqualTo(659.482821248d).Within(1e-9));
            Assert.That(SignalDelay.EarliestReplySeconds(659.482821248d, 600d), Is.EqualTo(1918.965642496d).Within(1e-9));
            Assert.That(SignalDelay.EarliestReplySeconds(659.482821248d, 0d), Is.GreaterThan(659.482821248d), "never a one-way delay");
            Assert.Throws<ArgumentOutOfRangeException>(() => SignalDelay.OneWaySeconds(-1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => SignalDelay.OneWaySeconds(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => SignalDelay.EarliestReplySeconds(1d, double.PositiveInfinity));
        }

        [Test]
        public void Selection_TakesTheLastSampleAtOrBeforeTheEpoch_AndRejectsEpochsOutsideTheSnapshot()
        {
            EarthMarsReference reference = EarthMarsReference.Embedded;

            Assert.That(Select(reference, Utc(2026, 11, 14)), Is.EqualTo(0));
            Assert.That(Select(reference, Utc(2026, 11, 15, 7, 59, 59)), Is.EqualTo(0), "the next sample is not used early");
            Assert.That(Select(reference, Utc(2026, 11, 15)), Is.EqualTo(1));
            Assert.That(Select(reference, Utc(2026, 12, 14, 23)), Is.EqualTo(30), "mission day 31");
            Assert.That(Select(reference, Utc(2026, 12, 15)), Is.EqualTo(31), "the boundary sample is valid exactly at its epoch");

            Assert.That(reference.TrySelect(Utc(2026, 11, 14, 7, 59, 59), out EarthMarsSample before, out string early), Is.False);
            Assert.That(early, Does.Contain("before 2026-11-14 08:00 UTC"));
            Assert.That(reference.TrySelect(Utc(2026, 12, 15, 8, 0, 1), out EarthMarsSample after, out string late), Is.False);
            Assert.That(late, Does.Contain("ends on 2026-12-15 08:00 UTC"));
        }

        private static int Select(EarthMarsReference reference, DateTime epoch)
        {
            Assert.That(reference.TrySelect(epoch, out EarthMarsSample sample, out string problem), Is.True, problem);
            return sample.Index;
        }

        [Test]
        public void Parse_AcceptsTheReviewedFormat()
        {
            EarthMarsReference parsed = EarthMarsReference.Parse(Table(Row0, Row1), "test");

            Assert.That(parsed.Count, Is.EqualTo(2));
            Assert.That(parsed.Samples[1].XKm, Is.EqualTo(-161009956.951080203d).Within(1e-6));
            Assert.That(parsed.Sha256, Has.Length.EqualTo(64));
        }

        private static System.Collections.Generic.IEnumerable<TestCaseData> DamagedTables()
        {
            const string end = "\n";
            const string head = EarthMarsReference.ExpectedHeader + end;
            const string prefix = "2026-11-14T08:00:00Z,2461358.833333333,-161160097.612842590,102305448.047787502,51472924.995187446,";
            yield return Damaged("wrong header", "wrong,header" + end + Row0 + end, "unexpected header");
            yield return Damaged("no samples", head, "at least one sample");
            yield return Damaged("wrong field count", head + "2026-11-14T08:00:00Z,1,2,3" + end, "expected 8 fields");
            yield return Damaged("local date format", head + Row0.Replace("2026-11-14T08:00:00Z", "14.11.2026 08:00") + end, "not a UTC epoch");
            yield return Damaged("NaN distance", head + prefix + "NaN,659.482821248,1318.965642497" + end, "not a finite");
            yield return Damaged("exponent notation", head + prefix + "1.97707975990852982e8,659.482821248,1318.965642497" + end, "not a finite");
            yield return Damaged("decimal comma", head + Row0.Replace("197707975.990852982", "197707975,990852982") + end, "expected 8 fields");
            yield return Damaged("distance that is not the vector length", head + prefix + "197707985.990852982,659.482821248,1318.965642497" + end, "position vector");
            yield return Damaged("light time that is not distance over c", head + prefix + "197707975.990852982,1080.000000000,1318.965642497" + end, "distance / speed of light");
            yield return Damaged("one-way value in the round-trip column", head + prefix + "197707975.990852982,659.482821248,659.482821248" + end, "twice the one-way");
            yield return Damaged("epochs out of order", head + Row1 + end + Row0 + end, "strictly increasing");
        }

        private static TestCaseData Damaged(string name, string text, string expectedReason)
        {
            return new TestCaseData(name, text, expectedReason).SetName("Parse rejects " + name);
        }

        [TestCaseSource(nameof(DamagedTables))]
        public void Parse_RejectsDamagedTables(string name, string text, string expectedReason)
        {
            FormatException error = Assert.Throws<FormatException>(() => EarthMarsReference.Parse(text, "test"), name);
            Assert.That(error.Message, Does.Contain(expectedReason));
        }

        [Test]
        public void Parse_RejectsTextThatDoesNotMatchTheRecordedHash()
        {
            string altered = HorizonsEarthMars2026Snapshot.NormalizedCsv.Replace("197707975.990852982", "197707975.990852983");

            FormatException error = Assert.Throws<FormatException>(() =>
                EarthMarsReference.Parse(altered, DatasetId, HorizonsEarthMars2026Snapshot.NormalizedSha256));

            Assert.That(error.Message, Does.Contain("integrity check failed"));
            Assert.DoesNotThrow(() => EarthMarsReference.Parse(HorizonsEarthMars2026Snapshot.NormalizedCsv, DatasetId, ManifestSha256.ToUpperInvariant()));
        }
    }
}

using System;
using NUnit.Framework;
using OutOfReach.Presentation.UI.Chapter;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA: the strip's Deep Space Network line is the DSN-CALC calculator's own line for the mission date,
    /// held for ten simulated minutes and marked as line-of-sight geometry.
    /// </summary>
    public sealed class DsnLineCopyTests
    {
        private static DateTime Utc(int month, int day, int hour, int minute = 0, int second = 0) =>
            new DateTime(2026, month, day, hour, minute, second, DateTimeKind.Utc);

        [Test]
        public void TheLine_NamesTheComplex_AndSaysItIsLineOfSight()
        {
            Assert.That(ChapterCopy.DsnLine(Utc(11, 14, 8)), Is.EqualTo("DSN: Madrid has Mars in view · el. 59° (line of sight)"));
            Assert.That(ChapterCopy.DsnLine(Utc(11, 14, 10)), Is.EqualTo("DSN: Madrid + Goldstone have Mars in view (line of sight)"));
            Assert.That(ChapterCopy.DsnLine(Utc(11, 14, 14)), Is.EqualTo("DSN: Goldstone has Mars in view · el. 69° (line of sight)"));
            Assert.That(ChapterCopy.DsnLine(Utc(11, 14, 20)), Is.EqualTo("DSN: Canberra has Mars in view · el. 39° (line of sight)"));
        }

        [Test]
        public void TheLine_IsTheCalculatorsOwn_ForEveryStepOfTheSnapshot()
        {
            int lines = 0;
            for (DateTime at = DsnVisibility.FirstSampleUtc; at <= DsnVisibility.LastSampleUtc; at = at.AddMinutes(170))
            {
                Assert.That(ChapterCopy.DsnLine(at), Is.EqualTo(DsnVisibility.Describe(ChapterCopy.DsnStep(at)) + ChapterCopy.DsnLineOfSight), at.ToString("u"));
                lines++;
            }

            Assert.That(lines, Is.GreaterThan(250));
        }

        [Test]
        public void TheLine_IsEmptyOutsideTheCommittedSnapshot()
        {
            Assert.That(ChapterCopy.DsnLine(Utc(11, 14, 7, 59)), Is.Empty);
            Assert.That(ChapterCopy.DsnLine(Utc(12, 15, 8, 10)), Is.Empty);
            Assert.That(ChapterCopy.DsnLine(Utc(12, 16, 8)), Is.Empty);
            Assert.That(ChapterCopy.DsnLine(Utc(12, 15, 8)), Is.Not.Empty, "the last sample itself is data");
        }

        [Test]
        public void TheStep_IsTenMinutesOfTheMissionDate()
        {
            Assert.That(ChapterCopy.DsnStep(Utc(11, 20, 13, 19, 59)), Is.EqualTo(Utc(11, 20, 13, 10)));
            Assert.That(ChapterCopy.DsnStep(Utc(11, 20, 13, 20)), Is.EqualTo(Utc(11, 20, 13, 20)));
            Assert.That(ChapterCopy.DsnStep(Utc(11, 20, 13, 19, 59)).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(ChapterCopy.DsnLine(Utc(11, 20, 13, 10)), Is.EqualTo(ChapterCopy.DsnLine(Utc(11, 20, 13, 19, 59))));
        }
    }
}

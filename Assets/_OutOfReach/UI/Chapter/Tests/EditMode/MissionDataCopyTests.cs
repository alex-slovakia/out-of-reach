using NUnit.Framework;
using OutOfReach.Presentation.UI.Chapter;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA: the strip's NASA/JPL Horizons line. Display rule of NASA-DATA.md: distance to the nearest thousand
    /// kilometres, delay to whole seconds, and the number named for what it is (Earth-Mars range, a Mars-vicinity proxy).
    /// </summary>
    public sealed class MissionDataCopyTests
    {
        private const double Day1Km = 197707975.990852982d, Day1Light = 659.482821248d;
        private const double Day32Km = 157475149.2d, Day32Light = 525.281d;

        [Test]
        public void Distance_IsRoundedToTheNearestThousandKilometres()
        {
            Assert.That(ChapterCopy.EarthDistance(Day1Km), Is.EqualTo("197,708,000 km"));
            Assert.That(ChapterCopy.EarthDistance(Day32Km), Is.EqualTo("157,475,000 km"));
            Assert.That(ChapterCopy.EarthDistance(196434081.368594199d), Is.EqualTo("196,434,000 km"));
            Assert.That(ChapterCopy.EarthDistance(1500d), Is.EqualTo("2,000 km"), "midpoint away from zero, like Core");
        }

        [Test]
        public void LightTime_IsRoundedToWholeSeconds_InCoresFormat()
        {
            Assert.That(ChapterCopy.LightTime(Day1Light), Is.EqualTo("10 min 59 s"));
            Assert.That(ChapterCopy.LightTime(Day32Light), Is.EqualTo("8 min 45 s"));
            Assert.That(ChapterCopy.LightTime(655.233566178d), Is.EqualTo("10 min 55 s"));
            Assert.That(ChapterCopy.LightTime(544.6d), Is.EqualTo("9 min 05 s"), "seconds are zero padded, like Core");
            Assert.That(ChapterCopy.LightTime(59.5d), Is.EqualTo("1 min 00 s"));
            Assert.That(ChapterCopy.LightTime(42.4d), Is.EqualTo("42 s"));
            Assert.That(ChapterCopy.LightTime(3725d), Is.EqualTo("1 h 02 min 05 s"));
        }

        [Test]
        public void EarthLine_StatesDistanceAndOneWayDelay()
        {
            Assert.That(ChapterCopy.EarthLine(Day1Km, Day1Light), Is.EqualTo("Earth 197,708,000 km · signal 10 min 59 s one way"));
            Assert.That(ChapterCopy.EarthLine(Day32Km, Day32Light), Is.EqualTo("Earth 157,475,000 km · signal 8 min 45 s one way"));
        }

        [Test]
        public void EarthLine_IsEmpty_WhenCoreHasNoUsableSample()
        {
            Assert.That(ChapterCopy.EarthLine(double.NaN, Day1Light), Is.Empty);
            Assert.That(ChapterCopy.EarthLine(double.PositiveInfinity, Day1Light), Is.Empty);
            Assert.That(ChapterCopy.EarthLine(0d, Day1Light), Is.Empty);
            Assert.That(ChapterCopy.EarthLine(-1d, Day1Light), Is.Empty);
            Assert.That(ChapterCopy.EarthLine(Day1Km, double.NaN), Is.Empty);
            Assert.That(ChapterCopy.EarthLine(Day1Km, -1d), Is.Empty);
        }

        [Test]
        public void Source_NamesHorizons_AndSaysWhatTheNumberStandsFor()
        {
            Assert.That(ChapterCopy.EarthLineSource(false),
                Is.EqualTo("NASA/JPL Horizons · today's Earth–Mars range, used as a Mars-vicinity proxy"));
            Assert.That(ChapterCopy.EarthLineSource(true),
                Is.EqualTo("NASA/JPL Horizons · Earth–Mars range when the call was sent, used as a Mars-vicinity proxy"));
        }
    }
}

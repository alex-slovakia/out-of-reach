using NUnit.Framework;
using OutOfReach.Presentation.UI.Chapter;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA (optional line): elapsed mission days x 1.8 mSv/day, the average the RAD instrument measured inside the
    /// Mars Science Laboratory spacecraft on its cruise to Mars (NASA/JPL, 30 May 2013). Always labelled an estimate.
    /// </summary>
    public sealed class RadiationEstimateCopyTests
    {
        private const double Day = 86400d;

        [Test]
        public void TheRate_IsTheOneNasaStates()
        {
            Assert.That(ChapterCopy.RadCruiseMilliSievertsPerDay, Is.EqualTo(1.8d));
        }

        [Test]
        public void Dose_IsElapsedDaysTimesTheRate()
        {
            Assert.That(ChapterCopy.RadiationDose(0d), Is.EqualTo("0.0 mSv"));
            Assert.That(ChapterCopy.RadiationDose(Day / 2d), Is.EqualTo("0.9 mSv"));
            Assert.That(ChapterCopy.RadiationDose(Day), Is.EqualTo("1.8 mSv"));
            Assert.That(ChapterCopy.RadiationDose(5d * Day), Is.EqualTo("9.0 mSv"));
            Assert.That(ChapterCopy.RadiationDose(6d * Day), Is.EqualTo("11 mSv"), "whole millisieverts from ten up");
            Assert.That(ChapterCopy.RadiationDose(14d * Day), Is.EqualTo("25 mSv"));
            Assert.That(ChapterCopy.RadiationDose(30d * Day), Is.EqualTo("54 mSv"));
        }

        [Test]
        public void Dose_NeverGoesNegative_AndIsEmptyForANonFiniteClock()
        {
            Assert.That(ChapterCopy.RadiationDose(-5000d), Is.EqualTo("0.0 mSv"));
            Assert.That(ChapterCopy.RadiationDose(double.NaN), Is.Empty);
            Assert.That(ChapterCopy.RadiationDose(double.PositiveInfinity), Is.Empty);
            Assert.That(ChapterCopy.RadiationLine(double.NaN), Is.Empty);
        }

        [Test]
        public void TheLine_SaysItIsAnEstimate_AndNamesTheMeasurement()
        {
            Assert.That(ChapterCopy.RadiationLine(14d * Day),
                Is.EqualTo("Radiation since Day 1 ≈ 25 mSv (est. at 1.8 mSv/day, NASA MSL RAD cruise measurement)"));
            Assert.That(ChapterCopy.RadiationLine(Day / 2d),
                Is.EqualTo("Radiation since Day 1 ≈ 0.9 mSv (est. at 1.8 mSv/day, NASA MSL RAD cruise measurement)"));
        }
    }
}

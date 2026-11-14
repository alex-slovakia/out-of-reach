using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    internal static class Fixtures
    {
        public const double Tolerance = 1e-9;

        public static MissionSession NewSession(string presetId = null)
        {
            return new MissionSession(AuroraMission.CreateDefinition(), null, presetId);
        }

        public static MissionSession LaunchedSession(string presetId = null)
        {
            MissionSession session = NewSession(presetId);
            CommandResult launch = session.Planner.TryLaunch();
            Assert.That(launch.Succeeded, Is.True, launch.Explanation);
            return session;
        }

        public static void AssertSnapshot(ResourceSnapshot expected, ResourceSnapshot actual, double tolerance = Tolerance)
        {
            Assert.That(actual.OxygenKg, Is.EqualTo(expected.OxygenKg).Within(tolerance), "OxygenKg");
            Assert.That(actual.WaterLitres, Is.EqualTo(expected.WaterLitres).Within(tolerance), "WaterLitres");
            Assert.That(actual.FoodKg, Is.EqualTo(expected.FoodKg).Within(tolerance), "FoodKg");
            Assert.That(actual.BatteryKWh, Is.EqualTo(expected.BatteryKWh).Within(tolerance), "BatteryKWh");
            Assert.That(actual.SparePartsCount, Is.EqualTo(expected.SparePartsCount), "SparePartsCount");
            Assert.That(actual.CarbonDioxidePpm, Is.EqualTo(expected.CarbonDioxidePpm).Within(tolerance), "CarbonDioxidePpm");
        }
    }
}

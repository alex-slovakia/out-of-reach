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

        /// <summary>Launched, impact applied and acknowledged: the mission is in Emergency.</summary>
        public static MissionSession EmergencySession(string presetId = null)
        {
            MissionSession session = LaunchedSession(presetId);
            TriggerImpact(session);
            return session;
        }

        /// <summary>Completes the cockpit check and acknowledges the Impact cinematic the way presentation would.</summary>
        public static void TriggerImpact(MissionSession session)
        {
            CommandResult check = session.Director.TryCompleteCockpitCheck();
            Assert.That(check.Succeeded, Is.True, check.Explanation);
            AcknowledgeActiveCinematic(session);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
        }

        public static void AcknowledgeActiveCinematic(MissionSession session, bool skipped = false)
        {
            if (session.Cinematics.IsWaiting)
            {
                session.Events.Publish(new CinematicFinished(session.Cinematics.ActiveCinematic, session.Cinematics.ActiveSequenceId, skipped));
            }
        }

        public static CommandResult Repair(MissionSession session, ShipSystemId system)
        {
            return session.Systems.Get(system).TryRepair(session.Resources);
        }

        public static void RepairOrFail(MissionSession session, ShipSystemId system)
        {
            CommandResult result = Repair(session, system);
            Assert.That(result.Succeeded, Is.True, result.Explanation);
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

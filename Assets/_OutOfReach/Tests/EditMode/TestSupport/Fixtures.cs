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

        /// <summary>Emergency with power and life support repaired: the mission is in Repair.</summary>
        public static MissionSession StabilizedSession(string presetId = null)
        {
            MissionSession session = EmergencySession(presetId);
            RepairOrFail(session, ShipSystemId.Power);
            RepairOrFail(session, ShipSystemId.LifeSupport);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
            return session;
        }

        public static void SuitUp(MissionSession session)
        {
            CommandResult result = session.Eva.TryPrepareSuit();
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        /// <summary>Seals and depressurizes the airlock and waits until the outer hatch is open (Eva phase).</summary>
        public static void OpenOuterHatch(MissionSession session)
        {
            CommandResult begun = session.Eva.TryBeginEva();
            Assert.That(begun.Succeeded, Is.True, begun.Explanation);
            Assert.That(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds).Succeeded, Is.True);
            Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.OuterOpen));
        }

        public static void GoOutside(MissionSession session)
        {
            CommandResult result = session.Eva.TryGoOutside();
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        /// <summary>Returns inside if needed, closes the outer hatch and waits for repressurization.</summary>
        public static void ReturnAndRepressurize(MissionSession session)
        {
            if (session.Eva.IsOutside)
            {
                Assert.That(session.Eva.TryReturnInside().Succeeded, Is.True);
            }

            CommandResult ended = session.Eva.TryEndEva();
            Assert.That(ended.Succeeded, Is.True, ended.Explanation);
            Assert.That(session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds).Succeeded, Is.True);
            Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.InnerOpen));
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

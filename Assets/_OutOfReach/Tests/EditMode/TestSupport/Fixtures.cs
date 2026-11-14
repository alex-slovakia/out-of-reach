using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

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

        /// <summary>
        /// Stabilized, antenna repaired on one EVA and the crew back inside: the mission is in Transmission.
        /// No frames pass, so the elapsed time is exactly suit preparation + depressurization + repressurization.
        /// </summary>
        public static MissionSession TransmissionSession(string presetId = null, MissionDefinition definition = null)
        {
            MissionSession session = definition == null ? StabilizedSession(presetId) : Stabilize(new MissionSession(definition, null, presetId));
            CompleteEva(session);
            return session;
        }

        /// <summary>One EVA that repairs the antenna and returns: Repair to Transmission.</summary>
        public static void CompleteEva(MissionSession session)
        {
            SuitUp(session);
            OpenOuterHatch(session);
            GoOutside(session);
            RepairOrFail(session, ShipSystemId.Antenna);
            ReturnAndRepressurize(session);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
        }

        /// <summary>Launches a fresh session, applies the impact and repairs power and life support.</summary>
        public static MissionSession Stabilize(MissionSession session)
        {
            CommandResult launch = session.Planner.TryLaunch();
            Assert.That(launch.Succeeded, Is.True, launch.Explanation);
            TriggerImpact(session);
            RepairOrFail(session, ShipSystemId.Power);
            RepairOrFail(session, ShipSystemId.LifeSupport);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
            return session;
        }

        /// <summary>Runs the one-time link diagnostic and acknowledges its FalseSignal sequence.</summary>
        public static void RunLinkDiagnostic(MissionSession session)
        {
            CommandResult diagnostic = session.Director.TryRunLinkDiagnostic();
            Assert.That(diagnostic.Succeeded, Is.True, diagnostic.Explanation);
            AcknowledgeActiveCinematic(session);
            Assert.That(session.Director.LinkDiagnosticCompleted, Is.True);
        }

        /// <summary>Runs the link diagnostic if it has not run yet, then sends the distress call.</summary>
        public static void Transmit(MissionSession session)
        {
            if (!session.Director.LinkDiagnosticRequested)
            {
                RunLinkDiagnostic(session);
            }

            CommandResult sent = session.Communications.TryTransmit();
            Assert.That(sent.Succeeded, Is.True, sent.Explanation);
            Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply));
        }

        /// <summary>Transmits and waits exactly the reply delay: the mission ends as RecoveryPossible.</summary>
        public static MissionSession RecoveredSession(string presetId = null)
        {
            MissionSession session = TransmissionSession(presetId);
            Transmit(session);
            Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);
            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible), session.State.OutcomeReason);
            return session;
        }

        /// <summary>Acknowledges every ending cinematic the way presentation would, until none is active.</summary>
        public static void FinishCinematics(MissionSession session, int limit = 8)
        {
            for (int i = 0; i < limit && session.Cinematics.IsWaiting; i++)
            {
                AcknowledgeActiveCinematic(session);
            }
        }

        /// <summary>The AURORA-7 definition with a different scenario (same catalog, limits and presets).</summary>
        public static MissionDefinition DefinitionWith(MissionScenario scenario)
        {
            MissionDefinition aurora = AuroraMission.CreateDefinition();
            var presets = new List<KeyValuePair<string, IReadOnlyDictionary<string, int>>>();
            foreach (string id in aurora.PresetIds)
            {
                presets.Add(new KeyValuePair<string, IReadOnlyDictionary<string, int>>(id, aurora.GetPreset(id)));
            }

            return new MissionDefinition(aurora.Catalog, aurora.Parameters, aurora.CrewCount, presets, aurora.DefaultPresetId, scenario);
        }

        /// <summary>The AURORA-7 scenario with a different mission clock T0 and/or communication assumptions.</summary>
        public static MissionScenario ScenarioWith(DateTime? missionEpochAtStartUtc = null, CommunicationSpec communication = null, MissionPacing pacing = null)
        {
            MissionScenario aurora = AuroraMission.CreateScenario();
            return new MissionScenario(
                aurora.Systems,
                pacing ?? aurora.Pacing,
                aurora.ImpactCinematicTimeoutRealSeconds,
                aurora.Eva,
                communication ?? aurora.Communication,
                missionEpochAtStartUtc,
                aurora.EndingCinematicTimeoutRealSeconds);
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

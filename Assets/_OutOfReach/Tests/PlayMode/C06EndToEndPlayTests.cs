using System;
using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using OutOfReach.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// C06-E2E-01 (Codex 6882784032): the post-contact chapter end to end in the grey-box harness, through the real
    /// MissionRuntime and the actual interaction targets.
    /// <para>
    /// EXPLICIT SHORTCUTS, and only these: the pre-contact route is set up through domain commands
    /// (RepairLoopHarness.ReachContact, covered by its own tests); the shortage variant empties the water store
    /// through the ledger; the ending cinematics are acknowledged the way presentation does (CinematicFinished) or
    /// left to the bounded timeout. Everything after contact runs through MissionRuntime.Step real-time frames (0.1 s
    /// each) and E presses on the harness targets: salvage, grow build/plant/harvest, ration, water, sleep and beacon.
    /// No simulated time is jumped after contact (JumpedSimulationSeconds stays unchanged).
    /// </para>
    /// </summary>
    public sealed class C06EndToEndPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const double RealStep = 0.1d;
        private const double Day = CrewMetabolicAssumptions.SecondsPerDay;

        private RepairLoopHarness harness;

        private MissionSession Session => harness.Session;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(survival: true);
            harness.Runtime.DriveFromUpdate = false;
        }

        [TearDown]
        public void TearDown()
        {
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>Stands one metre in front of a target, facing it, checks the aim and the prompt, and presses E once.</summary>
        private IEnumerator Use(InteractableTarget target, bool expectAllowed = true)
        {
            Vector3 at = target.transform.position;
            harness.PlacePlayerFacing(at.x - Mathf.Sign(at.x), at.z, at);
            yield return Frames(3);

            // The prompt of an unchanged focus refreshes at 10 Hz (and always right before E): read it fresh.
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(target), "aimed at " + target.name);
            string prompt = harness.Resolver.CurrentPrompt;
            Assert.That(harness.Resolver.CurrentCanInteract, Is.EqualTo(expectAllowed), target.name + " prompt: " + prompt);

            harness.Input.PressInteract();
            yield return Frames(3);
            if (expectAllowed)
            {
                CommandResult? last = harness.Player.LastInteraction;
                Assert.That(last.HasValue && last.Value.Succeeded, Is.True,
                    target.name + " (" + prompt + "): " + (last.HasValue ? last.Value.Explanation : "no interaction"));
            }
        }

        /// <summary>Drives the runtime with real-time frames until the condition holds (bounded by real seconds).</summary>
        private IEnumerator RunUntil(Func<bool> done, double maxRealSeconds, string what)
        {
            int frames = (int)Math.Ceiling(maxRealSeconds / RealStep);
            for (int i = 0; i < frames && !done(); i++)
            {
                harness.Runtime.Step(RealStep);
                if (i % 10 == 9)
                {
                    yield return null;
                }
            }

            Assert.That(done(), Is.True, what + " within " + maxRealSeconds.ToString("0.#") + " real seconds");
        }

        private static void Acknowledge(MissionSession session)
        {
            if (session.Cinematics.IsWaiting)
            {
                session.Events.Publish(new CinematicFinished(session.Cinematics.ActiveCinematic, session.Cinematics.ActiveSequenceId, false));
            }
        }

        /// <summary>Rests through the sleep target until the mission ends, answering the beacon and the harvest if asked.</summary>
        private IEnumerator RestToTheEnd(bool powerBeacon)
        {
            MissionSession session = Session;
            SurvivalRecovery recovery = session.SurvivalRecovery;
            for (int guard = 0; session.State.IsSimulationRunning; guard++)
            {
                Assert.That(guard, Is.LessThan(90), "the chapter ends");
                if (recovery.Stage == RecoveryStage.BeaconRequested)
                {
                    if (powerBeacon)
                    {
                        yield return Use(harness.Beacon);
                        Assert.That(recovery.BeaconPoweredAtSimulationSeconds, Is.Not.Null, "beacon through its target");
                    }
                    else
                    {
                        double open = recovery.BeaconWindowEndsAtSimulationSeconds.Value - session.State.ElapsedSimulationSeconds;
                        yield return RunUntil(() => recovery.Stage != RecoveryStage.BeaconRequested,
                            (open / session.Clock.TimeScale) + 2d, "the awake beacon window passes");
                    }

                    continue;
                }

                if (session.Survival.GrowStage == GrowModuleStage.HarvestReady)
                {
                    yield return Use(harness.GrowHarvest);
                    Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Harvested), "harvest through its target");
                    continue;
                }

                yield return Use(harness.Sleep);
                Assert.That(recovery.IsResting, Is.True, "rest through the sleep target");
                yield return RunUntil(() => !recovery.IsResting,
                    (recovery.RestRemainingSimulationSeconds / session.Clock.TimeScale) + 2d, "the rest stops");
            }
        }

        [UnityTest]
        public IEnumerator TheChapter_ThroughRealFramesAndTargets_IsRecovered_ReachesTheReport_AndARestartAwardsNothingExtra()
        {
            harness.ReachContact();
            MissionSession session = Session;
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            ResourceSnapshot atContact = session.Resources.Snapshot;
            SurvivalRecovery recovery = session.SurvivalRecovery;
            double contact = recovery.ContactAtSimulationSeconds.Value;
            double jumped = session.JumpedSimulationSeconds;

            yield return Use(harness.PayloadRack);
            Assert.That(session.FieldModifications.SciencePayloadSalvaged, Is.True, "salvage after contact through its target");
            yield return Use(harness.GrowBuild);
            Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.ReadyToPlant));
            yield return Use(harness.GrowPlant);
            Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Growing));

            // Awake for one portion interval, so a ration and a drink are due, then both through their targets.
            double start = session.State.ElapsedSimulationSeconds;
            double awake = session.Survival.Spec.PortionSeconds + 180d;
            yield return RunUntil(() => session.State.ElapsedSimulationSeconds - start >= awake,
                (awake / session.Clock.TimeScale) + 2d, "one portion interval awake");
            double hunger = session.Survival.Hunger01;
            yield return Use(harness.Ration);
            Assert.That(session.Survival.Hunger01, Is.LessThan(hunger), "ration through its target");
            double thirst = session.Survival.Thirst01;
            yield return Use(harness.Water);
            Assert.That(session.Survival.Thirst01, Is.LessThan(thirst), "drink through its target");

            yield return RestToTheEnd(powerBeacon: true);

            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
            Assert.That(recovery.BeaconMissed, Is.False);
            Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Harvested));
            Assert.That(recovery.RestCount, Is.GreaterThan(20), "the days pass by resting, not by a jump");
            Assert.That(session.JumpedSimulationSeconds, Is.EqualTo(jumped), "no simulated time jumped after contact");
            Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(contact + (12d * Day)).Within(1e-3), "rendezvous on schedule");
            Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.RecoveryArrival));
            Acknowledge(session);
            Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.MarsDeparture));
            Acknowledge(session);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
            Assert.That(session.Report.Headline, Is.EqualTo("RECOVERED"));

            // Restart in place and reach contact again the same way: nothing from the first run is carried over.
            harness.Runtime.RestartMission();
            MissionSession fresh = Session;
            Assert.That(fresh, Is.Not.SameAs(session), "an in-place restart builds a new session");
            Assert.That(fresh.SurvivalRecovery.Stage, Is.EqualTo(RecoveryStage.NotStarted));
            yield return Frames(3);
            harness.ReachContact();
            Assert.That(fresh.SurvivalRecovery.ContactAtSimulationSeconds, Is.EqualTo(contact), "the same timeline");
            ResourceSnapshot again = fresh.Resources.Snapshot;
            Assert.That(again.SparePartsCount, Is.EqualTo(atContact.SparePartsCount), "no carried salvage parts");
            Assert.That(again.FoodKg, Is.EqualTo(atContact.FoodKg).Within(1e-9), "no carried harvest credit");
            Assert.That(again.WaterLitres, Is.EqualTo(atContact.WaterLitres).Within(1e-9));
            Assert.That(again.OxygenKg, Is.EqualTo(atContact.OxygenKg).Within(1e-9));
            Assert.That(again.BatteryKWh, Is.EqualTo(atContact.BatteryKWh).Within(1e-9));
            Assert.That(fresh.FieldModifications.SciencePayloadSalvaged, Is.False);
            Assert.That(fresh.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Unbuilt));
            Assert.That(fresh.SurvivalRecovery.RestCount, Is.EqualTo(0));
            Assert.That(fresh.SurvivalRecovery.BeaconPoweredAtSimulationSeconds, Is.Null);
            Assert.That(fresh.SurvivalRecovery.Stage, Is.EqualTo(RecoveryStage.Inbound));
        }

        [UnityTest]
        public IEnumerator AMissedBeacon_DelaysTheRendezvousOnce_AndTheTimeoutStillReachesTheReport()
        {
            harness.ReachContact();
            MissionSession session = Session;
            SurvivalRecovery recovery = session.SurvivalRecovery;
            double contact = recovery.ContactAtSimulationSeconds.Value;

            yield return RestToTheEnd(powerBeacon: false);

            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
            Assert.That(recovery.BeaconMissed, Is.True);
            Assert.That(recovery.BeaconPoweredAtSimulationSeconds, Is.Null);
            Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(contact + (15d * Day)).Within(1e-3), "one 3-day delay");

            // Nobody acknowledges the ending cinematics: the bounded fallback still reaches the report.
            double timeout = session.Definition.Scenario.EndingCinematicTimeoutRealSeconds;
            int steps = (int)Math.Ceiling(((2d * timeout) + 2d) / RealStep);
            for (int i = 0; i < steps && session.State.Phase != GamePhase.Report; i++)
            {
                harness.Runtime.Step(RealStep);
                if (i % 50 == 49)
                {
                    yield return null;
                }
            }

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report), "the bounded ending sequence reaches the report");
            Assert.That(session.Report.Headline, Is.EqualTo("RECOVERED"));
        }

        [UnityTest]
        public IEnumerator AWaterShortage_EndsTheChapterLost_BeforeTheRendezvous()
        {
            harness.ReachContact();
            MissionSession session = Session;
            SurvivalRecovery recovery = session.SurvivalRecovery;

            // EXPLICIT SHORTCUT: leave one crew-day of water in the store.
            double perDay = CrewMetabolicAssumptions.WaterLitresPerCrewDay * session.Definition.CrewCount;
            double spend = session.Resources.GetQuantity(ResourceId.WaterLitres) - perDay;
            Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.WaterLitres, spend) }).Succeeded, Is.True);

            for (int guard = 0; recovery.LastRestStop != RestStopReason.WaterStoreEmpty; guard++)
            {
                Assert.That(guard, Is.LessThan(10), "the store runs out during the rests");
                yield return Use(harness.Sleep);
                Assert.That(recovery.IsResting, Is.True, "rest through the sleep target");
                yield return RunUntil(() => !recovery.IsResting,
                    (recovery.RestRemainingSimulationSeconds / session.Clock.TimeScale) + 2d, "the rest stops");
            }

            Assert.That(session.Resources.GetQuantity(ResourceId.WaterLitres), Is.EqualTo(0d).Within(1e-9));
            yield return Use(harness.Sleep, expectAllowed: false);
            Assert.That(recovery.IsResting, Is.False, "no rest on an empty water store");
            yield return RunUntil(() => !session.State.IsSimulationRunning,
                (session.Survival.Spec.ThirstLimitSeconds / session.Clock.TimeScale) + 5d, "the thirst limit");

            Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost), session.State.OutcomeReason);
            Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ThirstLimit));
            Assert.That(recovery.Stage, Is.Not.EqualTo(RecoveryStage.Arrived), "no rescue for a lost crew");
            Assert.That(recovery.RecoveredAtSimulationSeconds, Is.Null);
            Acknowledge(session);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
        }
    }
}

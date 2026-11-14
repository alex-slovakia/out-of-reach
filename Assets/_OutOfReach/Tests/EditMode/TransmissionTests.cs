using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>The finale through the whole session: diagnostic, transmission, the wait, both outcomes and the ending sequence.</summary>
    public sealed class TransmissionTests
    {
        private static IInteractable Console(MissionSession session)
        {
            Assert.That(session.Interactions.TryGet(InteractionIds.CockpitConsole, out IInteractable console), Is.True);
            return console;
        }

        private static ResourceAmount[] Cost(ResourceId resource, double quantity)
        {
            return new[] { new ResourceAmount(resource, quantity) };
        }

        /// <summary>Leaves exactly enough water to last the given simulated seconds at the current rate.</summary>
        private static void LeaveWaterFor(MissionSession session, double seconds)
        {
            double keep = session.Resources.Rates.WaterUseLitresPerSecond * seconds;
            double spend = session.Resources.Snapshot.WaterLitres - keep;
            Assert.That(session.Resources.TryConsume(Cost(ResourceId.WaterLitres, spend)).Succeeded, Is.True);
        }

        [Test]
        public void Console_WalksThroughDiagnostic_Transmission_AndTheWait()
        {
            using (var session = Fixtures.TransmissionSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                IInteractable console = Console(session);
                double battery = session.Resources.Snapshot.BatteryKWh;
                Assert.That(session.Clock.TimeScale, Is.EqualTo(60d), "the disclosed Transmission time scale");
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.Ready));
                Assert.That(console.Prompt, Is.EqualTo("Run communications diagnostic"));
                Assert.That(console.CanInteract, Is.True);

                CommandResult tooEarly = session.Communications.TryTransmit();
                Assert.That(tooEarly.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "the contract path cannot skip the diagnostic");
                Assert.That(tooEarly.Explanation, Does.Contain("diagnostic"));

                CommandResult diagnostic = console.TryInteract();

                Assert.That(diagnostic.Succeeded, Is.True, diagnostic.Explanation);
                Assert.That(diagnostic.Explanation, Does.Contain("old local recording").And.Contain("not a reply from Earth"));
                Assert.That(recorder.CinematicRequests, Has.Count.EqualTo(1));
                Assert.That(recorder.CinematicRequests[0].Cinematic, Is.EqualTo(CinematicId.FalseSignal));
                Assert.That(recorder.Communications, Is.Empty, "the false signal is not a communication event");
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.Ready));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery), "it costs nothing");
                Assert.That(session.IsPlayerControlAllowed, Is.False, "locked while the sequence plays");
                Assert.That(console.CanInteract, Is.False);
                Assert.That(console.Prompt, Is.EqualTo("The link diagnostic is still running."));
                Assert.That(session.Director.TryRunLinkDiagnostic().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Communications.TryTransmit().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));

                Fixtures.AcknowledgeActiveCinematic(session, skipped: true);

                Assert.That(session.Director.LinkDiagnosticCompleted, Is.True);
                Assert.That(session.Director.TryRunLinkDiagnostic().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "once only");
                Assert.That(recorder.CinematicRequests, Has.Count.EqualTo(1));
                Assert.That(console.Prompt, Is.EqualTo("Transmit distress call: 0.5 kWh. Earth is 197,708,000 km away; earliest reply in 31 min 59 s."));
                Assert.That(console.CanInteract, Is.True);

                CommandResult sent = console.TryInteract();

                Assert.That(sent.Succeeded, Is.True, sent.Explanation);
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery - 0.5d).Within(1e-9));
                Assert.That(recorder.Communications, Has.Count.EqualTo(1));
                Assert.That(recorder.Communications[0].State, Is.EqualTo(CommunicationState.AwaitingReply));
                Assert.That(recorder.Communications[0].ReplyRemainingSimulationSeconds, Is.EqualTo(1918.965642496d).Within(1e-6));
                Assert.That(console.Prompt, Is.EqualTo("Awaiting reply from Earth: 31 min 59 s (simulated)"));
                Assert.That(console.CanInteract, Is.False);
                Assert.That(console.TryInteract().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery - 0.5d).Within(1e-9), "a repeated press charges nothing");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
            }
        }

        [Test]
        public void LinkStates_ArePublishedInOrder_AcrossTheWholeMission()
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.Ready), "nominal before the impact");
                Assert.That(session.Communications.TryTransmit().Failure, Is.EqualTo(CommandFailure.WrongPhase), "no distress call without an emergency");

                Fixtures.TriggerImpact(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Assert.That(Console(session).Prompt, Is.EqualTo("Communications offline: antenna damaged"));
                Assert.That(session.Communications.TryTransmit().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));

                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Fixtures.GoOutside(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Antenna);
                CommandResult outside = session.Communications.TryTransmit();
                Assert.That(outside.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(outside.Explanation, Is.EqualTo("Antenna repaired. Return through the airlock before transmitting."));
                Assert.That(session.Director.TryRunLinkDiagnostic().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Fixtures.ReturnAndRepressurize(session);

                Fixtures.Transmit(session);
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);

                Assert.That(recorder.Communications.ConvertAll(e => e.State), Is.EqualTo(new[]
                {
                    CommunicationState.Offline, CommunicationState.Ready, CommunicationState.AwaitingReply, CommunicationState.ReplyReceived,
                }));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
            }
        }

        [Test]
        public void Reply_EndsTheMissionAsRecoveryPossible_AtTheExactTime_EvenInsideALongJump()
        {
            using (var session = Fixtures.TransmissionSession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.Transmit(session);
                double sentAt = session.State.ElapsedSimulationSeconds;
                double delay = session.Communications.ReplyDelaySimulationSeconds;
                Assert.That(sentAt, Is.EqualTo(9600d), "suit preparation plus one airlock cycle");
                Assert.That(delay, Is.EqualTo(1918.965642496d).Within(1e-6));

                using (var recorder = new EventRecorder(session.Events))
                {
                    Assert.That(session.AdvanceSimulation(delay - 1d).Succeeded, Is.True);
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
                    Assert.That(session.Communications.ReplyRemainingSimulationSeconds, Is.EqualTo(1d).Within(1e-6));
                    Assert.That(recorder.Communications, Is.Empty, "no event while only the countdown moves");

                    Assert.That(session.AdvanceSimulation(10d * 86400d).Succeeded, Is.True);

                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                    Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(sentAt + delay).Within(1e-6));
                    Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(sentAt + delay).Within(1e-6), "the rest of the jump is discarded");
                    Assert.That(session.Communications.ReplyReceivedAtSimulationSeconds.Value - sentAt, Is.GreaterThanOrEqualTo(delay - 1e-9));
                    Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.None));
                    List<string> decisive = recorder.Order.FindAll(name => name != "ResourceChanged");
                    Assert.That(decisive, Is.EqualTo(new[] { "CommunicationChanged", "PhaseChanged", "MissionEnded", "CinematicRequested" }),
                        "reply first, then the outcome, then the ending sequence");
                    Assert.That(recorder.Communications[0].State, Is.EqualTo(CommunicationState.ReplyReceived));
                    Assert.That(recorder.Endings[0].Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                    Assert.That(recorder.CinematicRequests[0].Cinematic, Is.EqualTo(CinematicId.CommunicationRestored));
                    Assert.That(session.State.OutcomeReason, Does.Contain("31 min 59 s").And.Contain("197,708,000 km").And.Contain("Rescue has not arrived"));
                    Assert.That(session.Resources.Snapshot.FoodKg, Is.GreaterThan(60d), "ten more days were not consumed");
                }
            }
        }

        [Test]
        public void FrameByFrame_AtTheTransmissionTimeScale_TheReplyArrivesAtTheSameSimulatedTime()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                double due = session.State.ElapsedSimulationSeconds + session.Communications.ReplyDelaySimulationSeconds;
                int frames = 0;
                while (session.State.Outcome == MissionOutcome.None && frames < 1000)
                {
                    Assert.That(session.Tick(0.25d).Succeeded, Is.True);
                    frames++;
                }

                Assert.That(frames, Is.EqualTo(128), "1,919 simulated seconds at x60 are about 32 real seconds");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(due).Within(1e-6), "not rounded up to the frame");
                Assert.That(session.PlayedRealSeconds, Is.EqualTo(32d).Within(1e-9));
            }
        }

        [Test]
        public void Pause_FreezesTheReplyClock()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                Assert.That(session.AdvanceSimulation(600d).Succeeded, Is.True);
                double remaining = session.Communications.ReplyRemainingSimulationSeconds;
                ResourceSnapshot reserves = session.Resources.Snapshot;

                Assert.That(session.Clock.Pause().Succeeded, Is.True);
                Assert.That(session.Tick(3600d).Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(remaining).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));

                Assert.That(session.Communications.ReplyRemainingSimulationSeconds, Is.EqualTo(remaining));
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply));
                Fixtures.AssertSnapshot(reserves, session.Resources.Snapshot, 0d);

                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                Assert.That(session.Tick(1d).Succeeded, Is.True);
                Assert.That(session.Communications.ReplyRemainingSimulationSeconds, Is.EqualTo(remaining - 60d).Within(1e-6), "no catch-up after the pause");
            }
        }

        [Test]
        public void InsufficientBattery_BlocksTheCall_UntilTheShipHasRecharged()
        {
            using (var session = Fixtures.TransmissionSession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.RunLinkDiagnostic(session);
                Assert.That(session.Resources.TryConsume(Cost(ResourceId.BatteryKWh, session.Resources.Snapshot.BatteryKWh - 0.2d)).Succeeded, Is.True);

                using (var recorder = new EventRecorder(session.Events))
                {
                    CommandResult blocked = Console(session).TryInteract();

                    Assert.That(blocked.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                    Assert.That(Console(session).CanInteract, Is.False);
                    Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.Ready));
                    Assert.That(session.Communications.HasTransmitted, Is.False);
                    Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(0.2d).Within(1e-9));
                    Assert.That(recorder.Total, Is.EqualTo(0));
                }

                Assert.That(session.AdvanceSimulation(3600d).Succeeded, Is.True);
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(0.7d).Within(1e-6), "500 W of surplus generation for one hour");
                Assert.That(Console(session).TryInteract().Succeeded, Is.True);
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(0.2d).Within(1e-6));
            }
        }

        [Test]
        public void ALossBeforeTheReply_IsNotSkippedByAJump_AndExplainsThePendingReply()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                double sentAt = session.State.ElapsedSimulationSeconds;
                double delay = session.Communications.ReplyDelaySimulationSeconds;
                LeaveWaterFor(session, delay - 100d);

                Assert.That(session.AdvanceSimulation(1e6).Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.WaterDepleted));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(sentAt + delay - 100d).Within(1e-3));
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply), "the reply never arrived");
                Assert.That(session.Communications.ReplyReceivedAtSimulationSeconds, Is.Null);
                Assert.That(session.Communications.ReplyRemainingSimulationSeconds, Is.EqualTo(100d).Within(1e-3));
                Assert.That(session.State.OutcomeReason, Does.Contain("Water reserve exhausted").And.Contain("could not arrive for another 1 min 40 s"));
            }
        }

        [Test]
        public void ALossAtTheSameInstantAsTheReply_Wins()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                LeaveWaterFor(session, session.Communications.ReplyDelaySimulationSeconds);

                Assert.That(session.AdvanceSimulation(1e6).Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.AwaitingReply));
            }
        }

        [Test]
        public void AFailureThatWouldComeAfterTheReply_NeverHappens()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                LeaveWaterFor(session, session.Communications.ReplyDelaySimulationSeconds + 100d);

                Assert.That(session.AdvanceSimulation(1e6).Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(session.Resources.Snapshot.WaterLitres, Is.GreaterThan(0d));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.None));
            }
        }

        [Test]
        public void EndingSequence_PlaysBothCinematics_IgnoresStaleCompletions_ThenEntersReport()
        {
            using (var session = Fixtures.TransmissionSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Fixtures.Transmit(session);
                int falseSignalSequence = session.Cinematics.ActiveSequenceId;
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                Assert.That(session.Cinematics.IsWaiting, Is.True);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.CommunicationRestored));
                Assert.That(session.IsPlayerControlAllowed, Is.False);
                int restoredSequence = session.Cinematics.ActiveSequenceId;
                int ignored = session.Cinematics.IgnoredAcknowledgements;

                session.Events.Publish(new CinematicFinished(CinematicId.FalseSignal, falseSignalSequence, false));
                session.Events.Publish(new CinematicFinished(CinematicId.EndingPullback, restoredSequence, false));
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.CommunicationRestored), "a stale and a mismatched completion change nothing");
                Assert.That(session.Cinematics.IgnoredAcknowledgements, Is.EqualTo(ignored + 2));

                Fixtures.AcknowledgeActiveCinematic(session);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.EndingPullback));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));

                session.Events.Publish(new CinematicFinished(CinematicId.CommunicationRestored, restoredSequence, false));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome), "a duplicate completion cannot skip the pullback");

                Fixtures.AcknowledgeActiveCinematic(session, skipped: true);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
                Assert.That(session.Cinematics.IsWaiting, Is.False);
                Assert.That(recorder.CinematicRequests.ConvertAll(r => r.Cinematic), Is.EqualTo(new[]
                {
                    CinematicId.FalseSignal, CinematicId.CommunicationRestored, CinematicId.EndingPullback,
                }));
                Assert.That(recorder.Phases[recorder.Phases.Count - 1].Current, Is.EqualTo(GamePhase.Report));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
            }
        }

        [Test]
        public void ALoss_PlaysOnlyThePullback_AndFallsBackOnItsTimeout()
        {
            using (var session = Fixtures.EmergencySession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.AdvanceSimulation(100d * 3600d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.EndingPullback));
                Assert.That(recorder.CinematicRequests, Has.Count.EqualTo(1));

                session.Tick(89d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                session.Tick(1.5d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report), "the 90 s fallback never strands the session");
                Assert.That(session.Cinematics.LastCompletion.Value.TimedOut, Is.True);
            }
        }

        [Test]
        public void WithoutPresentation_TheSessionStillReachesTheReport()
        {
            using (var session = Fixtures.RecoveredSession())
            {
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome));
                session.Tick(0d);
                session.Tick(0d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
                Assert.That(session.Tick(1d).Succeeded, Is.True, "ticking a finished mission is harmless");
                Assert.That(session.AdvanceSimulation(1d).Failure, Is.EqualTo(CommandFailure.WrongPhase));
            }
        }

        [Test]
        public void ALossDuringTheImpactSequence_SupersedesIt_AndTheLateImpactCompletionIsIgnored()
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
                int impactSequence = session.Cinematics.ActiveSequenceId;

                Assert.That(session.Director.TryEndMission(MissionOutcome.Lost, "test").Succeeded, Is.True);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.EndingPullback));

                session.Events.Publish(new CinematicFinished(CinematicId.Impact, impactSequence, false));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Outcome), "the dead Impact sequence cannot start an emergency");
                Assert.That(session.Cinematics.IsWaiting, Is.True);

                Fixtures.AcknowledgeActiveCinematic(session);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
            }
        }

        [Test]
        public void PresentationMaySkipStraightToTheReport()
        {
            using (var session = Fixtures.TransmissionSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                Fixtures.Transmit(session);
                session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds);
                Assert.That(session.Cinematics.IsWaiting, Is.True);

                Assert.That(session.State.TryTransition(GamePhase.Report).Succeeded, Is.True);

                Assert.That(session.Cinematics.IsWaiting, Is.False, "no ending cinematic keeps holding the session");
                session.Tick(200d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Report));
                Assert.That(recorder.CinematicRequests[recorder.CinematicRequests.Count - 1].Cinematic, Is.EqualTo(CinematicId.CommunicationRestored), "the pullback was never requested");
            }
        }

        [Test]
        public void TheMissionDate_SelectsTheSample_AfterDaysOfHolding()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.AdvanceSimulation(3d * 86400d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "a repaired ship holds for days");
                Fixtures.CompleteEva(session);

                Fixtures.Transmit(session);

                EarthMarsSample expected = EarthMarsReference.Embedded.Samples[3];
                Assert.That(session.Calendar.DayNumber, Is.EqualTo(4));
                Assert.That(session.Communications.ReferenceSample.Index, Is.EqualTo(3));
                Assert.That(session.Communications.ReferenceEpochUtc, Is.EqualTo(new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc)));
                Assert.That(session.Communications.ReferenceDistanceKm, Is.EqualTo(expected.DistanceKm));
                Assert.That(session.Communications.ReplyDelaySimulationSeconds, Is.EqualTo((2d * expected.DistanceKm / 299792.458d) + 600d).Within(1e-9));
                Assert.That(session.Communications.ReplyDelaySimulationSeconds, Is.LessThan(1918d), "Mars is closer than on day 1, so the wait is shorter");
            }
        }

        [Test]
        public void MissionDay31_UsesTheDecember14Sample()
        {
            MissionDefinition definition = Fixtures.DefinitionWith(Fixtures.ScenarioWith(new DateTime(2026, 12, 14, 8, 0, 0, DateTimeKind.Utc)));
            using (var session = Fixtures.TransmissionSession(definition: definition))
            {
                Fixtures.Transmit(session);

                Assert.That(session.Communications.ReferenceSample.Index, Is.EqualTo(30));
                Assert.That(session.Communications.OneWayLightSeconds, Is.EqualTo(529.587794443d).Within(1e-9));
                Assert.That(session.Communications.ReplyDelaySimulationSeconds, Is.EqualTo(1659.175588886d).Within(1e-6));
                Assert.That(session.AdvanceSimulation(1659d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.AdvanceSimulation(1d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
            }
        }

        [Test]
        public void PastTheEndOfTheSnapshot_NoCallCanBeSent()
        {
            MissionDefinition definition = Fixtures.DefinitionWith(Fixtures.ScenarioWith(new DateTime(2026, 12, 15, 7, 0, 0, DateTimeKind.Utc)));
            using (var session = Fixtures.TransmissionSession(definition: definition))
            {
                Fixtures.RunLinkDiagnostic(session);
                double battery = session.Resources.Snapshot.BatteryKWh;

                CommandResult late = Console(session).TryInteract();

                Assert.That(late.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(late.Explanation, Does.Contain("ends on 2026-12-15 08:00 UTC"));
                Assert.That(Console(session).CanInteract, Is.False);
                Assert.That(session.Communications.IsReferenceAvailable, Is.False);
                Assert.That(session.Communications.HasTransmitted, Is.False);
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery));
            }
        }

        [Test]
        public void AMissionClockOutsideTheSnapshot_IsRejectedWhenTheSessionIsBuilt()
        {
            MissionDefinition definition = Fixtures.DefinitionWith(Fixtures.ScenarioWith(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new MissionSession(definition));

            Assert.That(error.Message, Does.Contain("outside the packaged reference"));
        }

        [Test]
        public void TooFewSpareParts_ForTheAntenna_EndsAStableShipAsAnUnrecoverableObjective()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 1).Succeeded, Is.True);
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(4));
                Fixtures.TriggerImpact(session);

                using (var recorder = new EventRecorder(session.Events))
                {
                    Fixtures.RepairOrFail(session, ShipSystemId.Power);
                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "life support can still be repaired");

                    Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);

                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                    Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ContactImpossible));
                    Assert.That(session.State.OutcomeReason, Does.StartWith("Mission objective unrecoverable: the high-gain antenna needs 3 spare parts and only 0 remain"));
                    Assert.That(session.State.OutcomeReason, Does.Contain("The crew is alive").And.Contain("Reserves cover about 34 more days"));
                    Assert.That(session.Systems.LifeSupport.IsOperational, Is.True, "the repair itself succeeded");
                    Assert.That(recorder.Endings, Has.Count.EqualTo(1));
                    Assert.That(recorder.CinematicRequests[0].Cinematic, Is.EqualTo(CinematicId.EndingPullback));
                }
            }
        }

        [Test]
        public void TheUnrecoverableRule_WaitsUntilTheCrewIsBackInside_AndNeverFiresWithEnoughParts()
        {
            using (var enough = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(enough.Resources.Snapshot.SparePartsCount, Is.EqualTo(3), "exactly the antenna's need");
                Assert.That(enough.AdvanceSimulation(3600d).Succeeded, Is.True);
                Assert.That(enough.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }

            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Fixtures.GoOutside(session);
                Assert.That(session.Resources.TryConsume(Cost(ResourceId.SparePartsCount, 1d)).Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(60d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "nothing is decided while the crew is outside");

                Fixtures.ReturnAndRepressurize(session);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ContactImpossible));
                Assert.That(session.Eva.IsSuitOn, Is.False, "the crew is back in cabin air");
                Assert.That(session.State.OutcomeReason, Does.Contain("only 2 remain,"));
            }
        }
    }
}

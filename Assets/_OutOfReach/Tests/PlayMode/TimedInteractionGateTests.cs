using System;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// REPAIR-PRESENTATION-GATE-01: the opt-in gate around the real antenna repair of a real session, with the crew
    /// outside on the EVA. E starts visible work only; the real repair runs once at the end, after a fresh check.
    /// </summary>
    public sealed class TimedInteractionGateTests
    {
        private const double Work = 3d;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        /// <summary>Launch, impact, power and life support repaired, suited, depressurized and outside: the antenna can be repaired.</summary>
        private static MissionSession Outside()
        {
            var session = new MissionSession();
            Require(session.Planner.TryLaunch());
            Require(session.Director.TryCompleteCockpitCheck());
            if (session.Cinematics.IsWaiting)
            {
                session.Events.Publish(new CinematicFinished(session.Cinematics.ActiveCinematic, session.Cinematics.ActiveSequenceId, false));
            }

            Require(session.Systems.Power.TryRepair(session.Resources));
            Require(session.Systems.LifeSupport.TryRepair(session.Resources));
            Require(session.Eva.TryPrepareSuit());
            Require(session.Eva.TryBeginEva());
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds));
            Require(session.Eva.TryGoOutside());
            return session;
        }

        private static IInteractable Antenna(MissionSession session)
        {
            Assert.That(session.Interactions.TryGet(InteractionIds.RepairAntenna, out IInteractable antenna), Is.True);
            Assert.That(antenna.CanInteract, Is.True, antenna.Prompt);
            return antenna;
        }

        [Test]
        public void E_StartsTheWorkOnly_AndTheRealRepairRunsOnceAtTheEnd()
        {
            using (MissionSession session = Outside())
            {
                IInteractable antenna = Antenna(session);
                string idlePrompt = antenna.Prompt;
                int conditionEvents = 0;
                session.Events.SystemConditionChanged += _ => conditionEvents++;
                ResourceSnapshot before = session.Resources.Snapshot;
                var gate = new TimedInteractionGate(antenna, Work);
                int started = 0;
                int progressed = 0;
                CommandResult? finished = null;
                gate.Started += () => started++;
                gate.Progressed += _ => progressed++;
                gate.Finished += result => finished = result;
                Assert.That(gate.InteractionId, Is.EqualTo(InteractionIds.RepairAntenna));
                Assert.That(gate.Prompt, Is.EqualTo(idlePrompt), "idle: the real prompt");
                Assert.That(gate.CanInteract, Is.True);

                CommandResult start = gate.TryInteract();

                Assert.That(start.Succeeded, Is.True);
                Assert.That(start.Explanation, Is.EqualTo("Work started: 3.0 s"), "honest: started, not repaired");
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Working));
                Assert.That(started, Is.EqualTo(1));
                Assert.That(gate.Prompt, Is.EqualTo("Working: 0%"));
                Assert.That(gate.CanInteract, Is.False);
                Assert.That(session.Systems.Antenna.IsOperational, Is.False, "nothing repaired at the start");
                Assert.That(conditionEvents, Is.EqualTo(0), "no early SystemConditionChanged");
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount), "nothing spent at the start");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh));

                gate.Advance(1d);
                gate.Advance(1d);
                Assert.That(gate.Progress01, Is.EqualTo(2d / 3d).Within(1e-9));
                Assert.That(gate.Prompt, Is.EqualTo("Working: 67%"));
                Assert.That(session.Systems.Antenna.IsOperational, Is.False, "still nothing before the end");
                Assert.That(finished, Is.Null);

                gate.Advance(1.5d);

                Assert.That(finished.HasValue && finished.Value.Succeeded, Is.True, finished.HasValue ? finished.Value.Explanation : "not finished");
                Assert.That(session.Systems.Antenna.IsOperational, Is.True, "the real repair ran");
                Assert.That(conditionEvents, Is.EqualTo(1), "exactly once");
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.LessThan(before.SparePartsCount), "parts spent by the domain");
                Assert.That(progressed, Is.EqualTo(3));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
                int partsAfter = session.Resources.Snapshot.SparePartsCount;

                gate.Advance(5d);
                Assert.That(conditionEvents, Is.EqualTo(1), "advancing an idle gate does nothing");
                CommandResult again = gate.TryInteract();
                Assert.That(again.Succeeded, Is.False, "the antenna is repaired: nothing to start");
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(partsAfter));
            }
        }

        [Test]
        public void ARepeatedE_IsRefused_AndPauseFreezesTheWork()
        {
            using (MissionSession session = Outside())
            {
                bool paused = false;
                var gate = new TimedInteractionGate(Antenna(session), Work, null, () => paused);
                Require(gate.TryInteract());
                gate.Advance(1d);

                CommandResult repeat = gate.TryInteract();
                Assert.That(repeat.Succeeded, Is.False);
                Assert.That(repeat.Explanation, Is.EqualTo(TimedInteractionGate.AlreadyWorkingMessage));
                Assert.That(gate.ElapsedActiveSeconds, Is.EqualTo(1d), "a repeated E does not restart or duplicate");

                paused = true;
                gate.Advance(10d);
                Assert.That(gate.ElapsedActiveSeconds, Is.EqualTo(1d), "pause freezes the work");
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);

                paused = false;
                gate.Advance(2d);
                Assert.That(session.Systems.Antenna.IsOperational, Is.True);
            }
        }

        [Test]
        public void ACancel_SpendsNothing_AndANewStartBeginsFromZero()
        {
            using (MissionSession session = Outside())
            {
                ResourceSnapshot before = session.Resources.Snapshot;
                var gate = new TimedInteractionGate(Antenna(session), Work);
                string cancelled = null;
                gate.Cancelled += reason => cancelled = reason;
                Require(gate.TryInteract());
                gate.Advance(2.9d);

                gate.Cancel("Looked away from the antenna.");

                Assert.That(cancelled, Is.EqualTo("Looked away from the antenna."));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
                Assert.That(gate.ElapsedActiveSeconds, Is.EqualTo(0d));
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount), "nothing spent");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh));

                Require(gate.TryInteract());
                gate.Advance(2.9d);
                Assert.That(session.Systems.Antenna.IsOperational, Is.False, "the new work starts from zero");
                gate.Advance(0.2d);
                Assert.That(session.Systems.Antenna.IsOperational, Is.True);
            }
        }

        [Test]
        public void AnInvalidSessionOrFocus_CancelsWithNothingSpent_AndBlocksAStart()
        {
            using (MissionSession session = Outside())
            {
                bool valid = true;
                ResourceSnapshot before = session.Resources.Snapshot;
                var gate = new TimedInteractionGate(Antenna(session), Work, () => valid);
                int cancels = 0;
                gate.Cancelled += _ => cancels++;
                Require(gate.TryInteract());
                gate.Advance(1d);

                valid = false;
                gate.Advance(5d);

                Assert.That(cancels, Is.EqualTo(1));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount));
                Assert.That(gate.CanInteract, Is.False);
                Assert.That(gate.TryInteract().Succeeded, Is.False, "no start while the caller's policy says no");
            }
        }

        [Test]
        public void TheEnd_Revalidates_AndMissingPartsRefuseWithNothingSpent()
        {
            using (MissionSession session = Outside())
            {
                var gate = new TimedInteractionGate(Antenna(session), Work);
                CommandResult? finished = null;
                gate.Finished += result => finished = result;
                Require(gate.TryInteract());
                gate.Advance(1d);

                int parts = session.Resources.Snapshot.SparePartsCount;
                Require(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, parts) }));
                double battery = session.Resources.Snapshot.BatteryKWh;
                gate.Advance(5d);

                Assert.That(finished.HasValue, Is.True);
                Assert.That(finished.Value.Succeeded, Is.False, "refused at the end: the parts are gone");
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery), "nothing spent by a refused end");
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
            }
        }

        // Codex 6884680635: pause freezes time, not validity.
        [Test]
        public void AStaleSession_CancelsEvenWhilePaused_AndAnExternalCancelWorksWhilePaused()
        {
            using (MissionSession session = Outside())
            {
                bool valid = true;
                bool paused = false;
                ResourceSnapshot before = session.Resources.Snapshot;
                var gate = new TimedInteractionGate(Antenna(session), Work, () => valid, () => paused);
                var reasons = new System.Collections.Generic.List<string>();
                gate.Cancelled += reason => reasons.Add(reason);

                Require(gate.TryInteract());
                gate.Advance(1d);
                paused = true;
                valid = false;
                gate.Advance(0.5d);
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle), "a stale session is not kept alive by the pause");
                Assert.That(reasons, Has.Count.EqualTo(1));

                valid = true;
                Require(gate.TryInteract());
                gate.Cancel("Restart from the pause menu.");
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle), "an explicit cancel works while paused");
                Assert.That(reasons, Has.Count.EqualTo(2));
                Assert.That(reasons[1], Is.EqualTo("Restart from the pause menu."));
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount), "nothing spent");
            }
        }

        // Codex 6884680635: re-entrant handlers can never let a completion act on a replacement attempt.
        [Test]
        public void HandlersThatCancelOrRestart_NeverLetACompletionActOnAReplacement_AndFinishedSeesAClearedState()
        {
            using (MissionSession session = Outside())
            {
                int conditionEvents = 0;
                session.Events.SystemConditionChanged += _ => conditionEvents++;
                var gate = new TimedInteractionGate(Antenna(session), 1d);
                bool restartOnce = true;
                gate.Progressed += progress =>
                {
                    if (restartOnce)
                    {
                        restartOnce = false;
                        gate.Cancel("Replaced from a handler.");
                        Require(gate.TryInteract());
                    }
                };
                TimedWorkState stateInFinished = TimedWorkState.Working;
                gate.Finished += _ => stateInFinished = gate.State;

                Require(gate.TryInteract());
                gate.Advance(2d);

                Assert.That(session.Systems.Antenna.IsOperational, Is.False, "the advance that reached the end belonged to the cancelled attempt");
                Assert.That(conditionEvents, Is.EqualTo(0));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Working), "the replacement attempt runs on");
                Assert.That(gate.ElapsedActiveSeconds, Is.EqualTo(0d), "from zero");

                gate.Advance(1d);
                Assert.That(session.Systems.Antenna.IsOperational, Is.True);
                Assert.That(conditionEvents, Is.EqualTo(1), "one real repair");
                Assert.That(stateInFinished, Is.EqualTo(TimedWorkState.Idle), "the working state is cleared before Finished");
            }

            using (MissionSession session = Outside())
            {
                ResourceSnapshot before = session.Resources.Snapshot;
                var gate = new TimedInteractionGate(Antenna(session), Work);
                gate.Started += () => gate.Cancel("Cancelled at once.");

                CommandResult start = gate.TryInteract();

                Assert.That(start.Succeeded, Is.False, "a start cancelled by its own handler is reported as cancelled");
                Assert.That(start.Explanation, Is.EqualTo("Work cancelled."));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount));
            }
        }

        // Codex 6884992203: a Progressed handler that revokes the policy at progress 1 without calling Cancel.
        [Test]
        public void APolicyRevokedByAProgressedHandlerAtTheEnd_CancelsWithNothingSpent()
        {
            using (MissionSession session = Outside())
            {
                bool valid = true;
                int conditionEvents = 0;
                session.Events.SystemConditionChanged += _ => conditionEvents++;
                ResourceSnapshot before = session.Resources.Snapshot;
                IInteractable antenna = Antenna(session);
                var gate = new TimedInteractionGate(antenna, Work, () => valid);
                gate.Progressed += progress =>
                {
                    if (progress >= 1d)
                    {
                        valid = false;
                    }
                };
                int cancels = 0;
                bool finished = false;
                gate.Cancelled += _ => cancels++;
                gate.Finished += _ => finished = true;

                Require(gate.TryInteract());
                gate.Advance(Work);

                Assert.That(antenna.CanInteract, Is.True, "the inner repair alone would still say yes");
                Assert.That(finished, Is.False, "no real command after the policy was revoked");
                Assert.That(cancels, Is.EqualTo(1));
                Assert.That(gate.State, Is.EqualTo(TimedWorkState.Idle));
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(conditionEvents, Is.EqualTo(0));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(before.SparePartsCount), "nothing spent");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh));
            }
        }

        [Test]
        public void AnUngatedRepair_StaysInstant_AndInvalidInputIsRejected()
        {
            using (MissionSession session = Outside())
            {
                IInteractable antenna = Antenna(session);
                Require(antenna.TryInteract());
                Assert.That(session.Systems.Antenna.IsOperational, Is.True, "without the gate nothing changes");

                Assert.Throws<ArgumentNullException>(() => new TimedInteractionGate(null, Work));
                Assert.Throws<ArgumentOutOfRangeException>(() => new TimedInteractionGate(antenna, 0d));
                Assert.Throws<ArgumentOutOfRangeException>(() => new TimedInteractionGate(antenna, double.NaN));
                var gate = new TimedInteractionGate(antenna, Work);
                Assert.Throws<ArgumentOutOfRangeException>(() => gate.Advance(-1d));
                Assert.Throws<ArgumentOutOfRangeException>(() => gate.Advance(double.PositiveInfinity));
            }
        }
    }
}

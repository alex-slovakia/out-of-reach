using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>The link on its own, with a hand-driven mission clock: timing, single transmission, power and dates.</summary>
    public sealed class CommunicationSystemTests
    {
        private const double Row0OneWay = 659.482821248d;
        private const double Row0Delay = (2d * Row0OneWay) + 600d;

        private sealed class Rig
        {
            public readonly MissionEventBus Bus = new MissionEventBus();
            public readonly List<CommunicationChanged> Events = new List<CommunicationChanged>();
            public readonly ResourceLedger Ledger;
            public readonly CommunicationSystem Link;
            public double Clock;
            public bool Powered = true;
            public CommandResult LinkCheck = new CommandResult(true, CommandFailure.None, "ok");
            private readonly DateTime start;

            public Rig(double batteryKWh = 5d, DateTime? start = null, CommunicationSpec spec = null)
            {
                this.start = start ?? EarthMarsReference.Embedded.FirstEpochUtc;
                Ledger = new ResourceLedger(Bus);
                CommandResult loaded = Ledger.Initialize(new ResourceSnapshot(10d, 10d, 10d, batteryKWh, 3, 2000d), 30d, ResourceRates.Zero);
                Assert.That(loaded.Succeeded, Is.True, loaded.Explanation);
                Link = new CommunicationSystem(Bus, Ledger, EarthMarsReference.Embedded, spec ?? new CommunicationSpec(),
                    () => this.start.AddTicks((long)Math.Floor(Clock * TimeSpan.TicksPerSecond)), () => Clock, () => LinkCheck, () => Powered, Bus.DeferDispatch);
                Bus.CommunicationChanged += Events.Add;
            }

            /// <summary>Moves the mission clock, then lets the link look at it, the way the session does.</summary>
            public void Advance(double seconds)
            {
                Clock += seconds;
                Link.AdvanceTime(seconds);
            }
        }

        [Test]
        public void State_FollowsLinkPower_AndIsPublishedOnlyWhenItChanges()
        {
            var rig = new Rig();
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.Ready));
            Assert.That(rig.Link.AstronomicalReferenceId, Is.EqualTo(EarthMarsReferenceTests.DatasetId));

            rig.Powered = false;
            rig.Link.RefreshState();
            rig.Link.RefreshState();
            rig.Powered = true;
            rig.Link.RefreshState();
            rig.Advance(10d);

            Assert.That(rig.Events.ConvertAll(e => e.State), Is.EqualTo(new[] { CommunicationState.Offline, CommunicationState.Ready }));
            Assert.That(rig.Events[0].ReplyRemainingSimulationSeconds, Is.EqualTo(0d));
        }

        [Test]
        public void Transmit_SpendsTheEnergyOnce_FreezesTheSample_AndStartsTheReplyClock()
        {
            var rig = new Rig();
            rig.Clock = 3600d;

            CommandResult sent = rig.Link.TryTransmit();

            Assert.That(sent.Succeeded, Is.True, sent.Explanation);
            Assert.That(sent.Explanation, Does.Contain("31 min 59 s").And.Contain("10 min 59 s"));
            Assert.That(rig.Ledger.Snapshot.BatteryKWh, Is.EqualTo(4.5d).Within(1e-12));
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply));
            Assert.That(rig.Link.ReferenceDistanceKm, Is.EqualTo(197707975.990852982d).Within(1e-6));
            Assert.That(rig.Link.OneWayLightSeconds, Is.EqualTo(Row0OneWay).Within(1e-9));
            Assert.That(rig.Link.EarthProcessingSeconds, Is.EqualTo(600d));
            Assert.That(rig.Link.ReplyRemainingSimulationSeconds, Is.EqualTo(Row0Delay).Within(1e-6));
            Assert.That(rig.Link.TransmittedAtSimulationSeconds, Is.EqualTo(3600d));
            Assert.That(rig.Link.ReplyDueAtSimulationSeconds, Is.EqualTo(3600d + Row0Delay).Within(1e-6));
            Assert.That(rig.Events, Has.Count.EqualTo(1));
            Assert.That(rig.Events[0].State, Is.EqualTo(CommunicationState.AwaitingReply));
            Assert.That(rig.Events[0].ReplyRemainingSimulationSeconds, Is.EqualTo(Row0Delay).Within(1e-6));
        }

        [Test]
        public void Reply_IsNeverReceivedBeforeTwoLightTimesPlusProcessing()
        {
            var rig = new Rig();
            Assert.That(rig.Link.TryTransmit().Succeeded, Is.True);
            double oneWay = rig.Link.OneWayLightSeconds;
            double delay = rig.Link.ReplyDelaySimulationSeconds;
            Assert.That(delay, Is.EqualTo(Row0Delay).Within(1e-6));
            Assert.That(delay, Is.EqualTo((2d * oneWay) + rig.Link.EarthProcessingSeconds).Within(1e-12));

            rig.Advance(oneWay);
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply), "one light time is only the outbound leg");
            rig.Advance(oneWay);
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply), "the round trip without processing is still too early");
            rig.Advance(600d - 0.001d);
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply));
            Assert.That(rig.Link.ReplyRemainingSimulationSeconds, Is.EqualTo(0.001d).Within(1e-6));
            Assert.That(rig.Link.TimeToNextBoundary(), Is.EqualTo(0.001d).Within(1e-6));

            rig.Advance(0.001d);

            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.ReplyReceived));
            Assert.That(rig.Link.ReplyReceivedAtSimulationSeconds.Value - rig.Link.TransmittedAtSimulationSeconds.Value,
                Is.GreaterThanOrEqualTo(delay - CommunicationSystem.TimeToleranceSeconds));
            Assert.That(rig.Link.ReplyRemainingSimulationSeconds, Is.EqualTo(0d));
            Assert.That(double.IsPositiveInfinity(rig.Link.TimeToNextBoundary()), Is.True);
            Assert.That(rig.Events[rig.Events.Count - 1].State, Is.EqualTo(CommunicationState.ReplyReceived));
            Assert.That(rig.Events, Has.Count.EqualTo(2), "AwaitingReply, then ReplyReceived");
        }

        [Test]
        public void RepeatedTransmit_IsRejected_AndCannotRestartTheTimer()
        {
            var rig = new Rig();
            Assert.That(rig.Link.TryTransmit().Succeeded, Is.True);
            rig.Advance(500d);
            double dueAt = rig.Link.ReplyDueAtSimulationSeconds.Value;

            CommandResult again = rig.Link.TryTransmit();

            Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            Assert.That(rig.Link.ReplyDueAtSimulationSeconds, Is.EqualTo(dueAt));
            Assert.That(rig.Ledger.Snapshot.BatteryKWh, Is.EqualTo(4.5d).Within(1e-12), "charged once");
            Assert.That(rig.Events, Has.Count.EqualTo(1));

            rig.Advance(Row0Delay);
            Assert.That(rig.Link.TryTransmit().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "no second call after the reply either");
            Assert.That(rig.Ledger.Snapshot.BatteryKWh, Is.EqualTo(4.5d).Within(1e-12));
        }

        [Test]
        public void RejectedTransmit_SpendsNothing_AndPublishesNothing()
        {
            var noLink = new Rig();
            noLink.LinkCheck = new CommandResult(false, CommandFailure.PreconditionsNotMet, "Communications offline: antenna damaged");
            CommandResult blocked = noLink.Link.TryTransmit();
            Assert.That(blocked.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(blocked.Explanation, Is.EqualTo("Communications offline: antenna damaged"));
            Assert.That(noLink.Ledger.Snapshot.BatteryKWh, Is.EqualTo(5d));
            Assert.That(noLink.Link.HasTransmitted, Is.False);
            Assert.That(noLink.Events, Is.Empty);

            var lowBattery = new Rig(batteryKWh: 0.2d);
            CommandResult unaffordable = lowBattery.Link.TryTransmit();
            Assert.That(unaffordable.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
            Assert.That(lowBattery.Link.State, Is.EqualTo(CommunicationState.Ready), "the hardware is fine; the energy is missing");
            Assert.That(lowBattery.Ledger.Snapshot.BatteryKWh, Is.EqualTo(0.2d));
            Assert.That(lowBattery.Link.HasTransmitted, Is.False);
            Assert.That(double.IsPositiveInfinity(lowBattery.Link.TimeToNextBoundary()), Is.True, "no timer was started");
            Assert.That(lowBattery.Events, Is.Empty);
        }

        [Test]
        public void AStrayAdvance_CannotBringTheReplyForward_AndInvalidTimeIsRejected()
        {
            var rig = new Rig();
            Assert.That(rig.Link.TryTransmit().Succeeded, Is.True);

            rig.Link.AdvanceTime(1e9);
            rig.Link.AdvanceTime(1e9);

            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply), "only the mission clock moves the reply");
            Assert.That(rig.Link.ReplyRemainingSimulationSeconds, Is.EqualTo(Row0Delay).Within(1e-6));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Link.AdvanceTime(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Link.AdvanceTime(-1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Link.AdvanceTime(double.PositiveInfinity));
            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply));
        }

        [Test]
        public void AnUnpoweredLink_CannotReceiveTheReply_UntilPowerReturns()
        {
            var rig = new Rig();
            Assert.That(rig.Link.TryTransmit().Succeeded, Is.True);
            rig.Powered = false;

            rig.Advance(Row0Delay + 100d);

            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.AwaitingReply));
            Assert.That(rig.Link.ReplyRemainingSimulationSeconds, Is.EqualTo(0d));
            Assert.That(rig.Link.IsReplyHeldByUnpoweredLink, Is.True);
            Assert.That(double.IsPositiveInfinity(rig.Link.TimeToNextBoundary()), Is.True, "nothing to split a step on while the reply is held");

            rig.Powered = true;
            rig.Advance(1d);

            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.ReplyReceived));
            Assert.That(rig.Link.ReplyReceivedAtSimulationSeconds, Is.EqualTo(Row0Delay + 101d).Within(1e-6));
            Assert.That(rig.Link.IsReplyHeldByUnpoweredLink, Is.False);
        }

        [Test]
        public void TheMissionDate_SelectsTheSample_AndIsFrozenAtTransmission()
        {
            var rig = new Rig(start: new DateTime(2026, 12, 14, 8, 0, 0, DateTimeKind.Utc));
            Assert.That(rig.Link.ReferenceSample.Index, Is.EqualTo(30), "mission day 31 of the snapshot");
            Assert.That(rig.Link.OneWayLightSeconds, Is.EqualTo(529.587794443d).Within(1e-9));
            Assert.That(rig.Link.ReplyDelaySimulationSeconds, Is.EqualTo(1059.175588886d + 600d).Within(1e-6));

            rig.Clock = 3600d;
            Assert.That(rig.Link.TryTransmit().Succeeded, Is.True);
            rig.Advance(86400d);

            Assert.That(rig.Link.State, Is.EqualTo(CommunicationState.ReplyReceived));
            Assert.That(rig.Link.ReferenceSample.Index, Is.EqualTo(30), "the distance stays the one of the transmission");
            Assert.That(rig.Link.ReferenceEpochUtc, Is.EqualTo(new DateTime(2026, 12, 14, 8, 0, 0, DateTimeKind.Utc)));
            Assert.That(rig.Link.TransmittedAtEpochUtc, Is.EqualTo(new DateTime(2026, 12, 14, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void ADateAfterTheSnapshot_HasNoReference_SoNothingCanBeSent()
        {
            var rig = new Rig(start: new DateTime(2026, 12, 15, 7, 0, 0, DateTimeKind.Utc));
            rig.Clock = 3600d;
            Assert.That(rig.Link.IsReferenceAvailable, Is.True, "exactly the last epoch is still inside the snapshot");
            Assert.That(rig.Link.ReferenceSample.Index, Is.EqualTo(31));

            rig.Clock = 3601d;
            CommandResult late = rig.Link.TryTransmit();

            Assert.That(rig.Link.IsReferenceAvailable, Is.False);
            Assert.That(late.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(late.Explanation, Does.Contain("ends on 2026-12-15 08:00 UTC"));
            Assert.That(rig.Ledger.Snapshot.BatteryKWh, Is.EqualTo(5d));
            Assert.That(rig.Link.HasTransmitted, Is.False);
            Assert.That(rig.Link.ReferenceSample.Index, Is.EqualTo(31), "the display stays on the last reviewed sample");
            Assert.That(rig.Events, Is.Empty);
        }

        [Test]
        public void DisplayHelpers_RoundForPeople_NotForPhysics()
        {
            Assert.That(CommunicationSystem.FormatDuration(1918.965642d), Is.EqualTo("31 min 59 s"));
            Assert.That(CommunicationSystem.FormatDuration(659.482821248d), Is.EqualTo("10 min 59 s"));
            Assert.That(CommunicationSystem.FormatDuration(1059.175588885d), Is.EqualTo("17 min 39 s"));
            Assert.That(CommunicationSystem.FormatDuration(0.2d, true), Is.EqualTo("1 s"), "a countdown shows zero only when it is over");
            Assert.That(CommunicationSystem.FormatDuration(3725d), Is.EqualTo("1 h 02 min 05 s"));
            Assert.That(CommunicationSystem.FormatDuration(double.NaN), Is.EqualTo("unknown"));
            Assert.That(CommunicationSystem.FormatDistance(197707975.990852982d), Is.EqualTo("197,708,000 km"));
        }

        [Test]
        public void Spec_RejectsInvalidAssumptions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommunicationSpec(earthProcessingSeconds: -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommunicationSpec(transmitEnergyKWh: double.NaN));
            Assert.That(new CommunicationSpec().EarthProcessingSeconds, Is.EqualTo(600d));
            Assert.That(new CommunicationSpec().TransmitEnergyKWh, Is.EqualTo(0.5d));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class EventBusTests
    {
        [Test]
        public void ThrowingSubscriber_DoesNotStopLaterSubscribers_AndIsReported()
        {
            var errors = new List<Exception>();
            var bus = new MissionEventBus(errors.Add);
            var received = new List<string>();
            bus.PhaseChanged += _ => received.Add("first");
            bus.PhaseChanged += _ => throw new InvalidOperationException("broken view");
            bus.PhaseChanged += _ => received.Add("third");

            bus.Publish(new PhaseChanged(GamePhase.Planning, GamePhase.Intro));

            Assert.That(received, Is.EqualTo(new[] { "first", "third" }));
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Message, Is.EqualTo("broken view"));
            Assert.That(bus.SubscriberFailureCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingSubscriber_DoesNotRollBackCommittedLaunch()
        {
            var errors = new List<Exception>();
            using (var session = new MissionSession(AuroraMission.CreateDefinition(), errors.Add))
            {
                int resourceEvents = 0;
                session.Events.PhaseChanged += _ => throw new InvalidOperationException("view crashed");
                session.Events.ResourceChanged += _ => resourceEvents++;

                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(launch.Succeeded, Is.True, launch.Explanation);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.Resources.IsInitialized, Is.True);
                Assert.That(resourceEvents, Is.EqualTo(1));
                Assert.That(errors, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void SameHandlerSubscribedTwice_IsDeliveredOnce_AndUnsubscribeStopsDelivery()
        {
            var bus = new MissionEventBus();
            int calls = 0;
            Action<ResourceChanged> handler = _ => calls++;

            bus.ResourceChanged += handler;
            bus.ResourceChanged += handler;
            bus.Publish(new ResourceChanged(default(ResourceSnapshot)));

            Assert.That(bus.GetSubscriberCount(MissionEventKind.ResourceChanged), Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));

            bus.ResourceChanged -= handler;
            bus.Publish(new ResourceChanged(default(ResourceSnapshot)));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(bus.GetSubscriberCount(MissionEventKind.ResourceChanged), Is.EqualTo(0));
        }

        [Test]
        public void PublishFromInsideHandler_IsDeliveredAfterCurrentEvent_InCommitOrder()
        {
            var bus = new MissionEventBus();
            var log = new List<string>();
            bus.PhaseChanged += message =>
            {
                log.Add("A:" + message.Current);
                if (message.Current == GamePhase.Intro)
                {
                    bus.Publish(new PhaseChanged(GamePhase.Intro, GamePhase.Emergency));
                }
            };
            bus.PhaseChanged += message => log.Add("B:" + message.Current);

            bus.Publish(new PhaseChanged(GamePhase.Planning, GamePhase.Intro));

            Assert.That(log, Is.EqualTo(new[] { "A:Intro", "B:Intro", "A:Emergency", "B:Emergency" }));
            Assert.That(bus.IsDispatching, Is.False);
        }

        [Test]
        public void HandlerRemovedDuringDispatch_IsNotInvokedLaterInThatDispatch()
        {
            var bus = new MissionEventBus();
            var log = new List<string>();
            Action<PhaseChanged> second = _ => log.Add("second");
            bus.PhaseChanged += _ =>
            {
                log.Add("first");
                bus.PhaseChanged -= second;
            };
            bus.PhaseChanged += second;

            bus.Publish(new PhaseChanged(GamePhase.Planning, GamePhase.Intro));

            Assert.That(log, Is.EqualTo(new[] { "first" }));
        }

        [Test]
        public void DeferDispatch_HoldsDeliveryUntilTheScopeEnds()
        {
            var bus = new MissionEventBus();
            int calls = 0;
            bus.MissionEnded += _ => calls++;

            using (bus.DeferDispatch())
            {
                bus.Publish(new MissionEnded(MissionOutcome.Lost, "test", default(ResourceSnapshot)));
                Assert.That(calls, Is.EqualTo(0));
            }

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ClearAllSubscriptions_RemovesEveryListener()
        {
            var bus = new MissionEventBus();
            using (var recorder = new EventRecorder(bus))
            {
                Assert.That(bus.TotalSubscriberCount, Is.EqualTo(8));

                bus.ClearAllSubscriptions();
                bus.Publish(new PhaseChanged(GamePhase.Planning, GamePhase.Intro));

                Assert.That(bus.TotalSubscriberCount, Is.EqualTo(0));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }
    }
}

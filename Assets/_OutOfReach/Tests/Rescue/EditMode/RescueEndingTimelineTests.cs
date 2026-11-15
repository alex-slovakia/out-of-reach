using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Presentation.Rescue;
using T = OutOfReach.Presentation.Rescue.RescueEndingTimeline;

namespace OutOfReach.Tests.Rescue.EditMode
{
    public sealed class RescueEndingTimelineTests
    {
        private const string Vo24a = "Aurora Seven, rescue crew. We have you in sight. You've held on long enough. We're coming alongside. Stand by for docking.";
        private const string Vo24b = "Contact. Capture confirmed. Hard dock. Stay clear while we equalize the hatch. Aurora Seven... you're not alone anymore.";

        [Test]
        public void RadioSubtitles_AreTheApprovedTranscripts_WordForWord()
        {
            string Joined(float from, float to) => string.Join(" ", T.Lines
                .Where(l => l.Speaker == T.RadioSpeaker && l.Start >= from && l.Start < to).Select(l => l.Text));
            Assert.AreEqual(Vo24a, Joined(T.VoiceApproachStart, T.VoiceDockedStart));
            Assert.AreEqual(Vo24b, Joined(T.VoiceDockedStart, T.Duration));
        }

        [Test]
        public void DockedVoice_StartsOnlyAfterTheVisibleContact_OnTheDockingCamera()
        {
            Assert.Greater(T.VoiceDockedStart, T.Contact);
            Assert.AreEqual(RescueShot.DockingCamera, T.ShotAt(T.Contact));
            Assert.AreEqual(RescueShot.DockingCamera, T.ShotAt(T.VoiceDockedStart));
            Assert.AreEqual(1f, T.DockClosing(T.Contact), 1e-5f);
            Assert.Less(T.DockClosing(T.Contact - .5f), 1f);
            Assert.Less(T.Contact, T.Capture);
            Assert.Less(T.Capture, T.HardDock);
            Assert.LessOrEqual(T.VoiceApproachStart + T.VoiceApproachSeconds, T.Contact, "VO24a ends before the contact");
            Assert.LessOrEqual(T.VoiceDockedStart + T.VoiceDockedSeconds, T.FadeOutEnd, "VO24b is heard before the fade ends");
        }

        [Test]
        public void Lines_AreOrderedAndInsideTheSequence()
        {
            for (int i = 0; i < T.Lines.Length; i++)
            {
                Assert.Less(T.Lines[i].Start, T.Lines[i].End);
                Assert.GreaterOrEqual(T.Lines[i].Start, 0f);
                Assert.LessOrEqual(T.Lines[i].End, T.Duration);
                if (i > 0) Assert.GreaterOrEqual(T.Lines[i].Start, T.Lines[i - 1].End - 1e-4f, "no overlap at " + i);
            }
            Assert.AreEqual(-1, T.LineAt(0f));
            Assert.AreEqual(T.Lines.Length - 1, T.LineAt(T.Duration - .01f));
        }

        [Test]
        public void Shots_CutUnderBlack_AndTheSequenceEndsBlack()
        {
            Assert.AreEqual(RescueShot.Window, T.ShotAt(0f));
            Assert.AreEqual(RescueShot.DockingCamera, T.ShotAt(T.Cut1));
            Assert.AreEqual(RescueShot.Hatch, T.ShotAt(T.Cut2));
            Assert.AreEqual(1f, T.Blackout(T.Cut1, false), 1e-4f);
            Assert.AreEqual(1f, T.Blackout(T.Cut2, false), 1e-4f);
            Assert.AreEqual(1f, T.Blackout(T.OpeningCut, true), 1e-4f);
            Assert.AreEqual(0f, T.Blackout(0f, false), 1e-4f);
            Assert.AreEqual(0f, T.Blackout(8f, true), 1e-4f);
            Assert.AreEqual(1f, T.Blackout(T.Duration, false), 1e-4f);
            for (float t = 0; t <= T.Duration; t += .05f)
            {
                float b = T.Blackout(t, true);
                Assert.That(b, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ReducedEffects_RemoveShakeAndPush_ButKeepTheBeats()
        {
            for (float t = 0; t <= T.Duration; t += .05f)
            {
                Assert.AreEqual(0f, T.Shake(t, true));
                Assert.AreEqual(0f, T.HatchPush(t, true));
            }
            Assert.Greater(T.Shake(T.HardDock + .05f, false), .5f);
            Assert.Greater(T.Shake(T.Contact + .05f, false), .1f);
            Assert.AreEqual(1f, T.HatchOpen(T.HatchOpenEnd), 1e-5f);
            Assert.AreEqual(3, T.LatchesClosed(T.Duration));
        }

        [Test]
        public void Approach_BrakesToRest_AndCurvesAreMonotonic()
        {
            float previous = -1f, previousDock = -1f;
            for (float t = 0; t <= T.Duration; t += .05f)
            {
                float a = T.Approach(t), d = T.DockClosing(t);
                Assert.GreaterOrEqual(a, previous); Assert.GreaterOrEqual(d, previousDock);
                previous = a; previousDock = d;
            }
            // Braking: the last second covers far less distance than the first second of the approach.
            float first = T.Approach(T.ApproachStart + 1f) - T.Approach(T.ApproachStart);
            float last = T.Approach(T.ApproachEnd) - T.Approach(T.ApproachEnd - 1f);
            Assert.Greater(first, last * 10f);
            Assert.Greater(T.RetroBurn(4.5f), .5f);
            Assert.AreEqual(0f, T.RetroBurn(T.Cut2 + 1f));
            Assert.AreEqual(0f, T.Floodlight(T.Cut2 + .1f));
        }

        [Test]
        public void BusFilter_WithholdsOnlyATakenOverRecoveryArrival()
        {
            var bus = new MissionEventBus();
            bool take = true;
            var filter = new RescueEndingBusFilter(bus, request => take);
            var seen = new List<CinematicId>();
            System.Action<CinematicRequested> listener = request => seen.Add(request.Cinematic);
            filter.CinematicRequested += listener;
            bus.Publish(new CinematicRequested(CinematicId.RecoveryArrival, 1));
            bus.Publish(new CinematicRequested(CinematicId.MarsDeparture, 2));
            take = false;
            bus.Publish(new CinematicRequested(CinematicId.RecoveryArrival, 3));
            CollectionAssert.AreEqual(new[] { CinematicId.MarsDeparture, CinematicId.RecoveryArrival }, seen);
            Assert.AreEqual(1, filter.WithheldRequests);
            filter.CinematicRequested -= listener;
            bus.Publish(new CinematicRequested(CinematicId.MarsDeparture, 4));
            Assert.AreEqual(2, seen.Count, "unsubscribe removes the wrapped listener");
            Assert.AreEqual(0, bus.GetSubscriberCount(MissionEventKind.CinematicRequested));
        }

        [Test]
        public void BusFilter_FailsOpenWhenTheOwnerThrows()
        {
            var bus = new MissionEventBus();
            var filter = new RescueEndingBusFilter(bus, request => throw new System.InvalidOperationException("broken owner"));
            int seen = 0;
            filter.CinematicRequested += request => seen++;
            bus.Publish(new CinematicRequested(CinematicId.RecoveryArrival, 1));
            Assert.AreEqual(1, seen);
        }
    }
}

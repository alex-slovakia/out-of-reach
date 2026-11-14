using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class CinematicCoordinatorTests
    {
        [Test]
        public void MatchingAcknowledgement_CompletesOnce_StaleDuplicateAndMismatchedAreIgnored()
        {
            using (var session = Fixtures.NewSession())
            using (var presentation = new EventRecorder(session.Events))
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Succeeded, Is.True);
                CinematicRequested request = presentation.CinematicRequests.Single();

                session.Events.Publish(new CinematicFinished(CinematicId.Impact, request.SequenceId - 1, false));
                session.Events.Publish(new CinematicFinished(CinematicId.FalseSignal, request.SequenceId, false));
                Assert.That(completions, Is.Empty);

                session.Events.Publish(new CinematicFinished(CinematicId.Impact, request.SequenceId, true));
                session.Events.Publish(new CinematicFinished(CinematicId.Impact, request.SequenceId, false));

                Assert.That(completions, Has.Count.EqualTo(1));
                Assert.That(completions[0].SequenceId, Is.EqualTo(request.SequenceId));
                Assert.That(completions[0].WasSkipped, Is.True);
                Assert.That(completions[0].TimedOut, Is.False);
                Assert.That(session.Cinematics.IgnoredAcknowledgements, Is.EqualTo(3));
                Assert.That(session.Cinematics.IsWaiting, Is.False);
            }
        }

        [Test]
        public void MissingAcknowledgement_FallsBackAfterTimeout_AndALateAckIsIgnored()
        {
            using (var session = Fixtures.NewSession())
            using (var presentation = new EventRecorder(session.Events))
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add, 10d).Succeeded, Is.True);
                int sequenceId = presentation.CinematicRequests.Single().SequenceId;

                session.Tick(9.5d);
                Assert.That(completions, Is.Empty);

                session.Tick(0.5d);
                session.Events.Publish(new CinematicFinished(CinematicId.Impact, sequenceId, false));

                Assert.That(completions, Has.Count.EqualTo(1));
                Assert.That(completions[0].TimedOut, Is.True);
                Assert.That(session.Cinematics.IgnoredAcknowledgements, Is.EqualTo(1));
            }
        }

        [Test]
        public void NoPresentationListener_CompletesOnTheNextTick()
        {
            using (var session = Fixtures.NewSession())
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Succeeded, Is.True);
                Assert.That(completions, Is.Empty, "never completes synchronously inside the request");

                session.Tick(0d);

                Assert.That(completions, Has.Count.EqualTo(1));
                Assert.That(completions[0].TimedOut, Is.True);
            }
        }

        [Test]
        public void PausedGame_DoesNotTimeOutACinematic()
        {
            using (var session = Fixtures.NewSession())
            using (new EventRecorder(session.Events))
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add, 5d).Succeeded, Is.True);
                Assert.That(session.Clock.Pause().Succeeded, Is.True);

                session.Tick(100d);
                Assert.That(completions, Is.Empty);

                Assert.That(session.Clock.Resume().Succeeded, Is.True);
                session.Tick(5d);
                Assert.That(completions, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void SecondRequestWhileActive_IsRejected_WithoutANewEvent()
        {
            using (var session = Fixtures.NewSession())
            using (var presentation = new EventRecorder(session.Events))
            {
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, null).Succeeded, Is.True);

                CommandResult second = session.Cinematics.TryRequest(CinematicId.FalseSignal, null);

                Assert.That(second.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(presentation.CinematicRequests, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void SkipAcknowledgedFromInsideTheRequestHandler_IsProcessedAfterTheRequest()
        {
            using (var session = Fixtures.NewSession())
            {
                session.Events.CinematicRequested += request =>
                    session.Events.Publish(new CinematicFinished(request.Cinematic, request.SequenceId, true));
                var completions = new List<CinematicCompletion>();

                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Succeeded, Is.True);

                Assert.That(completions, Has.Count.EqualTo(1));
                Assert.That(completions[0].WasSkipped, Is.True);
                Assert.That(session.Cinematics.IsWaiting, Is.False);
            }
        }

        [Test]
        public void CompletionCallback_CanRequestTheNextCinematic()
        {
            using (var session = Fixtures.NewSession())
            using (var presentation = new EventRecorder(session.Events))
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, first =>
                {
                    completions.Add(first);
                    Assert.That(session.Cinematics.TryRequest(CinematicId.FalseSignal, completions.Add).Succeeded, Is.True);
                }).Succeeded, Is.True);

                int firstId = presentation.CinematicRequests[0].SequenceId;
                session.Events.Publish(new CinematicFinished(CinematicId.Impact, firstId, false));

                Assert.That(presentation.CinematicRequests, Has.Count.EqualTo(2));
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.FalseSignal));
                Assert.That(session.Cinematics.IsWaiting, Is.True);
                Assert.That(presentation.CinematicRequests[1].SequenceId, Is.Not.EqualTo(firstId));
            }
        }

        [Test]
        public void Dispose_UnsubscribesAndCancels_WithoutRunningTheCallback()
        {
            var session = Fixtures.NewSession();
            var completions = new List<CinematicCompletion>();
            Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Succeeded, Is.True);

            session.Dispose();

            Assert.That(session.Events.GetSubscriberCount(MissionEventKind.CinematicFinished), Is.EqualTo(0));
            Assert.That(session.Cinematics.IsWaiting, Is.False);
            Assert.That(session.Tick(1d).Succeeded, Is.False);
            Assert.That(completions, Is.Empty);
            Assert.That(session.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Failure, Is.EqualTo(CommandFailure.WrongPhase));
        }

        [Test]
        public void SequenceIds_AreUniqueAcrossSessions_SoAnOldAckCannotCompleteANewRequest()
        {
            using (var oldSession = Fixtures.NewSession())
            using (var newSession = Fixtures.NewSession())
            using (var oldPresentation = new EventRecorder(oldSession.Events))
            using (var newPresentation = new EventRecorder(newSession.Events))
            {
                var completions = new List<CinematicCompletion>();
                Assert.That(oldSession.Cinematics.TryRequest(CinematicId.Impact, null).Succeeded, Is.True);
                Assert.That(newSession.Cinematics.TryRequest(CinematicId.Impact, completions.Add).Succeeded, Is.True);
                int oldId = oldPresentation.CinematicRequests.Single().SequenceId;
                int newId = newPresentation.CinematicRequests.Single().SequenceId;

                newSession.Events.Publish(new CinematicFinished(CinematicId.Impact, oldId, false));

                Assert.That(newId, Is.Not.EqualTo(oldId));
                Assert.That(completions, Is.Empty);
                Assert.That(newSession.Cinematics.IsWaiting, Is.True);
            }
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Presentation.UI.Startup;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Tests.StartupUi
{
    /// <summary>R01 startup cards: timeline, skip, reduced effects, pictogram geometry and the exact on-screen wording.</summary>
    public sealed class StartupCardTests
    {
        private static void Run(StartupSequence sequence, float seconds)
        {
            for (float t = 0f; t < seconds; t += .05f) sequence.Advance(.05f);
        }

        [Test]
        public void FadesInAndOutAndEndsOnTheMenu()
        {
            var sequence = new StartupSequence(false);
            Assert.AreEqual(0f, sequence.Evaluate().PictogramAlpha);
            Assert.AreEqual(1f, sequence.Evaluate().OverlayAlpha);
            Run(sequence, sequence.Timing.PictogramStart + sequence.Timing.PictogramIn + .5f);
            Assert.AreEqual(1f, sequence.Evaluate().PictogramAlpha, .001f);
            Assert.AreEqual(0f, sequence.Evaluate().EventAlpha);
            Run(sequence, sequence.Timing.EventStart + sequence.Timing.EventIn + 1f - sequence.Elapsed);
            Assert.AreEqual(0f, sequence.Evaluate().PictogramAlpha, .001f);
            Assert.AreEqual(1f, sequence.Evaluate().EventAlpha, .001f);
            Run(sequence, sequence.TotalSeconds + 1f);
            Assert.IsTrue(sequence.IsFinished);
            Assert.AreEqual(0f, sequence.Evaluate().OverlayAlpha);
        }

        [Test]
        public void SkipFadesOutFromTheCurrentFrameInsteadOfCutting()
        {
            var sequence = new StartupSequence(false);
            Run(sequence, 2.5f);
            float before = sequence.Evaluate().PictogramAlpha;
            Assert.Greater(before, .5f);
            sequence.Skip();
            Assert.IsFalse(sequence.IsFinished);
            Assert.AreEqual(before, sequence.Evaluate().PictogramAlpha, .001f);
            sequence.Advance(sequence.Timing.SkipFade * .5f);
            Assert.Less(sequence.Evaluate().OverlayAlpha, 1f);
            Assert.Greater(sequence.Evaluate().OverlayAlpha, 0f);
            sequence.Advance(sequence.Timing.SkipFade);
            Assert.IsTrue(sequence.IsFinished);
        }

        [Test]
        public void ReducedEffectsIsShorterAndNeverAnimatesTheDrawOn()
        {
            var reduced = new StartupSequence(true);
            var full = new StartupSequence(false);
            Assert.Less(reduced.TotalSeconds, full.TotalSeconds);
            Assert.AreEqual(1f, reduced.Evaluate().Draw01, .0001f);
            Run(reduced, .5f);
            Assert.AreEqual(1f, reduced.Evaluate().Draw01, .0001f);
            Assert.Less(full.Evaluate().Draw01, 1f);
        }

        [Test]
        public void PictogramIsAClosedSlovakiaShapeWithPencilStrokes()
        {
            var outline = SlovakiaPictogram.Outline();
            Assert.GreaterOrEqual(outline.Length, 30);
            foreach (var point in outline)
            {
                Assert.That(point.x, Is.InRange(0f, SlovakiaPictogram.Aspect + .001f));
                Assert.That(point.y, Is.InRange(0f, 1.001f));
            }
            Assert.That(SlovakiaPictogram.Aspect, Is.InRange(1.8f, 2.2f));
            var strokes = SlovakiaPictogram.Build();
            Assert.Greater(strokes.Count, 20, "outline passes plus hatching");
            Assert.AreEqual(strokes.Count, SlovakiaPictogram.Build().Count, "deterministic");
            var buffer = new List<Vector2>();
            Assert.AreEqual(0, SlovakiaPictogram.VisiblePoints(strokes[0], -.1f, buffer));
            Assert.GreaterOrEqual(SlovakiaPictogram.VisiblePoints(strokes[0], .25f, buffer), 2);
            int partial = buffer.Count;
            SlovakiaPictogram.VisiblePoints(strokes[0], 1f, buffer);
            Assert.Greater(buffer.Count, partial, "draw-on reveals the stroke progressively");
        }

        [Test]
        public void SecondCardCarriesTheExactEventWordingAndTeamName()
        {
            Assert.AreEqual("NASA Space Apps Challenge · Nov 14–15 2026", StartupCardPresenter.EventLine);
            Assert.AreEqual("Polish Alex Goats", StartupCardPresenter.TeamName);
            var presenter = new StartupCardPresenter(false);
            Assert.AreEqual(StartupCardPresenter.EventLine, presenter.Q<Label>("startup-event-line").text);
            Assert.AreEqual(StartupCardPresenter.TeamName, presenter.Q<Label>("startup-team-name").text);
        }

        [Test]
        public void PresenterAdvancesFinishesAndSkipsAfterTheGraceWindow()
        {
            int finished = 0;
            var host = new VisualElement();
            var presenter = new StartupCardPresenter(true, null, null, () => finished++);
            host.Add(presenter);
            presenter.RequestSkip();                       // inside the grace window: ignored
            Assert.IsFalse(presenter.Sequence.IsSkipped);
            presenter.Advance(StartupCardPresenter.SkipGraceSeconds + .01f);
            presenter.RequestSkip();
            Assert.IsTrue(presenter.Sequence.IsSkipped);
            for (int i = 0; i < 20 && presenter.IsPlaying; i++) presenter.Advance(.05f);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, finished);
            Assert.IsNull(presenter.parent, "removed from the host when finished");
        }
    }
}

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
        public void SketchThenFillThenTeamName()
        {
            var sequence = new StartupSequence(false);
            var t = sequence.Timing;
            Run(sequence, t.DrawStart + t.DrawSeconds * .5f);
            var mid = sequence.Evaluate();
            Assert.That(mid.Draw01, Is.InRange(.3f, .7f));
            Assert.AreEqual(0f, mid.Fill01, "fill waits for the last stroke");
            Run(sequence, t.DrawSeconds * .5f + t.FillSeconds + .1f);
            var done = sequence.Evaluate();
            Assert.AreEqual(1f, done.Draw01, .001f);
            Assert.AreEqual(1f, done.Fill01, .001f);
        }

        [Test]
        public void ShippedPlaceholderArtLoadsAsOrderedStrokes()
        {
            var art = StartupCardPresenter.LoadArt();
            Assert.IsFalse(art.IsEmpty, "Resources/StartupCards/startup-art.txt must hold valid SVG");
            Assert.GreaterOrEqual(art.Shapes.Count, 10);
            Assert.That(art.Aspect, Is.InRange(.5f, 2f));
            float previousEnd = -1f;
            foreach (var shape in art.Shapes)
            {
                Assert.Greater(shape.RevealStart, previousEnd - .0001f, "document order = stroke order");
                Assert.Greater(shape.RevealEnd, shape.RevealStart);
                previousEnd = shape.RevealEnd;
            }
            Assert.LessOrEqual(previousEnd, 1.0001f);
        }

        [Test]
        public void SvgImportHandlesPrimitivesTransformsArcsAndFillOnlyShapes()
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 100'>" +
                "<rect x='10' y='10' width='40' height='30' fill='#ff0000'/>" +
                "<g transform='translate(100 50)'><circle r='20' fill='none' stroke='#00ff00' stroke-width='4'/></g>" +
                "<path d='M10 90 a20 20 0 0 1 40 0' fill='none' stroke='gold'/>" +
                "<path d='m 120,10 l 30,0 0,30 z' fill='rgb(0,0,255)' fill-opacity='.5'/>" +
                "<path d='M0 0 L5 5' fill='none' stroke='none'/>" +
                "</svg>";
            var art = VectorArt.Parse(svg);
            Assert.AreEqual(2f, art.Aspect, .0001f);
            Assert.AreEqual(4, art.Shapes.Count, "the stroke-less, fill-less path is dropped");
            Assert.IsTrue(art.Shapes[0].SketchedFromFill, "fill-only shapes are traced in pencil gold first");
            Assert.AreEqual(1f, art.Shapes[0].Fill.r, .001f);
            Assert.IsFalse(art.Shapes[1].HasFill);
            // circle centred at (100,50) with r 20: unit x range (80..120)/100
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in art.Shapes[1].Subpaths[0]) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
            Assert.AreEqual(.8f, minX, .01f);
            Assert.AreEqual(1.2f, maxX, .01f);
            Assert.AreEqual(.04f, art.Shapes[1].StrokeWidth, .0001f);
            Assert.AreEqual(.5f, art.Shapes[3].Fill.a, .001f);
            Assert.IsTrue(art.Shapes[3].Closed[0]);
            Assert.IsTrue(VectorArt.Parse("not xml").IsEmpty);
            Assert.IsTrue(VectorArt.Parse("<svg xmlns='http://www.w3.org/2000/svg'/>").IsEmpty);
        }

        [Test]
        public void VisibleSubpathsRevealProgressively()
        {
            var art = StartupCardPresenter.LoadArt();
            var shape = art.Shapes[0];
            var output = new List<List<Vector2>>();
            var pool = new Stack<List<Vector2>>();
            VectorArt.VisibleSubpaths(shape, false, shape.RevealStart - .01f, output, pool);
            Assert.AreEqual(0, output.Count);
            VectorArt.VisibleSubpaths(shape, false, Mathf.Lerp(shape.RevealStart, shape.RevealEnd, .4f), output, pool);
            Assert.AreEqual(1, output.Count);
            int partial = output[0].Count;
            VectorArt.VisibleSubpaths(shape, false, shape.RevealEnd, output, pool);
            Assert.Greater(output[0].Count, partial);
        }

        [Test]
        public void PencilSoundIsProceduralDeterministicAndFollowsThePen()
        {
            var art = StartupCardPresenter.LoadArt();
            var timing = StartupTiming.Normal;
            var samples = PencilScratchSynth.Render(art, timing);
            Assert.AreEqual((timing.DrawSeconds + timing.FillSeconds) * PencilScratchSynth.SampleRate, samples.Length, PencilScratchSynth.SampleRate * .2f);
            float peak = 0f;
            foreach (var v in samples) { Assert.That(v, Is.InRange(-1f, 1f)); peak = Mathf.Max(peak, Mathf.Abs(v)); }
            Assert.Greater(peak, .5f);
            var again = PencilScratchSynth.Render(art, timing);
            CollectionAssert.AreEqual(samples, again, "same art, timing and seed give identical audio");
            // The first stroke is audible, the last 40 ms of the clip are faded out.
            Assert.Greater(Rms(samples, (int)(.2f * PencilScratchSynth.SampleRate), 4000), .02f);
            Assert.Less(Rms(samples, samples.Length - 400, 400), .02f);
            Assert.IsEmpty(PencilScratchSynth.Render(art, StartupTiming.ReducedEffects), "reduced effects: no sketch, no scratch");
            Assert.IsEmpty(PencilScratchSynth.Render(new VectorArt(), timing));
        }

        private static float Rms(float[] samples, int start, int count)
        {
            double energy = 0;
            int n = Mathf.Min(count, samples.Length - start);
            for (int i = 0; i < n; i++) energy += samples[start + i] * samples[start + i];
            return n > 0 ? (float)System.Math.Sqrt(energy / n) : 0f;
        }

        [Test]
        public void SecondCardCarriesTheExactEventWordingAndTeamName()
        {
            Assert.AreEqual("NASA Space Apps Challenge · Nov 14–15 2026", StartupCardPresenter.EventLine);
            Assert.AreEqual("Polish Alex Goats", StartupCardPresenter.TeamName);
            var presenter = new StartupCardPresenter(false) { PlayAudio = false };
            Assert.AreEqual(StartupCardPresenter.EventLine, presenter.Q<Label>("startup-event-line").text);
            Assert.AreEqual(StartupCardPresenter.TeamName, presenter.Q<Label>("startup-team-name").text);
        }

        [Test]
        public void PresenterAdvancesFinishesAndSkipsAfterTheGraceWindow()
        {
            int finished = 0;
            var host = new VisualElement();
            var presenter = new StartupCardPresenter(true, null, null, () => finished++) { PlayAudio = false };
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

using System.Linq;
using NUnit.Framework;
using OutOfReach.Presentation.Radio;

namespace OutOfReach.Tests.Radio
{
    public sealed class RadioBeatTimelineTests
    {
        private static RadioBeat Beat(string id)
        {
            Assert.IsTrue(RadioBeats.TryGet(id, out RadioBeat beat), id);
            return beat;
        }

        private static void Run(RadioBeatTimeline t, float seconds, bool paused = false, float step = 1f / 60f)
        {
            for (float s = 0; s < seconds; s += step) t.Tick(step, paused);
        }

        [Test]
        public void AllFourStoryBeatsExistWithLines()
        {
            foreach (string id in new[] { RadioBeats.AuroraBoarding, RadioBeats.PadBoarding, RadioBeats.DistressCall, RadioBeats.EarthReplyDay14, RadioBeats.Day14Call })
                Assert.Greater(Beat(id).Lines.Count, 0, id);
            Assert.IsTrue(Beat(RadioBeats.EarthReplyDay14).Lines.Any(l => l.Text.Contains("day fourteen")), "Earth promises rescue on day 14");
            Assert.IsTrue(Beat(RadioBeats.Day14Call).Lines.Any(l => l.Text.Contains("console")), "Day-14 call sends the player to the console");
            Assert.IsTrue(Beat(RadioBeats.Day14Call).IncomingCall && Beat(RadioBeats.EarthReplyDay14).IncomingCall);
        }

        [Test]
        public void BoardingBeatIsAShortInsertedScene()
        {
            foreach (string id in new[] { RadioBeats.AuroraBoarding, RadioBeats.PadBoarding })
            {
                var t = new RadioBeatTimeline();
                t.Start(Beat(id), false);
                Assert.That(t.TotalSeconds, Is.InRange(8f, 12.5f), id + ": boarding radio check stays a short beat (~8-12 s)");
            }
        }

        [Test]
        public void LinesPlayInOrderWithPttAndLedForTransmitAndReceive()
        {
            var t = new RadioBeatTimeline();
            RadioBeat beat = Beat(RadioBeats.PadBoarding);
            t.Start(beat, false);
            Assert.AreEqual(RadioPhase.Raise, t.Phase);
            int seen = -1; bool sawTx = false, sawRx = false;
            for (int i = 0; i < 2000 && t.IsActive; i++)
            {
                t.Tick(1f / 60f, false);
                if (t.CurrentLine.HasValue)
                {
                    Assert.GreaterOrEqual(t.LineIndex, seen, "Lines never go backwards");
                    seen = t.LineIndex;
                    RadioLine line = t.CurrentLine.Value;
                    if (line.Kind == RadioLineKind.Transmit) { Assert.IsTrue(t.PttPressed); Assert.AreEqual(RadioLed.Transmit, t.Led); sawTx = true; }
                    else { Assert.IsFalse(t.PttPressed); Assert.AreEqual(RadioLed.Receive, t.Led); sawRx = true; }
                    Assert.AreEqual(1f, t.PoseWeight, 1e-5f, "Radio is fully raised while a line plays");
                }
            }
            Assert.AreEqual(beat.Lines.Count - 1, seen, "Every line was spoken");
            Assert.IsTrue(sawTx && sawRx);
            Assert.AreEqual(RadioPhase.Done, t.Phase);
            Assert.AreEqual(0f, t.PoseWeight);
        }

        [Test]
        public void PauseFreezesEverything()
        {
            var t = new RadioBeatTimeline();
            t.Start(Beat(RadioBeats.DistressCall), false);
            Run(t, 1.2f);
            float time = t.Time; RadioPhase phase = t.Phase; int line = t.LineIndex; float pose = t.PoseWeight;
            Run(t, 30f, paused: true);
            Assert.AreEqual(time, t.Time, 1e-6f);
            Assert.AreEqual(phase, t.Phase);
            Assert.AreEqual(line, t.LineIndex);
            Assert.AreEqual(pose, t.PoseWeight, 1e-6f);
            Assert.IsTrue(t.IsActive, "A paused beat neither ends nor advances");
        }

        [Test]
        public void SkipLowersFromCurrentHeightAndSpeaksNothingMore()
        {
            var t = new RadioBeatTimeline();
            t.Start(Beat(RadioBeats.EarthReplyDay14), false);
            Run(t, RadioBeatTimeline.IncomingSeconds + RadioBeatTimeline.RaiseSeconds + RadioBeatTimeline.GapSeconds + .5f);
            Assert.IsTrue(t.CurrentLine.HasValue);
            t.Skip();
            Assert.AreEqual(RadioPhase.Lower, t.Phase);
            Assert.IsFalse(t.CurrentLine.HasValue);
            Assert.IsTrue(t.Skipped);
            float last = t.PoseWeight;
            for (int i = 0; i < 200 && t.IsActive; i++)
            {
                t.Tick(1f / 60f, false);
                Assert.IsFalse(t.CurrentLine.HasValue, "No line after a skip");
                Assert.LessOrEqual(t.PoseWeight, last + 1e-6f, "Lowering never rises again");
                last = t.PoseWeight;
            }
            Assert.AreEqual(RadioPhase.Done, t.Phase);
        }

        [Test]
        public void SkipDuringRaiseHasNoPop()
        {
            var t = new RadioBeatTimeline();
            t.Start(Beat(RadioBeats.PadBoarding), false);
            Run(t, RadioBeatTimeline.RaiseSeconds * .4f);
            float before = t.PoseWeight;
            Assert.That(before, Is.InRange(.01f, .99f));
            t.Skip();
            Assert.AreEqual(before, t.PoseWeight, 1e-4f, "Lowering starts from the current height");
        }

        [Test]
        public void ReducedMotionShortensRaiseAndRemovesSway()
        {
            var full = new RadioBeatTimeline(); full.Start(Beat(RadioBeats.Day14Call), false);
            var reduced = new RadioBeatTimeline(); reduced.Start(Beat(RadioBeats.Day14Call), true);
            Assert.AreEqual(1f, full.SwayAmplitude);
            Assert.AreEqual(0f, reduced.SwayAmplitude);
            Assert.Less(reduced.TotalSeconds, full.TotalSeconds);
            Run(reduced, RadioBeatTimeline.IncomingSeconds + RadioBeatTimeline.ReducedRaiseSeconds + .01f);
            Assert.AreEqual(1f, reduced.PoseWeight, 1e-5f, "Reduced raise completes within its short window");
        }

        [Test]
        public void IncomingCallChirpsBeforeRaising()
        {
            var t = new RadioBeatTimeline();
            t.Start(Beat(RadioBeats.Day14Call), false);
            Assert.AreEqual(RadioPhase.Incoming, t.Phase);
            Assert.AreEqual(0f, t.PoseWeight);
            Run(t, RadioBeatTimeline.IncomingSeconds + .05f);
            Assert.AreEqual(RadioPhase.Raise, t.Phase);
        }

        [Test]
        public void Day14AnnouncementUsesTheApprovedVoiceCueAndNewDate()
        {
            RadioBeat beat = Beat(RadioBeats.Day14Announcement);
            Assert.AreEqual(RadioBeats.RescueLostCue, beat.Lines[0].CueId);
            StringAssert.Contains("mission day thirty-one", beat.Lines[0].Text);
            StringAssert.Contains("crashed", beat.Lines[0].Text);
            RadioBeat timed = beat.WithVoiceLengths(cue => cue == RadioBeats.RescueLostCue ? 17.5f : (float?)null);
            Assert.AreEqual(17.85f, timed.Lines[0].Seconds, 1e-4f, "A recorded line lasts as long as its clip");
            Assert.AreEqual(beat.Lines[0].Seconds, beat.WithVoiceLengths(_ => null).Lines[0].Seconds, "No clip: reading time");
        }

        [Test]
        public void LongLinesShowReadableChunksInOrder()
        {
            RadioLine line = Beat(RadioBeats.Day14Announcement).Lines[0].WithSeconds(18f);
            string first = line.SubtitleAt(0f), last = line.SubtitleAt(17.99f);
            Assert.AreNotEqual(first, last, "A long announcement is split");
            Assert.LessOrEqual(first.Length, 110);
            StringAssert.StartsWith("Aurora Seven... Mission Control.", first, "Ellipsis stays inside the sentence");
            StringAssert.EndsWith("We are not giving up on you.", last);
            RadioLine shortLine = Beat(RadioBeats.Day14Call).Lines[0];
            Assert.AreEqual(shortLine.Text, shortLine.SubtitleAt(1f), "Short lines are shown whole");
        }

        [Test]
        public void AbortEndsImmediately()
        {
            var t = new RadioBeatTimeline();
            t.Start(Beat(RadioBeats.DistressCall), false);
            Run(t, 2f);
            t.Abort();
            Assert.IsFalse(t.IsActive);
            Assert.AreEqual(0f, t.PoseWeight);
            Assert.IsFalse(t.CurrentLine.HasValue);
        }
    }
}

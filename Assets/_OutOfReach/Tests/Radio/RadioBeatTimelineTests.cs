using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
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
        public void PreflightBriefingIsASpokenPadWalkthroughForThePlanningPhase()
        {
            RadioBeat beat = Beat(RadioBeats.PreflightBriefing);
            Assert.IsTrue(beat.IncomingCall, "Pad calls the player");
            Assert.That(beat.Lines.Count, Is.InRange(4, 5));
            foreach (RadioLine line in beat.Lines)
            {
                Assert.AreEqual(RadioBeats.PadSpeaker, line.Speaker);
                Assert.AreEqual(RadioLineKind.Receive, line.Kind);
                Assert.That(line.Text.Length, Is.LessThanOrEqualTo(RadioLine.WholeLineCharacters), line.Text);
                Assert.Contains(line.CueId, RadioBeats.VoiceCues, "Every briefing line is recorded");
                string transcript = System.IO.File.ReadAllText(
                    "Assets/_OutOfReach/Scripts/Presentation/Radio/Voices/" + line.CueId + ".txt").Trim();
                Assert.AreEqual(transcript, line.Text, "The subtitle is exactly the transcript of the take");
            }
            string spoken = string.Join(" ", beat.Lines.Select(l => l.Text)).ToLowerInvariant();
            // Core's AuroraMission.MassLimitKg is 600 kg; the briefing names the same limit and the survival cargo.
            foreach (string topic in new[] { "food and water", "solar wing", "spare parts kit", "six hundred kilograms", "orbit" })
                StringAssert.Contains(topic, spoken);
            foreach (GamePhase phase in (GamePhase[])System.Enum.GetValues(typeof(GamePhase)))
                Assert.AreEqual(phase != GamePhase.Planning, RadioBeats.IsStale(RadioBeats.PreflightBriefing, phase), phase.ToString());
            Assert.AreEqual(RadioBeats.VoiceCues.Length, RadioBeats.VoiceCues.Distinct().Count(), "Cue ids are unique");
        }

        [Test]
        public void Day14AnnouncementUsesTheFreshTakeAndNamesNoDate()
        {
            RadioBeat beat = Beat(RadioBeats.Day14Announcement);
            Assert.AreEqual(RadioBeats.RescueLostCue, beat.Lines[0].CueId);
            Assert.AreEqual("vo40_mc_rescue_lost_day14", RadioBeats.RescueLostCue);
            StringAssert.Contains("crashed", beat.Lines[0].Text);
            StringAssert.Contains("A second craft is already in transit.", beat.Lines[0].Text);
            // Core owns the schedule (second rescue on day 30): the spoken line and its subtitle name no day.
            foreach (string dated in new[] { "thirty", "rendezvous", "day " })
                StringAssert.DoesNotContain(dated, beat.Lines[0].Text.ToLowerInvariant());
            // The subtitle is exactly the transcript of the recorded take.
            string transcript = System.IO.File.ReadAllText(
                "Assets/_OutOfReach/Scripts/Presentation/Radio/Voices/vo40_mc_rescue_lost_day14.txt").Trim();
            Assert.AreEqual(transcript, beat.Lines[0].Text);
            RadioBeat timed = beat.WithVoiceLengths(cue => cue == RadioBeats.RescueLostCue ? 17.5f : (float?)null);
            Assert.AreEqual(17.85f, timed.Lines[0].Seconds, 1e-4f, "A recorded line lasts as long as its clip");
            Assert.AreEqual(beat.Lines[0].Seconds, beat.WithVoiceLengths(_ => null).Lines[0].Seconds, "No clip: reading time");
        }

        [Test]
        public void BoardingCheckIsStaleOnceTheMissionMovedOn()
        {
            Assert.IsFalse(RadioBeats.IsStale(RadioBeats.AuroraBoarding, GamePhase.Intro), "Aboard, before the cockpit check");
            Assert.IsFalse(RadioBeats.IsStale(RadioBeats.PadBoarding, GamePhase.Planning), "On the pad");
            Assert.IsTrue(RadioBeats.IsStale(RadioBeats.PadBoarding, GamePhase.Intro), "The pad check never plays after liftoff");
            foreach (GamePhase later in new[] { GamePhase.Emergency, GamePhase.Repair, GamePhase.Eva, GamePhase.Transmission, GamePhase.Survival, GamePhase.Outcome, GamePhase.Report })
                Assert.IsTrue(RadioBeats.IsStale(RadioBeats.AuroraBoarding, later), "'We show you aboard' never plays in " + later);
            foreach (string id in new[] { RadioBeats.DistressCall, RadioBeats.EarthReplyDay14, RadioBeats.Day14Call, RadioBeats.Day14Announcement })
                foreach (GamePhase phase in (GamePhase[])System.Enum.GetValues(typeof(GamePhase)))
                    Assert.IsFalse(RadioBeats.IsStale(id, phase), id + " in " + phase);
        }

        [Test]
        public void AnnouncementBelongsToTheDayOfTheMessage()
        {
            const double received = 14 * 86400d;
            Assert.IsTrue(RadioBeats.IsAnnouncementFresh(received, received), "Right after the message");
            Assert.IsTrue(RadioBeats.IsAnnouncementFresh(received, received + 2 * 3600d), "Two hours later");
            Assert.IsTrue(RadioBeats.IsAnnouncementFresh(received, received + RadioBeats.AnnouncementWindowSeconds));
            Assert.IsFalse(RadioBeats.IsAnnouncementFresh(received, received + RadioBeats.AnnouncementWindowSeconds + 1d), "The next day it is old news");
            Assert.IsFalse(RadioBeats.IsAnnouncementFresh(received, 30 * 86400d), "Never at the day-30 rendezvous");
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
        public void SplitLinesStayClearOfTheChapterStrip()
        {
            // The chapter strip in the lower right covers the end of a HUD subtitle line longer than about 56
            // characters: every chunk of a split line fits, nothing is lost and the order is kept.
            foreach (string id in RadioBeats.Ids)
                foreach (RadioLine source in Beat(id).Lines)
                {
                    RadioLine line = source.WithSeconds(20f);
                    var shown = new System.Collections.Generic.List<string>();
                    for (float t = 0f; t <= 20f; t += .05f)
                    {
                        string chunk = line.SubtitleAt(t);
                        if (shown.Count == 0 || shown[shown.Count - 1] != chunk) shown.Add(chunk);
                    }
                    Assert.AreEqual(line.Text, string.Join(" ", shown), id + ": the chunks in order are the whole line");
                    if (line.Text.Length <= RadioLine.WholeLineCharacters) { Assert.AreEqual(1, shown.Count, id + ": shown whole"); continue; }
                    foreach (string chunk in shown)
                        Assert.LessOrEqual(chunk.Length, RadioLine.ChunkCharacters, id + ": '" + chunk + "'");
                }
            RadioLine announcement = Beat(RadioBeats.Day14Announcement).Lines[0].WithSeconds(18.33f);
            Assert.AreEqual("Aurora Seven... Mission Control.", announcement.SubtitleAt(0f));
            Assert.AreEqual("Conserve your supplies. We are not giving up on you.", announcement.SubtitleAt(18.3f));
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

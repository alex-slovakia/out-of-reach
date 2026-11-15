using NUnit.Framework;
using OutOfReach.Presentation.CinematicSkip;

namespace OutOfReach.Tests.CinematicSkip.EditMode
{
    public sealed class CinematicSkipHoldTests
    {
        private const float Frame = 1f / 60f;

        private static int Run(CinematicSkipHold hold, bool skippable, bool keyHeld, float seconds)
        {
            int fired = 0;
            for (float t = 0f; t < seconds; t += Frame)
                if (hold.Tick(skippable, keyHeld, Frame)) fired++;
            return fired;
        }

        private static CinematicSkipContext Departure() => new CinematicSkipContext { ModeChosen = true, DeparturePlaying = true };

        [Test]
        public void Hold_of_six_tenths_fires_exactly_once()
        {
            var hold = new CinematicSkipHold();
            hold.Tick(true, false, Frame);
            Assert.AreEqual(0, Run(hold, true, true, .55f), "0.55 s is not yet a skip");
            Assert.Greater(hold.Progress01, .8f);
            Assert.AreEqual(1, Run(hold, true, true, .1f), "0.6 s completes the hold");
        }

        [Test]
        public void Short_tap_never_skips()
        {
            var hold = new CinematicSkipHold();
            hold.Tick(true, false, Frame);
            for (int tap = 0; tap < 20; tap++)
            {
                Assert.AreEqual(0, Run(hold, true, true, .3f));
                Assert.AreEqual(0, Run(hold, true, false, .05f));
                Assert.AreEqual(0f, hold.Progress01, "releasing clears the progress");
            }
        }

        [Test]
        public void One_hold_skips_one_sequence_until_the_key_is_released()
        {
            var hold = new CinematicSkipHold();
            hold.Tick(true, false, Frame);
            Assert.AreEqual(1, Run(hold, true, true, 5f), "a long hold fires once, not every 0.6 s");
            Assert.AreEqual(0, Run(hold, true, false, .1f));
            Assert.AreEqual(1, Run(hold, true, true, .7f), "a fresh hold counts again");
        }

        [Test]
        public void Key_already_down_when_the_cinematic_starts_does_not_count()
        {
            var hold = new CinematicSkipHold();
            // Space held in gameplay (push-off) or through another sequence's own skip, then a cinematic starts.
            Assert.AreEqual(0, Run(hold, false, true, 1f));
            Assert.AreEqual(0, Run(hold, true, true, 3f), "the same hold never skips the new cinematic");
            Assert.IsFalse(hold.IsArmed);
            Assert.AreEqual(0, Run(hold, true, false, .05f));
            Assert.IsTrue(hold.IsArmed);
            Assert.AreEqual(1, Run(hold, true, true, .7f));
        }

        [Test]
        public void Leaving_the_skippable_window_drops_the_hold()
        {
            var hold = new CinematicSkipHold();
            hold.Tick(true, false, Frame);
            Assert.AreEqual(0, Run(hold, true, true, .5f));
            Assert.AreEqual(0, Run(hold, false, true, .5f), "pause or the end of the cinematic: nothing fires");
            Assert.AreEqual(0f, hold.Progress01);
            Assert.AreEqual(0, Run(hold, true, true, 2f), "resuming with the key still down needs a release first");
        }

        [Test]
        public void Stalled_or_invalid_frames_cannot_complete_a_hold()
        {
            var hold = new CinematicSkipHold();
            hold.Tick(true, false, Frame);
            Assert.IsFalse(hold.Tick(true, true, 30f), "one long frame counts as 0.1 s at most");
            Assert.AreEqual(CinematicSkipHold.MaximumStepSeconds, hold.HeldSeconds, 1e-6f);
            Assert.IsFalse(hold.Tick(true, true, float.NaN));
            Assert.IsFalse(hold.Tick(true, true, float.PositiveInfinity));
            Assert.IsFalse(hold.Tick(true, true, -1f));
            Assert.IsFalse(hold.Tick(true, true, 0f));
            Assert.AreEqual(CinematicSkipHold.MaximumStepSeconds, hold.HeldSeconds, 1e-6f);
        }

        [Test]
        public void Hold_length_matches_the_rescue_ending()
        {
            Assert.AreEqual(.6f, CinematicSkipHold.DefaultHoldSeconds);
            Assert.AreEqual(.6f, new CinematicSkipHold(float.NaN).HoldSeconds);
            Assert.AreEqual(.6f, new CinematicSkipHold(-1f).HoldSeconds);
        }

        [Test]
        public void Departure_with_locked_control_is_skippable()
        {
            Assert.AreEqual(CinematicSkipTarget.Departure, CinematicSkipHold.Resolve(Departure()));
        }

        [Test]
        public void Gameplay_pause_menu_cards_and_rescue_ending_are_never_skippable()
        {
            CinematicSkipContext gameplay = Departure();
            gameplay.ControlAllowed = true;
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(gameplay), "Space is push-off in gameplay");

            CinematicSkipContext paused = Departure();
            paused.Paused = true;
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(paused));

            CinematicSkipContext cards = Departure();
            cards.ModeChosen = false;
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(cards));

            CinematicSkipContext rescue = Departure();
            rescue.RescueEndingPlaying = true;
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(rescue), "the rescue ending owns its own skip");
        }

        [Test]
        public void Nothing_playing_is_never_skippable()
        {
            var locked = new CinematicSkipContext { ModeChosen = true };
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(locked), "locked control alone is not a cinematic");
            Assert.AreEqual(CinematicSkipTarget.None, CinematicSkipHold.Resolve(default));
        }
    }
}

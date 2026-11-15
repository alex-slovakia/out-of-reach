using System.Collections;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Presentation.Audio;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.AudioOpening
{
    /// <summary>
    /// AUDIO-OPENING: music ownership from loading through the departure, the NASA launch radio and their pause, skip,
    /// restart and phase behaviour. Real AudioSources with silent synthetic clips; times are real (unscaled) seconds.
    /// </summary>
    public sealed class OpeningMusicPlayTests
    {
        private const float Base = 1f * .8f * .65f; // master * music * departure trim (presenter defaults)
        private GameObject host;
        private MissionAudioPresenter audio;
        private AudioClip score, radio, isolation, tension, emergency;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            score = Clip("score", 86f);
            radio = Clip("radio", 17.66f);
            isolation = Clip("isolation", 12f);
            tension = Clip("tension", 12f);
            emergency = Clip("emergency", 12f);
            host = new GameObject("OpeningMusicPlayTests audio");
            audio = host.AddComponent<MissionAudioPresenter>();
            audio.ConfigureClips(isolation, tension, emergency, null, null);
            audio.ConfigureLaunchRadio(radio);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host) Object.Destroy(host);
            foreach (AudioClip clip in new[] { score, radio, isolation, tension, emergency }) if (clip) Object.Destroy(clip);
            yield return null;
        }

        [Test]
        public void RadioCaptionsFollowTheMeasuredCountdownInsideTheClip()
        {
            Assert.That(LaunchRadioCountdown.CaptionAt(.06f), Is.EqualTo("Ignition sequence start."));
            Assert.That(LaunchRadioCountdown.CaptionAt(LaunchRadioCountdown.ZeroRadioSeconds + .1f), Is.EqualTo("Zero."));
            Assert.That(LaunchRadioCountdown.CaptionAt(11.5f), Is.EqualTo("Liftoff!"));
            Assert.That(LaunchRadioCountdown.CaptionAt(14.0f), Is.Empty, "No caption in the gap between the two transmissions.");
            Assert.That(LaunchRadioCountdown.CaptionAt(17.6f), Is.Empty);
            Assert.That(LaunchRadioCountdown.CaptionAt(float.NaN), Is.Empty);
            // Zero lands 0.1 s before the vehicle moves (TransportDepartureTimeline.LiftoffSeconds = 10.5).
            Assert.That(LaunchRadioCountdown.DepartureStartSeconds + LaunchRadioCountdown.ZeroRadioSeconds, Is.EqualTo(10.40f).Within(.011f));
            for (int i = 0; i < LaunchRadioCountdown.CaptionCount; i++)
            {
                Assert.That(LaunchRadioCountdown.CaptionEnd(i), Is.GreaterThan(LaunchRadioCountdown.CaptionStart(i)));
                Assert.That(LaunchRadioCountdown.CaptionEnd(i), Is.LessThanOrEqualTo(17.66f));
                if (i > 0) Assert.That(LaunchRadioCountdown.CaptionStart(i), Is.GreaterThanOrEqualTo(LaunchRadioCountdown.CaptionEnd(i - 1)));
            }
        }

        [UnityTest]
        public IEnumerator MenuMoodStopsAndLoadingStartsTheScoreFromZero()
        {
            Assert.IsTrue(audio.SetMood(MusicMood.Isolation, 0f));
            yield return Seconds(.2f);
            Assert.That(Source("Score Isolation").volume, Is.GreaterThan(0f), "Precondition: a mood track is audible.");
            Assert.IsTrue(audio.BeginOpeningLoading(score), audio.LastRejection);
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Loading));
            yield return Seconds(.8f);
            Assert.That(audio.MoodGate, Is.EqualTo(0f), "The opening silences mood tracks.");
            Assert.That(Source("Score Isolation").volume, Is.EqualTo(0f));
            Assert.That(audio.OpeningScoreSeconds, Is.InRange(.3f, 1.4f), "Loading score starts at 0 s.");
            Assert.That(audio.OpeningScoreLevel, Is.GreaterThan(0f).And.LessThan(Base), "Still in its 2 s fade-in.");
            float before = audio.OpeningScoreSeconds;
            Assert.IsTrue(audio.BeginOpeningLoading(score), "Repeated loading request is accepted ...");
            yield return Seconds(.2f);
            Assert.That(audio.OpeningScoreSeconds, Is.GreaterThan(before), "... and never restarts the score.");
            Assert.IsTrue(audio.SetMood(MusicMood.Tension, 0f), "A preflight mood request is accepted ...");
            yield return Seconds(.2f);
            Assert.That(Source("Score Tension").volume, Is.EqualTo(0f), "... but never heard over the opening.");
        }

        [UnityTest]
        public IEnumerator LoadingRestsBeforeTheLaunchSectionWithoutRevealingAMood()
        {
            Assert.IsTrue(audio.BeginOpeningLoading(score), audio.LastRejection);
            yield return Seconds(2.1f);
            Assert.That(audio.OpeningScoreLevel, Is.EqualTo(Base).Within(.01f), "Fade-in complete.");
            Source("Opening score A").time = MissionAudioPresenter.OpeningLoadingRestSeconds - .2f;
            audio.SetMood(MusicMood.Isolation);
            yield return Seconds(1.4f);
            Assert.That(audio.OpeningScoreLevel, Is.GreaterThan(0f).And.LessThan(Base * .9f), "Resting fade is under way.");
            yield return Seconds(1.6f);
            Assert.IsFalse(Source("Opening score A").isPlaying, "The loading score stops before 49.9 s.");
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Loading), "Silence is still owned by the opening.");
            Assert.That(Source("Score Isolation").volume, Is.EqualTo(0f), "No mood track fills the rest.");
        }

        [UnityTest]
        public IEnumerator DepartureEntersOnTheDownbeatFollowsThePlayheadAndStartsTheRadio()
        {
            Assert.IsTrue(audio.BeginOpeningLoading(score), audio.LastRejection);
            yield return Seconds(2.2f);
            float loadingPosition = audio.OpeningScoreSeconds;
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Departure));
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(MissionAudioPresenter.OpeningDepartureScoreSeconds).Within(.05f));
            AudioSource outgoing = Source("Opening score A");
            Assert.That(outgoing.time, Is.GreaterThanOrEqualTo(loadingPosition), "The loading channel keeps its position while fading.");
            float start = Time.realtimeSinceStartup;
            float playhead = 0f;
            while (playhead < 2.6f)
            {
                playhead = Time.realtimeSinceStartup - start;
                audio.TrackOpeningDeparture(playhead);
                yield return null;
            }
            Assert.IsFalse(outgoing.isPlaying, "The loading position faded out within 1.5 s.");
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(MissionAudioPresenter.OpeningDepartureScoreSeconds + playhead).Within(.25f));
            Assert.That(audio.OpeningDriftCorrections, Is.EqualTo(0), "Normal frames need no correction.");
            Assert.IsTrue(audio.IsLaunchRadioPlaying, "Radio starts at departure 1.82 s.");
            Assert.IsTrue(audio.IsDialoguePlaying, "Queued dialogue waits for the radio.");
            AudioSource radioSource = Source("Launch radio countdown");
            Assert.That(radioSource.clip, Is.SameAs(radio));
            Assert.That(radioSource.time, Is.EqualTo(playhead - LaunchRadioCountdown.DepartureStartSeconds).Within(.25f));
            Assert.That(audio.CurrentSubtitle.CueId, Is.EqualTo(LaunchRadioCountdown.CueId));
            Assert.IsTrue(audio.CurrentSubtitle.IsVisible);
            Assert.That(audio.CurrentSubtitle.Text, Is.EqualTo("Ignition sequence start."));
            Assert.That(audio.OpeningScoreLevel, Is.EqualTo(Base * Mathf.Pow(10f, -5f / 20f)).Within(.03f), "Score ducks -5 dB under the radio.");
        }

        [UnityTest]
        public IEnumerator RadioStartsLateIntoTheClipAfterAStallAndDriftIsCorrected()
        {
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            yield return null;
            audio.TrackOpeningDeparture(4.0f); // a long stall: the first tracked frame is already past the radio start
            yield return null;
            AudioSource radioSource = Source("Launch radio countdown");
            Assert.That(radioSource.time, Is.EqualTo(4.0f - LaunchRadioCountdown.DepartureStartSeconds).Within(.1f), "Radio starts at the playhead.");
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(MissionAudioPresenter.OpeningDepartureScoreSeconds + 4f).Within(.1f), "Score jumps to the playhead.");
            int corrections = audio.OpeningDriftCorrections;
            Assert.That(corrections, Is.EqualTo(1), "One correction for the stalled score.");
            yield return Seconds(.6f);
            audio.TrackOpeningDeparture(4.0f); // picture held while audio ran on 0.6 s
            Assert.That(audio.OpeningDriftCorrections, Is.GreaterThan(corrections));
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(MissionAudioPresenter.OpeningDepartureScoreSeconds + 4f).Within(.1f));
        }

        [UnityTest]
        public IEnumerator PauseFreezesScoreRadioCaptionsAndPlayheadTracking()
        {
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            audio.TrackOpeningDeparture(2.5f);
            yield return Seconds(.3f);
            audio.SetPresentationPaused(true);
            float scoreAt = audio.OpeningScoreSeconds;
            float radioAt = Source("Launch radio countdown").time;
            string caption = audio.CurrentSubtitle.Text;
            int corrections = audio.OpeningDriftCorrections;
            yield return Seconds(.6f);
            audio.TrackOpeningDeparture(9f); // ignored while paused
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(scoreAt).Within(.02f));
            Assert.That(Source("Launch radio countdown").time, Is.EqualTo(radioAt).Within(.02f));
            Assert.That(audio.CurrentSubtitle.Text, Is.EqualTo(caption));
            Assert.That(audio.OpeningDriftCorrections, Is.EqualTo(corrections), "No catch-up while paused.");
            Assert.IsFalse(audio.BeginOpeningDeparture(score, 34f), "Nothing starts while paused.");
            audio.SetPresentationPaused(false);
            yield return Seconds(.3f);
            Assert.That(audio.OpeningScoreSeconds, Is.GreaterThan(scoreAt + .15f), "Resumes where it paused.");
            Assert.IsTrue(audio.IsLaunchRadioPlaying);
        }

        [UnityTest]
        public IEnumerator SkipStopsTheRadioFadesTheScoreAndHoldsSilenceUntilReleased()
        {
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            audio.TrackOpeningDeparture(6f);
            audio.SetMood(MusicMood.Isolation, 0f); // the Intro phase requests its mood during the departure
            yield return Seconds(.2f);
            Assert.IsTrue(audio.IsLaunchRadioPlaying);
            audio.EndOpeningDeparture(MissionAudioPresenter.OpeningSkipFadeSeconds);
            Assert.IsFalse(audio.IsLaunchRadioPlaying, "Radio stops at once on skip.");
            Assert.IsFalse(audio.CurrentSubtitle.IsVisible, "Radio caption is hidden.");
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Held));
            yield return Seconds(.8f);
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(-1f), "Score faded out and stopped.");
            yield return Seconds(1.5f);
            Assert.That(audio.MoodGate, Is.EqualTo(0f), "No mood returns after the departure (no elevator music).");
            Assert.That(Source("Score Isolation").volume, Is.EqualTo(0f));
            audio.ReleaseOpeningMusic(.25f);
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Idle));
            yield return Seconds(.1f);
            Assert.That(audio.MoodGate, Is.GreaterThan(0f).And.LessThan(1f), "The next phase's mood fades in ...");
            yield return Seconds(.3f);
            Assert.That(audio.MoodGate, Is.EqualTo(1f), "... over the release time.");
        }

        [UnityTest]
        public IEnumerator CompletedDepartureFadesOverTheLastShotAndNeverRestarts()
        {
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            audio.TrackOpeningDeparture(25f);
            yield return Seconds(.1f);
            Assert.That(audio.OpeningScoreLevel, Is.EqualTo(Base).Within(.02f), "Radio is over: full level.");
            Assert.IsFalse(audio.IsLaunchRadioPlaying);
            audio.TrackOpeningDeparture(32.25f);
            Assert.That(audio.OpeningScoreLevel, Is.LessThan(Base * .7f).And.GreaterThan(0f), "End fade in progress.");
            audio.TrackOpeningDeparture(34f);
            Assert.That(audio.OpeningScoreLevel, Is.EqualTo(0f).Within(.001f));
            audio.EndOpeningDeparture(.5f);
            yield return Seconds(.7f);
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(-1f));
            audio.TrackOpeningDeparture(10f); // late tracking after the end must not restart anything
            yield return null;
            Assert.That(audio.OpeningScoreSeconds, Is.EqualTo(-1f));
            Assert.IsFalse(audio.IsLaunchRadioPlaying);
            Assert.IsFalse(audio.BeginOpeningLoading(score), "The opening never restarts after its launch.");
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Held));
        }

        [UnityTest]
        public IEnumerator RestartStopsEveryOpeningChannelAndTheNextLoadingStartsAtZero()
        {
            Assert.IsTrue(audio.BeginOpeningLoading(score), audio.LastRejection);
            yield return Seconds(.3f);
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            audio.TrackOpeningDeparture(3f);
            yield return Seconds(.2f);
            audio.StopAllPresentation();
            Assert.That(audio.OpeningState, Is.EqualTo(OpeningMusicState.Idle));
            Assert.IsFalse(audio.IsLaunchRadioPlaying);
            Assert.That(host.GetComponentsInChildren<AudioSource>(true).Count(s => s.isPlaying), Is.EqualTo(0), "Nothing keeps playing.");
            Assert.IsFalse(audio.CurrentSubtitle.IsVisible);
            yield return Seconds(.5f);
            Assert.That(host.GetComponentsInChildren<AudioSource>(true).Count(s => s.isPlaying), Is.EqualTo(0), "Menu after restart stays silent.");
            Assert.IsTrue(audio.BeginOpeningLoading(score), audio.LastRejection);
            yield return Seconds(.2f);
            Assert.That(audio.OpeningScoreSeconds, Is.LessThan(.6f), "A new loading starts the score from 0 s.");
        }

        [UnityTest]
        public IEnumerator DialogueKeepsPriorityOverRadioCaptionsAndBothDuck()
        {
            AudioClip line = Clip("line", .8f);
            try
            {
                Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
                audio.TrackOpeningDeparture(2.5f);
                yield return Seconds(.2f);
                Assert.That(audio.CurrentSubtitle.CueId, Is.EqualTo(LaunchRadioCountdown.CueId));
                Assert.IsTrue(audio.PlayDialogue(line, "Test line.", "test-line"));
                Assert.That(audio.CurrentSubtitle.CueId, Is.EqualTo("test-line"), "A dialogue line owns the subtitle line.");
                yield return Seconds(.4f);
                Assert.That(audio.OpeningScoreLevel, Is.EqualTo(Base * Mathf.Pow(10f, -8f / 20f)).Within(.03f), "Deeper duck wins.");
                yield return Seconds(.8f);
                Assert.That(audio.CurrentSubtitle.CueId, Is.EqualTo(LaunchRadioCountdown.CueId), "Radio caption resumes after the line.");
                Assert.IsTrue(audio.CurrentSubtitle.IsVisible);
            }
            finally { Object.Destroy(line); }
        }

        [UnityTest]
        public IEnumerator DepartureWithoutARadioClipStillOwnsTheScore()
        {
            audio.ConfigureLaunchRadio(null);
            Assert.IsTrue(audio.BeginOpeningDeparture(score, 34f), audio.LastRejection);
            audio.TrackOpeningDeparture(5f);
            yield return Seconds(.2f);
            Assert.IsFalse(audio.IsLaunchRadioPlaying);
            Assert.That(audio.OpeningScoreLevel, Is.EqualTo(Base).Within(.02f));
            AudioClip shortScore = Clip("short", 20f);
            try { Assert.IsFalse(audio.BeginOpeningDeparture(shortScore, 34f), "A score shorter than its departure position is rejected."); }
            finally { Object.Destroy(shortScore); }
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private AudioSource Source(string name)
        {
            AudioSource match = host.GetComponentsInChildren<AudioSource>(true).FirstOrDefault(s => s.name == name);
            Assert.IsNotNull(match, "Owned audio channel not found: " + name);
            return match;
        }

        private static AudioClip Clip(string name, float seconds)
        {
            const int rate = 8000;
            AudioClip clip = AudioClip.Create(name, Mathf.CeilToInt(seconds * rate), 1, rate, false);
            clip.SetData(new float[clip.samples], 0);
            return clip;
        }
    }
}

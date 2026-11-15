using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Presentation.Rescue;
using UnityEngine;
using UnityEngine.TestTools;
using T = OutOfReach.Presentation.Rescue.RescueEndingTimeline;

namespace OutOfReach.Tests.Rescue.PlayMode
{
    public sealed class RescueEndingPresenterPlayTests
    {
        private sealed class FakeState : IGameState
        {
            public GamePhase Phase { get; set; } = GamePhase.Outcome;
            public MissionOutcome Outcome { get; set; } = MissionOutcome.Recovered;
            public double ElapsedSimulationSeconds { get; set; }
            public CommandResult TryTransition(GamePhase next) { Phase = next; return new CommandResult(true, CommandFailure.None, "ok"); }
            public void AdvanceTime(double simulationSeconds) => ElapsedSimulationSeconds += simulationSeconds;
        }

        private sealed class FakeClock : ISimulationClock
        {
            public bool IsPaused { get; set; }
            public double TimeScale => 1d;
            public double ElapsedSimulationSeconds => 0d;
        }

        private readonly List<GameObject> owned = new List<GameObject>();
        private MissionEventBus bus;
        private FakeState state;
        private FakeClock clock;
        private RescueEndingPresenter presenter;
        private Transform pivot, rig;
        private Camera camera;
        private GameObject visuals;
        private Transform docked;
        private AudioSource voice;
        private AudioClip approachClip, dockedClip;
        private readonly List<CinematicFinished> finished = new List<CinematicFinished>();
        private readonly List<CinematicRequested> downstream = new List<CinematicRequested>();

        [SetUp]
        public void SetUp()
        {
            bus = new MissionEventBus();
            state = new FakeState();
            clock = new FakeClock();
            finished.Clear(); downstream.Clear();
            bus.CinematicFinished += message => finished.Add(message);

            rig = New("Player rig").transform;
            rig.position = new Vector3(0f, 1.6f, 8f);
            pivot = New("Cinematic pivot", rig).transform;
            camera = New("Camera", pivot).AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0f, .05f, 0f);
            camera.fieldOfView = 70f;

            GameObject root = New("Rescue ending");
            presenter = root.AddComponent<RescueEndingPresenter>();
            visuals = New("Visuals", root.transform);
            var craftObject = New("Craft", visuals.transform);
            var craft = craftObject.AddComponent<RescueCraftV01>();
            visuals.SetActive(false);
            Transform Marker(string name, Vector3 p, Vector3 forward)
            {
                Transform m = New(name, root.transform).transform;
                m.SetPositionAndRotation(p, Quaternion.LookRotation(forward));
                return m;
            }
            docked = Marker("Docked", new Vector3(0, 1.45f, 30.67f), Vector3.back);
            voice = New("Voice", root.transform).AddComponent<AudioSource>();
            var sfx = New("Sfx", root.transform).AddComponent<AudioSource>();
            approachClip = AudioClip.Create("vo24a-test", 2400, 1, 24000, false);
            dockedClip = AudioClip.Create("vo24b-test", 2400, 1, 24000, false);
            Set("cameraPivot", pivot); Set("viewCamera", camera);
            Set("windowView", Marker("Window", new Vector3(.86f, 2.2f, -2.42f), Vector3.back));
            Set("dockingCameraView", Marker("DockCam", new Vector3(0, 1.45f, 21.3f), Vector3.forward));
            Set("hatchView", Marker("Hatch", new Vector3(0, 1.62f, 17.6f), Vector3.forward));
            Set("visualSet", visuals); Set("craft", craft);
            Set("craftFar", Marker("Far", new Vector3(16, 9, -260), Vector3.forward));
            Set("craftNear", Marker("Near", new Vector3(2.6f, 2f, -15.5f), Vector3.forward));
            Set("craftAside", Marker("Aside", new Vector3(16, 3.5f, -2), Vector3.left));
            Set("craftDockStart", Marker("DockStart", new Vector3(.55f, 1.95f, 54.67f), Vector3.back));
            Set("craftDocked", docked);
            Set("voiceSource", voice); Set("sfxSource", sfx);
            Set("approachVoice", approachClip); Set("dockedVoice", dockedClip);
            presenter.Configure(bus, state, clock);
            filtered = presenter.FilterFor(bus);
            filtered.CinematicRequested += request => downstream.Add(request);
        }

        private IMissionEventBus filtered;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in owned) if (go) Object.DestroyImmediate(go);
            owned.Clear();
            if (approachClip) Object.DestroyImmediate(approachClip);
            if (dockedClip) Object.DestroyImmediate(dockedClip);
        }

        private GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false); else owned.Add(go);
            return go;
        }

        private void Set(string field, Object value)
        {
            FieldInfo info = typeof(RescueEndingPresenter).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(info, field);
            info.SetValue(presenter, value);
        }

        private static int nextSequence = 100000;
        private static CinematicRequested Request(CinematicId id) => new CinematicRequested(id, ++nextSequence);

        [UnityTest]
        public IEnumerator RecoveryArrival_IsTakenOver_PlaysThroughTheShots_AndIsAcknowledgedOnce()
        {
            Assert.IsTrue(presenter.HasValidViews);
            Vector3 pivotLocal = pivot.localPosition;
            CinematicRequested request = Request(CinematicId.RecoveryArrival);
            bus.Publish(request);
            Assert.IsTrue(presenter.IsPlaying);
            Assert.AreEqual(request.SequenceId, presenter.ActiveSequenceId);
            Assert.IsEmpty(downstream, "the existing presenter never sees a taken-over RecoveryArrival");
            Assert.IsTrue(visuals.activeSelf);
            yield return null;
            presenter.Advance(T.Cut1 + .5f - presenter.ElapsedSeconds);
            Assert.AreEqual(RescueShot.DockingCamera, presenter.CurrentShot);
            Assert.That(Vector3.Distance(camera.transform.position, new Vector3(0, 1.45f, 21.3f)), Is.LessThan(.06f), "camera at the docking camera");
            presenter.Advance(T.Cut2 + .8f - presenter.ElapsedSeconds);
            Assert.AreEqual(RescueShot.Hatch, presenter.CurrentShot);
            Assert.IsEmpty(finished);
            presenter.Advance(T.Duration);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, finished.Count);
            Assert.AreEqual(request.SequenceId, finished[0].SequenceId);
            Assert.AreEqual(CinematicId.RecoveryArrival, finished[0].Cinematic);
            Assert.IsFalse(finished[0].WasSkipped);
            Assert.AreEqual(pivotLocal, pivot.localPosition, "pivot restored");
            Assert.AreEqual(70f, camera.fieldOfView, 1e-4f);
            Assert.IsFalse(visuals.activeSelf, "owned visuals hidden again");
            Assert.AreEqual(string.Empty, presenter.CurrentCaption);
            bus.Publish(request);   // stale replay
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, finished.Count);
        }

        [UnityTest]
        public IEnumerator DockedVoice_StartsOnlyAfterTheVisibleContact()
        {
            bus.Publish(Request(CinematicId.RecoveryArrival));
            presenter.Advance(T.VoiceApproachStart + .1f);
            Assert.AreSame(approachClip, voice.clip, "VO24a with the approach");
            presenter.Advance(T.Contact - .1f - presenter.ElapsedSeconds);
            Assert.AreSame(approachClip, voice.clip, "VO24b not before contact");
            presenter.Advance(.15f);
            Transform craft = ((RescueCraftV01)typeof(RescueEndingPresenter).GetField("craft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(presenter)).transform;
            Assert.That(Vector3.Distance(craft.position, docked.position), Is.LessThan(.06f), "the craft is visibly at the hatch");
            presenter.Advance(T.VoiceDockedStart + .05f - presenter.ElapsedSeconds);
            Assert.AreSame(dockedClip, voice.clip, "VO24b right after the contact");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Skip_AcknowledgesOnceAsSkipped_AndRestores()
        {
            Vector3 pivotLocal = pivot.localPosition;
            bus.Publish(Request(CinematicId.RecoveryArrival));
            presenter.Advance(5f);
            Assert.AreNotEqual(pivotLocal, pivot.localPosition);
            Assert.IsTrue(presenter.SkipActive());
            Assert.IsFalse(presenter.SkipActive());
            Assert.AreEqual(1, finished.Count);
            Assert.IsTrue(finished[0].WasSkipped);
            Assert.AreEqual(pivotLocal, pivot.localPosition);
            Assert.IsFalse(visuals.activeSelf);
            Assert.IsFalse(voice.isPlaying);
            yield return null;
            Assert.AreEqual(1, finished.Count);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesThePlayhead()
        {
            bus.Publish(Request(CinematicId.RecoveryArrival));
            presenter.Advance(3f);
            float at = presenter.ElapsedSeconds;
            clock.IsPaused = true;
            presenter.Advance(10f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(at, presenter.ElapsedSeconds, 1e-5f);
            Assert.IsTrue(presenter.IsPlaying);
            clock.IsPaused = false;
            yield return null;
            yield return null;
            Assert.Greater(presenter.ElapsedSeconds, at);
        }

        [UnityTest]
        public IEnumerator LostMission_NeverPlays_AndTheRequestPassesThrough()
        {
            state.Outcome = MissionOutcome.Lost;
            CinematicRequested request = Request(CinematicId.RecoveryArrival);
            bus.Publish(request);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, downstream.Count);
            Assert.IsEmpty(finished, "the rescue presenter does not answer a request it did not take");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OtherCinematics_PassThrough_AndALostOutcomeMidSequenceEndsIt()
        {
            bus.Publish(Request(CinematicId.MarsDeparture));
            Assert.AreEqual(1, downstream.Count);
            Assert.IsFalse(presenter.IsPlaying);
            bus.Publish(Request(CinematicId.RecoveryArrival));
            Assert.IsTrue(presenter.IsPlaying);
            state.Outcome = MissionOutcome.Lost;
            yield return null;
            yield return null;
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, finished.Count);
            Assert.IsTrue(finished[0].WasSkipped);
        }

        [UnityTest]
        public IEnumerator MissingViews_FailOpen_ToTheExistingPresenter()
        {
            Set("hatchView", null);
            Assert.IsFalse(presenter.HasValidViews);
            bus.Publish(Request(CinematicId.RecoveryArrival));
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, downstream.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReducedEffects_HaveNoShake_AtTheHardDock()
        {
            presenter.ReducedEffects = true;
            bus.Publish(Request(CinematicId.RecoveryArrival));
            presenter.Advance(T.HardDock + .05f);
            Vector3 a = camera.transform.position;
            presenter.Advance(.1f);
            Vector3 b = camera.transform.position;
            Assert.That(Vector3.Distance(a, new Vector3(0, 1.45f, 21.3f)), Is.LessThan(.06f));
            Assert.AreEqual(a, b, "no shake with reduced effects");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Release_AcknowledgesAnActiveSequenceOnce()
        {
            bus.Publish(Request(CinematicId.RecoveryArrival));
            presenter.Release();
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(1, finished.Count);
            Assert.IsTrue(finished[0].WasSkipped);
            presenter.Release();
            Assert.AreEqual(1, finished.Count);
            yield return null;
        }
    }
}

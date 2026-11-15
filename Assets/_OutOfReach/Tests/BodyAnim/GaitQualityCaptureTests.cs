using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using OutOfReach.Composition.Editor;
using OutOfReach.Contracts;
using OutOfReach.Presentation.Motion;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OutOfReach.Tests.BodyAnim
{
    /// <summary>
    /// R03 walk quality on the real WORK/EVA body (I06BodyAuthoring): pelvis bounce, knee bend, planted-foot slide
    /// and stride for forward, backward and sideways walking, with an uneven frame time like a real game loop.
    /// With OOR_BODYANIM_OUT set it writes a measurement table and first-person / side frames (needs a GPU).
    /// </summary>
    public sealed class GaitQualityCaptureTests
    {
        private sealed class Suit : IEvaStatus
        {
            public bool On;
            public bool IsSuitOn => On;
            public bool IsSuitSealed => On;
            public double SuitOxygenKg => 1;
            public double SuitOxygenCapacityKg => 1;
            public double SuitOxygenSecondsRemaining => 1000;
            public bool IsOutside => false;
            public AirlockState Airlock => AirlockState.InnerOpen;
            public double AirlockTransitionRemainingSeconds => 0;
            public event Action Changed;
            public void Raise() => Changed?.Invoke();
        }

        private sealed class Clock : ISimulationClock
        {
            public bool IsPaused => false;
            public double TimeScale => 1;
            public double ElapsedSimulationSeconds => 0;
        }

        private sealed class Stage
        {
            public readonly Suit Suit = new Suit();
            public Transform Player;
            public Camera Eye, Side;
            public EmbodiedBodyPresenter Body;
            public bool Sprint;
        }

        private static string OutDir => Environment.GetEnvironmentVariable("OOR_BODYANIM_OUT");

        private static Stage Build(bool eva)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var s = new Stage();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -.5f, 0f); ground.transform.localScale = new Vector3(80f, 1f, 80f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var player = new GameObject("Player");
            s.Player = player.transform;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .28f; controller.center = new Vector3(0f, .9f, 0f);
            var eye = new GameObject("Eye"); eye.transform.SetParent(player.transform, false); eye.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            s.Eye = eye.AddComponent<Camera>(); s.Eye.fieldOfView = 75f; s.Eye.nearClipPlane = .05f; s.Eye.enabled = false;
            s.Side = new GameObject("Side").AddComponent<Camera>(); s.Side.fieldOfView = 28f; s.Side.enabled = false;
            s.Body = I06BodyAuthoring.Create(new GameObject("Presentation").transform, player.transform, s.Eye);
            foreach (SkinnedMeshRenderer r in s.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            { r.forceMatrixRecalculationPerRender = true; r.updateWhenOffscreen = true; }
            s.Body.ConfigureSession(s.Suit, new Clock(), () => true, () => false, () => EmbodiedBodyPresenter.BodyArmVisibility.Both);
            s.Body.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing, new Vector3(0f, 0f, -.285f));
            s.Body.ConfigureGroundLocomotion(controller, () => true, () => s.Sprint);
            s.Suit.On = eva; s.Suit.Raise();
            for (int i = 0; i < 6; i++) s.Body.AdvancePresentation(1f / 60f);
            return s;
        }

        // Uneven frame times (about 35-145 fps) like a real game loop; the same sequence every run.
        private static float FrameTime(int i) => 1f / (90f + 40f * Mathf.Sin(i * 1.7f) + 15f * Mathf.Sin(i * .37f));

        private static Transform Bone(Stage s, string name) => s.Body.GetVisibleBone(name);

        private sealed class Stats
        {
            public float HipMin = float.MaxValue, HipMax = float.MinValue, KneeMax, Slide, SlideRun;
            public int Planted; public float Cycles, Seconds;
            private bool had; private Vector3 last; private float lastPhase = float.NaN;
            public void Sample(Stage s, ModelGaitRig rig, float dt)
            {
                float phase = s.Body.GaitPhase;
                if (!float.IsNaN(phase) && !float.IsNaN(lastPhase)) Cycles += Mathf.Repeat(phase - lastPhase, 1f);
                lastPhase = phase; Seconds += dt;
                float hip = Bone(s, "root").position.y;
                HipMin = Mathf.Min(HipMin, hip); HipMax = Mathf.Max(HipMax, hip);
                foreach (string side in new[] { "L", "R" })
                {
                    Vector3 h = Bone(s, "upperleg01." + side).position, k = Bone(s, "lowerleg01." + side).position, a = Bone(s, "foot." + side).position;
                    KneeMax = Mathf.Max(KneeMax, Vector3.Angle(k - h, a - k));
                }
                bool planted = rig.IsPlanted(true) && Mathf.Abs(rig.ShownPitch(true)) < .5f;
                Vector3 p = Bone(s, "foot.L").position;
                if (planted && had) { SlideRun += new Vector2(p.x - last.x, p.z - last.z).magnitude; Slide = Mathf.Max(Slide, SlideRun); Planted++; }
                else SlideRun = 0f;
                had = planted; last = p;
            }
        }

        private static Stats Walk(Stage s, Vector3 localDirection, float speed, float seconds, string frames, float warmUp = 1.2f)
        {
            ModelGaitRig rig = s.Body.ActiveGroundRig;
            s.Sprint = speed > 3f;
            float t = 0f; int i = 0;
            while (t < warmUp) { float dt = FrameTime(i++); t += dt; s.Player.position += s.Player.TransformDirection(localDirection.normalized) * speed * dt; s.Body.AdvancePresentation(dt); }
            var stats = new Stats();
            t = 0f; int shot = 0; float nextShot = 0f;
            while (t < seconds)
            {
                float dt = FrameTime(i++); t += dt;
                s.Player.position += s.Player.TransformDirection(localDirection.normalized) * speed * dt;
                s.Body.AdvancePresentation(dt);
                stats.Sample(s, rig, dt);
                if (frames != null && OutDir != null && t >= nextShot && shot < 6)
                {
                    Save(Render(s.Eye, 640, 360), frames + "_eye_" + shot);
                    s.Side.transform.position = s.Player.position + new Vector3(4.2f, .95f, 0f);
                    s.Side.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                    Save(Render(s.Side, 480, 270), frames + "_side_" + shot);
                    shot++; nextShot += .1f;
                }
            }
            return stats;
        }

        [TestCase(false), TestCase(true)]
        public void MeasureWalkQuality(bool eva)
        {
            Stage s = Build(eva);
            string tag = eva ? "eva" : "work";
            var table = new StringBuilder();
            float leg = s.Body.ActiveGroundRig.LegLength;
            table.AppendLine(tag + " leg length " + leg.ToString("F3") + " m");
            table.AppendLine("case | speed m/s | pelvis bob cm | max knee bend deg | worst planted slide mm | planted frames | steps per minute");
            var cases = new (string name, Vector3 dir, float speed, float pitch)[]
            {
                ("forward-carry", Vector3.forward, 1.5f, 40f),
                ("forward-walk", Vector3.forward, 2.5f, 40f),
                ("forward-sprint", Vector3.forward, 3.75f, 25f),
                ("backward", Vector3.back, 2.5f, 40f),
                ("strafe-right", Vector3.right, 2.5f, 40f),
                ("strafe-left", Vector3.left, 2.5f, 40f),
                ("diagonal", new Vector3(1f, 0f, 1f), 2.5f, 40f),
                ("back-diagonal", new Vector3(-1f, 0f, -1f), 2.5f, 40f),
                ("slow", Vector3.forward, .8f, 40f),
            };
            const float seconds = 2.4f;
            foreach (var c in cases)
            {
                s.Eye.transform.localRotation = Quaternion.Euler(c.pitch, 0f, 0f);
                Stats st = Walk(s, c.dir, c.speed, seconds, tag + "_" + c.name);
                table.AppendLine(c.name + " | " + c.speed + " | " + ((st.HipMax - st.HipMin) * 100f).ToString("F1") + " | " + st.KneeMax.ToString("F0")
                    + " | " + (st.Slide * 1000f).ToString("F1") + " | " + st.Planted + " | " + (120f * st.Cycles / st.Seconds).ToString("F0"));
                Assert.Greater(st.Planted, 10, c.name + ": the feet must actually plant");
                Assert.Less(st.Slide, .006f, c.name + ": planted foot slid");
            }
            // Reversals and a quarter turn of the travel direction at full speed, measured from the first frame after it.
            foreach (var c in new (string name, Vector3 from, Vector3 to)[]
            {
                ("reverse W-S", Vector3.forward, Vector3.back), ("reverse S-W", Vector3.back, Vector3.forward),
                ("reverse A-D", Vector3.left, Vector3.right), ("W to D", Vector3.forward, Vector3.right),
            })
            {
                Walk(s, c.from, 2.5f, .7f, null);
                Stats st = Walk(s, c.to, 2.5f, 1.2f, null, 0f);
                table.AppendLine(c.name + " | 2.5 | " + ((st.HipMax - st.HipMin) * 100f).ToString("F1") + " | " + st.KneeMax.ToString("F0")
                    + " | " + (st.Slide * 1000f).ToString("F1") + " | " + st.Planted + " | " + (120f * st.Cycles / st.Seconds).ToString("F0"));
                Assert.Less(st.Slide, .012f, c.name + ": planted foot dragged after the change of direction");
            }
            Debug.Log("[GAIT-QUALITY]\n" + table);
            if (!string.IsNullOrEmpty(OutDir)) { Directory.CreateDirectory(OutDir); File.WriteAllText(Path.Combine(OutDir, tag + "_gait_quality.txt"), table.ToString()); }
        }

        private static readonly Vector3[] Directions =
            { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, -1f).normalized };

        [Test]
        public void StanceTravelNeverExceedsTheLegReach()
        {
            const float leg = .855f, lead = .30f;
            foreach (Vector3 d in Directions)
                for (float v = .3f; v <= 4.2f; v += .1f)
                    foreach (float scale in new[] { 1f, .9f })
                    {
                        float stride = ModelGait.GaitStride(v, leg, d, lead, scale);
                        float stance = ModelGait.GaitStance(v, leg, d, lead, scale);
                        Assert.LessOrEqual(stride * stance, ModelGait.StanceSpan(d) * lead + 1e-4f, "travel in one stance at " + v + " " + d);
                        Assert.LessOrEqual(stride, ModelGait.DirectionalStride(v, leg, d) * scale + 1e-4f, "never longer than the natural stride");
                        Assert.That(stance, Is.InRange(ModelGait.MinimumGaitStance - 1e-4f, .66f + 1e-4f));
                        Assert.Greater(stride, .19f);
                    }
        }

        [Test]
        public void CadenceStaysHumanAtTheGameSpeeds()
        {
            const float leg = .855f, lead = .30f;
            // Steps per minute (both feet) for the carry, walk and sprint speeds of the game.
            float carry = 120f * 1.5f / ModelGait.GaitStride(1.5f, leg, Vector3.forward, lead, 1f);
            float walk = 120f * 2.5f / ModelGait.GaitStride(2.5f, leg, Vector3.forward, lead, 1f);
            float sprint = 120f * 3.75f / ModelGait.GaitStride(3.75f, leg, Vector3.forward, lead, 1f);
            Assert.That(carry, Is.InRange(110f, 150f), "carry cadence");
            Assert.That(walk, Is.InRange(140f, 190f), "walk cadence (a light jog at 2.5 m/s)");
            Assert.That(sprint, Is.InRange(160f, 215f), "sprint cadence");
            Assert.Less(carry, walk); Assert.Less(walk, sprint);
        }

        [Test]
        public void StrideIsContinuousInSpeedAndDirection()
        {
            const float leg = .855f, lead = .30f;
            for (float v = .3f; v < 4.2f; v += .02f)
                Assert.Less(Mathf.Abs(ModelGait.GaitStride(v + .02f, leg, Vector3.forward, lead, 1f) - ModelGait.GaitStride(v, leg, Vector3.forward, lead, 1f)), .05f, "speed " + v);
            for (float a = 0f; a < 360f; a += 2f)
            {
                Vector3 d0 = Quaternion.AngleAxis(a, Vector3.up) * Vector3.forward, d1 = Quaternion.AngleAxis(a + 2f, Vector3.up) * Vector3.forward;
                Assert.Less(Mathf.Abs(ModelGait.GaitStride(2.5f, leg, d1, lead, 1f) - ModelGait.GaitStride(2.5f, leg, d0, lead, 1f)), .06f, "heading " + a);
            }
        }

        /// <summary>
        /// First person only: the seated WORK body (it has no head) and its straps are drawn only while the camera is at
        /// the seat eye, never from outside, whatever the camera looks at. The neck stays closed from the seat eye and
        /// everything is restored when the lease ends.
        /// </summary>
        [Test]
        public void SeatedBodyIsNeverDrawnHeadless()
        {
            Stage s = Build(false);
            Transform vehicle = new GameObject("Vehicle").transform;
            Transform seat = new GameObject("Departure seated WORK body").transform;
            seat.SetParent(vehicle, false);
            seat.SetLocalPositionAndRotation(new Vector3(3f, .12f, 2f), Quaternion.Euler(0f, 180f, 0f));
            Camera view = s.Side; view.tag = "MainCamera"; view.enabled = true; view.fieldOfView = 60f;
            Assert.AreSame(view, Camera.main, "the test camera must be the main camera");
            Renderer[] renderers = s.Body.VisualRoot.GetComponentsInChildren<Renderer>(true);
            EmbodiedBodyPresenter.AllowEditModeDepartureSeat = true;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;   // the lease destroys its props with Object.Destroy
            try
            {
                Transform neck = s.Body.GetWorkBone("neck01");
                Assert.IsTrue(neck, "neck01 joint");
                Vector3 neckScale = neck.localScale;
                view.transform.SetPositionAndRotation(seat.TransformPoint(new Vector3(0f, 1.277f, .06f)), seat.rotation);
                Assert.IsTrue(s.Body.TryBeginDepartureSeat(seat), "seat lease");
                Assert.IsTrue(s.Body.PresentDepartureSeat(.5f));
                Assert.Less(neck.localScale.x, neckScale.x * .01f, "neck closed while seated");
                Vector3 head = neck.position + seat.up * .2f;
                Debug.Log("[SEAT-HEAD] head point in seat frame " + seat.InverseTransformPoint(head).ToString("F3")
                          + " seat eye distance " + Vector3.Distance(view.transform.position, head).ToString("F3"));

                // Seat eye, looking ahead, then glancing down at the buckle like the cinematic (lean .13 forward, .105 down).
                foreach (float pitch in new[] { 0f, 35f, 65f, 80f })
                {
                    Vector3 eye = seat.TransformPoint(new Vector3(0f, 1.277f - .105f * pitch / 80f, .06f + .13f * pitch / 80f));
                    view.transform.SetPositionAndRotation(eye, seat.rotation * Quaternion.Euler(pitch, 0f, 0f));
                    Assert.IsTrue(s.Body.PresentDepartureSeat(1f + pitch * .01f));
                    foreach (Renderer r in renderers) Assert.IsFalse(r.forceRenderingOff, "seat eye pitch " + pitch + ": " + r.name + " must be drawn");
                }

                // Outside, looking at the seat from several sides: nothing of the body or its straps is drawn.
                renderers = s.Body.VisualRoot.GetComponentsInChildren<Renderer>(true);
                foreach (Vector3 from in new[] { new Vector3(0f, .5f, 1.6f), new Vector3(1.4f, .4f, .6f), new Vector3(-1.2f, .9f, -.8f), new Vector3(0f, .6f, -1.5f), new Vector3(.3f, .25f, .75f) })
                {
                    view.transform.position = head + seat.TransformDirection(from);
                    view.transform.rotation = Quaternion.LookRotation(seat.TransformPoint(new Vector3(0f, .8f, .1f)) - view.transform.position, Vector3.up);
                    Assert.IsTrue(s.Body.PresentDepartureSeat(2f));
                    foreach (Renderer r in renderers) Assert.IsTrue(r.forceRenderingOff, "outside " + from + ": " + r.name + " must not be drawn");
                }

                // Outside and looking away from the seat: still not drawn (never in third person, whatever is in view).
                view.transform.SetPositionAndRotation(head + seat.TransformDirection(new Vector3(0f, .3f, 2f)), seat.rotation);
                Assert.IsTrue(s.Body.PresentDepartureSeat(2.2f));
                foreach (Renderer r in renderers) Assert.IsTrue(r.forceRenderingOff, "outside, looking away: " + r.name);

                // The camera flying into the seat like the cinematic (from 1.4 m in front, turning round): the body
                // appears only in the last 0.33 m and then stays.
                Vector3 seatEye = seat.TransformPoint(new Vector3(0f, 1.277f, .06f));
                Vector3 start = seatEye + seat.TransformDirection(new Vector3(.3f, .35f, 1.3f));
                Quaternion startLook = Quaternion.LookRotation(seatEye - start, Vector3.up);
                bool shown = false;
                for (int i = 0; i <= 90; i++)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, i / 90f);
                    view.transform.SetPositionAndRotation(Vector3.Lerp(start, seatEye, blend), Quaternion.Slerp(startLook, seat.rotation, blend));
                    Assert.IsTrue(s.Body.PresentDepartureSeat(i / 30f));
                    float distance = Vector3.Distance(view.transform.position, neck.position + seat.up * .2f);
                    bool hidden = renderers[0].forceRenderingOff;
                    foreach (Renderer r in renderers) Assert.AreEqual(hidden, r.forceRenderingOff, "all renderers switch together");
                    if (distance > .34f) Assert.IsTrue(hidden, "fly-in: drawn " + distance.ToString("F2") + " m from the head place");
                    if (distance < .29f) Assert.IsFalse(hidden, "fly-in: not drawn " + distance.ToString("F2") + " m from the head place");
                    if (shown) Assert.IsFalse(hidden, "no flicker once the body is shown");
                    shown = !hidden;
                }
                Assert.IsTrue(shown, "drawn at the seat eye at the end of the fly-in");

                // Back at the seat eye the body returns; ending the lease restores the neck and every renderer.
                view.transform.SetPositionAndRotation(seat.TransformPoint(new Vector3(0f, 1.277f, .06f)), seat.rotation * Quaternion.Euler(60f, 0f, 0f));
                Assert.IsTrue(s.Body.PresentDepartureSeat(2.4f));
                foreach (Renderer r in renderers) Assert.IsFalse(r.forceRenderingOff, "back at the eye: " + r.name);
                view.transform.position = head + seat.TransformDirection(new Vector3(0f, .5f, 1.6f));
                view.transform.rotation = Quaternion.LookRotation(head - view.transform.position, Vector3.up);
                Assert.IsTrue(s.Body.PresentDepartureSeat(2.6f));
                s.Body.EndDepartureSeat();
                Assert.IsFalse(s.Body.HasDepartureSeat);
                foreach (Renderer r in renderers) if (r) Assert.IsFalse(r.forceRenderingOff, "after the lease: " + r.name);
                Assert.AreEqual(neckScale.x, neck.localScale.x, 1e-5f, "neck restored");
            }
            finally
            {
                EmbodiedBodyPresenter.AllowEditModeDepartureSeat = false;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        private static Texture2D Render(Camera camera, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt; camera.enabled = true; camera.Render(); camera.enabled = false; camera.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static void Save(Texture2D tex, string name)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}

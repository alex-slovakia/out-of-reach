using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OutOfReach.Composition.Editor;
using OutOfReach.Contracts;
using OutOfReach.Presentation.Motion;
using OutOfReach.Presentation.Motion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Tests.BodyAnim
{
    /// <summary>
    /// Builds the real WORK/EVA body (I06BodyAuthoring) on a flat stage, walks it with synthetic feet motion and
    /// measures foot planting and mesh/camera clearance. With OOR_BODYANIM_OUT set it also writes PNG frames
    /// (needs a real GPU, so run it without -nographics). Measurements always run.
    /// </summary>
    public sealed class BodyFrameCaptureTests
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
#pragma warning disable CS0067
            public event Action Changed;
#pragma warning restore CS0067
            public void Raise() => Changed?.Invoke();
        }

        private sealed class Clock : ISimulationClock
        {
            public bool Paused;
            public bool IsPaused => Paused;
            public double TimeScale => 1;
            public double ElapsedSimulationSeconds => 0;
        }

        private sealed class Stage
        {
            public Suit Suit = new Suit();
            public Transform Player;
            public Camera Eye, Side;
            public EmbodiedBodyPresenter Body;
            public CharacterController Controller;
            public bool Sprint;
            public readonly Clock Clock = new Clock();
            public bool Visible = true, Resting;
            public RestStopReason LastStop;
            public bool Floating;
        }

        private static string OutDir => Environment.GetEnvironmentVariable("OOR_BODYANIM_OUT");

        private static Stage Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stage = new Stage();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground"; ground.transform.position = new Vector3(0f, -.5f, 0f); ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(.45f, .45f, .5f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var player = new GameObject("Player");
            stage.Player = player.transform;
            stage.Controller = player.AddComponent<CharacterController>();
            stage.Controller.height = 1.8f; stage.Controller.radius = .28f; stage.Controller.center = new Vector3(0f, .9f, 0f);
            var eye = new GameObject("Eye"); eye.transform.SetParent(player.transform, false); eye.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            stage.Eye = eye.AddComponent<Camera>(); stage.Eye.fieldOfView = 75f; stage.Eye.nearClipPlane = .05f; stage.Eye.enabled = false;
            var side = new GameObject("Side"); stage.Side = side.AddComponent<Camera>(); stage.Side.fieldOfView = 28f; stage.Side.enabled = false;
            var parent = new GameObject("Presentation").transform;
            stage.Body = I06BodyAuthoring.Create(parent, player.transform, stage.Eye);
            foreach (SkinnedMeshRenderer r in stage.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            { r.forceMatrixRecalculationPerRender = true; r.updateWhenOffscreen = true; }
            stage.Body.ConfigureSession(stage.Suit, stage.Clock, () => stage.Visible, () => stage.Floating, () => EmbodiedBodyPresenter.BodyArmVisibility.Both);
            stage.Body.SetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing, new Vector3(0f, 0f, -.285f));
            stage.Body.ConfigureGroundLocomotion(stage.Controller, () => true, () => stage.Sprint);
            return stage;
        }

        private static void Step(Stage s, float speed, float dt, Vector3 heading)
        {
            s.Player.position += heading.normalized * speed * dt;
            s.Body.AdvancePresentation(dt);
        }

        private static void SetPitch(Stage s, float downDegrees) => s.Eye.transform.localRotation = Quaternion.Euler(downDegrees, 0f, 0f);

        private static Texture2D Render(Camera camera, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt; camera.enabled = true;
            camera.Render();
            camera.enabled = false; camera.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static void Save(Texture2D tex, string name)
        {
            string dir = OutDir;
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        }

        private static void PlaceSide(Stage s)
        {
            Vector3 p = s.Player.position;
            s.Side.transform.position = p + new Vector3(4.2f, .95f, 0f);
            s.Side.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f), Vector3.up);
        }

        private static Transform Bone(Stage s, string name) => s.Body.GetVisibleBone(name);

        /// <summary>Smallest distance from the eye to any vertex of the visible body skins, in metres.</summary>
        private static float NearestBodyVertex(Stage s) => NearestBodyVertex(s, s.Eye.transform.position);

        private static float NearestBodyVertex(Stage s, Vector3 eye)
        {
            float best = float.MaxValue;
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer r in s.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled) continue;
                r.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (Vector3 v in mesh.vertices) best = Mathf.Min(best, (m.MultiplyPoint3x4(v) - eye).magnitude);
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            return best;
        }

        private static void Settle(Stage s)
        {
            for (int i = 0; i < 4; i++) s.Body.AdvancePresentation(1f / 60f);
        }

        [TestCase(false), TestCase(true)]
        public void WalkingKeepsPlantedFeetAndCapturesFrames(bool eva)
        {
            Stage s = Build();
            s.Suit.On = eva; s.Suit.Raise();
            Settle(s);
            Assert.IsTrue(s.Body.IsVisible, "body must be visible for the capture rig");
            string tag = eva ? "eva" : "work";
            Vector3 heading = Vector3.forward;
            const float dt = 1f / 60f;
            var ankleL = Bone(s, "foot.L"); var ankleR = Bone(s, "foot.R");
            Assert.IsTrue(ankleL && ankleR);

            foreach (float speed in new[] { 2.5f, 3.75f, 1.5f })
            {
                s.Sprint = speed > 3f;
                for (int i = 0; i < 90; i++) Step(s, speed, dt, heading);   // reach steady gait
                var left = new List<Vector3>(); var right = new List<Vector3>();
                var trackL = new SlideTracker(s, true); var trackR = new SlideTracker(s, false);
                float stridePeriod = ModelGait.StrideLength(speed, ModelGait.ReferenceLegLength) / speed;
                int frames = Mathf.RoundToInt(stridePeriod * 2f * 60f);
                for (int i = 0; i < frames; i++)
                {
                    Step(s, speed, dt, heading);
                    left.Add(ankleL.position); right.Add(ankleR.position);
                    trackL.Sample(); trackR.Sample();
                    if (speed == 2.5f && OutDir != null && i % 6 == 0 && i < 6 * 8)
                    {
                        PlaceSide(s); Save(Render(s.Side, 640, 360), tag + "_walk_side_" + (i / 6));
                    }
                }
                float slideL = MaxPlantedSlide(left), slideR = MaxPlantedSlide(right);
                Debug.Log("[BODYANIM] " + tag + " speed " + speed + " ankle-window slide mm L " + slideL * 1000f + " R " + slideR * 1000f
                          + " | planted flat-foot path mm L " + trackL.Worst * 1000f + " R " + trackR.Worst * 1000f
                          + " | flat frames " + trackL.Samples + "/" + trackR.Samples
                          + " | cadence " + ModelGait.StepsPerMinute(speed, ModelGait.ReferenceLegLength));
                Assert.Greater(trackL.Samples + trackR.Samples, 10, "the feet must actually plant");
                Assert.Less(trackL.Worst, .005f, tag + " planted left foot slid at " + speed);
                Assert.Less(trackR.Worst, .005f, tag + " planted right foot slid at " + speed);
            }

            // Look-down views: standing and walking.
            s.Sprint = false;
            foreach (float pitch in new[] { 0f, 35f, 60f, 80f })
            {
                SetPitch(s, pitch);
                for (int i = 0; i < 30; i++) s.Body.AdvancePresentation(dt);
                float standingNear = NearestBodyVertex(s);
                for (int i = 0; i < 40; i++) Step(s, 2.5f, dt, heading);
                float walkingNear = NearestBodyVertex(s);
                Debug.Log("[BODYANIM] " + tag + " pitch " + pitch + " nearest vertex to eye standing " + standingNear + " walking " + walkingNear);
                if (OutDir != null)
                {
                    Save(Render(s.Eye, 960, 540), tag + "_look" + pitch + "_walking");
                    for (int i = 0; i < 60; i++) s.Body.AdvancePresentation(dt);
                    Save(Render(s.Eye, 960, 540), tag + "_look" + pitch + "_standing");
                    PlaceSide(s); Save(Render(s.Side, 640, 360), tag + "_look" + pitch + "_side");
                }
                Assert.Greater(walkingNear, .05f, tag + " mesh crosses the camera near plane at pitch " + pitch);
                Assert.Greater(standingNear, .05f, tag + " standing mesh crosses the camera at pitch " + pitch);
            }
        }

        [TestCase(false), TestCase(true)]
        public void LegPushPlantsExtendsAndReleasesInBothSuits(bool eva)
        {
            Stage s = Build();
            s.Suit.On = eva; s.Suit.Raise();
            s.Floating = true;
            Settle(s);
            Assert.AreEqual(EmbodiedBodyPresenter.BodyPose.Floating, s.Body.CurrentPose);
            Transform footL = Bone(s, "foot.L"), footR = Bone(s, "foot.R"), hipL = Bone(s, "upperleg01.L");
            Vector3 direction = new Vector3(0f, .6f, .8f).normalized;
            float neutral = (footL.position - hipL.position).magnitude;
            s.Body.PlayLegPush(direction);
            float minReach = float.MaxValue, maxReach = 0f; string tag = eva ? "eva" : "work";
            for (int i = 0; i < 48; i++)
            {
                s.Body.AdvancePresentation(1f / 60f);
                float reach = (footL.position - hipL.position).magnitude;
                Assert.IsFalse(float.IsNaN(reach), "NaN leg at frame " + i);
                minReach = Mathf.Min(minReach, reach); maxReach = Mathf.Max(maxReach, reach);
                if (OutDir != null && i % 6 == 5)
                {
                    s.Side.transform.position = s.Player.position + new Vector3(4.2f, 1.2f, 0f);
                    s.Side.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -.1f, 0f), Vector3.up);
                    Save(Render(s.Side, 640, 360), tag + "_legpush_" + (i / 6));
                }
            }
            Debug.Log("[BODYANIM] " + tag + " legpush neutral reach " + neutral + " min " + minReach + " max " + maxReach);
            Assert.Less(minReach, neutral * .75f, "legs must tuck before the push");
            Assert.Greater(maxReach, neutral * 1.02f, "legs must extend towards the deck");
            for (int i = 0; i < 20; i++) s.Body.AdvancePresentation(1f / 60f);
            Assert.AreEqual(neutral, (footL.position - hipL.position).magnitude, neutral * .06f, "must return to the floating pose");
        }

        /// <summary>Path length of the real ankle bone while the rig reports the foot planted and flat.</summary>
        private sealed class SlideTracker
        {
            private readonly ModelGaitRig rig; private readonly Transform bone; private readonly bool left;
            private bool had; private Vector3 last; private float run;
            public float Worst; public int Samples;
            public SlideTracker(Stage s, bool leftFoot)
            {
                rig = s.Body.ActiveGroundRig; left = leftFoot;
                bone = Bone(s, leftFoot ? "foot.L" : "foot.R");
            }
            public void Sample()
            {
                bool flat = rig.IsPlanted(left) && Mathf.Abs(rig.ShownPitch(left)) < .5f;
                Vector3 p = bone.position;
                if (flat && had) { run += new Vector2(p.x - last.x, p.z - last.z).magnitude; Worst = Mathf.Max(Worst, run); Samples++; }
                else run = 0f;
                had = flat; last = p;
            }
        }

        private static void Turn(Stage s, float degrees) => s.Player.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * s.Player.rotation;

        private static void Shot(Stage s, Camera camera, string name, int w = 640, int h = 360)
        {
            if (OutDir != null) Save(Render(camera, w, h), name);
        }

        private static void PlaceAbove(Stage s)
        {
            s.Side.transform.position = s.Player.position + new Vector3(0f, 3.2f, -.3f);
            s.Side.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }

        [TestCase(false), TestCase(true)]
        public void TurningInPlaceStepsInsteadOfSkating(bool eva)
        {
            Stage s = Build();
            s.Suit.On = eva; s.Suit.Raise();
            Settle(s);
            string tag = eva ? "eva" : "work";
            ModelGaitRig rig = s.Body.ActiveGroundRig;
            var trackL = new SlideTracker(s, true); var trackR = new SlideTracker(s, false);
            const float dt = 1f / 60f;
            int steps = 0; bool plantedL = true, plantedR = true; float worstTwist = 0f;
            SetPitch(s, 70f);
            for (int i = 0; i < 150; i++)
            {
                Turn(s, 120f * dt);
                s.Body.AdvancePresentation(dt);
                trackL.Sample(); trackR.Sample();
                if (plantedL && !rig.IsPlanted(true)) steps++;
                if (plantedR && !rig.IsPlanted(false)) steps++;
                plantedL = rig.IsPlanted(true); plantedR = rig.IsPlanted(false);
                worstTwist = Mathf.Max(worstTwist, Mathf.Abs(rig.YawOffset(true)), Mathf.Abs(rig.YawOffset(false)));
                if (i % 15 == 7) { PlaceAbove(s); Shot(s, s.Side, tag + "_turn_above_" + (i / 15)); Shot(s, s.Eye, tag + "_turn_eye_" + (i / 15), 480, 270); }
            }
            for (int i = 0; i < 90; i++) { s.Body.AdvancePresentation(dt); trackL.Sample(); trackR.Sample(); }
            Debug.Log("[BODYANIM] " + tag + " turn 180deg: steps " + steps + " planted path mm L " + trackL.Worst * 1000f + " R " + trackR.Worst * 1000f
                      + " worst foot twist " + worstTwist + " final slot dist L " + rig.SlotDistance(true) + " R " + rig.SlotDistance(false)
                      + " yaw L " + rig.YawOffset(true) + " R " + rig.YawOffset(false));
            Assert.GreaterOrEqual(steps, 4, "turning 180 degrees in place needs several steps");
            Assert.Less(trackL.Worst, .003f, "planted left foot skated while turning");
            Assert.Less(trackR.Worst, .003f, "planted right foot skated while turning");
            Assert.Less(worstTwist, 75f, "feet must step before the legs twist too far");
            Assert.Less(rig.SlotDistance(true), .04f); Assert.Less(rig.SlotDistance(false), .04f);
            Assert.Less(Mathf.Abs(rig.YawOffset(true)), 10f); Assert.Less(Mathf.Abs(rig.YawOffset(false)), 10f);
        }

        [TestCase(false), TestCase(true)]
        public void WalkingACurveKeepsPlantedFeet(bool eva)
        {
            Stage s = Build();
            s.Suit.On = eva; s.Suit.Raise();
            Settle(s);
            string tag = eva ? "eva" : "work";
            var trackL = new SlideTracker(s, true); var trackR = new SlideTracker(s, false);
            const float dt = 1f / 60f;
            for (int i = 0; i < 240; i++)
            {
                Turn(s, 60f * dt);
                Step(s, 2.5f, dt, s.Player.forward);
                if (i > 30) { trackL.Sample(); trackR.Sample(); }
                if (i % 20 == 10) { PlaceAbove(s); Shot(s, s.Side, tag + "_curve_above_" + (i / 20)); }
            }
            Debug.Log("[BODYANIM] " + tag + " curve 60deg/s at 2.5 m/s: planted path mm L " + trackL.Worst * 1000f + " R " + trackR.Worst * 1000f
                      + " flat frames " + trackL.Samples + "/" + trackR.Samples);
            Assert.Greater(trackL.Samples + trackR.Samples, 20);
            Assert.Less(trackL.Worst, .006f, "planted left foot skated in the curve");
            Assert.Less(trackR.Worst, .006f, "planted right foot skated in the curve");
        }

        [TestCase(false), TestCase(true)]
        public void StoppingSettlesTheFeetWithoutSliding(bool eva)
        {
            Stage s = Build();
            s.Suit.On = eva; s.Suit.Raise();
            Settle(s);
            string tag = eva ? "eva" : "work";
            ModelGaitRig rig = s.Body.ActiveGroundRig;
            const float dt = 1f / 60f;
            for (int i = 0; i < 100; i++) Step(s, 2.5f, dt, Vector3.forward);
            var trackL = new SlideTracker(s, true); var trackR = new SlideTracker(s, false);
            int settleSteps = 0; bool stepping = false; int settledAt = -1;
            for (int i = 0; i < 90; i++)
            {
                s.Body.AdvancePresentation(dt);
                trackL.Sample(); trackR.Sample();
                if (rig.IsStepping && !stepping) settleSteps++;
                stepping = rig.IsStepping;
                if (settledAt < 0 && !rig.IsStepping && rig.IsPlanted(true) && rig.IsPlanted(false)
                    && rig.SlotDistance(true) < .04f && rig.SlotDistance(false) < .04f) settledAt = i;
                if (i % 6 == 0 && i < 48) { PlaceSide(s); Shot(s, s.Side, tag + "_stop_side_" + (i / 6)); }
            }
            Debug.Log("[BODYANIM] " + tag + " stop: settle steps " + settleSteps + " settled after s " + (settledAt + 1) * dt
                      + " planted path mm L " + trackL.Worst * 1000f + " R " + trackR.Worst * 1000f);
            Assert.GreaterOrEqual(settledAt, 0, "feet must end side by side after a stop");
            Assert.Less((settledAt + 1) * dt, 1.2f);
            Assert.Less(trackL.Worst, .003f); Assert.Less(trackR.Worst, .003f);
        }

        [Test]
        public void WalkingUpASlopeKeepsSolesOnTheRamp()
        {
            Stage s = Build();
            Settle(s);
            ModelGaitRig rig = s.Body.ActiveGroundRig;
            float flatAnkle = (Bone(s, "foot.L").position.y + Bone(s, "foot.R").position.y) * .5f;
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            Quaternion tilt = Quaternion.Euler(-12f, 0f, 0f);
            Vector3 n = tilt * Vector3.up;
            ramp.transform.SetPositionAndRotation(-.5f * n, tilt);
            ramp.transform.localScale = new Vector3(8f, 1f, 30f);
            Physics.SyncTransforms();
            float RampY(Vector3 p) => (-n.x * p.x - n.z * p.z) / n.y;
            const float dt = 1f / 60f;
            float worst = 0f; int samples = 0;
            s.Player.position = new Vector3(0f, RampY(new Vector3(0f, 0f, 1f)), 1f);
            s.Body.AdvancePresentation(dt);
            for (int i = 0; i < 150; i++)
            {
                Vector3 p = s.Player.position + Vector3.forward * 2.5f * dt;
                p.y = RampY(p);
                s.Player.position = p;
                s.Body.AdvancePresentation(dt);
                foreach (bool leftFoot in new[] { true, false })
                {
                    if (!rig.IsPlanted(leftFoot) || Mathf.Abs(rig.ShownPitch(leftFoot)) > .5f) continue;
                    Transform foot = Bone(s, leftFoot ? "foot.L" : "foot.R");
                    float above = foot.position.y - RampY(foot.position);
                    worst = Mathf.Max(worst, Mathf.Abs(above - flatAnkle)); samples++;
                }
                if (i % 25 == 12) { PlaceSide(s); Shot(s, s.Side, "work_ramp_side_" + (i / 25)); }
            }
            Debug.Log("[BODYANIM] ramp 12deg: flat ankle height " + flatAnkle + " worst planted ankle height error mm " + worst * 1000f + " samples " + samples);
            Assert.Greater(samples, 20);
            Assert.Less(worst, .03f, "planted feet must sit on the ramp, not float above or sink in");
        }

        [Serializable] private sealed class HabitatAnchor { public string name; public float[] point; public float[] forward; }
        [Serializable] private sealed class HabitatBox { public string name; public float[] centre; public float[] size; }
        [Serializable] private sealed class HabitatLayoutData { public HabitatAnchor[] anchors; public HabitatBox[] colliders; }
        private const string HabitatRoot = "Assets/_OutOfReach/Visuals/Models/Habitat";

        private sealed class Berth
        {
            public Transform Frame;
            public Vector3 CameraPosition, CameraForward, EntryPoint, EntryForward, EyeLocal;
            public Bounds BerthBox;
            public Camera Eye, Aisle;
            public readonly List<RestPhase> Phases = new List<RestPhase>();
        }

        private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        // The real habitat model and its authored anchors (HabitatLayout.json), the frame from the authoring helper.
        private static Berth BuildBerth(Stage s)
        {
            var layout = JsonUtility.FromJson<HabitatLayoutData>(AssetDatabase.LoadAssetAtPath<TextAsset>(HabitatRoot + "/HabitatLayout.json").text);
            var b = new Berth();
            foreach (HabitatAnchor a in layout.anchors)
            {
                if (a.name == "SleepCamera") { b.CameraPosition = V(a.point); b.CameraForward = V(a.forward); }
                if (a.name == "SleepEntry") { b.EntryPoint = V(a.point); b.EntryForward = V(a.forward); }
            }
            foreach (HabitatBox c in layout.colliders) if (c.name == "COL_Habitat_Berth") b.BerthBox = new Bounds(V(c.centre), V(c.size));
            var habitat = AssetDatabase.LoadAssetAtPath<GameObject>(HabitatRoot + "/Meshes/HB_Habitat_Placed.fbx");
            Assert.IsTrue(habitat, "habitat model");
            var placed = (GameObject)Object.Instantiate(habitat);
            placed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Renderer r in placed.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m && m.name.StartsWith("HB_BagQuilt"))
                        Debug.Log("[BODYANIM] berth bag renderer " + r.name + " bounds centre " + r.bounds.center.ToString("F3") + " size " + r.bounds.size.ToString("F3"));
            var lamp = new GameObject("Berth lamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 5f; lamp.intensity = 2.5f;
            lamp.transform.position = b.CameraPosition + b.CameraForward * .9f + Vector3.up * .2f;
            b.EyeLocal = ModelGaitBerthAuthoring.MeasureEyeLocal();
            Pose pose = ModelGaitBerthAuthoring.BodyFrameForCamera(b.CameraPosition, b.CameraForward, Vector3.up, b.EyeLocal);
            b.Frame = new GameObject("SleepBodyFrame").transform;
            b.Frame.SetPositionAndRotation(pose.position, pose.rotation);
            b.Eye = s.Eye;   // the presenter's own (sole) camera: root moves it, the body follows the eye
            b.Aisle = new GameObject("Aisle view").AddComponent<Camera>();
            b.Aisle.fieldOfView = 42f; b.Aisle.enabled = false;
            b.Aisle.transform.position = b.CameraPosition + b.CameraForward * 2.4f + new Vector3(0f, -.5f, .35f);
            b.Aisle.transform.rotation = Quaternion.LookRotation(pose.position + Vector3.up * .95f - b.Aisle.transform.position, Vector3.up);
            s.Floating = true;
            s.Player.SetPositionAndRotation(b.EntryPoint, Quaternion.LookRotation(b.EntryForward, Vector3.up));
            s.Body.ConfigureRest(() => s.Resting, b.Frame, () => s.LastStop);
            s.Body.RestPhaseChanged += phase => b.Phases.Add(phase);
            Physics.SyncTransforms();
            for (int i = 0; i < 8; i++) s.Body.AdvancePresentation(1f / 60f);
            return b;
        }

        // Root's camera: cut to the authored SleepCamera (pitched down to watch the straps) and back to the gameplay eye.
        private static void CameraToBerth(Stage s, Berth b, float pitch = 50f) =>
            s.Eye.transform.SetPositionAndRotation(b.CameraPosition, Quaternion.LookRotation(b.CameraForward, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f));

        private static void CameraHome(Stage s) => s.Eye.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.62f, 0f), Quaternion.identity);

        private static float MinBodyX(Stage s)
        {
            float min = float.MaxValue;
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer r in s.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled) continue;
                r.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (Vector3 v in mesh.vertices) min = Mathf.Min(min, m.MultiplyPoint3x4(v).x);
            }
            Object.DestroyImmediate(mesh);
            return min;
        }

        private static int VerticesBehind(Stage s, float planeX)
        {
            int count = 0;
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer r in s.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled) continue;
                r.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (Vector3 v in mesh.vertices) if (m.MultiplyPoint3x4(v).x < planeX) count++;
            }
            Object.DestroyImmediate(mesh);
            return count;
        }

        private static void RunUntil(Stage s, RestPhase phase, int maxFrames, Action<int> each = null)
        {
            for (int i = 0; i < maxFrames && s.Body.RestPhase != phase; i++) { s.Body.AdvancePresentation(1f / 60f); each?.Invoke(i); }
        }

        [Test]
        public void BerthRestEntersStrapsHoldsAndReleasesOnTheRealHabitat()
        {
            Stage s = Build();
            Berth b = BuildBerth(s);
            Assert.IsTrue(s.Body.PresentsRest);
            Assert.AreEqual(RestPhase.None, s.Body.RestPhase);
            Shot(s, b.Aisle, "rest_aisle_before");
            s.Resting = true;
            CameraToBerth(s, b);
            s.Body.AdvancePresentation(1f / 60f);
            Assert.AreEqual(RestPhase.Entering, s.Body.RestPhase);
            Assert.Greater(s.Body.RestDockWeight, .99f, "a camera cut into the berth docks the body at once");
            Assert.Less(Vector3.Distance(s.Body.VisualRoot.position, b.Frame.position), .01f, "no walk-in in front of the eye");
            Assert.Greater(NearestBodyVertex(s, s.Eye.transform.position), .05f);
            bool pausedChecked = false;
            RunUntil(s, RestPhase.Held, 400, i =>
            {
                if (i == 75 && !pausedChecked)
                {
                    pausedChecked = true;
                    float before = s.Body.RestPhaseSeconds;
                    s.Clock.Paused = true;
                    for (int k = 0; k < 30; k++) s.Body.AdvancePresentation(1f / 60f);
                    Assert.AreEqual(before, s.Body.RestPhaseSeconds, 1e-6f, "pause must freeze the rest playhead");
                    Assert.AreEqual(RestPhase.Entering, s.Body.RestPhase);
                    s.Clock.Paused = false;
                }
                if (i % 12 == 0) { Shot(s, b.Eye, "rest_eye_enter_" + (i / 12), 480, 270); Shot(s, b.Aisle, "rest_aisle_enter_" + (i / 12)); }
            });
            Assert.IsTrue(pausedChecked);
            Assert.AreEqual(RestPhase.Held, s.Body.RestPhase, "the strap sequence must finish");
            Transform restraint = s.Body.VisualRoot.Find("Berth restraint");
            Assert.IsTrue(restraint && restraint.gameObject.activeSelf, "visible buckled straps");
            for (int i = 0; i < 30; i++) s.Body.AdvancePresentation(1f / 60f);
            Assert.AreEqual(RestPhase.Held, s.Body.RestPhase);
            Shot(s, b.Eye, "rest_eye_held", 960, 540); Shot(s, b.Aisle, "rest_aisle_held", 960, 540);
            Assert.Less(Vector3.Distance(s.Body.VisualRoot.position, b.Frame.position), 1e-4f, "body on the berth frame");
            float near = NearestBodyVertex(s, b.CameraPosition);
            int behindWall = VerticesBehind(s, b.BerthBox.min.x);
            Debug.Log("[BODYANIM] berth: deepest skin vertex behind the berth back plane " + (b.BerthBox.min.x - MinBodyX(s)) * 1000f + " mm");
            Debug.Log("[BODYANIM] berth: eye offset " + b.EyeLocal.ToString("F4") + " frame " + b.Frame.position.ToString("F4")
                      + " yaw " + b.Frame.eulerAngles.y.ToString("F1") + " berth box " + b.BerthBox.min.ToString("F3") + ".." + b.BerthBox.max.ToString("F3")
                      + " nearest body vertex to SleepCamera " + near + " m, skin vertices behind the berth back plane " + behindWall);
            Assert.Greater(near, .05f, "no skin may cross the berth camera's near plane");

            s.Resting = false; s.LastStop = RestStopReason.Woken;
            Shot(s, b.Eye, "rest_eye_wake_start", 480, 270);
            int frames = 0;
            bool cameraHome = false;
            RunUntil(s, RestPhase.None, 300, i =>
            {
                frames = i + 1;
                // Root cuts back to the gameplay eye once the straps are open (here at 0.9 s).
                if (!cameraHome && i >= 54) { cameraHome = true; CameraHome(s); }
                if (i % 10 == 0) { Shot(s, b.Eye, "rest_eye_release_" + (i / 10), 480, 270); Shot(s, b.Aisle, "rest_aisle_release_" + (i / 10)); }
            });
            Debug.Log("[BODYANIM] berth ordinary wake: released in " + frames / 60f + " s; phases " + string.Join(",", b.Phases));
            Assert.AreEqual(RestPhase.None, s.Body.RestPhase);
            Assert.That(frames / 60f, Is.InRange(1.2f, 1.5f), "ordinary unstrap and exit");
            Assert.IsFalse(s.Body.VisualRoot.Find("Berth restraint"), "props must be gone");
            CollectionAssert.AreEqual(new[] { RestPhase.Entering, RestPhase.Held, RestPhase.Releasing, RestPhase.None }, b.Phases);
            s.Body.AdvancePresentation(1f / 60f);
            Assert.IsTrue(s.Body.IsVisible);
            Assert.AreEqual(EmbodiedBodyPresenter.BodyPose.Floating, s.Body.CurrentPose);
            Vector3 expected = s.Player.position + s.Player.rotation * s.Body.GetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Floating);
            Assert.Less(Vector3.Distance(s.Body.VisualRoot.position, expected), .01f, "presentation pose restored at the player");
        }

        [Test]
        public void HazardWakeReleasesWithinAQuarterSecond()
        {
            Stage s = Build();
            Berth b = BuildBerth(s);
            s.Resting = true;
            CameraToBerth(s, b);
            RunUntil(s, RestPhase.Held, 400);
            Assert.AreEqual(RestPhase.Held, s.Body.RestPhase);
            s.Resting = false; s.LastStop = RestStopReason.CarbonDioxideDanger;
            CameraHome(s);
            int frames = 0;
            RunUntil(s, RestPhase.None, 120, i => frames = i + 1);
            Debug.Log("[BODYANIM] berth hazard wake (CO2): released in " + frames / 60f + " s");
            Assert.AreEqual(RestPhase.None, s.Body.RestPhase);
            Assert.LessOrEqual(frames / 60f, .3f, "hazard release must take at most 0.3 s");
        }

        [Test]
        public void HiddenBodyOrClearedBindingReleasesAtOnce()
        {
            Stage s = Build();
            Berth b = BuildBerth(s);
            s.Resting = true;
            CameraToBerth(s, b);
            for (int i = 0; i < 100; i++) s.Body.AdvancePresentation(1f / 60f);
            Assert.AreEqual(RestPhase.Entering, s.Body.RestPhase);
            s.Visible = false;
            s.Body.AdvancePresentation(1f / 60f);
            Assert.AreEqual(RestPhase.None, s.Body.RestPhase, "a hidden body releases in the same frame");
            Assert.IsFalse(s.Body.VisualRoot.Find("Berth restraint"));
            s.Visible = true;
            RunUntil(s, RestPhase.Held, 400);
            Assert.AreEqual(RestPhase.Held, s.Body.RestPhase, "the rest presentation resumes while Core still rests");
            s.Body.ClearBinding();
            Assert.AreEqual(RestPhase.None, s.Body.RestPhase, "a cleared binding releases at once");
            Assert.IsFalse(s.Body.VisualRoot.Find("Berth restraint"));
        }

        private const string TransportPath = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx";

        // Root's lean toward the buckle (I06MarineBindings.PresentSeat), replayed on the harness camera.
        private static void SeatCamera(Stage s, Transform seatFrame, Vector3 seatedEye, float t)
        {
            float lookDown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.30f, 2.26f, t));
            float lookUp = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(3.23f, 4.38f, t));
            float glance = lookDown * lookUp;
            Vector3 leaning = seatedEye + (seatFrame.forward * .13f - seatFrame.up * .105f) * glance;
            Vector3 buckle = seatFrame.TransformPoint(new Vector3(0f, .665f, .315f));
            Quaternion target = Quaternion.LookRotation(buckle - leaning, seatFrame.up);
            s.Eye.transform.SetPositionAndRotation(leaning, Quaternion.Slerp(seatFrame.rotation, target, glance));
            s.Eye.fieldOfView = Mathf.Lerp(75f, 55f, glance);
        }

        [Test]
        public void DepartureSeatHarnessOnTheRealTransportSeat()
        {
            Stage s = Build();
            GameObject.Find("Ground").SetActive(false);
            Settle(s);
            var vehicleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TransportPath);
            Assert.IsTrue(vehicleAsset, "transport model");
            Transform vehicle = ((GameObject)Object.Instantiate(vehicleAsset)).transform;
            vehicle.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var seatFrame = new GameObject("Departure seated WORK body").transform;
            seatFrame.SetParent(vehicle, false);
            seatFrame.SetLocalPositionAndRotation(new Vector3(0f, .12f, 1.46f), Quaternion.Euler(0f, 180f, 0f));
            Vector3 seatedEye = vehicle.TransformPoint(new Vector3(0f, 1.397f, 1.40f));
            var lamp = new GameObject("Cabin lamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 4f; lamp.intensity = 2f;
            lamp.transform.position = seatFrame.TransformPoint(new Vector3(.3f, 1.6f, .9f));
            Camera side = s.Side;
            side.fieldOfView = 40f;
            side.transform.position = seatFrame.TransformPoint(new Vector3(1.15f, 1.05f, .85f));
            side.transform.rotation = Quaternion.LookRotation(seatFrame.TransformPoint(new Vector3(0f, .75f, .15f)) - side.transform.position, Vector3.up);
            EmbodiedBodyPresenter.AllowEditModeDepartureSeat = true;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;   // the lease destroys its props with Object.Destroy
            try
            {
                SeatCamera(s, seatFrame, seatedEye, 0f);
                Assert.IsTrue(s.Body.TryBeginDepartureSeat(seatFrame), "seat lease on the real transport");
                Transform wristR = Bone(s, "wrist.R"), wristL = Bone(s, "wrist.L");
                float latchedAt = -1f, nearest = float.MaxValue;
                for (int f = 0; f <= 150; f++)
                {
                    float t = f / 30f;
                    SeatCamera(s, seatFrame, seatedEye, t);
                    Assert.IsTrue(s.Body.PresentDepartureSeat(t), "present at " + t);
                    if (latchedAt < 0f && s.Body.IsDepartureSeatLatched) latchedAt = t;
                    nearest = Mathf.Min(nearest, NearestBodyVertex(s, s.Eye.transform.position));
                    if (f % 10 == 0) { Shot(s, s.Eye, "seat_eye_" + (f / 10), 480, 270); Shot(s, side, "seat_side_" + (f / 10)); }
                }
                Debug.Log("[BODYANIM] departure seat: latched at " + latchedAt + " s, nearest skin to the seat eye " + nearest
                          + " m, wrists " + seatFrame.InverseTransformPoint(wristL.position).ToString("F3") + " / " + seatFrame.InverseTransformPoint(wristR.position).ToString("F3"));
                Assert.Greater(latchedAt, 0f, "the restraint must latch on the departure playhead");
                Assert.Greater(nearest, .03f, "no skin across the seat camera");
                SeatFit(s, vehicle, seatFrame);
                // Pause holds the presented pose; the lease survives.
                Vector3 before = wristR.position;
                s.Clock.Paused = true;
                Assert.IsTrue(s.Body.PresentDepartureSeat(2.0f));
                Assert.Less(Vector3.Distance(before, wristR.position), 1e-5f, "pause must hold the seat pose");
                s.Clock.Paused = false;
                // A suit (or leaving the vehicle) releases the lease at once.
                s.Suit.On = true;
                Assert.IsFalse(s.Body.PresentDepartureSeat(5f));
                Assert.IsFalse(s.Body.HasDepartureSeat, "suit on releases the seat lease");
                s.Suit.On = false;
                Assert.IsTrue(s.Body.TryBeginDepartureSeat(seatFrame), "seat lease again");
                s.Body.EndDepartureSeat();
                Assert.IsFalse(s.Body.HasDepartureSeat, "EndDepartureSeat releases");
            }
            finally
            {
                EmbodiedBodyPresenter.AllowEditModeDepartureSeat = false;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        // Webbing against the actual skin (signed distance along the nearest skin normal) and the gap from the body's
        // back to the real chair surface behind it, measured at the latched pose.
        private static void SeatFit(Stage s, Transform vehicle, Transform seatFrame)
        {
            var skin = new List<Vector3>(); var normals = new List<Vector3>();
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer r in s.Body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled) continue;
                r.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                Vector3[] v = mesh.vertices, n = mesh.normals;
                for (int i = 0; i < v.Length; i++) { skin.Add(m.MultiplyPoint3x4(v[i])); normals.Add(m.MultiplyVector(n[i]).normalized); }
            }
            Object.DestroyImmediate(mesh);
            int strapPoints = 0, inside = 0; float deepest = 0f;
            var perPoint = new System.Text.StringBuilder();
            Transform props = s.Body.VisualRoot.Find("WORK departure restraint");
            if (props)
                foreach (MeshFilter filter in props.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (!filter.sharedMesh || !filter.name.ToLowerInvariant().Contains("webbing")) continue;
                    Vector3[] ribbon = filter.sharedMesh.vertices;
                    for (int vi = 0; vi < ribbon.Length; vi++)
                    {
                        Vector3 local = ribbon[vi];
                        Vector3 p = filter.transform.TransformPoint(local);
                        int best = -1; float bestDistance = float.MaxValue;
                        for (int i = 0; i < skin.Count; i++)
                        {
                            float d = (skin[i] - p).sqrMagnitude;
                            if (d < bestDistance) { bestDistance = d; best = i; }
                        }
                        if (best < 0 || bestDistance > .05f * .05f) { strapPoints++; continue; }
                        float signedDistance = Vector3.Dot(p - skin[best], normals[best]);
                        strapPoints++;
                        if (signedDistance < -.004f) { inside++; deepest = Mathf.Min(deepest, signedDistance); }
                        if (signedDistance < -.002f)
                            perPoint.Append(filter.name).Append(" pt").Append(vi / 4).Append(" v").Append(vi % 4).Append(' ')
                                .Append((signedDistance * 1000f).ToString("F1")).Append("mm n=").Append(seatFrame.InverseTransformDirection(normals[best]).ToString("F2")).Append("; ");
                    }
                }
            foreach (MeshFilter filter in vehicle.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && !filter.GetComponent<Collider>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
            var gaps = new System.Text.StringBuilder();
            foreach (string boneName in new[] { "spine05", "spine03", "spine01" })
            {
                Transform bone = Bone(s, boneName);
                if (!bone) continue;
                Vector3 back = -seatFrame.forward;
                float skinBack = 0f;
                for (int i = 0; i < skin.Count; i++)
                {
                    Vector3 d = skin[i] - bone.position;
                    if (Mathf.Abs(Vector3.Dot(d, seatFrame.up)) < .03f && Mathf.Abs(Vector3.Dot(d, seatFrame.right)) < .06f)
                        skinBack = Mathf.Max(skinBack, Vector3.Dot(d, back));
                }
                bool hit = Physics.Raycast(bone.position, back, out RaycastHit h, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                gaps.Append(boneName).Append(" y=").Append(seatFrame.InverseTransformPoint(bone.position).y.ToString("F2"))
                    .Append(hit ? " gap " + ((h.distance - skinBack) * 1000f).ToString("F0") + " mm (" + h.collider.name + ")" : " no chair hit").Append("; ");
            }
            Assert.AreEqual(0, inside, "seat webbing must not sink more than 4 mm into the skin");
            Debug.Log("[BODYANIM] departure seat fit: webbing points " + strapPoints + ", inside the skin by more than 4 mm " + inside
                      + " (deepest " + deepest * 1000f + " mm); back-to-chair " + gaps + "\n[BODYANIM] seat webbing inside: " + perPoint);
        }

        // Horizontal drift of an ankle across stretches where it stays on the ground (within 12 mm of its lowest point).
        private static float MaxPlantedSlide(List<Vector3> path)
        {
            float lowest = float.MaxValue;
            foreach (Vector3 p in path) lowest = Mathf.Min(lowest, p.y);
            float worst = 0f; int start = -1;
            for (int i = 0; i <= path.Count; i++)
            {
                bool planted = i < path.Count && path[i].y < lowest + .006f;
                if (planted && start < 0) start = i;
                if (!planted && start >= 0)
                {
                    if (i - start >= 4)
                    {
                        Vector3 a = path[start], b = path[i - 1];
                        worst = Mathf.Max(worst, new Vector2(a.x - b.x, a.z - b.z).magnitude);
                    }
                    start = -1;
                }
            }
            return worst;
        }
    }
}

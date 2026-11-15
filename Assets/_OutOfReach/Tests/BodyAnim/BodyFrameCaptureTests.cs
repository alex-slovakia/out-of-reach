using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OutOfReach.Composition.Editor;
using OutOfReach.Contracts;
using OutOfReach.Presentation.Motion;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            public bool IsPaused => false;
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
            stage.Body.ConfigureSession(stage.Suit, new Clock(), () => true, () => stage.Floating, () => EmbodiedBodyPresenter.BodyArmVisibility.Both);
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
        private static float NearestBodyVertex(Stage s)
        {
            float best = float.MaxValue;
            Vector3 eye = s.Eye.transform.position;
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
                float stridePeriod = ModelGait.StrideLength(speed, ModelGait.ReferenceLegLength) / speed;
                int frames = Mathf.RoundToInt(stridePeriod * 2f * 60f);
                for (int i = 0; i < frames; i++)
                {
                    Step(s, speed, dt, heading);
                    left.Add(ankleL.position); right.Add(ankleR.position);
                    if (speed == 2.5f && OutDir != null && i % 6 == 0 && i < 6 * 8)
                    {
                        PlaceSide(s); Save(Render(s.Side, 640, 360), tag + "_walk_side_" + (i / 6));
                    }
                }
                float slideL = MaxPlantedSlide(left), slideR = MaxPlantedSlide(right);
                Debug.Log("[BODYANIM] " + tag + " speed " + speed + " planted slide mm L " + slideL * 1000f + " R " + slideR * 1000f
                          + " cadence " + ModelGait.StepsPerMinute(speed, ModelGait.ReferenceLegLength));
                Assert.Less(slideL, .012f, tag + " left foot slid at " + speed);
                Assert.Less(slideR, .012f, tag + " right foot slid at " + speed);
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

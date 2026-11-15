using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OutOfReach.Composition;
using OutOfReach.Player;
using OutOfReach.Presentation.Motion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace OutOfReach.Tests.RealScene
{
    /// <summary>
    /// The real Bootstrap scene and real input: the mission starts through the composition's own timed-planning
    /// entry, then a virtual mouse moves the actual input action until the actual controller pitch reaches each
    /// look-down angle. Frames come from the actual gameplay camera. Set OOR_BODYANIM_OUT for PNGs and
    /// OOR_BODYANIM_TAG to label them (for example before / after).
    /// </summary>
    public sealed class BodyLookDownRealSceneTests
    {
        private const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        private static string OutDir => Environment.GetEnvironmentVariable("OOR_BODYANIM_OUT");
        private static string Tag => Environment.GetEnvironmentVariable("OOR_BODYANIM_TAG") ?? "run";

        [UnityTest]
        public IEnumerator LookingDownInTheRealSceneWithRealMouseInput()
        {
            LogAssert.ignoreFailingMessages = true;   // the full game scene logs its own warnings; frames are the evidence
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editorBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse mouse = null;
            try
            {
#if UNITY_EDITOR
                AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                while (!load.isDone) yield return null;
#endif
                for (int i = 0; i < 30; i++) yield return null;
                var composition = Object.FindFirstObjectByType<I01MissionComposition>();
                Assert.IsTrue(composition, "Bootstrap composition");
                composition.StartTimedPlanning();
                var body = Object.FindFirstObjectByType<EmbodiedBodyPresenter>();
                var player = Object.FindFirstObjectByType<FirstPersonController>();
                Assert.IsTrue(body && player, "body presenter and player");
                for (int i = 0; i < 120 && !(body.IsVisible && player.ControlAllowed); i++) yield return null;
                Debug.Log("[REALSCENE] mode chosen " + composition.ModeChosen + " body visible " + body.IsVisible + " pose " + body.CurrentPose
                          + " control " + player.ControlAllowed + " eva " + body.IsEvaVisible + " feet fit " + body.GetVisualFeetOffset(EmbodiedBodyPresenter.BodyPose.Standing));
                Assert.IsTrue(body.IsVisible, "the body must be shown in the started mission");
                Assert.IsTrue(player.ControlAllowed, "the player must have control");

                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse = InputSystem.AddDevice<Mouse>();
                Camera camera = body.PlayerCamera;
                foreach (string outfit in new[] { "work", "eva" })
                {
                    if (outfit == "eva")
                    {
                        // The EVA suit is only reachable in the Repair phase. For the same look-down check in this real
                        // scene the session's own suit flag is switched on; the presenter reads it like in the game.
                        var runtime = Object.FindFirstObjectByType(Type.GetType("OutOfReach.Core.Runtime.MissionRuntime, OutOfReach.Core.Runtime"));
                        object eva = runtime ? runtime.GetType().GetProperty("Eva")?.GetValue(runtime) : null;
                        var setter = eva?.GetType().GetProperty("IsSuitOn")?.GetSetMethod(true);
                        Assert.IsNotNull(setter, "session suit flag");
                        setter.Invoke(eva, new object[] { true });
                        for (int i = 0; i < 30 && !body.IsEvaVisible; i++) yield return null;
                        Debug.Log("[REALSCENE] " + Tag + " EVA outfit visible " + body.IsEvaVisible);
                        for (int i = 0; i < 240 && player.PitchDegrees < -1f; i++)
                        {
                            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, 30f) });
                            yield return null;
                        }
                    }
                    foreach (float down in new[] { 0f, 45f, 70f, 80f })
                    {
                        for (int i = 0; i < 240 && -player.PitchDegrees < down - .5f; i++)
                        {
                            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, -Mathf.Clamp((down + player.PitchDegrees) * 4f, 2f, 40f)) });
                            yield return null;
                        }
                        InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.zero });
                        for (int i = 0; i < 40; i++) yield return null;   // let the presenter settle its look-down posture
                        float coverage = BellyCoverage(body, camera, out float nearest);
                        Debug.Log("[REALSCENE] " + Tag + " " + outfit + " pitch down " + (-player.PitchDegrees).ToString("F1") + " deg: belly/chest screen coverage "
                                  + (coverage * 100f).ToString("F1") + "%, nearest skin " + nearest.ToString("F3") + " m");
                        Save(Render(camera, 960, 540), "real_" + Tag + "_" + outfit + "_look" + Mathf.RoundToInt(down));
                    }
                }
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorBehaviour;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        // CLAUDE-INTEGRATION: the loaded Bootstrap (and its MissionRuntime) must not leak into later fixtures, which build
        // their own runtime and otherwise fail with "a second mission runtime was enabled".
        [UnityTearDown]
        public IEnumerator UnloadTheRealScene()
        {
            Scene empty = SceneManager.CreateScene("RealScene teardown " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded != empty && loaded.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(loaded);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
            for (int i = 0; i < 3; i++) yield return null;
        }

        // Share of the screen (64 x 36 cells) that the belly and lower chest skin (0.95-1.40 m above the feet) covers.
        private static float BellyCoverage(EmbodiedBodyPresenter body, Camera camera, out float nearest)
        {
            var cells = new bool[64 * 36];
            nearest = float.MaxValue;
            var mesh = new Mesh();
            float feet = body.PlayerFeet.position.y;
            foreach (SkinnedMeshRenderer r in body.VisualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.enabled) continue;
                r.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 world = m.MultiplyPoint3x4(local);
                    nearest = Mathf.Min(nearest, Vector3.Distance(world, camera.transform.position));
                    float height = world.y - feet;
                    if (height < .95f || height > 1.40f) continue;
                    Vector3 v = camera.WorldToViewportPoint(world);
                    if (v.z <= 0f || v.x < 0f || v.x >= 1f || v.y < 0f || v.y >= 1f) continue;
                    cells[(int)(v.y * 36) * 64 + (int)(v.x * 64)] = true;
                }
            }
            Object.Destroy(mesh);
            int covered = 0;
            foreach (bool c in cells) if (c) covered++;
            return covered / (float)cells.Length;
        }

        private static Texture2D Render(Camera camera, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previousTarget;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static void Save(Texture2D tex, string name)
        {
            string dir = OutDir;
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        }
    }
}

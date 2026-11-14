using System.IO;
using OutOfReach.Tests.PlayMode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Tests.Editor
{
    /// <summary>
    /// Regenerates the two authored fixtures from the same code the PlayMode tests use, so they cannot drift:
    /// the hand-playable harness scene (test-only, never in the build list) and the camera-less PlayerRig prefab
    /// the integrator places at the PlayerSpawn anchor.
    /// Batch: Unity -batchmode -projectPath . -executeMethod OutOfReach.Tests.Editor.RepairLoopHarnessBuilder.Rebuild -quit
    /// </summary>
    public static class RepairLoopHarnessBuilder
    {
        public const string ScenePath = "Assets/_OutOfReach/Tests/Harness/RepairLoopHarness.unity";
        public const string PlayerRigPrefabPath = "Assets/_OutOfReach/Scripts/Player/PlayerRig.prefab";

        [MenuItem("OUT OF REACH/Tests/Rebuild repair-loop harness scene and PlayerRig prefab")]
        public static void Rebuild()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RepairLoopHarness harness = RepairLoopHarness.Build(true);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerRigPrefabPath));

            // The rig is saved without a camera: the single gameplay camera and audio listener belong to Bootstrap.
            PrefabUtility.SaveAsPrefabAssetAndConnect(harness.Player.gameObject, PlayerRigPrefabPath, InteractionMode.AutomatedAction);

            var cameraObject = new GameObject("HarnessCamera");
            cameraObject.transform.SetParent(harness.ViewPivot, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("HarnessLight");
            lightObject.transform.SetParent(harness.Root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new IOException("Could not save " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("OUT OF REACH: rebuilt " + ScenePath + " and " + PlayerRigPrefabPath);
        }
    }
}

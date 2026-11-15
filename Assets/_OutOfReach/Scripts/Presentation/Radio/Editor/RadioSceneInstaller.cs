using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.Radio.Editor
{
    /// <summary>
    /// STORY-RADIO-01 integrator entry point. Adds ONE scene object "Handheld radio V01" with the self-binding
    /// HandheldRadioPresenter (no composition, Core or collider change), points it at the RadioV01 prefab and the
    /// transport's BoardingThreshold anchor, and maps the Day-14 dispatch. Idempotent.
    /// Batch: -executeMethod OutOfReach.Presentation.Radio.Editor.RadioSceneInstaller.InstallIntoBootstrap -quit
    /// </summary>
    public static class RadioSceneInstaller
    {
        public const string BootstrapPath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string ObjectName = "Handheld radio V01";
        /// <summary>Existing Core content id of the Day-14 dispatch (MissionChapterFlow.RescueFailedContentId).</summary>
        public const string Day14DispatchContentId = "dispatch.rescue-1-failed";
        /// <summary>Codex's approved recording (feature/visuals-cinematics 8d95b7d), bound when present in the checkout.</summary>
        public const string RescueLostVoicePath = "Assets/_OutOfReach/Audio/Voices/Story02/vo23_mc_rescue_lost.wav";

        public static string LastReport { get; private set; }

        public static void InstallIntoBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap could not be saved.");
            Debug.Log("STORY_RADIO_01_INSTALLED " + BootstrapPath + " " + LastReport);
        }

        [MenuItem("Tools/OUT OF REACH/Radio V01 - add to open scene (no save)")]
        public static void ApplyToOpenSceneMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Radio V01", "Added. Review and save the scene.\n\n" + LastReport, "OK");
        }

        public static HandheldRadioPresenter Apply(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode only.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RadioV01Authoring.PrefabPath) ?? RadioV01Authoring.Build();
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform threshold = all.Where(t => t.name == "BoardingThreshold").OrderByDescending(t => t.gameObject.activeInHierarchy).FirstOrDefault();
            if (!threshold) throw new InvalidOperationException("BoardingThreshold anchor not found in " + scene.path);
            int colliders = all.Sum(t => t.GetComponents<Collider>().Length);

            GameObject host = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ObjectName);
            if (!host) { host = new GameObject(ObjectName); SceneManager.MoveGameObjectToScene(host, scene); }
            var presenter = host.GetComponent<HandheldRadioPresenter>() ?? host.AddComponent<HandheldRadioPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("radioPrefab").objectReferenceValue = prefab;
            so.FindProperty("boardingThreshold").objectReferenceValue = threshold;
            so.FindProperty("boardingPlacement").enumValueIndex = (int)RadioBoardingPlacement.AuroraArrival;
            SerializedProperty bindings = so.FindProperty("dispatchBeats");
            bindings.arraySize = 1;
            bindings.GetArrayElementAtIndex(0).FindPropertyRelative("contentId").stringValue = Day14DispatchContentId;
            bindings.GetArrayElementAtIndex(0).FindPropertyRelative("beatId").stringValue = RadioBeats.Day14Call;
            AudioClip rescueLost = AssetDatabase.LoadAssetAtPath<AudioClip>(RescueLostVoicePath);
            SerializedProperty voices = so.FindProperty("voiceClips");
            voices.arraySize = rescueLost ? 1 : 0;
            if (rescueLost)
            {
                voices.GetArrayElementAtIndex(0).FindPropertyRelative("cueId").stringValue = RadioBeats.RescueLostCue;
                voices.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = rescueLost;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            int after = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Sum(t => t.GetComponents<Collider>().Length);
            if (after != colliders) throw new InvalidOperationException("Radio install changed the collider count.");
            LastReport = "object=" + ObjectName + " prefab=" + RadioV01Authoring.PrefabPath + " threshold=" + Path(threshold)
                + " placement=AuroraArrival day14Dispatch=" + Day14DispatchContentId
                + " voice=" + (rescueLost ? RadioBeats.RescueLostCue + " bound (" + rescueLost.length.ToString("F1") + " s)" : "vo23 not in this checkout (subtitles and radio sound only)")
                + " colliders=" + after + " (unchanged)";
            return presenter;
        }

        private static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    }
}

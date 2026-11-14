using System;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.Audio.Editor
{
    /// <summary>Writes only an owned audio prefab. It never edits Bootstrap, settings, voice takes or SFX files.</summary>
    public static class MissionAudioAuthoring
    {
        public const string PrefabPath = "Assets/_OutOfReach/Audio/Runtime/MissionAudioPresenter.prefab";

        [MenuItem("OUT OF REACH/Audio/Create music and SFX presenter prefab")]
        public static void BuildMusicAndSfxPrefab()
        {
            if (Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("Audio authoring requires Unity 6000.3.28f1.");
            var root = new GameObject("Mission Audio Presenter - explicit clean dialogue binding required");
            try
            {
                var presenter = root.AddComponent<MissionAudioPresenter>();
                string[] names = { "ui_select", "ui_confirm", "planning_tick", "planning_urgent", "comms_loss", "power_down", "warning_pulse" };
                var effects = new SfxCueBinding[names.Length];
                for (int i = 0; i < names.Length; i++)
                    effects[i] = new SfxCueBinding(names[i], RequiredClip("Assets/_OutOfReach/Audio/SFX/" + names[i] + ".wav"));
                presenter.ConfigureClips(
                    RequiredClip("Assets/_OutOfReach/Audio/Music/mx01_isolation.wav"),
                    RequiredClip("Assets/_OutOfReach/Audio/Music/mx02_tension.wav"),
                    RequiredClip("Assets/_OutOfReach/Audio/Music/mx03_emergency.wav"),
                    Array.Empty<DialogueCueBinding>(), effects);
                if (!presenter.ValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                if (!PrefabUtility.SaveAsPrefabAsset(root, PrefabPath))
                    throw new InvalidOperationException("Could not save owned audio prefab.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            ValidateSavedPrefab();
            Debug.Log("AUDIO_PREFAB_CREATED " + PrefabPath + " | dialogue catalog intentionally unbound pending explicit take selection");
        }

        [MenuItem("OUT OF REACH/Audio/Validate saved presenter prefab")]
        public static void ValidateSavedPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) throw new InvalidOperationException("Audio prefab has not been authored yet.");
            MissionAudioPresenter presenter = prefab.GetComponent<MissionAudioPresenter>();
            if (!presenter || !presenter.ValidateConfiguration(out string reason))
                throw new InvalidOperationException("Audio prefab requires a configured MissionAudioPresenter.");
            if (prefab.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("The audio prefab must not contain an AudioListener.");
            if (prefab.GetComponentsInChildren<AudioSource>(true).Length != 0)
                throw new InvalidOperationException("Audio channels are created silent by the runtime owner; do not prepopulate autoplay sources.");
            var serialized = new SerializedObject(presenter);
            SerializedProperty cues = serialized.FindProperty("dialogueCues");
            for (int i = 0; i < cues.arraySize; i++)
            {
                var clip = cues.GetArrayElementAtIndex(i).FindPropertyRelative("cleanClip").objectReferenceValue as AudioClip;
                if (clip && AssetDatabase.GetAssetPath(clip).Contains("/Voices/Radio/"))
                    throw new InvalidOperationException("Rejected radio variants must not be the default dialogue catalog.");
            }
            Debug.Log("AUDIO_PREFAB_VALIDATION_PASS music=3 sfx=" + serialized.FindProperty("sfxCues").arraySize
                + " dialogueBindings=" + cues.arraySize + " listeners=0 authoredSources=0; runtime playback not tested by this check");
        }

        private static AudioClip RequiredClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (!clip) throw new InvalidOperationException("Missing audio clip: " + path);
            return clip;
        }
    }
}

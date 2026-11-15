using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Chapter.Editor
{
    /// <summary>
    /// CHAPTER-UI-01 installer for the one scene/Composition writer (the Claude integration assembler). Idempotent.
    /// It adds a "ChapterUi" child with <see cref="MissionChapterPresenter"/> under the I01 composition, points it at the
    /// flight-board UIDocument the survival UI already uses and at ChapterUi.uss, assigns the composition's chapterUi /
    /// chapterStyle fields when the Integration~ code patch is applied, and (optionally) turns on MissionRuntime's
    /// serialized chapterLoop toggle (which also needs survivalExtension, already on in Bootstrap).
    /// Batch: -executeMethod OutOfReach.Presentation.UI.Chapter.Editor.ChapterUiAuthoring.InstallIntoBootstrapAndSave
    /// </summary>
    public static class ChapterUiAuthoring
    {
        public const string BootstrapPath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string StylePath = "Assets/_OutOfReach/UI/Chapter/Styles/ChapterUi.uss";
        public const string ObjectName = "ChapterUi";
        private const string CompositionType = "OutOfReach.Composition.I01MissionComposition";
        private const string RuntimeType = "OutOfReach.Core.Runtime.MissionRuntime";

        [MenuItem("OUT OF REACH/Chapter UI/Install into the open scene (chapter loop on)")]
        private static void InstallMenu()
        {
            Debug.Log(Install(SceneManager.GetActiveScene(), true));
        }

        /// <summary>Batch entry: opens Bootstrap, installs with the chapter loop on, saves the scene.</summary>
        public static void InstallIntoBootstrapAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            string report = Install(scene, true);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(report);
        }

        /// <summary>Installs into a loaded scene and marks it dirty; the caller saves. Returns a human-readable report.</summary>
        public static string Install(Scene scene, bool enableChapterLoop)
        {
            var report = new StringBuilder("CHAPTER-UI-01 install into " + scene.path + ":\n");
            MonoBehaviour composition = Find(scene, CompositionType);
            if (!composition)
            {
                report.Append("  FAILED: no I01MissionComposition in the scene.");
                return report.ToString();
            }

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (!style) report.Append("  WARNING: ").Append(StylePath).Append(" did not load as a StyleSheet.\n");

            var compositionObject = new SerializedObject(composition);
            UIDocument document = compositionObject.FindProperty("survivalDocument")?.objectReferenceValue as UIDocument;
            if (!document)
            {
                SerializedProperty board = compositionObject.FindProperty("flightBoard");
                if (board?.objectReferenceValue is Component boardComponent) document = boardComponent.GetComponent<UIDocument>();
            }

            if (!document) report.Append("  WARNING: no flight-board UIDocument found; assign MissionChapterPresenter.document by hand.\n");

            Transform parent = composition.transform;
            Transform existing = parent.Find(ObjectName);
            GameObject host = existing ? existing.gameObject : new GameObject(ObjectName);
            if (!existing)
            {
                Undo.RegisterCreatedObjectUndo(host, "Install chapter UI");
                host.transform.SetParent(parent, false);
            }

            var presenter = host.GetComponent<MissionChapterPresenter>();
            if (!presenter) presenter = Undo.AddComponent<MissionChapterPresenter>(host);
            var presenterObject = new SerializedObject(presenter);
            presenterObject.FindProperty("document").objectReferenceValue = document;
            presenterObject.FindProperty("styleSheet").objectReferenceValue = style;
            presenterObject.FindProperty("keyboardShortcuts").boolValue = true;
            presenterObject.ApplyModifiedProperties();
            report.Append("  presenter: ").Append(GetPath(host.transform)).Append(" (document ")
                .Append(document ? GetPath(document.transform) : "none").Append(")\n");

            SerializedProperty chapterUi = compositionObject.FindProperty("chapterUi");
            SerializedProperty chapterStyle = compositionObject.FindProperty("chapterStyle");
            if (chapterUi != null && chapterStyle != null)
            {
                chapterUi.objectReferenceValue = presenter;
                chapterStyle.objectReferenceValue = style;
                compositionObject.ApplyModifiedProperties();
                report.Append("  composition: chapterUi and chapterStyle assigned\n");
            }
            else
            {
                report.Append("  WARNING: I01MissionComposition has no chapterUi/chapterStyle fields yet: apply UI/Chapter/Integration~/I01MissionComposition.chapter-ui.patch, recompile, run again.\n");
            }

            MonoBehaviour runtime = Find(scene, RuntimeType);
            if (runtime && enableChapterLoop)
            {
                var runtimeObject = new SerializedObject(runtime);
                SerializedProperty loop = runtimeObject.FindProperty("chapterLoop");
                SerializedProperty survival = runtimeObject.FindProperty("survivalExtension");
                if (loop != null && survival != null)
                {
                    loop.boolValue = true;
                    survival.boolValue = true;
                    runtimeObject.ApplyModifiedProperties();
                    report.Append("  MissionRuntime: chapterLoop = true, survivalExtension = true\n");
                }
                else
                {
                    report.Append("  WARNING: MissionRuntime has no chapterLoop field: merge feature/chapter-loop-core first.\n");
                }
            }
            else if (!runtime)
            {
                report.Append("  WARNING: no MissionRuntime in the scene.\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static MonoBehaviour Find(Scene scene, string typeName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(component => component && component.GetType().FullName == typeName);
        }

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}

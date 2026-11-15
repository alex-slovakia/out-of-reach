using OutOfReach.Presentation.UI.Chapter;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>A real UIDocument on a real panel with the project's theme and the chapter style sheet.</summary>
    public sealed class ChapterUiTestPanel
    {
        public const string StylePath = "Assets/_OutOfReach/UI/Chapter/Styles/ChapterUi.uss";
        public const string ThemePath = "Assets/_OutOfReach/UI/Styles/MissionRuntimeTheme.tss";

        public GameObject Host { get; }
        public UIDocument Document { get; }
        public MissionChapterPresenter Presenter { get; }
        public StyleSheet Style { get; }
        private readonly PanelSettings panel;

        public ChapterUiTestPanel()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
#if UNITY_EDITOR
            panel.themeStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            Style = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
#endif
            Host = new GameObject("ChapterUiTestPanel");
            Document = Host.AddComponent<UIDocument>();
            Document.panelSettings = panel;
            Presenter = Host.AddComponent<MissionChapterPresenter>();
            Presenter.DriveFromLateUpdate = false;
            Presenter.SetKeyboardShortcuts(false);
        }

        public VisualElement Root => Document.rootVisualElement;

        public string Text(string name) => Root.Q<Label>(name)?.text;

        public bool Shown(string name)
        {
            VisualElement element = Root.Q(name);
            for (VisualElement e = element; e != null; e = e.parent)
            {
                if (e.style.display == DisplayStyle.None) return false; // the chapter UI hides by inline style only
            }

            return element != null;
        }

        public void Destroy()
        {
            Object.DestroyImmediate(Host);
            Object.DestroyImmediate(panel);
        }
    }
}

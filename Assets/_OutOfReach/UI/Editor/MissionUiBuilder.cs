using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Editor
{
    /// <summary>Creates only UI-owned assets. Does not alter the open scene or project settings.</summary>
    public static class MissionUiBuilder
    {
        public const string StylePath = "Assets/_OutOfReach/UI/Styles/MissionFlightBoard.uss";
        public const string ThemePath = "Assets/_OutOfReach/UI/Styles/MissionRuntimeTheme.tss";
        public const string PanelPath = "Assets/_OutOfReach/UI/Generated/MissionPanelSettings.asset";
        public const string PrefabPath = "Assets/_OutOfReach/UI/Generated/MissionFlightBoard.prefab";

        [MenuItem("Tools/Out of Reach/UI/Build Planner and HUD Prefab")]
        public static void Build()
        {
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (style == null || theme == null)
                throw new InvalidOperationException("Import the owned UI style and theme assets before building the UI prefab.");

            if (!AssetDatabase.IsValidFolder("Assets/_OutOfReach/UI/Generated"))
                AssetDatabase.CreateFolder("Assets/_OutOfReach/UI", "Generated");

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.scale = 1f;
            panel.sortingOrder = 30;
            panel.themeStyleSheet = theme;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssetIfDirty(panel);

            var root = new GameObject("MissionFlightBoard");
            try
            {
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                var presenter = root.AddComponent<MissionFlightBoard>();
                var serialized = new SerializedObject(presenter);
                serialized.FindProperty("styleSheet").objectReferenceValue = style;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("The mission UI prefab could not be saved.");
                Debug.Log("V02 UI assets created. Bind MissionFlightBoard.Configure from the coordinated composition root: " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

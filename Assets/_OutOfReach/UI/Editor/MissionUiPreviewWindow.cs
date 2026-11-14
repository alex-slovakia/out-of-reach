using OutOfReach.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Editor
{
    /// <summary>Editor-only static layout preview. No domain services, simulation or callable mission controls.</summary>
    public sealed class MissionUiPreviewWindow : EditorWindow
    {
        private FlightBoardView view;

        [MenuItem("Tools/Out of Reach/UI/Open Read-only Layout Preview")]
        public static void Open()
        {
            var window = GetWindow<MissionUiPreviewWindow>();
            window.titleContent = new GUIContent("AURORA-7 UI Preview");
            window.minSize = new Vector2(1000, 600);
            window.Show();
        }

        public void CreateGUI()
        {
            view?.Dispose();
            rootVisualElement.Clear();
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexShrink = 0;
            toolbar.style.paddingTop = 6;
            toolbar.style.paddingBottom = 6;
            var planner = new Button(() => view.ShowPlanning(true)) { text = "Planner layout" };
            var hud = new Button(() => view.ShowPlanning(false)) { text = "HUD layout" };
            toolbar.Add(planner);
            toolbar.Add(hud);
            var notice = new Label("STATIC AUTHORING PREVIEW — MISSION CONTROLS DISABLED");
            notice.style.marginLeft = 12;
            notice.style.fontSize = 16;
            toolbar.Add(notice);
            rootVisualElement.Add(toolbar);
            var canvas = new VisualElement();
            canvas.style.flexGrow = 1;
            canvas.style.backgroundColor = new Color(0.14f, 0.18f, 0.21f);
            rootVisualElement.Add(canvas);

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(MissionUiBuilder.StylePath);
            view = new FlightBoardView(canvas, style, false);
            // Layout examples only: never exposed as a mission configuration or live game state.
            var catalog = new[]
            {
                new EquipmentSelection("oxygen-tank", 1, 30, 0, 40),
                new EquipmentSelection("water-tank", 1, 48, 0, 20),
                new EquipmentSelection("food-locker", 1, 35, 0, 25),
                new EquipmentSelection("battery-module", 1, 55, 0, 60),
                new EquipmentSelection("solar-wing", 1, 70, 0, 120),
                new EquipmentSelection("spare-parts-kit", 1, 25, 0, 45),
                new EquipmentSelection("spectrometer", 1, 90, 300, 220)
            };
            var selections = new[]
            {
                new EquipmentSelection("oxygen-tank", 2, 30, 0, 40),
                new EquipmentSelection("water-tank", 2, 48, 0, 20),
                new EquipmentSelection("food-locker", 2, 35, 0, 25),
                new EquipmentSelection("battery-module", 2, 55, 0, 60),
                new EquipmentSelection("solar-wing", 1, 70, 0, 120),
                new EquipmentSelection("spare-parts-kit", 2, 25, 0, 45),
                new EquipmentSelection("spectrometer", 1, 90, 300, 220)
            };
            var reserves = new ResourceSnapshot(34, 100, 64, 28, 7, 1000);
            view.SetEquipment(catalog, selections, false);
            view.SetConstraints(546, 600, 720, 800, 1400, 1900);
            view.SetPlanningResources(reserves);
            view.SetHudResources(reserves);
            view.SetPhase(GamePhase.Repair, MissionOutcome.None);
            view.SetValidation(false, "Static layout preview. No mission is running.", string.Empty);
            view.ShowPlanning(true);
        }

        private void OnDisable()
        {
            view?.Dispose();
            view = null;
        }
    }
}

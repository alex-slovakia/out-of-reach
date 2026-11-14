using OutOfReach.Contracts;
using OutOfReach.Presentation.UI;
using OutOfReach.Presentation.UI.Report;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Composition
{
    /// <summary>Small composition-owned mode/pause controls. Buttons only invoke the real runtime commands.</summary>
    [DisallowMultipleComponent]
    public sealed class I01SessionControls : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Font font;
        [SerializeField] private StyleSheet reportStyleSheet;

        private I01MissionComposition composition;
        private MissionFlightBoard flightBoard;
        private VisualElement root;
        private VisualElement modal;
        private VisualElement panel;
        private Label heading;
        private Label detail;
        private Button primary;
        private Button secondary;
        private Toggle reduced;
        private Label feedback;
        private Label controlsHint;
        private Label communications;
        private VisualElement reportHost;
        private FlightRecorderPresenter recorder;
        private IMissionReport displayedReport;
        private bool showingMode;
        private bool showingOutcome;
        private float feedbackUntil;

        public void ConfigureDocument(UIDocument boundDocument, Font bodyFont, StyleSheet reportStyles = null)
        {
            document = boundDocument;
            font = bodyFont;
            reportStyleSheet = reportStyles;
        }

        public void Configure(I01MissionComposition owner)
        {
            composition = owner;
            EnsureView();
            Refresh();
        }

        public void ClearBinding()
        {
            composition = null;
            ReleaseView();
        }

        private void OnEnable()
        {
            if (composition) { EnsureView(); Refresh(); }
        }

        private void OnDisable() => ReleaseView();

        private void LateUpdate()
        {
            if (composition && root == null) { EnsureView(); Refresh(); }
            if (feedback != null && feedback.style.display.value != DisplayStyle.None && Time.unscaledTime >= feedbackUntil)
                feedback.style.display = DisplayStyle.None;
            RefreshCommunications();
        }

        public void Refresh()
        {
            EnsureView();
            if (root == null || !composition || !composition.Runtime || composition.Runtime.GameState == null)
                return;
            root.BringToFront();
            var state = composition.Runtime.GameState;
            bool planning = state.Phase == GamePhase.Planning;
            bool paused = composition.Runtime.IsPaused;
            showingMode = planning && !composition.ModeChosen && !paused;
            showingOutcome = state.Phase == GamePhase.Report;
            bool showRecord = showingOutcome && composition.Runtime.Report != null && reportStyleSheet;
            reportHost.style.display = showRecord ? DisplayStyle.Flex : DisplayStyle.None;
            if (showRecord)
            {
                if (recorder == null)
                {
                    recorder = new FlightRecorderPresenter(reportHost, reportStyleSheet);
                    recorder.RestartRequested += OnReportRestart;
                }
                if (!ReferenceEquals(displayedReport, composition.Runtime.Report))
                {
                    displayedReport = composition.Runtime.Report;
                    recorder.Configure(displayedReport);
                }
            }
            else if (displayedReport != null)
            {
                recorder?.Clear();
                displayedReport = null;
            }
            bool visible = showingMode || paused || showingOutcome;
            if (flightBoard) flightBoard.SetMenuOverlayVisible(showingMode || paused);
            modal.style.display = visible && !showRecord ? DisplayStyle.Flex : DisplayStyle.None;
            controlsHint.style.display = paused && !showingOutcome ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) feedback.style.display = DisplayStyle.None;
            RefreshCommunications();
            if (!visible) return;

            heading.text = showingMode ? "Choose planning mode" : showingOutcome ? "Mission ended" : "Paused";
            detail.text = showingMode
                ? "60 active real seconds. At zero, launch the valid loadout or the last valid loadout. Carried cargo is not aboard. Practice has no deadline."
                : showingOutcome
                    ? "Review your mission record."
                    : string.Empty;
            detail.style.display = string.IsNullOrEmpty(detail.text) ? DisplayStyle.None : DisplayStyle.Flex;
            primary.text = showingMode ? "Start timed · 60 s" : showingOutcome ? "Restart mission" : "Resume";
            secondary.text = showingMode ? "Practice · no deadline" : "Restart mission";
            secondary.style.display = showingOutcome ? DisplayStyle.None : DisplayStyle.Flex;
            reduced.SetValueWithoutNotify(composition.Cinematics && composition.Cinematics.ReducedEffects);
        }

        public void ShowFeedback(string text, float seconds = 4f)
        {
            EnsureView();
            if (feedback == null) return;
            feedback.text = text ?? string.Empty;
            feedback.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
            feedbackUntil = Time.unscaledTime + Mathf.Max(.5f, seconds);
        }

        private void EnsureView()
        {
            if (root != null || !document || !isActiveAndEnabled) return;
            VisualElement host = document.rootVisualElement;
            if (host == null) return;
            flightBoard = document.GetComponent<MissionFlightBoard>();
            root = new VisualElement { name = "i01-session-controls", pickingMode = PickingMode.Ignore };
            root.AddToClassList("session-controls");
            if (flightBoard && flightBoard.SharedStyleSheet) root.styleSheets.Add(flightBoard.SharedStyleSheet);
            Fill(root);
            root.style.color = new Color(.92f, .93f, .93f);
            root.style.fontSize = 16;
            if (font) root.style.unityFont = font;
            host.Add(root);

            modal = new VisualElement { name = "i01-mode-pause-overlay", pickingMode = PickingMode.Position };
            Fill(modal);
            modal.AddToClassList("session-menu");
            root.Add(modal);
            panel = new VisualElement();
            panel.AddToClassList("session-menu-panel");
            modal.Add(panel);
            heading = new Label();
            heading.AddToClassList("session-menu-heading");
            panel.Add(heading);
            detail = new Label();
            detail.AddToClassList("session-menu-detail");
            primary = ActionButton(OnPrimary, true);
            secondary = ActionButton(OnSecondary, false);
            panel.Add(primary);
            panel.Add(secondary);
            panel.Add(detail);
            reduced = new Toggle("Reduced camera motion and light effects");
            reduced.AddToClassList("session-reduced");
            reduced.RegisterValueChangedCallback(OnReducedChanged);
            panel.Add(reduced);
            feedback = new Label { pickingMode = PickingMode.Ignore };
            feedback.AddToClassList("session-menu-feedback");
            feedback.style.display = DisplayStyle.None;
            panel.Add(feedback);
            controlsHint = new Label("WASD move · Mouse look\nE interact · Tab systems · Esc resume") { pickingMode = PickingMode.Ignore };
            controlsHint.AddToClassList("session-menu-controls");
            panel.Add(controlsHint);
            communications = new Label { pickingMode = PickingMode.Ignore };
            communications.style.position = Position.Absolute;
            communications.style.right = 28;
            communications.style.top = 80;
            communications.style.width = 360;
            communications.style.fontSize = 15;
            communications.style.whiteSpace = WhiteSpace.Normal;
            communications.style.unityTextAlign = TextAnchor.UpperRight;
            root.Add(communications);
            reportHost = new VisualElement { name = "mission-record-host" };
            Fill(reportHost);
            reportHost.style.left = reportHost.style.right = Length.Percent(6);
            reportHost.style.top = reportHost.style.bottom = Length.Percent(5);
            reportHost.style.display = DisplayStyle.None;
            root.Add(reportHost);
        }

        private void RefreshCommunications()
        {
            if (communications == null || !composition || !composition.Runtime || composition.Runtime.GameState == null) return;
            bool visible = composition.Runtime.GameState.Phase == GamePhase.Transmission
                && !composition.Runtime.IsPaused && !showingMode;
            communications.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible || composition.Runtime.Communications == null) return;
            var link = composition.Runtime.Communications;
            string distance = (System.Math.Round(link.ReferenceDistanceKm / 1000d) * 1000d).ToString("N0", CultureInfo.InvariantCulture);
            string wait = link.State == CommunicationState.AwaitingReply
                ? "Reply pending · " + Duration(link.ReplyRemainingSimulationSeconds) + " simulated remaining"
                : link.State == CommunicationState.ReplyReceived ? "Delayed reply received" : "Use the cockpit console";
            string epoch = composition.Runtime.Calendar != null
                ? composition.Runtime.Calendar.NowUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) + "\n" : string.Empty;
            communications.text = "EARTH LINK\n" + epoch + distance + " km · Mars-vicinity proxy\n"
                + "Light time " + Duration(link.OneWayLightSeconds) + " one way\n"
                + "Ground processing " + Duration(link.EarthProcessingSeconds) + "\n" + wait;
        }

        private static string Duration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "unavailable";
            long whole = (long)System.Math.Ceiling(System.Math.Max(0, seconds));
            return (whole / 60).ToString(CultureInfo.InvariantCulture) + "m " + (whole % 60).ToString("00", CultureInfo.InvariantCulture) + "s";
        }

        private void OnReportRestart() { if (composition) composition.Restart(); }

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = element.style.right = element.style.top = element.style.bottom = 0;
        }

        private static Button ActionButton(System.Action handler, bool highlighted)
        {
            var button = new Button(handler);
            button.AddToClassList("session-menu-action");
            button.EnableInClassList("session-menu-primary", highlighted);
            return button;
        }

        private void OnPrimary()
        {
            if (!composition) return;
            if (showingMode) composition.StartTimedPlanning();
            else if (showingOutcome) composition.Restart();
            else composition.Resume();
        }

        private void OnSecondary()
        {
            if (!composition) return;
            if (showingMode) composition.StartPractice();
            else composition.Restart();
        }

        private void OnReducedChanged(ChangeEvent<bool> change)
        {
            if (composition) composition.SetReducedEffects(change.newValue);
        }

        private void ReleaseView()
        {
            if (flightBoard) flightBoard.SetMenuOverlayVisible(false);
            flightBoard = null;
            if (recorder != null)
            {
                recorder.RestartRequested -= OnReportRestart;
                recorder.Dispose();
                recorder = null;
            }
            displayedReport = null;
            if (reduced != null) reduced.UnregisterValueChangedCallback(OnReducedChanged);
            if (root != null) root.RemoveFromHierarchy();
            root = modal = panel = null;
            heading = detail = feedback = controlsHint = communications = null;
            reportHost = null;
            primary = secondary = null;
            reduced = null;
        }
    }
}

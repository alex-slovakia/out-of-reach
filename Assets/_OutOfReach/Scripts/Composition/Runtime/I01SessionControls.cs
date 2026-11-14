using OutOfReach.Contracts;
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
            modal.style.display = visible && !showRecord ? DisplayStyle.Flex : DisplayStyle.None;
            controlsHint.style.display = !planning && !visible ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshCommunications();
            if (!visible) return;

            heading.text = showingMode ? "Choose your planning mode" : showingOutcome ? "Mission ended" : "Paused";
            detail.text = showingMode
                ? "Timed planning gives you 60 active seconds. If your draft is invalid at expiry, the mission launches with the last valid loadout shown in launch details. Practice has no deadline."
                : showingOutcome
                    ? "Review the mission record for the outcome, your equipment choices and the sources behind the simulation."
                    : "Mission time, the planning deadline and audio are paused.\nWASD to move · Mouse to look · E to interact · Tab for systems";
            primary.text = showingMode ? "Start timed · 60 s" : showingOutcome ? "Restart mission" : "Resume";
            secondary.text = showingMode ? "Practice · no deadline" : "Restart mission";
            secondary.style.display = showingOutcome ? DisplayStyle.None : DisplayStyle.Flex;
            reduced.SetValueWithoutNotify(composition.Cinematics && composition.Cinematics.ReducedEffects);
            panel.style.maxWidth = showingMode ? 430 : 480;
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
            root = new VisualElement { name = "i01-session-controls", pickingMode = PickingMode.Ignore };
            Fill(root);
            root.style.color = new Color(.92f, .93f, .93f);
            root.style.fontSize = 16;
            if (font) root.style.unityFont = font;
            host.Add(root);

            modal = new VisualElement { name = "i01-mode-pause-overlay", pickingMode = PickingMode.Position };
            Fill(modal);
            modal.style.backgroundColor = new Color(.025f, .035f, .045f, .55f);
            modal.style.justifyContent = Justify.Center;
            modal.style.alignItems = Align.FlexStart;
            modal.style.paddingLeft = Length.Percent(6);
            modal.style.paddingRight = 24;
            root.Add(modal);
            panel = new VisualElement();
            panel.style.backgroundColor = new Color(.045f, .06f, .075f, .97f);
            panel.style.width = Length.Percent(90);
            panel.style.maxWidth = 430;
            panel.style.paddingLeft = panel.style.paddingRight = 26;
            panel.style.paddingTop = panel.style.paddingBottom = 26;
            modal.Add(panel);
            heading = new Label();
            heading.style.fontSize = 26;
            heading.style.whiteSpace = WhiteSpace.Normal;
            heading.style.marginBottom = 14;
            panel.Add(heading);
            detail = new Label();
            detail.style.fontSize = 16;
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.color = new Color(.72f, .77f, .79f);
            detail.style.marginBottom = 20;
            panel.Add(detail);
            primary = ActionButton(OnPrimary, true);
            secondary = ActionButton(OnSecondary, false);
            panel.Add(primary);
            panel.Add(secondary);
            reduced = new Toggle("Reduced camera motion and light effects");
            reduced.style.marginTop = 16;
            reduced.style.whiteSpace = WhiteSpace.Normal;
            reduced.RegisterValueChangedCallback(OnReducedChanged);
            panel.Add(reduced);
            var scope = new Label("Simplified mission simulation · Sources and assumptions appear in the mission record.");
            scope.style.fontSize = 13;
            scope.style.color = new Color(.62f, .68f, .7f);
            scope.style.whiteSpace = WhiteSpace.Normal;
            scope.style.marginTop = 18;
            panel.Add(scope);

            feedback = new Label { pickingMode = PickingMode.Ignore };
            feedback.style.position = Position.Absolute;
            feedback.style.left = Length.Percent(32);
            feedback.style.right = Length.Percent(32);
            feedback.style.bottom = 112;
            feedback.style.fontSize = 14;
            feedback.style.whiteSpace = WhiteSpace.Normal;
            feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
            feedback.style.backgroundColor = new Color(.03f, .04f, .05f, .91f);
            feedback.style.paddingLeft = feedback.style.paddingRight = 14;
            feedback.style.paddingTop = feedback.style.paddingBottom = 10;
            feedback.style.display = DisplayStyle.None;
            root.Add(feedback);
            controlsHint = new Label("WASD move · Mouse look · E interact · Esc pause") { pickingMode = PickingMode.Ignore };
            controlsHint.style.position = Position.Absolute;
            controlsHint.style.left = 28;
            controlsHint.style.bottom = 20;
            controlsHint.style.fontSize = 13;
            controlsHint.style.color = new Color(.7f, .76f, .79f);
            root.Add(controlsHint);
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
            bool visible = composition.Runtime.GameState.Phase == GamePhase.Transmission;
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
            button.style.height = 46;
            button.style.marginTop = 6;
            button.style.marginLeft = button.style.marginRight = 0;
            button.style.fontSize = 17;
            button.style.backgroundColor = highlighted ? new Color(.87f, .65f, .29f) : new Color(.12f, .16f, .18f);
            button.style.color = highlighted ? new Color(.06f, .07f, .08f) : new Color(.9f, .92f, .92f);
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

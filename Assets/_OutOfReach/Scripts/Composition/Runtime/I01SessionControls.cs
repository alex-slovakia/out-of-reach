using OutOfReach.Contracts;
using OutOfReach.Presentation.UI;
using OutOfReach.Presentation.UI.Report;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Composition
{
    /// <summary>
    /// Small composition-owned mode/pause controls. Buttons only invoke the real runtime commands.
    /// QUIT-TO-DESKTOP-01: every menu (planning-mode choice, Paused, Mission ended and the mission record end card)
    /// offers "Quit to desktop". The first press only arms it ("Click again to quit") for a few real seconds; a second
    /// press inside that window runs the quit action once. A changed or hidden menu disarms it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class I01SessionControls : MonoBehaviour
    {
        public const string PrimaryButtonName = "session-primary-action";
        public const string QuitLabel = "Quit to desktop";
        public const string QuitConfirmLabel = "Click again to quit";
        public const string QuittingLabel = "Quitting…";
        public const string QuitButtonName = "session-quit-to-desktop";
        public const string RecordQuitButtonName = "reportQuitButton";
        /// <summary>The mission record end card's action column (FlightRecorderPresenter), where its quit button goes.</summary>
        public const string RecordActionsName = "reportEndActions";
        public const float DefaultQuitConfirmSeconds = 3f;

        // Pause help follows the active movement mode and authored contact support.
        public const string WalkingControlsHint = "WASD move · Shift sprint · Mouse look\nE interact · Tab systems · Esc resume";
        public const string RouteControlsHint = "W/S move along the tether · Mouse look\nE interact · Tab systems · Esc resume";
        public const string FloatingPushHint = "Left/right click a surface in reach: push off\n"
            + "Both mouse buttons: push with both hands\n"
            + "WASD only aims a push: hold it while clicking\n"
            + "Space: foot push, only while your feet touch support";
        public const string SuitedRailHint = "Ctrl + click a reachable rail to grip · Click to release";
        public const string FloatingSystemsHint = "Mouse look · E use equipment · Tab systems · Esc resume";

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
        private bool viewHydrated;
        private bool showingMode;
        private bool showingOutcome;
        private float feedbackUntil;
        private Button quit;
        private Button recordQuit;
        private System.Action quitAction;
        private readonly SessionQuitConfirmation quitConfirmation = new SessionQuitConfirmation();
        private float quitConfirmSeconds = DefaultQuitConfirmSeconds;
        private int quitMenuContext;
        private bool quitRequested;

        public void ConfigureDocument(UIDocument boundDocument, Font bodyFont, StyleSheet reportStyles = null)
        {
            document = boundDocument;
            font = bodyFont;
            reportStyleSheet = reportStyles;
        }

        /// <summary>Real seconds the first quit press stays armed (default 3, clamped to 0.25–10).</summary>
        public float QuitConfirmSeconds
        {
            get => quitConfirmSeconds;
            set => quitConfirmSeconds = float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultQuitConfirmSeconds : Mathf.Clamp(value, .25f, 10f);
        }

        /// <summary>True while the first quit press waits for its confirmation.</summary>
        public bool IsQuitArmed => quitConfirmation.IsArmed;

        /// <summary>True once a confirmed press has handed the quit to the quit action.</summary>
        public bool QuitRequested => quitRequested;

        /// <summary>
        /// Replaces the quit action so native checks and tests can assert it without closing anything. Null restores
        /// the real action: Application.Quit, and leaving Play Mode in the editor.
        /// </summary>
        public void ConfigureQuitAction(System.Action action)
        {
            quitAction = action;
            quitRequested = false;
            quitConfirmation.Disarm();
            RefreshQuitButtons();
        }

        public void Configure(I01MissionComposition owner)
        {
            composition = owner;
            EnsureView();
            Refresh();
        }

        public void ClearBinding()
        {
            ReleaseView();
            composition = null;
        }

        private void OnEnable()
        {
            if (composition) { EnsureView(); Refresh(); }
        }

        private void OnDisable() => ReleaseView();

        private void LateUpdate()
        {
            if (composition && (root == null || !viewHydrated)) { EnsureView(); Refresh(); }
            if (feedback != null && feedback.style.display.value != DisplayStyle.None && Time.unscaledTime >= feedbackUntil)
                feedback.style.display = DisplayStyle.None;
            if (quitConfirmation.Expire(Time.unscaledTime, quitConfirmSeconds)) RefreshQuitButtons();
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
                    recorder.MenuFeedbackRequested += OnReportMenuFeedback;
                    // The record replaces the session menu when the mission ends: its end card gets the same quit.
                    recordQuit = AddRecordQuitButton(recorder.Root, () => OnQuit(recordQuit), () => { if (composition) composition.PlayMenuCue(); });
                    RefreshQuitButtons();
                }
                recorder.SetReducedMotion(composition.Cinematics && composition.Cinematics.ReducedEffects);
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
            // Populate a rebuilt view before exposing it to input or rendering.
            heading.text = showingMode ? "Choose planning mode" : showingOutcome ? "Mission ended" : "Paused";
            detail.text = showingMode
                ? composition.Runtime.BoardAfterLoadingDeadline
                    ? "60 seconds to secure supplies. Then loading closes: board the transport and secure your seat. Carried cargo stays behind. Practice has no deadline."
                    : "60 active real seconds. At zero, launch the valid loadout or the last valid loadout. Carried cargo is not aboard. Practice has no deadline."
                : showingOutcome
                    ? "Review your mission record."
                    : string.Empty;
            detail.style.display = string.IsNullOrEmpty(detail.text) ? DisplayStyle.None : DisplayStyle.Flex;
            primary.text = showingMode ? "Start timed · 60 s" : showingOutcome ? "Restart mission" : "Resume";
            secondary.text = showingMode ? "Practice · no deadline" : "Restart mission";
            secondary.style.display = showingOutcome ? DisplayStyle.None : DisplayStyle.Flex;
            reduced.SetValueWithoutNotify(composition.Cinematics && composition.Cinematics.ReducedEffects);
            viewHydrated = true;
            bool visible = showingMode || paused || showingOutcome;
            // An armed quit belongs to the menu it was pressed in: any other or no menu disarms it.
            int menuContext = !visible ? 0 : showRecord ? (recordQuit != null && IsShown(recordQuit) ? 4 : 5)
                : showingOutcome ? 3 : showingMode ? 1 : 2;
            if (menuContext != quitMenuContext)
            {
                quitMenuContext = menuContext;
                quitConfirmation.Disarm();
            }
            RefreshQuitButtons();
            if (flightBoard) flightBoard.SetMenuOverlayVisible(showingMode || paused);
            composition.SetMenuPresentationVisible(visible);
            modal.EnableInClassList("reduced-motion", composition.Cinematics && composition.Cinematics.ReducedEffects);
            bool menuShown = visible && !showRecord;
            modal.style.display = menuShown ? DisplayStyle.Flex : DisplayStyle.None;
            // A hidden menu keeps no keyboard/gamepad focus, so play input (gamepad South is Interact) cannot reach it.
            if (!menuShown) BlurFocusWithin(modal);
            controlsHint.style.display = paused && !showingOutcome ? DisplayStyle.Flex : DisplayStyle.None;
            if (paused && !showingOutcome)
            {
                var player = composition.Runtime.Player;
                var eva = composition.Runtime.Eva;
                controlsHint.text = ControlsHintFor(player && player.IsFloating, player && player.IsOnRoute,
                    composition.HasInteriorRailGrip || (eva != null && eva.IsSuitOn));
            }
            if (!visible) feedback.style.display = DisplayStyle.None;
            RefreshCommunications();
            if (!visible) return;


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
            modal.style.display = DisplayStyle.None; // Configure/Refresh reveals only a fully bound menu.
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
            primary.name = PrimaryButtonName;
            secondary = ActionButton(OnSecondary, false);
            quit = ActionButton(() => OnQuit(quit), false);
            quit.name = QuitButtonName;
            panel.Add(primary);
            panel.Add(secondary);
            panel.Add(quit);
            panel.Add(detail);
            reduced = new Toggle("Reduced camera motion and light effects");
            reduced.AddToClassList("session-reduced");
            reduced.RegisterValueChangedCallback(OnReducedChanged);
            panel.Add(reduced);
            feedback = new Label { pickingMode = PickingMode.Ignore };
            feedback.AddToClassList("session-menu-feedback");
            feedback.style.display = DisplayStyle.None;
            panel.Add(feedback);
            controlsHint = new Label(WalkingControlsHint) { pickingMode = PickingMode.Ignore };
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
            reportHost.style.left = reportHost.style.right = 0;
            reportHost.style.top = reportHost.style.bottom = 0;
            reportHost.style.display = DisplayStyle.None;
            root.Add(reportHost);
            RefreshQuitButtons();
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

        private Button ActionButton(System.Action handler, bool highlighted)
        {
            var button = new Button(handler);
            button.AddToClassList("session-menu-action");
            button.EnableInClassList("session-menu-primary", highlighted);
            button.RegisterCallback<PointerEnterEvent>(_ => { if (composition) composition.PlayMenuCue(); });
            button.RegisterCallback<FocusInEvent>(_ => { if (composition) composition.PlayMenuCue(); });
            return button;
        }

        private void OnPrimary()
        {
            // A closed menu's focused button must not start, resume or restart from a stray Enter in play.
            if (!composition || !IsShown(primary)) return;
            if (showingMode) composition.StartTimedPlanning();
            else if (showingOutcome) composition.Restart();
            else composition.Resume();
            composition.PlayMenuCue(true);
        }

        private void OnSecondary()
        {
            if (!composition || !IsShown(secondary)) return;
            if (showingMode) composition.StartPractice();
            else composition.Restart();
            composition.PlayMenuCue(true);
        }

        private void OnReducedChanged(ChangeEvent<bool> change)
        {
            // A stray submit into a closed menu flips the toggle: put the shown value back and change nothing.
            // Programmatic SetValueWithoutNotify (Refresh) sends no change event and is never affected.
            if (reduced != null && !IsShown(reduced))
            {
                reduced.SetValueWithoutNotify(change.previousValue);
                return;
            }

            if (composition) composition.SetReducedEffects(change.newValue);
        }

        /// <summary>
        /// Clears keyboard/gamepad focus held by <paramref name="container"/> or anything inside it; focus elsewhere
        /// is left alone. Refresh calls it for the session menu whenever that menu is hidden.
        /// </summary>
        public static void BlurFocusWithin(VisualElement container)
        {
            if (container?.focusController?.focusedElement is VisualElement focused
                && (focused == container || container.Contains(focused)))
                focused.Blur();
        }

        /// <summary>
        /// The pause help for the player's actual movement mode. Floating wins over a tether route (the controller
        /// gives floating no WASD thrust either way). Show rail controls when the current outfit supports grips.
        /// </summary>
        public static string ControlsHintFor(bool floating, bool onRoute, bool supportsRailGrip)
        {
            if (!floating) return onRoute ? RouteControlsHint : WalkingControlsHint;
            return FloatingPushHint + (supportsRailGrip ? "\n" + SuitedRailHint : string.Empty) + "\n" + FloatingSystemsHint;
        }

        /// <summary>
        /// Appends a "Quit to desktop" button to a mission record end card, after "Plan again", with the record's own
        /// button style. Returns the button; its click runs <paramref name="onQuit"/>, hover and focus run
        /// <paramref name="onCue"/>. No UI file is changed: the record's action column is found by name.
        /// </summary>
        public static Button AddRecordQuitButton(VisualElement recordRoot, System.Action onQuit, System.Action onCue = null)
        {
            if (recordRoot == null) throw new System.ArgumentNullException(nameof(recordRoot));
            if (onQuit == null) throw new System.ArgumentNullException(nameof(onQuit));
            VisualElement actions = recordRoot.Q<VisualElement>(RecordActionsName);
            if (actions == null)
                Debug.LogWarning("I01SessionControls: the mission record has no '" + RecordActionsName + "'; its quit button goes to the record root.");
            var button = new Button(onQuit) { name = RecordQuitButtonName, text = QuitLabel };
            button.AddToClassList("recorder-button");
            if (onCue != null)
            {
                button.RegisterCallback<PointerEnterEvent>(_ => onCue());
                button.RegisterCallback<FocusInEvent>(_ => onCue());
            }
            (actions ?? recordRoot).Add(button);
            return button;
        }

        private void OnQuit(Button source)
        {
            // A hidden menu keeps no say: a button that kept keyboard focus after its menu closed must not arm or quit.
            if (quitRequested || source == null || !source.enabledInHierarchy || !IsShown(source)) return;
            if (!quitConfirmation.Press(Time.unscaledTime, quitConfirmSeconds))
            {
                // The first press only arms: one stray click never closes the game.
                RefreshQuitButtons();
                if (composition) composition.PlayMenuCue(true);
                return;
            }
            quitRequested = true;
            RefreshQuitButtons();
            if (composition) composition.PlayMenuCue(true);
            try { (quitAction ?? QuitApplication).Invoke(); }
            catch
            {
                quitRequested = false;
                RefreshQuitButtons();
                throw;
            }
        }

        // UI Toolkit still delivers submit (Enter, gamepad South) to a focused button inside a hidden menu. The menus are
        // hidden by inline display (Refresh) or the record's "recorder-hidden" class; both are current immediately,
        // unlike resolved styles, so a press right after a menu opens is never refused.
        private static bool IsShown(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.style.display == DisplayStyle.None || current.ClassListContains("recorder-hidden")) return false;
            }

            return true;
        }

        private void RefreshQuitButtons()
        {
            string text = quitRequested ? QuittingLabel : quitConfirmation.IsArmed ? QuitConfirmLabel : QuitLabel;
            if (quit != null) { quit.text = text; quit.SetEnabled(!quitRequested); }
            if (recordQuit != null) { recordQuit.text = text; recordQuit.SetEnabled(!quitRequested); }
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        private void ReleaseView()
        {
            if (flightBoard) flightBoard.SetMenuOverlayVisible(false);
            if (composition) composition.SetMenuPresentationVisible(false);
            flightBoard = null;
            if (recorder != null)
            {
                recorder.RestartRequested -= OnReportRestart;
                recorder.MenuFeedbackRequested -= OnReportMenuFeedback;
                recorder.Dispose();
                recorder = null;
            }
            displayedReport = null;
            if (reduced != null) reduced.UnregisterValueChangedCallback(OnReducedChanged);
            if (root != null) root.RemoveFromHierarchy();
            root = modal = panel = null;
            viewHydrated = false;
            heading = detail = feedback = controlsHint = communications = null;
            reportHost = null;
            primary = secondary = quit = recordQuit = null;
            reduced = null;
            quitConfirmation.Disarm();
            quitMenuContext = 0;
        }

        private void OnReportMenuFeedback(bool confirm)
        {
            if (composition) composition.PlayMenuCue(confirm);
        }
    }

    /// <summary>
    /// QUIT-TO-DESKTOP-01 two-step confirmation: the first press only arms for a window of real seconds; a second press
    /// inside that window confirms. A press after the window (or with time running backwards) arms again instead.
    /// </summary>
    public sealed class SessionQuitConfirmation
    {
        private float armedAt;

        public bool IsArmed { get; private set; }

        /// <summary>True when this press confirms; false when it only armed the confirmation.</summary>
        public bool Press(float now, float windowSeconds)
        {
            Validate(now, windowSeconds);
            if (IsArmed && Inside(now, windowSeconds))
            {
                IsArmed = false;
                return true;
            }

            IsArmed = true;
            armedAt = now;
            return false;
        }

        /// <summary>Disarms a confirmation whose window has passed; true when that changed the state.</summary>
        public bool Expire(float now, float windowSeconds)
        {
            Validate(now, windowSeconds);
            if (!IsArmed || Inside(now, windowSeconds)) return false;
            IsArmed = false;
            return true;
        }

        public void Disarm() => IsArmed = false;

        private bool Inside(float now, float windowSeconds) => now >= armedAt && now - armedAt <= windowSeconds;

        private static void Validate(float now, float windowSeconds)
        {
            if (float.IsNaN(now) || float.IsInfinity(now))
                throw new System.ArgumentOutOfRangeException(nameof(now), now, "Time must be finite.");
            if (float.IsNaN(windowSeconds) || float.IsInfinity(windowSeconds) || windowSeconds <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "The window must be finite and above zero.");
        }
    }
}

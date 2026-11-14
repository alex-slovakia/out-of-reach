using System;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Optional view of the existing survival services. Only the Wake button issues a command.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(260)]
    public sealed class SurvivalChapterPresenter : MonoBehaviour
    {
        private UIDocument document;
        private StyleSheet sheet;
        private ISurvivalExtension survival;
        private ISurvivalRecovery recovery;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> presentationAllowed;
        private bool subscribed;
        private VisualElement needsHost, detailsHost, restHost, needs, details, rest;
        private NeedRow hunger, hydration;
        private Label restDay, restTime, restFeedback, recoveryTitle, arrival, beacon;
        private Button wake;

        /// <param name="canPresent">Root-owned visibility gate, including menu, terminal and cinematic locks.
        /// Must allow actual rest even if the player's movement control is locked. Null fails closed.</param>
        public void Configure(UIDocument ui, StyleSheet styleSheet, ISurvivalExtension needsService,
            ISurvivalRecovery recoveryService, IGameState gameState, ISimulationClock simulationClock,
            Func<bool> canPresent)
        {
            ClearBinding();
            document = ui;
            sheet = styleSheet;
            survival = needsService;
            recovery = recoveryService;
            state = gameState;
            clock = simulationClock;
            presentationAllowed = canPresent;
            if (isActiveAndEnabled) Subscribe();
            RefreshPresentation();
        }

        public void ClearBinding()
        {
            Unsubscribe();
            ReleaseViews();
            document = null;
            sheet = null;
            survival = null;
            recovery = null;
            state = null;
            clock = null;
            presentationAllowed = null;
        }

        private void OnEnable() { Subscribe(); RefreshPresentation(); }
        private void OnDisable() { Unsubscribe(); ReleaseViews(); }
        private void OnDestroy() => ClearBinding();
        // Clock/state have no change event. Sample their values after Core; never integrate deltaTime.
        private void LateUpdate() => RefreshPresentation();

        private void Subscribe()
        {
            if (subscribed) return;
            if (survival != null) survival.Changed += OnServiceChanged;
            if (recovery != null) recovery.Changed += OnServiceChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (survival != null) survival.Changed -= OnServiceChanged;
            if (recovery != null) recovery.Changed -= OnServiceChanged;
            subscribed = false;
        }

        private void OnServiceChanged() => RefreshPresentation();

        private bool CanPresent() => isActiveAndEnabled && document && document.isActiveAndEnabled && sheet
            && SurvivalChapterReadout.CanShow(survival, state, clock)
            && presentationAllowed != null && presentationAllowed();

        public void RefreshPresentation()
        {
            bool visible = CanPresent();
            if (!visible) { HideViews(); return; }
            if (!EnsureViews()) return;
            Show(needs, true);
            hunger.Set(survival.Hunger01, false);
            hydration.Set(survival.Thirst01, true);

            bool resting = recovery != null && recovery.IsResting;
            Show(rest, resting);
            wake.SetEnabled(resting);
            if (resting)
            {
                restDay.text = recovery.MissionDay > 0
                    ? "DAY " + recovery.MissionDay.ToString(CultureInfo.InvariantCulture) : "DAY —";
                restTime.text = "Resting · " + SurvivalChapterReadout.Remaining(recovery.RestRemainingSimulationSeconds)
                    + " remaining (simulated)";
            }
            else
            {
                restFeedback.text = string.Empty;
                Show(restFeedback, false);
            }

            // The existing expanded-telemetry parent owns Tab/pinned visibility. No second detail toggle/input.
            Show(details, recovery != null);
            if (recovery == null) return;
            recoveryTitle.text = SurvivalChapterReadout.StageLabel(recovery.Stage);
            arrival.text = recovery.ArrivalAtSimulationSeconds.HasValue
                ? "Rendezvous in " + SurvivalChapterReadout.Remaining(recovery.ArrivalRemainingSimulationSeconds) + " (simulated)"
                : "Arrival time unconfirmed";
            Show(arrival, recovery.Stage != RecoveryStage.Arrived);
            double? ends = recovery.BeaconWindowEndsAtSimulationSeconds;
            bool window = recovery.Stage == RecoveryStage.BeaconRequested && ends.HasValue;
            Show(beacon, window);
            if (window)
                beacon.text = SurvivalChapterReadout.BeaconWindow(ends.Value, clock.ElapsedSimulationSeconds);
        }

        private bool EnsureViews()
        {
            VisualElement board = document.rootVisualElement?.Q("mission-flight-board");
            VisualElement nextNeeds = board?.Q(className: "primary-telemetry");
            VisualElement nextDetails = board?.Q(className: "expanded-telemetry");
            VisualElement nextRest = board?.Q(className: "hud");
            if (nextNeeds == null || nextDetails == null || nextRest == null)
            {
                ReleaseViews();
                return false;
            }
            if (needsHost == nextNeeds && detailsHost == nextDetails && restHost == nextRest
                && needs?.parent == nextNeeds && details?.parent == nextDetails && rest?.parent == nextRest)
                return true;
            ReleaseViews();
            needsHost = nextNeeds;
            detailsHost = nextDetails;
            restHost = nextRest;
            needs = Container("survival-needs", "survival-needs");
            needs.styleSheets.Add(sheet);
            hunger = new NeedRow(needs, "Hunger", "survival-hunger-bar", false);
            hydration = new NeedRow(needs, "Hydration", "survival-hydration-bar", true);
            needsHost.Add(needs);

            rest = Container("survival-rest", "survival-rest");
            rest.styleSheets.Add(sheet);
            restDay = Text(string.Empty, "survival-rest-day");
            restTime = Text(string.Empty, "survival-rest-time");
            rest.Add(restDay);
            rest.Add(restTime);
            wake = new Button(OnWake) { name = "survival-wake", text = "Wake", pickingMode = PickingMode.Position };
            wake.AddToClassList("survival-wake");
            rest.Add(wake);
            restFeedback = Text(string.Empty, "survival-rest-feedback");
            Show(restFeedback, false);
            rest.Add(restFeedback);
            restHost.Add(rest);

            details = Container("survival-recovery-detail", "survival-recovery-detail");
            details.styleSheets.Add(sheet);
            recoveryTitle = Text(string.Empty, "survival-recovery-title");
            arrival = Text(string.Empty, "survival-recovery-line");
            beacon = Text(string.Empty, "survival-recovery-line");
            details.Add(recoveryTitle);
            details.Add(arrival);
            details.Add(beacon);
            details.Add(Text("Nearby recovery craft · fictional scenario", "survival-recovery-note"));
            detailsHost.Add(details);
            return true;
        }

        private void OnWake()
        {
            // Recheck current authority, not the button's cached visibility. No local rest state or timer.
            if (!CanPresent() || !SurvivalChapterReadout.CanWake(survival, recovery, state, clock)) return;
            CommandResult result = recovery.TryWake();
            if (restFeedback != null)
            {
                restFeedback.text = result.Succeeded ? string.Empty : result.Explanation ?? "Unable to wake.";
                Show(restFeedback, !result.Succeeded);
            }
            RefreshPresentation();
        }

        private void HideViews()
        {
            Show(needs, false);
            Show(details, false);
            Show(rest, false);
            wake?.SetEnabled(false);
        }

        private void ReleaseViews()
        {
            if (wake != null) wake.clicked -= OnWake;
            needs?.RemoveFromHierarchy();
            details?.RemoveFromHierarchy();
            rest?.RemoveFromHierarchy();
            needsHost = detailsHost = restHost = needs = details = rest = null;
            hunger = hydration = null;
            restDay = restTime = restFeedback = recoveryTitle = arrival = beacon = null;
            wake = null;
        }

        private static VisualElement Container(string name, string css)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList(css);
            return element;
        }

        private static Label Text(string content, string css)
        {
            var label = new Label(content) { enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList(css);
            return label;
        }

        private static void Show(VisualElement element, bool visible) => element?.EnableInClassList("survival-hidden", !visible);

        private static void IgnorePicking(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            foreach (VisualElement child in element.Children()) IgnorePicking(child);
        }

        private sealed class NeedRow
        {
            private readonly VisualElement row;
            private readonly ProgressBar bar;
            private readonly NeedIcon icon;
            private readonly string label;
            public NeedRow(VisualElement parent, string title, string id, bool water)
            {
                label = title;
                row = Container(id + "-row", "survival-need-row");
                icon = new NeedIcon(water);
                icon.AddToClassList("survival-need-icon");
                row.Add(icon);
                row.Add(Text(title, "survival-need-label"));
                bar = new ProgressBar { name = id, lowValue = 0, highValue = 1, title = string.Empty, pickingMode = PickingMode.Ignore };
                bar.AddToClassList("survival-need-bar");
                IgnorePicking(bar);
                row.Add(bar);
                parent.Add(row);
            }

            public void Set(double unmetNeed, bool invert)
            {
                bool known = SurvivalChapterReadout.ValidNeed(unmetNeed);
                bar.value = known ? (float)(invert ? 1d - unmetNeed : unmetNeed) : 0;
                // Presentation attention colours only; these do not define medical or gameplay thresholds.
                row.EnableInClassList("survival-attention", known && unmetNeed >= .5d && unmetNeed < .8d);
                row.EnableInClassList("survival-critical", known && unmetNeed >= .8d);
                row.EnableInClassList("survival-unknown", !known);
                row.tooltip = !known ? label + " unavailable" : invert
                    ? "Hydration: a full bar means thirst is satisfied. This is a crew need, not water stores."
                    : "Hunger: an empty bar means hunger is satisfied; a rising bar means food is needed. This is not food stores.";
                icon.MarkDirtyRepaint();
            }
        }

        private sealed class NeedIcon : VisualElement
        {
            private readonly bool water;
            public NeedIcon(bool hydration)
            {
                water = hydration;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }
            private void Draw(MeshGenerationContext context)
            {
                if (contentRect.width < 1 || contentRect.height < 1) return;
                var p = context.painter2D;
                p.strokeColor = resolvedStyle.color;
                p.lineWidth = 1.5f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                if (water)
                {
                    p.BeginPath(); p.MoveTo(new Vector2(10, 2));
                    p.BezierCurveTo(new Vector2(6, 7), new Vector2(3, 10), new Vector2(4, 13));
                    p.BezierCurveTo(new Vector2(5, 19), new Vector2(15, 19), new Vector2(16, 13));
                    p.BezierCurveTo(new Vector2(17, 10), new Vector2(14, 7), new Vector2(10, 2)); p.Stroke();
                }
                else
                {
                    Line(p, 4, 2, 4, 7); Line(p, 7, 2, 7, 7); Line(p, 10, 2, 10, 7);
                    Line(p, 4, 7, 10, 7); Line(p, 7, 7, 7, 18);
                    Line(p, 15, 2, 15, 18); Line(p, 15, 2, 18, 7); Line(p, 18, 7, 15, 10);
                }
            }
            private static void Line(Painter2D p, float x, float y, float tx, float ty)
            { p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(tx, ty)); p.Stroke(); }
        }
    }

    // Pure readout rules: testable without creating a Unity object or running another simulation.
    internal static class SurvivalChapterReadout
    {
        public static bool CanShow(ISurvivalExtension needs, IGameState state, ISimulationClock clock) =>
            needs != null && needs.Enabled && needs.IsActive && state != null && state.Phase == GamePhase.Survival
            && state.Outcome == MissionOutcome.None && clock != null && !clock.IsPaused;

        public static bool CanWake(ISurvivalExtension needs, ISurvivalRecovery rest, IGameState state, ISimulationClock clock) =>
            CanShow(needs, state, clock) && rest != null && rest.IsResting;

        public static bool ValidNeed(double value) => Finite(value) && value >= 0d && value <= 1d;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static string Remaining(double seconds)
        {
            if (!Finite(seconds) || seconds < 0 || seconds / 60d >= long.MaxValue) return "time unavailable";
            long minutes = (long)Math.Ceiling(seconds / 60d);
            long days = minutes / 1440, hours = minutes / 60 % 24, mins = minutes % 60;
            if (days > 0) return string.Format(CultureInfo.InvariantCulture, "{0}d {1}h {2}m", days, hours, mins);
            if (hours > 0) return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", hours, mins);
            return minutes.ToString(CultureInfo.InvariantCulture) + " min";
        }

        public static string BeaconWindow(double ends, double now)
        {
            if (!Finite(ends) || !Finite(now)) return "Beacon window unavailable";
            return ends <= now ? "Beacon window closed" : "Beacon window: " + Remaining(ends - now) + " (simulated)";
        }

        public static string StageLabel(RecoveryStage stage)
        {
            switch (stage)
            {
                case RecoveryStage.Inbound: return "Recovery craft inbound";
                case RecoveryStage.BeaconRequested: return "Beacon requested";
                case RecoveryStage.Delayed: return "Rendezvous delayed";
                case RecoveryStage.Arrived: return "Recovery craft arrived";
                default: return "Recovery plan pending";
            }
        }
    }
}

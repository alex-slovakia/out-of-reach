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
        private Func<double?> readBeaconRequestTime;
        private IInteractable drinkAction, rationAction;
        private Camera guidanceCamera;
        private Transform waterTarget;
        private Func<bool> reducedMotion;
        private VisualElement presentationLayer;
        private SurvivalRestEyes restEyes;
        private SurvivalWakeTitle wakeTitle;
        private bool restObservationInitialized, wasResting, wakePending;
        private int pendingWakeDay;
        private RestStopReason pendingWakeReason;
        private SurvivalWaterLocator waterLocator;
        private bool subscribed;
        private VisualElement needsHost, detailsHost, restHost, alertsHost, needs, details, rest, alerts;
        private readonly SurvivalRestNotice restNotice = new SurvivalRestNotice();
        private NeedRow hunger, hydration;
        private Label restDay, restTime, restFeedback, recoveryTitle, arrival, beacon, beaconAction, restStop;
        private Label waterGuidance, foodGuidance;
        private Button wake;
        /// <summary>Once per observed rest exit, when its UI can present; supplies the actual exit-day snapshot and reason.</summary>
        public event Action<int, RestStopReason> WakePresentationStarted;

        /// <param name="canPresent">Root-owned visibility gate, including menu, terminal and cinematic locks.
        /// Must allow actual rest even if the player's movement control is locked. Null fails closed.</param>
        public void Configure(UIDocument ui, StyleSheet styleSheet, ISurvivalExtension needsService,
            ISurvivalRecovery recoveryService, IGameState gameState, ISimulationClock simulationClock,
            Func<bool> canPresent, Func<double?> readBeaconRequestTime = null,
            IInteractable drinkAction = null, IInteractable rationAction = null,
            Camera guidanceCamera = null, Transform waterTarget = null, Func<bool> reducedMotion = null)
        {
            ClearBinding();
            document = ui;
            sheet = styleSheet;
            survival = needsService;
            recovery = recoveryService;
            state = gameState;
            clock = simulationClock;
            presentationAllowed = canPresent;
            this.readBeaconRequestTime = readBeaconRequestTime;
            this.drinkAction = drinkAction;
            this.rationAction = rationAction;
            this.guidanceCamera = guidanceCamera;
            this.waterTarget = waterTarget;
            this.reducedMotion = reducedMotion;
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
            readBeaconRequestTime = null;
            drinkAction = rationAction = null;
            guidanceCamera = null;
            waterTarget = null;
            reducedMotion = null;
            state = null;
            clock = null;
            presentationAllowed = null;
            restNotice.Reset();
            ResetWakeObservation();
        }

        private void OnEnable() { Subscribe(); RefreshPresentation(); }
        private void OnDisable() { Unsubscribe(); ReleaseViews(); restNotice.Reset(); ResetWakeObservation(); }
        private void OnDestroy() => ClearBinding();
        // Only the notice's display lifetime uses real seconds. Mission/rest time is always read from Core.
        private void LateUpdate()
        {
            restNotice.Advance(Time.unscaledDeltaTime, CanPresent());
            RefreshPresentation();
            restEyes?.Advance(Time.unscaledDeltaTime, CanPresent());
            wakeTitle?.Advance(Time.unscaledDeltaTime, CanPresent(), reducedMotion?.Invoke() ?? false);
        }

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
            ObserveRestTransition();
            restNotice.Observe(survival != null && survival.Enabled && survival.IsActive
                && state != null && state.Phase == GamePhase.Survival && state.Outcome == MissionOutcome.None,
                recovery != null && recovery.IsResting, recovery?.LastRestStop ?? RestStopReason.None);
            bool visible = CanPresent();
            restEyes?.Observe(recovery != null && recovery.IsResting, reducedMotion?.Invoke() ?? false);
            if (!visible) { HideViews(); return; }
            if (!EnsureViews()) return;
            Show(presentationLayer, true);
            restEyes.Observe(recovery != null && recovery.IsResting, reducedMotion?.Invoke() ?? false);
            if (wakePending)
            {
                wakePending = false;
                wakeTitle.Begin(pendingWakeDay);
                WakePresentationStarted?.Invoke(pendingWakeDay, pendingWakeReason);
                if (!CanPresent()) { HideViews(); return; }
            }
            Show(needs, true);
            hunger.Set(survival.Hunger01);
            hydration.Set(survival.Thirst01);
            Show(alerts, true);
            RefreshNeedGuidance();
            restStop.text = restNotice.Text;
            Show(restStop, restNotice.IsVisible);

            bool resting = recovery != null && recovery.IsResting;
            waterLocator.Present(guidanceCamera, waterTarget, !resting);
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
            bool beaconRequested = recovery != null && recovery.Stage == RecoveryStage.BeaconRequested;
            double untilRequest = recovery?.Stage == RecoveryStage.Inbound
                ? (readBeaconRequestTime?.Invoke() ?? double.PositiveInfinity) - clock.ElapsedSimulationSeconds
                : double.PositiveInfinity;
            bool approachWindow = untilRequest > 0d && untilRequest <= 18d * 3600d;
            Show(beaconAction, beaconRequested || approachWindow);
            Show(beacon, beaconRequested || approachWindow);
            if (recovery == null) return;
            recoveryTitle.text = SurvivalChapterReadout.StageLabel(recovery.Stage);
            arrival.text = recovery.ArrivalAtSimulationSeconds.HasValue
                ? "Rendezvous in " + SurvivalChapterReadout.Remaining(recovery.ArrivalRemainingSimulationSeconds) + " (simulated)"
                : "Arrival time unconfirmed";
            Show(arrival, recovery.Stage != RecoveryStage.Arrived);
            double? ends = recovery.BeaconWindowEndsAtSimulationSeconds;
            if (beaconRequested)
            {
                beaconAction.text = "Power the recovery beacon at the cockpit console.";
                beacon.text = ends.HasValue
                    ? SurvivalChapterReadout.BeaconWindow(ends.Value, clock.ElapsedSimulationSeconds)
                    : "Beacon window unavailable";
            }
            else if (approachWindow)
            {
                beaconAction.text = "Head to the cockpit for the recovery beacon.";
                beacon.text = "Beacon opens in " + SurvivalChapterReadout.Remaining(untilRequest) + " (simulated)";
            }
        }

        private bool EnsureViews()
        {
            VisualElement board = document.rootVisualElement?.Q("mission-flight-board");
            VisualElement nextNeeds = board?.Q(className: "primary-telemetry");
            VisualElement nextDetails = board?.Q(className: "expanded-telemetry");
            VisualElement nextRest = board?.Q(className: "hud");
            VisualElement nextAlerts = board?.Q(className: "hud-header");
            if (nextNeeds == null || nextDetails == null || nextRest == null || nextAlerts == null)
            {
                ReleaseViews();
                return false;
            }
            if (needsHost == nextNeeds && detailsHost == nextDetails && restHost == nextRest
                && alertsHost == nextAlerts && alerts?.parent == nextAlerts
                && needs?.parent == nextNeeds && details?.parent == nextDetails
                && presentationLayer?.parent == board && rest?.parent == presentationLayer)
                return true;
            ReleaseViews();
            needsHost = nextNeeds;
            detailsHost = nextDetails;
            restHost = nextRest;
            alertsHost = nextAlerts;
            presentationLayer = Container("survival-presentation-layer", "survival-presentation-layer");
            presentationLayer.styleSheets.Add(sheet);
            waterLocator = new SurvivalWaterLocator();
            restEyes = new SurvivalRestEyes();
            wakeTitle = new SurvivalWakeTitle();
            restEyes.Reset();
            presentationLayer.Add(waterLocator);
            presentationLayer.Add(restEyes);
            presentationLayer.Add(wakeTitle);
            board.Add(presentationLayer);
            needs = Container("survival-needs", "survival-needs");
            needs.styleSheets.Add(sheet);
            hunger = new NeedRow(needs, "Fed", "survival-hunger-bar", false);
            hydration = new NeedRow(needs, "Hydrated", "survival-hydration-bar", true);
            needs.Add(Text("Full = needs met · not stored supplies", "survival-needs-key"));
            needsHost.Add(needs);

            rest = Container("survival-rest", "survival-rest");
            rest.styleSheets.Add(sheet);
            restDay = Text(string.Empty, "survival-rest-day");
            restDay.name = "survival-rest-day";
            restTime = Text(string.Empty, "survival-rest-time");
            rest.Add(restDay);
            rest.Add(restTime);
            wake = new Button(OnWake) { name = "survival-wake", text = "Wake", pickingMode = PickingMode.Position };
            wake.AddToClassList("survival-wake");
            rest.Add(wake);
            restFeedback = Text(string.Empty, "survival-rest-feedback");
            Show(restFeedback, false);
            rest.Add(restFeedback);
            presentationLayer.Add(rest);

            details = Container("survival-recovery-detail", "survival-recovery-detail");
            details.styleSheets.Add(sheet);
            recoveryTitle = Text(string.Empty, "survival-recovery-title");
            arrival = Text(string.Empty, "survival-recovery-line");
            details.Add(recoveryTitle);
            details.Add(arrival);
            details.Add(Text("Nearby recovery craft · fictional scenario", "survival-recovery-note"));
            detailsHost.Add(details);

            // This host stays visible without Tab/Systems; do not overwrite the root's mission objective.
            alerts = Container("survival-alerts", "survival-alerts");
            alerts.styleSheets.Add(sheet);
            waterGuidance = Text(string.Empty, "survival-need-guidance");
            waterGuidance.name = "survival-water-guidance";
            foodGuidance = Text(string.Empty, "survival-need-guidance");
            foodGuidance.name = "survival-food-guidance";
            alerts.Add(waterGuidance);
            alerts.Add(foodGuidance);
            beaconAction = Text("Power the recovery beacon at the cockpit console.", "survival-beacon-action");
            beaconAction.name = "survival-beacon-action";
            beacon = Text(string.Empty, "survival-beacon-window");
            beacon.name = "survival-beacon-window";
            restStop = Text(string.Empty, "survival-rest-stop");
            restStop.name = "survival-rest-stop";
            alerts.Add(beaconAction);
            alerts.Add(beacon);
            alerts.Add(restStop);
            alertsHost.Add(alerts);
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

        private void ObserveRestTransition()
        {
            bool active = survival != null && survival.Enabled && survival.IsActive && recovery != null
                && state != null && state.Phase == GamePhase.Survival && state.Outcome == MissionOutcome.None;
            if (!active) { ResetWakeObservation(); return; }
            bool nowResting = recovery.IsResting;
            if (!restObservationInitialized)
            {
                restObservationInitialized = true;
                wasResting = nowResting;
                return;
            }
            if (nowResting == wasResting) return;
            wasResting = nowResting;
            if (nowResting)
            {
                wakePending = false;
                wakeTitle?.Reset();
                return;
            }
            pendingWakeDay = recovery.MissionDay;
            pendingWakeReason = recovery.LastRestStop;
            wakePending = true;
        }

        private void ResetWakeObservation()
        {
            restObservationInitialized = wasResting = wakePending = false;
            pendingWakeDay = 0;
            pendingWakeReason = RestStopReason.None;
            wakeTitle?.Reset();
        }

        private void RefreshNeedGuidance()
        {
            bool thirsty = SurvivalChapterReadout.ValidNeed(survival.Thirst01) && survival.Thirst01 >= .25d;
            bool hungry = SurvivalChapterReadout.ValidNeed(survival.Hunger01) && survival.Hunger01 >= .5d;
            waterGuidance.text = thirsty
                ? NeedGuidance(drinkAction, "Drink water", "Water dispenser", "galley beside the crew berth")
                : "Food & water: galley beside the crew berth.";
            waterGuidance.EnableInClassList("survival-need-urgent", thirsty);
            waterGuidance.EnableInClassList("survival-need-critical", survival.Thirst01 >= .8d);
            foodGuidance.text = hungry
                ? NeedGuidance(rationAction, "Eat a ration", "Food locker", "galley beside the crew berth") : string.Empty;
            foodGuidance.EnableInClassList("survival-need-urgent", hungry);
            foodGuidance.EnableInClassList("survival-need-critical", survival.Hunger01 >= .8d);
            Show(foodGuidance, hungry);
        }

        private static string NeedGuidance(IInteractable action, string verb, string station, string location)
        {
            if (action == null) return station + ": " + location + ".";
            if (action.CanInteract) return verb + " at the " + station.ToLowerInvariant()
                + ".\n" + location + " · Look at it and press E when in reach.";
            return station + ": " + location + ".\n" + action.Prompt;
        }

        private void HideViews()
        {
            Show(needs, false);
            Show(details, false);
            Show(rest, false);
            Show(alerts, false);
            Show(presentationLayer, false);
            wake?.SetEnabled(false);
        }

        private void ReleaseViews()
        {
            if (wake != null) wake.clicked -= OnWake;
            needs?.RemoveFromHierarchy();
            details?.RemoveFromHierarchy();
            rest?.RemoveFromHierarchy();
            alerts?.RemoveFromHierarchy();
            restEyes?.Reset();
            wakeTitle?.Reset();
            presentationLayer?.RemoveFromHierarchy();
            presentationLayer = null;
            restEyes = null;
            wakeTitle = null;
            waterLocator = null;
            needsHost = detailsHost = restHost = alertsHost = needs = details = rest = alerts = null;
            hunger = hydration = null;
            restDay = restTime = restFeedback = recoveryTitle = arrival = beacon = beaconAction = restStop = null;
            waterGuidance = foodGuidance = null;
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

            public void Set(double unmetNeed)
            {
                bool known = SurvivalChapterReadout.ValidNeed(unmetNeed);
                bar.value = (float)SurvivalChapterReadout.SatisfiedFraction(unmetNeed);
                // Presentation attention colours only; these do not define medical or gameplay thresholds.
                row.EnableInClassList("survival-attention", known && unmetNeed >= .5d && unmetNeed < .8d);
                row.EnableInClassList("survival-critical", known && unmetNeed >= .8d);
                row.EnableInClassList("survival-unknown", !known);
                row.tooltip = !known ? label + " unavailable"
                    : label + ": a full bar means this need is met. This is not stored supplies.";
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

    internal sealed class SurvivalRestNotice
    {
        private bool observed, wasResting;
        private RestStopReason lastReason;
        private double remaining;
        public string Text { get; private set; } = string.Empty;
        public bool IsVisible => remaining > 0d;

        public void Reset()
        {
            observed = wasResting = false;
            lastReason = RestStopReason.None;
            remaining = 0;
            Text = string.Empty;
        }

        public void Observe(bool inChapter, bool resting, RestStopReason reason)
        {
            if (!inChapter) { Reset(); return; }
            if (resting || reason == RestStopReason.None) { remaining = 0; Text = string.Empty; }
            else if (observed && (wasResting || lastReason != reason))
            {
                Text = Message(reason);
                remaining = string.IsNullOrEmpty(Text) ? 0d : 8d;
            }
            observed = true;
            wasResting = resting;
            lastReason = reason;
        }

        public void Advance(double visibleRealSeconds, bool visible)
        {
            if (visible && !double.IsNaN(visibleRealSeconds) && !double.IsInfinity(visibleRealSeconds)
                && visibleRealSeconds > 0d)
                remaining = Math.Max(0d, remaining - visibleRealSeconds);
        }

        public static string Message(RestStopReason reason)
        {
            switch (reason)
            {
                case RestStopReason.Completed: return "Planned rest complete.";
                case RestStopReason.Woken: return "You woke from rest.";
                case RestStopReason.FoodStoreEmpty: return "Rest stopped: food stores are empty.";
                case RestStopReason.WaterStoreEmpty: return "Rest stopped: water stores are empty. Drinking and resting require stored water.";
                case RestStopReason.OxygenStoreEmpty: return "Rest stopped: stored oxygen is depleted. Only cabin air remains.";
                case RestStopReason.PowerLost: return "Rest stopped: power is lost. The battery is empty and generation is below demand.";
                case RestStopReason.CarbonDioxideDanger: return "Rest stopped: cabin CO₂ is dangerously high.";
                case RestStopReason.CropReady: return "Rest stopped: the crop is ready to harvest.";
                case RestStopReason.CropFailed: return "Rest stopped: the grow module failed.";
                case RestStopReason.RecoveryMilestone: return "Rest stopped: the recovery craft needs the crew. Check recovery status.";
                case RestStopReason.MissionEnded: return "Rest stopped: the mission ended.";
                default: return string.Empty;
            }
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
        public static double SatisfiedFraction(double unmetNeed) => ValidNeed(unmetNeed) ? 1d - unmetNeed : 0d;
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

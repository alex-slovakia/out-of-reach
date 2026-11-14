using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OutOfReach.Contracts;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Visual elements only. Values are supplied by the bound presenter or an explicit editor preview.</summary>
    public sealed class FlightBoardView : IDisposable
    {
        public event Action<string, int> QuantityChangeRequested;
        public event Action LaunchRequested;

        private readonly VisualElement root;
        private readonly VisualElement planner;
        private readonly VisualElement hud;
        private readonly ScrollView equipmentList;
        private readonly Dictionary<string, EquipmentRow> equipmentRows = new Dictionary<string, EquipmentRow>();
        private readonly Label mass;
        private readonly Label budget;
        private readonly Label power;
        private readonly Label validation;
        private readonly Label commandFeedback;
        private readonly Label phase;
        private readonly Label outcomeReason;
        private readonly Label simulationTiming;
        private readonly Label deadlineFallback;
        private readonly VisualElement planningTiming;
        private readonly Label deadlineCountdown;
        private readonly Label deadlineRule;
        private readonly VisualElement lastValidDetail;
        private readonly Label lastValidCounts;
        private IMissionConfiguration displayedLastValid;
        private readonly VisualElement instrumentStrip;
        private readonly VisualElement subtitleOverlay;
        private readonly Label subtitleSpeaker;
        private readonly Label subtitleText;
        private readonly Label[] planningResources;
        private readonly Label[] hudResources;
        private readonly Button launch;
        private readonly bool commandsEnabled;

        public FlightBoardView(VisualElement host, StyleSheet styleSheet, bool commandsEnabled = true)
        {
            this.commandsEnabled = commandsEnabled;
            host.pickingMode = PickingMode.Ignore;
            root = Element("flight-board");
            root.name = "mission-flight-board";
            root.pickingMode = PickingMode.Ignore;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            host.Add(root);

            planner = Element("planner");
            root.Add(planner);
            var heading = Element("planner-heading");
            planner.Add(heading);
            heading.Add(Text("AURORA-7 / MISSION PREPARATION", "eyebrow"));
            heading.Add(Text("Configure the mission", "title"));
            heading.Add(Text("Balance research equipment with the reserves and parts you may need to survive.", "intro"));
            planningTiming = Element("planning-timing");
            heading.Add(planningTiming);
            deadlineCountdown = Text(string.Empty, "deadline-countdown");
            planningTiming.Add(deadlineCountdown);
            deadlineRule = Text(string.Empty, "deadline-rule");
            planningTiming.Add(deadlineRule);

            var columns = Element("planner-columns");
            planner.Add(columns);
            var manifest = Element("equipment-column");
            columns.Add(manifest);
            manifest.Add(Text("01  /  EQUIPMENT MANIFEST", "section-heading"));
            equipmentList = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            equipmentList.AddToClassList("equipment-list");
            manifest.Add(equipmentList);

            var overview = Element("overview");
            columns.Add(overview);
            overview.Add(Text("02  /  LAUNCH REVIEW", "section-heading"));
            var reviewDetails = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            reviewDetails.AddToClassList("overview-scroll");
            overview.Add(reviewDetails);
            mass = AddConstraint(reviewDetails, "PAYLOAD MASS");
            budget = AddConstraint(reviewDetails, "BUDGET / FICTIONAL CREDITS");
            power = AddConstraint(reviewDetails, "LOAD / GENERATION");
            reviewDetails.Add(Text("RESERVES AT LAUNCH", "reserves-heading"));
            var reserves = Element("planning-reserves");
            reviewDetails.Add(reserves);
            planningResources = AddResources(reserves);
            reviewDetails.Add(Text("Oxygen is a stored reserve. CO₂ is cabin concentration.", "small-note"));
            lastValidDetail = Element("last-valid-detail");
            reviewDetails.Add(lastValidDetail);
            lastValidDetail.Add(Text("LAST VALID LOADOUT / AUTOMATIC BACKUP", "reserves-heading"));
            lastValidCounts = Text(string.Empty, "last-valid-counts");
            lastValidDetail.Add(lastValidCounts);
            var actions = Element("launch-review-actions");
            overview.Add(actions);
            validation = Text(string.Empty, "validation");
            actions.Add(validation);
            commandFeedback = Text(string.Empty, "command-feedback");
            actions.Add(commandFeedback);
            launch = new Button(() => LaunchRequested?.Invoke()) { text = "BEGIN MISSION" };
            launch.AddToClassList("launch-button");
            launch.tooltip = "Validate this loadout and begin the mission.";
            actions.Add(launch);
            actions.Add(Text("Your equipment choices are locked at launch.", "small-note"));

            var footer = Element("planner-footer");
            planner.Add(footer);
            footer.Add(Text("A FICTIONAL RESEARCH VESSEL", "footer-label"));
            footer.Add(Text("Mission limits and resource behavior are simplified for gameplay.", "footer-note"));

            hud = Element("hud");
            root.Add(hud);
            var header = Element("hud-header");
            hud.Add(header);
            header.Add(Text("AURORA-7", "hud-vessel"));
            phase = Text("WAITING FOR MISSION", "hud-phase");
            header.Add(phase);
            simulationTiming = Text(string.Empty, "simulation-timing");
            header.Add(simulationTiming);
            deadlineFallback = Text(string.Empty, "deadline-fallback");
            hud.Add(deadlineFallback);
            outcomeReason = Text(string.Empty, "outcome-reason");
            hud.Add(outcomeReason);
            instrumentStrip = Element("hud-instruments");
            hud.Add(instrumentStrip);
            instrumentStrip.Add(Text("ONBOARD RESERVES / CABIN", "hud-strip-title"));
            var readouts = Element("hud-readouts");
            instrumentStrip.Add(readouts);
            hudResources = AddResources(readouts);
            instrumentStrip.Add(Text("Oxygen: stored reserve   /   CO₂: cabin concentration", "hud-note"));
            IgnorePicking(hud);

            subtitleOverlay = Element("subtitle-overlay");
            root.Add(subtitleOverlay);
            var subtitleBox = Element("subtitle-box");
            subtitleOverlay.Add(subtitleBox);
            subtitleSpeaker = Text(string.Empty, "subtitle-speaker");
            subtitleBox.Add(subtitleSpeaker);
            subtitleText = Text(string.Empty, "subtitle-text");
            subtitleBox.Add(subtitleText);
            IgnorePicking(subtitleOverlay);
            instrumentStrip.RegisterCallback<GeometryChangedEvent>(OnInstrumentGeometryChanged);
            ClearSubtitle();
            ShowUnbound();
        }

        public void ShowUnbound()
        {
            ShowPlanning(false);
            phase.text = "WAITING FOR MISSION";
            outcomeReason.text = string.Empty;
            foreach (var value in hudResources) value.text = "—";
            launch.SetEnabled(false);
            SetSimulationTiming(false, false, 0);
            SetPlanningTiming(false, false, 0, false, null, null);
            SetDeadlineFallback(false);
        }

        public void ShowPlanning(bool show)
        {
            planner.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetPhase(GamePhase current, MissionOutcome outcome)
        {
            switch (current)
            {
                case GamePhase.Planning: phase.text = "MISSION PREPARATION"; break;
                case GamePhase.Intro: phase.text = "DEPARTURE"; break;
                case GamePhase.Emergency: phase.text = "EMERGENCY"; break;
                case GamePhase.Repair: phase.text = "SYSTEM REPAIRS"; break;
                case GamePhase.Eva: phase.text = "EXTRAVEHICULAR OPERATIONS"; break;
                case GamePhase.Transmission: phase.text = "COMMUNICATIONS"; break;
                case GamePhase.Outcome:
                case GamePhase.Report:
                    phase.text = outcome == MissionOutcome.RecoveryPossible ? "RECOVERY PLANNING POSSIBLE"
                        : outcome == MissionOutcome.Lost ? "MISSION LOST" : "MISSION REPORT";
                    break;
                default: phase.text = "MISSION STATUS"; break;
            }
            phase.EnableInClassList("constraint-exceeded", current == GamePhase.Emergency || outcome == MissionOutcome.Lost);
            if (current != GamePhase.Outcome && current != GamePhase.Report) outcomeReason.text = string.Empty;
        }

        public void SetOutcomeReason(string reason) => outcomeReason.text = reason ?? string.Empty;

        public void SetSimulationTiming(bool available, bool paused, double timeScale)
        {
            bool validScale = !double.IsNaN(timeScale) && !double.IsInfinity(timeScale) && timeScale > 0;
            simulationTiming.style.display = available && (paused || validScale) ? DisplayStyle.Flex : DisplayStyle.None;
            string scale = validScale ? Number(timeScale, "0.##") + "× SIMULATION" : string.Empty;
            simulationTiming.text = paused ? "PAUSED" + (validScale ? " / " + scale : string.Empty) : scale;
            simulationTiming.EnableInClassList("timing-paused", paused);
        }

        public void SetPlanningTiming(bool available, bool enabled, double remainingRealSeconds, bool paused,
            IMissionConfiguration lastValid, IReadOnlyList<EquipmentSelection> catalog)
        {
            planningTiming.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            lastValidDetail.style.display = available && enabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available)
            {
                displayedLastValid = null;
                return;
            }
            if (!enabled)
            {
                deadlineCountdown.text = "UNTIMED PLANNING";
                deadlineCountdown.style.display = DisplayStyle.Flex;
                deadlineCountdown.RemoveFromClassList("deadline-urgent");
                deadlineRule.text = "Launch when your loadout is ready. No automatic launch.";
                return;
            }

            bool validRemaining = !double.IsNaN(remainingRealSeconds) && !double.IsInfinity(remainingRealSeconds);
            deadlineCountdown.style.display = validRemaining ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineCountdown.text = (paused ? "PLANNING PAUSED / " : "PLANNING / ")
                + Number(Math.Ceiling(Math.Max(0, remainingRealSeconds)), "0") + " s remaining";
            deadlineCountdown.EnableInClassList("deadline-urgent", validRemaining && remainingRealSeconds <= 15);
            deadlineRule.text = "At zero, your valid loadout launches automatically. If it is invalid, the last valid loadout in Launch Review launches instead.";
            if (lastValid == null)
            {
                displayedLastValid = null;
                lastValidCounts.text = "Backup loadout unavailable.";
            }
            else if (!ReferenceEquals(displayedLastValid, lastValid))
            {
                displayedLastValid = lastValid;
                lastValidCounts.text = FormatLoadoutCounts(lastValid.Equipment, catalog);
            }
        }

        public void SetDeadlineFallback(bool usedLastValidLoadout)
        {
            deadlineFallback.style.display = usedLastValidLoadout ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineFallback.text = usedLastValidLoadout
                ? "Planning time expired. The mission launched with your last valid loadout." : string.Empty;
        }

        public void ShowSubtitle(string text, string speakerLabel)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearSubtitle();
                return;
            }
            subtitleText.text = text;
            subtitleSpeaker.text = speakerLabel ?? string.Empty;
            subtitleSpeaker.style.display = string.IsNullOrWhiteSpace(speakerLabel) ? DisplayStyle.None : DisplayStyle.Flex;
            subtitleOverlay.style.display = DisplayStyle.Flex;
        }

        public void ClearSubtitle()
        {
            subtitleOverlay.style.display = DisplayStyle.None;
            subtitleText.text = string.Empty;
            subtitleSpeaker.text = string.Empty;
        }

        private void OnInstrumentGeometryChanged(GeometryChangedEvent change)
        {
            // Keep captions above the actual HUD height, including wrapped labels or scaled text.
            if (change.newRect.height > 0) subtitleOverlay.style.bottom = change.newRect.height + 44f;
        }

        private static string FormatLoadoutCounts(IReadOnlyList<EquipmentSelection> selections,
            IReadOnlyList<EquipmentSelection> catalog)
        {
            var text = new StringBuilder();
            var shown = new HashSet<string>();
            if (catalog != null)
                foreach (var item in catalog)
                {
                    int count = 0;
                    foreach (var selected in selections)
                        if (selected.EquipmentId == item.EquipmentId) count = selected.Quantity;
                    AppendLoadoutCount(text, item.EquipmentId, count);
                    shown.Add(item.EquipmentId);
                }
            foreach (var selected in selections)
                if (shown.Add(selected.EquipmentId)) AppendLoadoutCount(text, selected.EquipmentId, selected.Quantity);
            return text.Length == 0 ? "No equipment selected." : text.ToString();
        }

        private static void AppendLoadoutCount(StringBuilder text, string id, int count)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(EquipmentCopy.Name(id)).Append("  × ").Append(count.ToString(CultureInfo.InvariantCulture));
        }

        public void SetEquipment(IReadOnlyList<EquipmentSelection> catalog,
            IReadOnlyList<EquipmentSelection> selections, bool editable)
        {
            bool rebuild = equipmentRows.Count != catalog.Count;
            if (!rebuild)
                foreach (var item in catalog)
                    if (!equipmentRows.ContainsKey(item.EquipmentId)) { rebuild = true; break; }
            if (rebuild)
            {
                equipmentList.Clear();
                equipmentRows.Clear();
                foreach (var item in catalog)
                {
                    string id = item.EquipmentId;
                    var row = new EquipmentRow(id,
                        () => QuantityChangeRequested?.Invoke(id, -1),
                        () => QuantityChangeRequested?.Invoke(id, 1));
                    equipmentRows.Add(id, row);
                    equipmentList.Add(row.Element);
                }
            }
            foreach (var item in catalog)
            {
                int count = 0;
                foreach (var selected in selections)
                    if (selected.EquipmentId == item.EquipmentId) count = selected.Quantity;
                equipmentRows[item.EquipmentId].Render(item, count, editable && commandsEnabled);
            }
        }

        public void SetConstraints(double massKg, double massLimitKg, double credits, double creditLimit,
            double loadWatts, double generationWatts)
        {
            SetConstraint(mass, massKg, massLimitKg, "kg");
            SetConstraint(budget, credits, creditLimit, "cr");
            SetConstraint(power, loadWatts, generationWatts, "W");
        }

        public void SetPlanningResources(ResourceSnapshot snapshot) => SetResources(planningResources, snapshot);
        public void SetHudResources(ResourceSnapshot snapshot) => SetResources(hudResources, snapshot);

        public void SetValidation(bool succeeded, string explanation, string rejectedCommand)
        {
            validation.text = succeeded ? "Loadout meets mission requirements."
                : string.IsNullOrWhiteSpace(explanation) ? "Review your loadout before launch." : explanation;
            validation.EnableInClassList("constraint-exceeded", !succeeded);
            commandFeedback.text = rejectedCommand ?? string.Empty;
            commandFeedback.style.display = string.IsNullOrWhiteSpace(rejectedCommand) ? DisplayStyle.None : DisplayStyle.Flex;
            launch.SetEnabled(succeeded && commandsEnabled);
        }

        public void Dispose()
        {
            QuantityChangeRequested = null;
            LaunchRequested = null;
            instrumentStrip.UnregisterCallback<GeometryChangedEvent>(OnInstrumentGeometryChanged);
            root.RemoveFromHierarchy();
        }

        private static Label AddConstraint(VisualElement parent, string title)
        {
            var row = Element("constraint-row");
            row.Add(Text(title, "constraint-label"));
            var value = Text("—", "constraint-value");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        private static Label[] AddResources(VisualElement parent)
        {
            string[] names = { "STORED OXYGEN", "WATER", "FOOD", "BATTERY", "SPARE PARTS", "CABIN CO₂" };
            var values = new Label[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var cell = Element("resource-cell");
                cell.Add(Text(names[i], "resource-label"));
                values[i] = Text("—", "resource-value");
                cell.Add(values[i]);
                parent.Add(cell);
            }
            return values;
        }

        private static void SetResources(Label[] values, ResourceSnapshot snapshot)
        {
            values[0].text = Number(snapshot.OxygenKg, "0.0") + " kg";
            values[1].text = Number(snapshot.WaterLitres, "0.0") + " L";
            values[2].text = Number(snapshot.FoodKg, "0.0") + " kg";
            values[3].text = Number(snapshot.BatteryKWh, "0.0") + " kWh";
            values[4].text = snapshot.SparePartsCount.ToString(CultureInfo.InvariantCulture);
            values[5].text = Number(snapshot.CarbonDioxidePpm, "0") + " ppm";
        }

        private static void SetConstraint(Label label, double current, double limit, string unit)
        {
            label.text = Number(current, "0.#") + " / " + Number(limit, "0.#") + " " + unit;
            label.EnableInClassList("constraint-exceeded", current > limit);
        }

        private static string Number(double value, string format)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "—" : value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        private static Label Text(string content, string className)
        {
            var label = new Label(content) { enableRichText = false };
            label.AddToClassList(className);
            return label;
        }

        private static void IgnorePicking(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            foreach (var child in element.Children()) IgnorePicking(child);
        }

        private sealed class EquipmentRow
        {
            public VisualElement Element { get; }
            private readonly Label costs;
            private readonly Label quantity;
            private readonly Button remove;
            private readonly Button add;

            public EquipmentRow(string id, Action onRemove, Action onAdd)
            {
                Element = FlightBoardView.Element("equipment-row");
                var copy = FlightBoardView.Element("equipment-copy");
                Element.Add(copy);
                copy.Add(Text(EquipmentCopy.Name(id), "equipment-name"));
                copy.Add(Text(EquipmentCopy.Description(id), "equipment-description"));
                costs = Text(string.Empty, "equipment-costs");
                copy.Add(costs);
                var stepper = FlightBoardView.Element("stepper");
                Element.Add(stepper);
                remove = new Button(onRemove) { text = "−", tooltip = "Remove one " + EquipmentCopy.Name(id) };
                remove.AddToClassList("quantity-button");
                stepper.Add(remove);
                quantity = Text("0", "quantity-value");
                stepper.Add(quantity);
                add = new Button(onAdd) { text = "+", tooltip = "Add one " + EquipmentCopy.Name(id) };
                add.AddToClassList("quantity-button");
                stepper.Add(add);
            }

            public void Render(EquipmentSelection item, int count, bool editable)
            {
                costs.text = Number(item.UnitMassKg, "0.#") + " kg  /  " + Number(item.UnitPowerWatts, "0.#")
                    + " W load  /  " + Number(item.UnitBudgetCredits, "0.#") + " cr per unit";
                quantity.text = count.ToString(CultureInfo.InvariantCulture);
                remove.SetEnabled(editable && count > 0);
                add.SetEnabled(editable && count < int.MaxValue);
            }
        }
    }
}

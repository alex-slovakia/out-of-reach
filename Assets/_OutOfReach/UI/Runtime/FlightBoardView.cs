using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OutOfReach.Contracts;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Visual elements only. The bound presenter supplies every mission value.</summary>
    public sealed class FlightBoardView : IDisposable
    {
        public event Action<string, int> QuantityChangeRequested;
        public event Action LaunchRequested;

        private readonly VisualElement root, planner, hud, equipmentList;
        private readonly Dictionary<string, EquipmentRow> equipmentRows = new Dictionary<string, EquipmentRow>();
        private readonly Label mass, budget, power, validation, commandFeedback;
        private readonly Label phase, objective, outcomeReason, simulationTiming, elapsedTime, deadlineFallback;
        private readonly VisualElement planningTiming, lastValidDetail;
        private readonly Label deadlineCountdown, deadlineRule, lastValidCounts;
        private IMissionConfiguration displayedLastValid;
        private readonly VisualElement instruments, expandedTelemetry;
        private readonly Button systems;
        private bool systemsPinned, systemsHeld, showingPlanning;
        private readonly VisualElement interactionPrompt, subtitleOverlay;
        private readonly Label interactionText, subtitleSpeaker, subtitleText;
        private readonly Label[] planningResources, hudResources;
        private readonly ResourceGauge oxygenGauge, batteryGauge, co2Gauge, suitGauge;
        private ResourceSnapshot resourceReference, latestResources;
        private bool hasResourceReference, hasHudResources, hasCo2Thresholds;
        private double co2ActionPpm, co2EmergencyPpm;
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
            heading.Add(Text("Out of reach", "title"));
            heading.Add(Text("Prepare AURORA-7 for deep space.\nWhat you bring determines what comes next.", "intro"));
            IgnorePicking(heading);

            var payload = Element("payload-panel");
            planner.Add(payload);
            var payloadHeader = Element("payload-header");
            payload.Add(payloadHeader);
            payloadHeader.Add(Text("Payload", "payload-title"));
            planningTiming = Element("planning-timing");
            payloadHeader.Add(planningTiming);
            deadlineCountdown = Text(string.Empty, "deadline-countdown");
            planningTiming.Add(deadlineCountdown);

            var constraints = Element("constraints");
            payload.Add(constraints);
            mass = AddConstraint(constraints, "Mass");
            budget = AddConstraint(constraints, "Budget");
            power = AddConstraint(constraints, "Load / generation");
            power.parent.AddToClassList("power-constraint");

            var payloadScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            payloadScroll.AddToClassList("payload-scroll");
            payload.Add(payloadScroll);
            equipmentList = Element("equipment-list");
            payloadScroll.Add(equipmentList);

            var details = new Foldout { text = "Reserves & launch details", value = false };
            details.AddToClassList("launch-details");
            payloadScroll.Add(details);
            details.Add(Text("Initial reserves", "details-title"));
            var reserves = Element("planning-reserves");
            details.Add(reserves);
            planningResources = AddResources(reserves);
            details.Add(Text("Oxygen is stored reserve; CO₂ is cabin concentration. Equipment is locked at launch.", "small-note"));
            deadlineRule = Text(string.Empty, "deadline-rule");
            details.Add(deadlineRule);
            lastValidDetail = Element("last-valid-detail");
            details.Add(lastValidDetail);
            lastValidDetail.Add(Text("Last valid loadout", "details-title"));
            lastValidCounts = Text(string.Empty, "last-valid-counts");
            lastValidDetail.Add(lastValidCounts);
            details.Add(Text("A fictional research mission. Credits, engineering limits and resource behaviour are simplified for gameplay.", "small-note"));

            var actions = Element("launch-review-actions");
            payload.Add(actions);
            validation = Text(string.Empty, "validation");
            actions.Add(validation);
            commandFeedback = Text(string.Empty, "command-feedback");
            actions.Add(commandFeedback);
            launch = new Button(() => LaunchRequested?.Invoke()) { text = "Begin mission" };
            launch.AddToClassList("launch-button");
            launch.tooltip = "Validate this payload and begin the mission. Equipment choices lock at launch.";
            actions.Add(launch);

            hud = Element("hud");
            root.Add(hud);
            var header = Element("hud-header");
            hud.Add(header);
            phase = Text("Waiting for mission", "hud-phase");
            header.Add(phase);
            objective = Text(string.Empty, "hud-objective");
            header.Add(objective);
            deadlineFallback = Text(string.Empty, "deadline-fallback");
            header.Add(deadlineFallback);
            outcomeReason = Text(string.Empty, "outcome-reason");
            header.Add(outcomeReason);

            var timing = Element("hud-timing");
            hud.Add(timing);
            elapsedTime = Text(string.Empty, "elapsed-time");
            timing.Add(elapsedTime);
            simulationTiming = Text(string.Empty, "simulation-timing");
            timing.Add(simulationTiming);

            instruments = Element("hud-instruments");
            hud.Add(instruments);
            instruments.Add(Text("SURVIVAL / AURORA-7", "instrument-heading"));
            var readouts = Element("hud-readouts");
            instruments.Add(readouts);
            var primary = Element("primary-telemetry");
            readouts.Add(primary);
            expandedTelemetry = Element("expanded-telemetry");
            readouts.Add(expandedTelemetry);
            hudResources = new Label[6];
            oxygenGauge = new ResourceGauge(primary, "O₂ reserve", "oxygen", 0);
            batteryGauge = new ResourceGauge(primary, "Battery", "battery", 3);
            co2Gauge = new ResourceGauge(primary, "Cabin CO₂", "co2", 5);
            hudResources[0] = oxygenGauge.Value;
            hudResources[3] = batteryGauge.Value;
            hudResources[5] = co2Gauge.Value;
            suitGauge = new ResourceGauge(readouts, "EVA / SUIT O₂", "suit", -1);
            suitGauge.Element.name = "suit-oxygen-gauge";
            suitGauge.Element.AddToClassList("suit-gauge");
            suitGauge.Element.style.display = DisplayStyle.None;
            hudResources[1] = AddResource(expandedTelemetry, "Water", 1);
            hudResources[2] = AddResource(expandedTelemetry, "Food", 2);
            hudResources[4] = AddResource(expandedTelemetry, "Spare parts", 4);
            systems = new Button(() => { systemsPinned = !systemsPinned; RefreshTelemetry(); })
            {
                text = "Systems",
                tooltip = "Show or hide water, food and spare parts. Hold Tab for a quick look."
            };
            systems.AddToClassList("systems-button");
            instruments.Add(systems);
            instruments.Add(Text("Hold Tab to inspect", "systems-hint"));
            IgnorePicking(hud);
            systems.pickingMode = PickingMode.Position;
            RefreshTelemetry();

            interactionPrompt = Element("interaction-prompt");
            hud.Add(interactionPrompt);
            interactionText = Text(string.Empty, "interaction-text");
            interactionPrompt.Add(interactionText);
            IgnorePicking(interactionPrompt);
            ClearInteractionPrompt();

            subtitleOverlay = Element("subtitle-overlay");
            root.Add(subtitleOverlay);
            var subtitleBox = Element("subtitle-box");
            subtitleOverlay.Add(subtitleBox);
            subtitleSpeaker = Text(string.Empty, "subtitle-speaker");
            subtitleBox.Add(subtitleSpeaker);
            subtitleText = Text(string.Empty, "subtitle-text");
            subtitleBox.Add(subtitleText);
            IgnorePicking(subtitleOverlay);
            instruments.RegisterCallback<GeometryChangedEvent>(OnInstrumentGeometryChanged);
            ClearSubtitle();
            ShowUnbound();
        }

        public void ShowUnbound()
        {
            ShowPlanning(false);
            phase.text = "Waiting for mission";
            objective.text = string.Empty;
            outcomeReason.text = string.Empty;
            foreach (var value in hudResources) value.text = "—";
            hasHudResources = false;
            UpdateGauges();
            SetEvaStatus(null);
            launch.SetEnabled(false);
            SetSimulationTiming(false, false, 0);
            SetSimulationElapsedTime(false, 0);
            SetPlanningTiming(false, false, 0, false, null, null);
            SetDeadlineFallback(false);
            ClearInteractionPrompt();
        }

        public void ShowPlanning(bool show)
        {
            showingPlanning = show;
            planner.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            if (show)
            {
                systemsHeld = false;
                systemsPinned = false;
                RefreshTelemetry();
                ClearInteractionPrompt();
            }
        }

        public void SetPhase(GamePhase current, MissionOutcome outcome)
        {
            switch (current)
            {
                case GamePhase.Planning: phase.text = "Mission preparation"; objective.text = string.Empty; break;
                case GamePhase.Intro: phase.text = "Departure"; objective.text = "AURORA-7"; break;
                case GamePhase.Emergency: phase.text = "Emergency"; objective.text = "Stabilise the ship."; break;
                case GamePhase.Repair: phase.text = "Restore essential systems"; objective.text = "Power and life support"; break;
                case GamePhase.Eva: phase.text = "Repair the antenna"; objective.text = "Exterior operations"; break;
                case GamePhase.Transmission: phase.text = "Restore contact with Earth"; objective.text = "Communications"; break;
                case GamePhase.Outcome:
                case GamePhase.Report:
                    phase.text = outcome == MissionOutcome.RecoveryPossible ? "Contact restored"
                        : outcome == MissionOutcome.Lost ? "Mission lost" : "Mission report";
                    objective.text = outcome == MissionOutcome.RecoveryPossible ? "Recovery planning is possible. Rescue is not confirmed." : string.Empty;
                    break;
                default: phase.text = "Mission status"; objective.text = string.Empty; break;
            }
            phase.EnableInClassList("constraint-exceeded", current == GamePhase.Emergency || outcome == MissionOutcome.Lost);
            if (current != GamePhase.Outcome && current != GamePhase.Report) outcomeReason.text = string.Empty;
        }

        public void SetOutcomeReason(string reason) => outcomeReason.text = reason ?? string.Empty;

        public void SetSimulationTiming(bool available, bool paused, double timeScale)
        {
            bool validScale = !double.IsNaN(timeScale) && !double.IsInfinity(timeScale) && timeScale > 0;
            simulationTiming.style.display = available && (paused || validScale) ? DisplayStyle.Flex : DisplayStyle.None;
            string scale = validScale ? Number(timeScale, "0.##") + "× simulation" : string.Empty;
            simulationTiming.text = paused ? "Paused" + (validScale ? " · " + scale : string.Empty) : scale;
            simulationTiming.EnableInClassList("timing-paused", paused);
        }

        /// <summary>Formats Core's elapsed value; never integrates or advances time.</summary>
        public void SetSimulationElapsedTime(bool available, double elapsedSimulationSeconds)
        {
            bool valid = available && !double.IsNaN(elapsedSimulationSeconds)
                && !double.IsInfinity(elapsedSimulationSeconds) && elapsedSimulationSeconds >= 0;
            elapsedTime.style.display = valid ? DisplayStyle.Flex : DisplayStyle.None;
            if (!valid) return;
            double seconds = Math.Floor(elapsedSimulationSeconds);
            double hours = Math.Floor(seconds / 3600);
            elapsedTime.text = "Mission " + Number(hours, "00") + ":"
                + Number(Math.Floor(seconds / 60) % 60, "00") + ":" + Number(seconds % 60, "00");
        }

        public void SetPlanningTiming(bool available, bool enabled, double remainingRealSeconds, bool paused,
            IMissionConfiguration lastValid, IReadOnlyList<EquipmentSelection> catalog)
        {
            planningTiming.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineRule.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            lastValidDetail.style.display = available && enabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available) { displayedLastValid = null; return; }
            if (!enabled)
            {
                deadlineCountdown.text = "Untimed";
                deadlineCountdown.style.display = DisplayStyle.Flex;
                deadlineCountdown.RemoveFromClassList("deadline-urgent");
                deadlineRule.text = "Launch when your payload is ready. No automatic launch.";
                return;
            }
            bool validRemaining = !double.IsNaN(remainingRealSeconds) && !double.IsInfinity(remainingRealSeconds);
            deadlineCountdown.style.display = validRemaining ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineCountdown.text = (paused ? "Paused · " : "Launch in ")
                + Number(Math.Ceiling(Math.Max(0, remainingRealSeconds)), "0") + " s";
            deadlineCountdown.EnableInClassList("deadline-urgent", validRemaining && remainingRealSeconds <= 15);
            deadlineRule.text = "At zero, this payload launches automatically if valid. Otherwise, the last valid loadout below launches. The deadline uses unpaused real time.";
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
                ? "Planning time expired. Your last valid loadout was launched." : string.Empty;
        }

        /// <summary>Presentation-only disclosure. The caller owns keyboard input and player authority.</summary>
        public void SetSystemsHeld(bool held)
        {
            bool next = held && !showingPlanning;
            if (systemsHeld == next) return;
            systemsHeld = next;
            RefreshTelemetry();
        }

        private void RefreshTelemetry()
        {
            expandedTelemetry.style.display = systemsPinned || systemsHeld ? DisplayStyle.Flex : DisplayStyle.None;
            systems.text = systemsPinned ? "Hide systems" : "Systems";
            systems.EnableInClassList("systems-open", systemsPinned || systemsHeld);
        }

        /// <summary>Displays supplied interaction wording without issuing or simulating a command.</summary>
        public void ShowInteractionPrompt(string text, bool available)
        {
            if (string.IsNullOrWhiteSpace(text)) { ClearInteractionPrompt(); return; }
            interactionText.text = text;
            interactionText.EnableInClassList("constraint-exceeded", !available);
            interactionPrompt.style.display = DisplayStyle.Flex;
        }

        public void ClearInteractionPrompt()
        {
            interactionPrompt.style.display = DisplayStyle.None;
            interactionText.text = string.Empty;
        }

        public void ShowSubtitle(string text, string speakerLabel)
        {
            if (string.IsNullOrWhiteSpace(text)) { ClearSubtitle(); return; }
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
            // Leave a real gap above both collapsed and expanded telemetry at the current panel scale.
            if (change.newRect.height > 0)
                subtitleOverlay.style.bottom = Math.Max(112f, change.newRect.height + 48f);
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
        public void SetHudResources(ResourceSnapshot snapshot)
        {
            latestResources = snapshot;
            hasHudResources = true;
            SetResources(hudResources, snapshot);
            UpdateGauges();
        }

        public void ConfigureResourceReference(ResourceSnapshot launchResources)
        {
            resourceReference = launchResources;
            hasResourceReference = true;
            UpdateGauges();
        }

        public void ClearResourceReference()
        {
            hasResourceReference = false;
            UpdateGauges();
        }

        public void ConfigureCo2Scale(double actionPpm, double emergencyPpm)
        {
            if (!Finite(actionPpm) || !Finite(emergencyPpm) || actionPpm <= 0 || emergencyPpm <= actionPpm)
                throw new ArgumentOutOfRangeException(nameof(emergencyPpm), "CO₂ levels must be finite, positive and ordered action < emergency.");
            co2ActionPpm = actionPpm;
            co2EmergencyPpm = emergencyPpm;
            hasCo2Thresholds = true;
            UpdateGauges();
        }

        public void SetEvaStatus(IEvaStatus status)
        {
            bool visible = status != null && (status.IsSuitOn || status.IsSuitSealed || status.IsOutside);
            suitGauge.Element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            double remaining = status.SuitOxygenSecondsRemaining;
            suitGauge.Value.text = double.IsPositiveInfinity(remaining) ? "∞ sim s remaining"
                : !Finite(remaining) || remaining < 0 ? "— sim s remaining"
                : Number(Math.Ceiling(remaining), "#,0") + " sim s remaining";
            bool known = Finite(status.SuitOxygenKg) && status.SuitOxygenKg >= 0
                && Finite(status.SuitOxygenCapacityKg) && status.SuitOxygenCapacityKg > 0;
            double fraction = known ? status.SuitOxygenKg / status.SuitOxygenCapacityKg : 0;
            string location = status.IsOutside ? "Outside" : "Cabin";
            string seal = status.IsSuitSealed ? "sealed" : "suit open";
            suitGauge.SetLevel(known, fraction, known && fraction <= 0.25, known && fraction <= 0,
                Number(status.SuitOxygenKg, "0.000") + " / " + Number(status.SuitOxygenCapacityKg, "0.000")
                + " kg · " + location + " · " + seal);
            suitGauge.Element.tooltip = "Suit oxygen from Core. Bar: actual suit capacity. Remaining duration is simulated time; no HUD countdown is calculated.";
        }

        private void UpdateGauges()
        {
            SetReserveGauge(oxygenGauge, latestResources.OxygenKg, resourceReference.OxygenKg, "Stored oxygen reserve");
            SetReserveGauge(batteryGauge, latestResources.BatteryKWh, resourceReference.BatteryKWh, "Available energy");
            double scale = hasCo2Thresholds ? co2EmergencyPpm : 10000d;
            double current = latestResources.CarbonDioxidePpm;
            bool known = hasHudResources && Finite(current) && current >= 0;
            bool emergency = known && hasCo2Thresholds && current >= co2EmergencyPpm;
            bool action = known && hasCo2Thresholds && current >= co2ActionPpm;
            string stateText = emergency ? "Emergency level" : action ? "Action required" : "Cabin concentration";
            co2Gauge.SetLevel(known, known ? current / scale : 0, action, emergency,
                "Scale 0–" + Number(scale, "#,0") + " ppm\n" + stateText, !hasCo2Thresholds);
            co2Gauge.Element.tooltip = hasCo2Thresholds
                ? "Scenario thresholds from Core: action " + Number(co2ActionPpm, "#,0")
                    + " ppm; emergency " + Number(co2EmergencyPpm, "#,0") + " ppm."
                : "Instrument range only. No safety or warning threshold has been configured.";
        }

        private void SetReserveGauge(ResourceGauge gauge, double current, double reference, string description)
        {
            bool known = hasHudResources && hasResourceReference && Finite(current) && current >= 0
                && Finite(reference) && reference > 0;
            double fraction = known ? current / reference : 0;
            bool low = known && fraction <= 0.25;
            bool empty = known && current <= 0;
            string note = known ? Number(fraction * 100, "0") + "% of launch" : "Launch reference —";
            gauge.SetLevel(known, fraction, low, empty,
                note + "\n" + (empty ? "Reserve empty" : low ? "Low reserve" : description));
            gauge.Element.tooltip = "Bar is relative to the committed launch reserve, not tank capacity or cabin oxygen percentage. Amber denotes 25% of launch reserve or less.";
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public void SetValidation(bool succeeded, string explanation, string rejectedCommand)
        {
            validation.text = succeeded ? "Ready for launch."
                : string.IsNullOrWhiteSpace(explanation) ? "Review your payload before launch." : explanation;
            validation.EnableInClassList("constraint-exceeded", !succeeded);
            commandFeedback.text = rejectedCommand ?? string.Empty;
            commandFeedback.style.display = string.IsNullOrWhiteSpace(rejectedCommand) ? DisplayStyle.None : DisplayStyle.Flex;
            launch.SetEnabled(succeeded && commandsEnabled);
        }

        public void Dispose()
        {
            QuantityChangeRequested = null;
            LaunchRequested = null;
            instruments.UnregisterCallback<GeometryChangedEvent>(OnInstrumentGeometryChanged);
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
            string[] names = { "Oxygen reserve", "Water", "Food", "Battery", "Spare parts", "Cabin CO₂" };
            var values = new Label[names.Length];
            for (int i = 0; i < names.Length; i++) values[i] = AddResource(parent, names[i], i);
            return values;
        }

        private static Label AddResource(VisualElement parent, string title, int index)
        {
            var cell = Element("resource-cell");
            cell.name = "resource-" + index;
            cell.Add(Text(title, "resource-label"));
            var value = Text("—", "resource-value");
            cell.Add(value);
            parent.Add(cell);
            return value;
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

        private sealed class ResourceGauge
        {
            public VisualElement Element { get; }
            public Label Value { get; }
            private readonly ProgressBar bar;
            private readonly Label note;

            public ResourceGauge(VisualElement parent, string title, string iconName, int resourceIndex)
            {
                Element = FlightBoardView.Element("instrument-gauge");
                Element.AddToClassList("resource-cell");
                if (resourceIndex >= 0) Element.name = "resource-" + resourceIndex;
                var heading = FlightBoardView.Element("gauge-heading");
                Element.Add(heading);
                var icon = FlightBoardView.Element("gauge-icon");
                icon.AddToClassList("gauge-icon-" + iconName);
                for (int i = 0; i < 3; i++)
                {
                    var mark = FlightBoardView.Element("icon-mark");
                    mark.AddToClassList("icon-mark-" + i);
                    icon.Add(mark);
                }
                heading.Add(icon);
                heading.Add(Text(title, "resource-label"));
                Value = Text("—", "resource-value");
                Element.Add(Value);
                bar = new ProgressBar { lowValue = 0f, highValue = 1f, value = 0f, title = string.Empty };
                bar.name = resourceIndex >= 0 ? "resource-bar-" + resourceIndex : "suit-oxygen-bar";
                bar.AddToClassList("gauge-bar");
                Element.Add(bar);
                note = Text(string.Empty, "gauge-note");
                Element.Add(note);
                parent.Add(Element);
            }

            public void SetLevel(bool known, double fraction, bool low, bool critical, string detail, bool neutral = false)
            {
                bar.value = known ? (float)Math.Max(0, Math.Min(1, fraction)) : 0f;
                Element.EnableInClassList("gauge-unknown", !known);
                Element.EnableInClassList("gauge-neutral", neutral);
                Element.EnableInClassList("gauge-low", low && !critical);
                Element.EnableInClassList("gauge-critical", critical);
                note.text = detail;
            }
        }

        private sealed class EquipmentRow
        {
            public VisualElement Element { get; }
            private readonly Label costs, quantity;
            private readonly Button remove, add;
            private readonly string description;

            public EquipmentRow(string id, Action onRemove, Action onAdd)
            {
                Element = FlightBoardView.Element("equipment-row");
                Element.name = id;
                description = EquipmentCopy.Description(id);
                var copy = FlightBoardView.Element("equipment-copy");
                Element.Add(copy);
                copy.Add(Text(EquipmentCopy.Name(id), "equipment-name"));
                costs = Text(string.Empty, "equipment-costs");
                copy.Add(costs);
                var stepper = FlightBoardView.Element("stepper");
                Element.Add(stepper);
                remove = new Button(onRemove) { text = "−" };
                remove.AddToClassList("quantity-button");
                stepper.Add(remove);
                quantity = Text("0", "quantity-value");
                stepper.Add(quantity);
                add = new Button(onAdd) { text = "+" };
                add.AddToClassList("quantity-button");
                stepper.Add(add);
                remove.RegisterCallback<FocusInEvent>(_ => Element.AddToClassList("equipment-focused"));
                remove.RegisterCallback<FocusOutEvent>(_ => Element.RemoveFromClassList("equipment-focused"));
                add.RegisterCallback<FocusInEvent>(_ => Element.AddToClassList("equipment-focused"));
                add.RegisterCallback<FocusOutEvent>(_ => Element.RemoveFromClassList("equipment-focused"));
            }

            public void Render(EquipmentSelection item, int count, bool editable)
            {
                costs.text = Number(item.UnitMassKg, "0.#") + " kg · " + Number(item.UnitBudgetCredits, "0.#")
                    + " cr · " + Number(item.UnitPowerWatts, "0.#") + " W load";
                string detail = description + " Per unit: " + costs.text + ".";
                Element.tooltip = detail;
                remove.tooltip = "Remove one " + EquipmentCopy.Name(item.EquipmentId) + ". " + detail;
                add.tooltip = "Add one " + EquipmentCopy.Name(item.EquipmentId) + ". " + detail;
                quantity.text = count.ToString(CultureInfo.InvariantCulture);
                remove.SetEnabled(editable && count > 0);
                add.SetEnabled(editable && count < int.MaxValue);
            }
        }
    }
}

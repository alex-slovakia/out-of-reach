using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OutOfReach.Contracts;
using UnityEngine;
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
        private readonly VisualElement preflight, preflightDetails, preflightLastValidDetail;
        private readonly Label preflightMass, preflightBudget, preflightPower, preflightValidation;
        private readonly Label preflightObjective, preflightCountdown, preflightClockNote, preflightCarried, preflightCarryPace;
        private readonly Label preflightDeadlineRule, preflightLastValidCounts, preflightLoadedCounts;
        private readonly Label[] preflightResources;
        private bool physicalPlanning, planningDetailsHeld;
        private IMissionConfiguration displayedLastValid;
        private readonly VisualElement instruments, primaryTelemetry, evaTelemetry, expandedTelemetry;
        private readonly Label suitDuration, suitSeal;
        private readonly Button systems;
        private bool systemsPinned, systemsHeld, showingPlanning, evaContext, co2NeedsAttention;
        private readonly VisualElement interactionPrompt, subtitleOverlay, microgravityTutorial, railPrompt;
        private readonly Label interactionKey, interactionText, subtitleSpeaker, subtitleText;
        private bool showMicrogravityTutorial, showRailPrompt;
        private readonly Label[] planningResources, hudResources;
        private readonly ResourceGauge oxygenGauge, waterGauge, foodGauge, batteryGauge, co2Gauge, suitGauge;
        private readonly ResourceGauge exertionGauge;
        private readonly VisualElement exertionReadout;
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
            heading.Add(Text("AURORA-7\nChoose what comes with you.", "intro"));
            IgnorePicking(heading);

            var payload = Element("payload-panel");
            planner.Add(payload);
            var payloadHeader = Element("payload-header");
            payload.Add(payloadHeader);
            payloadHeader.Add(Text("Mission loadout", "payload-title"));
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

            preflight = Element("preflight");
            preflight.name = "physical-preflight";
            root.Add(preflight);
            preflightObjective = Text("Load cargo, then board the transport", "preflight-objective");
            preflight.Add(preflightObjective);
            var preflightTiming = Element("preflight-timing");
            preflight.Add(preflightTiming);
            preflightCountdown = Text(string.Empty, "preflight-countdown");
            preflightCountdown.name = "cargo-countdown";
            preflightTiming.Add(preflightCountdown);
            preflightClockNote = Text("Active real time", "preflight-clock-note");
            preflightTiming.Add(preflightClockNote);
            var preflightLoadout = Element("preflight-loadout");
            preflight.Add(preflightLoadout);
            preflightCarried = Text(string.Empty, "preflight-carried");
            preflightCarried.name = "carried-cargo-mass";
            preflightLoadout.Add(preflightCarried);
            SetVisible(preflightCarried, false);
            preflightDetails = Element("preflight-details");
            preflight.Add(preflightDetails);
            var preflightConstraints = Element("preflight-constraints");
            preflightDetails.Add(preflightConstraints);
            preflightMass = AddConstraint(preflightConstraints, "Loaded mass");
            preflightBudget = AddConstraint(preflightConstraints, "Credits");
            preflightPower = AddConstraint(preflightConstraints, "Load / generation");
            preflightValidation = Text(string.Empty, "preflight-validation");
            preflightDetails.Add(preflightValidation);
            var preflightDetailColumns = Element("preflight-detail-columns");
            preflightDetails.Add(preflightDetailColumns);
            var preflightReserveColumn = Element("preflight-detail-column");
            preflightDetailColumns.Add(preflightReserveColumn);
            preflightReserveColumn.Add(Text("Aboard cargo", "details-title"));
            preflightLoadedCounts = Text(string.Empty, "last-valid-counts");
            preflightLoadedCounts.name = "preflight-loaded-counts";
            preflightReserveColumn.Add(preflightLoadedCounts);
            preflightReserveColumn.Add(Text("Aboard reserves", "details-title"));
            var preflightReserves = Element("preflight-reserves");
            preflightReserveColumn.Add(preflightReserves);
            preflightResources = AddResources(preflightReserves);
            preflightLastValidDetail = Element("last-valid-detail");
            preflightLastValidDetail.AddToClassList("preflight-detail-column");
            preflightDetailColumns.Add(preflightLastValidDetail);
            preflightLastValidDetail.Add(Text("Last valid loadout", "details-title"));
            preflightLastValidCounts = Text(string.Empty, "last-valid-counts");
            preflightLastValidDetail.Add(preflightLastValidCounts);
            preflightDeadlineRule = Text(string.Empty, "deadline-rule");
            preflightDetails.Add(preflightDeadlineRule);
            preflightCarryPace = Text(string.Empty, "small-note");
            preflightDetails.Add(preflightCarryPace);
            IgnorePicking(preflight);

            hud = Element("hud");
            root.Add(hud);
            var header = Element("hud-header");
            hud.Add(header);
            var missionObjective = Element("hud-mission-objective");
            header.Add(missionObjective);
            phase = Text("Waiting for mission", "hud-phase");
            missionObjective.Add(phase);
            objective = Text(string.Empty, "hud-objective");
            missionObjective.Add(objective);
            deadlineFallback = Text(string.Empty, "deadline-fallback");
            header.Add(deadlineFallback);
            outcomeReason = Text(string.Empty, "outcome-reason");
            header.Add(outcomeReason);

            var compression = Element("hud-timing");
            hud.Add(compression);
            simulationTiming = Text(string.Empty, "simulation-timing");
            compression.Add(simulationTiming);
            elapsedTime = Text(string.Empty, "elapsed-time");
            compression.Add(elapsedTime);
            microgravityTutorial = Element("microgravity-move");
            microgravityTutorial.name = "microgravity-tutorial";
            var handsHint = Element("control-hint-row");
            handsHint.Add(MousePictogram("left-mouse", true, false));
            handsHint.Add(MousePictogram("right-mouse", false, true));
            handsHint.Add(Text("Push / grip", "control-hint-action"));
            microgravityTutorial.Add(handsHint);
            var pushHint = Element("control-hint-row");
            pushHint.Add(MousePictogram("both-mouse-buttons", true, true));
            pushHint.Add(Text("/", "control-hint-separator"));
            pushHint.Add(KeyPictogram("W"));
            pushHint.Add(Text("+", "control-hint-separator"));
            pushHint.Add(KeyPictogram("SPACE"));
            pushHint.Add(Text("Push", "control-hint-action"));
            microgravityTutorial.Add(pushHint);
            var tiltHint = Element("control-hint-row");
            tiltHint.Add(KeyPictogram("A"));
            tiltHint.Add(KeyPictogram("D"));
            tiltHint.Add(Text("Tilt", "control-hint-action"));
            microgravityTutorial.Add(tiltHint);
            compression.Add(microgravityTutorial);
            railPrompt = Text("Handhold in reach", "microgravity-rail-prompt");
            compression.Add(railPrompt);
            SetVisible(microgravityTutorial, false);
            SetVisible(railPrompt, false);

            instruments = Element("hud-instruments");
            hud.Add(instruments);
            var readouts = Element("hud-readouts");
            instruments.Add(readouts);
            primaryTelemetry = Element("primary-telemetry");
            readouts.Add(primaryTelemetry);
            evaTelemetry = Element("eva-telemetry");
            readouts.Add(evaTelemetry);
            expandedTelemetry = Element("expanded-telemetry");
            readouts.Add(expandedTelemetry);
            hudResources = new Label[6];
            oxygenGauge = new ResourceGauge(primaryTelemetry, "O₂ reserve", 0);
            suitGauge = new ResourceGauge(primaryTelemetry, "Suit O₂", -1);
            suitGauge.Element.name = "suit-oxygen-gauge";
            suitGauge.Element.AddToClassList("suit-gauge");
            foodGauge = new ResourceGauge(primaryTelemetry, "Food stores", 2);
            batteryGauge = new ResourceGauge(primaryTelemetry, "Battery", 3);
            co2Gauge = new ResourceGauge(readouts, "Cabin CO₂", 5);
            co2Gauge.Element.AddToClassList("co2-alert");
            hudResources[0] = oxygenGauge.Value;
            hudResources[2] = foodGauge.Value;
            hudResources[3] = batteryGauge.Value;
            hudResources[5] = co2Gauge.Value;
            var duration = Element("suit-duration");
            duration.AddToClassList("resource-cell");
            duration.Add(Text("Time remaining", "resource-label"));
            suitDuration = Text("—", "resource-value");
            duration.Add(suitDuration);
            suitSeal = Text(string.Empty, "suit-seal");
            duration.Add(suitSeal);
            evaTelemetry.Add(duration);
            waterGauge = new ResourceGauge(expandedTelemetry, "Water stores", 1);
            hudResources[1] = waterGauge.Value;
            hudResources[4] = AddResource(expandedTelemetry, "Spare parts", 4);
            systems = new Button(() => { systemsPinned = !systemsPinned; RefreshTelemetry(); })
            {
                text = "Tab  Systems",
                tooltip = "Hold Tab to inspect all ship reserves. Click to keep them visible."
            };
            systems.AddToClassList("systems-button");
            instruments.Add(systems);
            IgnorePicking(hud);
            systems.pickingMode = PickingMode.Position;
            RefreshTelemetry();

            interactionPrompt = Element("interaction-prompt");
            hud.Add(interactionPrompt);
            interactionKey = Text("E", "interaction-key");
            interactionPrompt.Add(interactionKey);
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
            ClearSubtitle();
            exertionReadout = Element("exertion-readout");
            exertionReadout.AddToClassList("hud-readouts");
            root.Add(exertionReadout);
            exertionGauge = new ResourceGauge(exertionReadout, "Stamina", 6);
            exertionGauge.Element.name = "player-stamina-gauge";
            IgnorePicking(exertionReadout);
            SetExertion(float.NaN);
            ShowUnbound();
        }

        public void SetExertion(float remaining)
        {
            bool available = !float.IsNaN(remaining) && !float.IsInfinity(remaining);
            SetVisible(exertionReadout, available && remaining < .995f);
            if (!available) return;
            remaining = Mathf.Clamp01(remaining);
            exertionGauge.Value.text = (remaining * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
            exertionGauge.SetLevel(true, remaining, remaining < .3f, remaining < .12f, string.Empty);
        }

        public void ShowUnbound()
        {
            root.RemoveFromClassList("report-visible");
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
            bool enteringPlanning = show && !showingPlanning;
            showingPlanning = show;
            if (!show) planningDetailsHeld = false;
            RefreshPlanningVisibility();
            if (enteringPlanning || (show && !physicalPlanning))
            {
                systemsHeld = false;
                systemsPinned = false;
                RefreshTelemetry();
                ClearInteractionPrompt();
            }
        }

        public void SetPhysicalPlanning(bool enabled)
        {
            physicalPlanning = enabled;
            if (!enabled) planningDetailsHeld = false;
            RefreshPlanningVisibility();
        }

        public void SetMenuOverlayVisible(bool visible) => root.EnableInClassList("menu-overlay-hidden", visible);

        public void SetDeparturePresentation(bool active) => root.EnableInClassList("departure-presentation", active);

        public void SetPhysicalTerminalPresentation(bool active) => root.EnableInClassList("physical-terminal-presentation", active);

        public void SetPlanningDetailsHeld(bool held)
        {
            bool next = held && showingPlanning && physicalPlanning;
            if (planningDetailsHeld == next) return;
            planningDetailsHeld = next;
            RefreshPlanningVisibility();
        }

        private void RefreshPlanningVisibility()
        {
            bool physical = showingPlanning && physicalPlanning;
            root.EnableInClassList("physical-planning", physical);
            SetVisible(planner, showingPlanning && !physical);
            SetVisible(preflight, physical);
            SetVisible(preflightDetails, physical && planningDetailsHeld);
            SetVisible(hud, !showingPlanning || physical);
        }

        public void SetPhysicalPlanningInstruction(string text)
        {
            preflightObjective.text = text ?? string.Empty;
        }

        public void SetPhase(GamePhase current, MissionOutcome outcome)
            => SetPhase(current, outcome, false);

        public void SetPhase(GamePhase current, MissionOutcome outcome, bool interiorRepairsComplete)
        {
            root.EnableInClassList("report-visible", current == GamePhase.Report);
            root.EnableInClassList("intro-active", current == GamePhase.Intro);
            root.EnableInClassList("in-flight-active", current != GamePhase.Planning
                && current != GamePhase.Outcome && current != GamePhase.Report);
            root.EnableInClassList("survival-active", current == GamePhase.Survival);
            // Move the existing authoritative Water readout; keep its value/binding and other phase layouts.
            VisualElement water = hudResources[1].parent;
            VisualElement waterHost = current == GamePhase.Survival && outcome == MissionOutcome.None
                ? primaryTelemetry : expandedTelemetry;
            if (water.parent != waterHost)
                waterHost.Insert(waterHost == primaryTelemetry ? 2 : 0, water);
            switch (current)
            {
                case GamePhase.Planning: phase.text = "Mission preparation"; objective.text = string.Empty; break;
                case GamePhase.Intro: phase.text = "Complete the cockpit check"; objective.text = "You are Houston, aboard Aurora-7."; break;
                case GamePhase.Emergency: phase.text = "Restore power"; objective.text = string.Empty; break;
                case GamePhase.Repair:
                    phase.text = interiorRepairsComplete ? "Prepare for a spacewalk" : "Restore power and life support";
                    objective.text = interiorRepairsComplete ? "Put on the suit, then cycle the airlock." : string.Empty;
                    break;
                case GamePhase.Eva: phase.text = "Repair the antenna"; objective.text = "Spacewalk (EVA) · Stay attached to the safety line."; break;
                case GamePhase.Transmission: phase.text = "Restore contact with Earth"; objective.text = string.Empty; break;
                case GamePhase.Survival: phase.text = "Survive until rescue"; objective.text = "Manage your food, water and ship systems."; break;
                case GamePhase.Outcome:
                case GamePhase.Report:
                    phase.text = outcome == MissionOutcome.RecoveryPossible ? "Contact restored"
                        : outcome == MissionOutcome.Lost ? "Mission lost"
                        : outcome == MissionOutcome.Recovered ? "Rescue arrived" : "Mission report";
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
            SetVisible(preflightCountdown, available);
            SetVisible(preflightClockNote, available && enabled);
            SetVisible(preflightDeadlineRule, available);
            SetVisible(preflightLastValidDetail, available && enabled);
            planningTiming.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineRule.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            lastValidDetail.style.display = available && enabled ? DisplayStyle.Flex : DisplayStyle.None;
            preflightCountdown.EnableInClassList("deadline-practice", !enabled);
            if (!available) { displayedLastValid = null; return; }
            if (!enabled)
            {
                deadlineCountdown.text = "Untimed";
                deadlineCountdown.style.display = DisplayStyle.Flex;
                deadlineCountdown.RemoveFromClassList("deadline-urgent");
                deadlineRule.text = "Launch when your payload is ready. No automatic launch.";
                preflightCountdown.text = "Practice · no deadline";
                preflightCountdown.tooltip = "Untimed loading. Board the transport when ready.";
                preflightCountdown.RemoveFromClassList("deadline-urgent");
                preflightDeadlineRule.text = "Load cargo, then board the transport when ready. No automatic departure. Carried cargo is not aboard.";
                return;
            }
            bool validRemaining = !double.IsNaN(remainingRealSeconds) && !double.IsInfinity(remainingRealSeconds);
            deadlineCountdown.style.display = validRemaining ? DisplayStyle.Flex : DisplayStyle.None;
            deadlineCountdown.text = (paused ? "Paused · " : "Launch in ")
                + Number(Math.Ceiling(Math.Max(0, remainingRealSeconds)), "0") + " s";
            deadlineCountdown.EnableInClassList("deadline-urgent", validRemaining && remainingRealSeconds <= 10);
            SetVisible(preflightCountdown, validRemaining);
            double wholeSeconds = validRemaining ? Math.Ceiling(Math.Max(0, remainingRealSeconds)) : 0;
            preflightCountdown.text = Number(Math.Floor(wholeSeconds / 60), "00") + ":"
                + Number(wholeSeconds % 60, "00");
            preflightCountdown.tooltip = "Loading window: " + Number(wholeSeconds, "0") + " active real seconds remaining.";
            preflightClockNote.text = validRemaining && remainingRealSeconds <= 0
                ? "Loading closed · board the transport"
                : paused ? "Loading paused" : "Loading window · active real time";
            preflightCountdown.EnableInClassList("deadline-urgent", validRemaining && remainingRealSeconds <= 10);
            deadlineRule.text = "At zero, this payload launches automatically if valid. Otherwise, the last valid loadout below launches. The deadline uses unpaused real time.";
            preflightDeadlineRule.text = "At zero, loading closes. Board the transport to depart. The loading window uses unpaused real time. Carried cargo is not aboard.";
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
            preflightLastValidCounts.text = lastValidCounts.text;
        }

        public void SetCarriedEquipment(ICargoLoading cargo, IReadOnlyList<EquipmentSelection> catalog)
        {
            string id = cargo?.CarriedEquipmentId;
            if (string.IsNullOrEmpty(id))
            {
                preflightCarried.text = string.Empty;
                SetVisible(preflightCarried, false);
                preflightCarryPace.text = string.Empty;
                SetVisible(preflightCarryPace, false);
                return;
            }
            double unitMassKg = double.NaN;
            if (catalog != null)
                foreach (var item in catalog)
                    if (item.EquipmentId == id) { unitMassKg = item.UnitMassKg; break; }
            bool knownMass = Finite(unitMassKg) && unitMassKg >= 0;
            preflightCarried.text = knownMass ? Number(unitMassKg, "0.#") + " kg" : string.Empty;
            SetVisible(preflightCarried, knownMass);
            preflightCarryPace.text = "Carrying " + EquipmentCopy.Name(id) + " · "
                + Number(unitMassKg, "0.#") + " kg · not aboard.\nCarry pace: " + Number(cargo.CarrySpeedFactor * 100d, "0")
                + "% of unladen speed.";
            SetVisible(preflightCarryPace, true);
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
            bool inspect = systemsPinned || systemsHeld;
            SetVisible(expandedTelemetry, inspect);
            SetVisible(primaryTelemetry, true);
            SetVisible(oxygenGauge.Element, !evaContext || inspect);
            SetVisible(suitGauge.Element, evaContext);
            SetVisible(co2Gauge.Element, co2NeedsAttention || inspect);
            SetVisible(evaTelemetry, evaContext && inspect);
            instruments.EnableInClassList("inspecting-systems", inspect);
            instruments.EnableInClassList("eva-context", evaContext);
            systems.text = systemsPinned ? "Close systems" : "Tab  Systems";
            systems.EnableInClassList("systems-open", inspect);
        }

        /// <summary>Displays supplied interaction wording without issuing or simulating a command.</summary>
        public void ShowInteractionPrompt(string text, bool available)
        {
            if (string.IsNullOrWhiteSpace(text)) { ClearInteractionPrompt(); return; }
            bool hasKey = text.StartsWith("[E] ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("E — ", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("E · ", StringComparison.OrdinalIgnoreCase);
            interactionText.text = hasKey ? text.Substring(4) : text;
            SetVisible(interactionKey, available);
            interactionText.EnableInClassList("constraint-exceeded", !available);
            SetVisible(interactionPrompt, true);
        }

        public void ClearInteractionPrompt()
        {
            SetVisible(interactionPrompt, false);
            interactionText.text = string.Empty;
        }

        public void SetMicrogravityPrompt(bool showTutorial, bool showRail)
        {
            if (showMicrogravityTutorial == showTutorial && showRailPrompt == showRail) return;
            showMicrogravityTutorial = showTutorial;
            showRailPrompt = showRail;
            SetVisible(microgravityTutorial, showTutorial);
            SetVisible(railPrompt, showRail);
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

        private static VisualElement KeyPictogram(string key)
        {
            var cap = Text(key, "control-key");
            cap.name = "control-key-" + key.ToLowerInvariant();
            if (key == "SPACE") cap.AddToClassList("control-key-wide");
            cap.pickingMode = PickingMode.Ignore;
            return cap;
        }

        internal static VisualElement MousePictogram(string name, bool left, bool right)
        {
            var element = Element("mouse-pictogram");
            element.name = name;
            element.generateVisualContent += context =>
            {
                var painter = context.painter2D;
                painter.fillColor = new Color32(238, 237, 225, 235);
                painter.BeginPath();
                if (left)
                {
                    painter.MoveTo(new Vector2(3, 13));
                    painter.LineTo(new Vector2(3, 9));
                    painter.QuadraticCurveTo(new Vector2(3, 3), new Vector2(11, 2));
                    painter.LineTo(new Vector2(11, 13));
                    painter.ClosePath();
                }
                if (right)
                {
                    painter.MoveTo(new Vector2(13, 13));
                    painter.LineTo(new Vector2(13, 2));
                    painter.QuadraticCurveTo(new Vector2(21, 3), new Vector2(21, 9));
                    painter.LineTo(new Vector2(21, 13));
                    painter.ClosePath();
                }
                painter.Fill();
                painter.strokeColor = new Color32(245, 237, 222, 255);
                painter.lineWidth = 1.5f;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(12, 2));
                painter.QuadraticCurveTo(new Vector2(3, 2), new Vector2(3, 10));
                painter.LineTo(new Vector2(3, 19));
                painter.QuadraticCurveTo(new Vector2(3, 27), new Vector2(12, 27));
                painter.QuadraticCurveTo(new Vector2(21, 27), new Vector2(21, 19));
                painter.LineTo(new Vector2(21, 10));
                painter.QuadraticCurveTo(new Vector2(21, 2), new Vector2(12, 2));
                painter.ClosePath();
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(3, 13));
                painter.LineTo(new Vector2(21, 13));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(12, 2));
                painter.LineTo(new Vector2(12, 13));
                painter.Stroke();
            };
            return element;
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
            preflightLoadedCounts.text = FormatLoadoutCounts(selections, catalog);
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
            SetConstraint(preflightMass, massKg, massLimitKg, "kg");
            SetConstraint(preflightBudget, credits, creditLimit, "cr");
            SetConstraint(preflightPower, loadWatts, generationWatts, "W");
        }

        public void SetPlanningResources(ResourceSnapshot snapshot)
        {
            SetResources(planningResources, snapshot);
            SetResources(preflightResources, snapshot);
        }
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
            evaContext = status != null && (status.IsSuitSealed || status.IsOutside);
            RefreshTelemetry();
            if (!evaContext) return;
            double remaining = status.SuitOxygenSecondsRemaining;
            suitGauge.Value.text = Number(status.SuitOxygenKg, "0.000") + " kg";
            suitDuration.text = double.IsPositiveInfinity(remaining) ? "No draw"
                : !Finite(remaining) || remaining < 0 ? "—"
                : Number(Math.Ceiling(remaining / 60), "#,0") + " min sim";
            bool known = Finite(status.SuitOxygenKg) && status.SuitOxygenKg >= 0
                && Finite(status.SuitOxygenCapacityKg) && status.SuitOxygenCapacityKg > 0;
            double fraction = known ? status.SuitOxygenKg / status.SuitOxygenCapacityKg : 0;
            suitSeal.text = status.IsSuitSealed ? "Sealed" : "Suit open";
            suitGauge.SetLevel(known, fraction, known && fraction <= 0.25, known && fraction <= 0,
                known && fraction <= 0 ? "Empty" : known && fraction <= 0.25 ? "Low reserve" : string.Empty);
            suitGauge.Element.tooltip = "Suit oxygen: " + suitGauge.Value.text
                + ". Bar: actual suit capacity. Exact reserve and simulated duration: Tab systems.";
            suitDuration.tooltip = Number(remaining, "#,0") + " simulated seconds remaining. Supplied by the suit system.";
        }

        private void UpdateGauges()
        {
            SetReserveGauge(oxygenGauge, latestResources.OxygenKg, resourceReference.OxygenKg, "Stored oxygen reserve");
            SetReserveGauge(waterGauge, latestResources.WaterLitres, resourceReference.WaterLitres, "Stored water, not personal hydration");
            SetReserveGauge(foodGauge, latestResources.FoodKg, resourceReference.FoodKg, "Stored food mass, not hunger or stamina");
            SetReserveGauge(batteryGauge, latestResources.BatteryKWh, resourceReference.BatteryKWh, "Available energy");
            double scale = hasCo2Thresholds ? co2EmergencyPpm : 10000d;
            double current = latestResources.CarbonDioxidePpm;
            bool known = hasHudResources && Finite(current) && current >= 0;
            bool emergency = known && hasCo2Thresholds && current >= co2EmergencyPpm;
            bool action = known && hasCo2Thresholds && current >= co2ActionPpm;
            co2NeedsAttention = action;
            string stateText = emergency ? "Emergency" : action ? "Check life support" : string.Empty;
            co2Gauge.SetLevel(known, known ? current / scale : 0, action, emergency,
                stateText, !hasCo2Thresholds);
            co2Gauge.Element.tooltip = hasCo2Thresholds
                ? "Scenario thresholds from Core: action " + Number(co2ActionPpm, "#,0")
                    + " ppm; emergency " + Number(co2EmergencyPpm, "#,0") + " ppm."
                : "Instrument range only. No safety or warning threshold has been configured.";
            RefreshTelemetry();
        }

        private void SetReserveGauge(ResourceGauge gauge, double current, double reference, string description)
        {
            bool known = hasHudResources && hasResourceReference && Finite(current) && current >= 0
                && Finite(reference) && reference > 0;
            double fraction = known ? current / reference : 0;
            bool low = known && fraction <= 0.25;
            bool empty = known && current <= 0;
            gauge.SetLevel(known, fraction, low, empty,
                empty ? "Empty" : low ? "Low reserve" : string.Empty);
            gauge.Element.tooltip = description + ". " + gauge.Value.text
                + ". Bar is relative to the committed launch reserve, not capacity. Amber: 25% of launch reserve or less. Exact values: Tab systems.";
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public void SetValidation(bool succeeded, string explanation, string rejectedCommand)
        {
            validation.text = succeeded ? "Ready for launch."
                : string.IsNullOrWhiteSpace(explanation) ? "Review your payload before launch." : explanation;
            validation.EnableInClassList("constraint-exceeded", !succeeded);
            preflightValidation.text = succeeded ? "Loadout ready · board the transport"
                : string.IsNullOrWhiteSpace(explanation) ? "Loadout invalid. Review the limits." : explanation;
            preflightValidation.EnableInClassList("constraint-exceeded", !succeeded);
            commandFeedback.text = rejectedCommand ?? string.Empty;
            commandFeedback.style.display = string.IsNullOrWhiteSpace(rejectedCommand) ? DisplayStyle.None : DisplayStyle.Flex;
            launch.SetEnabled(succeeded && commandsEnabled && !physicalPlanning);
        }

        /// <summary>Ease painted bar widths only. ProgressBar values and warnings stay authoritative.</summary>
        public void AdvancePresentation(float elapsedRealSeconds, bool paused)
        {
            if (paused || float.IsNaN(elapsedRealSeconds) || float.IsInfinity(elapsedRealSeconds)
                || elapsedRealSeconds <= 0) return;
            float step = Math.Min(elapsedRealSeconds, 0.1f);
            oxygenGauge.Advance(step);
            waterGauge.Advance(step);
            foodGauge.Advance(step);
            batteryGauge.Advance(step);
            suitGauge.Advance(step);
            co2Gauge.Advance(step);
            exertionGauge.Advance(step);
        }

        public void Dispose()
        {
            QuantityChangeRequested = null;
            LaunchRequested = null;
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
            string[] names = { "Oxygen reserve", "Water", "Food stores", "Battery", "Spare parts", "Cabin CO₂" };
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

        private static void SetVisible(VisualElement element, bool visible) => element.EnableInClassList("is-hidden", !visible);

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
            private readonly ReserveFill fill;
            private readonly ResourcePictogram icon;
            private readonly Label note;
            private readonly string accessibleName;
            private bool initialized;

            public ResourceGauge(VisualElement parent, string title, int resourceIndex)
            {
                accessibleName = title;
                Element = FlightBoardView.Element("instrument-gauge");
                Element.AddToClassList("resource-cell");
                if (resourceIndex >= 0) Element.name = "resource-" + resourceIndex;
                icon = new ResourcePictogram(resourceIndex);
                icon.AddToClassList("resource-icon");
                Element.Add(icon);
                Element.Add(Text(title, "resource-label"));
                Value = Text("—", "resource-value");
                Element.Add(Value);
                bar = new ProgressBar { lowValue = 0f, highValue = 1f, value = 0f, title = string.Empty };
                bar.name = resourceIndex >= 0 ? "resource-bar-" + resourceIndex : "suit-oxygen-bar";
                bar.AddToClassList("gauge-bar");
                Element.Add(bar);
                fill = new ReserveFill();
                fill.AddToClassList("reserve-fill");
                bar.Add(fill);
                note = Text(string.Empty, "gauge-note");
                Element.Add(note);
                parent.Add(Element);
            }

            public void SetLevel(bool known, double fraction, bool low, bool critical, string detail, bool neutral = false)
            {
                // Retain the real ProgressBar value for existing lookups and accessibility consumers.
                bar.value = known ? (float)Math.Max(0, Math.Min(1, fraction)) : 0f;
                Color tint = critical ? new Color(1f, .43f, .36f)
                    : low ? new Color(.96f, .72f, .39f) : new Color(.95f, .94f, .89f);
                fill.SetTarget(bar.value, known, tint, !initialized || !known || critical);
                icon.SetTint(tint);
                initialized = known;
                Element.EnableInClassList("gauge-unknown", !known);
                Element.EnableInClassList("gauge-neutral", neutral);
                Element.EnableInClassList("gauge-low", low && !critical);
                Element.EnableInClassList("gauge-critical", critical);
                note.text = known ? detail : "Unavailable";
                SetVisible(note, !string.IsNullOrEmpty(note.text));
                bar.tooltip = accessibleName + ": " + Value.text + (string.IsNullOrEmpty(note.text) ? string.Empty : ". " + note.text);
            }

            public void Advance(float elapsedRealSeconds) => fill.Advance(elapsedRealSeconds);
        }

        private sealed class ReserveFill : VisualElement
        {
            private float displayed, target;
            private bool known;
            private Color tint;

            public ReserveFill()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetTarget(float value, bool available, Color color, bool snap)
            {
                target = value;
                known = available;
                tint = color;
                if (snap) displayed = target;
                MarkDirtyRepaint();
            }

            public void Advance(float seconds)
            {
                if (Math.Abs(target - displayed) < .0005f) return;
                displayed += (target - displayed) * (1f - (float)Math.Exp(-18f * seconds));
                if (Math.Abs(target - displayed) < .0005f) displayed = target;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                float width = contentRect.width, height = contentRect.height;
                if (width < 1 || height < 1) return;
                var painter = context.painter2D;
                const int count = 8;
                float segment = (width - (count - 1) * 2f) / count;
                for (int i = 0; i < count; i++)
                {
                    float x = i * (segment + 2f);
                    Rectangle(painter, x, 0, segment, height, new Color(.04f, .05f, .05f, .84f));
                    Rectangle(painter, x + 1, 1, segment - 2, height - 2, new Color(.95f, .94f, .89f, .22f));
                    float amount = known ? Mathf.Clamp01(displayed * count - i) : 0;
                    if (amount > 0) Rectangle(painter, x + 1, 1, (segment - 2) * amount, height - 2, tint);
                }
            }

            private static void Rectangle(Painter2D painter, float x, float y, float w, float h, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x + w, y));
                painter.LineTo(new Vector2(x + w, y + h));
                painter.LineTo(new Vector2(x, y + h));
                painter.ClosePath();
                painter.Fill();
            }
        }

        /// <summary>Original functional line pictograms; no imported art or icon package.</summary>
        private sealed class ResourcePictogram : VisualElement
        {
            private readonly int resource;
            private Color tint = new Color(.95f, .94f, .89f);
            public ResourcePictogram(int resourceIndex)
            {
                resource = resourceIndex;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }
            public void SetTint(Color value) { tint = value; MarkDirtyRepaint(); }
            private void Draw(MeshGenerationContext context)
            {
                if (contentRect.width < 1 || contentRect.height < 1) return;
                var p = context.painter2D;
                p.strokeColor = tint;
                p.lineWidth = 1.5f;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                if (resource == 6)
                {
                    Stroke(p, new Vector2(12, 2), new Vector2(14, 3), new Vector2(12, 5), new Vector2(10, 3), new Vector2(12, 2));
                    Stroke(p, new Vector2(4, 9), new Vector2(7, 6), new Vector2(11, 7), new Vector2(15, 10), new Vector2(18, 10));
                    Stroke(p, new Vector2(11, 7), new Vector2(8, 12), new Vector2(4, 17));
                    Stroke(p, new Vector2(8, 12), new Vector2(13, 13), new Vector2(15, 18));
                }
                else if (resource == 1)
                {
                    Stroke(p, new Vector2(10, 2), new Vector2(5, 9), new Vector2(4, 13), new Vector2(6, 17),
                        new Vector2(10, 19), new Vector2(14, 17), new Vector2(16, 13), new Vector2(15, 9), new Vector2(10, 2));
                    Stroke(p, new Vector2(7, 12), new Vector2(7, 14), new Vector2(9, 16));
                }
                else if (resource == 3)
                {
                    Stroke(p, new Vector2(2, 5), new Vector2(16, 5), new Vector2(16, 15), new Vector2(2, 15), new Vector2(2, 5));
                    Stroke(p, new Vector2(18, 8), new Vector2(18, 12));
                    Stroke(p, new Vector2(10, 7), new Vector2(7, 10), new Vector2(11, 10), new Vector2(8, 13));
                }
                else if (resource == 2)
                {
                    Stroke(p, new Vector2(6, 2), new Vector2(14, 2), new Vector2(14, 6), new Vector2(17, 17), new Vector2(3, 17), new Vector2(6, 6), new Vector2(6, 2));
                    Stroke(p, new Vector2(6, 6), new Vector2(14, 6));
                    Stroke(p, new Vector2(5, 12), new Vector2(15, 12));
                }
                else if (resource == 5)
                {
                    Stroke(p, new Vector2(10, 2), new Vector2(19, 18), new Vector2(1, 18), new Vector2(10, 2));
                    Stroke(p, new Vector2(10, 7), new Vector2(10, 12));
                    Stroke(p, new Vector2(10, 15), new Vector2(10, 15.2f));
                }
                else
                {
                    Stroke(p, new Vector2(8, 2), new Vector2(12, 2), new Vector2(12, 5), new Vector2(15, 7), new Vector2(15, 18), new Vector2(5, 18), new Vector2(5, 7), new Vector2(8, 5), new Vector2(8, 2));
                }
            }
            private static void Stroke(Painter2D painter, params Vector2[] points)
            {
                Color foreground = painter.strokeColor;
                painter.strokeColor = new Color(.04f, .05f, .05f, .84f);
                painter.lineWidth = 3.5f;
                painter.BeginPath();
                painter.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
                painter.Stroke();
                painter.strokeColor = foreground;
                painter.lineWidth = 1.5f;
                painter.Stroke();
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

using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Localization;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Builds the <see cref="MissionReport"/> from the session's authoritative state. It reads only; building a
    /// report never changes the mission. Runtime strings are plain ASCII so any UI font can show them.
    /// </summary>
    internal static class MissionReportBuilder
    {
        private const string Nasa01 = "NASA/JPL Horizons API: https://ssd-api.jpl.nasa.gov/doc/horizons.html";
        private const string Nasa02 = "NASA Environmental Control and Life Support Systems: https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/";
        private const string Nasa03 = "NASA Mars Communications Disruption and Delay: https://www.nasa.gov/wp-content/uploads/2024/01/mars-communications-disruption-and-delay.pdf";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static MissionReport Build(MissionSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            MissionDefinition definition = session.Definition;
            MissionDirector director = session.Director;
            MissionConfiguration launched = session.Planner.LaunchedConfiguration;
            MissionConfiguration configuration = launched ?? session.Planner.DraftConfiguration;
            MissionGameState state = session.State;
            CommunicationSystem link = director.Communications;
            EvaOperations eva = director.Eva;
            EvaSpec evaSpec = definition.Scenario.Eva;
            FieldModifications mods = director.FieldModifications;

            ResourceSnapshot atLaunch = configuration.InitialResources;
            ResourceSnapshot atEnd = session.Resources.IsInitialized ? session.Resources.Snapshot : atLaunch;
            double elapsed = state.ElapsedSimulationSeconds;
            bool final = state.Outcome != MissionOutcome.None;

            var report = new MissionReport
            {
                Outcome = state.Outcome,
                IsFinal = final,
                // LANG-01: the report is built in the player's language; the state keeps the English text.
                Headline = GameText.Translate(Headline(state.Outcome)),
                OutcomeReason = GameText.Translate(state.OutcomeReason),
                ElapsedSimulationSeconds = elapsed,
                MissionId = configuration.MissionId,
                LossCause = director.LossCause,
                MissionEpochAtStartUtc = director.Calendar.EpochAtStartUtc,
                MissionEpochUtc = director.Calendar.NowUtc,
                MissionDayNumber = director.Calendar.DayNumber,
                LoadoutName = LoadoutName(definition, configuration),
                UsedLastValidLoadoutAtDeadline = session.PlanningDeadline.UsedLastValidLoadoutAtDeadline,
                Equipment = configuration.Equipment,
                TotalMassKg = configuration.TotalMassKg,
                MassLimitKg = configuration.MassLimitKg,
                TotalBudgetCredits = configuration.TotalBudgetCredits,
                BudgetLimitCredits = configuration.BudgetLimitCredits,
                RequiredPowerWatts = configuration.RequiredPowerWatts,
                AvailablePowerWatts = configuration.AvailablePowerWatts,
                ResourcesAtLaunch = atLaunch,
                ResourcesAtEnd = atEnd,
                PeakCarbonDioxidePpm = Math.Max(director.PeakCarbonDioxidePpm, atEnd.CarbonDioxidePpm),
                CompletedEvaCount = eva.CompletedEvaCount,
                SuitPreparationCount = eva.SuitPreparationCount,
                AirlockCycleCount = eva.AirlockCycleCount,
                ScienceObjectives = configuration.ScienceObjectives,
                ScienceDataReturned = configuration.HasSciencePayload && !mods.SciencePayloadSalvaged
                    && (state.Outcome == MissionOutcome.RecoveryPossible || state.Outcome == MissionOutcome.Recovered),
                SciencePayloadSalvaged = mods.SciencePayloadSalvaged,
                AbsorberCartridgesBuilt = mods.CartridgesBuilt,
                AbsorberCartridgesInstalled = mods.CartridgesInstalled,
                AbsorbedCarbonDioxideKg = mods.AbsorbedCarbonDioxideKg,
                Communication = new ReportCommunication(link, director.Calendar, definition.Scenario.Pacing.TransmissionTimeScale),
                FalseSignalReplayed = director.LinkDiagnosticRequested,
                Holding = HoldingEstimate.Compute(atEnd, HoldingProjectionRates(session)),
                Timeline = director.Journal.Snapshot(),
                PlayedRealSeconds = session.PlayedRealSeconds,
                JumpedSimulationSeconds = session.JumpedSimulationSeconds,
            };

            report.Systems = BuildSystems(definition, director);
            report.Expenditures = BuildExpenditures(report, director, evaSpec, link);
            double? salvagedAt = mods.SalvagedAtSimulationSeconds;
            report.Sections = Array.AsReadOnly(new[]
            {
                OutcomeSection(report, session),
                MissionDesignSection(report, definition, session),
                ResourcesSection(report, session),
                SystemsSection(report, mods.Spec),
                ScienceSection(report, salvagedAt),
                CommunicationSection(report),
                TimelineSection(report),
                NasaDataSection(report),
                DerivedPhysicsSection(),
                FictionalScenarioSection(),
                TimeCompressionSection(report, definition, evaSpec),
            });
            return report;
        }

        private static string Headline(MissionOutcome outcome)
        {
            switch (outcome)
            {
                case MissionOutcome.Recovered:
                    return "RECOVERED";
                case MissionOutcome.RecoveryPossible:
                    return "RECOVERY POSSIBLE";
                case MissionOutcome.Lost:
                    return "MISSION LOST";
                default:
                    return "MISSION IN PROGRESS";
            }
        }

        private static string LoadoutName(MissionDefinition definition, MissionConfiguration configuration)
        {
            foreach (string presetId in definition.PresetIds)
            {
                IReadOnlyDictionary<string, int> preset = definition.GetPreset(presetId);
                bool same = true;
                foreach (EquipmentDefinition item in definition.Catalog.Definitions)
                {
                    preset.TryGetValue(item.Id, out int expected);
                    if (configuration.GetQuantity(item.Id) != expected)
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    return presetId;
                }
            }

            return "custom";
        }

        private static IReadOnlyList<ReportSystemStatus> BuildSystems(MissionDefinition definition, MissionDirector director)
        {
            var result = new List<ReportSystemStatus>();
            foreach (ShipSystemSpec spec in definition.Scenario.Systems)
            {
                ShipSystem system = director.Systems.Get(spec.SystemId);
                result.Add(new ReportSystemStatus(
                    spec.SystemId,
                    system.DisplayName,
                    spec.ConditionAfterAccident01,
                    system.Condition01,
                    system.WasDamaged,
                    system.IsOperational,
                    spec.RepairRequiresEva,
                    director.GetRepairTime(spec.SystemId),
                    system.DescribeCosts()));
            }

            return result.AsReadOnly();
        }

        private static IReadOnlyList<ReportExpenditure> BuildExpenditures(MissionReport report, MissionDirector director, EvaSpec evaSpec, CommunicationSystem link)
        {
            double repairOxygen = 0d;
            double repairWater = 0d;
            double repairFood = 0d;
            double repairBattery = 0d;
            int repairParts = 0;
            foreach (ShipSystem system in director.Systems.All)
            {
                foreach (ResourceAmount cost in system.RepairCosts)
                {
                    double total = cost.Quantity * system.RepairCount;
                    switch (cost.Resource)
                    {
                        case ResourceId.OxygenKg:
                            repairOxygen += total;
                            break;
                        case ResourceId.WaterLitres:
                            repairWater += total;
                            break;
                        case ResourceId.FoodKg:
                            repairFood += total;
                            break;
                        case ResourceId.BatteryKWh:
                            repairBattery += total;
                            break;
                        case ResourceId.SparePartsCount:
                            repairParts += (int)total;
                            break;
                    }
                }
            }

            double evaOxygen = (report.SuitPreparationCount * evaSpec.SuitPreparationOxygenKg) + (report.AirlockCycleCount * evaSpec.AirlockCycleOxygenLossKg);
            double evaBattery = report.AirlockCycleCount * evaSpec.AirlockCycleEnergyKWh;
            double transmitBattery = link.HasTransmitted ? link.TransmitEnergyKWh : 0d;
            FieldModifications mods = director.FieldModifications;
            double modsBattery = (mods.SciencePayloadSalvaged ? mods.Spec.SalvageEnergyKWh : 0d) + (mods.CartridgesBuilt * mods.Spec.CartridgeEnergyKWh);
            int modsParts = (mods.CartridgesBuilt * mods.Spec.CartridgeSpareParts) - (mods.SciencePayloadSalvaged ? mods.Spec.SalvageSpareParts : 0);

            ResourceSnapshot start = report.ResourcesAtLaunch;
            ResourceSnapshot end = report.ResourcesAtEnd;
            var overTime = new ReportExpenditure(
                "over time",
                (start.OxygenKg - end.OxygenKg) - repairOxygen - evaOxygen,
                (start.WaterLitres - end.WaterLitres) - repairWater,
                (start.FoodKg - end.FoodKg) - repairFood,
                (start.BatteryKWh - end.BatteryKWh) - repairBattery - evaBattery - transmitBattery - modsBattery,
                (start.SparePartsCount - end.SparePartsCount) - repairParts - modsParts);

            return Array.AsReadOnly(new[]
            {
                new ReportExpenditure("repairs", repairOxygen, repairWater, repairFood, repairBattery, repairParts),
                new ReportExpenditure("EVA", evaOxygen, 0d, 0d, evaBattery, 0),
                new ReportExpenditure("transmission", 0d, 0d, 0d, transmitBattery, 0),
                new ReportExpenditure("field modifications", 0d, 0d, 0d, modsBattery, modsParts),
                overTime,
            });
        }

        private static MissionReportSection OutcomeSection(MissionReport report, MissionSession session)
        {
            var lines = new List<MissionReportLine>
            {
                Line("Result", report.Headline),
            };
            if (report.IsFinal)
            {
                lines.Add(Line("Why", report.OutcomeReason));
            }
            else
            {
                lines.Add(Line("Status", "The mission is still running; this is a preview."));
            }

            lines.Add(Line("Mission time", string.Format(Invariant, "{0}, mission day {1} ({2})",
                MissionCalendar.FormatElapsed(report.ElapsedSimulationSeconds), report.MissionDayNumber, MissionCalendar.Format(report.MissionEpochUtc))));

            if (report.Outcome == MissionOutcome.RecoveryPossible)
            {
                lines.Add(Line("What it means", "Contact and recovery planning, not a rescue. No craft has arrived; the ship must hold on its own reserves."));
            }

            if (report.Outcome == MissionOutcome.Recovered && session.SurvivalRecovery != null)
            {
                lines.Add(Line("What it means", session.SurvivalRecovery.DescribeRecoveryForReport()));
            }

            if (report.IsFinal && report.Holding.IsBounded && report.LossCause == SurvivalFailure.None)
            {
                lines.Add(Line("Holding margin", string.Format(Invariant, "About {0:0} days at current use; {1} runs out first (estimate).",
                    Math.Floor(report.Holding.Days), report.Holding.LimitedBy)));
            }

            if (report.Outcome == MissionOutcome.Lost)
            {
                lines.Add(Line("Cause", DescribeCause(report.LossCause)));
            }

            return Section(MissionReportSections.Outcome, "Outcome", lines);
        }

        private static ResourceRates HoldingProjectionRates(MissionSession session)
        {
            ResourceRates rates = session.Resources.Rates;
            if (!session.Survival.ContactRestoredAtSimulationSeconds.HasValue) return rates;
            // Projection only. Actual food/water debits are the crew's commands, the rest draw and crop irrigation.
            double crewDaysPerSecond = session.Definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
            return new ResourceRates(rates.OxygenUseKgPerSecond,
                CrewMetabolicAssumptions.WaterLitresPerCrewDay * crewDaysPerSecond + session.Survival.GrowWaterLitresPerSecond,
                CrewMetabolicAssumptions.FoodKgPerCrewDay * crewDaysPerSecond, rates.NetLoadWatts, rates.CarbonDioxideChangePpmPerSecond);
        }

        private static string DescribeCause(SurvivalFailure cause)
        {
            switch (cause)
            {
                case SurvivalFailure.OxygenDepleted:
                    return "Oxygen: the stored reserve and then the cabin air ran out.";
                case SurvivalFailure.CarbonDioxideLimit:
                    return "Carbon dioxide: the cabin reached the emergency level.";
                case SurvivalFailure.HungerLimit:
                    return "Unmet food demand reached the scenario's game viability limit; not a medical starvation threshold.";
                case SurvivalFailure.ThirstLimit:
                    return "Unmet water demand reached the scenario's game viability limit; not a medical dehydration threshold.";
                case SurvivalFailure.WaterDepleted:
                    return "Water: the reserve ran out.";
                case SurvivalFailure.FoodDepleted:
                    return "Food: the reserve ran out.";
                case SurvivalFailure.SuitOxygenDepleted:
                    return "Suit oxygen: the tank ran empty before the crew was back in a pressurized cabin.";
                case SurvivalFailure.ContactImpossible:
                    return "Unrecoverable mission objective: too few spare parts to repair the antenna. The crew was alive when the mission was closed.";
                default:
                    return "Ended by mission control.";
            }
        }

        private static MissionReportSection MissionDesignSection(MissionReport report, MissionDefinition definition, MissionSession session)
        {
            var lines = new List<MissionReportLine>
            {
                Line("Loadout", report.LoadoutName == "custom" ? "Custom loadout" : "Preset '" + report.LoadoutName + "'"),
            };
            foreach (EquipmentSelection selection in report.Equipment)
            {
                if (selection.Quantity <= 0)
                {
                    continue;
                }

                string name = definition.Catalog.TryGet(selection.EquipmentId, out EquipmentDefinition item) ? item.DisplayName : selection.EquipmentId;
                string power = selection.UnitPowerWatts > 0d
                    ? string.Format(Invariant, ", {0:0} W load", selection.UnitPowerWatts * selection.Quantity)
                    : string.Empty;
                lines.Add(Line(string.Format(Invariant, "{0} x {1}", selection.Quantity, name),
                    string.Format(Invariant, "{0:0} kg, {1:0} credits{2}", selection.UnitMassKg * selection.Quantity, selection.UnitBudgetCredits * selection.Quantity, power)));
            }

            lines.Add(Line("Payload mass", string.Format(Invariant, "{0:0} of {1:0} kg", report.TotalMassKg, report.MassLimitKg)));
            lines.Add(Line("Budget", string.Format(Invariant, "{0:0} of {1:0} credits (fictional)", report.TotalBudgetCredits, report.BudgetLimitCredits)));
            lines.Add(Line("Power", string.Format(Invariant, "{0:#,0} W load on {1:#,0} W generation", report.RequiredPowerWatts, report.AvailablePowerWatts)));

            int partsForAllRepairs = 0;
            foreach (ShipSystemSpec spec in definition.Scenario.Systems)
            {
                if (spec.ConditionAfterAccident01 >= 1d)
                {
                    continue;
                }

                foreach (ResourceAmount cost in spec.RepairCosts)
                {
                    if (cost.Resource == ResourceId.SparePartsCount)
                    {
                        partsForAllRepairs += (int)cost.Quantity;
                    }
                }
            }

            lines.Add(Line("Spare parts at launch", string.Format(Invariant, "{0}; the three repairs need {1}", report.ResourcesAtLaunch.SparePartsCount, partsForAllRepairs)));

            string planning;
            if (report.UsedLastValidLoadoutAtDeadline)
            {
                planning = "Launched automatically at the planning deadline with the last valid loadout.";
            }
            else if (session.PlanningDeadline.IsEnabled)
            {
                planning = session.PlanningDeadline.HasExpired
                    ? "Launched automatically at the planning deadline with the loadout as drafted."
                    : "Launched by the crew inside the timed planning window.";
            }
            else
            {
                planning = "Launched by the crew; planning was not timed.";
            }

            lines.Add(Line("Planning", planning));
            return Section(MissionReportSections.MissionDesign, "Mission design", lines);
        }

        private static MissionReportSection ResourcesSection(MissionReport report, MissionSession session)
        {
            ResourceSnapshot start = report.ResourcesAtLaunch;
            ResourceSnapshot end = report.ResourcesAtEnd;
            var lines = new List<MissionReportLine>
            {
                Line("Oxygen", string.Format(Invariant, "{0:0.00} kg at launch, {1:0.00} kg left", start.OxygenKg, end.OxygenKg)),
                Line("Water", string.Format(Invariant, "{0:0.0} L at launch, {1:0.0} L left", start.WaterLitres, end.WaterLitres)),
                Line("Food", string.Format(Invariant, "{0:0.0} kg at launch, {1:0.0} kg left", start.FoodKg, end.FoodKg)),
                Line("Battery", string.Format(Invariant, "{0:0.00} kWh at launch, {1:0.00} kWh left", start.BatteryKWh, end.BatteryKWh)),
                Line("Spare parts", string.Format(Invariant, "{0} at launch, {1} left", start.SparePartsCount, end.SparePartsCount)),
                Line("Cabin CO2", string.Format(Invariant, "{0:#,0} ppm at launch, {1:#,0} ppm at the end, peak {2:#,0} ppm (action level {3:#,0}, loss limit {4:#,0})",
                    start.CarbonDioxidePpm, end.CarbonDioxidePpm, report.PeakCarbonDioxidePpm,
                    SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm)),
            };

            foreach (ReportExpenditure expenditure in report.Expenditures)
            {
                if (expenditure.Purpose == "over time")
                {
                    string battery = expenditure.BatteryKWh >= 0d
                        ? string.Format(Invariant, "battery net {0:0.00} kWh drawn", expenditure.BatteryKWh)
                        : string.Format(Invariant, "battery net {0:0.00} kWh recharged", -expenditure.BatteryKWh);
                    lines.Add(Line(session.Survival.ContactRestoredAtSimulationSeconds.HasValue ? "Net use including survival actions" : "Used over time", string.Format(Invariant, "{0:0.00} kg oxygen, {1:0.0} L water, {2:0.0} kg food; {3}",
                        expenditure.OxygenKg, expenditure.WaterLitres, expenditure.FoodKg, battery)));
                    continue;
                }

                if (expenditure.Purpose == "field modifications")
                {
                    if (report.SciencePayloadSalvaged || report.AbsorberCartridgesBuilt > 0)
                    {
                        lines.Add(Line("Field modifications", DescribeFieldModifications(report, expenditure)));
                    }

                    continue;
                }

                lines.Add(Line("Spent on " + (expenditure.Purpose == "EVA" ? "the EVA" : expenditure.Purpose == "transmission" ? "the transmission" : expenditure.Purpose),
                    DescribeSpending(expenditure)));
            }

            if (session.Survival.ContactRestoredAtSimulationSeconds.HasValue)
            {
                lines.Add(Line("Survival accounting", "Awake, food and drinking water were spent by crew commands; while resting they were drawn at the BVAD crew-day rates. Crop irrigation and power used the same ledger. Net use includes building, planting and the one harvest credit; actions are in the timeline."));
                lines.Add(Line("Holding projection", "Food and water assume full BVAD crew-day portions, not the zero automatic meal rate. This estimate is not a rescue ETA."));
                lines.Add(Line("Grow module", session.Survival.GrowStage.ToString()));
                if (session.SurvivalRecovery != null)
                {
                    lines.Add(Line("Rest", session.SurvivalRecovery.DescribeRestForReport()));
                    lines.Add(Line("Recovery beacon", session.SurvivalRecovery.DescribeBeaconForReport()));
                }
            }
            return Section(MissionReportSections.Resources, "Resources", lines);
        }

        private static string DescribeFieldModifications(MissionReport report, ReportExpenditure expenditure)
        {
            var parts = new List<string>();
            if (report.SciencePayloadSalvaged)
            {
                parts.Add("science instrument dismantled for spare parts");
            }

            if (report.AbsorberCartridgesBuilt > 0)
            {
                parts.Add(string.Format(Invariant, "{0} CO2 absorber cartridge{1} built", report.AbsorberCartridgesBuilt, report.AbsorberCartridgesBuilt == 1 ? string.Empty : "s"));
            }

            string net = expenditure.SpareParts < 0
                ? string.Format(Invariant, "net +{0} spare part{1}", -expenditure.SpareParts, expenditure.SpareParts == -1 ? string.Empty : "s")
                : string.Format(Invariant, "net {0} spare part{1} used", expenditure.SpareParts, expenditure.SpareParts == 1 ? string.Empty : "s");
            return string.Format(Invariant, "{0}; {1}, {2:0.00} kWh", string.Join(", ", parts), net, expenditure.BatteryKWh);
        }

        private static string DescribeSpending(ReportExpenditure expenditure)
        {
            var parts = new List<string>();
            if (expenditure.SpareParts > 0)
            {
                parts.Add(string.Format(Invariant, "{0} spare part{1}", expenditure.SpareParts, expenditure.SpareParts == 1 ? string.Empty : "s"));
            }

            if (expenditure.OxygenKg > 0d)
            {
                parts.Add(string.Format(Invariant, "{0:0.00} kg oxygen", expenditure.OxygenKg));
            }

            if (expenditure.WaterLitres > 0d)
            {
                parts.Add(string.Format(Invariant, "{0:0.0} L water", expenditure.WaterLitres));
            }

            if (expenditure.FoodKg > 0d)
            {
                parts.Add(string.Format(Invariant, "{0:0.0} kg food", expenditure.FoodKg));
            }

            if (expenditure.BatteryKWh > 0d)
            {
                parts.Add(string.Format(Invariant, "{0:0.00} kWh", expenditure.BatteryKWh));
            }

            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        private static MissionReportSection SystemsSection(MissionReport report, FieldModificationSpec modificationSpec)
        {
            var lines = new List<MissionReportLine>();
            foreach (ReportSystemStatus system in report.Systems)
            {
                string value;
                if (!system.WasDamaged)
                {
                    value = "Not damaged.";
                }
                else if (system.RepairedAtSimulationSeconds.HasValue)
                {
                    value = string.Format(Invariant, "{0:0} % after the impact; repaired at {1} for {2}{3}.",
                        system.ConditionAfterAccident01 * 100d, MissionCalendar.FormatElapsed(system.RepairedAtSimulationSeconds.Value),
                        system.RepairCosts, system.RepairRequiresEva ? " during the EVA" : string.Empty);
                }
                else
                {
                    value = string.Format(Invariant, "{0:0} % after the impact; never repaired (needs {1}{2}).",
                        system.ConditionAfterAccident01 * 100d, system.RepairCosts, system.RepairRequiresEva ? " and an EVA" : string.Empty);
                }

                lines.Add(Line(Capitalize(system.DisplayName), value));
            }

            string evaValue = report.SuitPreparationCount == 0
                ? "No EVA was attempted."
                : string.Format(Invariant, "{0} completed; the suit was put on {1} time{2} and the airlock cycled {3} time{4}.",
                    report.CompletedEvaCount, report.SuitPreparationCount, report.SuitPreparationCount == 1 ? string.Empty : "s",
                    report.AirlockCycleCount, report.AirlockCycleCount == 1 ? string.Empty : "s");
            lines.Add(Line("EVA", evaValue));
            if (report.AbsorberCartridgesBuilt > 0)
            {
                lines.Add(Line("CO2 absorber", string.Format(Invariant,
                    "{0} of {1} built cartridge{2} installed; {3:0.00} kg CO2 taken up. GAME ASSUMPTION: {4:0.000} kg per cartridge, stated as {5:0.##} crew-hours of the cited {6:0.00} kg per crew-day; not a rated scrubber.",
                    report.AbsorberCartridgesInstalled, report.AbsorberCartridgesBuilt, report.AbsorberCartridgesBuilt == 1 ? string.Empty : "s",
                    report.AbsorbedCarbonDioxideKg, modificationSpec.CartridgeCapacityKg, modificationSpec.CartridgeCrewHours,
                    CrewMetabolicAssumptions.CarbonDioxideKgPerCrewDay)));
            }

            return Section(MissionReportSections.Systems, "Systems and repairs", lines);
        }

        private static MissionReportSection ScienceSection(MissionReport report, double? salvagedAtSimulationSeconds)
        {
            var lines = new List<MissionReportLine>();
            if (report.ScienceObjectives == null || report.ScienceObjectives.Count == 0)
            {
                lines.Add(Line("Objective", "No science payload was carried: its mass, power and budget went to reserves and spares."));
                lines.Add(Line("Science return", "None possible."));
                return Section(MissionReportSections.Science, "Science", lines);
            }

            foreach (string objective in report.ScienceObjectives)
            {
                lines.Add(Line("Objective", objective));
            }

            string result;
            if (report.SciencePayloadSalvaged)
            {
                result = "Abandoned: the instrument was dismantled for spare parts at "
                    + MissionCalendar.FormatElapsed(salvagedAtSimulationSeconds ?? 0d) + ". Nothing can be returned.";
            }
            else if (report.ScienceDataReturned)
            {
                result = "Returned: with the link restored, the instrument's data can be sent to Earth.";
            }
            else if (report.Outcome == MissionOutcome.Lost)
            {
                result = "Not returned: the mission was lost without contact, so the data never reached Earth.";
            }
            else
            {
                result = "Pending: nothing can be returned until contact is restored.";
            }

            lines.Add(Line("Science return", result));
            return Section(MissionReportSections.Science, "Science", lines);
        }

        private static MissionReportSection CommunicationSection(MissionReport report)
        {
            ReportCommunication c = report.Communication;
            var lines = new List<MissionReportLine>
            {
                Line("Reference", "NASA/JPL Horizons, dataset " + c.DatasetId),
            };

            if (c.HasTransmitted && c.TransmittedAtEpochUtc.HasValue)
            {
                lines.Add(Line("Mission date at transmission", string.Format(Invariant, "{0} (mission day {1})",
                    MissionCalendar.Format(c.TransmittedAtEpochUtc.Value), c.TransmittedOnMissionDay)));
            }
            else
            {
                lines.Add(Line("Mission date", string.Format(Invariant, "{0} (mission day {1})",
                    MissionCalendar.Format(c.MissionEpochUtc), report.MissionDayNumber)));
            }

            if (!c.IsReferenceAvailable)
            {
                lines.Add(Line("Reference sample", string.Format(Invariant, "None: the snapshot ends on {0}. No call could be sent after that date.",
                    MissionCalendar.Format(c.SnapshotLastEpochUtc))));
            }
            else
            {
                lines.Add(Line("Reference sample", string.Format(Invariant, "{0} (row {1} of {2}; last sample at or before the mission date)",
                    MissionCalendar.Format(c.ReferenceEpochUtc), c.ReferenceSampleIndex + 1, c.SampleCount)));
                lines.Add(Line("Earth distance", CommunicationSystem.FormatDistance(c.ReferenceDistanceKm) + " (Earth-Mars range, used as a Mars-vicinity proxy)"));
                lines.Add(Line("One-way light time", string.Format(Invariant, "{0} ({1:0.000} s)",
                    CommunicationSystem.FormatDuration(c.OneWayLightSeconds), c.OneWayLightSeconds)));
                lines.Add(Line("Earth processing", CommunicationSystem.FormatDuration(c.EarthProcessingSeconds) + " (game assumption)"));
                lines.Add(Line("Earliest reply", CommunicationSystem.FormatDuration(c.EarliestReplySeconds) + " = 2 x light time + processing"));
            }

            if (report.FalseSignalReplayed)
            {
                lines.Add(Line("Link diagnostic", "The signal found before the transmission was the damaged receive buffer replaying an old local recording. It was not a reply from Earth and changed nothing."));
            }

            if (!c.HasTransmitted)
            {
                lines.Add(Line("Transmission", "No distress call was sent."));
            }
            else
            {
                lines.Add(Line("Transmitted", string.Format(Invariant, "{0}, using {1:0.##} kWh",
                    MissionCalendar.FormatElapsed(c.TransmittedAtSimulationSeconds ?? 0d), c.TransmitEnergyKWh)));
                if (c.ReplyReceived)
                {
                    lines.Add(Line("Reply received", string.Format(Invariant, "{0}, {1} after the transmission",
                        MissionCalendar.FormatElapsed(c.ReplyReceivedAtSimulationSeconds ?? 0d),
                        CommunicationSystem.FormatDuration(c.ReplyWaitSimulationSeconds ?? 0d))));
                }
                else if (c.ReplyRemainingSimulationSeconds > 0d)
                {
                    lines.Add(Line("Reply", "Not received: it could not arrive for another " + CommunicationSystem.FormatDuration(c.ReplyRemainingSimulationSeconds, true) + "."));
                }
                else
                {
                    lines.Add(Line("Reply", "Not received: the link had no power when the reply arrived."));
                }

                lines.Add(Line("Shown to the player", string.Format(Invariant, "At x{0:0.##} the {1} wait takes about {2:0} real seconds.",
                    c.TransmissionTimeScale, CommunicationSystem.FormatDuration(c.EarliestReplySeconds), c.EarliestReplySeconds / c.TransmissionTimeScale)));
            }

            return Section(MissionReportSections.Communication, "Communication", lines);
        }

        private static MissionReportSection TimelineSection(MissionReport report)
        {
            var lines = new List<MissionReportLine>();
            foreach (MissionLogEntry entry in report.Timeline)
            {
                lines.Add(Line(MissionCalendar.FormatElapsed(entry.AtSimulationSeconds), entry.Text));
            }

            return Section(MissionReportSections.Timeline, "Timeline", lines);
        }

        private static MissionReportSection NasaDataSection(MissionReport report)
        {
            ReportCommunication c = report.Communication;
            var lines = new List<MissionReportLine>
            {
                Line("Dataset", c.DatasetId),
                Line("Provider", HorizonsEarthMars2026Snapshot.Provider),
                Line("Content", string.Format(Invariant, "{0} daily samples, {1} to {2}: Mars centre relative to Earth centre, km, {3}; {4}.",
                    c.SampleCount, MissionCalendar.Format(c.SnapshotFirstEpochUtc), MissionCalendar.Format(c.SnapshotLastEpochUtc),
                    HorizonsEarthMars2026Snapshot.Frame, "geometric state vectors without light-time correction")),
                Line("SHA-256", c.DatasetSha256),
                Line("Packaged copy", "Compiled into the game from " + HorizonsEarthMars2026Snapshot.SourcePath + " and checked against this hash when the mission starts. The game makes no network request."),
                Line("NASA-01", Nasa01),
                Line("NASA-02", Nasa02),
                Line("NASA-03", Nasa03),
                Line("Crew consumption", string.Format(Invariant,
                    "NASA Life Support Baseline Values and Assumptions Document (NASA/TP-2015-218570/REV2): oxygen {0:0.00} kg, water {1:0.0} L, food {2:0.00} kg and CO2 output {3:0.00} kg per crew-day.",
                    CrewMetabolicAssumptions.OxygenKgPerCrewDay, CrewMetabolicAssumptions.WaterLitresPerCrewDay,
                    CrewMetabolicAssumptions.FoodKgPerCrewDay, CrewMetabolicAssumptions.CarbonDioxideKgPerCrewDay)),
            };
            return Section(MissionReportSections.NasaData, "NASA/JPL data", lines);
        }

        private static MissionReportSection DerivedPhysicsSection()
        {
            var lines = new List<MissionReportLine>
            {
                Line("Light time", "one-way seconds = distance in km / 299,792.458 km/s"),
                Line("Reply", "earliest reply = 2 x one-way light time + processing on Earth, with the distance frozen at the transmission"),
                Line("Energy", "kWh = watts x seconds / 3,600,000"),
                Line("Cabin CO2", string.Format(Invariant, "ppm from the ideal gas law for a {0:0} m3 cabin at {1:0.0} kPa and {2:0} K (cabin figures are game assumptions)",
                    CabinAtmosphere.CabinFreeVolumeCubicMetres, CabinAtmosphere.CabinPressureKPa, CabinAtmosphere.CabinTemperatureKelvin)),
            };
            return Section(MissionReportSections.DerivedPhysics, "Derived physics", lines);
        }

        private static MissionReportSection FictionalScenarioSection()
        {
            var lines = new List<MissionReportLine>
            {
                Line("Spacecraft", "AURORA-7 and its mission are fictional. No trajectory is computed: the Earth-Mars range stands in for a ship in Mars vicinity."),
                Line("Accident", "The debris strike and the damage it does are authored events."),
                Line("Solo EVA", "A one-person EVA is a fictional emergency: the cited procedures require two crew members. Suit and airlock figures follow published references for other designs."),
                Line("Life support", "Simplified for education. Consumption follows the cited NASA document; damage, leak and scrubber rates and repair costs are gameplay tuning. Loss levels are game rules anchored to cited operational levels, not medical limits."),
                Line("Communication", "Link availability, occultation, acquisition and message length are not modelled. Processing time on Earth and transmit energy are game assumptions."),
                Line("False signal", "The signal heard before the transmission is a story beat: a damaged local buffer replaying an old recording. It is not an echo from space and not a model of a NASA link."),
                Line("Field modifications", "The salvage yield and the cartridge's cost and capacity are game assumptions. The capacity is stated in crew-hours of the cited metabolic CO2 output; it is not sized from or rated like a real lithium-hydroxide canister."),
                Line("Outcome", "A reply permits recovery planning. It does not show that a rescue is feasible."),
                Line("Budget", "Credits are fictional, not cost estimates."),
                Line("Attribution", "Earth-Mars reference ephemeris: NASA/JPL Horizons. AURORA-7 uses a fictional Mars-vicinity distance approximation. Life-support behavior is simplified for education. Mission timing may be compressed."),
            };
            return Section(MissionReportSections.FictionalScenario, "Fictional scenario and simplifications", lines);
        }

        private static MissionReportSection TimeCompressionSection(MissionReport report, MissionDefinition definition, EvaSpec evaSpec)
        {
            MissionPacing pacing = definition.Scenario.Pacing;
            var lines = new List<MissionReportLine>
            {
                Line("Simulated mission time", CommunicationSystem.FormatDuration(report.ElapsedSimulationSeconds)),
            };
            if (report.PlayedRealSeconds > 0d)
            {
                lines.Add(Line("Real play time", "About " + CommunicationSystem.FormatDuration(report.PlayedRealSeconds) + " in flight phases (pauses and cinematics excluded)."));
            }

            lines.Add(Line("Time scales", string.Format(Invariant,
                "Cockpit check x{0:0.##}; emergency and repair x{1:0.##}; EVA x{2:0.##}; transmission x{3:0.##} (simulated seconds per real second).",
                pacing.IntroTimeScale, pacing.InteriorTimeScale, pacing.EvaTimeScale, pacing.TransmissionTimeScale)));

            if (report.JumpedSimulationSeconds > 0d)
            {
                string suit = report.SuitPreparationCount > 0
                    ? string.Format(Invariant, " (each suit preparation is {0})", CommunicationSystem.FormatDuration(evaSpec.SuitPreparationSeconds))
                    : string.Empty;
                lines.Add(Line("Chapter jumps", CommunicationSystem.FormatDuration(report.JumpedSimulationSeconds)
                    + " of simulated time passed in jumps" + suit + ". Reserves and the communication clock advanced through them."));
            }
            else
            {
                lines.Add(Line("Chapter jumps", "None."));
            }

            return Section(MissionReportSections.TimeCompression, "Time compression", lines);
        }

        private static MissionReportSection Section(string id, string title, List<MissionReportLine> lines)
        {
            return new MissionReportSection(id, GameText.Translate(title), lines.AsReadOnly());
        }

        private static MissionReportLine Line(string label, string value)
        {
            return new MissionReportLine(GameText.Translate(label), GameText.Translate(value));
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}

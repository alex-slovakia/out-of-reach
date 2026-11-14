using System;
using System.Collections.Generic;
using System.Text;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>Stable section IDs, in report order. The four last ones keep data, physics, fiction and pacing apart.</summary>
    public static class MissionReportSections
    {
        public const string Outcome = "outcome";
        public const string MissionDesign = "mission-design";
        public const string Resources = "resources";
        public const string Systems = "systems";
        public const string Science = "science";
        public const string Communication = "communication";
        public const string Timeline = "timeline";
        public const string NasaData = "nasa-data";
        public const string DerivedPhysics = "derived-physics";
        public const string FictionalScenario = "fictional-scenario";
        public const string TimeCompression = "time-compression";

        public static readonly IReadOnlyList<string> Ordered = Array.AsReadOnly(new[]
        {
            Outcome, MissionDesign, Resources, Systems, Science, Communication, Timeline,
            NasaData, DerivedPhysics, FictionalScenario, TimeCompression,
        });
    }

    /// <summary>
    /// What one kind of activity cost. Battery energy may be negative for "over time" (net charging) and spare
    /// parts may be negative for "field modifications" (a net gain from salvage).
    /// </summary>
    public readonly struct ReportExpenditure
    {
        public ReportExpenditure(string purpose, double oxygenKg, double waterLitres, double foodKg, double batteryKWh, int spareParts)
        {
            Purpose = purpose ?? string.Empty;
            OxygenKg = oxygenKg;
            WaterLitres = waterLitres;
            FoodKg = foodKg;
            BatteryKWh = batteryKWh;
            SpareParts = spareParts;
        }

        public string Purpose { get; }

        public double OxygenKg { get; }

        public double WaterLitres { get; }

        public double FoodKg { get; }

        public double BatteryKWh { get; }

        public int SpareParts { get; }
    }

    /// <summary>Damage and repair record of one ship system.</summary>
    public readonly struct ReportSystemStatus
    {
        public ReportSystemStatus(ShipSystemId systemId, string displayName, double conditionAfterAccident01, double finalCondition01,
            bool wasDamaged, bool isOperational, bool repairRequiresEva, double? repairedAtSimulationSeconds, string repairCosts)
        {
            SystemId = systemId;
            DisplayName = displayName ?? systemId.ToString();
            ConditionAfterAccident01 = conditionAfterAccident01;
            FinalCondition01 = finalCondition01;
            WasDamaged = wasDamaged;
            IsOperational = isOperational;
            RepairRequiresEva = repairRequiresEva;
            RepairedAtSimulationSeconds = repairedAtSimulationSeconds;
            RepairCosts = repairCosts ?? string.Empty;
        }

        public ShipSystemId SystemId { get; }

        public string DisplayName { get; }

        /// <summary>Condition the scripted impact leaves (1 = untouched).</summary>
        public double ConditionAfterAccident01 { get; }

        public double FinalCondition01 { get; }

        public bool WasDamaged { get; }

        public bool IsOperational { get; }

        public bool RepairRequiresEva { get; }

        /// <summary>Simulation time of the repair, or null if it was never repaired.</summary>
        public double? RepairedAtSimulationSeconds { get; }

        public string RepairCosts { get; }
    }

    /// <summary>The numbers behind the communication section. Distances in km, times in simulated seconds.</summary>
    public sealed class ReportCommunication
    {
        internal ReportCommunication(CommunicationSystem link, MissionCalendar calendar, double transmissionTimeScale)
        {
            EarthMarsSample sample = link.ReferenceSample;
            DatasetId = link.AstronomicalReferenceId;
            DatasetSha256 = link.Reference.Sha256;
            SampleCount = link.Reference.Count;
            SnapshotFirstEpochUtc = link.Reference.FirstEpochUtc;
            SnapshotLastEpochUtc = link.Reference.LastEpochUtc;
            IsReferenceAvailable = link.IsReferenceAvailable;
            State = link.State;
            HasTransmitted = link.HasTransmitted;
            ReplyReceived = link.ReplyReceivedAtSimulationSeconds.HasValue;
            ReferenceSampleIndex = sample.Index;
            ReferenceEpochUtc = sample.EpochUtc;
            ReferenceDistanceKm = sample.DistanceKm;
            OneWayLightSeconds = sample.OneWayLightSeconds;
            MinimumRoundTripSeconds = sample.MinimumRoundTripSeconds;
            EarthProcessingSeconds = link.EarthProcessingSeconds;
            EarliestReplySeconds = link.ReplyDelaySimulationSeconds;
            TransmitEnergyKWh = link.TransmitEnergyKWh;
            TransmittedAtSimulationSeconds = link.TransmittedAtSimulationSeconds;
            TransmittedAtEpochUtc = link.TransmittedAtEpochUtc;
            TransmittedOnMissionDay = link.TransmittedAtSimulationSeconds.HasValue
                ? MissionCalendar.DayNumberAt(link.TransmittedAtSimulationSeconds.Value)
                : (int?)null;
            ReplyDueAtSimulationSeconds = link.ReplyDueAtSimulationSeconds;
            ReplyReceivedAtSimulationSeconds = link.ReplyReceivedAtSimulationSeconds;
            ReplyRemainingSimulationSeconds = link.ReplyRemainingSimulationSeconds;
            TransmissionTimeScale = transmissionTimeScale;
            MissionEpochUtc = calendar.NowUtc;
        }

        /// <summary>dataset_id of the NASA/JPL Horizons snapshot the delay was computed from.</summary>
        public string DatasetId { get; }

        /// <summary>SHA-256 of the packaged normalized CSV.</summary>
        public string DatasetSha256 { get; }

        public int SampleCount { get; }

        public DateTime SnapshotFirstEpochUtc { get; }

        public DateTime SnapshotLastEpochUtc { get; }

        /// <summary>False when the mission date ran past the snapshot and no call was sent.</summary>
        public bool IsReferenceAvailable { get; }

        public CommunicationState State { get; }

        public bool HasTransmitted { get; }

        public bool ReplyReceived { get; }

        /// <summary>Zero-based row of the reference sample (frozen at transmission, otherwise the mission date's).</summary>
        public int ReferenceSampleIndex { get; }

        public DateTime ReferenceEpochUtc { get; }

        /// <summary>NASA/JPL DATA: geometric Earth-Mars range of the sample, km.</summary>
        public double ReferenceDistanceKm { get; }

        /// <summary>DERIVED: distance / speed of light, seconds.</summary>
        public double OneWayLightSeconds { get; }

        /// <summary>DERIVED: twice the one-way light time, seconds.</summary>
        public double MinimumRoundTripSeconds { get; }

        /// <summary>GAME ASSUMPTION: turnaround time on Earth, simulated seconds.</summary>
        public double EarthProcessingSeconds { get; }

        /// <summary>DERIVED: 2 x one-way light time + Earth processing, simulated seconds.</summary>
        public double EarliestReplySeconds { get; }

        public double TransmitEnergyKWh { get; }

        public double? TransmittedAtSimulationSeconds { get; }

        public DateTime? TransmittedAtEpochUtc { get; }

        /// <summary>1-based mission day of the transmission, or null.</summary>
        public int? TransmittedOnMissionDay { get; }

        public double? ReplyDueAtSimulationSeconds { get; }

        public double? ReplyReceivedAtSimulationSeconds { get; }

        /// <summary>Simulated seconds the reply still needed when the report was built (0 once received).</summary>
        public double ReplyRemainingSimulationSeconds { get; }

        /// <summary>GAMEPLAY TIME COMPRESSION: simulated seconds per real second while waiting for the reply.</summary>
        public double TransmissionTimeScale { get; }

        /// <summary>Mission date when the report was built, UTC.</summary>
        public DateTime MissionEpochUtc { get; }

        /// <summary>Simulated seconds between transmission and the received reply, or null.</summary>
        public double? ReplyWaitSimulationSeconds =>
            TransmittedAtSimulationSeconds.HasValue && ReplyReceivedAtSimulationSeconds.HasValue
                ? ReplyReceivedAtSimulationSeconds.Value - TransmittedAtSimulationSeconds.Value
                : (double?)null;
    }

    /// <summary>
    /// The mission debrief (implements <see cref="IMissionReport"/>): an immutable snapshot Core builds from
    /// authoritative state. Presentation only displays it. <see cref="Sections"/> is display-ready text grouped
    /// under the stable IDs of <see cref="MissionReportSections"/>; the typed members carry the same facts as
    /// numbers for views that reference Core.
    /// </summary>
    public sealed class MissionReport : IMissionReport
    {
        internal MissionReport()
        {
        }

        public MissionOutcome Outcome { get; internal set; }

        /// <summary>False for a preview built while the mission still runs.</summary>
        public bool IsFinal { get; internal set; }

        /// <summary>"RECOVERY POSSIBLE", "MISSION LOST" or "MISSION IN PROGRESS".</summary>
        public string Headline { get; internal set; }

        /// <summary>Core's causal explanation of the outcome (empty while the mission runs).</summary>
        public string OutcomeReason { get; internal set; }

        public double ElapsedSimulationSeconds { get; internal set; }

        public IReadOnlyList<MissionReportSection> Sections { get; internal set; }

        public string MissionId { get; internal set; }

        /// <summary>Why the mission was lost, or None.</summary>
        public SurvivalFailure LossCause { get; internal set; }

        /// <summary>Mission clock T0, UTC.</summary>
        public DateTime MissionEpochAtStartUtc { get; internal set; }

        /// <summary>Mission date at the end (or now, for a preview), UTC.</summary>
        public DateTime MissionEpochUtc { get; internal set; }

        /// <summary>1-based mission day at the end.</summary>
        public int MissionDayNumber { get; internal set; }

        /// <summary>A preset ID when the launched loadout equals one, otherwise "custom".</summary>
        public string LoadoutName { get; internal set; }

        /// <summary>True when the timed planning deadline launched the last valid loadout instead of the draft.</summary>
        public bool UsedLastValidLoadoutAtDeadline { get; internal set; }

        public IReadOnlyList<EquipmentSelection> Equipment { get; internal set; }

        public double TotalMassKg { get; internal set; }

        public double MassLimitKg { get; internal set; }

        public double TotalBudgetCredits { get; internal set; }

        public double BudgetLimitCredits { get; internal set; }

        public double RequiredPowerWatts { get; internal set; }

        public double AvailablePowerWatts { get; internal set; }

        public ResourceSnapshot ResourcesAtLaunch { get; internal set; }

        public ResourceSnapshot ResourcesAtEnd { get; internal set; }

        public double PeakCarbonDioxidePpm { get; internal set; }

        /// <summary>Repairs, EVA, transmission and use over time. Their sums equal launch minus end.</summary>
        public IReadOnlyList<ReportExpenditure> Expenditures { get; internal set; }

        public IReadOnlyList<ReportSystemStatus> Systems { get; internal set; }

        public int CompletedEvaCount { get; internal set; }

        public int SuitPreparationCount { get; internal set; }

        public int AirlockCycleCount { get; internal set; }

        public IReadOnlyList<string> ScienceObjectives { get; internal set; }

        /// <summary>True when a science payload was carried and contact with Earth was restored.</summary>
        public bool ScienceDataReturned { get; internal set; }

        public ReportCommunication Communication { get; internal set; }

        /// <summary>True when the science instrument was dismantled for spare parts: its objective is abandoned.</summary>
        public bool SciencePayloadSalvaged { get; internal set; }

        public int AbsorberCartridgesBuilt { get; internal set; }

        public int AbsorberCartridgesInstalled { get; internal set; }

        /// <summary>CO2 taken up by absorber cartridges, kg.</summary>
        public double AbsorbedCarbonDioxideKg { get; internal set; }

        /// <summary>True when the link diagnostic ran: the false signal was an old local recording, not a reply.</summary>
        public bool FalseSignalReplayed { get; internal set; }

        /// <summary>ESTIMATE of how long the remaining consumables last at the final rates.</summary>
        public HoldingEstimate Holding { get; internal set; }

        public IReadOnlyList<MissionLogEntry> Timeline { get; internal set; }

        /// <summary>Unpaused real seconds spent in flight phases (0 when the session was not driven by frames).</summary>
        public double PlayedRealSeconds { get; internal set; }

        /// <summary>Simulated seconds that passed in chapter jumps rather than frame by frame.</summary>
        public double JumpedSimulationSeconds { get; internal set; }

        public bool TryGetSection(string id, out MissionReportSection section)
        {
            if (Sections != null)
            {
                foreach (MissionReportSection candidate in Sections)
                {
                    if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                    {
                        section = candidate;
                        return true;
                    }
                }
            }

            section = default(MissionReportSection);
            return false;
        }

        /// <summary>The whole report as plain text, one "Label: value" per line under upper-case section titles.</summary>
        public string ToPlainText()
        {
            var builder = new StringBuilder();
            builder.Append(Headline).Append('\n');
            if (Sections == null)
            {
                return builder.ToString();
            }

            foreach (MissionReportSection section in Sections)
            {
                builder.Append('\n').Append(section.Title.ToUpperInvariant()).Append('\n');
                foreach (MissionReportLine line in section.Lines)
                {
                    builder.Append(line.Label).Append(": ").Append(line.Value).Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>Authored rules for one ship system: what the accident does to it and what its repair costs.</summary>
    public sealed class ShipSystemSpec
    {
        public ShipSystemSpec(
            ShipSystemId systemId,
            string displayName,
            double conditionAfterAccident01,
            bool repairRequiresEva,
            params ResourceAmount[] repairCosts)
        {
            if (!Enum.IsDefined(typeof(ShipSystemId), systemId))
            {
                throw new ArgumentOutOfRangeException(nameof(systemId), systemId, "Unknown ship system.");
            }

            if (double.IsNaN(conditionAfterAccident01) || conditionAfterAccident01 < 0d || conditionAfterAccident01 > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(conditionAfterAccident01), conditionAfterAccident01, "Condition must be between 0 and 1.");
            }

            SystemId = systemId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? systemId.ToString() : displayName;
            ConditionAfterAccident01 = conditionAfterAccident01;
            RepairRequiresEva = repairRequiresEva;
            RepairCosts = Array.AsReadOnly((ResourceAmount[])(repairCosts ?? new ResourceAmount[0]).Clone());
        }

        public ShipSystemId SystemId { get; }

        public string DisplayName { get; }

        /// <summary>Condition the scripted impact leaves behind (1 = untouched).</summary>
        public double ConditionAfterAccident01 { get; }

        /// <summary>True for exterior hardware: repairable only in the Eva phase. Interior systems: Emergency or Repair.</summary>
        public bool RepairRequiresEva { get; }

        public IReadOnlyList<ResourceAmount> RepairCosts { get; }
    }

    /// <summary>
    /// GAMEPLAY TUNING: simulated seconds per real second in each phase. Planning, Outcome and Report are frozen.
    /// Any value other than 1 must be shown on the HUD (for example "x300, 1 s = 5 min").
    /// </summary>
    public sealed class MissionPacing
    {
        public MissionPacing(double introTimeScale = 1d, double interiorTimeScale = 300d, double evaTimeScale = 60d, double transmissionTimeScale = 60d)
        {
            IntroTimeScale = Require(introTimeScale, nameof(introTimeScale));
            InteriorTimeScale = Require(interiorTimeScale, nameof(interiorTimeScale));
            EvaTimeScale = Require(evaTimeScale, nameof(evaTimeScale));
            TransmissionTimeScale = Require(transmissionTimeScale, nameof(transmissionTimeScale));
        }

        /// <summary>Cockpit check before the impact: real time.</summary>
        public double IntroTimeScale { get; }

        /// <summary>Emergency and Repair: default x300, so one real second is five simulated minutes.</summary>
        public double InteriorTimeScale { get; }

        public double EvaTimeScale { get; }

        public double TransmissionTimeScale { get; }

        public double GetTimeScale(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Intro:
                    return IntroTimeScale;
                case GamePhase.Emergency:
                case GamePhase.Repair:
                    return InteriorTimeScale;
                case GamePhase.Eva:
                    return EvaTimeScale;
                case GamePhase.Transmission:
                    return TransmissionTimeScale;
                default:
                    return 1d;
            }
        }

        private static double Require(double scale, string name)
        {
            if (double.IsNaN(scale) || scale <= 0d || scale > SimulationClock.MaximumTimeScale)
            {
                throw new ArgumentOutOfRangeException(name, scale, "Time scale must be greater than zero and at most " + SimulationClock.MaximumTimeScale + ".");
            }

            return scale;
        }
    }

    /// <summary>The scripted accident, repair requirements and pacing of one mission.</summary>
    public sealed class MissionScenario
    {
        private readonly Dictionary<ShipSystemId, ShipSystemSpec> byId = new Dictionary<ShipSystemId, ShipSystemSpec>();

        public MissionScenario(
            IEnumerable<ShipSystemSpec> systems,
            MissionPacing pacing,
            double impactCinematicTimeoutRealSeconds = CinematicCoordinator.DefaultTimeoutRealSeconds,
            EvaSpec eva = null,
            CommunicationSpec communication = null,
            DateTime? missionEpochAtStartUtc = null,
            double endingCinematicTimeoutRealSeconds = DefaultEndingCinematicTimeoutRealSeconds)
        {
            if (systems == null)
            {
                throw new ArgumentNullException(nameof(systems));
            }

            var ordered = new List<ShipSystemSpec>();
            foreach (ShipSystemSpec spec in systems)
            {
                if (spec == null || byId.ContainsKey(spec.SystemId))
                {
                    throw new ArgumentException("Each ship system must be specified exactly once.", nameof(systems));
                }

                byId.Add(spec.SystemId, spec);
                ordered.Add(spec);
            }

            foreach (ShipSystemId id in Enum.GetValues(typeof(ShipSystemId)))
            {
                if (!byId.ContainsKey(id))
                {
                    throw new ArgumentException($"Ship system {id} has no specification.", nameof(systems));
                }
            }

            if (double.IsNaN(impactCinematicTimeoutRealSeconds) || double.IsInfinity(impactCinematicTimeoutRealSeconds) || impactCinematicTimeoutRealSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(impactCinematicTimeoutRealSeconds), impactCinematicTimeoutRealSeconds, "Timeout must be finite and non-negative.");
            }

            if (double.IsNaN(endingCinematicTimeoutRealSeconds) || double.IsInfinity(endingCinematicTimeoutRealSeconds) || endingCinematicTimeoutRealSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(endingCinematicTimeoutRealSeconds), endingCinematicTimeoutRealSeconds, "Timeout must be finite and non-negative.");
            }

            Systems = ordered.AsReadOnly();
            Pacing = pacing ?? throw new ArgumentNullException(nameof(pacing));
            ImpactCinematicTimeoutRealSeconds = impactCinematicTimeoutRealSeconds;
            Eva = eva ?? new EvaSpec();
            Communication = communication ?? new CommunicationSpec();
            MissionEpochAtStartUtc = missionEpochAtStartUtc.HasValue
                ? DateTime.SpecifyKind(missionEpochAtStartUtc.Value, DateTimeKind.Utc)
                : (DateTime?)null;
            EndingCinematicTimeoutRealSeconds = endingCinematicTimeoutRealSeconds;
        }

        /// <summary>Fallback for each ending cinematic: long enough for a reply voice-over or a final camera move.</summary>
        public const double DefaultEndingCinematicTimeoutRealSeconds = 90d;

        public IReadOnlyList<ShipSystemSpec> Systems { get; }

        public MissionPacing Pacing { get; }

        /// <summary>Unpaused real seconds Core waits for the Impact cinematic before continuing without it.</summary>
        public double ImpactCinematicTimeoutRealSeconds { get; }

        /// <summary>Suit and airlock assumptions for the EVA.</summary>
        public EvaSpec Eva { get; }

        /// <summary>Earth processing time and transmit energy for the distress call.</summary>
        public CommunicationSpec Communication { get; }

        /// <summary>
        /// Mission clock T0, UTC: the calendar date at zero elapsed simulated seconds. Null means the first epoch
        /// of the packaged Earth-Mars reference, so the mission date always starts inside the reviewed data.
        /// </summary>
        public DateTime? MissionEpochAtStartUtc { get; }

        /// <summary>Unpaused real seconds Core waits for each ending cinematic before continuing without it.</summary>
        public double EndingCinematicTimeoutRealSeconds { get; }

        public ShipSystemSpec GetSystem(ShipSystemId systemId)
        {
            return byId[systemId];
        }
    }
}

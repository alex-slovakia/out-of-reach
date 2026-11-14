using System;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// GAME ASSUMPTIONS for AURORA-7's distress call. Neither value comes from NASA data: the light time does (see
    /// <see cref="EarthMarsReference"/> and <see cref="SignalDelay"/>), these two are fictional operations figures.
    /// </summary>
    public sealed class CommunicationSpec
    {
        public CommunicationSpec(double earthProcessingSeconds = 600d, double transmitEnergyKWh = 0.5d)
        {
            if (double.IsNaN(earthProcessingSeconds) || double.IsInfinity(earthProcessingSeconds) || earthProcessingSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(earthProcessingSeconds), earthProcessingSeconds, "Processing time must be finite and non-negative.");
            }

            if (double.IsNaN(transmitEnergyKWh) || double.IsInfinity(transmitEnergyKWh) || transmitEnergyKWh < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(transmitEnergyKWh), transmitEnergyKWh, "Transmit energy must be finite and non-negative.");
            }

            EarthProcessingSeconds = earthProcessingSeconds;
            TransmitEnergyKWh = transmitEnergyKWh;
        }

        /// <summary>
        /// Simulated seconds mission control needs between receiving the call and sending its answer: 10 minutes.
        /// Fictional turnaround; real scheduling, acquisition and decision time are not modelled.
        /// </summary>
        public double EarthProcessingSeconds { get; }

        /// <summary>
        /// Battery energy drawn once when the call is sent, kWh: the high-gain transmitter burst and keeping the
        /// receiver powered for the reply window.
        /// </summary>
        public double TransmitEnergyKWh { get; }
    }

    /// <summary>
    /// The link to Earth (implements <see cref="ICommunicationSystem"/>).
    /// <list type="bullet">
    /// <item>The distance is the NASA/JPL reference sample of the current mission date: the last sample at or
    /// before it. A date outside the snapshot has no reviewed distance, so transmitting is refused.</item>
    /// <item>One validated transmission. It needs the link prerequisites Core supplies (antenna, power, phase) and
    /// the transmit energy; a rejected send spends nothing and a repeated send cannot restart the timer.</item>
    /// <item>At transmission the sample is frozen and the reply becomes due at an absolute time on the mission
    /// clock: transmission time + 2 x one-way light time + Earth processing. Because the due time is absolute,
    /// pause, time acceleration and chapter jumps all act on it through the one clock, and calling
    /// <see cref="AdvanceTime"/> twice cannot bring the reply forward.</item>
    /// <item>A reply is received only while the link is powered. If it is not, the state stays AwaitingReply and
    /// the reply is received as soon as power returns (ASSUMPTION: the ground keeps repeating its answer).</item>
    /// </list>
    /// CommunicationChanged is published when the state changes, not every frame; a countdown view polls
    /// <see cref="ReplyRemainingSimulationSeconds"/>.
    /// </summary>
    public sealed class CommunicationSystem : ICommunicationSystem
    {
        /// <summary>Slack in simulated seconds for a step computed to land exactly on the reply time.</summary>
        public const double TimeToleranceSeconds = 1e-9;

        private readonly IMissionEventBus events;
        private readonly ResourceLedger resources;
        private readonly EarthMarsReference reference;
        private readonly CommunicationSpec spec;
        private readonly Func<DateTime> missionEpochUtc;
        private readonly Func<double> missionClockSeconds;
        private readonly Func<CommandResult> checkLink;
        private readonly Func<bool> isLinkPowered;
        private readonly Func<IDisposable> beginTransaction;

        private CommunicationState publishedState;
        private EarthMarsSample transmittedSample;
        private double replyDueAtSeconds;

        /// <param name="events">Bus that receives CommunicationChanged.</param>
        /// <param name="resources">Ledger the transmit energy is drawn from.</param>
        /// <param name="reference">Validated Earth-Mars reference table.</param>
        /// <param name="spec">Processing time and transmit energy.</param>
        /// <param name="missionEpochUtc">Current mission date on the simulation clock.</param>
        /// <param name="missionClockSeconds">Elapsed simulated seconds of the one mission clock.</param>
        /// <param name="checkLink">Core's prerequisites for sending (phase, antenna, power); null = always allowed.</param>
        /// <param name="isLinkPowered">True while antenna and power can carry a signal; null = always.</param>
        /// <param name="beginTransaction">Optional event deferral so one command's events arrive together.</param>
        public CommunicationSystem(
            IMissionEventBus events,
            ResourceLedger resources,
            EarthMarsReference reference,
            CommunicationSpec spec,
            Func<DateTime> missionEpochUtc,
            Func<double> missionClockSeconds,
            Func<CommandResult> checkLink = null,
            Func<bool> isLinkPowered = null,
            Func<IDisposable> beginTransaction = null)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.reference = reference ?? throw new ArgumentNullException(nameof(reference));
            this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
            this.missionEpochUtc = missionEpochUtc ?? throw new ArgumentNullException(nameof(missionEpochUtc));
            this.missionClockSeconds = missionClockSeconds ?? throw new ArgumentNullException(nameof(missionClockSeconds));
            this.checkLink = checkLink;
            this.isLinkPowered = isLinkPowered;
            this.beginTransaction = beginTransaction;
            publishedState = State;
        }

        public CommunicationSpec Spec => spec;

        public EarthMarsReference Reference => reference;

        public CommunicationState State
        {
            get
            {
                if (ReplyReceivedAtSimulationSeconds.HasValue)
                {
                    return CommunicationState.ReplyReceived;
                }

                if (HasTransmitted)
                {
                    return CommunicationState.AwaitingReply;
                }

                return IsLinkPowered() ? CommunicationState.Ready : CommunicationState.Offline;
            }
        }

        /// <summary>True once the distress call was sent. It can be sent only once.</summary>
        public bool HasTransmitted { get; private set; }

        /// <summary>
        /// True while the current mission date has a reviewed reference sample. Before the transmission the
        /// distance and light time follow the mission date; afterwards they stay frozen at the transmitted sample.
        /// </summary>
        public bool IsReferenceAvailable
        {
            get { return HasTransmitted || TrySelectCurrent(out _, out _); }
        }

        /// <summary>
        /// The sample the distance and light time are read from: the transmitted one once the call was sent,
        /// otherwise the mission date's. If the mission date is outside the snapshot, the nearest edge sample is
        /// shown and <see cref="IsReferenceAvailable"/> is false.
        /// </summary>
        public EarthMarsSample ReferenceSample
        {
            get
            {
                if (HasTransmitted)
                {
                    return transmittedSample;
                }

                DateTime now = missionEpochUtc();
                if (reference.TrySelect(now, out EarthMarsSample current, out _))
                {
                    return current;
                }

                return now > reference.LastEpochUtc ? reference.Samples[reference.Count - 1] : reference.Samples[0];
            }
        }

        /// <summary>Epoch of <see cref="ReferenceSample"/>, UTC.</summary>
        public DateTime ReferenceEpochUtc => ReferenceSample.EpochUtc;

        public double ReferenceDistanceKm => ReferenceSample.DistanceKm;

        public double OneWayLightSeconds => ReferenceSample.OneWayLightSeconds;

        public double EarthProcessingSeconds => spec.EarthProcessingSeconds;

        /// <summary>Earliest reply after a transmission: 2 x one-way light time + Earth processing, simulated seconds.</summary>
        public double ReplyDelaySimulationSeconds => SignalDelay.EarliestReplySeconds(OneWayLightSeconds, spec.EarthProcessingSeconds);

        public double ReplyRemainingSimulationSeconds
        {
            get
            {
                if (!HasTransmitted || ReplyReceivedAtSimulationSeconds.HasValue)
                {
                    return 0d;
                }

                return Math.Max(0d, replyDueAtSeconds - missionClockSeconds());
            }
        }

        public string AstronomicalReferenceId => reference.DatasetId;

        public double TransmitEnergyKWh => spec.TransmitEnergyKWh;

        /// <summary>Mission clock time of the transmission, or null.</summary>
        public double? TransmittedAtSimulationSeconds { get; private set; }

        /// <summary>Mission date of the transmission, UTC, or null.</summary>
        public DateTime? TransmittedAtEpochUtc { get; private set; }

        /// <summary>Mission clock time before which no reply can be received, or null before the transmission.</summary>
        public double? ReplyDueAtSimulationSeconds => HasTransmitted ? replyDueAtSeconds : (double?)null;

        /// <summary>Mission clock time at which the reply was received, or null.</summary>
        public double? ReplyReceivedAtSimulationSeconds { get; private set; }

        /// <summary>True while a reply has arrived at the ship but the unpowered link cannot receive it.</summary>
        public bool IsReplyHeldByUnpoweredLink =>
            HasTransmitted && !ReplyReceivedAtSimulationSeconds.HasValue && IsReplyDue() && !IsLinkPowered();

        /// <summary>Explains whether the call can be sent now, without changing anything. Its text is the console prompt.</summary>
        public CommandResult CheckTransmit()
        {
            if (ReplyReceivedAtSimulationSeconds.HasValue)
            {
                return Fail(CommandFailure.AlreadyComplete, "Earth's reply has been received.");
            }

            if (HasTransmitted)
            {
                return Fail(CommandFailure.AlreadyComplete, "The distress call is on its way. Earliest reply in "
                    + FormatDuration(ReplyRemainingSimulationSeconds, true) + ".");
            }

            if (checkLink != null)
            {
                CommandResult link = checkLink();
                if (!link.Succeeded)
                {
                    return link;
                }
            }

            if (!TrySelectCurrent(out EarthMarsSample sample, out string problem))
            {
                return Fail(CommandFailure.PreconditionsNotMet, problem);
            }

            if (spec.TransmitEnergyKWh > 0d)
            {
                CommandResult affordable = resources.CheckAffordable(TransmitCost());
                if (!affordable.Succeeded)
                {
                    return affordable;
                }
            }

            double delay = SignalDelay.EarliestReplySeconds(sample.OneWayLightSeconds, spec.EarthProcessingSeconds);
            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Transmit distress call: {0:0.##} kWh. Earth is {1} away; earliest reply in {2}.",
                spec.TransmitEnergyKWh, FormatDistance(sample.DistanceKm), FormatDuration(delay)));
        }

        public CommandResult TryTransmit()
        {
            CommandResult check = CheckTransmit();
            if (!check.Succeeded)
            {
                return check;
            }

            if (!TrySelectCurrent(out EarthMarsSample sample, out string problem))
            {
                return Fail(CommandFailure.PreconditionsNotMet, problem);
            }

            double delay = SignalDelay.EarliestReplySeconds(sample.OneWayLightSeconds, spec.EarthProcessingSeconds);
            using (beginTransaction != null ? beginTransaction() : null)
            {
                if (spec.TransmitEnergyKWh > 0d)
                {
                    CommandResult spent = resources.TryConsume(TransmitCost());
                    if (!spent.Succeeded)
                    {
                        return spent;
                    }
                }

                double now = missionClockSeconds();
                transmittedSample = sample;
                HasTransmitted = true;
                TransmittedAtSimulationSeconds = now;
                TransmittedAtEpochUtc = missionEpochUtc();
                replyDueAtSeconds = now + delay;
                Publish(CommunicationState.AwaitingReply);
            }

            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Distress call transmitted. Earliest reply in {0}: 2 x {1} light time + {2} on Earth.",
                FormatDuration(delay), FormatDuration(sample.OneWayLightSeconds), FormatDuration(spec.EarthProcessingSeconds)));
        }

        /// <summary>
        /// Core calls this once per simulation step, after the mission clock advanced and after the survival rules
        /// were applied. A due reply is received here, provided the link is powered. The argument is validated;
        /// the reply itself is decided against the mission clock, so a stray second call changes nothing.
        /// </summary>
        public void AdvanceTime(double simulationSeconds)
        {
            if (double.IsNaN(simulationSeconds) || double.IsInfinity(simulationSeconds) || simulationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds), simulationSeconds, "Simulation time must be finite and non-negative.");
            }

            if (HasTransmitted && !ReplyReceivedAtSimulationSeconds.HasValue && IsReplyDue() && IsLinkPowered())
            {
                ReplyReceivedAtSimulationSeconds = Math.Max(missionClockSeconds(), replyDueAtSeconds);
            }

            RefreshState();
        }

        /// <summary>
        /// Publishes CommunicationChanged if the state changed (Offline at the impact, Ready after the antenna
        /// repair). Core calls it after damage and repairs. It never receives a reply: that only happens in
        /// <see cref="AdvanceTime"/>, after the survival rules.
        /// </summary>
        public void RefreshState()
        {
            CommunicationState current = State;
            if (current != publishedState)
            {
                Publish(current);
            }
        }

        /// <summary>Simulated seconds until the reply becomes due; +Infinity when no reply is pending.</summary>
        public double TimeToNextBoundary()
        {
            if (!HasTransmitted || ReplyReceivedAtSimulationSeconds.HasValue)
            {
                return double.PositiveInfinity;
            }

            double remaining = replyDueAtSeconds - missionClockSeconds();
            return remaining > TimeToleranceSeconds ? remaining : double.PositiveInfinity;
        }

        /// <summary>"31 min 59 s", "2 h 05 min 10 s". Rounds to the nearest second, or up for a countdown.</summary>
        public static string FormatDuration(double seconds, bool roundUp = false)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                return "unknown";
            }

            double rounded = roundUp ? Math.Ceiling(seconds - 1e-6) : Math.Round(seconds, MidpointRounding.AwayFromZero);
            long total = (long)Math.Max(0d, Math.Min(rounded, long.MaxValue / 2d));
            long hours = total / 3600;
            long minutes = (total % 3600) / 60;
            long secs = total % 60;
            if (hours > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min {2:00} s", hours, minutes, secs);
            }

            return minutes > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} min {1:00} s", minutes, secs)
                : string.Format(CultureInfo.InvariantCulture, "{0} s", secs);
        }

        /// <summary>"197,708,000 km": rounded to the nearest thousand kilometres for display.</summary>
        public static string FormatDistance(double distanceKm)
        {
            double rounded = Math.Round(distanceKm / 1000d, MidpointRounding.AwayFromZero) * 1000d;
            return rounded.ToString("#,0", CultureInfo.InvariantCulture) + " km";
        }

        private bool TrySelectCurrent(out EarthMarsSample sample, out string problem)
        {
            return reference.TrySelect(missionEpochUtc(), out sample, out problem);
        }

        private bool IsReplyDue()
        {
            return replyDueAtSeconds - missionClockSeconds() <= TimeToleranceSeconds;
        }

        private bool IsLinkPowered()
        {
            return isLinkPowered == null || isLinkPowered();
        }

        private ResourceAmount[] TransmitCost()
        {
            return new[] { new ResourceAmount(ResourceId.BatteryKWh, spec.TransmitEnergyKWh) };
        }

        private void Publish(CommunicationState state)
        {
            publishedState = state;
            events.Publish(new CommunicationChanged(state, ReplyRemainingSimulationSeconds));
        }

        private static CommandResult Ok(string explanation)
        {
            return new CommandResult(true, CommandFailure.None, explanation);
        }

        private static CommandResult Fail(CommandFailure failure, string explanation)
        {
            return new CommandResult(false, failure, explanation);
        }
    }
}

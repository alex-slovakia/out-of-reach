using System;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.UI.Chapter
{
    /// <summary>Presentation weight of a line or card. Colours only; it never defines a gameplay threshold.</summary>
    public enum ChapterTone { Neutral, Info, Attention, Danger }

    /// <summary>What an option button of a chapter card does.</summary>
    public enum ChapterOptionKind
    {
        /// <summary>Issues <see cref="IMissionChapterFlow.TryChoose"/> with the option's choice.</summary>
        Choose,
        /// <summary>Closes the card: the option is the existing physical interaction in the ship (E at its station).</summary>
        Physical,
    }

    /// <summary>One option of a chapter decision card.</summary>
    public readonly struct ChapterOptionCopy
    {
        public ChapterOptionCopy(ChapterOptionKind kind, ChapterChoice choice, string title, string station)
        {
            Kind = kind; Choice = choice; Title = title; Station = station;
        }

        public ChapterOptionKind Kind { get; }
        public ChapterChoice Choice { get; }
        public string Title { get; }
        /// <summary>Physical options only: where the existing interaction is.</summary>
        public string Station { get; }
    }

    /// <summary>
    /// Pure copy and formatting rules of the chapter UI. Every number is supplied by the caller from Core (the one mission
    /// clock, the ledger, the chapter flow); nothing here keeps time, charges resources or decides an outcome.
    /// The day rule is Core's MissionCalendar.DayNumberAt: day 1 begins at mission time 0.
    /// </summary>
    public static class ChapterCopy
    {
        public const double SecondsPerDay = 86400d;

        /// <summary>Core's stable id of the day-14 dispatch (MissionChapterFlow.RescueFailedDispatchId).</summary>
        public const string RescueFailedDispatchId = "rescue-1-failed";

        /// <summary>Core's content id of the day-14 dispatch (MissionChapterFlow.RescueFailedContentId).</summary>
        public const string RescueFailedContentId = "dispatch.rescue-1-failed";

        /// <summary>The routine's ordinary rest limit stated by the approved spec (SurvivalRecovery.MaximumRestHours).</summary>
        public const string NightLimit = "8.5 h";

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>Mission day of a simulation time (Core's rule: floor(t / 86,400 s) + 1, day 1 for t ≤ 0).</summary>
        public static int DayAt(double simulationSeconds)
        {
            if (!Finite(simulationSeconds) || simulationSeconds <= 0d) return 1;
            double day = Math.Floor(simulationSeconds / SecondsPerDay);
            return day >= int.MaxValue - 1 ? int.MaxValue : 1 + (int)day;
        }

        /// <summary>Time of the mission day, "08:00" (truncated to the minute, never rounded up).</summary>
        public static string ClockAt(double simulationSeconds)
        {
            if (!Finite(simulationSeconds) || simulationSeconds < 0d) return "--:--";
            double inDay = simulationSeconds - (Math.Floor(simulationSeconds / SecondsPerDay) * SecondsPerDay);
            long minutes = Math.Max(0L, Math.Min(1439L, (long)Math.Floor(inDay / 60d)));
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes / 60, minutes % 60);
        }

        /// <summary>"Day 4, 13:42".</summary>
        public static string DayAndClock(double simulationSeconds) =>
            "Day " + DayAt(simulationSeconds).ToString(CultureInfo.InvariantCulture) + ", " + ClockAt(simulationSeconds);

        /// <summary>Simulated duration: "2 d 23 h", "5 h 12 min", "42 min", "18 s". Never negative.</summary>
        public static string Span(double seconds)
        {
            if (!Finite(seconds)) return "unknown";
            seconds = Math.Max(0d, seconds);
            if (seconds < 60d) return ((long)Math.Floor(seconds)).ToString(CultureInfo.InvariantCulture) + " s";
            long minutes = (long)Math.Floor(seconds / 60d);
            long days = minutes / 1440, hours = (minutes % 1440) / 60, rest = minutes % 60;
            if (days > 0)
                return hours > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} d {1} h", days, hours)
                    : string.Format(CultureInfo.InvariantCulture, "{0} d", days);
            if (hours > 0)
                return rest > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} h {1} min", hours, rest)
                    : string.Format(CultureInfo.InvariantCulture, "{0} h", hours);
            return string.Format(CultureInfo.InvariantCulture, "{0} min", rest);
        }

        /// <summary>Light-travel duration with seconds: "13 min 52 s", "1 h 2 min 5 s".</summary>
        public static string Transit(double seconds)
        {
            if (!Finite(seconds) || seconds < 0d) return "unknown";
            long whole = (long)Math.Floor(seconds);
            long hours = whole / 3600, minutes = (whole % 3600) / 60, rest = whole % 60;
            if (hours > 0) return string.Format(CultureInfo.InvariantCulture, "{0} h {1} min {2} s", hours, minutes, rest);
            return minutes > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} min {1} s", minutes, rest)
                : string.Format(CultureInfo.InvariantCulture, "{0} s", rest);
        }

        /// <summary>Whole calendar days a routine segment crosses: day 2 08:30 to day 5 08:00 = 3.</summary>
        public static int CalendarDaysCrossed(double from, double to) => Math.Max(0, DayAt(to) - DayAt(from));

        public static string Plural(int count, string one, string many) =>
            count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);

        /// <summary>The routine's real command tally, as Core counted it.</summary>
        public static string Tally(int rations, int drinks, int rests) =>
            Plural(Math.Max(0, rations), "meal", "meals") + " · " + Plural(Math.Max(0, drinks), "water portion", "water portions")
            + " · " + Plural(Math.Max(0, rests), "night of ordinary sleep", "nights of ordinary sleep");

        /// <summary>Upper card label of a beat: the authored mission days of the approved loop.</summary>
        public static string BeatDays(MissionChapter chapter, int actualDay)
        {
            string day = Math.Max(1, actualDay).ToString(CultureInfo.InvariantCulture);
            return chapter == MissionChapter.Accident && actualDay <= 2 ? "MISSION DAYS 1–2" : "MISSION DAY " + day;
        }

        public static string BeatTitle(MissionChapter chapter)
        {
            switch (chapter)
            {
                case MissionChapter.Accident: return "The accident";
                case MissionChapter.Consequences: return "Consequences";
                case MissionChapter.Contact: return "Contact";
                case MissionChapter.RescueFailed: return "The message";
                case MissionChapter.FinalDecision: return "Last reserves";
                case MissionChapter.Rendezvous: return "Rendezvous";
                default: return string.Empty;
            }
        }

        /// <param name="contactHolds">True once Earth's reply has arrived (the mission is in the Survival phase).</param>
        public static string BeatLine(MissionChapter chapter, bool contactHolds)
        {
            switch (chapter)
            {
                case MissionChapter.Accident:
                    return "Survive the impact. Restore power and life support, eat the first stored meal and secure a place to sleep.";
                case MissionChapter.Consequences:
                    return "Inspect the repairs and the real reserves, then decide what the ship can afford to keep running.";
                case MissionChapter.Contact:
                    return contactHolds
                        ? "Contact with Earth holds. Keep to the routine and wait for news of the rescue."
                        : "Finish the exterior repair and call Earth.";
                case MissionChapter.RescueFailed:
                    return "Mission control reports on the first rescue attempt.";
                case MissionChapter.FinalDecision:
                    return "The final power and supply decision before the rendezvous.";
                case MissionChapter.Rendezvous:
                    return "The recovery craft is close. Power the beacon when it asks.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Short name of the beat the next routine would reach, for the request strip.</summary>
        public static string NextName(MissionChapter chapter)
        {
            switch (chapter)
            {
                case MissionChapter.Consequences: return "Inspection of the repairs and reserves";
                case MissionChapter.Contact: return "Exterior repair and the call to Earth";
                case MissionChapter.RescueFailed: return "News of the rescue attempt";
                case MissionChapter.FinalDecision: return "Final power and supply decision";
                case MissionChapter.Rendezvous: return "Rendezvous with the recovery craft";
                default: return "Next chapter";
            }
        }

        public static string StopLabel(RoutineStopReason reason)
        {
            switch (reason)
            {
                case RoutineStopReason.ReachedChapter: return "CHAPTER REACHED";
                case RoutineStopReason.PlayerCancelled: return "ROUTINE STOPPED BY THE CREW";
                case RoutineStopReason.Danger: return "ROUTINE STOPPED · DANGER";
                case RoutineStopReason.ResourceShortage: return "ROUTINE STOPPED · SUPPLIES";
                case RoutineStopReason.CropReady: return "ROUTINE STOPPED · CROP READY";
                case RoutineStopReason.CropFailed: return "ROUTINE STOPPED · CROP FAILED";
                case RoutineStopReason.DispatchReceived: return "ROUTINE STOPPED · MESSAGE FROM EARTH";
                case RoutineStopReason.DecisionRequired: return "ROUTINE STOPPED · DECISION NEEDED";
                case RoutineStopReason.PrerequisiteMissing: return "ROUTINE STOPPED · CANNOT CONTINUE";
                case RoutineStopReason.MissionEnded: return "MISSION ENDED";
                default: return "ROUTINE STOPPED";
            }
        }

        public static ChapterTone StopTone(RoutineStopReason reason)
        {
            switch (reason)
            {
                case RoutineStopReason.Danger:
                case RoutineStopReason.ResourceShortage:
                case RoutineStopReason.MissionEnded:
                    return ChapterTone.Danger;
                case RoutineStopReason.DecisionRequired:
                case RoutineStopReason.PrerequisiteMissing:
                case RoutineStopReason.CropFailed:
                    return ChapterTone.Attention;
                case RoutineStopReason.DispatchReceived:
                case RoutineStopReason.CropReady:
                    return ChapterTone.Info;
                default:
                    return ChapterTone.Neutral;
            }
        }

        /// <summary>Core's lower-case stop phrase as a sentence: "the food store is empty" → "The food store is empty.".</summary>
        public static string Sentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string trimmed = text.Trim();
            string first = trimmed.Substring(0, 1).ToUpperInvariant() + trimmed.Substring(1);
            char last = first[first.Length - 1];
            return last == '.' || last == '!' || last == '?' ? first : first + ".";
        }

        public static string DecisionKicker(ChapterDecision decision)
        {
            switch (decision)
            {
                case ChapterDecision.ScienceOrSalvage:
                case ChapterDecision.GrowOrConserve:
                    return "DECISION · RESERVES";
                case ChapterDecision.AcknowledgeRescueFailure: return "MESSAGE FROM EARTH";
                case ChapterDecision.GrowOrBeacon: return "FINAL DECISION · POWER AND SUPPLIES";
                default: return "DECISION";
            }
        }

        public static string DecisionTitle(ChapterDecision decision)
        {
            switch (decision)
            {
                case ChapterDecision.ScienceOrSalvage: return "Keep the science instrument, or salvage it?";
                case ChapterDecision.GrowOrConserve: return "Build the grow module, or conserve the reserves?";
                case ChapterDecision.AcknowledgeRescueFailure: return "The first rescue attempt failed";
                case ChapterDecision.GrowOrBeacon: return "Keep the grow module, or save its power for the beacon?";
                default: return string.Empty;
            }
        }

        public static string DecisionBody(ChapterDecision decision)
        {
            switch (decision)
            {
                case ChapterDecision.ScienceOrSalvage:
                    return "The inspection shows what the repairs cost. The instrument still draws power from the bus; dismantled, it gives spare parts for later repairs. The choice cannot be undone.";
                case ChapterDecision.GrowOrConserve:
                    return "No science instrument is left to salvage. Spare parts and battery energy can go into a small grow module (a food supplement, never a full supply), or stay in reserve for repairs and the beacon.";
                case ChapterDecision.AcknowledgeRescueFailure:
                    return "The support vehicle was lost before it could reach the ship. Acknowledge, then reassess the reserves through day 30.";
                case ChapterDecision.GrowOrBeacon:
                    return "The rendezvous needs a powered beacon. The grow module's 70 W and irrigation compete with it for the battery and the water.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The two options of a decision, primary first. Physical options name the existing interaction.</summary>
        public static ChapterOptionCopy[] Options(ChapterDecision decision)
        {
            switch (decision)
            {
                case ChapterDecision.ScienceOrSalvage:
                    return new[]
                    {
                        new ChapterOptionCopy(ChapterOptionKind.Choose, ChapterChoice.PreserveScience, "Keep the instrument", null),
                        new ChapterOptionCopy(ChapterOptionKind.Physical, ChapterChoice.None, "Salvage it for spare parts",
                            "Dismantle it at the science payload rack (E). The salvage itself settles the decision."),
                    };
                case ChapterDecision.GrowOrConserve:
                    return new[]
                    {
                        new ChapterOptionCopy(ChapterOptionKind.Choose, ChapterChoice.ConserveReserves, "Conserve the reserves", null),
                        new ChapterOptionCopy(ChapterOptionKind.Physical, ChapterChoice.None, "Build the grow module",
                            "Build it at the grow module in the habitat (E). The paid build itself settles the decision."),
                    };
                case ChapterDecision.AcknowledgeRescueFailure:
                    return new[]
                    {
                        new ChapterOptionCopy(ChapterOptionKind.Choose, ChapterChoice.AcknowledgeRescueFailure, "Acknowledge", null),
                    };
                case ChapterDecision.GrowOrBeacon:
                    return new[]
                    {
                        new ChapterOptionCopy(ChapterOptionKind.Choose, ChapterChoice.StopGrowForBeacon, "Shut the grow module down", null),
                        new ChapterOptionCopy(ChapterOptionKind.Choose, ChapterChoice.KeepGrowRunning, "Keep the current power plan", null),
                    };
                default:
                    return Array.Empty<ChapterOptionCopy>();
            }
        }

        /// <summary>Text of a received dispatch by its content id (unknown ids get a neutral line, never invented news).</summary>
        public static string DispatchText(string contentId)
        {
            return contentId == RescueFailedContentId
                ? "The first rescue attempt failed. The support vehicle was lost before it could reach the ship."
                : "A message from Earth was received.";
        }

        /// <summary>The dispatch card's body under its title (the title already states the news).</summary>
        public static string DispatchDetail(string contentId)
        {
            return contentId == RescueFailedContentId
                ? "The support vehicle was lost before it could reach the ship."
                : "A message from Earth was received.";
        }

        /// <summary>Day-9 concrete hope. <paramref name="reportDay"/> = Core's scheduled report day, or null when unknown.</summary>
        public static string HopeText(int? reportDay)
        {
            string when = reportDay.HasValue
                ? "about mission day " + reportDay.Value.ToString(CultureInfo.InvariantCulture)
                : "in the coming days";
            return "A first rescue attempt is under way: an uncrewed support vehicle already near Mars will try to reach the ship. Mission control will report its progress " + when + ".";
        }

        public static string CommittedText(int? dockDay)
        {
            string dock = dockDay.HasValue ? "on mission day " + dockDay.Value.ToString(CultureInfo.InvariantCulture) : "later";
            return "A cargo craft already in transit nearby is diverted to the ship. It will ask for the beacon and dock " + dock
                + " if the crew is still alive. No new launch from Earth could reach the ship in time.";
        }

        public const string FictionNote = "Fictional rescue scenario. Message delays follow NASA/JPL Horizons light time.";

        public static string BeaconWindow(double? endsAt, double now)
        {
            if (!endsAt.HasValue || !Finite(endsAt.Value) || !Finite(now)) return "Beacon window: unknown";
            return endsAt.Value <= now ? "The beacon window has closed." : "Window closes in " + Span(endsAt.Value - now) + " (simulated), " + DayAndClock(endsAt.Value) + ".";
        }

        /// <summary>A meal's name from the actual time Core issued it (presentation only).</summary>
        public static string MealName(double simulationSeconds)
        {
            if (!Finite(simulationSeconds)) return "Meal";
            double hour = (simulationSeconds - (Math.Floor(simulationSeconds / SecondsPerDay) * SecondsPerDay)) / 3600d;
            if (hour >= 4.5d && hour < 10.5d) return "Breakfast";
            if (hour >= 10.5d && hour < 15.5d) return "Lunch";
            if (hour >= 15.5d && hour < 22d) return "Dinner";
            return "Late ration";
        }

        // ---------- MISSION-DATA: the NASA/JPL Horizons line of the next-routine strip ----------

        /// <summary>Source label of the reference range (NASA-DATA.md, NASA-01).</summary>
        public const string HorizonsSource = "NASA/JPL Horizons";

        /// <summary>
        /// Core's display rule (CommunicationSystem.FormatDistance, NASA-DATA.md): nearest thousand kilometres,
        /// "197,708,000 km". Empty for a value Core's reference table cannot hold.
        /// </summary>
        public static string EarthDistance(double distanceKm)
        {
            if (!Finite(distanceKm) || distanceKm <= 0d) return string.Empty;
            double rounded = Math.Round(distanceKm / 1000d, MidpointRounding.AwayFromZero) * 1000d;
            return rounded.ToString("#,0", CultureInfo.InvariantCulture) + " km";
        }

        /// <summary>Core's display rule (CommunicationSystem.FormatDuration): nearest whole second, "10 min 59 s", "9 min 05 s".</summary>
        public static string LightTime(double seconds)
        {
            if (!Finite(seconds) || seconds < 0d) return string.Empty;
            double rounded = Math.Round(seconds, MidpointRounding.AwayFromZero);
            long total = (long)Math.Max(0d, Math.Min(rounded, long.MaxValue / 2d));
            long hours = total / 3600, minutes = (total % 3600) / 60, rest = total % 60;
            if (hours > 0) return string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min {2:00} s", hours, minutes, rest);
            return minutes > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} min {1:00} s", minutes, rest)
                : string.Format(CultureInfo.InvariantCulture, "{0} s", rest);
        }

        /// <summary>"Earth 197,708,000 km · signal 10 min 59 s one way"; empty when Core supplies no usable sample.</summary>
        public static string EarthLine(double referenceDistanceKm, double oneWayLightSeconds)
        {
            string distance = EarthDistance(referenceDistanceKm), light = LightTime(oneWayLightSeconds);
            return distance.Length == 0 || light.Length == 0 ? string.Empty : "Earth " + distance + " · signal " + light + " one way";
        }

        /// <summary>
        /// What the number is: the Earth–Mars range of the reference ephemeris standing in for the ship's own distance (no
        /// trajectory of the ship is computed). Core follows the mission date until the call is sent and keeps the
        /// transmitted sample afterwards, so the label says which of the two is on screen. Nothing is interpolated.
        /// </summary>
        public static string EarthLineSource(bool frozenAtTransmission) => HorizonsSource
            + (frozenAtTransmission ? " · Earth–Mars range when the call was sent" : " · today's Earth–Mars range")
            + ", used as a Mars-vicinity proxy";

        public static string Mass(double kg) => Finite(kg) ? kg.ToString("0.0", CultureInfo.InvariantCulture) + " kg" : "—";
        public static string Litres(double litres) => Finite(litres) ? litres.ToString("0.0", CultureInfo.InvariantCulture) + " L" : "—";
        public static string Energy(double kWh) => Finite(kWh) ? kWh.ToString("0.00", CultureInfo.InvariantCulture) + " kWh" : "—";
        public static string Ppm(double ppm) => Finite(ppm) ? ppm.ToString("0", CultureInfo.InvariantCulture) + " ppm" : "—";

        /// <summary>Signed change since the routine began: "−4.5", "+0.2", "±0" (same decimals as the value).</summary>
        public static string Delta(double change, string format)
        {
            if (!Finite(change)) return string.Empty;
            string magnitude = Math.Abs(change).ToString(format, CultureInfo.InvariantCulture);
            double zero = 0d;
            if (magnitude == zero.ToString(format, CultureInfo.InvariantCulture)) return "±0";
            return (change < 0d ? "−" : "+") + magnitude;
        }
    }
}

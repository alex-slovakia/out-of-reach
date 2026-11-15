using System;
using System.Globalization;

namespace OutOfReach.Presentation.Routine
{
    /// <summary>Stations of the routine montage, in the order one crew day is shown: crops, meal, water, sleep.</summary>
    public enum RoutineStation { None = 0, Crops = 1, Pantry = 2, Water = 3, Berth = 4 }

    /// <summary>What Core reports about the running routine segment in one frame. Read-only observation.</summary>
    public readonly struct RoutineFacts
    {
        public RoutineFacts(bool running, double segmentKey, int rations, int drinks, int rests, bool resting,
            bool cropGrowing, double cropRemainingSimulationSeconds, int missionDay)
        {
            Running = running;
            SegmentKey = segmentKey;
            Rations = rations;
            Drinks = drinks;
            Rests = rests;
            Resting = resting;
            CropGrowing = cropGrowing;
            CropRemainingSimulationSeconds = cropRemainingSimulationSeconds;
            MissionDay = missionDay;
        }

        public bool Running { get; }
        /// <summary>Start time of the segment (NaN when unknown): a new value is a new segment.</summary>
        public double SegmentKey { get; }
        /// <summary>Core's own tally of real commands in this segment.</summary>
        public int Rations { get; }
        public int Drinks { get; }
        public int Rests { get; }
        public bool Resting { get; }
        /// <summary>The grow module is in its Growing stage (Core charges its power and water).</summary>
        public bool CropGrowing { get; }
        public double CropRemainingSimulationSeconds { get; }
        public int MissionDay { get; }
    }

    /// <summary>
    /// R18 shot schedule of the routine montage. It decides which station is on screen, and only from what Core
    /// actually did: a ration eaten, a water portion drunk, a night's rest begun, a grow module that is growing.
    /// A station whose action did not happen is never shown. Pure logic: no time is advanced, nothing is charged.
    /// </summary>
    public sealed class RoutineStationSchedule
    {
        /// <summary>Real seconds a station stays before the next executed action may replace it.</summary>
        public const float ShotSeconds = 1.15f;
        /// <summary>Reduced effects: fewer, calmer cuts.</summary>
        public const float ReducedShotSeconds = 2.3f;
        /// <summary>With nothing new to show, the last station leaves after this long.</summary>
        public const float IdleHoldSeconds = 3.5f;

        private const double SecondsPerDay = 86400d;
        private const int StationCount = 4;

        private readonly bool[] pending = new bool[StationCount + 1];
        private bool observed;
        private double segmentKey = double.NaN;
        private int seenRations, seenDrinks, seenRests, cropDay = int.MinValue;

        public RoutineStation Current { get; private set; }
        /// <summary>Real seconds the current station has been on screen (frozen by pause).</summary>
        public float CurrentSeconds { get; private set; }
        /// <summary>Increments whenever the station changes (including to None).</summary>
        public int Cuts { get; private set; }
        public string Caption { get; private set; } = string.Empty;

        public bool IsPending(RoutineStation station) => station != RoutineStation.None && pending[(int)station];

        /// <summary>Forgets the segment: nothing is shown and nothing is owed.</summary>
        public void Reset()
        {
            Array.Clear(pending, 0, pending.Length);
            observed = false;
            segmentKey = double.NaN;
            seenRations = seenDrinks = seenRests = 0;
            cropDay = int.MinValue;
            CurrentSeconds = 0f;
            Caption = string.Empty;
            if (Current != RoutineStation.None)
            {
                Current = RoutineStation.None;
                Cuts++;
            }
        }

        /// <summary>One presentation frame. A stopped routine clears the screen in this same call.</summary>
        public void Observe(in RoutineFacts facts, float realSeconds, bool paused, bool reducedEffects)
        {
            if (!facts.Running)
            {
                Reset();
                return;
            }

            if (!observed || !facts.SegmentKey.Equals(segmentKey))
            {
                Reset();
                observed = true;
                segmentKey = facts.SegmentKey;
            }

            // Only real commands raise a station: Core's tally went up since the last frame.
            if (facts.Rations > seenRations) pending[(int)RoutineStation.Pantry] = true;
            if (facts.Drinks > seenDrinks) pending[(int)RoutineStation.Water] = true;
            if (facts.Rests > seenRests) pending[(int)RoutineStation.Berth] = true;
            seenRations = facts.Rations;
            seenDrinks = facts.Drinks;
            seenRests = facts.Rests;

            // The crop is shown once per mission day, and only while the module really grows.
            if (!facts.CropGrowing)
            {
                pending[(int)RoutineStation.Crops] = false;
            }
            else if (facts.MissionDay != cropDay)
            {
                cropDay = facts.MissionDay;
                pending[(int)RoutineStation.Crops] = true;
            }

            if (!paused)
            {
                if (Current != RoutineStation.None && realSeconds > 0f && !float.IsNaN(realSeconds))
                    CurrentSeconds += realSeconds;

                float dwell = reducedEffects ? ReducedShotSeconds : ShotSeconds;
                if (Current == RoutineStation.None || CurrentSeconds >= dwell)
                {
                    RoutineStation next = NextPending();
                    // Asleep with nothing else owed: the berth is where the crew is.
                    if (next == RoutineStation.None && facts.Resting && Current != RoutineStation.Berth)
                        next = RoutineStation.Berth;

                    if (next != RoutineStation.None)
                    {
                        pending[(int)next] = false;
                        if (next != Current) Cut(next);
                        else CurrentSeconds = 0f;
                    }
                    else if (Current == RoutineStation.Berth ? !facts.Resting : CurrentSeconds >= IdleHoldSeconds)
                    {
                        if (Current != RoutineStation.None) Cut(RoutineStation.None);
                    }
                }
            }

            Caption = Describe(Current, facts);
        }

        /// <summary>The caption states Core's own numbers; it never names an action that did not happen.</summary>
        public static string Describe(RoutineStation station, in RoutineFacts facts)
        {
            switch (station)
            {
                case RoutineStation.Crops:
                {
                    double days = facts.CropRemainingSimulationSeconds / SecondsPerDay;
                    return !double.IsNaN(days) && !double.IsInfinity(days) && days > 0d
                        ? "CROPS · grow module running · harvest in " + days.ToString("0.0", CultureInfo.InvariantCulture) + " days"
                        : "CROPS · grow module running";
                }
                case RoutineStation.Pantry:
                    return "GALLEY · stored ration eaten · " + Count(facts.Rations, "meal", "meals") + " this stretch";
                case RoutineStation.Water:
                    return "WATER · portion drunk · " + Count(facts.Drinks, "drink", "drinks") + " this stretch";
                case RoutineStation.Berth:
                    return "BERTH · ordinary sleep in the secured bag · night "
                        + Math.Max(1, facts.Rests).ToString(CultureInfo.InvariantCulture);
                default:
                    return string.Empty;
            }
        }

        private static string Count(int value, string one, string many)
        {
            int shown = Math.Max(1, value);
            return shown.ToString(CultureInfo.InvariantCulture) + " " + (shown == 1 ? one : many);
        }

        private void Cut(RoutineStation next)
        {
            Current = next;
            CurrentSeconds = 0f;
            Cuts++;
        }

        /// <summary>Round robin in day order, starting after the station on screen.</summary>
        private RoutineStation NextPending()
        {
            int start = (int)Current;
            for (int step = 1; step <= StationCount; step++)
            {
                int index = ((start + StationCount - 1 + step) % StationCount) + 1;
                if (pending[index]) return (RoutineStation)index;
            }

            return RoutineStation.None;
        }
    }
}

using System;
using System.Globalization;

namespace OutOfReach.Core
{
    /// <summary>
    /// The single mapping between the simulation clock and a calendar date:
    /// mission date = mission clock T0 (UTC) + elapsed simulated seconds. Day numbers are 1-based for display, so
    /// elapsed day 0 is "mission day 1" and reference row 30 is "mission day 31". No wall clock is ever read, and
    /// because the date follows the one simulation clock, pause, acceleration and chapter jumps move it consistently.
    /// </summary>
    public sealed class MissionCalendar
    {
        public const double SecondsPerDay = 86400d;

        private readonly Func<double> elapsedSimulationSeconds;

        public MissionCalendar(DateTime epochAtStartUtc, Func<double> elapsedSimulationSeconds)
        {
            EpochAtStartUtc = DateTime.SpecifyKind(epochAtStartUtc, DateTimeKind.Utc);
            this.elapsedSimulationSeconds = elapsedSimulationSeconds ?? throw new ArgumentNullException(nameof(elapsedSimulationSeconds));
        }

        /// <summary>Mission clock T0: the date at zero elapsed simulated seconds, UTC.</summary>
        public DateTime EpochAtStartUtc { get; }

        /// <summary>Current mission date, UTC.</summary>
        public DateTime NowUtc => ToEpochUtc(elapsedSimulationSeconds());

        /// <summary>Current 1-based mission day.</summary>
        public int DayNumber => DayNumberAt(elapsedSimulationSeconds());

        /// <summary>
        /// Mission date for an elapsed time. Rounded down to whole clock ticks (100 ns), so a date never runs ahead
        /// of the simulation; times beyond the calendar's range clamp to its last representable date.
        /// </summary>
        public DateTime ToEpochUtc(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0d)
            {
                return EpochAtStartUtc;
            }

            double ticks = Math.Floor(elapsedSeconds * TimeSpan.TicksPerSecond);
            long room = DateTime.MaxValue.Ticks - EpochAtStartUtc.Ticks;
            if (double.IsInfinity(ticks) || ticks >= room)
            {
                return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            }

            return new DateTime(EpochAtStartUtc.Ticks + (long)ticks, DateTimeKind.Utc);
        }

        /// <summary>1-based mission day for an elapsed time: day 1 covers the first 86,400 simulated seconds.</summary>
        public static int DayNumberAt(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0d)
            {
                return 1;
            }

            double day = Math.Floor(elapsedSeconds / SecondsPerDay);
            return day >= int.MaxValue - 1 ? int.MaxValue : 1 + (int)day;
        }

        /// <summary>"2026-11-14 16:40 UTC".</summary>
        public static string Format(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        }

        /// <summary>Elapsed mission time for the report: "T+0 h 32 min" or "T+3 d 04 h 10 min".</summary>
        public static string FormatElapsed(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            {
                return "T+?";
            }

            long totalMinutes = (long)Math.Floor(Math.Min(elapsedSeconds, long.MaxValue / 2d) / 60d);
            long days = totalMinutes / 1440;
            long hours = (totalMinutes % 1440) / 60;
            long minutes = totalMinutes % 60;
            return days > 0
                ? string.Format(CultureInfo.InvariantCulture, "T+{0} d {1:00} h {2:00} min", days, hours, minutes)
                : string.Format(CultureInfo.InvariantCulture, "T+{0} h {1:00} min", hours, minutes);
        }
    }
}

using System;
using System.Collections.Generic;

namespace OutOfReach.Core
{
    /// <summary>One thing that happened, at its exact time on the simulation clock.</summary>
    public readonly struct MissionLogEntry
    {
        public MissionLogEntry(double atSimulationSeconds, string text)
        {
            AtSimulationSeconds = atSimulationSeconds;
            Text = text ?? string.Empty;
        }

        public double AtSimulationSeconds { get; }

        public string Text { get; }
    }

    /// <summary>
    /// What happened and when, recorded by Core at the moment each state change is committed (not when its event
    /// is delivered, which may be after a long simulation step). Feeds the report's timeline. One per session, so
    /// nothing survives a restart.
    /// </summary>
    public sealed class MissionJournal
    {
        private readonly List<MissionLogEntry> entries = new List<MissionLogEntry>();
        private readonly Func<double> clock;

        public MissionJournal(Func<double> simulationClockSeconds)
        {
            clock = simulationClockSeconds ?? throw new ArgumentNullException(nameof(simulationClockSeconds));
        }

        public int Count => entries.Count;

        /// <summary>Records an entry at the current simulation time.</summary>
        public void Record(string text)
        {
            Record(clock(), text);
        }

        /// <summary>Records an entry at an explicit simulation time.</summary>
        public void Record(double atSimulationSeconds, string text)
        {
            if (string.IsNullOrEmpty(text) || double.IsNaN(atSimulationSeconds))
            {
                return;
            }

            entries.Add(new MissionLogEntry(atSimulationSeconds, text));
        }

        /// <summary>A copy ordered by time; entries at the same time keep the order they were recorded in.</summary>
        public IReadOnlyList<MissionLogEntry> Snapshot()
        {
            var copy = new List<MissionLogEntry>(entries);
            var order = new int[copy.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byTime = copy[a].AtSimulationSeconds.CompareTo(copy[b].AtSimulationSeconds);
                return byTime != 0 ? byTime : a.CompareTo(b);
            });

            var sorted = new MissionLogEntry[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                sorted[i] = copy[order[i]];
            }

            return Array.AsReadOnly(sorted);
        }
    }
}

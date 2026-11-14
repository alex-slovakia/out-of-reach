using System;
using System.Collections.Generic;

namespace OutOfReach.Contracts
{
    /// <summary>One display-ready fact. Units are part of Value.</summary>
    public readonly struct MissionReportLine
    {
        public string Label { get; }
        public string Value { get; }

        public MissionReportLine(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }
    }

    /// <summary>A titled group of snapshot facts. Id is stable for layout and styling.</summary>
    public readonly struct MissionReportSection
    {
        private readonly IReadOnlyList<MissionReportLine> lines;
        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<MissionReportLine> Lines => lines ?? Array.Empty<MissionReportLine>();

        public MissionReportSection(string id, string title, IReadOnlyList<MissionReportLine> lines)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            if (lines == null || lines.Count == 0)
            {
                this.lines = Array.Empty<MissionReportLine>();
                return;
            }

            var snapshot = new MissionReportLine[lines.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = lines[i];
            this.lines = Array.AsReadOnly(snapshot);
        }
    }

    /// <summary>Immutable debrief built by Core from authoritative state. Presentation only displays it.</summary>
    public interface IMissionReport
    {
        MissionOutcome Outcome { get; }
        /// <summary>False for a preview requested while the mission still runs.</summary>
        bool IsFinal { get; }
        string Headline { get; }
        string OutcomeReason { get; }
        double ElapsedSimulationSeconds { get; }
        IReadOnlyList<MissionReportSection> Sections { get; }
    }
}

using System;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Report
{
    /// <summary>Text-only elements. Values and units remain exactly as supplied by Core.</summary>
    public static class FlightRecorderElements
    {
        public static Label Text(string text, string className)
        {
            var label = new Label(text ?? string.Empty) { enableRichText = false };
            label.AddToClassList(className);
            return label;
        }

        public static VisualElement Section(MissionReportSection section, int ordinal)
        {
            var element = new VisualElement
            {
                name = section.Id ?? string.Empty,
                userData = section.Id,
                focusable = true,
                tabIndex = -1
            };
            element.AddToClassList("recorder-section");
            string classification = Classification(section.Id);
            if (classification != null)
            {
                element.AddToClassList("recorder-evidence");
                element.Add(Text(classification, "recorder-classification"));
            }

            var heading = new VisualElement();
            heading.AddToClassList("recorder-section-heading");
            heading.Add(Text(ordinal.ToString("00", CultureInfo.InvariantCulture), "recorder-number"));
            heading.Add(Text(string.IsNullOrWhiteSpace(section.Title) ? section.Id : section.Title,
                "recorder-section-title"));
            element.Add(heading);

            foreach (MissionReportLine line in section.Lines)
            {
                var row = new VisualElement();
                row.AddToClassList("recorder-fact");
                row.Add(Text(line.Label, "recorder-label"));
                var value = Text(line.Value, "recorder-value");
                value.selection.isSelectable = true;
                row.Add(value);
                element.Add(row);
            }

            if (section.Lines.Count == 0)
                element.Add(Text("No facts recorded in this section.", "recorder-empty"));
            return element;
        }

        public static string Classification(string sectionId)
        {
            switch (sectionId)
            {
                case "nasa-data": return "NASA / JPL DATA";
                case "derived-physics": return "DERIVED PHYSICS";
                case "fictional-scenario": return "FICTIONAL SCENARIO";
                case "time-compression": return "GAMEPLAY TIME COMPRESSION";
                default: return null;
            }
        }

        public static string ElapsedTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
                return "Simulation time unavailable";
            // Do not use TimeSpan: a valid finite domain value may exceed its range.
            double wholeSeconds = Math.Floor(seconds);
            double days = Math.Floor(wholeSeconds / 86400d);
            double hours = Math.Floor(wholeSeconds % 86400d / 3600d);
            double minutes = Math.Floor(wholeSeconds % 3600d / 60d);
            return string.Format(CultureInfo.InvariantCulture,
                "Elapsed simulation time  {0:0}d {1:00}h {2:00}m {3:00}s",
                days, hours, minutes, wholeSeconds % 60d);
        }
    }
}

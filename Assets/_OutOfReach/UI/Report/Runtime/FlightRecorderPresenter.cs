using System;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Report
{
    /// <summary>
    /// Owned subtree for a UIDocument or terminal surface. No simulation, scene loading or event-bus writes.
    /// The composition root supplies a report snapshot and handles restart requests.
    /// </summary>
    public sealed class FlightRecorderPresenter : IDisposable
    {
        private readonly Label status;
        private readonly Label headline;
        private readonly Label reason;
        private readonly Label context;
        private readonly Label elapsed;
        private readonly ScrollView record;
        private readonly Button recordButton;
        private readonly Button sourcesButton;
        private readonly Button restartButton;
        private VisualElement firstEvidence;
        private bool canRestart;
        private bool restartPending;
        private bool disposed;

        public FlightRecorderPresenter(VisualElement host, StyleSheet styleSheet)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (styleSheet == null) throw new ArgumentNullException(nameof(styleSheet));

            Root = new VisualElement { name = "flightRecorder" };
            Root.AddToClassList("flight-recorder");
            Root.styleSheets.Add(styleSheet);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            var masthead = new VisualElement();
            masthead.AddToClassList("recorder-masthead");
            masthead.Add(FlightRecorderElements.Text("AURORA-7  /  FLIGHT RECORDER", "recorder-identifier"));
            status = FlightRecorderElements.Text(string.Empty, "recorder-status");
            masthead.Add(status);
            Root.Add(masthead);

            var summary = new VisualElement();
            summary.AddToClassList("recorder-summary");
            headline = FlightRecorderElements.Text(string.Empty, "recorder-headline");
            reason = FlightRecorderElements.Text(string.Empty, "recorder-reason");
            context = FlightRecorderElements.Text(string.Empty, "recorder-context");
            elapsed = FlightRecorderElements.Text(string.Empty, "recorder-elapsed");
            summary.Add(headline);
            summary.Add(reason);
            summary.Add(context);
            summary.Add(elapsed);

            // Summary lives inside the scroll area too: long causal explanations cannot push controls offscreen.
            record = new ScrollView(ScrollViewMode.Vertical) { name = "missionRecord" };
            record.AddToClassList("recorder-scroll");
            record.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            record.verticalScrollerVisibility = ScrollerVisibility.Auto;
            Summary = summary;
            Root.Add(record);

            var footer = new VisualElement();
            footer.AddToClassList("recorder-footer");
            var navigation = new VisualElement();
            navigation.AddToClassList("recorder-navigation");
            recordButton = Button("Mission record", "missionRecordButton", OnRecordRequested);
            sourcesButton = Button("Sources & assumptions", "reportSourcesButton", OnSourcesRequested);
            navigation.Add(recordButton);
            navigation.Add(sourcesButton);
            footer.Add(navigation);
            restartButton = Button("Plan again", "reportRestartButton", OnRestartRequested);
            restartButton.AddToClassList("recorder-restart");
            footer.Add(restartButton);
            Root.Add(footer);
            host.Add(Root);
            Clear();
        }

        public VisualElement Root { get; }
        private VisualElement Summary { get; }
        public event Action RestartRequested;

        /// <summary>Replaces every displayed fact. Null clears the view. No model reference is retained.</summary>
        public void Configure(IMissionReport report)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FlightRecorderPresenter));
            Clear();
            if (report == null) return;

            bool recovery = report.IsFinal && report.Outcome == MissionOutcome.RecoveryPossible;
            bool lost = report.IsFinal && report.Outcome == MissionOutcome.Lost;
            status.text = !report.IsFinal ? "PREVIEW / MISSION IN PROGRESS"
                : recovery ? "CONTACT RESTORED" : lost ? "MISSION LOST" : "OUTCOME UNAVAILABLE";
            Root.EnableInClassList("recorder-recovery", recovery);
            Root.EnableInClassList("recorder-lost", lost);
            headline.text = string.IsNullOrWhiteSpace(report.Headline) ? status.text : report.Headline;
            reason.text = string.IsNullOrWhiteSpace(report.OutcomeReason)
                ? (report.IsFinal ? "No outcome explanation recorded." : "The mission has not ended.")
                : report.OutcomeReason;
            context.text = recovery ? "Recovery planning is possible. Rescue has not arrived."
                : lost ? "The recorded cause below explains why the mission objective was lost."
                : report.IsFinal ? "The final outcome was not supplied."
                : "This is not a final mission record.";
            elapsed.text = FlightRecorderElements.ElapsedTime(report.ElapsedSimulationSeconds);
            record.Add(Summary);

            var sections = report.Sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    MissionReportSection section = sections[i];
                    VisualElement element = FlightRecorderElements.Section(section, i + 1);
                    record.Add(element);
                    if (firstEvidence == null && FlightRecorderElements.Classification(section.Id) != null)
                        firstEvidence = element;
                }
            }
            if (sections == null || sections.Count == 0)
                record.Add(FlightRecorderElements.Text("No detailed facts were supplied with this report.", "recorder-empty"));

            canRestart = recovery || lost;
            restartButton.SetEnabled(canRestart);
            sourcesButton.SetEnabled(firstEvidence != null);
            Root.RemoveFromClassList("recorder-hidden");
            record.scrollOffset = Vector2.zero;
        }

        /// <summary>Hides and empties only this presenter's subtree. Safe after disposal.</summary>
        public void Clear()
        {
            if (disposed) return;
            canRestart = false;
            restartPending = false;
            firstEvidence = null;
            record.Clear();
            status.text = headline.text = reason.text = context.text = elapsed.text = string.Empty;
            Root.RemoveFromClassList("recorder-recovery");
            Root.RemoveFromClassList("recorder-lost");
            Root.AddToClassList("recorder-hidden");
            restartButton.text = "Plan again";
            restartButton.SetEnabled(false);
            sourcesButton.SetEnabled(false);
        }

        /// <summary>Composition may re-enable the request if a scene restart was rejected or failed.</summary>
        public void ResetRestartRequest()
        {
            if (disposed) return;
            restartPending = false;
            restartButton.text = "Plan again";
            restartButton.SetEnabled(canRestart);
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            Root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            recordButton.clicked -= OnRecordRequested;
            sourcesButton.clicked -= OnSourcesRequested;
            restartButton.clicked -= OnRestartRequested;
            RestartRequested = null;
            Root.RemoveFromHierarchy();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Root.EnableInClassList("recorder-compact", evt.newRect.width < 600f);
        }

        private void OnRecordRequested()
        {
            record.scrollOffset = Vector2.zero;
            record.Focus();
        }

        private void OnSourcesRequested()
        {
            if (firstEvidence == null) return;
            record.ScrollTo(firstEvidence);
            firstEvidence.Focus();
        }

        private void OnRestartRequested()
        {
            if (disposed || !canRestart || restartPending || RestartRequested == null) return;
            restartPending = true;
            restartButton.text = "Restart requested";
            restartButton.SetEnabled(false);
            try { RestartRequested.Invoke(); }
            catch
            {
                ResetRestartRequest();
                throw;
            }
        }

        private static Button Button(string text, string name, Action onClick)
        {
            var button = new Button(onClick) { text = text, name = name };
            button.AddToClassList("recorder-button");
            return button;
        }
    }
}

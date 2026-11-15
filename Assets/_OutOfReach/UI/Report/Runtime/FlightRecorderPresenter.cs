using System;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Report
{
    /// <summary>Optional record behind a quiet end card. Core/root own outcomes, ending timing and restart.</summary>
    public sealed class FlightRecorderPresenter : IDisposable
    {
        private const string MarsSourceUrl = "https://science.nasa.gov/3d-resources/mars/";
        private readonly VisualElement endCard, detail, summary, marsCredits, rollControls;
        private readonly Label endHeadline, endSummary, endEyebrow, endElapsed, detailTitle, headline, reason, elapsed;
        private readonly ScrollView record, sources;
        private readonly VisualElement sourceLayout, nasaPage, artPage, assumptionsPage;
        private readonly Button nasaTab, artTab, assumptionsTab;
        private readonly Button sourceUp, sourceDown;
        private readonly Button recordButton, sourcesButton, restartButton, backButton, marsSourceButton, rollPauseButton, rollSkipButton;
        private bool hasReport, hasSources, showingSources, canRestart, restartPending, disposed;
        private bool reducedMotion;
        private float lastMenuFeedback = -1f;
        private IVisualElementScheduledItem entrance;
        private IVisualElementScheduledItem recordRoll;
        private bool finalRecord, rollPaused;
        private double rollLastTime, rollStartsAt;

        public FlightRecorderPresenter(VisualElement host, StyleSheet styleSheet)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (styleSheet == null) throw new ArgumentNullException(nameof(styleSheet));
            Root = Element("flightRecorder", "flight-recorder");
            Root.styleSheets.Add(styleSheet);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Root.RegisterCallback<KeyDownEvent>(OnDetailKeyDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnDetailCancel);
            Root.Add(new ReportShade());
            endCard = Element("reportEndCard", "recorder-end-card");
            var hero = Element("reportHero", "recorder-end-hero");
            endEyebrow = FlightRecorderElements.Text(string.Empty, "recorder-end-eyebrow");
            endHeadline = FlightRecorderElements.Text(string.Empty, "recorder-end-headline");
            endSummary = FlightRecorderElements.Text(string.Empty, "recorder-end-summary");
            hero.Add(endHeadline);
            hero.Add(endSummary);
            var actions = Element("reportEndActions", "recorder-end-actions");
            recordButton = Button("Mission record", "missionRecordButton", OnRecordRequested);
            sourcesButton = Button("Sources", "reportSourcesButton", OnSourcesRequested);
            restartButton = Button("Plan again", "reportRestartButton", OnRestartRequested);
            restartButton.AddToClassList("recorder-restart");
            recordButton.AddToClassList("recorder-primary-action");
            actions.Add(recordButton);
            actions.Add(sourcesButton);
            actions.Add(restartButton);
            hero.Add(actions);
            endCard.Add(hero);
            endElapsed = FlightRecorderElements.Text(string.Empty, "recorder-end-elapsed");
            Root.Add(endCard);

            detail = Element("reportDetail", "recorder-detail");
            var navigation = Element("reportDetailNavigation", "recorder-detail-navigation");
            backButton = Button("Back", "reportBackButton", OnBackRequested);
            navigation.Add(backButton);
            detailTitle = FlightRecorderElements.Text(string.Empty, "recorder-detail-title");
            navigation.Add(detailTitle);
            detail.Add(navigation);
            rollControls = Element("recordRollControls", "recorder-roll-controls");
            rollControls.Add(FlightRecorderElements.Text("YOUR DECISIONS / THE SCIENCE BEHIND THEM", "recorder-roll-caption"));
            rollPauseButton = Button("Pause scroll", "recordRollPauseButton", ToggleRecordRoll);
            rollSkipButton = Button("Skip recap", "recordRollSkipButton", OnBackRequested);
            rollControls.Add(rollPauseButton);
            rollControls.Add(rollSkipButton);
            detail.Add(rollControls);
            record = Scroll("missionRecord");
            record.RegisterCallback<WheelEvent>(OnRecordWheel, TrickleDown.TrickleDown);
            record.RegisterCallback<PointerDownEvent>(OnRecordPointerDown, TrickleDown.TrickleDown);
            sources = Scroll("reportSources");
            sources.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            sourceLayout = Element("reportSourceLayout", "recorder-source-layout");
            var sourceNav = Element("reportSourceNavigation", "recorder-source-navigation");
            sourceNav.Add(FlightRecorderElements.Text("SOURCES", "recorder-source-title"));
            nasaTab = SourceTab("NASA DATA", "reportNasaTab", 0);
            artTab = SourceTab("MUSIC & ART", "reportArtTab", 1);
            assumptionsTab = SourceTab("ASSUMPTIONS", "reportAssumptionsTab", 2);
            sourceNav.Add(nasaTab); sourceNav.Add(artTab); sourceNav.Add(assumptionsTab);
            nasaPage = Element("reportNasaPage", "recorder-source-page");
            artPage = Element("reportArtPage", "recorder-source-page");
            assumptionsPage = Element("reportAssumptionsPage", "recorder-source-page");
            sources.Add(nasaPage); sources.Add(artPage); sources.Add(assumptionsPage);
            var sourceBody = Element("reportSourceBody", "recorder-source-body");
            var sourceArrows = Element("reportSourceArrows", "recorder-source-arrows");
            sourceUp = Button("↑", "reportSourcesUp", () => ScrollSources(-1));
            sourceDown = Button("↓", "reportSourcesDown", () => ScrollSources(1));
            sourceUp.tooltip = "Scroll up";
            sourceDown.tooltip = "Scroll down";
            sourceArrows.Add(sourceUp); sourceArrows.Add(sourceDown);
            sourceBody.Add(sources); sourceBody.Add(sourceArrows);
            sources.contentContainer.RegisterCallback<GeometryChangedEvent>(OnSourceGeometryChanged);
            sources.contentViewport.RegisterCallback<GeometryChangedEvent>(OnSourceGeometryChanged);
            sources.verticalScroller.valueChanged += OnSourceScrollChanged;
            sourceLayout.Add(sourceNav); sourceLayout.Add(sourceBody);
            marsCredits = Element("marsImageryCredits", "recorder-section");
            marsCredits.Add(FlightRecorderElements.Text("Mars surface imagery", "recorder-section-title"));
            marsCredits.Add(FlightRecorderElements.Text("NASA/Jet Propulsion Laboratory & Caltech", "recorder-classification"));
            marsCredits.Add(FlightRecorderElements.Text(
                "Historical Viking imagery processed at USGS; JPL/Caltech planetary maps database. Not a live observation.",
                "recorder-empty"));
            marsCredits.Add(FlightRecorderElements.Text(
                "Lighting and framing are authored for OUT OF REACH. The later Mars shot is a cinematic time jump, not a real-time trajectory. No NASA endorsement is implied.",
                "recorder-empty"));
            marsSourceButton = new Button(OnMarsSourceRequested)
            {
                name = "marsSourceButton", text = "NASA Mars image source (opens browser)", tooltip = MarsSourceUrl
            };
            marsSourceButton.AddToClassList("recorder-button");
            marsSourceButton.RegisterCallback<PointerEnterEvent>(OnMarsSourcePointerEnter);
            marsSourceButton.RegisterCallback<FocusInEvent>(OnMarsSourceFocusIn);
            marsCredits.Add(marsSourceButton);
            detail.Add(record);
            detail.Add(sourceLayout);
            Root.Add(detail);

            summary = Element("reportFullSummary", "recorder-summary");
            headline = FlightRecorderElements.Text(string.Empty, "recorder-headline");
            reason = FlightRecorderElements.Text(string.Empty, "recorder-reason");
            elapsed = FlightRecorderElements.Text(string.Empty, "recorder-elapsed");
            summary.Add(headline);
            summary.Add(reason);
            summary.Add(elapsed);
            host.Add(Root);
            RegisterFeedback(recordButton);
            RegisterFeedback(sourcesButton);
            RegisterFeedback(restartButton);
            RegisterFeedback(backButton);
            RegisterFeedback(rollPauseButton);
            RegisterFeedback(rollSkipButton);
            Clear();
        }

        public VisualElement Root { get; }
        public event Action RestartRequested;
        public event Action<bool> MenuFeedbackRequested;

        public void SetReducedMotion(bool reduced)
        {
            bool newlyReduced = reduced && !reducedMotion;
            reducedMotion = reduced;
            Root.EnableInClassList("recorder-reduced", reduced);
            if (reduced) { entrance?.Pause(); endCard.RemoveFromClassList("recorder-entering"); }
            if (newlyReduced) PauseRecordRoll();
        }

        /// <summary>Copies snapshot text; Configure always starts on the end card, never the detailed record.</summary>
        public void Configure(IMissionReport report)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FlightRecorderPresenter));
            Clear();
            if (report == null) return;
            hasReport = true;
            finalRecord = report.IsFinal;
            endHeadline.text = FlightRecorderEndCardCopy.Headline(report);
            endSummary.text = FlightRecorderEndCardCopy.Summary(report);
            endEyebrow.text = report.Outcome == MissionOutcome.Recovered ? "CREW RECOVERED"
                : report.Outcome == MissionOutcome.Lost ? "FLIGHT ENDED" : "EARTH LINK RESTORED";
            endElapsed.text = FlightRecorderElements.ElapsedTime(report.ElapsedSimulationSeconds).ToUpperInvariant();
            Root.EnableInClassList("recorder-recovery", report.IsFinal &&
                (report.Outcome == MissionOutcome.Recovered || report.Outcome == MissionOutcome.RecoveryPossible));
            Root.EnableInClassList("recorder-lost", report.IsFinal && report.Outcome == MissionOutcome.Lost);
            Root.EnableInClassList("recorder-contact", report.IsFinal && report.Outcome == MissionOutcome.RecoveryPossible);
            headline.text = string.IsNullOrWhiteSpace(report.Headline) ? endHeadline.text : report.Headline;
            reason.text = string.IsNullOrWhiteSpace(report.OutcomeReason)
                ? (report.IsFinal ? "No outcome explanation recorded." : "The mission has not ended.") : report.OutcomeReason;
            elapsed.text = FlightRecorderElements.ElapsedTime(report.ElapsedSimulationSeconds);
            record.Add(summary);
            var sections = report.Sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    MissionReportSection section = sections[i];
                    record.Add(FlightRecorderElements.Section(section, i + 1, OnReportSourceRequested));
                }
            }
            if (sections == null || sections.Count == 0)
                record.Add(FlightRecorderElements.Text("No detailed facts were supplied with this report.", "recorder-empty"));
            BuildSources(report);
            marsSourceButton.SetEnabled(true);
            hasSources = true;
            canRestart = FlightRecorderEndCardCopy.CanRestart(report);
            recordButton.SetEnabled(true);
            restartButton.SetEnabled(canRestart);
            sourcesButton.SetEnabled(hasSources);
            Show(Root, true);
            ShowEndCard(false);
            if (!reducedMotion)
            {
                endCard.AddToClassList("recorder-entering");
                entrance = endCard.schedule.Execute(() => endCard.RemoveFromClassList("recorder-entering")).StartingIn(40);
            }
        }

        /// <summary>Hides and empties only the owned subtree. Safe after disposal.</summary>
        public void Clear()
        {
            if (disposed) return;
            StopRecordRoll();
            entrance?.Pause();
            entrance = null;
            endCard.RemoveFromClassList("recorder-entering");
            hasReport = hasSources = showingSources = canRestart = restartPending = false;
            finalRecord = false;
            record.Clear();
            nasaPage.Clear(); artPage.Clear(); assumptionsPage.Clear();
            marsSourceButton.SetEnabled(false);
            endHeadline.text = endSummary.text = detailTitle.text = headline.text = reason.text = elapsed.text = string.Empty;
            endEyebrow.text = endElapsed.text = string.Empty;
            Root.RemoveFromClassList("recorder-recovery");
            Root.RemoveFromClassList("recorder-lost");
            Root.RemoveFromClassList("recorder-contact");
            Show(Root, false);
            Show(detail, false);
            Show(rollControls, false);
            Show(record, false);
            Show(sources, false);
            Show(sourceLayout, false);
            Show(endCard, true);
            restartButton.text = "Plan again";
            restartButton.SetEnabled(false);
            recordButton.SetEnabled(false);
            sourcesButton.SetEnabled(false);
        }

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
            Root.UnregisterCallback<KeyDownEvent>(OnDetailKeyDown);
            Root.UnregisterCallback<NavigationCancelEvent>(OnDetailCancel);
            sources.contentContainer.UnregisterCallback<GeometryChangedEvent>(OnSourceGeometryChanged);
            sources.contentViewport.UnregisterCallback<GeometryChangedEvent>(OnSourceGeometryChanged);
            sources.verticalScroller.valueChanged -= OnSourceScrollChanged;
            record.UnregisterCallback<WheelEvent>(OnRecordWheel, TrickleDown.TrickleDown);
            record.UnregisterCallback<PointerDownEvent>(OnRecordPointerDown, TrickleDown.TrickleDown);
            recordButton.clicked -= OnRecordRequested;
            sourcesButton.clicked -= OnSourcesRequested;
            restartButton.clicked -= OnRestartRequested;
            backButton.clicked -= OnBackRequested;
            marsSourceButton.clicked -= OnMarsSourceRequested;
            marsSourceButton.UnregisterCallback<PointerEnterEvent>(OnMarsSourcePointerEnter);
            marsSourceButton.UnregisterCallback<FocusInEvent>(OnMarsSourceFocusIn);
            RestartRequested = null;
            MenuFeedbackRequested = null;
            Root.RemoveFromHierarchy();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Root.EnableInClassList("recorder-compact", evt.newRect.width < 600f);
            Root.EnableInClassList("recorder-loss-compact", evt.newRect.width < 1000f || evt.newRect.height < 600f);
        }
        private void RegisterFeedback(Button button)
        {
            button.RegisterCallback<PointerEnterEvent>(_ => MenuFeedback(button, false));
            button.RegisterCallback<FocusInEvent>(_ => MenuFeedback(button, false));
        }
        private void MenuFeedback(Button button, bool confirm)
        {
            if (disposed || !hasReport || !button.enabledInHierarchy) return;
            if (!confirm && Time.unscaledTime - lastMenuFeedback < .08f) return;
            lastMenuFeedback = Time.unscaledTime;
            MenuFeedbackRequested?.Invoke(confirm);
        }
        private void OnMarsSourcePointerEnter(PointerEnterEvent evt) => MenuFeedback(marsSourceButton, false);
        private void OnMarsSourceFocusIn(FocusInEvent evt) => MenuFeedback(marsSourceButton, false);
        private void OnMarsSourceRequested()
        {
            if (disposed || !hasReport || sources.ClassListContains("recorder-hidden")) return;
            MenuFeedback(marsSourceButton, true);
            if (disposed || !hasReport || sources.ClassListContains("recorder-hidden")) return;
            Application.OpenURL(MarsSourceUrl);
        }
        private void OnRecordRequested() { if (!disposed && hasReport) OpenDetail(false); }
        private void OnSourcesRequested() { if (!disposed && hasReport && hasSources) OpenDetail(true); }
        private void OnBackRequested() { if (!disposed && hasReport) ShowEndCard(true); }

        private void OnDetailKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || detail.ClassListContains("recorder-hidden")) return;
            OnBackRequested();
            evt.StopPropagation();
        }
        private void OnDetailCancel(NavigationCancelEvent evt)
        {
            if (detail.ClassListContains("recorder-hidden")) return;
            OnBackRequested();
            evt.StopPropagation();
        }
        private void ScrollSources(int direction)
        {
            if (disposed || !showingSources || detail.ClassListContains("recorder-hidden")) return;
            float step = Mathf.Max(100f, sources.contentViewport.layout.height * .65f);
            sources.scrollOffset = new Vector2(0f, Mathf.Clamp(sources.scrollOffset.y + direction * step,
                0f, Mathf.Max(0f, sources.verticalScroller.highValue)));
            UpdateSourceArrows();
        }
        private void OnSourceGeometryChanged(GeometryChangedEvent evt) => UpdateSourceArrows();
        private void OnSourceScrollChanged(float value) => UpdateSourceArrows();
        private void UpdateSourceArrows()
        {
            float bottom = sources.verticalScroller.highValue;
            bool overflowing = bottom > 1f;
            sourceUp.style.visibility = overflowing ? Visibility.Visible : Visibility.Hidden;
            sourceDown.style.visibility = overflowing ? Visibility.Visible : Visibility.Hidden;
            sourceUp.SetEnabled(overflowing && sources.scrollOffset.y > 1f);
            sourceDown.SetEnabled(overflowing && sources.scrollOffset.y < bottom - 1f);
        }

        private Button SourceTab(string text, string name, int index)
        {
            var button = Button(text, name, () =>
            {
                if (!disposed && hasReport && showingSources && !detail.ClassListContains("recorder-hidden"))
                    SelectSourceTab(index);
            });
            button.AddToClassList("recorder-source-tab");
            RegisterFeedback(button);
            return button;
        }

        private void SelectSourceTab(int index)
        {
            Show(nasaPage, index == 0); Show(artPage, index == 1); Show(assumptionsPage, index == 2);
            nasaTab.EnableInClassList("recorder-source-selected", index == 0);
            artTab.EnableInClassList("recorder-source-selected", index == 1);
            assumptionsTab.EnableInClassList("recorder-source-selected", index == 2);
            sources.scrollOffset = Vector2.zero;
        }

        private void BuildSources(IMissionReport report)
        {
            nasaPage.Add(FlightRecorderElements.Text("NASA / JPL HORIZONS", "recorder-source-heading"));
            nasaPage.Add(FlightRecorderElements.Text("The distance behind the delay.", "recorder-source-deck"));
            nasaPage.Add(FlightRecorderElements.Text("Earth–Mars reference data sets the minimum time for a signal to reach Earth and return.", "recorder-source-description"));
            var dataset = new Foldout { name = "reportDatasetDetails", text = "View dataset details", value = false };
            dataset.AddToClassList("recorder-source-disclosure");
            assumptionsPage.Add(FlightRecorderElements.Text("WHAT THE GAME SIMPLIFIES", "recorder-source-heading"));
            assumptionsPage.Add(FlightRecorderElements.Text("One fictional mission. Explicit assumptions.", "recorder-source-deck"));
            assumptionsPage.Add(FlightRecorderElements.Text("The ship, damage, recovery and compressed timing belong to the scenario. The complete recorded assumptions are below.", "recorder-source-description"));
            bool primaryLinkAdded = false;
            if (report.Sections != null)
                for (int i = 0; i < report.Sections.Count; i++)
                {
                    MissionReportSection section = report.Sections[i];
                    if (FlightRecorderElements.Classification(section.Id) == null) continue;
                    var facts = FlightRecorderElements.Section(section, i + 1, OnReportSourceRequested);
                    if (section.Id == "nasa-data" || section.Id == "derived-physics")
                    {
                        dataset.Add(facts);
                        if (section.Id == "nasa-data" && !primaryLinkAdded)
                            foreach (MissionReportLine line in section.Lines)
                                if (line.Label == "NASA-01" && FlightRecorderElements.TrySourceUrl(line.Value, out string url))
                                {
                                    nasaPage.Add(SourceLink("Open NASA source", url, false));
                                    primaryLinkAdded = true;
                                    break;
                                }
                    }
                    else assumptionsPage.Add(facts);
                }
            nasaPage.Add(dataset);
            nasaPage.Add(FlightRecorderElements.Text("AURORA-7 is fictional. This is a distance reference, not its trajectory.", "recorder-source-note"));
            artPage.Add(FlightRecorderElements.Text("MUSIC & ART", "recorder-source-heading"));
            artPage.Add(FlightRecorderElements.Text("Cobalt — Scott Buckley", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Exploration music · Creative Commons Attribution 4.0. Edited loop with level adjustment and an eight-second crossfade.", "recorder-source-description"));
            artPage.Add(SourceLink("Cobalt · original track", "https://www.scottbuckley.com.au/library/cobalt/", true));
            artPage.Add(FlightRecorderElements.Text("Titan — Scott Buckley", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Opening music · Creative Commons Attribution 4.0. Excerpt 48–132 s with added original percussion, gain staging and mastering.", "recorder-source-description"));
            artPage.Add(SourceLink("Scott Buckley · original track", "https://www.scottbuckley.com.au/library/titan/", true));
            artPage.Add(SourceLink("Read the CC BY 4.0 license", "https://creativecommons.org/licenses/by/4.0/", true));
            // CLAUDE-INTEGRATION credits (claim #7 6896214560): texts from audio-opening (6896198045) and submit-pack-02 (credits §C).
            artPage.Add(FlightRecorderElements.Text("Launch radio — NASA", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Apollo 11 launch commentary (public domain). Two excerpts, radio band-limited; the mission name is omitted. No NASA endorsement is implied.", "recorder-source-description"));
            artPage.Add(SourceLink("NASA launch commentary · source", "https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3", true));
            artPage.Add(FlightRecorderElements.Text("Space sky — NASA/Goddard Space Flight Center Scientific Visualization Studio", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Deep Star Maps 2020; Gaia DR2: ESA/Gaia/DPAC. Unmodified; public-domain reuse per NASA SVS.", "recorder-source-description"));
            artPage.Add(SourceLink("NASA SVS · Deep Star Maps 2020", "https://svs.gsfc.nasa.gov/4851/", true));
            artPage.Add(FlightRecorderElements.Text("Synthetic voices — Google Gemini text-to-speech", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Voices Orus and Kore. AI-generated audio. Original score cues and sound effects synthesized by scripts written with AI coding assistants.", "recorder-source-description"));
            artPage.Add(marsCredits);
            artPage.Add(FlightRecorderElements.Text("Typography · Barlow Condensed Bold", "recorder-source-deck"));
            artPage.Add(FlightRecorderElements.Text("Copyright 2017 The Barlow Project Authors. Unmodified font distributed under the SIL Open Font License 1.1.", "recorder-source-description"));
            artPage.Add(SourceLink("Barlow Condensed · font and license", "https://github.com/google/fonts/blob/60824dce48f7dd28fe7d65559f2da1f6e04e585b/ofl/barlowcondensed/OFL.txt", true));
            SelectSourceTab(0);
        }

        private Button SourceLink(string caption, string url, bool attribution)
        {
            var button = Button(caption, "reportSourceLink", () =>
            {
                if (disposed || !hasReport || !showingSources || detail.ClassListContains("recorder-hidden")) return;
                if (attribution) Application.OpenURL(url);
                else OnReportSourceRequested(url);
            });
            button.tooltip = url;
            button.AddToClassList("recorder-source-link");
            RegisterFeedback(button);
            return button;
        }

        private void OpenDetail(bool sourceView)
        {
            StopRecordRoll();
            showingSources = sourceView;
            Show(endCard, false);
            Show(detail, true);
            Show(record, !sourceView);
            Show(sources, sourceView);
            Show(sourceLayout, sourceView);
            detail.EnableInClassList("recorder-sources-open", sourceView);
            Show(rollControls, !sourceView && finalRecord);
            detailTitle.text = sourceView ? string.Empty : "Mission record";
            backButton.text = sourceView ? "ESC   Back" : "Back";
            (sourceView ? sources : record).scrollOffset = Vector2.zero;
            backButton.Focus();
            if (!sourceView && finalRecord) StartRecordRoll();
        }

        private void ShowEndCard(bool restoreFocus)
        {
            StopRecordRoll();
            Show(detail, false);
            Show(record, false);
            Show(sources, false);
            Show(sourceLayout, false);
            Show(rollControls, false);
            Show(endCard, true);
            if (restoreFocus) (showingSources ? sourcesButton : recordButton).Focus();
        }

        private void StartRecordRoll()
        {
            rollPaused = reducedMotion;
            rollLastTime = Time.realtimeSinceStartupAsDouble;
            rollStartsAt = rollLastTime + 1.5d;
            rollPauseButton.text = rollPaused ? "Start scroll" : "Pause scroll";
            recordRoll = record.schedule.Execute(AdvanceRecordRoll).Every(16);
        }

        private void AdvanceRecordRoll()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double delta = Math.Min(.1d, Math.Max(0d, now - rollLastTime));
            rollLastTime = now;
            if (disposed || rollPaused || now < rollStartsAt || !hasReport || showingSources
                || Root.panel == null || Root.ClassListContains("recorder-hidden")
                || detail.ClassListContains("recorder-hidden")) return;
            float bottom = record.verticalScroller.highValue;
            if (float.IsNaN(bottom) || float.IsInfinity(bottom) || bottom <= 0f) return;
            float next = Mathf.Min(bottom, record.scrollOffset.y + (float)delta * 24f);
            record.scrollOffset = new Vector2(0f, next);
            if (next >= bottom - .5f)
            {
                PauseRecordRoll();
                rollPauseButton.text = "Replay scroll";
            }
        }

        private void ToggleRecordRoll()
        {
            if (disposed || !hasReport || showingSources || detail.ClassListContains("recorder-hidden")) return;
            if (!rollPaused) { PauseRecordRoll(); return; }
            if (record.scrollOffset.y >= record.verticalScroller.highValue - .5f)
                record.scrollOffset = Vector2.zero;
            rollPaused = false;
            rollLastTime = Time.realtimeSinceStartupAsDouble;
            rollStartsAt = rollLastTime + .3d;
            rollPauseButton.text = "Pause scroll";
        }

        private void PauseRecordRoll()
        {
            rollPaused = true;
            if (rollPauseButton != null) rollPauseButton.text = "Continue scroll";
        }

        private void StopRecordRoll()
        {
            recordRoll?.Pause();
            recordRoll = null;
            rollPaused = true;
        }

        private void OnRecordWheel(WheelEvent evt) => PauseRecordRoll();
        private void OnRecordPointerDown(PointerDownEvent evt) => PauseRecordRoll();

        private void OnReportSourceRequested(string url)
        {
            if (disposed || !hasReport || detail.ClassListContains("recorder-hidden")
                || !FlightRecorderElements.TrySourceUrl(url, out string approvedUrl)) return;
            PauseRecordRoll();
            Application.OpenURL(approvedUrl);
        }

        private void OnRestartRequested()
        {
            if (disposed || !canRestart || restartPending || RestartRequested == null) return;
            restartPending = true;
            restartButton.text = "Restart requested";
            restartButton.SetEnabled(false);
            try { RestartRequested.Invoke(); }
            catch { ResetRestartRequest(); throw; }
        }

        private static VisualElement Element(string name, string css)
        {
            var result = new VisualElement { name = name };
            result.AddToClassList(css);
            return result;
        }
        private static ScrollView Scroll(string name)
        {
            var result = new ScrollView(ScrollViewMode.Vertical) { name = name };
            result.AddToClassList("recorder-scroll");
            result.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            result.verticalScrollerVisibility = ScrollerVisibility.Auto;
            return result;
        }
        private Button Button(string text, string name, Action onClick)
        {
            Button button = null;
            button = new Button(() => { MenuFeedback(button, true); onClick(); }) { text = text, name = name };
            button.AddToClassList("recorder-button");
            return button;
        }
        private static void Show(VisualElement element, bool visible) => element.EnableInClassList("recorder-hidden", !visible);

        private sealed class ReportShade : VisualElement
        {
            public ReportShade()
            {
                name = "reportReadabilityShade";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.top = style.right = style.bottom = 0;
                generateVisualContent += Draw;
            }
            private void Draw(MeshGenerationContext context)
            {
                if (contentRect.width < 1 || contentRect.height < 1) return;
                const int strips = 24;
                MeshWriteData mesh = context.Allocate((strips + 1) * 2, strips * 6);
                for (int i = 0; i <= strips; i++)
                {
                    float t = i / (float)strips;
                    var tint = (Color32)new Color(.018f, .024f, .025f,
                        Mathf.Lerp(.90f, .10f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, .85f, t))));
                    float x = contentRect.width * t;
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x, 0, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x, contentRect.height, Vertex.nearZ), tint = tint });
                }
                for (ushort i = 0; i < strips; i++)
                {
                    ushort a = (ushort)(i * 2);
                    mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 2)); mesh.SetNextIndex((ushort)(a + 1));
                    mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + 2)); mesh.SetNextIndex((ushort)(a + 3));
                }
            }
        }
    }

    internal static class FlightRecorderEndCardCopy
    {
        public static bool CanRestart(IMissionReport report) => report != null && report.IsFinal &&
            (report.Outcome == MissionOutcome.Recovered || report.Outcome == MissionOutcome.RecoveryPossible || report.Outcome == MissionOutcome.Lost);

        public static string Headline(IMissionReport report)
        {
            if (report == null || !report.IsFinal) return "MISSION IN PROGRESS";
            switch (report.Outcome)
            {
                case MissionOutcome.Recovered: return "SURVIVED";
                case MissionOutcome.RecoveryPossible: return "CONTACT RESTORED";
                case MissionOutcome.Lost: return IsRecordedCrewDeath(report) ? "YOU DIED" : "MISSION LOST";
                default: return "MISSION ENDED";
            }
        }

        public static string Summary(IMissionReport report)
        {
            if (report == null || !report.IsFinal) return "The mission is still in progress.";
            if (report.Outcome == MissionOutcome.RecoveryPossible)
                return "Contact is restored. Rescue has not arrived.";
            if (report.Outcome == MissionOutcome.Recovered)
            {
                // Recovered is the actual crew-recovery outcome; Mars remains a destination,
                // not a fabricated claim that an interplanetary transfer took no time.
                return "Rescue reached AURORA-7.";
            }
            if (report.Outcome == MissionOutcome.Lost)
            {
                string cause = OutcomeLine(report, "Cause");
                if (cause.StartsWith("Unmet water demand", StringComparison.Ordinal)) return "You went too long without water.";
                if (cause.StartsWith("Unmet food demand", StringComparison.Ordinal)) return "You went too long without food.";
                if (cause.StartsWith("Suit oxygen:", StringComparison.Ordinal)) return "Your suit ran out of oxygen.";
                if (cause.StartsWith("Oxygen:", StringComparison.Ordinal)) return "The cabin ran out of breathable air.";
                if (cause.StartsWith("Carbon dioxide:", StringComparison.Ordinal)) return "Carbon dioxide overwhelmed the cabin.";
                if (cause.StartsWith("Water:", StringComparison.Ordinal)) return "Your water reserves ran out.";
                if (cause.StartsWith("Food:", StringComparison.Ordinal)) return "Your food reserves ran out.";
                if (cause.StartsWith("Unrecoverable mission objective:", StringComparison.Ordinal))
                    return "Not enough spare parts to restore contact. The crew is still alive.";
                if (!string.IsNullOrWhiteSpace(cause) && cause.Length <= 240) return cause;
                if (!string.IsNullOrWhiteSpace(report.OutcomeReason) && report.OutcomeReason.Length <= 240)
                    return report.OutcomeReason;
                return "The mission could not continue. Its recorded cause is in Mission record.";
            }
            return "The mission record contains the recorded outcome.";
        }

        private static bool IsRecordedCrewDeath(IMissionReport report)
        {
            string cause = OutcomeLine(report, "Cause");
            return cause.StartsWith("Unmet water demand", StringComparison.Ordinal)
                || cause.StartsWith("Unmet food demand", StringComparison.Ordinal)
                || cause.StartsWith("Suit oxygen:", StringComparison.Ordinal)
                || cause.StartsWith("Oxygen:", StringComparison.Ordinal)
                || cause.StartsWith("Carbon dioxide:", StringComparison.Ordinal)
                || cause.StartsWith("Water:", StringComparison.Ordinal)
                || cause.StartsWith("Food:", StringComparison.Ordinal);
        }

        private static string OutcomeLine(IMissionReport report, string label)
        {
            if (report.Sections != null)
                foreach (MissionReportSection section in report.Sections)
                    if (string.Equals(section.Id, "outcome", StringComparison.Ordinal))
                        foreach (MissionReportLine line in section.Lines)
                            if (string.Equals(line.Label, label, StringComparison.Ordinal)) return line.Value ?? string.Empty;
            return string.Empty;
        }
    }
}

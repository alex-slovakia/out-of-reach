using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.UI.Chapter
{
    public enum ChapterCueKind { StopReport, BeatCard, HopeMessage, FailureDispatch, RescueCommitted, BeaconDelayed }

    /// <summary>A one-shot presentation cue with the values captured at the moment Core committed the change.</summary>
    public sealed class ChapterCue
    {
        public ChapterCueKind Kind { get; internal set; }
        public MissionChapter Chapter { get; internal set; }
        /// <summary>Mission day at the observed change (Core's day rule).</summary>
        public int Day { get; internal set; }
        /// <summary>Simulation time at which the change was observed.</summary>
        public double ObservedAt { get; internal set; }
        public RoutineStopReason Stop { get; internal set; }
        public string StopText { get; internal set; } = string.Empty;
        public double? RoutineStartedAt { get; internal set; }
        /// <summary>Core's actual committed stop time of the segment (never the target).</summary>
        public double? RoutineStoppedAt { get; internal set; }
        public int Rations { get; internal set; }
        public int Drinks { get; internal set; }
        public int Rests { get; internal set; }
        /// <summary>A beat card that closes a routine which reached it carries that routine's summary.</summary>
        public bool HasRoutineSummary { get; internal set; }
        public string DispatchId { get; internal set; }
        public string ContentId { get; internal set; }
        public double TransmittedAt { get; internal set; }
        public double ReceivedAt { get; internal set; }
        public int? ReportDay { get; internal set; }
        public int? DockDay { get; internal set; }
    }

    /// <summary>
    /// Turns committed Core state into one-shot cues, read once per presentation frame. It keeps no clock and makes no
    /// decision: it only compares what Core reports now with what it reported last time. Beat cards are shown once per
    /// chapter, a dispatch once per stable <see cref="MissionDispatch.Id"/> (never by Revision), a routine report once per
    /// segment. Binding to a session that is already under way replays nothing except the opening beat of a fresh mission.
    /// </summary>
    public sealed class ChapterCueObserver
    {
        private readonly List<ChapterCue> queue = new List<ChapterCue>();
        private readonly HashSet<MissionChapter> beatsShown = new HashSet<MissionChapter>();
        private readonly HashSet<string> dispatchesShown = new HashSet<string>(StringComparer.Ordinal);
        private bool initialized, ended;
        private MissionChapter lastChapter;
        private RescuePlan lastRescue;
        private RecoveryStage lastStage;
        private double? lastSegmentStart, lastSegmentStop;

        public IReadOnlyList<ChapterCue> Pending => queue;
        public bool Initialized => initialized;
        public bool Ended => ended;

        public void Reset()
        {
            queue.Clear();
            beatsShown.Clear();
            dispatchesShown.Clear();
            initialized = ended = false;
            lastChapter = MissionChapter.None;
            lastRescue = RescuePlan.None;
            lastStage = RecoveryStage.NotStarted;
            lastSegmentStart = lastSegmentStop = null;
        }

        /// <summary>The next cue to present, or null.</summary>
        public ChapterCue Peek() => queue.Count > 0 ? queue[0] : null;

        public ChapterCue Dequeue()
        {
            if (queue.Count == 0) return null;
            ChapterCue first = queue[0];
            queue.RemoveAt(0);
            return first;
        }

        /// <param name="active">The chapter loop applies now: flow enabled, survival active, mission not decided, flight phase.</param>
        /// <param name="ended">The mission reached an outcome: clears everything that is still queued, for good.</param>
        public void Observe(IMissionChapterFlow flow, bool active, bool missionEnded, double now,
            RecoveryStage stage, double? arrivalAt)
        {
            if (flow == null || !flow.Enabled)
            {
                Reset();
                return;
            }

            if (missionEnded)
            {
                ended = true;
                queue.Clear();
                return;
            }

            if (!active || ended)
            {
                return;
            }

            if (!initialized)
            {
                initialized = true;
                Remember(flow, stage);
                beatsShown.Add(flow.Chapter);
                if (!string.IsNullOrEmpty(flow.LatestDispatch.Id)) dispatchesShown.Add(flow.LatestDispatch.Id);
                if (flow.Chapter == MissionChapter.Accident && !flow.RoutineStartedAtSimulationSeconds.HasValue)
                {
                    queue.Add(new ChapterCue
                    {
                        Kind = ChapterCueKind.BeatCard, Chapter = MissionChapter.Accident,
                        Day = ChapterCopy.DayAt(now), ObservedAt = now,
                    });
                }

                return;
            }

            ChapterCue stopReport = null;
            if (!flow.IsRoutineRunning && flow.RoutineStoppedAtSimulationSeconds.HasValue
                && (flow.RoutineStartedAtSimulationSeconds != lastSegmentStart || flow.RoutineStoppedAtSimulationSeconds != lastSegmentStop))
            {
                stopReport = new ChapterCue
                {
                    Kind = ChapterCueKind.StopReport, Chapter = flow.Chapter, ObservedAt = now,
                    Day = ChapterCopy.DayAt(flow.RoutineStoppedAtSimulationSeconds.Value),
                    Stop = flow.LastRoutineStop, StopText = flow.LastRoutineStopText ?? string.Empty,
                    RoutineStartedAt = flow.RoutineStartedAtSimulationSeconds,
                    RoutineStoppedAt = flow.RoutineStoppedAtSimulationSeconds,
                    Rations = flow.RoutineRations, Drinks = flow.RoutineDrinks, Rests = flow.RoutineRests,
                };
            }

            ChapterCue beat = null;
            if (flow.Chapter != lastChapter && flow.Chapter != MissionChapter.None && !beatsShown.Contains(flow.Chapter))
            {
                beatsShown.Add(flow.Chapter);
                beat = new ChapterCue
                {
                    Kind = ChapterCueKind.BeatCard, Chapter = flow.Chapter, Day = ChapterCopy.DayAt(now), ObservedAt = now,
                };
                if (stopReport != null && stopReport.Stop == RoutineStopReason.ReachedChapter)
                {
                    // One card, not two: the beat closes the routine that reached it and carries its real summary.
                    beat.HasRoutineSummary = true;
                    beat.RoutineStartedAt = stopReport.RoutineStartedAt;
                    beat.RoutineStoppedAt = stopReport.RoutineStoppedAt;
                    beat.Rations = stopReport.Rations;
                    beat.Drinks = stopReport.Drinks;
                    beat.Rests = stopReport.Rests;
                    beat.Stop = stopReport.Stop;
                    stopReport = null;
                }
            }

            if (stopReport != null && stopReport.Stop != RoutineStopReason.MissionEnded) queue.Add(stopReport);
            if (beat != null) queue.Add(beat);

            if (flow.Rescue != lastRescue)
            {
                if (flow.Rescue == RescuePlan.FirstRescuePlanned)
                {
                    int? reportDay = flow.NextChapter == MissionChapter.RescueFailed && flow.NextChapterAtSimulationSeconds.HasValue
                        ? ChapterCopy.DayAt(flow.NextChapterAtSimulationSeconds.Value)
                        : ChapterCopy.DayAt(now) < 14 ? 14 : (int?)null;
                    queue.Add(new ChapterCue
                    {
                        Kind = ChapterCueKind.HopeMessage, Chapter = flow.Chapter, Day = ChapterCopy.DayAt(now), ObservedAt = now,
                        ReportDay = reportDay,
                    });
                }
                else if (flow.Rescue == RescuePlan.InTransitCraftCommitted)
                {
                    queue.Add(new ChapterCue
                    {
                        Kind = ChapterCueKind.RescueCommitted, Chapter = flow.Chapter, Day = ChapterCopy.DayAt(now), ObservedAt = now,
                        DockDay = arrivalAt.HasValue && ChapterCopy.Finite(arrivalAt.Value) ? ChapterCopy.DayAt(arrivalAt.Value) : (int?)null,
                    });
                }
            }

            MissionDispatch dispatch = flow.LatestDispatch;
            if (!string.IsNullOrEmpty(dispatch.Id) && dispatchesShown.Add(dispatch.Id))
            {
                queue.Add(new ChapterCue
                {
                    Kind = ChapterCueKind.FailureDispatch, Chapter = flow.Chapter, ObservedAt = now,
                    Day = ChapterCopy.DayAt(dispatch.ReceivedAtSimulationSeconds),
                    DispatchId = dispatch.Id, ContentId = dispatch.ContentId,
                    TransmittedAt = dispatch.TransmittedAtSimulationSeconds, ReceivedAt = dispatch.ReceivedAtSimulationSeconds,
                });
            }

            if (stage == RecoveryStage.Delayed && lastStage != RecoveryStage.Delayed)
            {
                queue.Add(new ChapterCue
                {
                    Kind = ChapterCueKind.BeaconDelayed, Chapter = flow.Chapter, Day = ChapterCopy.DayAt(now), ObservedAt = now,
                    DockDay = arrivalAt.HasValue && ChapterCopy.Finite(arrivalAt.Value) ? ChapterCopy.DayAt(arrivalAt.Value) : (int?)null,
                });
            }

            Remember(flow, stage);
        }

        /// <summary>A routine that starts again supersedes any older report or card still waiting in the queue.</summary>
        public void DropStaleReports()
        {
            queue.RemoveAll(cue => cue.Kind == ChapterCueKind.StopReport);
        }

        private void Remember(IMissionChapterFlow flow, RecoveryStage stage)
        {
            lastChapter = flow.Chapter;
            lastRescue = flow.Rescue;
            lastStage = stage;
            lastSegmentStart = flow.RoutineStartedAtSimulationSeconds;
            lastSegmentStop = flow.RoutineStoppedAtSimulationSeconds;
        }
    }
}

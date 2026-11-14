using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>The debrief: what it must contain for both outcomes, and that a restart starts from nothing.</summary>
    public sealed class MissionReportTests
    {
        private static string Value(MissionReport report, string sectionId, string label)
        {
            Assert.That(report.TryGetSection(sectionId, out MissionReportSection section), Is.True, "section " + sectionId);
            foreach (MissionReportLine line in section.Lines)
            {
                if (line.Label == label)
                {
                    return line.Value;
                }
            }

            Assert.Fail("No line '" + label + "' in section '" + sectionId + "'.");
            return null;
        }

        private static List<string> SectionIds(MissionReport report)
        {
            var ids = new List<string>();
            foreach (MissionReportSection section in report.Sections)
            {
                ids.Add(section.Id);
            }

            return ids;
        }

        [Test]
        public void Report_IsNullWhileTheMissionRuns_AndAPreviewIsNotFinal()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Assert.That(session.Report, Is.Null);

                MissionReport preview = session.BuildReport();

                Assert.That(preview.IsFinal, Is.False);
                Assert.That(preview.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(preview.Headline, Is.EqualTo("MISSION IN PROGRESS"));
                Assert.That(SectionIds(preview), Is.EqualTo(MissionReportSections.Ordered));
                Assert.That(Value(preview, MissionReportSections.Science, "Science return"), Does.StartWith("Pending"));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair), "building a report changes nothing");
            }

            using (var planning = Fixtures.NewSession())
            {
                Assert.That(planning.BuildReport().IsFinal, Is.False, "a report can even be previewed before launch");
            }
        }

        [Test]
        public void RecoveryReport_CarriesTheDatasetIdentity_TheTiming_AndTheOutcome()
        {
            using (var session = Fixtures.RecoveredSession(AuroraMission.ScienceFocusPresetId))
            {
                MissionReport report = session.Report;

                Assert.That(report, Is.Not.Null);
                Assert.That(session.Report, Is.SameAs(report), "one immutable snapshot");
                Assert.That(report.IsFinal, Is.True);
                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(report.Headline, Is.EqualTo("RECOVERY POSSIBLE"));
                Assert.That(report.OutcomeReason, Is.EqualTo(session.State.OutcomeReason));
                Assert.That(report.ElapsedSimulationSeconds, Is.EqualTo(session.State.ElapsedSimulationSeconds));
                Assert.That(report.MissionId, Is.EqualTo("AURORA-7"));
                Assert.That(report.MissionDayNumber, Is.EqualTo(1));
                Assert.That(report.LoadoutName, Is.EqualTo(AuroraMission.ScienceFocusPresetId));
                Assert.That(report.LossCause, Is.EqualTo(SurvivalFailure.None));
                Assert.That(SectionIds(report), Is.EqualTo(MissionReportSections.Ordered));

                ReportCommunication c = report.Communication;
                Assert.That(c.DatasetId, Is.EqualTo(EarthMarsReferenceTests.DatasetId));
                Assert.That(c.DatasetSha256, Is.EqualTo(EarthMarsReferenceTests.ManifestSha256));
                Assert.That(c.ReferenceSampleIndex, Is.EqualTo(0));
                Assert.That(c.ReferenceDistanceKm, Is.EqualTo(197707975.990852982d).Within(1e-6));
                Assert.That(c.OneWayLightSeconds, Is.EqualTo(659.482821248d).Within(1e-9));
                Assert.That(c.MinimumRoundTripSeconds, Is.EqualTo(1318.965642497d).Within(2e-9));
                Assert.That(c.EarthProcessingSeconds, Is.EqualTo(600d));
                Assert.That(c.EarliestReplySeconds, Is.EqualTo(1918.965642496d).Within(1e-6));
                Assert.That(c.HasTransmitted, Is.True);
                Assert.That(c.ReplyReceived, Is.True);
                Assert.That(c.TransmittedAtSimulationSeconds, Is.EqualTo(9600d));
                Assert.That(c.TransmittedOnMissionDay, Is.EqualTo(1));
                Assert.That(c.ReplyWaitSimulationSeconds.Value, Is.EqualTo(c.EarliestReplySeconds).Within(1e-6));
                Assert.That(c.TransmissionTimeScale, Is.EqualTo(60d));

                Assert.That(report.ScienceDataReturned, Is.True);
                Assert.That(report.FalseSignalReplayed, Is.True);
                Assert.That(report.Holding.IsBounded, Is.True);
                Assert.That(report.Holding.LimitedBy, Is.EqualTo("food"));
                Assert.That(report.Holding.Days, Is.InRange(30d, 36d));

                IMissionReport contract = report;
                Assert.That(contract.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(contract.IsFinal, Is.True);
                Assert.That(contract.Headline, Is.EqualTo("RECOVERY POSSIBLE"));
                Assert.That(contract.Sections, Has.Count.EqualTo(MissionReportSections.Ordered.Count));
                foreach (MissionReportSection section in contract.Sections)
                {
                    Assert.That(section.Title, Is.Not.Empty, section.Id);
                    Assert.That(section.Lines, Is.Not.Empty, section.Id);
                }

                string text = report.ToPlainText();
                Assert.That(text, Does.StartWith("RECOVERY POSSIBLE"));
                Assert.That(text, Does.Contain(EarthMarsReferenceTests.ManifestSha256));
                Assert.That(text, Does.Contain("NASA/JPL Horizons, dataset " + EarthMarsReferenceTests.DatasetId));
                Assert.That(text, Does.Contain("197,708,000 km").And.Contain("10 min 59 s (659.483 s)").And.Contain("31 min 59 s = 2 x light time + processing"));
                Assert.That(text, Does.Contain("old local recording"));
                Assert.That(text, Does.Contain("A one-person EVA is a fictional emergency"));
                Assert.That(text, Does.Contain("NASA-01").And.Contain("NASA-02").And.Contain("NASA-03"));
                Assert.That(text, Does.Contain("At x60 the 31 min 59 s wait takes about 32 real seconds."));
                Assert.That(Value(report, MissionReportSections.Outcome, "What it means"), Does.Contain("not a rescue"));
                Assert.That(Value(report, MissionReportSections.Science, "Science return"), Does.StartWith("Returned"));
            }
        }

        [Test]
        public void Expenditures_AddUpToWhatLeftTheReserves()
        {
            using (var session = Fixtures.RecoveredSession(AuroraMission.ScienceFocusPresetId))
            {
                MissionReport report = session.Report;
                var byPurpose = new Dictionary<string, ReportExpenditure>();
                double oxygen = 0d;
                double water = 0d;
                double food = 0d;
                double battery = 0d;
                int parts = 0;
                foreach (ReportExpenditure e in report.Expenditures)
                {
                    byPurpose.Add(e.Purpose, e);
                    oxygen += e.OxygenKg;
                    water += e.WaterLitres;
                    food += e.FoodKg;
                    battery += e.BatteryKWh;
                    parts += e.SpareParts;
                }

                ResourceSnapshot start = report.ResourcesAtLaunch;
                ResourceSnapshot end = report.ResourcesAtEnd;
                Assert.That(oxygen, Is.EqualTo(start.OxygenKg - end.OxygenKg).Within(1e-9));
                Assert.That(water, Is.EqualTo(start.WaterLitres - end.WaterLitres).Within(1e-9));
                Assert.That(food, Is.EqualTo(start.FoodKg - end.FoodKg).Within(1e-9));
                Assert.That(battery, Is.EqualTo(start.BatteryKWh - end.BatteryKWh).Within(1e-9));
                Assert.That(parts, Is.EqualTo(start.SparePartsCount - end.SparePartsCount));

                Assert.That(byPurpose["repairs"].SpareParts, Is.EqualTo(7));
                Assert.That(byPurpose["repairs"].BatteryKWh, Is.EqualTo(1.5d).Within(1e-12));
                Assert.That(byPurpose["EVA"].OxygenKg, Is.EqualTo(0.84d + session.Eva.Spec.AirlockCycleOxygenLossKg).Within(1e-12));
                Assert.That(byPurpose["EVA"].BatteryKWh, Is.EqualTo(0.2d).Within(1e-12));
                Assert.That(byPurpose["transmission"].BatteryKWh, Is.EqualTo(0.5d));
                Assert.That(byPurpose["over time"].OxygenKg, Is.GreaterThan(0d));
                Assert.That(byPurpose["over time"].SpareParts, Is.EqualTo(0));
                Assert.That(end.SparePartsCount, Is.EqualTo(0), "the science-focus loadout carries exactly the seven parts");
                Assert.That(report.CompletedEvaCount, Is.EqualTo(1));
                Assert.That(report.SuitPreparationCount, Is.EqualTo(1));
                Assert.That(report.AirlockCycleCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void Systems_AndTimeline_RecordWhatHappenedAndWhen()
        {
            using (var session = Fixtures.RecoveredSession(AuroraMission.ScienceFocusPresetId))
            {
                MissionReport report = session.Report;

                Assert.That(report.Systems, Has.Count.EqualTo(3));
                foreach (ReportSystemStatus system in report.Systems)
                {
                    Assert.That(system.WasDamaged, Is.True, system.DisplayName);
                    Assert.That(system.IsOperational, Is.True, system.DisplayName);
                    Assert.That(system.RepairedAtSimulationSeconds, Is.EqualTo(system.SystemId == ShipSystemId.Antenna ? 9000d : 0d), system.DisplayName);
                }

                Assert.That(Value(report, MissionReportSections.Systems, "High-gain antenna"),
                    Is.EqualTo("0 % after the impact; repaired at T+2 h 30 min for 3 spare parts, 0.5 kWh during the EVA."));

                IReadOnlyList<MissionLogEntry> timeline = report.Timeline;
                Assert.That(timeline[0].Text, Does.StartWith("Launch on 2026-11-14 08:00 UTC"));
                Assert.That(timeline[timeline.Count - 1].Text, Is.EqualTo("Mission outcome: recovery possible."));
                for (int i = 1; i < timeline.Count; i++)
                {
                    Assert.That(timeline[i].AtSimulationSeconds, Is.GreaterThanOrEqualTo(timeline[i - 1].AtSimulationSeconds), "ordered by time");
                }

                MissionLogEntry sent = default(MissionLogEntry);
                MissionLogEntry reply = default(MissionLogEntry);
                foreach (MissionLogEntry entry in timeline)
                {
                    if (entry.Text.StartsWith("Distress call transmitted on mission day 1."))
                    {
                        sent = entry;
                    }
                    else if (entry.Text == "Reply from Earth received.")
                    {
                        reply = entry;
                    }
                }

                Assert.That(sent.AtSimulationSeconds, Is.EqualTo(9600d));
                Assert.That(sent.Text, Does.Contain("Reference sample 2026-11-14 08:00 UTC").And.Contain("197,708,000 km"));
                Assert.That(reply.AtSimulationSeconds, Is.EqualTo(9600d + report.Communication.EarliestReplySeconds).Within(1e-6));
                Assert.That(report.JumpedSimulationSeconds, Is.EqualTo(report.ElapsedSimulationSeconds).Within(1e-6), "this fixture never ticks frames");
                Assert.That(Value(report, MissionReportSections.TimeCompression, "Chapter jumps"), Does.Contain("each suit preparation is 2 h 20 min 00 s"));
            }
        }

        [Test]
        public void Loadouts_AreNamed_AndScienceReturnFollowsThePayload()
        {
            using (var redundancy = Fixtures.RecoveredSession(AuroraMission.RedundancyPresetId))
            {
                MissionReport report = redundancy.Report;
                Assert.That(report.LoadoutName, Is.EqualTo(AuroraMission.RedundancyPresetId));
                Assert.That(report.ScienceDataReturned, Is.False, "contact was restored, but there was nothing to return");
                Assert.That(Value(report, MissionReportSections.Science, "Objective"), Does.StartWith("No science payload was carried"));
                Assert.That(report.ResourcesAtEnd.SparePartsCount, Is.EqualTo(6), "the margin the science payload would have cost");
            }

            using (var custom = Fixtures.NewSession())
            {
                Assert.That(custom.Planner.TrySetEquipment(AuroraMission.OxygenTank, 3).Succeeded, Is.True);
                Assert.That(custom.Planner.TryLaunch().Succeeded, Is.True);
                MissionReport report = custom.BuildReport();
                Assert.That(report.LoadoutName, Is.EqualTo("custom"));
                Assert.That(Value(report, MissionReportSections.MissionDesign, "Loadout"), Is.EqualTo("Custom loadout"));
                Assert.That(Value(report, MissionReportSections.MissionDesign, "3 x Oxygen tank"), Is.EqualTo("90 kg, 120 credits"));
                Assert.That(Value(report, MissionReportSections.MissionDesign, "Planning"), Is.EqualTo("Launched by the crew; planning was not timed."));
            }
        }

        [Test]
        public void LossReport_ExplainsTheCause_AndThatNoCallWasSent()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.AdvanceSimulation(100d * 3600d).Succeeded, Is.True);

                MissionReport report = session.Report;

                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(report.Headline, Is.EqualTo("MISSION LOST"));
                Assert.That(report.LossCause, Is.EqualTo(SurvivalFailure.CarbonDioxideLimit));
                Assert.That(report.OutcomeReason, Does.Contain("Life support was not repaired"));
                Assert.That(report.Communication.HasTransmitted, Is.False);
                Assert.That(report.Communication.State, Is.EqualTo(CommunicationState.Offline));
                Assert.That(report.ScienceDataReturned, Is.False);
                Assert.That(report.FalseSignalReplayed, Is.False);
                Assert.That(report.PeakCarbonDioxidePpm, Is.EqualTo(SurvivalRules.CarbonDioxideLossLimitPpm).Within(1e-3));
                Assert.That(Value(report, MissionReportSections.Communication, "Transmission"), Is.EqualTo("No distress call was sent."));
                Assert.That(Value(report, MissionReportSections.Outcome, "Cause"), Does.StartWith("Carbon dioxide"));
                Assert.That(Value(report, MissionReportSections.Systems, "Life support"), Does.Contain("never repaired"));
                Assert.That(Value(report, MissionReportSections.Science, "Science return"), Does.StartWith("Not returned"));
                Assert.That(Value(report, MissionReportSections.Systems, "EVA"), Is.EqualTo("No EVA was attempted."));
            }
        }

        [Test]
        public void ALossWhileWaiting_ReportsTheReplyThatCouldNotArrive()
        {
            using (var session = Fixtures.TransmissionSession())
            {
                Fixtures.Transmit(session);
                double keep = session.Resources.Rates.WaterUseLitresPerSecond * (session.Communications.ReplyDelaySimulationSeconds - 100d);
                Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.WaterLitres, session.Resources.Snapshot.WaterLitres - keep) }).Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(1e6).Succeeded, Is.True);

                MissionReport report = session.Report;

                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(report.Communication.HasTransmitted, Is.True);
                Assert.That(report.Communication.ReplyReceived, Is.False);
                Assert.That(report.Communication.ReplyRemainingSimulationSeconds, Is.EqualTo(100d).Within(1e-3));
                Assert.That(Value(report, MissionReportSections.Communication, "Reply"), Is.EqualTo("Not received: it could not arrive for another 1 min 40 s."));
                Assert.That(report.ScienceDataReturned, Is.False);
            }
        }

        [Test]
        public void AnUnrecoverableObjective_IsReportedAsSuch_NotAsADeath()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 1).Succeeded, Is.True);
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Fixtures.TriggerImpact(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);

                MissionReport report = session.Report;

                Assert.That(report.LossCause, Is.EqualTo(SurvivalFailure.ContactImpossible));
                Assert.That(report.LoadoutName, Is.EqualTo("custom"));
                Assert.That(Value(report, MissionReportSections.Outcome, "Cause"), Does.StartWith("Unrecoverable mission objective"));
                Assert.That(Value(report, MissionReportSections.Outcome, "Cause"), Does.Contain("The crew was alive"));
                Assert.That(Value(report, MissionReportSections.MissionDesign, "Spare parts at launch"), Is.EqualTo("4; the three repairs need 7"));
                Assert.That(Value(report, MissionReportSections.Systems, "High-gain antenna"), Does.Contain("never repaired (needs 3 spare parts, 0.5 kWh and an EVA)"));
            }
        }

        [Test]
        public void TheTimedPlanningFallback_IsDisclosed()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.PlanningDeadline.TryEnable(30d).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 0).Succeeded, Is.True, "invalid: no power for the spectrometer");
                session.Tick(30d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));

                MissionReport report = session.BuildReport();

                Assert.That(report.UsedLastValidLoadoutAtDeadline, Is.True);
                Assert.That(Value(report, MissionReportSections.MissionDesign, "Planning"),
                    Is.EqualTo("Launched automatically at the planning deadline with the last valid loadout."));
                Assert.That(report.LoadoutName, Is.EqualTo(AuroraMission.ScienceFocusPresetId), "the last valid loadout was the starting one");
            }
        }

        [Test]
        public void RealPlayTime_AndChapterJumps_AreDisclosedSeparately()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                Assert.That(session.Tick(2d).Succeeded, Is.True);
                Assert.That(session.AdvanceSimulation(100d).Succeeded, Is.True);

                MissionReport report = session.BuildReport();

                Assert.That(report.PlayedRealSeconds, Is.EqualTo(2d));
                Assert.That(report.JumpedSimulationSeconds, Is.EqualTo(100d).Within(1e-9));
                Assert.That(report.ElapsedSimulationSeconds, Is.EqualTo(102d).Within(1e-9), "the cockpit check runs in real time");
                Assert.That(Value(report, MissionReportSections.TimeCompression, "Chapter jumps"), Does.StartWith("1 min 40 s of simulated time passed in jumps."));
                Assert.That(Value(report, MissionReportSections.TimeCompression, "Time scales"),
                    Is.EqualTo("Cockpit check x1; emergency and repair x300; EVA x60; transmission x60 (simulated seconds per real second)."));
            }
        }

        [Test]
        public void Restart_LeavesNothingBehind_AndTheOldEndingCannotLeakIn()
        {
            var first = Fixtures.RecoveredSession();
            Fixtures.FinishCinematics(first);
            Assert.That(first.State.Phase, Is.EqualTo(GamePhase.Report));
            Assert.That(first.Report, Is.Not.Null);
            int oldSequence = first.Cinematics.ActiveSequenceId;
            first.Dispose();
            Assert.That(first.Communications.TryTransmit().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "the old link stays as it ended");
            Assert.That(first.Director.TryRunLinkDiagnostic().Failure, Is.EqualTo(CommandFailure.WrongPhase), "a disposed session rejects commands");

            using (var second = Fixtures.NewSession())
            {
                Assert.That(second.Report, Is.Null);
                Assert.That(second.Communications.State, Is.EqualTo(CommunicationState.Ready));
                Assert.That(second.Communications.HasTransmitted, Is.False);
                Assert.That(second.Communications.ReplyReceivedAtSimulationSeconds, Is.Null);
                Assert.That(second.Director.LinkDiagnosticRequested, Is.False);
                Assert.That(second.Director.Journal.Count, Is.EqualTo(0));
                Assert.That(second.PlayedRealSeconds, Is.EqualTo(0d));
                Assert.That(second.JumpedSimulationSeconds, Is.EqualTo(0d));
                Assert.That(second.Calendar.DayNumber, Is.EqualTo(1));

                second.Events.Publish(new CinematicFinished(CinematicId.EndingPullback, oldSequence, false));
                Assert.That(second.Cinematics.IgnoredAcknowledgements, Is.EqualTo(1), "a completion from the old mission is stale");
                Assert.That(second.State.Phase, Is.EqualTo(GamePhase.Planning));

                Fixtures.Stabilize(second);
                Fixtures.CompleteEva(second);
                Fixtures.Transmit(second);
                Assert.That(second.Communications.HasTransmitted, Is.True, "the single transmission belongs to the mission, not to the process");
                Assert.That(second.Resources.Snapshot.SparePartsCount, Is.EqualTo(0));
            }
        }
    }
}

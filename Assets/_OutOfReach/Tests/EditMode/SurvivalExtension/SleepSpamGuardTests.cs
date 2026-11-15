using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// R22: the berth cannot be spammed in the chapter loop. Every press goes through the session's real
    /// survival-sleep interaction; a refusal says why and changes nothing (time, rests, stores, chapter); a valid
    /// rest still advances the one clock. The beats and the routine's pacing are unchanged.
    /// </summary>
    public sealed class SleepSpamGuardTests
    {
        private const double Hour = 3600d;
        private const double Frame = 0.1d;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static MissionSession Accident()
        {
            var session = new MissionSession(survivalExtension: true, chapterLoop: true);
            session.EnableFieldModifications(true, false);
            Require(session.Planner.TryLaunch());
            Fixtures.TriggerImpact(session);
            return session;
        }

        private static IInteractable Berth(MissionSession session)
        {
            Assert.That(session.Interactions.TryGet(InteractionIds.SurvivalSleep, out IInteractable berth), Is.True);
            return berth;
        }

        private sealed class Snapshot
        {
            public double Time;
            public int Rests;
            public int CompletedRests;
            public bool Resting;
            public MissionChapter Chapter;
            public ResourceSnapshot Stores;
        }

        private static Snapshot Take(MissionSession session)
        {
            return new Snapshot
            {
                Time = session.State.ElapsedSimulationSeconds,
                Rests = session.SurvivalRecovery.RestCount,
                CompletedRests = session.SurvivalRecovery.CompletedRestCount,
                Resting = session.SurvivalRecovery.IsResting,
                Chapter = session.ChapterFlow.Chapter,
                Stores = session.Resources.Snapshot,
            };
        }

        /// <summary>A refused press: the reason is the berth's own prompt, and nothing at all changed.</summary>
        private static string AssertRefusedWithoutEffect(MissionSession session, string because)
        {
            IInteractable berth = Berth(session);
            Snapshot before = Take(session);
            Assert.That(berth.CanInteract, Is.False, "the berth is offered: " + berth.Prompt);
            string prompt = berth.Prompt;
            CommandResult result = berth.TryInteract();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Explanation, Is.EqualTo(prompt), "the refusal is the reason shown at the berth");
            Assert.That(result.Explanation, Does.Contain(because));
            Snapshot after = Take(session);
            Assert.That(after.Time, Is.EqualTo(before.Time), "no time passes");
            Assert.That(after.Rests, Is.EqualTo(before.Rests), "no rest starts");
            Assert.That(after.CompletedRests, Is.EqualTo(before.CompletedRests));
            Assert.That(after.Resting, Is.EqualTo(before.Resting), "a rest is neither started nor ended");
            Assert.That(after.Chapter, Is.EqualTo(before.Chapter), "no chapter transition");
            Assert.That(after.Stores.FoodKg, Is.EqualTo(before.Stores.FoodKg), "no food drawn");
            Assert.That(after.Stores.WaterLitres, Is.EqualTo(before.Stores.WaterLitres), "no water drawn");
            return result.Explanation;
        }

        /// <summary>Awake time in steps of at most an hour, taking each ration and water portion as it falls due.</summary>
        private static void StayAwake(MissionSession session, double seconds)
        {
            double left = seconds;
            while (left > 1e-9)
            {
                double step = left < Hour ? left : Hour;
                Require(session.AdvanceSimulation(step));
                left -= step;
                session.Survival.TryConsumeRation();
                session.Survival.TryDrinkWater();
            }
        }

        /// <summary>Repairs and the first ration: the day-1 order before the first rest.</summary>
        private static void ReadyForTheFirstRest(MissionSession session)
        {
            Fixtures.RepairOrFail(session, ShipSystemId.Power);
            Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
            Require(session.Survival.TryConsumeRation());
        }

        private static void FullNightThroughTheBerth(MissionSession session)
        {
            double before = session.State.ElapsedSimulationSeconds;
            Require(Berth(session).TryInteract());
            Assert.That(session.SurvivalRecovery.IsResting, Is.True);
            Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
            Assert.That(session.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
            Assert.That(session.State.ElapsedSimulationSeconds - before, Is.EqualTo(SurvivalRecovery.MaximumRestHours * Hour).Within(1e-6),
                "a valid rest advances the one clock by the whole night");
        }

        [Test]
        public void TheFirstRest_FollowsTheDay1Order_AndEachRefusalSaysWhatIsMissing()
        {
            using (MissionSession session = Accident())
            {
                AssertRefusedWithoutEffect(session, string.Empty);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                AssertRefusedWithoutEffect(session, "ration");
                Require(session.Survival.TryConsumeRation());

                IInteractable berth = Berth(session);
                Assert.That(berth.CanInteract, Is.True, berth.Prompt);
                FullNightThroughTheBerth(session);
                Assert.That(session.SurvivalRecovery.CompletedRestCount, Is.EqualTo(1));
                Assert.That(session.ChapterFlow.CheckContinueRoutine().Succeeded, Is.True,
                    "the day-1 beat is done exactly as before: the routine to day 5 is offered");
            }
        }

        [Test]
        public void ADoublePress_NeitherEndsTheRestAtOnce_NorStartsAnotherRightAfterWaking()
        {
            using (MissionSession session = Accident())
            {
                ReadyForTheFirstRest(session);
                Require(Berth(session).TryInteract());
                Assert.That(session.SurvivalRecovery.RestCount, Is.EqualTo(1));
                AssertRefusedWithoutEffect(session, "settling in");

                Require(session.AdvanceSimulation(Hour));
                Require(Berth(session).TryInteract());
                Assert.That(session.SurvivalRecovery.IsResting, Is.False, "a deliberate later press still wakes the crew");
                Assert.That(session.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Woken));

                // One hour asleep needs its share of the 15.5 h waking day (about 1.8 h) before the next rest.
                double need = MissionChapterFlow.SleepNeedAwakeSeconds(Hour);
                Assert.That(need, Is.EqualTo(MissionChapterFlow.RoutineAwakeHours * Hour / SurvivalRecovery.MaximumRestHours).Within(1e-6));
                AssertRefusedWithoutEffect(session, "Not tired yet");
                StayAwake(session, need - 60d);
                AssertRefusedWithoutEffect(session, "Press R");
                StayAwake(session, 60d);
                Assert.That(Berth(session).CanInteract, Is.True, Berth(session).Prompt);
                Require(Berth(session).TryInteract());
                Assert.That(session.SurvivalRecovery.RestCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void AfterAFullNight_RepeatedPressesNeverChainRests_UntilAWholeWakingDayPassed()
        {
            using (MissionSession session = Accident())
            {
                ReadyForTheFirstRest(session);
                FullNightThroughTheBerth(session);
                Assert.That(session.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Accident));

                for (int press = 0; press < 25; press++)
                {
                    AssertRefusedWithoutEffect(session, "Not tired yet");
                }

                StayAwake(session, (MissionChapterFlow.RoutineAwakeHours * Hour) - 60d);
                AssertRefusedWithoutEffect(session, "can sleep again in 1 min");
                StayAwake(session, 60d);
                FullNightThroughTheBerth(session);
                Assert.That(session.SurvivalRecovery.CompletedRestCount, Is.EqualTo(2));
                Assert.That(session.ChapterFlow.Chapter, Is.EqualTo(MissionChapter.Accident), "rests alone never move the story");
                AssertRefusedWithoutEffect(session, "Not tired yet");
            }
        }

        [Test]
        public void DuringARoutine_TheBerthNeitherStartsNorEndsARest_AndTheRoutineReachesDay5AsBefore()
        {
            using (MissionSession session = Accident())
            {
                ReadyForTheFirstRest(session);
                FullNightThroughTheBerth(session);
                MissionChapterFlow flow = session.ChapterFlow;
                Require(flow.TryContinueRoutine());

                int frames = 0;
                int pressesWhileResting = 0;
                int pressesWhileAwake = 0;
                while (flow.IsRoutineRunning)
                {
                    Assert.That(frames++, Is.LessThan(20000), "the routine stops");
                    bool resting = session.SurvivalRecovery.IsResting;
                    int routineRests = flow.RoutineRests;
                    AssertRefusedWithoutEffect(session, "routine is running");
                    Assert.That(flow.IsRoutineRunning, Is.True, "a press never stops the routine");
                    Assert.That(flow.RoutineRests, Is.EqualTo(routineRests));
                    if (resting)
                    {
                        pressesWhileResting++;
                    }
                    else
                    {
                        pressesWhileAwake++;
                    }

                    Require(session.Tick(Frame));
                }

                Assert.That(pressesWhileResting, Is.GreaterThan(0), "pressed during the routine's nights");
                Assert.That(pressesWhileAwake, Is.GreaterThan(0), "pressed during the routine's days");
                Assert.That(flow.LastRoutineStop, Is.EqualTo(RoutineStopReason.ReachedChapter), flow.LastRoutineStopText);
                Assert.That(flow.Chapter, Is.EqualTo(MissionChapter.Consequences), "exactly one transition, to day 5");
                Assert.That(session.Calendar.DayNumber, Is.EqualTo(MissionChapterFlow.ConsequencesDay));
                AssertRefusedWithoutEffect(session, "Decide first");
            }
        }

        [Test]
        public void TheLegacySession_KeepsItsBerthAsBefore()
        {
            using (var session = new MissionSession(survivalExtension: true))
            {
                Assert.That(session.ChapterFlow, Is.Null);
                Assert.That(session.Interactions.TryGet(InteractionIds.SurvivalSleep, out IInteractable berth), Is.True);
                Assert.That(berth.CanInteract, Is.False, "no rest before the survival chapter");
            }
        }
    }
}

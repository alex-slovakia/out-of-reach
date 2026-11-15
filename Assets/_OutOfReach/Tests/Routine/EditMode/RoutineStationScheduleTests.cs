using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Core;
using OutOfReach.Presentation.Routine;

namespace OutOfReach.Tests.Routine.EditMode
{
    /// <summary>R18: the montage shows a station only for an action Core really executed, in day order, at a calm pace.</summary>
    public sealed class RoutineStationScheduleTests
    {
        private const float Frame = .05f;

        private static RoutineFacts Facts(int rations = 0, int drinks = 0, int rests = 0, bool resting = false,
            bool crop = false, int day = 2, double key = 1000d, double cropSeconds = 4d * 86400d)
        {
            return new RoutineFacts(true, key, rations, drinks, rests, resting, crop, cropSeconds, day);
        }

        private static RoutineFacts Stopped() => new RoutineFacts(false, double.NaN, 0, 0, 0, false, false, double.NaN, 1);

        private static List<RoutineStation> Run(RoutineStationSchedule schedule, RoutineFacts facts, float seconds,
            bool paused = false, bool reduced = false)
        {
            var seen = new List<RoutineStation>();
            for (float t = 0f; t < seconds; t += Frame)
            {
                schedule.Observe(facts, Frame, paused, reduced);
                if (seen.Count == 0 || seen[seen.Count - 1] != schedule.Current) seen.Add(schedule.Current);
            }

            return seen;
        }

        [Test]
        public void NothingIsShownWhileNoRoutineRuns()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Stopped(), Frame, false, false);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.None));
            Assert.That(schedule.Caption, Is.Empty);
        }

        [Test]
        public void ARunningRoutineWithNoExecutedActionShowsNoStation()
        {
            var schedule = new RoutineStationSchedule();
            List<RoutineStation> seen = Run(schedule, Facts(), 6f);
            Assert.That(seen, Is.EqualTo(new[] { RoutineStation.None }));
        }

        [Test]
        public void OnlyTheExecutedActionIsShown()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(), Frame, false, false);
            List<RoutineStation> seen = Run(schedule, Facts(rations: 1), 2f);
            Assert.That(seen, Has.Member(RoutineStation.Pantry));
            Assert.That(seen, Has.No.Member(RoutineStation.Water), "no water portion was drunk");
            Assert.That(seen, Has.No.Member(RoutineStation.Berth), "no rest began");
            Assert.That(seen, Has.No.Member(RoutineStation.Crops), "no grow module is growing");
        }

        [Test]
        public void ADayIsShownInOrderCropsMealWaterSleep()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(), Frame, false, false);
            List<RoutineStation> seen = Run(schedule, Facts(rations: 1, drinks: 1, rests: 1, resting: true, crop: true), 6f);
            Assert.That(seen, Is.EqualTo(new[]
            {
                RoutineStation.Crops, RoutineStation.Pantry, RoutineStation.Water, RoutineStation.Berth
            }));
        }

        [Test]
        public void AStationStaysForItsMinimumTime()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(rations: 1), Frame, false, false);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.Pantry));
            // A drink arrives at once; the meal shot is not cut short.
            for (float t = Frame; t < RoutineStationSchedule.ShotSeconds - Frame; t += Frame)
            {
                schedule.Observe(Facts(rations: 1, drinks: 1), Frame, false, false);
                Assert.That(schedule.Current, Is.EqualTo(RoutineStation.Pantry), "at " + t);
            }

            Run(schedule, Facts(rations: 1, drinks: 1), 3f * Frame);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.Water));
        }

        [Test]
        public void PauseFreezesTheShot()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(rations: 1), Frame, false, false);
            float before = schedule.CurrentSeconds;
            List<RoutineStation> seen = Run(schedule, Facts(rations: 1, drinks: 1), 10f, paused: true);
            Assert.That(seen, Is.EqualTo(new[] { RoutineStation.Pantry }));
            Assert.That(schedule.CurrentSeconds, Is.EqualTo(before));
            Assert.That(schedule.IsPending(RoutineStation.Water), Is.True, "the drink is still owed after the pause");
        }

        [Test]
        public void ReducedEffectsHoldEachShotLonger()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(rations: 1), Frame, false, true);
            Run(schedule, Facts(rations: 1, drinks: 1), RoutineStationSchedule.ShotSeconds + .3f, reduced: true);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.Pantry), "the normal cut time has passed");
            Run(schedule, Facts(rations: 1, drinks: 1), RoutineStationSchedule.ReducedShotSeconds, reduced: true);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.Water));
        }

        [Test]
        public void AStoppedRoutineClearsTheScreenAtOnce()
        {
            var schedule = new RoutineStationSchedule();
            schedule.Observe(Facts(rations: 1, drinks: 1, rests: 1, resting: true), Frame, false, false);
            Assert.That(schedule.Current, Is.Not.EqualTo(RoutineStation.None));
            schedule.Observe(Stopped(), Frame, false, false);
            Assert.That(schedule.Current, Is.EqualTo(RoutineStation.None));
            Assert.That(schedule.Caption, Is.Empty);
            Assert.That(schedule.IsPending(RoutineStation.Water), Is.False, "nothing is carried into the next segment");
        }

        [Test]
        public void ANewSegmentDoesNotReplayTheLastOne()
        {
            var schedule = new RoutineStationSchedule();
            Run(schedule, Facts(rations: 3, drinks: 3, rests: 1, key: 1000d), 1f);
            List<RoutineStation> seen = Run(schedule, Facts(key: 5000d), 5f);
            Assert.That(seen, Is.EqualTo(new[] { RoutineStation.None }), "the new segment has executed nothing yet");
        }

        [Test]
        public void TheCropIsShownOncePerDayAndOnlyWhileGrowing()
        {
            var schedule = new RoutineStationSchedule();
            List<RoutineStation> day2 = Run(schedule, Facts(crop: true, day: 2), 8f);
            Assert.That(day2, Is.EqualTo(new[] { RoutineStation.Crops, RoutineStation.None }), "once, then it leaves");
            List<RoutineStation> day3 = Run(schedule, Facts(crop: true, day: 3), 2f);
            Assert.That(day3, Has.Member(RoutineStation.Crops), "a new day shows it again");
            var stopped = new RoutineStationSchedule();
            Assert.That(Run(stopped, Facts(crop: false, day: 2), 4f), Is.EqualTo(new[] { RoutineStation.None }));
        }

        [Test]
        public void TheBerthIsHeldWhileAsleepAndLeavesAfterWaking()
        {
            var schedule = new RoutineStationSchedule();
            List<RoutineStation> asleep = Run(schedule, Facts(rests: 1, resting: true), 6f);
            Assert.That(asleep, Is.EqualTo(new[] { RoutineStation.Berth }), "held past the idle limit while the crew sleeps");
            List<RoutineStation> awake = Run(schedule, Facts(rests: 1, resting: false), 2f);
            Assert.That(awake[awake.Count - 1], Is.EqualTo(RoutineStation.None));
        }

        [Test]
        public void CaptionsCarryCoresOwnNumbers()
        {
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.Pantry, Facts(rations: 3)), Does.Contain("3 meals"));
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.Water, Facts(drinks: 1)), Does.Contain("1 drink "));
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.Berth, Facts(rests: 2)), Does.Contain("night 2"));
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.Crops, Facts(crop: true, cropSeconds: 2.5d * 86400d)),
                Does.Contain("harvest in 2.5 days"));
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.Crops, Facts(crop: true, cropSeconds: double.NaN)),
                Is.EqualTo("CROPS · grow module running"));
            Assert.That(RoutineStationSchedule.Describe(RoutineStation.None, Facts()), Is.Empty);
        }

        [Test]
        public void StationsMapToTheExistingInteractionAnchors()
        {
            Assert.That(RoutineStationMontage.StationOf(InteractionIds.ConsumeRation), Is.EqualTo(RoutineStation.Pantry));
            Assert.That(RoutineStationMontage.StationOf(InteractionIds.DrinkWater), Is.EqualTo(RoutineStation.Water));
            Assert.That(RoutineStationMontage.StationOf(InteractionIds.SurvivalSleep), Is.EqualTo(RoutineStation.Berth));
            Assert.That(RoutineStationMontage.StationOf(InteractionIds.HarvestCrop), Is.EqualTo(RoutineStation.Crops));
            Assert.That(RoutineStationMontage.StationOf(InteractionIds.CockpitConsole), Is.EqualTo(RoutineStation.None));
        }
    }
}

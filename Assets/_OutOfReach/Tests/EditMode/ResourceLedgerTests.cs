using System;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class ResourceLedgerTests
    {
        private static readonly ResourceSnapshot Initial = new ResourceSnapshot(
            oxygenKg: 10d, waterLitres: 20d, foodKg: 5d, batteryKWh: 4d, sparePartsCount: 3, carbonDioxidePpm: 1000d);

        private static ResourceLedger NewLedger(MissionEventBus bus, ResourceRates rates, ResourceSnapshot? initial = null, double? capacityKWh = null)
        {
            var ledger = new ResourceLedger(bus);
            ResourceSnapshot snapshot = initial ?? Initial;
            CommandResult result = ledger.Initialize(snapshot, capacityKWh ?? snapshot.BatteryKWh, rates);
            Assert.That(result.Succeeded, Is.True, result.Explanation);
            return ledger;
        }

        private static ResourceAmount Cost(ResourceId resource, double quantity)
        {
            return new ResourceAmount(resource, quantity);
        }

        [Test]
        public void Spending_IsAllOrNothing_WhenAnyEntryIsUnaffordable()
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, ResourceRates.Zero);
            using (var recorder = new EventRecorder(bus))
            {
                CommandResult result = ledger.TryConsume(new[] { Cost(ResourceId.SparePartsCount, 2d), Cost(ResourceId.BatteryKWh, 100d) });

                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(result.Explanation, Does.Contain("battery energy"));
                Fixtures.AssertSnapshot(Initial, ledger.Snapshot);
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void DuplicateEntries_AreAggregated_BeforeCheckingAffordability()
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, ResourceRates.Zero);
            using (var recorder = new EventRecorder(bus))
            {
                CommandResult tooMany = ledger.TryConsume(new[] { Cost(ResourceId.SparePartsCount, 2d), Cost(ResourceId.SparePartsCount, 2d) });
                Assert.That(tooMany.Failure, Is.EqualTo(CommandFailure.InsufficientResources), "2 + 2 parts exceed the 3 available");
                Assert.That(ledger.Snapshot.SparePartsCount, Is.EqualTo(3));

                CommandResult exact = ledger.TryConsume(new[] { Cost(ResourceId.SparePartsCount, 2d), Cost(ResourceId.SparePartsCount, 1d) });

                Assert.That(exact.Succeeded, Is.True, exact.Explanation);
                Assert.That(ledger.Snapshot.SparePartsCount, Is.EqualTo(0));
                Assert.That(ledger.GetDepletionTime(ResourceId.SparePartsCount), Is.EqualTo(0d));
                Assert.That(recorder.Resources, Has.Count.EqualTo(1), "one event per successful transaction");
            }
        }

        private static IEnumerable<TestCaseData> InvalidCostLists()
        {
            yield return new TestCaseData((object)new[] { Cost(ResourceId.OxygenKg, -1d) }).SetName("Rejects a negative quantity");
            yield return new TestCaseData((object)new[] { Cost(ResourceId.OxygenKg, double.NaN) }).SetName("Rejects NaN");
            yield return new TestCaseData((object)new[] { Cost(ResourceId.BatteryKWh, double.PositiveInfinity) }).SetName("Rejects infinity");
            yield return new TestCaseData((object)new[] { Cost(ResourceId.SparePartsCount, 1.5d) }).SetName("Rejects fractional spare parts");
            yield return new TestCaseData((object)new[] { Cost((ResourceId)99, 1d) }).SetName("Rejects an unknown resource such as CO2");
            yield return new TestCaseData((object)new[] { Cost(ResourceId.SparePartsCount, 1d), Cost(ResourceId.WaterLitres, double.NaN) })
                .SetName("Rejects the whole list when one entry is invalid");
            yield return new TestCaseData((object)new[] { Cost(ResourceId.OxygenKg, double.MaxValue), Cost(ResourceId.OxygenKg, double.MaxValue) })
                .SetName("Rejects totals that overflow");
        }

        [TestCaseSource(nameof(InvalidCostLists))]
        public void InvalidCosts_AreRejected_WithoutMutationOrEvents(ResourceAmount[] costs)
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, ResourceRates.Zero);
            using (var recorder = new EventRecorder(bus))
            {
                CommandResult result = ledger.TryConsume(costs);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Fixtures.AssertSnapshot(Initial, ledger.Snapshot);
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void MissingCostList_IsInvalidInput()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), ResourceRates.Zero);

            Assert.That(ledger.TryConsume(null).Failure, Is.EqualTo(CommandFailure.InvalidInput));
        }

        [Test]
        public void ExactSpend_ReachesZero_NeverNegative_EvenWithFloatDust()
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, ResourceRates.Zero, new ResourceSnapshot(10d, 0.3d, 5d, 4d, 3, 1000d));

            CommandResult oxygen = ledger.TryConsume(new[] { Cost(ResourceId.OxygenKg, 4d), Cost(ResourceId.OxygenKg, 6d) });
            CommandResult water = ledger.TryConsume(new[] { Cost(ResourceId.WaterLitres, 0.1d), Cost(ResourceId.WaterLitres, 0.2d) });

            Assert.That(oxygen.Succeeded, Is.True, oxygen.Explanation);
            Assert.That(water.Succeeded, Is.True, "0.1 + 0.2 slightly exceeds 0.3 in floating point; tolerance absorbs it");
            Assert.That(ledger.Snapshot.OxygenKg, Is.EqualTo(0d));
            Assert.That(ledger.Snapshot.WaterLitres, Is.EqualTo(0d));
        }

        [Test]
        public void SpendingBeforeLaunch_IsRejected()
        {
            var ledger = new ResourceLedger(new MissionEventBus());

            CommandResult result = ledger.TryConsume(new[] { Cost(ResourceId.SparePartsCount, 1d) });

            Assert.That(result.Failure, Is.EqualTo(CommandFailure.WrongPhase));
        }

        [Test]
        public void EnergyConversion_UsesWattsTimesSecondsOver3600000()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), new ResourceRates(0d, 0d, 0d, 1000d, 0d));

            ledger.AdvanceTime(3600d);

            Assert.That(ledger.Snapshot.BatteryKWh, Is.EqualTo(3d).Within(1e-12), "1000 W for one hour is 1 kWh");
            Assert.That(EnergyMath.KilowattHours(250d, 7200d), Is.EqualTo(0.5d).Within(1e-15));
            Assert.That(EnergyMath.KilowattHours(-500d, 3600d), Is.EqualTo(-0.5d).Within(1e-15));
        }

        [Test]
        public void ManySmallSteps_MatchOneLargeStep()
        {
            var rates = new ResourceRates(0.001d, 0.002d, 0.0005d, 500d, 0.1d);
            ResourceLedger small = NewLedger(new MissionEventBus(), rates);
            ResourceLedger large = NewLedger(new MissionEventBus(), rates);

            for (int i = 0; i < 3600; i++)
            {
                small.AdvanceTime(1d);
            }

            large.AdvanceTime(3600d);

            var expected = new ResourceSnapshot(6.4d, 12.8d, 3.2d, 3.5d, 3, 1360d);
            Fixtures.AssertSnapshot(expected, large.Snapshot);
            Fixtures.AssertSnapshot(large.Snapshot, small.Snapshot);
            Assert.That(small.ElapsedSimulationSeconds, Is.EqualTo(3600d).Within(Fixtures.Tolerance));
        }

        [Test]
        public void LongStep_ClampsAtZero_AndRecordsTheExactDepletionTime()
        {
            var rates = new ResourceRates(0.001d, 0d, 0d, 0d, 0d);
            ResourceLedger ledger = NewLedger(new MissionEventBus(), rates, new ResourceSnapshot(1d, 20d, 5d, 4d, 3, 1000d));

            Assert.That(ledger.TimeToNextDepletion(), Is.EqualTo(1000d).Within(Fixtures.Tolerance));

            ledger.AdvanceTime(5000d);

            Assert.That(ledger.Snapshot.OxygenKg, Is.EqualTo(0d));
            Assert.That(ledger.GetDepletionTime(ResourceId.OxygenKg), Is.EqualTo(1000d).Within(Fixtures.Tolerance));
            Assert.That(ledger.Snapshot.WaterLitres, Is.EqualTo(20d), "other reserves are untouched");
            Assert.That(ledger.TimeToNextDepletion(), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void BatteryCharging_ClampsAtCapacity_AndClearsDepletion()
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, new ResourceRates(0d, 0d, 0d, 2000d, 0d), new ResourceSnapshot(10d, 20d, 5d, 2d, 3, 1000d), capacityKWh: 4d);

            ledger.AdvanceTime(7200d);
            Assert.That(ledger.Snapshot.BatteryKWh, Is.EqualTo(0d));
            Assert.That(ledger.GetDepletionTime(ResourceId.BatteryKWh), Is.EqualTo(3600d).Within(Fixtures.Tolerance));

            Assert.That(ledger.TrySetRates(new ResourceRates(0d, 0d, 0d, -1000d, 0d)).Succeeded, Is.True);
            ledger.AdvanceTime(3d * 3600d);
            Assert.That(ledger.Snapshot.BatteryKWh, Is.EqualTo(3d).Within(1e-12));
            Assert.That(ledger.GetDepletionTime(ResourceId.BatteryKWh), Is.Null);

            ledger.AdvanceTime(10d * 3600d);
            Assert.That(ledger.Snapshot.BatteryKWh, Is.EqualTo(4d), "never above capacity");
        }

        [Test]
        public void InvalidTimeStep_Throws_WithoutMutation([Values(-1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity)] double invalid)
        {
            var bus = new MissionEventBus();
            ResourceLedger ledger = NewLedger(bus, new ResourceRates(0.001d, 0d, 0d, 100d, 0d));
            using (var recorder = new EventRecorder(bus))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => ledger.AdvanceTime(invalid));
                Fixtures.AssertSnapshot(Initial, ledger.Snapshot);
                Assert.That(ledger.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void CarbonDioxide_IsAnAtmosphericState_NotInventory()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), new ResourceRates(0d, 0d, 0d, 0d, 0.5d));

            ledger.AdvanceTime(100d);
            Assert.That(ledger.Snapshot.CarbonDioxidePpm, Is.EqualTo(1050d).Within(Fixtures.Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.GetQuantity((ResourceId)99));

            Assert.That(ledger.TrySetRates(new ResourceRates(0d, 0d, 0d, 0d, -100d)).Succeeded, Is.True);
            ledger.AdvanceTime(100d);
            Assert.That(ledger.Snapshot.CarbonDioxidePpm, Is.EqualTo(0d), "concentration cannot go negative");
        }

        [Test]
        public void CarbonDioxide_StaysBounded_BetweenTheCabinBaselineAndOneMillionPpm()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), new ResourceRates(0d, 0d, 0d, 0d, 1e300));

            ledger.AdvanceTime(1e10);
            Assert.That(ledger.Snapshot.CarbonDioxidePpm, Is.EqualTo(ResourceLedger.MaximumCarbonDioxidePpm), "finite and capped even across an absurd jump");

            Assert.That(ledger.TrySetCarbonDioxideFloor(1000d).Succeeded, Is.True);
            Assert.That(ledger.TrySetCarbonDioxideFloor(double.NaN).Failure, Is.EqualTo(CommandFailure.InvalidInput));
            Assert.That(ledger.TrySetRates(new ResourceRates(0d, 0d, 0d, 0d, -0.5d)).Succeeded, Is.True);
            ledger.AdvanceTime(1e9);
            Assert.That(ledger.Snapshot.CarbonDioxidePpm, Is.EqualTo(1000d), "scrubbing stops at the cabin baseline");
        }

        [Test]
        public void TimeUntilCarbonDioxideLimit_IsExact_AndInfiniteWhenNotRising()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), new ResourceRates(0d, 0d, 0d, 0d, 0.25d));

            Assert.That(ledger.TimeUntilCarbonDioxideReaches(2000d), Is.EqualTo(4000d).Within(Fixtures.Tolerance));
            Assert.That(ledger.TimeUntilCarbonDioxideReaches(900d), Is.EqualTo(0d), "already above");

            Assert.That(ledger.TrySetRates(ResourceRates.Zero).Succeeded, Is.True);
            Assert.That(ledger.TimeUntilCarbonDioxideReaches(2000d), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void InactiveMission_FreezesReserves_WhoeverCalls()
        {
            bool active = true;
            var bus = new MissionEventBus();
            var ledger = new ResourceLedger(bus, () => active);
            Assert.That(ledger.Initialize(Initial, Initial.BatteryKWh, new ResourceRates(0.001d, 0d, 0d, 100d, 0.1d)).Succeeded, Is.True);

            active = false;
            using (var recorder = new EventRecorder(bus))
            {
                ledger.AdvanceTime(3600d);
                CommandResult spend = ledger.TryConsume(new[] { Cost(ResourceId.SparePartsCount, 1d) });

                Assert.That(spend.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Fixtures.AssertSnapshot(Initial, ledger.Snapshot, 0d);
                Assert.That(ledger.ElapsedSimulationSeconds, Is.EqualTo(0d));
                Assert.That(recorder.Total, Is.EqualTo(0));
            }

            active = true;
            ledger.AdvanceTime(1000d);
            Assert.That(ledger.Snapshot.OxygenKg, Is.EqualTo(9d).Within(Fixtures.Tolerance));
        }

        [Test]
        public void InvalidRates_AreRejected_AndKeepThePreviousRates()
        {
            var original = new ResourceRates(0.001d, 0d, 0d, 100d, 0d);
            ResourceLedger ledger = NewLedger(new MissionEventBus(), original);

            Assert.That(ledger.TrySetRates(new ResourceRates(-0.1d, 0d, 0d, 0d, 0d)).Failure, Is.EqualTo(CommandFailure.InvalidInput));
            Assert.That(ledger.TrySetRates(new ResourceRates(0d, 0d, 0d, double.NaN, 0d)).Failure, Is.EqualTo(CommandFailure.InvalidInput));
            Assert.That(ledger.Rates.OxygenUseKgPerSecond, Is.EqualTo(original.OxygenUseKgPerSecond));
            Assert.That(ledger.Rates.NetLoadWatts, Is.EqualTo(original.NetLoadWatts));
        }

        [Test]
        public void ReservesLoadOnlyOnce()
        {
            ResourceLedger ledger = NewLedger(new MissionEventBus(), ResourceRates.Zero);

            CommandResult second = ledger.Initialize(new ResourceSnapshot(99d, 99d, 99d, 99d, 99, 0d), 99d, ResourceRates.Zero);

            Assert.That(second.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
            Fixtures.AssertSnapshot(Initial, ledger.Snapshot);
        }
    }
}

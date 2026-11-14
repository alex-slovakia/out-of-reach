using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class RepairTests
    {
        private static ResourceAmount Parts(double count)
        {
            return new ResourceAmount(ResourceId.SparePartsCount, count);
        }

        [Test]
        public void Repair_SpendsTheExactCostOnce_AndAnnouncesTheWholeTransactionInOrder()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult repaired = Fixtures.Repair(session, ShipSystemId.LifeSupport);

                Assert.That(repaired.Succeeded, Is.True, repaired.Explanation);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5), "7 - 2 parts");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(27d).Within(Fixtures.Tolerance), "28 - 1 kWh");
                Assert.That(session.Systems.LifeSupport.IsOperational, Is.True);
                Assert.That(recorder.Order, Is.EqualTo(new[] { "ResourceChanged", "SystemConditionChanged", "PhaseChanged" }));
                Assert.That(recorder.SystemConditions.Single().SystemId, Is.EqualTo(ShipSystemId.LifeSupport));
                Assert.That(recorder.SystemConditions.Single().Condition01, Is.EqualTo(1d));
                Assert.That(recorder.Phases.Single().Current, Is.EqualTo(GamePhase.Repair));

                CommandResult again = Fixtures.Repair(session, ShipSystemId.LifeSupport);

                Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5), "a repeated repair never charges again");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(27d).Within(Fixtures.Tolerance));
                Assert.That(session.Systems.LifeSupport.RepairCount, Is.EqualTo(1));
                Assert.That(recorder.Total, Is.EqualTo(3), "no duplicate success event");
            }
        }

        [Test]
        public void Repair_WithoutEnoughParts_ChangesNothing_AndExplainsTheShortfall()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.Resources.TryConsume(new[] { Parts(6d) }).Succeeded, Is.True);
                ResourceSnapshot before = session.Resources.Snapshot;
                using (var recorder = new EventRecorder(session.Events))
                {
                    CommandResult result = Fixtures.Repair(session, ShipSystemId.Power);

                    Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                    Assert.That(result.Explanation, Does.Contain("needs 2 spare parts, 1 spare part available"));
                    Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
                    Assert.That(session.Systems.Power.Condition01, Is.EqualTo(0.2d));
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                    Assert.That(recorder.Total, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void Repair_WithoutEnoughEnergy_KeepsThePartsToo_ButPowerNeverNeedsStoredEnergy()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.BatteryKWh, 27.5d) }).Succeeded, Is.True);

                CommandResult lifeSupport = Fixtures.Repair(session, ShipSystemId.LifeSupport);

                Assert.That(lifeSupport.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(lifeSupport.Explanation, Does.Contain("kWh"));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(7), "no partial payment");
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(0.5d).Within(Fixtures.Tolerance));

                CommandResult power = Fixtures.Repair(session, ShipSystemId.Power);
                Assert.That(power.Succeeded, Is.True, "a nearly flat battery cannot make the power repair impossible");
            }
        }

        [Test]
        public void Repairs_AreRejected_BeforeTheAccident_AndAfterTheMissionEnded()
        {
            using (var intro = Fixtures.LaunchedSession())
            using (var ended = Fixtures.EmergencySession())
            {
                Assert.That(Fixtures.Repair(intro, ShipSystemId.Power).Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "nothing to repair yet");
                Assert.That(intro.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));

                Assert.That(ended.Director.TryEndMission(MissionOutcome.Lost, "test").Succeeded, Is.True);
                CommandResult late = Fixtures.Repair(ended, ShipSystemId.Power);

                Assert.That(late.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(ended.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));
                Assert.That(ended.Systems.Power.IsOperational, Is.False);
            }
        }

        [Test]
        public void AntennaRepair_RequiresAnEva()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                CommandResult inside = Fixtures.Repair(session, ShipSystemId.Antenna);
                Assert.That(inside.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(inside.Explanation, Does.Contain("EVA"));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));

                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);

                CommandResult fromTheAirlock = Fixtures.Repair(session, ShipSystemId.Antenna);
                Assert.That(fromTheAirlock.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "the antenna is out of reach from inside the airlock");
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(3));

                Fixtures.GoOutside(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Antenna);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(0), "science-focus carries exactly the 7 parts the three repairs need");
                Assert.That(session.Systems.Antenna.IsOperational, Is.True);
            }
        }

        [Test]
        public void Loadout_DecidesWhatIsLeftAfterTheInteriorRepairs()
        {
            using (var science = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            using (var redundancy = Fixtures.EmergencySession(AuroraMission.RedundancyPresetId))
            {
                foreach (MissionSession session in new[] { science, redundancy })
                {
                    Fixtures.RepairOrFail(session, ShipSystemId.Power);
                    Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                }

                Assert.That(science.Resources.Snapshot.SparePartsCount, Is.EqualTo(3), "just enough for the antenna, nothing for extras");
                Assert.That(redundancy.Resources.Snapshot.SparePartsCount, Is.EqualTo(9), "six parts in hand after the antenna");
                Assert.That(redundancy.Resources.Snapshot.BatteryKWh, Is.GreaterThan(science.Resources.Snapshot.BatteryKWh));
            }
        }

        [Test]
        public void RepairCosts_ComeFromTheDomain_AndCannotBeMutated()
        {
            using (var session = Fixtures.NewSession())
            {
                IRepairable power = session.Systems.Repairables.Single(system => system.SystemId == ShipSystemId.Power);

                Assert.That(power.RepairCosts, Has.Count.EqualTo(1));
                Assert.That(power.RepairCosts[0].Resource, Is.EqualTo(ResourceId.SparePartsCount));
                Assert.That(power.RepairCosts[0].Quantity, Is.EqualTo(2d));
                var costs = (IList<ResourceAmount>)power.RepairCosts;
                Assert.Throws<NotSupportedException>(() => costs[0] = Parts(0d));
            }
        }
    }
}

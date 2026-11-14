using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>Workbench, absorber bay and instrument rack in the grey-box harness, through their scene targets.</summary>
    public sealed class FieldModificationPlayTests
    {
        private const float FrameSeconds = 0.02f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build();
            // Simulated time is advanced explicitly; the runtime still syncs the scene every frame.
            harness.Runtime.DriveFromUpdate = false;
        }

        [TearDown]
        public void TearDown()
        {
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator PressInteract()
        {
            harness.Input.PressInteract();
            yield return Frames(3);
        }

        /// <summary>Stands the player 1.5 m in front of a wall-mounted target (west wall at -X, east wall at +X).</summary>
        private IEnumerator Face(InteractableTarget target, Vector3 position)
        {
            float side = position.x < 0f ? 1f : -1f;
            harness.PlacePlayerFacing(position.x + (side * (0.15f + 1.5f)), position.z, position);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(target));
        }

        [UnityTest]
        public IEnumerator Bench_Bay_AndInstrument_WorkThroughTheirTargets()
        {
            harness.ReachEmergency();
            MissionSession session = harness.Session;
            IFieldModifications view = harness.Runtime.FieldModifications;
            Assert.That(session.FieldModifications.SalvageEnabled, Is.True, "the instrument target is bound in this scene");
            Assert.That(session.FieldModifications.AbsorberEnabled, Is.True, "bench and bay are both bound");

            yield return Face(harness.Workbench, RepairLoopHarness.WorkbenchPosition);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Build CO2 absorber cartridge: 1 spare part, 0.2 kWh."));
            yield return PressInteract();
            Assert.That(view.AbsorberCartridgesCarried, Is.EqualTo(1));
            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(6));

            yield return Face(harness.AbsorberBay, RepairLoopHarness.AbsorberBayPosition);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Install CO2 absorber cartridge: cabin CO2 stops rising for up to 12 h 00 min"));
            yield return PressInteract();
            Assert.That(view.AbsorberInstalled, Is.True);
            Assert.That(view.AbsorberCartridgesCarried, Is.EqualTo(0));
            Assert.That(session.Resources.Rates.CarbonDioxideChangePpmPerSecond, Is.EqualTo(0d).Within(1e-12), "life support is still down, but CO2 holds");
            yield return Frames(2);
            Assert.That(harness.Resolver.CurrentCanInteract, Is.False);
            Assert.That(harness.Resolver.CurrentPrompt, Does.StartWith("CO2 absorber working: "));

            yield return Face(harness.PayloadRack, RepairLoopHarness.PayloadRackPosition);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Dismantle science instrument: +3 spare parts for 0.2 kWh. The science objective is abandoned for good."));
            yield return PressInteract();
            Assert.That(view.SciencePayloadSalvaged, Is.True);
            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(9));
            yield return Frames(2);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Science instrument dismantled"));
            yield return PressInteract();
            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(9), "a second press gives nothing");
        }

        [UnityTest]
        public IEnumerator AShipWithoutTheTargets_OffersNoModification()
        {
            harness.Runtime.Configure(harness.Player, harness.Spawn, new[] { harness.Console, harness.PowerPanel, harness.LifeSupportPanel, harness.Workbench });
            harness.Runtime.RestartMission();
            yield return Frames(3);

            FieldModifications mods = harness.Session.FieldModifications;
            Assert.That(mods.SalvageEnabled, Is.False, "no instrument target bound");
            Assert.That(mods.AbsorberEnabled, Is.False, "a bench without a bay cannot complete a cartridge");

            harness.ReachEmergency();
            Assert.That(mods.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(mods.TrySalvagePayload().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(mods.SalvageablePartsNow, Is.EqualTo(0), "so the loss rule is the one from before C05-B");
        }
    }
}

using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>The physical preflight in the grey-box harness: walk during Planning, carry, load, launch from the console.</summary>
    public sealed class PreflightPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float WalkwayZ = 6.5f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(false, true);
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

        /// <summary>Stands the player on the walkway in a type's column, facing its pallet or its bay stack.</summary>
        private IEnumerator Face(string equipmentId, bool pallet)
        {
            Vector3 target = pallet ? RepairLoopHarness.PalletPosition(equipmentId) : RepairLoopHarness.BayStackPosition(equipmentId);
            harness.PlacePlayerFacing(target.x, WalkwayZ, target);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(pallet ? harness.CargoSupply[equipmentId] : harness.CargoBay[equipmentId]));
        }

        /// <summary>Walks along the walkway for half a second and returns the distance covered.</summary>
        private IEnumerator WalkAlongWalkway(System.Action<float> report)
        {
            harness.PlacePlayerFacing(-5f, WalkwayZ, new Vector3(6f, 0f, WalkwayZ));
            yield return Frames(2);
            float startX = harness.Player.transform.position.x;
            harness.Input.Move = new Vector2(0f, 1f);
            yield return Frames(25);
            harness.Input.Move = Vector2.zero;
            report(harness.Player.transform.position.x - startX);
        }

        [UnityTest]
        public IEnumerator Preflight_WalkCarryLoadAndLaunchFromTheConsole()
        {
            MissionSession session = harness.Session;
            yield return Frames(3);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(harness.Player.ControlAllowed, Is.True, "the player walks before launch");
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.PreflightSpawnPosition.z).Within(0.05f), "starts on the pad");

            float emptyHanded = 0f;
            yield return WalkAlongWalkway(distance => emptyHanded = distance);
            Assert.That(emptyHanded, Is.EqualTo(harness.Player.WalkSpeedMetresPerSecond * 25 * FrameSeconds).Within(0.1f));

            // Pick up a battery module: 55 kg.
            yield return Face(AuroraMission.BatteryModule, true);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Pick up battery module: 55 kg, 60 credits, +10 kWh battery (2 on the pallet)"));
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.BatteryModule));
            Assert.That(harness.Player.SpeedMultiplier, Is.EqualTo(1f - (55f / 180f)).Within(1e-4f));

            float carrying = 0f;
            yield return WalkAlongWalkway(distance => carrying = distance);
            Assert.That(carrying, Is.EqualTo(emptyHanded * (1f - (55f / 180f))).Within(0.08f), "heavier crates are slower");
            Assert.That(carrying, Is.LessThan(emptyHanded - 0.2f));

            // Load it: one unit more in the planner's draft, and over the mass limit.
            yield return Face(AuroraMission.BatteryModule, false);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Load battery module: payload 546 -> 601 of 600 kg (over the limit)"));
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);
            Assert.That(harness.Runtime.Planner.Draft.TotalMassKg, Is.EqualTo(601d));
            Assert.That(harness.Player.SpeedMultiplier, Is.EqualTo(1f));

            // R27: the console warns that the launch is overweight (launching anyway ends in a crash),
            // so the player does not press it here.
            Vector3 console = RepairLoopHarness.ConsolePosition;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Console));
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Begin mission OVERWEIGHT: 601 of 600 kg, over the launch limit"));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));

            // Make room: take one spare-parts kit out and put it back on its pallet.
            yield return Face(AuroraMission.SparePartsKit, false);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Unload one spare parts kit (2 aboard)"));
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.SparePartsKit));
            yield return Face(AuroraMission.SparePartsKit, true);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Put the spare parts kit back on the pallet"));
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);
            Assert.That(harness.Runtime.Planner.Draft.TotalMassKg, Is.EqualTo(576d));

            // Launch from the console: the frozen loadout is what the bay held, and the player is in the cockpit.
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("Begin mission: 576 of 600 kg, 735 of 800 credits"));
            harness.PlacePlayerFacing(4f, WalkwayZ, console);
            yield return Frames(2);
            Assert.That(harness.Session.Interactions.TryGet(InteractionIds.CockpitConsole, out IInteractable domainConsole), Is.True);
            CommandResult launched = domainConsole.TryInteract();
            Assert.That(launched.Succeeded, Is.True, launched.Explanation);
            yield return Frames(3);

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
            Assert.That(harness.Player.transform.position.x, Is.EqualTo(RepairLoopHarness.SpawnPosition.x).Within(0.05f), "moved to the cockpit at launch");
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.SpawnPosition.z).Within(0.05f));
            Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(38d), "three battery modules");
            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(4), "one kit left behind");

            yield return Face(AuroraMission.BatteryModule, true);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo("The cargo bay is sealed after launch."));
            Assert.That(harness.Resolver.CurrentCanInteract, Is.False);
        }

        [UnityTest]
        public IEnumerator Preflight_TheDeadlineLaunchesTheLastValidLoadout_AndMovesThePlayerToTheCockpit()
        {
            harness.Runtime.DriveFromUpdate = false;
            MissionSession session = harness.Session;
            harness.Runtime.BeginTimedPlanning();
            Assert.That(harness.Runtime.PlanningDeadline.IsEnabled, Is.True);

            // Take the solar wing out of the bay and keep it in hands: the draft cannot power the instrument.
            yield return Face(AuroraMission.SolarWing, false);
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.SolarWing));
            Assert.That(harness.Runtime.Planner.Draft.Validate().Succeeded, Is.False);
            Assert.That(harness.Player.SpeedMultiplier, Is.LessThan(0.62f));

            Assert.That(harness.Runtime.Step(61d).Succeeded, Is.True);
            yield return Frames(3);

            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
            Assert.That(harness.Runtime.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);
            Assert.That(harness.Player.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.SpawnPosition.z).Within(0.05f), "in the cockpit");
            Assert.That(session.Configuration.AvailablePowerWatts, Is.EqualTo(1900d), "the wing of the last valid loadout is aboard");
        }

        [UnityTest]
        public IEnumerator Restart_DuringPreflight_ReturnsToThePadWithEmptyHands()
        {
            yield return Face(AuroraMission.FoodLocker, true);
            yield return PressInteract();
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.FoodLocker));

            harness.Runtime.RestartMission();
            yield return Frames(3);

            Assert.That(harness.Session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(harness.Runtime.Cargo.CarriedEquipmentId, Is.Null);
            Assert.That(harness.Player.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(harness.Player.transform.position.z, Is.EqualTo(RepairLoopHarness.PreflightSpawnPosition.z).Within(0.05f));
            Assert.That(harness.Player.ControlAllowed, Is.True);
        }
    }
}

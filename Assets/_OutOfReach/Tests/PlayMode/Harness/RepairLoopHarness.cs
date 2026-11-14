using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// TEST FIXTURE. A grey-box layout built from primitives and wired exactly the way an integrator wires the real
    /// scene (through the Configure methods that set the serialized fields):
    /// a cabin with the cockpit console and two repair panels, a suit locker, an airlock corridor with an inner and
    /// an outer gameplay gate and its control panel, and outside a short tethered route to the antenna.
    /// Metres; the player spawns at the origin facing +Z. The airlock runs along -Z. Behind the console, at +Z,
    /// is the preflight pad: one supply pallet and one bay stack per equipment type, with a walkway between them.
    /// </summary>
    public sealed class RepairLoopHarness
    {
        public const float EyeHeightMetres = 1.65f;
        public const float ReachMetres = 2f;
        public const float InnerGateZ = -3f;
        public const float OuterGateZ = -7f;

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.02f, 0f);
        public static readonly Vector3 ConsolePosition = new Vector3(0f, 1.4f, 3f);
        public static readonly Vector3 PowerPanelPosition = new Vector3(-4f, 1.4f, 0f);
        public static readonly Vector3 LifeSupportPanelPosition = new Vector3(4f, 1.4f, 0f);
        public static readonly Vector3 SuitLockerPosition = new Vector3(-2.5f, 1.4f, -1.5f);
        public static readonly Vector3 AirlockControlsPosition = new Vector3(-1.35f, 1.4f, -5f);
        public static readonly Vector3 EvaExitPosition = new Vector3(0.9f, 1.4f, -6.75f);
        public static readonly Vector3 EvaReturnPosition = new Vector3(-0.9f, 1.4f, -7.3f);
        public static readonly Vector3 AntennaPosition = new Vector3(3f, 1.4f, -13.2f);
        public static readonly Vector3 ReentryPosition = new Vector3(0f, 0.02f, -6f);
        public const float PalletRowZ = 8f;
        public const float BayRowZ = 5f;
        public static readonly Vector3 PreflightSpawnPosition = new Vector3(0f, 0.02f, 6.5f);

        /// <summary>Catalog order, one column each from x = -6 to x = 6.</summary>
        public static readonly string[] CargoEquipmentIds =
        {
            AuroraMission.OxygenTank, AuroraMission.WaterTank, AuroraMission.FoodLocker, AuroraMission.BatteryModule,
            AuroraMission.SolarWing, AuroraMission.SparePartsKit, AuroraMission.Spectrometer,
        };

        public static readonly Vector3[] RouteWaypoints =
        {
            new Vector3(0f, 0.02f, -8f), new Vector3(3f, 0.02f, -8f), new Vector3(3f, 0.02f, -12f),
        };

        public GameObject Root { get; private set; }

        public MissionRuntime Runtime { get; private set; }

        public FirstPersonController Player { get; private set; }

        public InteractionResolver Resolver { get; private set; }

        public Transform ViewPivot { get; private set; }

        public Transform Spawn { get; private set; }

        public ScriptedInput Input { get; private set; }

        public MissionStatusListener Listener { get; private set; }

        public InteractableTarget Console { get; private set; }

        public InteractableTarget PowerPanel { get; private set; }

        public InteractableTarget LifeSupportPanel { get; private set; }

        public InteractableTarget SuitLocker { get; private set; }

        public InteractableTarget AirlockControls { get; private set; }

        public InteractableTarget EvaExit { get; private set; }

        public InteractableTarget EvaReturn { get; private set; }

        public InteractableTarget Antenna { get; private set; }

        public GameplayGate InnerGate { get; private set; }

        public GameplayGate OuterGate { get; private set; }

        public TetheredRoute Route { get; private set; }

        public Dictionary<string, InteractableTarget> CargoSupply { get; } = new Dictionary<string, InteractableTarget>();

        public Dictionary<string, InteractableTarget> CargoBay { get; } = new Dictionary<string, InteractableTarget>();

        public Transform PreflightSpawn { get; private set; }

        public MissionSession Session => Runtime.Session;

        public static float CargoColumnX(string equipmentId)
        {
            return -6f + (2f * System.Array.IndexOf(CargoEquipmentIds, equipmentId));
        }

        public static Vector3 PalletPosition(string equipmentId)
        {
            return new Vector3(CargoColumnX(equipmentId), 1.4f, PalletRowZ);
        }

        public static Vector3 BayStackPosition(string equipmentId)
        {
            return new Vector3(CargoColumnX(equipmentId), 1.4f, BayRowZ);
        }

        /// <summary>
        /// Builds the layout. With scripted input the player is driven by tests; for authoring (the saved harness
        /// scene) the real Input System bindings and the debug overlay are used instead. With preflight the player
        /// starts on the pad and may walk and load crates during Planning.
        /// </summary>
        public static RepairLoopHarness Build(bool authoring = false, bool preflight = false)
        {
            var harness = new RepairLoopHarness();
            harness.Root = new GameObject("RepairLoopHarness");
            harness.Root.SetActive(false);
            Transform root = harness.Root.transform;

            CreateBlock(root, "Floor", new Vector3(0f, -0.1f, -3.5f), new Vector3(14f, 0.2f, 24f));
            harness.Spawn = CreatePoint(root, "PlayerSpawn", SpawnPosition, 0f);

            harness.Console = CreateTarget(root, "CockpitConsole", InteractionIds.CockpitConsole, ConsolePosition, new Vector3(0.8f, 1f, 0.3f));
            harness.PowerPanel = CreateTarget(root, "PowerRepair", InteractionIds.RepairPower, PowerPanelPosition, new Vector3(0.3f, 1f, 0.8f));
            harness.LifeSupportPanel = CreateTarget(root, "LifeSupportRepair", InteractionIds.RepairLifeSupport, LifeSupportPanelPosition, new Vector3(0.3f, 1f, 0.8f));
            harness.SuitLocker = CreateTarget(root, "SuitLocker", InteractionIds.SuitLocker, SuitLockerPosition, new Vector3(0.3f, 1f, 0.8f));

            // Airlock corridor: side walls, inner gate towards the cabin, outer gate towards vacuum.
            CreateBlock(root, "AirlockWallLeft", new Vector3(-1.6f, 1.25f, -5f), new Vector3(0.2f, 2.5f, 5f));
            CreateBlock(root, "AirlockWallRight", new Vector3(1.6f, 1.25f, -5f), new Vector3(0.2f, 2.5f, 5f));
            harness.InnerGate = CreateGate(root, "AirlockInner", GateIds.AirlockInner, new Vector3(0f, 1.25f, InnerGateZ), new Vector3(3f, 2.5f, 0.2f));
            harness.OuterGate = CreateGate(root, "AirlockOuter", GateIds.AirlockOuter, new Vector3(0f, 1.25f, OuterGateZ), new Vector3(3f, 2.5f, 0.2f));
            harness.AirlockControls = CreateTarget(root, "AirlockControls", InteractionIds.AirlockControls, AirlockControlsPosition, new Vector3(0.3f, 1f, 0.6f));
            harness.EvaExit = CreateTarget(root, "EvaExit", InteractionIds.EvaExit, EvaExitPosition, new Vector3(0.6f, 1f, 0.3f));
            Transform reentry = CreatePoint(root, "EvaReentryPoint", ReentryPosition, 0f);

            // Outside: the tethered route and the antenna work site.
            harness.EvaReturn = CreateTarget(root, "EvaReturn", InteractionIds.EvaReturn, EvaReturnPosition, new Vector3(0.6f, 1f, 0.3f));
            harness.Antenna = CreateTarget(root, "AntennaRepair", InteractionIds.RepairAntenna, AntennaPosition, new Vector3(1f, 1f, 0.3f));
            var routeObject = new GameObject("EvaRoute");
            routeObject.transform.SetParent(root, false);
            var waypoints = new Transform[RouteWaypoints.Length];
            for (int i = 0; i < RouteWaypoints.Length; i++)
            {
                waypoints[i] = CreatePoint(routeObject.transform, "Waypoint" + i, RouteWaypoints[i], 0f);
            }

            harness.Route = routeObject.AddComponent<TetheredRoute>();
            harness.Route.Configure(waypoints, 0.5f, 1f);

            // Preflight pad: pallets in the far row, bay stacks in the near row, the player walks between them.
            harness.PreflightSpawn = CreatePoint(root, "PreflightSpawn", PreflightSpawnPosition, 0f);
            foreach (string equipmentId in CargoEquipmentIds)
            {
                harness.CargoSupply[equipmentId] = CreateTarget(root, "CargoSupply_" + equipmentId, InteractionIds.CargoSupply(equipmentId),
                    PalletPosition(equipmentId), new Vector3(0.8f, 1f, 0.6f));
                harness.CargoBay[equipmentId] = CreateTarget(root, "CargoBay_" + equipmentId, InteractionIds.CargoBay(equipmentId),
                    BayStackPosition(equipmentId), new Vector3(0.8f, 1f, 0.6f));
            }

            var rig = new GameObject("PlayerRig");
            rig.transform.SetParent(root, false);
            rig.transform.position = SpawnPosition;
            var body = rig.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;
            body.skinWidth = 0.03f;

            var pivot = new GameObject("ViewPivot");
            pivot.transform.SetParent(rig.transform, false);
            pivot.transform.localPosition = new Vector3(0f, EyeHeightMetres, 0f);
            harness.ViewPivot = pivot.transform;
            harness.Resolver = pivot.AddComponent<InteractionResolver>();
            harness.Resolver.Configure(pivot.transform, rig.transform, ReachMetres);

            harness.Player = rig.AddComponent<FirstPersonController>();
            harness.Player.Configure(pivot.transform, harness.Resolver);
            if (!authoring)
            {
                harness.Input = new ScriptedInput();
                harness.Player.InputSource = harness.Input;
            }

            var mission = new GameObject("Mission");
            mission.transform.SetParent(root, false);
            harness.Runtime = mission.AddComponent<MissionRuntime>();
            var targets = new List<InteractableTarget>
            {
                harness.Console, harness.PowerPanel, harness.LifeSupportPanel, harness.SuitLocker,
                harness.AirlockControls, harness.EvaExit, harness.EvaReturn, harness.Antenna,
            };
            foreach (string equipmentId in CargoEquipmentIds)
            {
                targets.Add(harness.CargoSupply[equipmentId]);
                targets.Add(harness.CargoBay[equipmentId]);
            }

            harness.Runtime.Configure(harness.Player, harness.Spawn, targets);
            harness.Runtime.ConfigureEva(new[] { harness.InnerGate, harness.OuterGate }, harness.Route, reentry);
            harness.Runtime.ConfigurePreflight(preflight, harness.PreflightSpawn);
            harness.Runtime.RestartMode = MissionRestartMode.RebuildInPlace;
            harness.Listener = mission.AddComponent<MissionStatusListener>();
            harness.Listener.Configure(harness.Runtime, harness.Resolver, authoring);

            harness.Root.SetActive(true);
            Physics.SyncTransforms();
            return harness;
        }

        /// <summary>Launches the default loadout (Planning to Intro).</summary>
        public void Launch()
        {
            Require(Session.Planner.TryLaunch(), "launch");
        }

        /// <summary>Launches, applies the impact and acknowledges its cinematic: the mission is in Emergency.</summary>
        public void ReachEmergency()
        {
            Launch();
            Require(Session.Director.TryCompleteCockpitCheck(), "impact");
            if (Session.Cinematics.IsWaiting)
            {
                Session.Events.Publish(new CinematicFinished(Session.Cinematics.ActiveCinematic, Session.Cinematics.ActiveSequenceId, false));
            }
        }

        /// <summary>Emergency with power and life support repaired through the domain: the mission is in Repair.</summary>
        public void ReachStabilized()
        {
            ReachEmergency();
            Require(Session.Systems.Power.TryRepair(Session.Resources), "power repair");
            Require(Session.Systems.LifeSupport.TryRepair(Session.Resources), "life-support repair");
        }

        /// <summary>Stands the player on the floor at (x, z), facing a world point, and syncs physics. Leaves any route.</summary>
        public void PlacePlayerFacing(float x, float z, Vector3 lookAt)
        {
            Vector3 feet = new Vector3(x, SpawnPosition.y, z);
            Player.Teleport(feet, YawTowards(feet, lookAt));
            Physics.SyncTransforms();
        }

        /// <summary>Turns the player towards a world point without moving (also valid on the EVA route).</summary>
        public void FacePlayerTowards(Vector3 lookAt)
        {
            Player.transform.rotation = Quaternion.Euler(0f, YawTowards(Player.transform.position, lookAt), 0f);
        }

        public GameObject AddWall(Vector3 center, Vector3 size)
        {
            GameObject wall = CreateBlock(Root.transform, "OccluderWall", center, size);
            Physics.SyncTransforms();
            return wall;
        }

        /// <summary>Removes the layout immediately, so the next harness never overlaps this one's mission runtime.</summary>
        public void Destroy()
        {
            if (Root != null)
            {
                Object.DestroyImmediate(Root);
                Root = null;
            }
        }

        private static float YawTowards(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        private static void Require(CommandResult result, string step)
        {
            if (!result.Succeeded)
            {
                throw new System.InvalidOperationException("Harness " + step + " failed: " + result.Explanation);
            }
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position, float yawDegrees)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            return point.transform;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = center;
            block.transform.localScale = size;
            return block;
        }

        private static InteractableTarget CreateTarget(Transform parent, string name, string interactionId, Vector3 center, Vector3 size)
        {
            GameObject block = CreateBlock(parent, name, center, size);
            var target = block.AddComponent<InteractableTarget>();
            target.InteractionId = interactionId;
            return target;
        }

        private static GameplayGate CreateGate(Transform parent, string name, string gateId, Vector3 center, Vector3 size)
        {
            var gateObject = new GameObject(name);
            gateObject.transform.SetParent(parent, false);
            gateObject.transform.position = center;
            GameObject slab = CreateBlock(gateObject.transform, "Slab", center, size);
            var gate = gateObject.AddComponent<GameplayGate>();
            gate.Configure(gateId, slab.GetComponent<Collider>(), slab);
            return gate;
        }
    }
}

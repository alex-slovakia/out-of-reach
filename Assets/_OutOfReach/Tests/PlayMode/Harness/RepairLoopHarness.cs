using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// TEST FIXTURE. A grey-box room built from primitives: a floor, a cockpit console in front of the spawn and
    /// two repair panels to the sides, plus the player rig and the mission runtime wired exactly the way an
    /// integrator would wire them (through the Configure methods that set the serialized fields).
    /// Metres; the player spawns at the origin facing +Z.
    /// </summary>
    public sealed class RepairLoopHarness
    {
        public const float EyeHeightMetres = 1.65f;
        public const float ReachMetres = 2f;

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.02f, 0f);
        public static readonly Vector3 ConsolePosition = new Vector3(0f, 1.4f, 3f);
        public static readonly Vector3 PowerPanelPosition = new Vector3(-4f, 1.4f, 0f);
        public static readonly Vector3 LifeSupportPanelPosition = new Vector3(4f, 1.4f, 0f);

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

        public MissionSession Session => Runtime.Session;

        /// <summary>
        /// Builds the room. With scripted input the player is driven by tests; for authoring (the saved harness
        /// scene) the real Input System bindings and the debug overlay are used instead.
        /// </summary>
        public static RepairLoopHarness Build(bool authoring = false)
        {
            var harness = new RepairLoopHarness();
            harness.Root = new GameObject("RepairLoopHarness");
            harness.Root.SetActive(false);
            Transform root = harness.Root.transform;

            CreateBlock(root, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(14f, 0.2f, 14f));
            harness.Spawn = new GameObject("PlayerSpawn").transform;
            harness.Spawn.SetParent(root, false);
            harness.Spawn.position = SpawnPosition;

            harness.Console = CreateTarget(root, "CockpitConsole", InteractionIds.CockpitConsole, ConsolePosition, new Vector3(0.8f, 1f, 0.3f));
            harness.PowerPanel = CreateTarget(root, "PowerRepair", InteractionIds.RepairPower, PowerPanelPosition, new Vector3(0.3f, 1f, 0.8f));
            harness.LifeSupportPanel = CreateTarget(root, "LifeSupportRepair", InteractionIds.RepairLifeSupport, LifeSupportPanelPosition, new Vector3(0.3f, 1f, 0.8f));

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
            harness.Runtime.Configure(harness.Player, harness.Spawn, new[] { harness.Console, harness.PowerPanel, harness.LifeSupportPanel });
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
            CommandResult launch = Session.Planner.TryLaunch();
            if (!launch.Succeeded)
            {
                throw new System.InvalidOperationException("Harness launch failed: " + launch.Explanation);
            }
        }

        /// <summary>Launches, applies the impact and acknowledges its cinematic: the mission is in Emergency.</summary>
        public void ReachEmergency()
        {
            Launch();
            CommandResult check = Session.Director.TryCompleteCockpitCheck();
            if (!check.Succeeded)
            {
                throw new System.InvalidOperationException("Harness impact failed: " + check.Explanation);
            }

            if (Session.Cinematics.IsWaiting)
            {
                Session.Events.Publish(new CinematicFinished(Session.Cinematics.ActiveCinematic, Session.Cinematics.ActiveSequenceId, false));
            }
        }

        /// <summary>Stands the player on the floor at (x, z), facing a world point, and syncs physics.</summary>
        public void PlacePlayerFacing(float x, float z, Vector3 lookAt)
        {
            Vector3 feet = new Vector3(x, SpawnPosition.y, z);
            Vector3 toTarget = lookAt - feet;
            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            Player.Teleport(feet, yaw);
            Physics.SyncTransforms();
        }

        public GameObject AddWall(Vector3 center, Vector3 size)
        {
            GameObject wall = CreateBlock(Root.transform, "OccluderWall", center, size);
            Physics.SyncTransforms();
            return wall;
        }

        /// <summary>Removes the room immediately, so the next harness never overlaps this one's mission runtime.</summary>
        public void Destroy()
        {
            if (Root != null)
            {
                Object.DestroyImmediate(Root);
                Root = null;
            }
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
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Tests.Editor
{
    /// <summary>Validates the committed fixtures: serialized references are intact and no script is missing.</summary>
    public sealed class HarnessFixtureTests
    {
        private static void AssertNoMissingScripts(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, "missing script on " + child.name);
            }
        }

        [Test]
        public void PlayerRigPrefab_IsSelfContained_AndCarriesNoCameraOrListener()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepairLoopHarnessBuilder.PlayerRigPrefabPath);
            Assert.That(prefab, Is.Not.Null, RepairLoopHarnessBuilder.PlayerRigPrefabPath);
            AssertNoMissingScripts(prefab);

            var controller = prefab.GetComponent<FirstPersonController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(controller.ViewPivot, Is.Not.Null, "view pivot reference");
            Assert.That(controller.ViewPivot.IsChildOf(prefab.transform), Is.True);
            Assert.That(controller.ViewPivot.localPosition.y, Is.EqualTo(1.65f).Within(0.001f), "eye height in metres");
            Assert.That(controller.Interaction, Is.Not.Null, "interaction resolver reference");
            Assert.That(controller.Interaction.ReachMetres, Is.EqualTo(2f).Within(0.001f));
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty, "Bootstrap owns the only gameplay camera");
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true), Is.Empty, "Bootstrap owns the only audio listener");
        }

        [Test]
        public void HarnessScene_HasValidBindings_KnownIds_AndNoMissingScripts()
        {
            Scene scene = EditorSceneManager.OpenScene(RepairLoopHarnessBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var runtimes = new List<MissionRuntime>();
                var cameras = new List<Camera>();
                var listeners = new List<AudioListener>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AssertNoMissingScripts(root);
                    runtimes.AddRange(root.GetComponentsInChildren<MissionRuntime>(true));
                    cameras.AddRange(root.GetComponentsInChildren<Camera>(true));
                    listeners.AddRange(root.GetComponentsInChildren<AudioListener>(true));
                }

                Assert.That(runtimes.Count, Is.EqualTo(1), "exactly one simulation driver");
                Assert.That(cameras.Count, Is.EqualTo(1), "one harness camera");
                Assert.That(listeners.Count, Is.EqualTo(1), "one audio listener");

                MissionRuntime runtime = runtimes[0];
                Assert.That(runtime.Player, Is.Not.Null, "player binding");
                Assert.That(runtime.Player.ViewPivot, Is.Not.Null);
                Assert.That(runtime.Player.Interaction, Is.Not.Null);

                using (var session = new MissionSession())
                {
                    Assert.That(runtime.InteractionTargets.Count, Is.EqualTo(session.Interactions.All.Count), "one scene target per domain interaction");
                    foreach (InteractableTarget target in runtime.InteractionTargets)
                    {
                        Assert.That(target, Is.Not.Null, "interaction target binding");
                        Assert.That(session.Interactions.TryGet(target.InteractionId, out IInteractable unused), Is.True, "unknown interaction ID " + target.InteractionId);
                        Assert.That(target.GetComponentInChildren<Collider>(), Is.Not.Null, target.name + " needs a collider to be targeted");
                    }

                    string[] ids = runtime.InteractionTargets.Select(target => target.InteractionId).ToArray();
                    Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "each interaction is bound once");
                }

                string[] gateIds = runtime.Gates.Select(gate => gate != null ? gate.GateId : null).ToArray();
                Assert.That(gateIds, Is.EquivalentTo(new[] { GateIds.AirlockInner, GateIds.AirlockOuter }));
                foreach (GameplayGate gate in runtime.Gates)
                {
                    Assert.That(gate.Blocker, Is.Not.Null, gate.name + " needs a blocking collider");
                }

                Assert.That(runtime.EvaRoute, Is.Not.Null, "EVA route binding");
                Assert.That(runtime.EvaRoute.IsValid, Is.True, "the route needs at least two waypoints");
                Assert.That(runtime.EvaRoute.LengthMetres, Is.GreaterThan(1f));
                Assert.That(runtime.EvaReentryPoint, Is.Not.Null, "re-entry point binding");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

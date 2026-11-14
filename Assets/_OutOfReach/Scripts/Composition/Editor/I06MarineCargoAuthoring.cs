using System;
using System.Linq;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation;
using OutOfReach.Presentation.Cargo;
using OutOfReach.Presentation.Cargo.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Scene-only marine placement of the existing C05 stage. No inventory, deadline, movement or launch authority.</summary>
    public static class I06MarineCargoAuthoring
    {
        public const float UpperCellPitchMetres = .37f;
        public const float BayInboardOffsetMetres = .04f;
        public const float TopShelfY = 1.20f;
        public const float BayTargetX = .60f;
        public const float BayTargetY = 1.36f;
        public const float BayLabelY = 1.02f;
        public const float SourceColumnSpreadMetres = .02f;
        private const float StationFrontZ = -1.48f;
        private static readonly string[] SourceBases = { "A", "B", "C", "D", "A", "B", "C" };

        /// <summary>Call instead of the old cargo Create call, once per newly authored scene. Returned references use the existing root composition API.</summary>
        public static CargoStageAuthoring.Result Create(I06MarineAuthoring.Result marine, Transform gameplayCamera, Font labelFont)
        {
            if (Application.isPlaying || marine == null || !marine.Root || !marine.DockRoot || !marine.VehicleRoot
                || !gameplayCamera || !labelFont || EditorUtility.IsPersistent(marine.Root))
                throw new ArgumentException("Bind the actual authored marine scene result, gameplay camera and existing project font in Edit mode.");
            if ((marine.Root.lossyScale - Vector3.one).sqrMagnitude > 1e-8f || (gameplayCamera.lossyScale - Vector3.one).sqrMagnitude > 1e-8f)
                throw new InvalidOperationException("Marine cargo and its carried-case rig require unchanged metre scale.");
            if (marine.Root.GetComponentInChildren<CargoVisualPresenter>(true)
                || gameplayCamera.Find("Preflight cargo settling") || gameplayCamera.Find("Preflight work-glove presenter"))
                throw new InvalidOperationException("A cargo stage/held rig already exists. Replace the original Create call; do not create parallel cargo presenters.");
            if (marine.CargoDropBases == null || marine.CargoDropBases.Length != 7 || marine.CargoDropInteractionSurfaces == null
                || marine.CargoDropInteractionSurfaces.Length != 7 || !CargoStageAuthoring.EquipmentIds.SequenceEqual(I06MarineAuthoring.EquipmentIds))
                throw new InvalidOperationException("Use the exact seven-type marine import result and canonical C05 equipment order.");
            foreach (string name in new[] { "A", "B", "C", "D" })
                if (!marine.DockAnchors.ContainsKey("CargoCaseReference_" + name)) throw new InvalidOperationException("Missing authored source base " + name);

            CargoStageAuthoring.Result stage = CargoStageAuthoring.Create(marine.Root, gameplayCamera, labelFont);
            stage.Root.name = "Marine physical cargo - existing C05 authority";
            stage.SupplyRoot.SetParent(marine.DockRoot, false);
            stage.AboardRoot.SetParent(marine.VehicleRoot, false);
            stage.Spawn.SetPositionAndRotation(marine.Spawn.position, marine.Spawn.rotation);
            Material graphite = AssetDatabase.LoadAssetAtPath<Material>(CargoStageAuthoring.PropRoot + "/Materials/MAT_Cargo_Graphite.mat");
            if (!graphite) throw new InvalidOperationException("Existing cargo graphite material missing.");
            AddStationStand(stage.SupplyRoot, graphite);
            for (int i = 0; i < stage.Slots.Length; i++)
            {
                CargoVisualPresenter.Slot slot = stage.Slots[i];
                Transform sourceBase = marine.DockAnchors["CargoCaseReference_" + SourceBases[i]];
                Vector3 source = marine.DockRoot.InverseTransformPoint(sourceBase.position);
                source.x += source.x < -2.65f ? -SourceColumnSpreadMetres : SourceColumnSpreadMetres;
                if (i >= 4) { source.y += UpperCellPitchMetres; AddUpperShelf(stage.SupplyRoot, source, graphite, slot.EquipmentId); }
                slot.SupplyCase.transform.SetPositionAndRotation(marine.DockRoot.TransformPoint(source), marine.DockRoot.rotation * Quaternion.Euler(0, 180, 0));
                slot.SupplyCase.transform.localScale = Vector3.one;
                float columnX = i % 2 == 0 ? -2.97f : -2.34f;
                float rowY = .37f + .40f * (i / 2);
                Vector3 marker = new Vector3(columnX, rowY, StationFrontZ);
                var supplyTarget = stage.Targets.Single(t => t.InteractionId == "cargo-supply:" + slot.EquipmentId);
                supplyTarget.transform.SetPositionAndRotation(marine.DockRoot.TransformPoint(marker), marine.DockRoot.rotation * Quaternion.Euler(0, 90, 0));
                // Trigger local X remains its 0.12m depth; rotate that depth onto the rack front normal.
                PlaceLabel(slot.SupplyLabel, marine.DockRoot.TransformPoint(marker + new Vector3(0, 0, -.067f)), marine.DockRoot.rotation);

                Transform bayBase = marine.CargoDropBases[i], surface = marine.CargoDropInteractionSurfaces[i];
                Vector3 forward = surface.forward.normalized;
                Vector3 localBay = marine.Root.InverseTransformPoint(bayBase.position);
                // Original 590mm handled width cannot run along the 550mm cell pitch. Keep full size, with handles across rack depth.
                localBay += marine.Root.InverseTransformDirection(forward) * BayInboardOffsetMetres;
                localBay.y = TopShelfY;
                slot.AboardCase.transform.SetPositionAndRotation(marine.Root.TransformPoint(localBay),
                    marine.Root.rotation * Quaternion.Euler(0, localBay.x < 0 ? 0 : 180, 0));
                slot.AboardCase.transform.localScale = Vector3.one;
                var bayTarget = stage.Targets.Single(t => t.InteractionId == "cargo-bay:" + slot.EquipmentId);
                Vector3 localTarget = marine.Root.InverseTransformPoint(surface.position);
                localTarget.x = localBay.x < 0 ? -BayTargetX : BayTargetX;
                localTarget.y = BayTargetY;
                bayTarget.transform.SetPositionAndRotation(marine.Root.TransformPoint(localTarget), marine.Root.rotation);
                slot.DestinationAnchor = bayTarget.transform; // Existing destination guide reads this exact real target.
                Vector3 localLabel = marine.Root.InverseTransformPoint(surface.position + forward * .055f);
                localLabel.y = BayLabelY;
                PlaceLabel(slot.AboardLabel, marine.Root.TransformPoint(localLabel), Quaternion.LookRotation(-forward, marine.Root.up));
                CheckCaseBounds(slot.SupplyCase, false);
                AddAboardCaseCollider(slot.AboardCase);
                CheckCaseBounds(slot.AboardCase, true);
            }
            // Aboard solids follow those exact case GameObjects: existing presenter activation controls their lifetime.
            // No changes to carried case/cradle/hands, trigger dimensions, inventory or scene-state bindings.
            Validate(stage, marine, gameplayCamera.GetComponentInParent<FirstPersonController>());
            return stage;
        }

        /// <summary>Feet position for all seven source fronts, outside the original rack/cleat proxies. QA reference, not a player command.</summary>
        public static Vector3 SupplyApproach(I06MarineAuthoring.Result marine) => marine.DockRoot.TransformPoint(new Vector3(-2.65f, .04f, -2.10f));
        /// <summary>Feet on the capsule centre aisle at the actual corresponding bay Z. QA reference only.</summary>
        public static Vector3 BayApproach(I06MarineAuthoring.Result marine, int index)
        {
            if (index < 0 || index >= 7) throw new ArgumentOutOfRangeException(nameof(index));
            Vector3 cell = marine.Root.InverseTransformPoint(marine.CargoDropInteractionSurfaces[index].position);
            return marine.Root.TransformPoint(new Vector3(0, .035f, cell.z));
        }
        private static void PlaceLabel(TextMesh label, Vector3 worldPoint, Quaternion worldRotation)
        {
            label.transform.SetPositionAndRotation(worldPoint, worldRotation);
            // Fit actual native TextMesh bounds for a two-digit count; never guess world text scale or change case scale.
            string original = label.text;
            label.text = original.Split('\n')[0] + "\nABOARD x99";
            label.GetComponent<ShipLabelAtlasBinder>().RefreshAtlas();
            Vector3 size = label.GetComponent<MeshRenderer>().localBounds.size;
            if (size.x <= 0 || size.y <= 0 || float.IsNaN(size.x) || float.IsNaN(size.y))
                throw new InvalidOperationException("Native TextMesh bounds unavailable for marine cargo label " + original);
            label.characterSize *= Mathf.Min(1, Mathf.Min(.43f / size.x, .105f / size.y));
            label.text = original;
            label.GetComponent<ShipLabelAtlasBinder>().RefreshAtlas();
        }
        private static void AddUpperShelf(Transform supply, Vector3 floor, Material material, string equipmentId)
        {
            // Independent support avoids making an upper station float when another type's lower supply becomes empty.
            Box(supply, "Upper supply cell " + equipmentId, floor + new Vector3(0, -.008f, 0), new Vector3(.61f, .016f, .40f), material);
            foreach (float x in new[] { -.305f, .305f }) foreach (float z in new[] { -.20f, .20f })
                Box(supply, "Upper cell support " + equipmentId, floor + new Vector3(x, -UpperCellPitchMetres * .5f, z),
                    new Vector3(.012f, UpperCellPitchMetres, .012f), material);
        }
        private static void AddStationStand(Transform supply, Material material)
        {
            // Small attached station frame, outside the 2.2m central route. Existing root-owned labels retain all counts.
            foreach (float x in new[] { -3.235f, -2.075f })
                Box(supply, "Source station label upright", new Vector3(x, .85f, StationFrontZ + .025f), new Vector3(.018f, 1.66f, .018f), material);
            foreach (float y in new[] { .22f, 1.67f })
                Box(supply, "Source station label rail", new Vector3(-2.655f, y, StationFrontZ + .025f), new Vector3(1.178f, .018f, .018f), material);
        }
        private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube); node.name = name; node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition; node.transform.localScale = size; node.layer = 2;
            Object.DestroyImmediate(node.GetComponent<Collider>()); node.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static Bounds MeshBounds(GameObject root, Transform space)
        {
            bool first = true; Bounds result = default;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("No original meshes on cargo case."); return result;
        }
        private static void AddAboardCaseCollider(GameObject value)
        {
            if (EditorUtility.IsPersistent(value) || value.GetComponentsInChildren<Collider>(true).Length != 0
                || value.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Add the marine solid only to a fresh scene case instance.");
            Bounds bounds = MeshBounds(value, value.transform);
            // One conservative box includes the real 34mm handles; no prefab/FBX modification or Rigidbody.
            value.layer = 0;
            BoxCollider solid = value.AddComponent<BoxCollider>();
            solid.center = bounds.center;
            solid.size = bounds.size;
            solid.isTrigger = false;
        }
        private static void CheckCaseBounds(GameObject value, bool aboard)
        {
            Bounds bounds = MeshBounds(value, value.transform);
            Collider[] colliders = value.GetComponentsInChildren<Collider>(true);
            BoxCollider solid = value.GetComponent<BoxCollider>();
            bool physicsValid = aboard
                ? colliders.Length == 1 && solid && solid.enabled && !solid.isTrigger && value.layer == 0
                    && (solid.center - bounds.center).sqrMagnitude < 1e-8f && (solid.size - bounds.size).sqrMagnitude < 1e-8f
                : colliders.Length == 0;
            if ((value.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-8f || Mathf.Abs(bounds.size.x - .590f) > .003f
                || Mathf.Abs(bounds.size.y - .319f) > .003f || Mathf.Abs(bounds.size.z - .3541384f) > .003f
                || !physicsValid || value.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Expected original full-size case with exactly one scene-only aboard box, no source/held physics: " + bounds.size);
        }
        private static void Validate(CargoStageAuthoring.Result stage, I06MarineAuthoring.Result marine, FirstPersonController player)
        {
            if (stage.Slots.Length != 7 || stage.Targets.Length != 14 || stage.Targets.Select(t => t.InteractionId).Distinct().Count() != 14)
                throw new InvalidOperationException("Marine cargo must reuse exactly seven slots/fourteen targets.");
            Physics.SyncTransforms();
            for (int i = 0; i < 7; i++)
            {
                var slot = stage.Slots[i]; Bounds bay = MeshBounds(slot.AboardCase, marine.Root);
                if (bay.min.x < 0 ? bay.max.x > -.65f + .002f : bay.min.x < .65f - .002f)
                    throw new InvalidOperationException("Full handled case must preserve the 1.30m visual/solid aisle and protected 1.20m corridor: " + slot.EquipmentId);
                if (Mathf.Abs(bay.min.y - TopShelfY) > .002f || bay.max.y > 1.521f
                    || Mathf.Max(Mathf.Abs(bay.min.x), Mathf.Abs(bay.max.x)) > 1.242f
                    || !slot.AboardCase.activeInHierarchy)
                    throw new InvalidOperationException("Validate all seven loaded case solids at the authored top shelf before session binding.");
                foreach (int other in Enumerable.Range(i + 1, 6 - i))
                {
                    if (bay.Intersects(MeshBounds(stage.Slots[other].AboardCase, marine.Root))
                        || MeshBounds(slot.SupplyCase, marine.DockRoot).Intersects(MeshBounds(stage.Slots[other].SupplyCase, marine.DockRoot)))
                        throw new InvalidOperationException("Full-size case bounds overlap: " + slot.EquipmentId + " / " + stage.Slots[other].EquipmentId);
                }
                foreach (bool supply in new[] { true, false })
                {
                    var target = stage.Targets.Single(t => t.InteractionId == (supply ? "cargo-supply:" : "cargo-bay:") + slot.EquipmentId);
                    BoxCollider trigger = target.GetComponent<BoxCollider>();
                    if (!trigger || !trigger.isTrigger || (trigger.size - new Vector3(.12f, .36f, .42f)).sqrMagnitude > 1e-8f)
                        throw new InvalidOperationException("Preserve existing station trigger dimensions.");
                    Vector3 eye = (supply ? SupplyApproach(marine) : BayApproach(marine, i)) + marine.Root.up * 1.65f;
                    RaycastHit[] hits = Physics.RaycastAll(eye, (trigger.bounds.center - eye).normalized, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                        .Where(h => !player || !h.collider.transform.IsChildOf(player.transform)).OrderBy(h => h.distance).ToArray();
                    Collider first = hits.FirstOrDefault(h => !h.collider.isTrigger || h.collider.GetComponentInParent<InteractableTarget>()).collider;
                    if (!first || first.GetComponentInParent<InteractableTarget>() != target)
                        throw new InvalidOperationException("Real two-metre approach ray is blocked for " + target.InteractionId + "; first=" + (first ? first.name : "none"));
                }
            }
            for (int i = 0; i < stage.Targets.Length; i++) for (int j = i + 1; j < stage.Targets.Length; j++)
                if (stage.Targets[i].GetComponent<Collider>().bounds.Intersects(stage.Targets[j].GetComponent<Collider>().bounds))
                    throw new InvalidOperationException("Marine station trigger bounds overlap.");
            Debug.Log("MARINE_CARGO_LAYOUT_VALIDATED | 7 original full-size cases per side, 14 unique real targets/rays, seven exact case-owned solids, 1.30m aisle, four source bases, one C05 presenter/hands rig. Authoring geometry/rays only; runtime movement/deadline acceptance still required.");
        }
    }
}

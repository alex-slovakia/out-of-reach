using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Habitat
{
    /// <summary>
    /// R36 presentation only: a row of sealed meal pouches on hook-and-loop below the starboard ration cords
    /// (DP02_HAB_StrapStow_R_Z1325). Each pouch stands for an equal share of the packaged food held at binding
    /// (launch stores); pouches disappear one by one as Core's authoritative FoodKg falls, the last one only when the
    /// store is empty. Reads IResourceSystem; never consumes, simulates or predicts food.
    /// </summary>
    public sealed class HabitatStoredRations
    {
        public const string ObjectName = "Stored ration portions (authoritative FoodKg)";
        public const string PouchMeshName = "GC_MealPouch_Sealed";
        public const int Slots = 8;

        // Habitat-root (= ship) frame of the starboard blanket bay at z 13.25: quilt face plus 3.5 mm at x 2.3265
        // (DetailPass02Layout.json). Below the existing ration/drink cords (pouch centres y 0.62, bottom 0.545).
        private const float HostX = 2.3265f, BayZ = 13.25f, RowY = .42f, Spacing = .152f, WallGap = .002f;

        private readonly GameObject root;
        private readonly GameObject[] pouches;
        private IResourceSystem resources;
        private double referenceKg;
        private int shown = -1;

        private HabitatStoredRations(GameObject root, GameObject[] pouches)
        {
            this.root = root; this.pouches = pouches;
        }

        /// <summary>Pure mapping, tested: share-of-launch-stores pouches, never more than are left, 0 only when empty.</summary>
        public static int VisiblePortions(double foodKg, double referenceKg, int slots)
        {
            if (slots <= 0 || double.IsNaN(foodKg) || foodKg <= 1e-6) return 0;
            if (double.IsNaN(referenceKg) || referenceKg <= 1e-6 || double.IsInfinity(referenceKg)) return slots;
            double share = Math.Min(1d, foodKg / referenceKg);
            int count = (int)Math.Ceiling(share * slots - 1e-9);
            return Math.Max(1, Math.Min(slots, count));
        }

        /// <summary>Builds the inactive display under the habitat root from the meal presentation's sealed pouch.</summary>
        public static HabitatStoredRations Build(Transform habitatRoot)
        {
            if (!habitatRoot) return null;
            MeshFilter source = null;
            foreach (MeshFilter filter in habitatRoot.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == PouchMeshName && filter.sharedMesh) { source = filter; break; }
            var sourceRenderer = source ? source.GetComponent<MeshRenderer>() : null;
            if (!source || !sourceRenderer) return null;

            var root = new GameObject(ObjectName);
            root.SetActive(false);
            root.layer = habitatRoot.gameObject.layer;
            root.transform.SetParent(habitatRoot, false);
            // Face into the room (-x), pouch up = ship up; local +x runs along the bay toward -z.
            root.transform.localPosition = new Vector3(HostX, RowY, BayZ);
            root.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

            // Prop space: y up the pouch, +z label side, 0.141 x 0.178 x 0.029 m (GalleyConsumablesV01 README).
            Bounds b = source.sharedMesh.bounds;
            var pouches = new GameObject[Slots];
            for (int i = 0; i < Slots; i++)
            {
                var pouch = new GameObject("Stored meal pouch " + (i + 1));
                pouch.layer = root.layer;
                pouch.transform.SetParent(root.transform, false);
                float x = (i - (Slots - 1) * .5f) * Spacing;
                // Label toward the room: prop +z must point along -root.forward, so turn 180 degrees about up.
                pouch.transform.localRotation = Quaternion.Euler(0f, 180f, (i % 3 - 1) * 2.5f);
                Vector3 centreOffset = pouch.transform.localRotation * b.center;
                pouch.transform.localPosition = new Vector3(x, 0f, -(b.extents.z + WallGap)) - centreOffset;
                pouch.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = pouch.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                pouches[i] = pouch;
            }
            return new HabitatStoredRations(root, pouches);
        }

        public bool IsBound => resources != null;

        public void Bind(IResourceSystem actualResources)
        {
            resources = actualResources;
            referenceKg = resources != null ? resources.GetQuantity(ResourceId.FoodKg) : 0d;
            shown = -1;
            Refresh();
        }

        public void Clear()
        {
            resources = null; referenceKg = 0d; shown = -1;
            if (root) root.SetActive(false);
        }

        public void Refresh()
        {
            if (!root) return;
            if (resources == null) { if (root.activeSelf) root.SetActive(false); return; }
            double food = resources.GetQuantity(ResourceId.FoodKg);
            // Stores loaded after binding (or a harvest credit) raise the reference instead of overfilling the row.
            if (food > referenceKg) referenceKg = food;
            int count = VisiblePortions(food, referenceKg, Slots);
            if (!root.activeSelf) root.SetActive(true);
            if (count == shown) return;
            for (int i = 0; i < pouches.Length; i++)
                if (pouches[i]) pouches[i].SetActive(i < count);
            shown = count;
        }

        public void Destroy()
        {
            Clear();
            if (root) UnityEngine.Object.Destroy(root);
        }
    }
}

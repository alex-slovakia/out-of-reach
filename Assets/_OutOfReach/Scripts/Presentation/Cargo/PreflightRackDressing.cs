using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>
    /// R02 + crate legibility, presentation only. On the preflight supply rack each crate owns a shelf, two label
    /// brackets, two shelf supports and a manifest sign (I06CargoPlacardAuthoring). When CargoVisualPresenter hides a
    /// crate because Core's supply for it is empty, this hides that crate's holders and sign with it; other crates
    /// stay intact. Support posts are run down to the rack base so no upper level floats when a lower one goes.
    /// Manifest signs get a plate that meets its brackets and sharper, larger text. Reads presentation state only.
    /// Installs itself after scene load; nothing in the scene or in CargoVisualPresenter changes.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class PreflightRackDressing : MonoBehaviour
    {
        public const string SupplyRootName = "Temporary preflight supply";
        public const string SupportsName = "Aligned supply cells V02";
        public const string LabelName = "Manifest label";
        public const string PlateName = "Printed manifest plate";
        private const string TargetPrefix = "cargo-supply:";

        // Brackets sit at +/-.285 m with .012 m width: a .582 m plate meets both instead of floating between them.
        private static readonly Vector3 RackPlateScale = new Vector3(.582f, CargoSignText.PlateHeight, .012f);

        private sealed class Cell
        {
            public string Id;
            public GameObject Case;
            public readonly List<Renderer> Holders = new List<Renderer>();
            public bool? Shown;
        }

        private readonly List<Cell> cells = new List<Cell>();
        private bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterFirstLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) Install(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        /// <summary>Adds the dressing to every authored supply rack in the scene. Idempotent.</summary>
        public static int Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            int installed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != SupplyRootName || !t.Find(SupportsName)) continue;
                if (!t.GetComponent<PreflightRackDressing>()) t.gameObject.AddComponent<PreflightRackDressing>();
                installed++;
            }
            // Short, large, high-contrast manifest signs everywhere (rack, aboard, carried) and a matching name plate
            // over each case's printed label window; the dock's modelled legend gets a readable overlay.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>(true))
                if (label.name == LabelName && !label.GetComponent<CargoSignText>()) label.gameObject.AddComponent<CargoSignText>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == CargoCaseNamePlate.CaseName) CargoCaseNamePlate.Attach(t);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == CargoSignText.DockLegendName) CargoSignText.OverlayLegend(t);
            return installed;
        }

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            Transform supports = transform.Find(SupportsName);
            if (!supports) return;
            var targets = new List<Transform>();
            var labels = new List<Transform>();
            var others = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child == supports) continue;
                if (child.name.StartsWith(TargetPrefix, StringComparison.Ordinal)) targets.Add(child);
                else if (child.name == LabelName) labels.Add(child);
                else if (!child.name.StartsWith("Source station", StringComparison.Ordinal)) others.Add(child);
            }
            Transform dock = transform.parent ? transform.parent : transform;
            foreach (Transform target in targets)
            {
                var cell = new Cell { Id = target.name.Substring(TargetPrefix.Length) };
                // Authored association (I06CargoPlacardAuthoring): crate floor = marker + (0, -.175, .38) in dock space.
                Vector3 marker = dock.InverseTransformPoint(target.position);
                Vector3 expectedCase = dock.TransformPoint(marker + new Vector3(0f, -.175f, .38f));
                cell.Case = Nearest(others, expectedCase, .3f)?.gameObject;
                Transform label = Nearest(labels, target.position, .15f);
                if (label)
                {
                    cell.Holders.AddRange(label.GetComponentsInChildren<Renderer>(true));
                    Transform plate = label.Find(PlateName);
                    if (plate) plate.localScale = RackPlateScale;
                }
                foreach (Transform part in supports)
                    if (part.name.StartsWith(cell.Id + " ", StringComparison.Ordinal))
                        cell.Holders.AddRange(part.GetComponentsInChildren<Renderer>(true));
                if (cell.Case) cells.Add(cell);
            }
            GroundSupports(supports);
            if (labels.Count > 0) AddHeader(labels[0]);
        }

        public const string HeaderName = "Rack header sign (legible)";

        /// <summary>A plain, high-contrast header resting on the top rail of the authored sign frame.</summary>
        private void AddHeader(Transform templateLabel)
        {
            if (transform.Find(HeaderName)) return;
            Transform topRail = null;
            foreach (Transform child in transform)
                if (child.name == "Source station label rail" && (!topRail || child.localPosition.y > topRail.localPosition.y))
                    topRail = child;
            if (!topRail) return;
            // Clone an authored sign: same font, depth-tested label material, atlas binder and plate material.
            GameObject header = Instantiate(templateLabel.gameObject, transform);
            header.name = HeaderName;
            header.transform.localRotation = templateLabel.localRotation;
            float railTop = topRail.localPosition.y + topRail.localScale.y * .5f;
            float railFront = topRail.localPosition.z - topRail.localScale.z * .5f;
            const float plateHeight = .2f, plateDepth = .012f, plateOffset = .008f;
            header.transform.localPosition = new Vector3(topRail.localPosition.x, railTop + plateHeight * .5f,
                railFront - plateOffset - plateDepth * .5f);
            Transform plate = header.transform.Find(PlateName);
            if (plate) plate.localScale = new Vector3(topRail.localScale.x - .008f, plateHeight, plateDepth);
            var text = header.GetComponent<TextMesh>();
            if (!text) return;
            // Not a manifest sign: stop the cloned formatter and set one big, short line.
            var cloned = header.GetComponent<CargoSignText>();
            if (cloned) { cloned.enabled = false; Destroy(cloned); }
            CargoSignText.Style(text, .12f);
            text.text = "SUPPLY RACK";
        }

        /// <summary>Each level's posts stood on the shelf below; run every post to the rack base instead.</summary>
        private static void GroundSupports(Transform supports)
        {
            var posts = new List<Transform>();
            foreach (Transform part in supports)
                if (part.name.EndsWith(" shelf support", StringComparison.Ordinal)) posts.Add(part);
            if (posts.Count == 0) return;
            float baseY = float.MaxValue;
            var tops = new List<float>();
            foreach (Transform post in posts)
            {
                baseY = Mathf.Min(baseY, post.localPosition.y - post.localScale.y * .5f);
                float top = post.localPosition.y + post.localScale.y * .5f;
                if (!tops.Exists(v => Mathf.Abs(v - top) < .01f)) tops.Add(top);
            }
            tops.Sort();
            foreach (Transform post in posts)
            {
                float top = post.localPosition.y + post.localScale.y * .5f;
                int level = tops.FindIndex(v => Mathf.Abs(v - top) < .01f);
                // Coincident posts of different levels get slightly different widths so their faces never z-fight.
                float width = post.localScale.x + .0014f * level;
                post.localScale = new Vector3(width, top - baseY, post.localScale.z + .0014f * level);
                Vector3 p = post.localPosition;
                post.localPosition = new Vector3(p.x, (top + baseY) * .5f, p.z);
            }
        }

        private static Transform Nearest(List<Transform> candidates, Vector3 point, float maxDistance)
        {
            Transform best = null;
            float bestDistance = maxDistance;
            foreach (Transform candidate in candidates)
            {
                if (!candidate) continue;
                float d = Vector3.Distance(candidate.position, point);
                if (d < bestDistance) { best = candidate; bestDistance = d; }
            }
            return best;
        }

        // After CargoVisualPresenter (LateUpdate, default order) has applied Core's supply to the crates.
        private void LateUpdate()
        {
            foreach (Cell cell in cells)
            {
                bool present = cell.Case && cell.Case.activeSelf;
                if (cell.Shown == present) continue;
                foreach (Renderer holder in cell.Holders) if (holder) holder.enabled = present;
                cell.Shown = present;
            }
        }

        private void OnDisable()
        {
            // The whole rack is hidden with its root; restore so a later planning phase starts from the authored state.
            foreach (Cell cell in cells) cell.Shown = null;
        }
    }
}

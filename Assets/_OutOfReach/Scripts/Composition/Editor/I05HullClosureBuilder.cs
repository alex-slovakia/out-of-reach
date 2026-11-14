using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Closes the two confirmed width-transition shoulders; does not alter delivered ship assets.</summary>
    public static class I05HullClosureBuilder
    {
        public const string RootName = "I05 Hull shoulder closures";
        private const string LeftName = "CockpitEngineeringShoulder_L";
        private const string RightName = "CockpitEngineeringShoulder_R";
        private const string VisualName = "Opaque shoulder panel";
        private static readonly Vector3 VisualSize = new Vector3(.62f, 3.12f, .16f);
        private static readonly Vector3 CollisionSize = new Vector3(.64f, 3.32f, .22f);
        private const float Tolerance = .0001f;

        /// <summary>
        /// Call under an identity ship root with its existing opaque bulkhead material. Rebuilds only this
        /// helper's reserved direct child after validating the replacement. No scene save or asset mutation.
        /// </summary>
        public static GameObject Build(Transform parent, Material panelMaterial)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!panelMaterial) throw new ArgumentNullException(nameof(panelMaterial));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build hull closures outside Play mode.");
            RequireIdentity(parent);
            string renderType = panelMaterial.GetTag("RenderType", false, string.Empty);
            if (panelMaterial.renderQueue >= (int)RenderQueue.Transparent || renderType == "Transparent"
                || renderType == "TransparentCutout"
                || (panelMaterial.HasProperty("_Surface") && panelMaterial.GetFloat("_Surface") > .5f)
                || (panelMaterial.HasProperty("_AlphaClip") && panelMaterial.GetFloat("_AlphaClip") > .5f))
                throw new ArgumentException("Shoulder closures need an opaque, non-clipped bulkhead material.", nameof(panelMaterial));

            Transform previous = null;
            foreach (Transform child in parent)
            {
                if (child.name != RootName) continue;
                if (previous) throw new InvalidOperationException("Duplicate reserved hull-closure roots; nothing was removed.");
                RequireOwnedStructure(child);
                previous = child;
            }

            var replacement = new GameObject(RootName + " (building)");
            replacement.transform.SetParent(parent, false);
            try
            {
                CreatePanel(replacement.transform, LeftName, -2.70f, panelMaterial);
                CreatePanel(replacement.transform, RightName, 2.70f, panelMaterial);
                RequireOwnedStructure(replacement.transform);
                ValidatePanel(replacement.transform.Find(LeftName), -2.70f);
                ValidatePanel(replacement.transform.Find(RightName), 2.70f);
                // The old subtree is removed only after both new panels pass. Nothing else under parent is touched.
                if (previous) Object.DestroyImmediate(previous.gameObject);
                replacement.name = RootName;
                return replacement;
            }
            catch
            {
                Object.DestroyImmediate(replacement);
                throw;
            }
        }

        private static void CreatePanel(Transform parent, string name, float x, Material material)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(x, 1.56f, 3f);
            var solid = panel.AddComponent<BoxCollider>();
            solid.center = Vector3.zero;
            solid.size = CollisionSize;
            solid.isTrigger = false;
            solid.enabled = true;

            // Built-in closed cube supplies outward faces on both sides. Its mesh asset is never edited.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = VisualName;
            visual.transform.SetParent(panel.transform, false);
            visual.transform.localScale = VisualSize;
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void RequireOwnedStructure(Transform root)
        {
            if (root.GetComponents<Component>().Length != 1 || root.childCount != 2)
                throw new InvalidOperationException("Reserved hull-closure root contains unrecognized content; nothing was removed.");
            foreach (string name in new[] { LeftName, RightName })
            {
                Transform panel = root.Find(name);
                Transform visual = panel ? panel.Find(VisualName) : null;
                if (!panel || panel.childCount != 1 || panel.GetComponents<Component>().Length != 2
                    || !panel.GetComponent<BoxCollider>() || !visual || visual.childCount != 0
                    || visual.GetComponents<Component>().Length != 3 || !visual.GetComponent<MeshFilter>()
                    || !visual.GetComponent<MeshRenderer>())
                    throw new InvalidOperationException("Reserved shoulder subtree contains unrecognized content; nothing was removed.");
            }
        }

        private static void ValidatePanel(Transform panel, float x)
        {
            Vector3 centre = new Vector3(x, 1.56f, 3f);
            var renderer = panel.Find(VisualName).GetComponent<MeshRenderer>();
            Bounds visualBounds = renderer.bounds;
            var collider = panel.GetComponent<BoxCollider>();
            // Analytic bounds do not depend on an Editor physics step or whether the parent is active.
            var collisionBounds = new Bounds(panel.TransformPoint(collider.center), Vector3.Scale(collider.size, panel.lossyScale));
            if (!Near(visualBounds.center, centre) || !Near(visualBounds.size, VisualSize)
                || !Near(collisionBounds.center, centre) || !Near(collisionBounds.size, CollisionSize)
                || collider.isTrigger || !collider.enabled)
                throw new InvalidOperationException("Hull closure bounds differ from the reviewed shoulder patch.");
            var centralPassage = new Bounds(new Vector3(0f, 1.25f, 3f), new Vector3(1.8f, 2.5f, .6f));
            if (visualBounds.Intersects(centralPassage) || collisionBounds.Intersects(centralPassage))
                throw new InvalidOperationException("Hull closure obstructs the existing central hatch.");
        }

        private static void RequireIdentity(Transform parent)
        {
            if (!Near(parent.position, Vector3.zero) || !Near(parent.lossyScale, Vector3.one)
                || Quaternion.Angle(parent.rotation, Quaternion.identity) > Tolerance)
                throw new ArgumentException("Hull closure coordinates require an identity world-space ship parent.", nameof(parent));
        }

        private static bool Near(Vector3 actual, Vector3 expected) => (actual - expected).sqrMagnitude <= Tolerance * Tolerance;
    }
}

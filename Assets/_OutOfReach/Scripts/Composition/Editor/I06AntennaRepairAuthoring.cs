using System;
using System.Linq;
using OutOfReach.Interaction;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Places the delivered antenna kit and its scene-only housing proxy. No session, route or domain edits.</summary>
    public static class I06AntennaRepairAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Props/AntennaRepair";
        public const string OwnedRootName = "Antenna repair worksite";
        public static readonly Vector3 ProposedRouteEnd = new Vector3(3.65f, .9f, 13.15f);
        public sealed class Result
        {
            public Transform Root;
            public I06AntennaRepairBindings Bindings;
            public BoxCollider HousingProxy;
            public Transform GripFrame;
        }

        public static Result Create(Transform actualAntennaAnchor, InteractableTarget existingTarget)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Author antenna geometry before play.");
            if (!actualAntennaAnchor || !existingTarget) throw new ArgumentNullException("Antenna anchor and original target are required.");
            if (Vector3.Distance(actualAntennaAnchor.position, new Vector3(3.2f, 2.4f, 13f)) > .001f
                || Quaternion.Angle(actualAntennaAnchor.rotation, Quaternion.Euler(0, -90, 0)) > .05f
                || (actualAntennaAnchor.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("This measured placement requires the existing identity-ship antenna anchor.");
            var importer = AssetImporter.GetAtPath(AssetRoot + "/Models/AntennaRepairMotion.fbx") as ModelImporter;
            if (!importer || importer.animationCompression != ModelImporterAnimationCompression.Off)
                throw new InvalidOperationException("Prepare the exact antenna imports with animation compression Off first.");
            var clip = AssetDatabase.LoadAllAssetsAtPath(AssetRoot + "/Models/AntennaRepairMotion.fbx")
                .OfType<AnimationClip>().Single(x => !x.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (Mathf.Abs(clip.length - 3f) > .0001f || Mathf.Abs(clip.frameRate - 30f) > .0001f)
                throw new InvalidOperationException("Expected delivered three-second, 30 fps work clip.");
            Transform previous = actualAntennaAnchor.Find(OwnedRootName);
            if (previous) Object.DestroyImmediate(previous.gameObject); // This helper's exact owned subtree only.
            Transform root = new GameObject(OwnedRootName).transform; root.SetParent(actualAntennaAnchor, false);
            GameObject art = Instantiate(AssetRoot + "/Models/AntennaRepair.fbx", root, "Static antenna repair art");
            GameObject motion = Instantiate(AssetRoot + "/Models/AntennaRepairMotion.fbx", root, "Animated antenna repair art");
            Transform damaged = Find(art.transform, "ConnectorDamaged"), repaired = Find(art.transform, "ConnectorRepaired");
            Find(art.transform, "ServiceWrench").gameObject.SetActive(false);
            clip.SampleAnimation(motion, 0f);
            Transform wrench = Find(motion.transform, "ServiceWrench");
            Transform grip = new GameObject("Calibrated right tool grip frame").transform;
            grip.SetParent(wrench, false); grip.localPosition = Vector3.zero;
            // Preserve this imported tool-relative basis for all frames; camera look never reorients the grip.
            grip.rotation = Quaternion.LookRotation(root.forward, Vector3.Cross(root.forward, wrench.right).normalized);
            BoxCollider proxy = CreateHousingProxy(root);
            var binding = root.gameObject.AddComponent<I06AntennaRepairBindings>();
            binding.ConfigureViews(existingTarget, motion.transform, grip, clip, damaged.gameObject, repaired.gameObject,
                wrench.GetComponentsInChildren<Renderer>(true));
            damaged.gameObject.SetActive(true); repaired.gameObject.SetActive(false); motion.SetActive(false);
            return new Result { Root = root, Bindings = binding, HousingProxy = proxy, GripFrame = grip };
        }

        private static BoxCollider CreateHousingProxy(Transform owner)
        {
            Vector3 normal = new Vector3(3.2f, .84f, 0).normalized;
            Transform proxy = new GameObject("Antenna housing collision - authored box").transform;
            proxy.SetParent(owner, false);
            proxy.SetPositionAndRotation(new Vector3(0, 1.56f, 12.98f) + normal * 3.19f,
                Quaternion.LookRotation(normal, Vector3.forward));
            var collider = proxy.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.56f, .92f, .22f);
            return collider;
        }
        private static GameObject Instantiate(string path, Transform parent, string name)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source) throw new InvalidOperationException("Missing exact delivered antenna asset: " + path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent); instance.name = name;
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); instance.transform.localScale = Vector3.one;
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            { animator.enabled = false; animator.applyRootMotion = false; }
            if (instance.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("The delivered visual kit must not add gameplay blockers.");
            return instance;
        }
        private static Transform Find(Transform root, string name)
            => root.GetComponentsInChildren<Transform>(true).Single(x => x.name == name);
    }
}

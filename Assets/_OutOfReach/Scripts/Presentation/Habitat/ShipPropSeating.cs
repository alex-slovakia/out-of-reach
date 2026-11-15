using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.Habitat
{
    /// <summary>
    /// Presentation fix for free-floating PolyHaven wall mounts (I06PolyHavenSceneAuthoring placed them for a 4.8 m wide
    /// room at |x| 2.13): in the 6 m cockpit the instrument and binoculars hung in mid-air in front of the stowage
    /// rack, and the storage medical box stood 0.19 m off its blanket-bay quilt (face x 2.3265, DetailPass02 layout).
    /// Cockpit mounts are hidden; the medical box is seated flush on the quilt. Self-installing, no scene edit.
    /// </summary>
    public static class ShipPropSeating
    {
        public const string ShipRootName = "AURORA-7 interior";
        public const string GroupName = "Curated CC0 service details";
        public static readonly string[] HiddenMounts = { "Secured vintage_spacecraft_instrument", "Secured binoculars" };
        public const string MedicalMount = "Secured medical_box";
        // Starboard blanket-bay quilt face plus 3.5 mm; the mount's plate back sits at the mount origin.
        public const float QuiltFaceX = 2.326f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterFirstLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) Apply(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        public static int Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == ShipRootName) changed += Apply(root.transform);
            return changed;
        }

        /// <summary>Applies to the ship-interior root. Returns the number of mounts changed. Idempotent.</summary>
        public static int Apply(Transform shipRoot)
        {
            Transform group = shipRoot ? shipRoot.Find(GroupName) : null;
            if (!group) return 0;
            int changed = 0;
            foreach (string name in HiddenMounts)
            {
                Transform mount = group.Find(name);
                if (mount && mount.gameObject.activeSelf) { mount.gameObject.SetActive(false); changed++; }
            }
            Transform medical = group.Find(MedicalMount);
            if (medical && Mathf.Abs(Mathf.Abs(medical.localPosition.x) - QuiltFaceX) > .001f)
            {
                Vector3 p = medical.localPosition;
                medical.localPosition = new Vector3(Mathf.Sign(p.x) * QuiltFaceX, p.y, p.z);
                changed++;
            }
            return changed;
        }
    }
}

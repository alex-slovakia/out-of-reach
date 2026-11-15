using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Visuals.AuroraCapsulesV02
{
    /// <summary>Stops the launch tower's crew access arm from showing inside the closed crew transport cabin.
    /// The arm swings about a pivot beside its centre line, so while it retracts its white-room corner (boot
    /// seal, hazard chevrons) arcs about 0.4 m through the capsule wall and stood in the cabin as a dark slab
    /// beside the hatch. From inside a closed cabin the arm can never legitimately be seen, so it is simply not
    /// drawn then. Presentation only: Renderer.forceRenderingOff; nothing is moved, enabled or disabled, and
    /// nothing outside the launch transport is affected.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AuroraTransportCabinDresser : MonoBehaviour
    {
        public const string PrologueName = "I06 isolated marine prologue";
        public const string VehiclePath = "Transport vehicle - root controls launch";
        public const string HatchFramePath = VehiclePath + "/Hatch control +Y 0 closed to 160 open";
        public const string ArmPath = "Launch site - stationary/Access arm control +Y 0 connected to -90 retracted";

        private static AuroraTransportCabinDresser instance;
        private Transform vehicle, hatchFrame, arm;
        private Renderer[] hiddenRenderers = new Renderer[0];
        private bool hidden;
        private float nextSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance) return;
            var host = new GameObject("AURORA transport cabin dresser (AC2)");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AuroraTransportCabinDresser>();
        }

        /// <summary>True when the view point is inside the cabin and the hatch is shut.</summary>
        public static bool ShouldHide(Transform vehicleRoot, Transform hatch, Vector3 viewWorld)
        {
            Vector3 p = vehicleRoot.InverseTransformPoint(viewWorld);
            bool inside = p.y > -.1f && p.y < 2.6f && p.x * p.x + p.z * p.z < 2.42f * 2.42f;
            bool shut = !hatch || Mathf.Abs(Mathf.DeltaAngle(hatch.localEulerAngles.y, 0f)) < 2f;
            return inside && shut;
        }

        public static void SetDrawn(Renderer[] renderers, bool drawn)
        {
            foreach (Renderer renderer in renderers)
                if (renderer) renderer.forceRenderingOff = !drawn;
        }

        /// <summary>Exact paths under the prologue root; inactive objects are found too.</summary>
        private void Resolve()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != PrologueName) continue;
                    vehicle = root.transform.Find(VehiclePath);
                    arm = root.transform.Find(ArmPath);
                    hatchFrame = root.transform.Find(HatchFramePath);
                    if (vehicle && arm) return;
                }
            }
        }

        private void LateUpdate()
        {
            if (!vehicle || !arm)
            {
                if (hidden) { SetDrawn(hiddenRenderers, true); hidden = false; }
                if (Time.unscaledTime < nextSearch) return;
                nextSearch = Time.unscaledTime + .5f;
                Resolve();
                if (!vehicle || !arm) return;
            }
            bool hide = false;
            foreach (Camera view in Camera.allCameras)
            {
                if (!view || view.targetTexture) continue;
                if (ShouldHide(vehicle, hatchFrame, view.transform.position)) { hide = true; break; }
            }
            if (hide == hidden) return;
            if (hide) hiddenRenderers = arm.GetComponentsInChildren<Renderer>(true);
            SetDrawn(hiddenRenderers, !hide);
            hidden = hide;
        }

        private void OnDisable()
        {
            if (hidden) SetDrawn(hiddenRenderers, true);
            hidden = false;
        }
    }
}

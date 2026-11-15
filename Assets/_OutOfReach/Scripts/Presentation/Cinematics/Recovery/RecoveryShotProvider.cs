using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cinematics.Recovery
{
    /// <summary>Authored visual tableau, sampled only by MissionCinematicPresenter. No camera reference,
    /// Update, input, clock, domain subscription or completion publication. Moves owned visual couriers only.</summary>
    [DisallowMultipleComponent]
    public sealed class RecoveryShotProvider : MonoBehaviour
    {
        [SerializeField] private GameObject arrivalGroup, marsGroup;
        [SerializeField] private Transform arrivalCraft, arrivalCraftStart, arrivalCraftEnd, arrivalCameraStart, arrivalCameraEnd;
        [SerializeField] private Transform marsCraft, marsCraftStart, marsCraftEnd, marsCameraStart, marsCameraEnd;
        [SerializeField] private Renderer marsSurface;
        [SerializeField] private Shader marsShader;
        [SerializeField] private Texture2D marsColorMap;
        private bool active, arrivalWasActive, marsWasActive;
        private CinematicId shot;
        private Vector3 arrivalPosition, marsPosition;
        private Quaternion arrivalRotation, marsRotation;
        private Material priorMarsMaterial, ownedMarsMaterial;
        public bool IsPresenting => active;

        public readonly struct Frame
        {
            public readonly Vector3 CameraPosition;
            public readonly Quaternion CameraRotation;
            public readonly float FieldOfView, FarClip, Blackout;
            public readonly string Caption;
            public Frame(Vector3 position, Quaternion rotation, float fov, float far, float blackout, string caption)
            { CameraPosition = position; CameraRotation = rotation; FieldOfView = fov; FarClip = far; Blackout = blackout; Caption = caption; }
        }

        public static bool Supports(CinematicId id) => id == CinematicId.RecoveryArrival || id == CinematicId.MarsDeparture;
        public static float Duration(CinematicId id) => id == CinematicId.RecoveryArrival ? 12f : 10f;

        /// <summary>Owner calls this only after its confirmed recovered-outcome request. Invalid/unconfigured views fail closed.</summary>
        public bool TryBegin(CinematicId id)
        {
            EndShot();
            if (!isActiveAndEnabled || !Supports(id) || !ValidViews()) return false;
            shot = id;
            arrivalWasActive = arrivalGroup.activeSelf; marsWasActive = marsGroup.activeSelf;
            arrivalPosition = arrivalCraft.localPosition; arrivalRotation = arrivalCraft.localRotation;
            marsPosition = marsCraft.localPosition; marsRotation = marsCraft.localRotation;
            priorMarsMaterial = marsSurface.sharedMaterial;
            active = true;
            if (id == CinematicId.MarsDeparture)
            {
                // Verified NASA/JPL Viking color map, explicitly bound. Framing remains a cinematic illustration; never mutate a shared material.
                ownedMarsMaterial = new Material(marsShader) { name = "Recovery Mars - owned transient", hideFlags = HideFlags.DontSave };
                ownedMarsMaterial.SetTexture("_BaseMap", marsColorMap);
                marsSurface.sharedMaterial = ownedMarsMaterial;
            }
            arrivalGroup.SetActive(id == CinematicId.RecoveryArrival);
            marsGroup.SetActive(id == CinematicId.MarsDeparture);
            return true;
        }

        /// <summary>Normalized progress is supplied by the sole cinematic owner; there is no local time accumulation.</summary>
        public bool TrySample(CinematicId id, float normalizedProgress, bool reducedMotion, out Frame frame)
        {
            frame = default;
            if (!active || id != shot || !isActiveAndEnabled || !ValidViews() || float.IsNaN(normalizedProgress) || float.IsInfinity(normalizedProgress))
            { EndShot(); return false; }
            float t = Mathf.Clamp01(normalizedProgress);
            // Establish/cut under black. Never fly the real camera through the ship or between two distant sets.
            float travel = Smooth(Mathf.InverseLerp(.12f, .88f, t));
            Transform craft = id == CinematicId.RecoveryArrival ? arrivalCraft : marsCraft;
            Transform from = id == CinematicId.RecoveryArrival ? arrivalCraftStart : marsCraftStart;
            Transform to = id == CinematicId.RecoveryArrival ? arrivalCraftEnd : marsCraftEnd;
            craft.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, travel), Quaternion.Slerp(from.rotation, to.rotation, travel));
            Transform cameraFrom = id == CinematicId.RecoveryArrival ? arrivalCameraStart : marsCameraStart;
            Transform cameraTo = id == CinematicId.RecoveryArrival ? arrivalCameraEnd : marsCameraEnd;
            float cameraTravel = reducedMotion ? 1f : travel;
            float curtain = Mathf.Max(1f - Smooth(Mathf.InverseLerp(.02f, .14f, t)), Smooth(Mathf.InverseLerp(.88f, 1f, t)));
            frame = new Frame(Vector3.Lerp(cameraFrom.position, cameraTo.position, cameraTravel),
                Quaternion.Slerp(cameraFrom.rotation, cameraTo.rotation, cameraTravel),
                id == CinematicId.RecoveryArrival ? 55f : 48f, id == CinematicId.RecoveryArrival ? 350f : 900f,
                curtain, id == CinematicId.RecoveryArrival
                    ? "RECOVERY · AURORA-7\nRecovery craft on approach. Prepare for crew transfer."
                    : "LATER · MARS TRANSFER");
            return true;
        }

        public void EndShot()
        {
            if (!active) return;
            active = false;
            if (arrivalCraft) arrivalCraft.SetLocalPositionAndRotation(arrivalPosition, arrivalRotation);
            if (marsCraft) marsCraft.SetLocalPositionAndRotation(marsPosition, marsRotation);
            if (marsSurface) marsSurface.sharedMaterial = priorMarsMaterial;
            if (arrivalGroup) arrivalGroup.SetActive(arrivalWasActive);
            if (marsGroup) marsGroup.SetActive(marsWasActive);
            if (ownedMarsMaterial)
            {
                if (Application.isPlaying) Destroy(ownedMarsMaterial); else DestroyImmediate(ownedMarsMaterial);
            }
            ownedMarsMaterial = null; priorMarsMaterial = null;
        }

        private bool ValidViews() => arrivalGroup && marsGroup && arrivalGroup != marsGroup
            && arrivalCraft && arrivalCraftStart && arrivalCraftEnd && arrivalCameraStart && arrivalCameraEnd
            && marsCraft && marsCraftStart && marsCraftEnd && marsCameraStart && marsCameraEnd
            && marsSurface && marsShader && marsShader.isSupported && marsColorMap
            && arrivalCraft.IsChildOf(arrivalGroup.transform) && marsCraft.IsChildOf(marsGroup.transform)
            && marsSurface.transform.IsChildOf(marsGroup.transform);
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private void OnDisable() => EndShot();
        private void OnDestroy() => EndShot();
    }
}

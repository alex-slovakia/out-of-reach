using UnityEngine;

namespace OutOfReach.Presentation
{
    /// <summary>Serialized authoring references only. No mission state, gameplay gates or resource authority.</summary>
    public sealed class ShipVisualAnchors : MonoBehaviour
    {
        [Header("Metres; spawn is foot position, camera eye target is 1.65 m above it")]
        public Transform PlayerSpawn;
        public Transform CockpitConsole;
        public Transform PowerRepair;
        public Transform LifeSupportRepair;
        public Transform AirlockInner;
        public Transform AirlockOuter;
        public Transform EvaStart;
        public Transform AntennaRepair;
        public Transform EvaReturn;
        public Transform FinaleCameraAnchor;

        [Header("Visual leaves only; the integrator supplies independent authoritative gameplay gates")]
        public Transform InnerDoorLeft;
        public Transform InnerDoorRight;
        public Transform OuterDoorLeft;
        public Transform OuterDoorRight;
    }
}

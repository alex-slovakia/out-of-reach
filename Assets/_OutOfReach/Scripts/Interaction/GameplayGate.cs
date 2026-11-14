using UnityEngine;

namespace OutOfReach.Interaction
{
    /// <summary>
    /// An authoritative passage blocker, separate from any door visual. The composition root opens or closes it
    /// from mission state every frame; a door animation can never open it. It starts closed and stays closed
    /// until the mission says otherwise, so a missing binding fails safe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayGate : MonoBehaviour
    {
        [Tooltip("Stable gate ID: airlock-inner or airlock-outer.")]
        [SerializeField] private string gateId = string.Empty;

        [Tooltip("Solid collider that fills the passage while the gate is closed.")]
        [SerializeField] private Collider blocker;

        [Tooltip("Optional object shown only while the gate is closed (grey-box fixtures; real doors are presentation).")]
        [SerializeField] private GameObject closedVisual;

        public string GateId
        {
            get { return gateId; }
            set { gateId = value ?? string.Empty; }
        }

        public Collider Blocker => blocker;

        /// <summary>True while the passage is open for walking.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Explicit wiring for code-built scenes; authored scenes use the serialized fields.</summary>
        public void Configure(string id, Collider blockingCollider, GameObject shownWhileClosed = null)
        {
            GateId = id;
            blocker = blockingCollider;
            closedVisual = shownWhileClosed;
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (closedVisual != null && closedVisual != gameObject && closedVisual.activeSelf == open)
            {
                closedVisual.SetActive(!open);
            }

            if (blocker != null && blocker.enabled == open)
            {
                blocker.enabled = !open;
            }
        }

        private void OnEnable()
        {
            SetOpen(IsOpen);
        }
    }
}

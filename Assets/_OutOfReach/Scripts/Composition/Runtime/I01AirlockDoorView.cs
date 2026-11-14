using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Door leaf presentation only. The independently bound Core GameplayGates own passage.</summary>
    public sealed class I01AirlockDoorView : MonoBehaviour
    {
        [SerializeField] private Transform[] leaves = new Transform[4];
        [SerializeField] private Vector3[] openPositions = new Vector3[4];
        [SerializeField] private Vector3[] closedPositions = new Vector3[4];
        private IEvaStatus status;
        private ISimulationClock clock;

        public void ConfigureLeaves(Transform innerLeft, Transform innerRight, Transform outerLeft, Transform outerRight,
            Transform innerAnchor, Transform outerAnchor)
        {
            leaves = new[] { innerLeft, innerRight, outerLeft, outerRight };
            openPositions = new Vector3[4];
            closedPositions = new Vector3[4];
            for (int i = 0; i < leaves.Length; i++)
            {
                Vector3 world = leaves[i].position;
                float centre = i < 2 ? innerAnchor.position.x : outerAnchor.position.x;
                float sign = i % 2 == 0 ? -1f : 1f;
                Vector3 open = new Vector3(centre + sign * 1.4f, world.y, world.z);
                Vector3 closed = new Vector3(centre + sign * .45f, world.y, world.z);
                openPositions[i] = leaves[i].parent ? leaves[i].parent.InverseTransformPoint(open) : open;
                closedPositions[i] = leaves[i].parent ? leaves[i].parent.InverseTransformPoint(closed) : closed;
            }
        }

        public void Configure(IEvaStatus eva, ISimulationClock simulationClock)
        {
            status = eva;
            clock = simulationClock;
            Apply(true);
        }

        public void ClearBinding() { status = null; clock = null; }
        private void LateUpdate() { if (status != null && (clock == null || !clock.IsPaused)) Apply(false); }

        private void Apply(bool immediate)
        {
            if (status == null) return;
            for (int i = 0; i < leaves.Length; i++)
            {
                if (!leaves[i]) continue;
                bool open = i < 2 ? status.Airlock == AirlockState.InnerOpen : status.Airlock == AirlockState.OuterOpen;
                Vector3 target = open ? openPositions[i] : closedPositions[i];
                leaves[i].localPosition = immediate ? target : Vector3.MoveTowards(leaves[i].localPosition, target, Time.unscaledDeltaTime * 2.2f);
            }
        }
    }
}

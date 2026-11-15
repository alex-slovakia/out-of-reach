using UnityEngine;

namespace OutOfReach.Presentation.Routine
{
    /// <summary>
    /// Finds a camera pose that looks at an existing station anchor from free cabin space: clear of geometry, with a
    /// line of sight to the anchor, preferring the cabin side. No authored camera points and no new art are needed;
    /// when no clear pose exists the station simply gets no picture (fail open).
    /// </summary>
    public static class RoutineStationFraming
    {
        private const float Clearance = .16f, TargetSlack = .4f;
        private static readonly float[] Distances = { 1.5f, 1.25f, 1f, .8f };
        private static readonly float[] Heights = { .15f, .4f, -.1f };
        private static readonly Collider[] Overlaps = new Collider[16];
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool TrySolve(Vector3 target, Vector3 cabinFocus, Transform ignoreRoot, out Pose pose)
        {
            pose = default;
            Vector3 toCabin = Vector3.ProjectOnPlane(cabinFocus - target, Vector3.up);
            Vector3 baseDirection = toCabin.sqrMagnitude > 1e-4f ? toCabin.normalized : Vector3.forward;
            float best = float.NegativeInfinity;
            bool found = false;
            for (int d = 0; d < Distances.Length; d++)
            {
                for (int step = 0; step < 16; step++)
                {
                    // 0, +22.5, -22.5, +45, -45 ... so the cabin side is tried first.
                    float angle = ((step + 1) / 2) * 22.5f * ((step & 1) == 1 ? 1f : -1f);
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
                    for (int h = 0; h < Heights.Length; h++)
                    {
                        Vector3 position = target + (direction * Distances[d]) + (Vector3.up * Heights[h]);
                        if (!IsFree(position, ignoreRoot) || !Sees(position, target, ignoreRoot)) continue;
                        float score = Distances[d] + (Vector3.Dot(direction, baseDirection) * .6f) - (h * .05f);
                        if (score <= best) continue;
                        best = score;
                        found = true;
                        pose = new Pose(position, Quaternion.LookRotation((target - position).normalized, Vector3.up));
                    }
                }
            }

            return found;
        }

        private static bool IsFree(Vector3 position, Transform ignoreRoot)
        {
            int count = Physics.OverlapSphereNonAlloc(position, Clearance, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!Ignored(Overlaps[i], ignoreRoot)) return false;
            return true;
        }

        private static bool Sees(Vector3 position, Vector3 target, Transform ignoreRoot)
        {
            Vector3 span = target - position;
            float length = span.magnitude;
            if (length < .05f) return false;
            int count = Physics.RaycastNonAlloc(position, span / length, Hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Ignored(Hits[i].collider, ignoreRoot)) continue;
                // The station's own furniture near the anchor may be hit; anything nearer to the camera blocks the view.
                if (Hits[i].distance < length - TargetSlack) return false;
            }

            return true;
        }

        private static bool Ignored(Collider collider, Transform ignoreRoot)
        {
            return !collider || (ignoreRoot && collider.transform.IsChildOf(ignoreRoot));
        }
    }
}

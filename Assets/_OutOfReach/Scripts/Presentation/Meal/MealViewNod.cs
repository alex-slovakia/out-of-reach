using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// A presentation lease on the first-person view while a meal runs (R35): a small pitch of the eye, down to meet the
    /// pouch and back while squeezing. It is applied on top of whatever pose the view has: when another writer has
    /// re-posed the view since the last frame, that pose is the new rest. The rest pose is given back exactly when the
    /// meal ends. It changes no look input, no player yaw or pitch and no gameplay transform.
    /// </summary>
    internal sealed class MealViewNod
    {
        private Transform view;
        private Quaternion rest, lastWritten;
        private bool written;

        /// <summary>The nod on the view right now, degrees (positive looks down).</summary>
        public float Degrees { get; private set; }

        public void Apply(Transform eye, float degrees)
        {
            if (!eye) { Release(); return; }
            if (eye != view) { Release(); view = eye; }
            Quaternion current = view.localRotation;
            if (!written || !Same(current, lastWritten)) rest = current;
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) degrees = 0f;
            view.localRotation = Mathf.Abs(degrees) < 1e-4f ? rest : rest * Quaternion.Euler(degrees, 0f, 0f);
            lastWritten = view.localRotation;
            written = true;
            Degrees = degrees;
        }

        /// <summary>Gives the view back its rest pose (unless another writer already re-posed it).</summary>
        public void Release()
        {
            if (written && view && Same(view.localRotation, lastWritten)) view.localRotation = rest;
            written = false;
            view = null;
            Degrees = 0f;
        }

        // Exact comparison: Unity's Quaternion equality is approximate and would hide a small nod.
        private static bool Same(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
    }
}

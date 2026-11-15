using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Pure timing curves for the floating body and the leg push from the deck.</summary>
    public static class ModelFloatMotion
    {
        public const float PushDuration = .66f;
        private const float PlantEnd = .18f, ExtendEnd = .34f;

        /// <summary>How much the planted-leg pose replaces the neutral floating pose (0 at both ends).</summary>
        public static float PushWeight(float t)
        {
            if (t <= 0f || t >= PushDuration) return 0f;
            if (t < PlantEnd) return Mathf.SmoothStep(0f, 1f, t / PlantEnd);
            if (t < ExtendEnd) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - ExtendEnd) / (PushDuration - ExtendEnd));
        }

        /// <summary>Hip-to-ankle distance as a fraction of leg length: tuck, then a fast extension, then recover.</summary>
        public static float LegReach(float t, float neutral)
        {
            const float tucked = .50f, extended = .985f;
            if (t < PlantEnd) return Mathf.Lerp(neutral, tucked, Mathf.SmoothStep(0f, 1f, t / PlantEnd));
            if (t < ExtendEnd)
            {
                float k = (t - PlantEnd) / (ExtendEnd - PlantEnd);
                return Mathf.Lerp(tucked, extended, 1f - (1f - k) * (1f - k));
            }
            return Mathf.Lerp(extended, neutral, Mathf.SmoothStep(0f, 1f, (t - ExtendEnd) / (PushDuration - ExtendEnd)));
        }

        /// <summary>Slow relaxed drift of the legs while floating: thigh and shin pitch in degrees for each side.</summary>
        public static void RelaxedLegDrift(float time, out float leftThigh, out float leftShin, out float rightThigh, out float rightShin)
        {
            const float tau = 2f * Mathf.PI;
            leftThigh = 3.0f * Mathf.Sin(tau * time / 5.3f);
            rightThigh = 3.0f * Mathf.Sin(tau * time / 5.3f + 1.9f);
            leftShin = 2.5f * Mathf.Sin(tau * time / 4.1f + .8f);
            rightShin = 2.5f * Mathf.Sin(tau * time / 4.1f + 2.6f);
        }
    }

    /// <summary>Floating-body leg motion for the shared rig: relaxed drift, and the plant-extend-release leg push.</summary>
    internal sealed class ModelFloatRig
    {
        private readonly ModelGaitRig.Leg left, right;
        private Vector3 neutralLeftLocal, neutralRightLocal;
        private float legLength = ModelGait.ReferenceLegLength, neutralReach = .93f;
        private float clock;
        private float pushT = -1f;
        private Vector3 pushDirection;

        public bool IsPushing => pushT >= 0f;

        public ModelFloatRig(Dictionary<string, Transform> nodes)
        {
            left = new ModelGaitRig.Leg(nodes, "L", -1f);
            right = new ModelGaitRig.Leg(nodes, "R", 1f);
        }

        /// <summary>Call right after the held floating clip was sampled (neutral pose).</summary>
        public void Capture(Transform reference)
        {
            left.CaptureStanding(reference);
            right.CaptureStanding(reference);
            legLength = Mathf.Clamp((left.Length + right.Length) * .5f, .55f, 1.3f);
            neutralLeftLocal = reference.InverseTransformPoint(left.AnklePosition);
            neutralRightLocal = reference.InverseTransformPoint(right.AnklePosition);
            float l = (left.AnklePosition - left.HipPosition).magnitude, r = (right.AnklePosition - right.HipPosition).magnitude;
            neutralReach = Mathf.Clamp((l + r) * .5f / legLength, .5f, .99f);
            clock = 0f; pushT = -1f;
        }

        public void Restore()
        {
            left.RestoreStanding();
            right.RestoreStanding();
        }

        public void CancelPush() => pushT = -1f;

        /// <summary>Start a push along a world direction. The deck is behind the feet: opposite the push.</summary>
        public void BeginPush(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 1e-6f) return;
            pushDirection = worldDirection.normalized;
            pushT = 0f;
        }

        public void Apply(Transform reference, float delta)
        {
            Restore();
            clock += delta;
            ModelFloatMotion.RelaxedLegDrift(clock, out float lt, out float ls, out float rt, out float rs);
            Vector3 axis = reference.right;
            left.Sway(axis, lt, ls);
            right.Sway(axis, rt, rs);
            if (pushT < 0f) return;
            pushT += delta;
            if (pushT >= ModelFloatMotion.PushDuration) { pushT = -1f; return; }
            float w = ModelFloatMotion.PushWeight(pushT);
            if (w <= 0f) return;
            float reach = ModelFloatMotion.LegReach(pushT, neutralReach) * legLength;
            Vector3 deck = -pushDirection;
            Vector3 side = Vector3.ProjectOnPlane(reference.right, pushDirection);
            side = side.sqrMagnitude < 1e-4f ? Vector3.ProjectOnPlane(reference.forward, pushDirection) : side.normalized;
            Vector3 pole = reference.forward + pushDirection * .6f;
            PlantLeg(left, reference, neutralLeftLocal, deck, -side, reach, w, pole);
            PlantLeg(right, reference, neutralRightLocal, deck, side, reach, w, pole);
        }

        private void PlantLeg(ModelGaitRig.Leg leg, Transform reference, Vector3 neutralLocal, Vector3 deck,
            Vector3 lateral, float reach, float weight, Vector3 pole)
        {
            Vector3 hip = leg.HipPosition;
            Vector3 neutral = reference.TransformPoint(neutralLocal);
            Vector3 planted = hip + deck * reach + lateral * legLength * .09f;
            Vector3 target = Vector3.Lerp(neutral, planted, weight);
            leg.Pose(reference, target, 0f, 0f, pole);
        }
    }
}

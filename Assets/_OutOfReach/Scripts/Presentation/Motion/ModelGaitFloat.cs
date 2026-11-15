using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Pure timing curves and posture for the floating body and the leg push from the deck.</summary>
    public static class ModelFloatMotion
    {
        public const float PushDuration = .66f;
        private const float PlantEnd = .18f, ExtendEnd = .34f;

        /// <summary>
        /// Legs of the relaxed microgravity posture, degrees: approximately the neutral body posture that NASA's
        /// Man-Systems Integration Standards (NASA-STD-3000) describe for weightlessness: hips and knees flexed
        /// about 50 degrees, legs slightly apart. Blended over the held float clip by <see cref="NeutralBlend"/>.
        /// </summary>
        public const float NeutralHipFlexion = 48f, NeutralKneeFlexion = 50f, NeutralLegAbduction = 7f, NeutralBlend = .8f;

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

        /// <summary>Trunk curl in degrees during the push: curls in with the tuck, opens with the extension.</summary>
        public static float TrunkCurl(float t)
        {
            if (t <= 0f || t >= PushDuration) return 0f;
            float curl = Mathf.Lerp(6f, -3.5f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlantEnd, ExtendEnd, t)));
            return curl * PushWeight(t);
        }

        /// <summary>Slow relaxed drift of the legs while floating: hip and knee flexion offsets in degrees for each side.</summary>
        public static void RelaxedLegDrift(float time, out float leftHip, out float leftKnee, out float rightHip, out float rightKnee)
        {
            const float tau = 2f * Mathf.PI;
            leftHip = 3.0f * Mathf.Sin(tau * time / 5.3f);
            rightHip = 3.0f * Mathf.Sin(tau * time / 5.3f + 1.9f);
            leftKnee = 2.5f * Mathf.Sin(tau * time / 4.1f + .8f);
            rightKnee = 2.5f * Mathf.Sin(tau * time / 4.1f + 2.6f);
        }
    }

    /// <summary>Floating-body legs for the shared rig: neutral body posture with a slow drift, and the plant-extend-release push.</summary>
    internal sealed class ModelFloatRig
    {
        private readonly ModelGaitRig.Leg left, right;
        private readonly Transform spineLow;
        private Quaternion spineLowRotation;
        private float legLength = ModelGait.ReferenceLegLength;
        private float clock;
        private float pushT = -1f;
        private Vector3 pushDirection;

        public bool IsPushing => pushT >= 0f;

        public ModelFloatRig(Dictionary<string, Transform> nodes)
        {
            left = new ModelGaitRig.Leg(nodes, "L", -1f);
            right = new ModelGaitRig.Leg(nodes, "R", 1f);
            if (nodes.TryGetValue("root", out Transform hips) && nodes.TryGetValue("spine05", out Transform low) && low.parent == hips)
                spineLow = low;
        }

        /// <summary>Call right after the held floating clip was sampled.</summary>
        public void Capture(Transform reference)
        {
            left.CaptureStanding(reference);
            right.CaptureStanding(reference);
            if (spineLow) spineLowRotation = spineLow.localRotation;
            legLength = Mathf.Clamp((left.Length + right.Length) * .5f, .55f, 1.3f);
            clock = 0f; pushT = -1f;
        }

        public void Restore()
        {
            left.RestoreStanding();
            right.RestoreStanding();
            if (spineLow) spineLow.localRotation = spineLowRotation;
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
            ModelFloatMotion.RelaxedLegDrift(clock, out float lh, out float lk, out float rh, out float rk);
            Vector3 leftTarget = Neutral(left, reference, lh, lk), rightTarget = Neutral(right, reference, rh, rk);
            Vector3 pole = reference.forward;

            if (pushT >= 0f)
            {
                pushT += delta;
                if (pushT >= ModelFloatMotion.PushDuration) pushT = -1f;
            }
            float w = pushT >= 0f ? ModelFloatMotion.PushWeight(pushT) : 0f;
            if (w > 0f)
            {
                Vector3 deck = -pushDirection;
                Vector3 side = Vector3.ProjectOnPlane(reference.right, pushDirection);
                side = side.sqrMagnitude < 1e-4f ? Vector3.ProjectOnPlane(reference.forward, pushDirection).normalized : side.normalized;
                leftTarget = Vector3.Lerp(leftTarget, Planted(left, deck, -side), w);
                rightTarget = Vector3.Lerp(rightTarget, Planted(right, deck, side), w);
                pole = Vector3.Slerp(pole, (reference.forward + pushDirection * .6f).normalized, w);
                if (spineLow) spineLow.rotation = Quaternion.AngleAxis(ModelFloatMotion.TrunkCurl(pushT), reference.right) * spineLow.rotation;
            }
            left.PoseKeepFoot(leftTarget, pole);
            right.PoseKeepFoot(rightTarget, pole);
        }

        private Vector3 Planted(ModelGaitRig.Leg leg, Vector3 deck, Vector3 lateral)
        {
            float neutral = Mathf.Clamp((leg.AnklePosition - leg.HipPosition).magnitude / legLength, .5f, .99f);
            float reach = ModelFloatMotion.LegReach(pushT, neutral) * legLength;
            return leg.HipPosition + deck * reach + lateral * legLength * .09f;
        }

        // Neutral body posture target for one ankle, blended over the clip's own ankle.
        private static Vector3 Neutral(ModelGaitRig.Leg leg, Transform reference, float hipDrift, float kneeDrift)
        {
            Vector3 right = reference.right;
            Vector3 thigh = Quaternion.AngleAxis(-(ModelFloatMotion.NeutralHipFlexion + hipDrift), right)
                * (Quaternion.AngleAxis(leg.Side * ModelFloatMotion.NeutralLegAbduction, reference.forward) * -reference.up);
            Vector3 shin = Quaternion.AngleAxis(ModelFloatMotion.NeutralKneeFlexion + kneeDrift, right) * thigh;
            Vector3 neutral = leg.HipPosition + thigh * leg.ThighLength + shin * leg.ShinLength;
            return Vector3.Lerp(leg.AnklePosition, neutral, ModelFloatMotion.NeutralBlend);
        }
    }
}

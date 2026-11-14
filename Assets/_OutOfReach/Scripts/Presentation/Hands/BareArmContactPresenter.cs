using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Optional WORK palm push on the existing skin. Rotations only; root owns visibility, input and physics.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class BareArmContactPresenter : MonoBehaviour
    {
        [SerializeField, Range(.15f, .25f)] private float reachSeconds = .20f;
        [SerializeField, Range(.15f, .35f)] private float returnSeconds = .24f;
        private BareArmContactRig left, right, active;
        private Transform eye;
        private ISimulationClock clock;
        private Func<ContactHand, bool> leaseAllowed;
        private Func<int> readRevision;
        private Func<HandContactSample, bool> validate, action;
        private HandContactTarget target;
        private ContactHand hand;
        private int poseRevision, generation;
        private float elapsed;
        private Vector3 palmPoint, startWrist, elbowPole, previousPoint, previousEye;
        private Quaternion palmFrame, startWristRotation;
        private readonly Quaternion[] baseline = new Quaternion[3], returnStart = new Quaternion[3];
        public HandContactStage ContactStage { get; private set; }
        public float ContactErrorMetres { get; private set; } = float.NaN;

        /// <summary>Root must subscribe CancelImmediately to EmbodiedBodyPresenter.PoseWillReset before use.</summary>
        public void Configure(BareArmContactRig leftArm, BareArmContactRig rightArm, Transform unbobbedEye,
            ISimulationClock simulationClock, Func<ContactHand, bool> isWorkArmLeaseAllowed,
            Func<int> currentBodyPoseRevision, Func<HandContactSample, bool> validateActualContact)
        {
            ClearBinding();
            if (leftArm == null || rightArm == null || !leftArm.IsUsable || !rightArm.IsUsable || !unbobbedEye
                || simulationClock == null || isWorkArmLeaseAllowed == null || currentBodyPoseRevision == null
                || validateActualContact == null) throw new ArgumentException("Bind the existing WORK arms and body/contact authority.");
            left = leftArm; right = rightArm; eye = unbobbedEye; clock = simulationClock;
            leaseAllowed = isWorkArmLeaseAllowed; readRevision = currentBodyPoseRevision; validate = validateActualContact;
        }

        /// <summary>Bare Grip is intentionally unavailable until a real authored/calibrated bare-hand curl is delivered.</summary>
        public bool TryReachPush(ContactHand requestedHand, HandContactTarget actualSurface,
            Func<HandContactSample, bool> tryPushAtContact)
        {
            if (!isActiveAndEnabled || clock == null || clock.IsPaused || ContactStage != HandContactStage.None
                || (requestedHand != ContactHand.Left && requestedHand != ContactHand.Right)
                || tryPushAtContact == null || !leaseAllowed(requestedHand)) return false;
            active = requestedHand == ContactHand.Left ? left : right; hand = requestedHand;
            if (!active.IsUsable || !active.Renderer.enabled || !active.Renderer.gameObject.activeInHierarchy) { Forget(); return false; }
            poseRevision = readRevision(); target = actualSurface;
            if (!BarePalmCalibration.TryMeasure(active, out palmPoint, out palmFrame)) { Forget(); return false; }
            baseline[0] = active.Upper.localRotation; baseline[1] = active.Lower.localRotation; baseline[2] = active.Wrist.localRotation;
            startWrist = active.Upper.parent.InverseTransformPoint(active.Wrist.position);
            startWristRotation = Quaternion.Inverse(active.Upper.parent.rotation) * active.Wrist.rotation;
            elbowPole = active.Upper.parent.InverseTransformVector(active.Lower.position - active.Upper.position);
            previousPoint = actualSurface.Surface ? actualSurface.Surface.TransformPoint(actualSurface.LocalPoint) : Vector3.zero;
            previousEye = eye.position;
            if (!TryTarget(out _, out Vector3 point, out Quaternion rotation) || !CanReach(point)) { Forget(); return false; }
            action = tryPushAtContact; elapsed = 0f; ContactStage = HandContactStage.Reaching;
            return true;
        }

        /// <summary>Synchronous lease return before the body owner samples, hides or restores its own skeleton.</summary>
        public void CancelImmediately()
        {
            if (active != null && ContactStage != HandContactStage.None && readRevision != null && readRevision() == poseRevision)
                Restore(baseline, 1f);
            Forget();
        }

        public void CancelContact()
        {
            if (active == null || ContactStage == HandContactStage.None || ContactStage == HandContactStage.Returning) return;
            returnStart[0] = active.Upper.localRotation; returnStart[1] = active.Lower.localRotation; returnStart[2] = active.Wrist.localRotation;
            generation++; action = null; target = default; elapsed = 0f;
            ContactErrorMetres = float.NaN; ContactStage = HandContactStage.Returning;
        }

        public void ClearBinding()
        {
            CancelImmediately(); left = right = null; eye = null; clock = null;
            leaseAllowed = null; readRevision = null; validate = null;
        }
        private void OnDisable() => CancelImmediately();
        private void OnDestroy() => ClearBinding();
        private void Forget()
        {
            generation++; active = null; action = null; target = default;
            ContactStage = HandContactStage.None; elapsed = 0f; ContactErrorMetres = float.NaN;
        }

        private void LateUpdate()
        {
            if (ContactStage == HandContactStage.None) return;
            if (active == null || !active.IsUsable || readRevision() != poseRevision) { Forget(); return; }
            if (clock.IsPaused) { CancelContact(); return; } // Freeze visible pose; no queued action survives resume.
            if (!leaseAllowed(hand) || !active.Renderer.enabled || !active.Renderer.gameObject.activeInHierarchy)
            { CancelImmediately(); return; }
            float dt = Time.unscaledDeltaTime;
            if (!Finite(dt) || dt < 0f) { CancelContact(); return; }
            // Slow rendering alone must neither abandon a valid reach nor fast-forward its action.
            // Revalidate the real target/lease below while holding the animation at its prior time.
            if (dt > .1f) dt = 0f;
            elapsed += dt;
            if (ContactStage == HandContactStage.Returning)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Clamp(returnSeconds, .15f, .35f));
                Restore(returnStart, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) Forget();
                return;
            }
            if (!TryTarget(out HandContactSample sample, out Vector3 finalWrist, out Quaternion finalRotation))
            { CancelContact(); return; }
            float reach = Mathf.Clamp01(elapsed / Mathf.Clamp(reachSeconds, .15f, .25f));
            float blend = ContactStage == HandContactStage.Reaching ? Mathf.SmoothStep(0f, 1f, reach) : 1f;
            Vector3 wrist = Vector3.Lerp(active.Upper.parent.TransformPoint(startWrist), finalWrist, blend);
            Quaternion rotation = Quaternion.Slerp(active.Upper.parent.rotation * startWristRotation, finalRotation, blend);
            // Solve from the captured fitting pose every frame, never accumulating rotations or changing bone lengths.
            Restore(baseline, 1f);
            if (!SolveArm(wrist, rotation)) { CancelContact(); return; }
            ContactErrorMetres = Vector3.Distance(active.Wrist.TransformPoint(palmPoint), sample.Point);
            if (ContactStage == HandContactStage.PushContact)
            { if (elapsed >= .06f) CancelContact(); return; }
            if (reach < 1f) return;
            if (ContactErrorMetres > .003f) { CancelContact(); return; }
            var callback = action; action = null;
            ContactStage = HandContactStage.PushContact; elapsed = 0f;
            int token = generation;
            bool accepted;
            try { accepted = callback != null && callback(sample); }
            catch (Exception error) { Debug.LogException(error, this); accepted = false; }
            if (token != generation) return;
            if (!accepted) CancelContact();
        }

        private void Restore(Quaternion[] from, float blend)
        {
            if (active == null) return;
            if (active.Upper) active.Upper.localRotation = Quaternion.Slerp(from[0], baseline[0], blend);
            if (active.Lower) active.Lower.localRotation = Quaternion.Slerp(from[1], baseline[1], blend);
            if (active.Wrist) active.Wrist.localRotation = Quaternion.Slerp(from[2], baseline[2], blend);
        }

        private bool TryTarget(out HandContactSample sample, out Vector3 wrist, out Quaternion rotation)
        {
            sample = default; wrist = default; rotation = Quaternion.identity;
            Transform surface = target.Surface;
            if (!surface || !surface.gameObject.activeInHierarchy || !eye) return false;
            Vector3 p = surface.TransformPoint(target.LocalPoint);
            Vector3 n = surface.localToWorldMatrix.inverse.transpose.MultiplyVector(target.LocalNormal).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(surface.TransformVector(target.LocalTangent), n).normalized;
            Vector3 local = Quaternion.Inverse(eye.rotation) * (p - eye.position);
            float side = hand == ContactHand.Left ? -1f : 1f;
            if (!FiniteVector(p) || !FiniteVector(n) || !FiniteVector(tangent) || n.sqrMagnitude < .99f || tangent.sqrMagnitude < .99f
                || local.magnitude > .8f || local.z < .10f || local.x * side < -.15f
                || Vector3.Dot(n, (eye.position - p).normalized) < .15f
                || Vector3.Distance(previousPoint, p) > .12f || Vector3.Distance(previousEye, eye.position) > .25f) return false;
            rotation = Quaternion.LookRotation(tangent, -n) * Quaternion.Inverse(palmFrame);
            wrist = p - rotation * palmPoint;
            if (!CanReach(wrist)) return false;
            sample = new HandContactSample(hand, HandContactKind.Push, target, p, n, tangent, active.Wrist.position, wrist);
            int token = generation;
            try
            {
                if (!validate(sample) || token != generation || readRevision() != poseRevision) return false;
                previousPoint = p; previousEye = eye.position; return true;
            }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        private bool CanReach(Vector3 wrist)
        {
            float a = Vector3.Distance(active.Upper.position, active.Lower.position);
            float b = Vector3.Distance(active.Lower.position, active.Wrist.position);
            float d = Vector3.Distance(active.Upper.position, wrist);
            if (a < .15f || b < .15f || a > .4f || b > .4f || !Finite(d) || d <= .001f) return false;
            float cosine = (a * a + b * b - d * d) / (2f * a * b);
            // Interior elbow angle 25–175 degrees: no stretching, folded-through arm or locked straight elbow.
            return cosine >= Mathf.Cos(175f * Mathf.Deg2Rad) && cosine <= Mathf.Cos(25f * Mathf.Deg2Rad);
        }

        private bool SolveArm(Vector3 wrist, Quaternion rotation)
        {
            if (!CanReach(wrist)) return false;
            Vector3 shoulder = active.Upper.position, direction = (wrist - shoulder).normalized;
            float a = Vector3.Distance(shoulder, active.Lower.position);
            float b = Vector3.Distance(active.Lower.position, active.Wrist.position), d = Vector3.Distance(shoulder, wrist);
            Vector3 pole = Vector3.ProjectOnPlane(active.Upper.parent.TransformVector(elbowPole), direction).normalized;
            if (pole.sqrMagnitude < .99f) return false;
            float along = (a * a - b * b + d * d) / (2f * d);
            Vector3 elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            active.Upper.rotation = Quaternion.FromToRotation(active.Lower.position - shoulder, elbow - shoulder) * active.Upper.rotation;
            active.Lower.rotation = Quaternion.FromToRotation(active.Wrist.position - active.Lower.position,
                wrist - active.Lower.position) * active.Lower.rotation;
            Vector3 axis = (wrist - active.Lower.position).normalized;
            Vector3 currentNormal = Vector3.ProjectOnPlane(active.Wrist.rotation * (palmFrame * Vector3.up), axis);
            Vector3 desiredNormal = Vector3.ProjectOnPlane(rotation * (palmFrame * Vector3.up), axis);
            if (currentNormal.sqrMagnitude > .01f && desiredNormal.sqrMagnitude > .01f)
            {
                float roll = Mathf.Clamp(Vector3.SignedAngle(currentNormal, desiredNormal, axis), -75f, 75f);
                active.Lower.rotation = Quaternion.AngleAxis(roll, axis) * active.Lower.rotation;
            }
            Quaternion wristLocal = Quaternion.Inverse(active.Wrist.parent.rotation) * rotation;
            if (Quaternion.Angle(wristLocal, baseline[2]) > 80f) { Restore(baseline, 1f); return false; }
            active.Wrist.localRotation = wristLocal;
            return Vector3.Distance(active.Wrist.position, wrist) <= .002f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}

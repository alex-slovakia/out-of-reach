using System;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// EVA-TETHER-01: hand-over-hand gloves on the visible tether rope. While the crew is outside on the route and a
    /// rope is configured, the leading glove is closed around its rung and stays fixed on the rope while the body
    /// moves; the other glove opens, swings forward along the rope and closes on the next rung (see
    /// <see cref="EvaTetherClimbMath"/>). Driven only by the real route distance (the existing route-progress query) and
    /// the view yaw; it owns no gameplay, physics or input. Tool holds and world contacts keep priority.
    /// </summary>
    public sealed partial class AstronautHandsPresenter
    {
        [Header("EVA tether climb (EVA-TETHER-01, optional)")]
        [Tooltip("The visible tether rope on the EVA route. Null keeps the older glove flex on the route.")]
        [SerializeField] private EvaTetherRope tetherRope;

        [SerializeField, Range(0.1f, 0.5f)] private float tetherBlendSeconds = 0.22f;

        /// <summary>Invisible upper-arm length used to place the elbow (the delivered forearm is 0.32 m), metres.</summary>
        public const float TetherUpperArmMetres = 0.40f;

        public const float TetherMaxObliquityDegrees = 28f;

        private static readonly Vector3 TetherShoulderInView = new Vector3(0.19f, -0.25f, -0.04f);
        private bool tetherActive;
        private int tetherFacing = 1;
        private float tetherBlendElapsed;
        private float tetherLastDistance = float.NaN;
        private readonly EvaTetherArmPose[] tetherPoses = new EvaTetherArmPose[2];
        private readonly float[] tetherGrip = new float[2];
        private readonly float[] tetherParam = new float[2];

        public EvaTetherRope TetherRope => tetherRope;

        /// <summary>True while the gloves are posed on the tether rope.</summary>
        public bool IsTetherClimbActive => tetherActive && !toolHoldActive && ContactStage == HandContactStage.None;

        /// <summary>+1 while the gloves reach towards the route end, -1 towards the start.</summary>
        public int TetherFacingSense => tetherFacing;

        /// <summary>The glove currently holding the leading rung (the pulling glove).</summary>
        public EvaTetherHand TetherHoldingHand { get; private set; }

        /// <summary>Phase of the current pull in [0, 1).</summary>
        public float TetherStrokePhase { get; private set; }

        /// <summary>How far the climb pose has blended in, 0 to 1.</summary>
        public float TetherBlend { get; private set; }

        /// <summary>Last solved pose of a glove on the rope (world frames).</summary>
        public EvaTetherArmPose TetherPose(EvaTetherHand hand) => tetherPoses[(int)hand];

        /// <summary>How closed a glove's fist is, 0 (open) to 1 (closed on the rope).</summary>
        public float TetherGrip(EvaTetherHand hand) => tetherGrip[(int)hand];

        /// <summary>Rope station (route distance) a glove holds or is swinging through.</summary>
        public float TetherRopeParam(EvaTetherHand hand) => tetherParam[(int)hand];

        /// <summary>Editor/composition wiring of the visible rope (EvaTetherAuthoring.Install).</summary>
        public void ConfigureTetherRope(EvaTetherRope rope)
        {
            ForgetTetherClimb();
            tetherRope = rope;
        }

        /// <summary>Off the rope (back inside, or no route): gloves return to neutral once, then normal gestures resume.</summary>
        private bool LeaveTetherClimb()
        {
            if (tetherActive)
            {
                ResetToNeutral(); // Also forgets the climb; the next pose is the ordinary neutral one.
            }

            return false;
        }

        private void ForgetTetherClimb()
        {
            tetherActive = false;
            tetherBlendElapsed = 0f;
            tetherLastDistance = float.NaN;
            TetherBlend = 0f;
        }

        /// <summary>Returns true while it owns the glove pose this frame.</summary>
        private bool AdvanceTetherClimb(float seconds)
        {
            if (!tetherRope || routeProgress == null || suit == null || !suit.IsOutside || !motionMount || !modelRoot)
            {
                return LeaveTetherClimb();
            }

            float distance;
            try
            {
                distance = routeProgress();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                return LeaveTetherClimb();
            }

            if (!Finite(distance) || !tetherRope.IsGeometryReady)
            {
                return LeaveTetherClimb();
            }

            Vector3 up = Vector3.up;
            Vector3 forward = ViewYawForward(up);
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 ropeHere = tetherRope.TangentAt(distance);
            float alignment = Vector3.Dot(forward, Vector3.ProjectOnPlane(ropeHere, up).normalized);
            bool restart = !tetherActive;
            if (!tetherActive)
            {
                tetherFacing = alignment >= 0f ? 1 : -1;
            }
            else if ((tetherFacing > 0 && alignment < -0.35f) || (tetherFacing < 0 && alignment > 0.35f))
            {
                tetherFacing = -tetherFacing;
                restart = true; // Turned round: both gloves re-grip in front.
            }

            if (Finite(tetherLastDistance) && Mathf.Abs(distance - tetherLastDistance) > 0.6f)
            {
                restart = true; // A reposition is not a pull.
            }

            tetherLastDistance = distance;
            if (restart)
            {
                foreach (JointBinding joint in bindings)
                {
                    if (!joint.Joint)
                    {
                        ClearBinding();
                        return false;
                    }

                    joint.GestureStartPosition = joint.Joint.localPosition;
                    joint.GestureStartRotation = joint.Joint.localRotation;
                }

                gesture = Gesture.None;
                gestureElapsed = gestureBlend = 0f;
                tetherBlendElapsed = 0f;
                tetherActive = true;
            }

            // A rendering hitch is not animation time (no catch-up jump).
            if (Finite(seconds) && seconds > 0f && seconds <= 0.1f)
            {
                tetherBlendElapsed = Mathf.Min(tetherBlendSeconds, tetherBlendElapsed + seconds);
            }

            float blend = Mathf.SmoothStep(0f, 1f, tetherBlendElapsed / Mathf.Max(0.05f, tetherBlendSeconds));
            TetherBlend = blend;
            SolveTetherPoses(distance, up, forward, right);
            return ApplyTetherPoses(blend);
        }

        private Vector3 ViewYawForward(Vector3 up)
        {
            Vector3 viewForward = motionMount.forward;
            Vector3 flat = Vector3.ProjectOnPlane(viewForward, up)
                + (Vector3.ProjectOnPlane(motionMount.up, up) * (Vector3.Dot(viewForward, up) < 0f ? 1f : -1f));
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        }

        private void SolveTetherPoses(float distance, Vector3 up, Vector3 forward, Vector3 right)
        {
            float stroke = tetherRope.HandStrokeMetres;
            float near = tetherRope.NearReachMetres;
            int facing = tetherFacing;
            float rung = EvaTetherClimbMath.HoldingRung(distance, facing, stroke, near);
            float phase = EvaTetherClimbMath.StrokePhase(distance, facing, stroke, near);
            EvaTetherHand holder = EvaTetherClimbMath.HandForRung(rung, stroke);
            EvaTetherHand free = holder == EvaTetherHand.Right ? EvaTetherHand.Left : EvaTetherHand.Right;
            TetherHoldingHand = holder;
            TetherStrokePhase = phase;

            float min = tetherRope.MinParam;
            float max = tetherRope.MaxGripParam;
            // Rungs are route distances; the glove holds the rope straight across from each (GripParam).
            float held = Mathf.Clamp(tetherRope.GripParam(rung), min, max);
            float swing = EvaTetherClimbMath.SwingProgress(phase);
            float from = Mathf.Clamp(tetherRope.GripParam(rung - (facing * stroke)), min, max);
            float to = Mathf.Clamp(tetherRope.GripParam(rung + (facing * stroke)), min, max);
            float swingParam = Mathf.Lerp(from, to, swing);
            tetherParam[(int)holder] = held;
            tetherParam[(int)free] = swingParam;
            tetherGrip[(int)holder] = 1f;
            tetherGrip[(int)free] = EvaTetherClimbMath.FreeGloveGrip(phase);

            Vector3 eye = motionMount.position;
            Vector3 ropeAtBody = tetherRope.PointAt(distance);
            float ropeSide = Vector3.Dot(ropeAtBody - eye, right) >= 0f ? 1f : -1f;
            float lift = EvaTetherClimbMath.SwingLiftMetres * Mathf.Sin(Mathf.PI * swing);
            tetherPoses[(int)holder] = SolveRopeGlove(tetherRope, holder, held, 0f, facing, eye, up, forward, right, ropeSide);
            tetherPoses[(int)free] = SolveRopeGlove(tetherRope, free, swingParam, lift, facing, eye, up, forward, right, ropeSide);
        }

        /// <summary>The closed glove's rope centre in the hand frame (same calibration as the rail grip).</summary>
        public static Vector3 TetherTubeCentreInHand => TubeCentreInHand;

        /// <summary>
        /// Solves one glove on the rope at a rope station (route distance) for an eye position and view yaw frame:
        /// shoulder from the view (reaching with the shoulder up to 30 cm when needed), an elbow bent outwards for the
        /// glove on the rope's side and downwards for the crossing glove, and the fist closed around the rope.
        /// <paramref name="lift"/> raises a swinging glove off the rope. Pure geometry, for this presenter and tests.
        /// </summary>
        public static EvaTetherArmPose SolveRopeGlove(EvaTetherRope tetherRope, EvaTetherHand hand, float param, float lift,
            int facing, Vector3 eye, Vector3 up, Vector3 forward, Vector3 right, float ropeSide)
        {
            float handSide = hand == EvaTetherHand.Right ? 1f : -1f;
            Vector3 shoulder = eye + (right * (handSide * TetherShoulderInView.x)) + (up * TetherShoulderInView.y)
                + (forward * TetherShoulderInView.z);
            bool sameSide = Mathf.Approximately(handSide, ropeSide);
            Vector3 sideOut = right * ropeSide;
            // Elbows out to the glove's own side, down and a little back, as when pulling a line; the sideways and
            // backward parts keep the bend well defined even when the rope runs straight below the shoulder.
            Vector3 pole = sameSide
                ? ((sideOut * 0.65f) - (up * 0.6f) - (forward * 0.25f)).normalized
                : ((-sideOut * 0.55f) - (up * 0.7f) - (forward * 0.2f)).normalized;
            Vector3 ropeForward = tetherRope.TangentAt(param) * facing;
            // On a steep stretch (the hatch climb) the line runs down along the arm: swing the elbows further out
            // so the forearm crosses the rope instead of lying along it.
            float steep = Mathf.Clamp01((Mathf.Abs(Vector3.Dot(ropeForward, up)) - 0.3f) / 0.4f);
            if (steep > 0f)
            {
                Vector3 outward = ((sameSide ? sideOut : -sideOut) - (forward * 0.3f)).normalized;
                pole = Vector3.Slerp(pole, outward, steep);
            }

            Vector3 centre = tetherRope.PointAt(param);
            if (lift > 0f)
            {
                EvaTetherArmPose resting = SolveReaching(centre, ropeForward, hand, shoulder, pole, up);
                Vector3 liftDirection = ((resting.Hand * Vector3.up) + (up * 0.6f)).normalized;
                centre += liftDirection * lift;
            }

            return SolveReaching(centre, ropeForward, hand, shoulder, pole, up);
        }

        /// <summary>
        /// Solves the grip, reaching with the shoulder (protraction and a torso turn, at most 30 cm in all) until the
        /// wrist is within arm's length, so the delivered forearm always meets its elbow.
        /// </summary>
        private static EvaTetherArmPose SolveReaching(Vector3 centre, Vector3 ropeForward, EvaTetherHand hand,
            Vector3 shoulder, Vector3 pole, Vector3 up)
        {
            const float maxLean = 0.3f;
            float reach = TetherUpperArmMetres + ToolForearmMetres;
            float leaned = 0f;
            Vector3 toward = centre - shoulder;
            float excess = toward.magnitude - (reach - 0.03f);
            if (excess > 0f)
            {
                leaned = Mathf.Min(maxLean, excess);
                shoulder += toward.normalized * leaned;
            }

            EvaTetherArmPose pose = EvaTetherClimbMath.SolveGrip(centre, ropeForward, hand, shoulder, pole, up,
                TubeCentreInHand, TetherUpperArmMetres, ToolForearmMetres, TetherMaxObliquityDegrees);
            for (int pass = 0; pass < 2 && !pose.Reachable && leaned < maxLean; pass++)
            {
                // The wrist sits off the rope centre, so the first lean can fall a few centimetres short.
                Vector3 toWrist = pose.Wrist - shoulder;
                float deficit = toWrist.magnitude - (reach - 0.01f);
                float step = Mathf.Min(maxLean - leaned, Mathf.Max(0.005f, deficit));
                shoulder += toWrist.normalized * step;
                leaned += step;
                pose = EvaTetherClimbMath.SolveGrip(centre, ropeForward, hand, shoulder, pole, up,
                    TubeCentreInHand, TetherUpperArmMetres, ToolForearmMetres, TetherMaxObliquityDegrees);
            }

            return pose;
        }

        private bool ApplyTetherPoses(float blend)
        {
            Quaternion rootInverse = Quaternion.Inverse(modelRoot.rotation);
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint)
                {
                    ClearBinding();
                    return false;
                }

                int index = joint.Left ? (int)EvaTetherHand.Left : (int)EvaTetherHand.Right;
                EvaTetherArmPose pose = tetherPoses[index];
                Vector3 position;
                Quaternion rotation;
                string name = joint.Joint.name;
                if (name.EndsWith("_Arm", StringComparison.Ordinal))
                {
                    position = modelRoot.InverseTransformPoint(pose.Wrist);
                    rotation = rootInverse * pose.Arm;
                }
                else if (name.EndsWith("_Hand", StringComparison.Ordinal))
                {
                    position = joint.AuthoredPosition;
                    rotation = Quaternion.Inverse(pose.Arm) * pose.Hand;
                }
                else
                {
                    position = joint.AuthoredPosition;
                    rotation = Quaternion.Slerp(joint.NeutralRotation, joint.GripRotation, tetherGrip[index]);
                }

                joint.Joint.SetLocalPositionAndRotation(
                    Vector3.Lerp(joint.GestureStartPosition, position, blend),
                    Quaternion.Slerp(joint.GestureStartRotation, rotation, blend));
            }

            return true;
        }
    }
}

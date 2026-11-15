using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Which glove (EVA-TETHER-01).</summary>
    public enum EvaTetherHand
    {
        Right = 0,
        Left = 1,
    }

    /// <summary>One solved glove placement on the tether: rigid forearm frame at the wrist, hand frame and elbow.</summary>
    public struct EvaTetherArmPose
    {
        public Vector3 Wrist;
        public Quaternion Arm;
        public Quaternion Hand;
        public Vector3 Elbow;
        public Vector3 Shoulder;
        public float WristBendDegrees;
        public float RopeObliquityDegrees;
        public bool Reachable;
    }

    /// <summary>
    /// EVA-TETHER-01: pure hand-over-hand rules for the gloves. Grip stations ("rungs") sit every stroke along the
    /// route line, counted from its start. The leading glove holds the rung inside the reach band (near, near + stroke]
    /// ahead of the body in the facing sense and stays fixed on it while the body moves; the other glove releases the
    /// rung that reached the chest, swings forward and closes on the next rung just before the leading glove arrives
    /// by the chest. Same lattice and phase as <c>OutOfReach.Player.RouteMath</c> (checked by tests); no gameplay here.
    /// </summary>
    public static class EvaTetherClimbMath
    {
        /// <summary>Phase at which the released glove has opened enough to leave its rung.</summary>
        public const float ReleaseOpenPhase = 0.08f;

        /// <summary>Phase at which the free glove starts swinging forward.</summary>
        public const float ReachStartPhase = 0.06f;

        /// <summary>Phase at which the free glove arrives at its next rung and starts closing.</summary>
        public const float ReachEndPhase = 0.82f;

        /// <summary>Phase from which both gloves hold the rope (the change-over overlap).</summary>
        public const float GripClosedPhase = 0.95f;

        /// <summary>Peak lift of a swinging glove off the rope, metres.</summary>
        public const float SwingLiftMetres = 0.065f;

        public static float HoldingRung(float distance, int facingSense, float strokeMetres, float nearMetres)
        {
            float stroke = Mathf.Max(0.05f, strokeMetres);
            float near = Mathf.Max(0f, nearMetres);
            float d = float.IsNaN(distance) ? 0f : distance;
            if (facingSense >= 0)
            {
                return (Mathf.Floor((d + near) / stroke) + 1f) * stroke;
            }

            return (Mathf.Ceil((d - near) / stroke) - 1f) * stroke;
        }

        public static float StrokePhase(float distance, int facingSense, float strokeMetres, float nearMetres)
        {
            float stroke = Mathf.Max(0.05f, strokeMetres);
            float near = Mathf.Max(0f, nearMetres);
            float d = float.IsNaN(distance) ? 0f : distance;
            float rung = HoldingRung(d, facingSense, stroke, near);
            float ahead = facingSense >= 0 ? rung - d : d - rung;
            return Mathf.Clamp01((near + stroke - ahead) / stroke) % 1f;
        }

        /// <summary>Rungs alternate gloves: even rung index = right glove, odd = left glove.</summary>
        public static EvaTetherHand HandForRung(float rung, float strokeMetres)
        {
            int index = Mathf.RoundToInt(rung / Mathf.Max(0.05f, strokeMetres));
            return (index & 1) == 0 ? EvaTetherHand.Right : EvaTetherHand.Left;
        }

        /// <summary>Progress of the free glove's swing from its released rung to its next rung, 0 to 1 (eased).</summary>
        public static float SwingProgress(float phase)
        {
            float t = Mathf.Clamp01((phase - ReachStartPhase) / (ReachEndPhase - ReachStartPhase));
            return t * t * (3f - (2f * t));
        }

        /// <summary>How closed the free glove's fist is through one pull of the other glove, 0 (open) to 1 (closed).</summary>
        public static float FreeGloveGrip(float phase)
        {
            if (phase < ReleaseOpenPhase)
            {
                return 1f - Mathf.SmoothStep(0f, 1f, phase / ReleaseOpenPhase);
            }

            if (phase < ReachEndPhase)
            {
                return 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - ReachEndPhase) / (GripClosedPhase - ReachEndPhase)));
        }

        /// <summary>
        /// Two-bone arm: the elbow for a shoulder, wrist, segment lengths and a bend (pole) direction. Out of reach,
        /// the arm points straight at the wrist (the elbow sits one upper-arm length along it).
        /// </summary>
        public static Vector3 SolveElbow(Vector3 shoulder, Vector3 wrist, float upperMetres, float foreMetres, Vector3 pole,
            out bool reachable)
        {
            Vector3 axis = wrist - shoulder;
            float distance = axis.magnitude;
            reachable = distance <= upperMetres + foreMetres + 0.002f;
            if (distance < 1e-4f)
            {
                return shoulder + (pole.sqrMagnitude > 1e-6f ? pole.normalized : Vector3.down) * upperMetres;
            }

            Vector3 direction = axis / distance;
            float clamped = Mathf.Clamp(distance, Mathf.Abs(upperMetres - foreMetres) + 0.002f, upperMetres + foreMetres - 0.0005f);
            float along = ((upperMetres * upperMetres) - (foreMetres * foreMetres) + (clamped * clamped)) / (2f * clamped);
            float height = Mathf.Sqrt(Mathf.Max(0f, (upperMetres * upperMetres) - (along * along)));
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction);
            if (bend.sqrMagnitude < 1e-6f)
            {
                bend = Vector3.ProjectOnPlane(Vector3.down, direction);
            }

            if (bend.sqrMagnitude < 1e-6f)
            {
                bend = Vector3.ProjectOnPlane(Vector3.right, direction);
            }

            return shoulder + (direction * along) + (bend.normalized * height);
        }

        /// <summary>
        /// Places one glove closed around the rope: the rope runs across the knuckles (the glove's grip axis, local
        /// X), thumb side towards the facing sense, fingers wrapping from the side of the arrival direction. A rope
        /// that runs nearly along the forearm is gripped obliquely (up to <paramref name="maxObliquityDegrees"/>),
        /// as a real power grip does, to keep the wrist bend natural. <paramref name="tubeCentreInHand"/> is the rope
        /// centre in the closed glove's frame.
        /// </summary>
        public static EvaTetherArmPose SolveGrip(Vector3 ropeCentre, Vector3 ropeForward, EvaTetherHand hand,
            Vector3 shoulder, Vector3 pole, Vector3 bodyUp, Vector3 tubeCentreInHand, float upperMetres, float foreMetres,
            float maxObliquityDegrees)
        {
            Vector3 forward = ropeForward.sqrMagnitude > 1e-8f ? ropeForward.normalized : Vector3.forward;
            Vector3 thumbForward = hand == EvaTetherHand.Left ? forward : -forward;
            // Thumb towards the facing sense is the natural pull; where the rope runs along the forearm (steep or
            // behind the shoulder) the reversed grip can bend the wrist far less, so keep the better of the two.
            EvaTetherArmPose best = SolveGripAxis(ropeCentre, thumbForward, shoulder, pole, bodyUp, tubeCentreInHand,
                upperMetres, foreMetres, maxObliquityDegrees);
            if (best.WristBendDegrees > 55f)
            {
                EvaTetherArmPose reversed = SolveGripAxis(ropeCentre, -thumbForward, shoulder, pole, bodyUp,
                    tubeCentreInHand, upperMetres, foreMetres, maxObliquityDegrees);
                if (reversed.WristBendDegrees + 10f < best.WristBendDegrees)
                {
                    best = reversed;
                }
            }

            return best;
        }

        private static EvaTetherArmPose SolveGripAxis(Vector3 ropeCentre, Vector3 x, Vector3 shoulder, Vector3 pole,
            Vector3 bodyUp, Vector3 tubeCentreInHand, float upperMetres, float foreMetres, float maxObliquityDegrees)
        {
            // Fixed-point refinement: elbow -> grip frame -> wrist -> elbow, so the fingers follow the final forearm.
            Vector3 wrist = ropeCentre;
            Vector3 elbow = SolveElbow(shoulder, wrist, upperMetres, foreMetres, pole, out bool reachable);
            Quaternion handRotation = Quaternion.identity;
            float oblique = 0f;
            for (int pass = 0; pass < 3; pass++)
            {
                handRotation = GripRotation(x, wrist - elbow, bodyUp, maxObliquityDegrees, out oblique);
                wrist = ropeCentre - (handRotation * tubeCentreInHand);
                elbow = SolveElbow(shoulder, wrist, upperMetres, foreMetres, pole, out reachable);
            }

            Vector3 forearm = wrist - elbow;
            if (forearm.sqrMagnitude < 1e-8f)
            {
                forearm = wrist - shoulder;
            }

            Vector3 armUp = Vector3.ProjectOnPlane(handRotation * Vector3.up, forearm.normalized);
            if (armUp.sqrMagnitude < 1e-4f)
            {
                armUp = Vector3.ProjectOnPlane(bodyUp, forearm.normalized);
            }

            Quaternion arm = Quaternion.LookRotation(forearm, armUp.sqrMagnitude > 1e-6f ? armUp : Vector3.up);
            return new EvaTetherArmPose
            {
                Wrist = wrist,
                Arm = arm,
                Hand = handRotation,
                Elbow = elbow,
                Shoulder = shoulder,
                WristBendDegrees = Quaternion.Angle(arm, handRotation),
                RopeObliquityDegrees = oblique,
                Reachable = reachable,
            };
        }

        private static Quaternion GripRotation(Vector3 x, Vector3 forearm, Vector3 bodyUp, float maxObliquityDegrees,
            out float obliquity)
        {
            Vector3 z = Vector3.ProjectOnPlane(forearm, x);
            if (z.sqrMagnitude < 1e-6f)
            {
                z = Vector3.ProjectOnPlane(-bodyUp, x);
            }

            if (z.sqrMagnitude < 1e-6f)
            {
                z = Vector3.ProjectOnPlane(Vector3.forward, x);
            }

            z.Normalize();
            Vector3 y = Vector3.Cross(z, x).normalized;
            Quaternion rotation = Quaternion.LookRotation(z, y);
            obliquity = 0f;
            if (forearm.sqrMagnitude > 1e-8f)
            {
                // Tilt the knuckle line towards the forearm (rotation about the back-of-hand axis), as a power grip.
                float misalignment = Vector3.Angle(z, forearm);
                float tilt = Mathf.Clamp(misalignment - 18f, 0f, Mathf.Max(0f, maxObliquityDegrees));
                Vector3 axis = Vector3.Cross(z, forearm);
                if (tilt > 0f && axis.sqrMagnitude > 1e-8f)
                {
                    rotation = Quaternion.AngleAxis(tilt, axis.normalized) * rotation;
                    obliquity = tilt;
                }
            }

            return rotation;
        }
    }
}

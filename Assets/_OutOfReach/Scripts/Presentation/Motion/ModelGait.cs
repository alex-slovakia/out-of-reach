using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Gait character of one outfit on the shared 164-bone rig.</summary>
    public readonly struct ModelGaitProfile
    {
        public readonly float StrideScale, LiftScale, ArmSwingScale, ArmAbduction, StanceWiden, SwayScale, PelvisYaw, PelvisRoll, Lean;

        public ModelGaitProfile(float strideScale, float liftScale, float armSwingScale, float armAbduction, float stanceWiden,
            float swayScale, float pelvisYaw, float pelvisRoll, float lean)
        {
            StrideScale = strideScale; LiftScale = liftScale; ArmSwingScale = armSwingScale; ArmAbduction = armAbduction;
            StanceWiden = stanceWiden; SwayScale = swayScale; PelvisYaw = pelvisYaw; PelvisRoll = pelvisRoll; Lean = lean;
        }

        /// <summary>Shirt and trousers: free arm swing and normal pelvis rotation.</summary>
        public static readonly ModelGaitProfile Work = new ModelGaitProfile(1f, 1f, 1f, 0f, 0f, 1f, 5f, 3f, 0f);

        /// <summary>Pressure suit: shorter, wider, stiffer steps, arms held off the bulky torso, more side-to-side rock.</summary>
        public static readonly ModelGaitProfile Eva = new ModelGaitProfile(.9f, .8f, .5f, 10f, .03f, 1.4f, 2.5f, 1.5f, 2f);
    }

    /// <summary>
    /// Pure, allocation-free gait math for the passive first-person body. Everything is expressed in fractions of the
    /// measured leg length so both outfits (same 164-bone rig) share one model. No scene state lives here.
    /// Foot trajectories are defined in the body frame: during stance the foot moves exactly opposite the body's
    /// travel (a planted sole never slides); swing is a Hermite arc whose end tangents match the stance velocity.
    /// </summary>
    public static class ModelGait
    {
        /// <summary>Default leg length (thigh + shin) of the reference body, metres. Real value is measured per rig.</summary>
        public const float ReferenceLegLength = .90f;
        public const float WalkSpeed = 2.5f, SprintFactor = 1.5f;

        /// <summary>Stride length (same foot to same foot), metres. Human gait: about 1.55 L at 1.4 m/s, 2.2 L at 2.5 m/s.</summary>
        public static float StrideLength(float speed, float legLength)
        {
            float v = Mathf.Clamp(speed, .25f, 6f);
            return legLength * Mathf.Clamp(1.25f * Mathf.Pow(v, .62f), .55f, 3.2f);
        }

        /// <summary>Stride shortened for sideways travel so the legs do not cross.</summary>
        public static float StrideLength(float speed, float legLength, float lateral01) =>
            StrideLength(speed, legLength) * Mathf.Lerp(1f, .72f, Mathf.Clamp01(lateral01));

        /// <summary>Fraction of the cycle a foot is on the ground. Stays above .5 in a walk and drops towards a run.</summary>
        public static float Stance(float speed) => Mathf.Clamp(.70f - .085f * speed, .36f, .66f);

        /// <summary>0 at a slow shuffle, 1 at a full sprint.</summary>
        public static float Intensity(float speed) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, 4f, speed));

        /// <summary>Peak clearance of the swing foot above the planted sole.</summary>
        public static float Lift(float speed, float legLength) => legLength * Mathf.Lerp(.05f, .25f, Intensity(speed));

        /// <summary>Cycles of one foot per second.</summary>
        public static float CyclesPerSecond(float speed, float legLength) => speed / StrideLength(speed, legLength);

        /// <summary>Steps (both feet) per minute.</summary>
        public static float StepsPerMinute(float speed, float legLength) => 120f * CyclesPerSecond(speed, legLength);

        /// <summary>
        /// Foot offset along the direction of travel, relative to the standing foot, at cycle <paramref name="phase"/>
        /// (0 = heel strike). The stance half is linear: foot velocity relative to the body equals minus the body speed.
        /// </summary>
        public static float FootForeAft(float phase, float stride, float stance)
        {
            float reach = stride * stance * .5f;
            phase = Mathf.Repeat(phase, 1f);
            if (phase < stance) return Mathf.Lerp(reach, -reach, phase / stance);
            float s = (phase - stance) / (1f - stance);
            float tangent = -stride * (1f - stance);
            float s2 = s * s, s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * -reach + (s3 - 2f * s2 + s) * tangent
                + (-2f * s3 + 3f * s2) * reach + (s3 - s2) * tangent;
        }

        /// <summary>Sole height above the ground: zero throughout stance, one skewed bump in swing that peaks early.</summary>
        public static float FootHeight(float phase, float stance, float lift)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (phase < stance) return 0f;
            float s = (phase - stance) / (1f - stance);
            float bump = Mathf.Sin(Mathf.PI * Mathf.Pow(s, .85f));
            return lift * bump * bump;
        }

        /// <summary>
        /// Foot pitch in degrees, toes up positive: heel strike, roll flat, heel rise and toe-off, then a swing that
        /// rotates from toe-off back to a toes-up landing.
        /// </summary>
        public static float FootPitch(float phase, float stance, float speed)
        {
            float k = Intensity(speed);
            float heelStrike = Mathf.Lerp(11f, 5f, k), toeOff = Mathf.Lerp(20f, 32f, k);
            phase = Mathf.Repeat(phase, 1f);
            if (phase < stance)
            {
                float q = phase / stance;
                float land = heelStrike * (1f - Mathf.SmoothStep(0f, 1f, q / .18f));
                float rise = -toeOff * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, q));
                return land + rise;
            }
            float s = (phase - stance) / (1f - stance);
            return Mathf.Lerp(-toeOff, heelStrike, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s * 1.25f)));
        }

        /// <summary>
        /// Ankle displacement (forward, up) that keeps the true contact point planted while the foot pitches:
        /// the heel for toes-up pitch, the ball for heel rise. Heel is <paramref name="heelBack"/> behind the ankle,
        /// ball <paramref name="ballForward"/> ahead, both <paramref name="ankleHeight"/> above the sole.
        /// </summary>
        public static Vector2 RockerAnkleShift(float pitchDegrees, float heelBack, float ballForward, float ankleHeight)
        {
            float t = pitchDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            Vector2 pivot = t >= 0f ? new Vector2(-heelBack, -ankleHeight) : new Vector2(ballForward, -ankleHeight);
            Vector2 rotated = new Vector2(pivot.x * c - pivot.y * s, pivot.x * s + pivot.y * c);
            return pivot - rotated;
        }

        /// <summary>
        /// Lateral pelvis shift (+x = body right) over the stance foot. <paramref name="leftPhase"/> is the LEFT foot's
        /// cycle phase; the left foot stands at about a quarter cycle, which moves the pelvis to the left (negative).
        /// </summary>
        public static float PelvisSway(float leftPhase, float legLength, float speed)
        {
            float width = legLength * Mathf.Lerp(.022f, .008f, Intensity(speed));
            return -width * Mathf.Sin(2f * Mathf.PI * leftPhase);
        }

        /// <summary>Pelvis setback (metres, along minus forward) that clears the lower view when the camera looks down.</summary>
        public static float LookDownSetback(float cameraPitchDownDegrees, float legLength)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 72f, cameraPitchDownDegrees));
            return legLength * .13f * t;
        }

        /// <summary>Quintic ease: zero velocity and acceleration at both ends (a foot leaves and lands without a skid).</summary>
        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (u * (6f * u - 15f) + 10f);
        }

        /// <summary>Lift profile of one step: zero at both ends, one bump that peaks early (about 44 %).</summary>
        public static float StepBump(float u)
        {
            u = Mathf.Clamp01(u);
            float b = Mathf.Sin(Mathf.PI * Mathf.Pow(u, .85f));
            return b * b;
        }

        /// <summary>Pelvis yaw in degrees for the LEFT foot phase; positive brings the left hip forward (left heel strike).</summary>
        public static float PelvisYaw(float leftPhase, float amplitude) => amplitude * Mathf.Cos(2f * Mathf.PI * leftPhase);

        /// <summary>Pelvis roll in degrees; positive raises the right hip. The hip of the swinging leg drops.</summary>
        public static float PelvisRoll(float leftPhase, float amplitude) => -amplitude * Mathf.Sin(2f * Mathf.PI * leftPhase);

        /// <summary>Forward trunk lean in degrees that grows with speed.</summary>
        public static float ForwardLean(float speed) => Mathf.Lerp(1.5f, 5f, Intensity(speed));

        /// <summary>Opposing arm swing amplitude in degrees.</summary>
        public static float ArmSwing(float speed) => Mathf.Lerp(9f, 24f, Intensity(speed) * .6f + .4f * Mathf.InverseLerp(.2f, 2.5f, speed));
    }
}

using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>
    /// SLEEP-SEQ: first-person path between the live gameplay eye and the eye of the body in the berth. Pure math, no
    /// state. Approach: float to the berth mouth, turn round and back into the bag. Exit: the same way out, back to
    /// the live eye. Camera and body move as one rig along it.
    /// </summary>
    public static class BerthApproachPath
    {
        /// <summary>Berth mouth: this far in front of the berth eye along the berth's outward (chest) direction, metres.</summary>
        public const float MouthDepth = .62f;
        /// <summary>A rest that starts farther than this from the berth eye shows no walk-in.</summary>
        public const float MaxPresentDistance = 3.2f;
        /// <summary>View pitch (degrees down) while strapping (the hands at the chest), once settled, and while unbuckling.</summary>
        public const float StrapPitch = 60f, HeldPitch = 18f, UnbucklePitch = 56f;
        /// <summary>Ordinary wake: the eyes open in the berth, then the hand unbuckles, then the exit.</summary>
        public const float WakeSeconds = .5f, UnbuckleSeconds = .95f;
        /// <summary>Hazard wake: the straps spring open and gameplay input is back within this time.</summary>
        public const float HazardControlSeconds = .25f;
        /// <summary>Berth exit: feet this far in front of root's SleepEntry (clear of the berth face), just above the deck.</summary>
        public const float ExitStandOff = .30f, ExitFeetLift = .03f;

        /// <summary>Hazard wake: a quick float out to the live eye (input is already back after HazardControlSeconds).</summary>
        public static float HazardExitSeconds(Vector3 from, Vector3 mouth, Vector3 liveEye) =>
            Mathf.Clamp(.35f + .2f * (Vector3.Distance(from, mouth) + Vector3.Distance(mouth, liveEye)), .5f, .9f);

        public struct View
        {
            public Vector3 Position;
            public float Yaw, Pitch, Roll;
            public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, Roll);
        }

        public static View FromPose(Vector3 position, Quaternion rotation)
        {
            Vector3 e = rotation.eulerAngles;
            return new View { Position = position, Yaw = e.y, Pitch = Mathf.DeltaAngle(0f, e.x), Roll = Mathf.DeltaAngle(0f, e.z) };
        }

        public static Vector3 Mouth(Vector3 berthEye, Vector3 berthForward)
        {
            Vector3 flat = Vector3.ProjectOnPlane(berthForward, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) flat = berthForward;
            return berthEye + flat.normalized * MouthDepth;
        }

        /// <summary>Unhurried float time for a path of this length (approach).</summary>
        public static float ApproachSeconds(Vector3 from, Vector3 mouth, Vector3 berthEye) =>
            Mathf.Clamp(1.1f + .7f * (Vector3.Distance(from, mouth) + Vector3.Distance(mouth, berthEye)), 1.6f, 2.8f);

        /// <summary>Float time out of the berth to the live eye (ordinary exit).</summary>
        public static float ExitSeconds(Vector3 from, Vector3 mouth, Vector3 liveEye) =>
            Mathf.Clamp(1f + .6f * (Vector3.Distance(from, mouth) + Vector3.Distance(mouth, liveEye)), 1.4f, 2.4f);

        /// <summary>Signed yaw turn; a near-half turn always goes the preferred way so the exit mirrors the approach.</summary>
        public static float Turn(float fromYaw, float toYaw, float preferredSign)
        {
            float delta = Mathf.DeltaAngle(fromYaw, toYaw);
            if (Mathf.Abs(delta) > 150f && preferredSign != 0f && Mathf.Sign(delta) != Mathf.Sign(preferredSign))
                delta -= 360f * Mathf.Sign(delta);
            return delta;
        }

        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
        {
            float v = 1f - u;
            return v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d;
        }

        /// <summary>
        /// Approach at progress <paramref name="u"/> (0 = live eye, 1 = berth eye): towards the mouth while turning
        /// round, then straight back into the bag along the berth axis. Pitch ends looking down at the straps.
        /// </summary>
        public static View Approach(View live, Vector3 mouth, Vector3 berthEye, float berthYaw, float turnSign, float u)
        {
            float p = Ease(u);
            return new View
            {
                Position = Bezier(live.Position, Vector3.Lerp(live.Position, mouth, .8f), mouth, berthEye, p),
                Yaw = live.Yaw + Turn(live.Yaw, berthYaw, turnSign) * Ease(Mathf.InverseLerp(.04f, .86f, p)),
                Pitch = Mathf.Lerp(live.Pitch, StrapPitch, Ease(Mathf.InverseLerp(.5f, 1f, p))),
                Roll = Mathf.Lerp(live.Roll, 0f, Ease(Mathf.InverseLerp(0f, .4f, p)))
            };
        }

        /// <summary>
        /// Exit at progress <paramref name="u"/> (0 = where the view was at wake, 1 = the live eye): out of the bag along
        /// the berth axis, then turning back the way the approach came, onto the live gameplay view.
        /// </summary>
        public static View Exit(View from, View live, Vector3 mouth, Vector3 berthEye, float turnSign, float u)
        {
            float p = Ease(u);
            float depth = Mathf.Clamp01(1f - Vector3.Distance(from.Position, berthEye) / MouthDepth);
            Vector3 outward = mouth - berthEye;
            return new View
            {
                Position = Bezier(from.Position, from.Position + outward * depth, Vector3.Lerp(live.Position, mouth, .35f * depth),
                    live.Position, p),
                Yaw = from.Yaw + Turn(from.Yaw, live.Yaw, -turnSign) * Ease(Mathf.InverseLerp(.12f, .95f, p)),
                Pitch = Mathf.Lerp(from.Pitch, live.Pitch, Ease(Mathf.InverseLerp(.15f, 1f, p))),
                Roll = Mathf.Lerp(from.Roll, live.Roll, Ease(Mathf.InverseLerp(.5f, 1f, p)))
            };
        }
    }
}

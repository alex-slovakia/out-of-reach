using System;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>One sampled frame of the meal: where the pouch is and how the hand holds it. Visual only.</summary>
    public struct MealPouchFrame
    {
        public Vector3 Position;
        public Quaternion Rotation;
        /// <summary>Show the opened variant (top cut off) instead of the sealed one.</summary>
        public bool Opened;
        /// <summary>The pouch is visible (false after it went into the wet-trash bag).</summary>
        public bool Visible;
        /// <summary>Thickness factor along the pouch's own z: a dry rehydratable pouch is flatter until water goes in.</summary>
        public float Thickness;
        /// <summary>0..1 how much the right arm follows the pouch (reach in, carry, release).</summary>
        public float HandWeight;
        /// <summary>0..1 how far the measured clasp has closed.</summary>
        public float Clasp;
        /// <summary>0..1 how much the pouch rides in the actual hand (0 while it still lies in the drawer).</summary>
        public float Attach;
    }

    /// <summary>
    /// World anchors of one meal, captured when it starts. The camera is frozen by the composition's movement lock while
    /// a meal runs, so the view-relative keys stay where the player looked when E was pressed.
    /// </summary>
    public struct MealStations
    {
        public Vector3 EyePosition;
        public Quaternion EyeRotation;
        /// <summary>The galley ration drawer surface (forward = out of the drawer into the aisle).</summary>
        public Vector3 DrawerPosition;
        public Quaternion DrawerRotation;
        /// <summary>The potable water dispenser's needle nozzle, or null when the habitat frame is missing.</summary>
        public Vector3? Nozzle;
        /// <summary>The front of the strapped food warmer, or null.</summary>
        public Vector3? Warmer;
        /// <summary>The wet-trash bag opening, or null.</summary>
        public Vector3? Trash;
        /// <summary>Into the galley (the opposite of the drawer's forward), used for insert directions.</summary>
        public Vector3 IntoGalley;
        public Vector3 Up;
    }

    /// <summary>
    /// Deterministic pouch path for one meal. Keys come from the reviewed GALLEY-USE-MOTION-01 clip (feature/galley-use-motion-v1
    /// @79447bf: lifted, held low in the view, near the face, opening towards the lips) re-targeted to the actual galley
    /// anchors and the eye at E. The poses are ideal targets: the presenter lets the actual arm reach towards them and the
    /// pouch rides in the hand (a far station is reached towards, never teleported to; see <see cref="ClampReach"/>).
    /// Pure function of (stations, preparation, stage, progress): no clock, no command, no scene state.
    /// </summary>
    public sealed class MealChoreography
    {
        // Eye-relative keys (x right, y up, z forward, metres). The assumed mouth is GALLEY-USE-MOTION-01's (0, -0.085, 0.03).
        public static readonly Vector3 HoldKey = new Vector3(.085f, -.150f, .380f);
        public static readonly Vector3 OpenKey = new Vector3(.050f, -.125f, .330f);
        public static readonly Vector3 NearFaceKey = new Vector3(.012f, -.140f, .215f);
        public static readonly Vector3 BiteKey = new Vector3(.004f, -.118f, .158f);
        public static readonly Vector3 MouthKey = new Vector3(0f, -.085f, .030f);
        /// <summary>Right shoulder relative to the eye when no body bone is available (the antenna worksite uses the same).</summary>
        public static readonly Vector3 ShoulderKey = new Vector3(.18f, -.28f, -.08f);

        private readonly MealStations stations;
        private readonly MealPreparation preparation;
        private readonly bool labelCheck;
        private readonly Pose drawer, drawerOut, hold, open, nearFace, bite, check;
        private readonly Pose? station, trash;

        public MealChoreography(MealStations actual, MealPreparation kind, bool repeatedMenu)
        {
            stations = actual;
            preparation = kind;
            labelCheck = repeatedMenu;
            Quaternion eye = Normalize(actual.EyeRotation);
            Vector3 up = actual.Up.sqrMagnitude > 1e-6f ? actual.Up.normalized : Vector3.up;
            Vector3 drawerForward = Normalize(actual.DrawerRotation) * Vector3.forward;

            // In the drawer: standing upright, label out towards the aisle, leaning back 12 degrees like the stowed pouches.
            Quaternion inDrawer = Quaternion.LookRotation(drawerForward, up) * Quaternion.Euler(-12f, 0f, 0f);
            drawer = new Pose(actual.DrawerPosition - drawerForward * .03f - up * .03f, inDrawer);
            drawerOut = new Pose(actual.DrawerPosition + drawerForward * .09f - up * .02f, inDrawer);

            hold = FacingEye(eye, actual.EyePosition, HoldKey, -9f);
            check = FacingEye(eye, actual.EyePosition, HoldKey + new Vector3(-.025f, .020f, -.035f), 0f);
            open = FacingEye(eye, actual.EyePosition, OpenKey, -18f);
            nearFace = ToMouth(eye, actual.EyePosition, NearFaceKey);
            bite = ToMouth(eye, actual.EyePosition, BiteKey);

            Vector3 into = actual.IntoGalley.sqrMagnitude > 1e-6f ? actual.IntoGalley.normalized : -drawerForward;
            if (kind == MealPreparation.Rehydrated && actual.Nozzle.HasValue)
            {
                // Upright under the needle, label to the aisle: the top seal (0.093 m above the origin) just below the nozzle.
                station = new Pose(actual.Nozzle.Value - up * .105f, Quaternion.LookRotation(-into, up));
            }
            else if (kind == MealPreparation.Warmed && actual.Warmer.HasValue)
            {
                // Flat, label up, sliding into the warmer's front.
                station = new Pose(actual.Warmer.Value + -into * .02f, Quaternion.LookRotation(up, into));
            }

            if (actual.Trash.HasValue)
                trash = new Pose(actual.Trash.Value, Quaternion.LookRotation(up, into));
        }

        public MealPreparation Preparation => preparation;
        public Pose Hold => hold;
        public Pose? Station => station;

        /// <summary>The pouch frame at a stage and its 0..1 progress.</summary>
        public MealPouchFrame Sample(MealStage stage, double stageProgress)
        {
            float s = Mathf.Clamp01((float)stageProgress);
            float thickness = preparation == MealPreparation.Rehydrated ? .76f : 1f;
            switch (stage)
            {
                case MealStage.Take:
                {
                    // Reach in (hand only), clasp at the drawer, lift out, carry to the low hold in view.
                    if (s < .30f) return Frame(drawer, false, thickness, Smooth(s / .30f), 0f, 0f);
                    if (s < .45f) return Frame(drawer, false, thickness, 1f, Smooth((s - .30f) / .15f), Smooth((s - .30f) / .25f));
                    float k = (s - .45f) / .55f;
                    Pose p = k < .35f ? Blend(drawer, drawerOut, Smooth(k / .35f)) : Blend(drawerOut, hold, Smooth((k - .35f) / .65f));
                    return Frame(p, false, thickness, 1f, 1f, Smooth((s - .30f) / .25f));
                }
                case MealStage.Prepare:
                    return SamplePrepare(s, thickness);
                case MealStage.Open:
                {
                    float start = 0f;
                    Pose from = hold;
                    if (labelCheck)
                    {
                        // Restrained repeat reaction: a short look at the familiar label before opening. No taste claim.
                        start = .40f;
                        if (s < start) return Frame(Blend(hold, check, Bell(s / start)), false, 1f, 1f, 1f);
                    }
                    float k = (s - start) / (1f - start);
                    bool opened = k >= .60f;
                    Pose p = Blend(from, open, Smooth(Mathf.Min(1f, k / .55f)));
                    if (opened)
                    {
                        // The cut: a quick 7 degree jerk about the view axis as the top comes off.
                        float jerk = Mathf.Sin(Mathf.Clamp01((k - .60f) / .25f) * Mathf.PI) * 7f;
                        p = new Pose(p.position, p.rotation * Quaternion.Euler(0f, 0f, jerk));
                    }
                    return Frame(p, opened, 1f, 1f, 1f);
                }
                case MealStage.Eat:
                {
                    Pose p;
                    if (s < .25f) p = Blend(open, nearFace, Smooth(s / .25f));
                    else if (s < .45f) p = Blend(nearFace, bite, Bell((s - .25f) / .20f));
                    else if (s < .65f) p = Blend(nearFace, bite, Bell((s - .45f) / .20f));
                    else if (s < .80f) p = nearFace;
                    else p = Blend(nearFace, hold, Smooth((s - .80f) / .20f));
                    return Frame(p, true, 1f, 1f, 1f);
                }
                case MealStage.Stow:
                {
                    Pose target = trash ?? new Pose(hold.position - stations.EyeRotation * Vector3.up * .25f, hold.rotation);
                    if (s < .70f) return Frame(Blend(hold, target, Smooth(s / .70f)), true, 1f, 1f, 1f);
                    float release = Smooth((s - .70f) / .30f);
                    MealPouchFrame frame = Frame(target, true, 1f, 1f - release, 1f - release);
                    frame.Visible = false;
                    return frame;
                }
                default:
                    return new MealPouchFrame { Position = drawer.position, Rotation = drawer.rotation, Visible = false, Thickness = 1f };
            }
        }

        private MealPouchFrame SamplePrepare(float s, float dryThickness)
        {
            if (!station.HasValue)
            {
                // Ready to eat (or a station frame is missing): a short squeeze and a turn of the label towards the eye.
                float squeeze = 1f - .06f * Bell(s);
                return Frame(Blend(hold, check, Bell(s)), false, squeeze * (preparation == MealPreparation.Rehydrated ? dryThickness : 1f), 1f, 1f);
            }

            Pose at = station.Value;
            float thickness = preparation == MealPreparation.Rehydrated
                ? Mathf.Lerp(dryThickness, 1f, Smooth(Mathf.Clamp01((s - .30f) / .45f))) : 1f;
            if (s < .30f) return Frame(Blend(hold, at, Smooth(s / .30f)), false, thickness, 1f, 1f);
            if (s < .75f)
            {
                // Dwell at the station; rehydration gets a slow fill wobble, warming a still hold.
                float wobble = preparation == MealPreparation.Rehydrated ? Mathf.Sin((s - .30f) * 40f) * .8f : 0f;
                return Frame(new Pose(at.position, at.rotation * Quaternion.Euler(0f, 0f, wobble)), false, thickness, 1f, 1f);
            }
            float back = Smooth((s - .75f) / .25f);
            Pose p = Blend(at, hold, back);
            if (preparation == MealPreparation.Rehydrated)
            {
                // Knead the water through the dry food on the way back.
                float knead = Mathf.Sin(back * Mathf.PI * 3f) * 6f * (1f - back);
                p = new Pose(p.position, p.rotation * Quaternion.Euler(knead, 0f, 0f));
            }
            return Frame(p, false, thickness, 1f, 1f);
        }

        private static MealPouchFrame Frame(Pose pose, bool opened, float thickness, float hand, float clasp, float attach = 1f)
        {
            return new MealPouchFrame
            {
                Position = pose.position,
                Rotation = pose.rotation,
                Opened = opened,
                Visible = true,
                Thickness = thickness,
                HandWeight = hand,
                Clasp = clasp,
                Attach = Mathf.Clamp01(attach),
            };
        }

        /// <summary>Limits a point to a sphere around the shoulder: the hand never leaves the pouch, a far station is reached towards.</summary>
        public static Vector3 ClampReach(Vector3 point, Vector3 shoulder, float radius)
        {
            Vector3 offset = point - shoulder;
            float distance = offset.magnitude;
            return distance <= radius || distance < 1e-6f ? point : shoulder + offset * (radius / distance);
        }

        private static Pose FacingEye(Quaternion eye, Vector3 eyePosition, Vector3 key, float rollDegrees)
        {
            Vector3 position = eyePosition + eye * key;
            Vector3 toEye = (eyePosition - position).normalized;
            Quaternion facing = Quaternion.LookRotation(toEye, eye * Vector3.up);
            return new Pose(position, facing * Quaternion.Euler(0f, 0f, rollDegrees));
        }

        private static Pose ToMouth(Quaternion eye, Vector3 eyePosition, Vector3 key)
        {
            // The cut opening (pouch +y) points at the assumed mouth; the label (+z) faces up and away.
            Vector3 position = eyePosition + eye * key;
            Vector3 y = ((eyePosition + eye * MouthKey) - position).normalized;
            Vector3 x = Vector3.Cross(y, eye * Vector3.up);
            if (x.sqrMagnitude < 1e-6f) x = eye * Vector3.right;
            x.Normalize();
            Vector3 z = Vector3.Cross(x, y).normalized;
            return new Pose(position, Quaternion.LookRotation(z, y));
        }

        private static Pose Blend(Pose a, Pose b, float t) => new Pose(Vector3.LerpUnclamped(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Bell(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        private static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
    }
}

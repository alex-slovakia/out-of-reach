using System;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>One sampled frame of the meal: where the pouch is and how the hands hold it. Visual only.</summary>
    public struct MealPouchFrame
    {
        public Vector3 Position;
        public Quaternion Rotation;
        /// <summary>Show the opened variant (top torn off) instead of the sealed one.</summary>
        public bool Opened;
        /// <summary>The pouch is visible (false after it went into the wet-trash bag).</summary>
        public bool Visible;
        /// <summary>Thickness factor along the pouch's own z: a dry rehydratable pouch is flatter until water goes in, and
        /// an eaten pouch flattens again (it already includes <see cref="Empty"/> and <see cref="Squeeze"/>).</summary>
        public float Thickness;
        /// <summary>0..1 how much the right arm follows the pouch (reach in, carry, release).</summary>
        public float HandWeight;
        /// <summary>0..1 how far the measured clasp has closed.</summary>
        public float Clasp;
        /// <summary>0..1 how much the pouch rides in the actual hand (0 while it still lies in the drawer).</summary>
        public float Attach;

        /// <summary>0..1 how far the top strip is torn off: 0 = sealed, 1 = free of the pouch (R35).</summary>
        public float Tear;
        /// <summary>The torn strip is shown: from the tear until the left hand has taken it out of the view.</summary>
        public bool StripVisible;
        /// <summary>0..1 how much the left arm follows its pinch target.</summary>
        public float OffHandWeight;
        /// <summary>0..1 how far the left thumb and index finger have closed on the strip.</summary>
        public float OffHandPinch;
        /// <summary>The left pinch point in pouch space (x to the viewer's left, y up the pouch, z label side), metres.</summary>
        public Vector3 OffHandLocal;
        /// <summary>Where the left hand takes the torn strip (world), and 0..1 how far it has left the pouch for it.</summary>
        public Vector3 OffHandAway;
        public float OffHandAwayBlend;
        /// <summary>The left hand frame in the world: forward along the fingers, up out of the palm.</summary>
        public Quaternion OffHandRotation;
        /// <summary>0..1 squeeze of the pouch by the holding hand right now (a sip, a knead).</summary>
        public float Squeeze;
        /// <summary>0..1 how much of the food is gone.</summary>
        public float Empty;
        /// <summary>Head nod in degrees, positive looks down. The presenter drops it with reduced motion.</summary>
        public float NodDegrees;
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
        /// <summary>tan(half vertical field of view) and tan(half horizontal field of view) of the eye; 0 = defaults (68 deg, 16:9).</summary>
        public float ViewTanY, ViewTanX;
    }

    /// <summary>
    /// The part of the pouch that comes off when it is opened, in pouch space (x to the viewer's left when the label faces
    /// the eye, y up the pouch, z out of the label side): the band between the opened variant's cut and the sealed
    /// variant's top. Its two walls start as far apart as the pouch is thick at the cut and meet in the flat top seal.
    /// Defaults are the GALLEY-PROPS-01 meal pouch (sealed top 0.093 m, cut 0.052 m, half width 0.0705 m, 0.024 m thick
    /// at the cut, seal from 0.075 m).
    /// </summary>
    public readonly struct MealPouchShape
    {
        public MealPouchShape(float halfWidth, float stripBottom, float stripTop, float cutHalfThickness = .012f)
        {
            HalfWidth = halfWidth; StripBottom = stripBottom; StripTop = stripTop; CutHalfThickness = cutHalfThickness;
        }

        public float HalfWidth { get; }
        public float StripBottom { get; }
        public float StripTop { get; }
        /// <summary>Half the distance of the two pouch walls at the cut.</summary>
        public float CutHalfThickness { get; }
        public float Width => HalfWidth * 2f;
        public float StripMiddle => (StripBottom + StripTop) * .5f;
        public float StripHeight => StripTop - StripBottom;
        /// <summary>Where the walls meet in the flat top seal.</summary>
        public float SealBottom => StripBottom + StripHeight * .56f;
        public bool IsValid => HalfWidth > .01f && StripHeight > .004f && CutHalfThickness >= 0f
            && !float.IsNaN(HalfWidth + StripBottom + StripTop + CutHalfThickness);

        public static readonly MealPouchShape Default = new MealPouchShape(.0705f, .052f, .093f, .012f);
    }

    /// <summary>
    /// The torn strip as one ribbon, shared by the choreography (where the left hand pinches it) and the visible mesh.
    /// Arc fraction u runs from the viewer's right end (0) to the left end (1). The tear starts at the left end: with tear p
    /// the part u &gt; 1 - p has left the pouch and bends out of the pouch plane at the tear front. Pure functions.
    /// </summary>
    public static class MealStripShape
    {
        /// <summary>The fingers hold the strip this far from its left end, metres.</summary>
        public const float PinchInset = .016f;
        /// <summary>Bend length at the tear front, metres (no sharp crease).</summary>
        public const float BendLength = .014f;
        /// <summary>
        /// Where a torn part goes when nothing else decides: it swings out of the pouch plane towards the eye like a door
        /// hinged at the tear front, by about 35 degrees, so its face (not its edge) is what the eye sees.
        /// </summary>
        public static readonly Vector3 PeelDirection = new Vector3(.80f, .22f, .56f).normalized;
        /// <summary>The fingers hold the strip on its flat seal: this share of the strip's height above its lower edge.</summary>
        public const float PinchHeightShare = .78f;
        /// <summary>Half the thickness of the flat top seal, metres.</summary>
        public const float SealHalfThickness = .0010f;

        /// <summary>The tear front (the last point still on the pouch) in pouch space.</summary>
        public static Vector3 TearFront(MealPouchShape shape, float tear) =>
            new Vector3(shape.HalfWidth - Mathf.Clamp01(tear) * shape.Width, shape.StripMiddle, 0f);

        /// <summary>
        /// The centre line of the ribbon at arc fraction <paramref name="u"/> in pouch space, and its direction
        /// (<paramref name="tangent"/>, towards the left end). <paramref name="peel"/> is the unit direction of the torn part.
        /// </summary>
        public static Vector3 Centre(MealPouchShape shape, float tear, float u, Vector3 peel, out Vector3 tangent)
        {
            tear = Mathf.Clamp01(tear); u = Mathf.Clamp01(u);
            float attached = 1f - tear;
            if (u <= attached)
            {
                tangent = Vector3.right;
                return new Vector3(-shape.HalfWidth + u * shape.Width, shape.StripMiddle, 0f);
            }
            Vector3 front = TearFront(shape, tear);
            float q = (u - attached) * shape.Width;
            float bend = Mathf.Min(BendLength, tear * shape.Width);
            if (q < bend)
            {
                // Parabolic blend: the direction turns from along the pouch to the peel direction over the bend.
                float k = q / bend;
                tangent = Vector3.Lerp(Vector3.right, peel, k).normalized;
                return front + Vector3.right * (q * (1f - k * .5f)) + peel * (q * k * .5f);
            }
            tangent = peel;
            return front + Vector3.right * (bend * .5f) + peel * (q - bend * .5f);
        }

        /// <summary>
        /// The ribbon's own frame at arc fraction <paramref name="u"/> in pouch space: its centre, its direction, its
        /// height direction (up the strip) and its face normal (out of the label side while it is still on the pouch).
        /// </summary>
        public static void Frame(MealPouchShape shape, float tear, float u, Vector3 peel,
            out Vector3 centre, out Vector3 tangent, out Vector3 height, out Vector3 normal)
        {
            centre = Centre(shape, tear, u, peel, out tangent);
            height = Vector3.up - tangent * Vector3.Dot(Vector3.up, tangent);
            height = height.sqrMagnitude > 1e-6f ? height.normalized : Vector3.up;
            normal = Vector3.Cross(tangent, height).normalized;
        }

        /// <summary>Height above the strip's middle line at which the fingers hold it.</summary>
        public static float PinchHeight(MealPouchShape shape) => shape.StripBottom + shape.StripHeight * PinchHeightShare - shape.StripMiddle;

        /// <summary>Where the fingers hold the strip (pouch space) for a tear and a peel direction.</summary>
        public static Vector3 Pinch(MealPouchShape shape, float tear, Vector3 peel)
        {
            Frame(shape, tear, 1f - PinchInset / shape.Width, peel, out Vector3 centre, out _, out Vector3 height, out _);
            return centre + height * PinchHeight(shape);
        }

        /// <summary>
        /// The strip's cross-section in its own (height above the middle line, distance along the face normal) plane,
        /// from the lower edge of the label-side wall over the seal to the lower edge of the back wall: 5 points.
        /// </summary>
        public static void Profile(MealPouchShape shape, Vector2[] points)
        {
            float middle = shape.StripMiddle;
            points[0] = new Vector2(shape.StripBottom - middle, shape.CutHalfThickness);
            points[1] = new Vector2(shape.SealBottom - middle, SealHalfThickness);
            points[2] = new Vector2(shape.StripTop - middle, 0f);
            points[3] = new Vector2(shape.SealBottom - middle, -SealHalfThickness);
            points[4] = new Vector2(shape.StripBottom - middle, -shape.CutHalfThickness);
        }
    }

    /// <summary>
    /// Deterministic pouch path for one meal. Keys come from the reviewed GALLEY-USE-MOTION-01 clip (feature/galley-use-motion-v1
    /// @79447bf: lifted, held low in the view, near the face, opening towards the lips) re-targeted to the actual galley
    /// anchors and the eye at E. The poses are ideal targets: the presenter lets the actual arm reach towards them and the
    /// pouch rides in the hand (a far station is reached towards, never teleported to; see <see cref="ClampReach"/>).
    /// R35 adds the readable first-person beats inside the unchanged stage lengths: the left hand pinches the top strip and
    /// tears it off, the opened pouch is tipped to the mouth below the eye for three squeezed sips with a small head nod,
    /// and the emptied pouch is stowed. Pure function of (stations, preparation, stage, progress): no clock, no command,
    /// no scene state.
    /// </summary>
    public sealed class MealChoreography
    {
        // Eye-relative keys (x right, y up, z forward, metres). The assumed mouth is GALLEY-USE-MOTION-01's (0, -0.085, 0.03).
        // R35: the keys sit in the middle of the view. The lower right belongs to the chapter panel, the lower left to
        // the resource bars and the bottom edge to the subtitles: a pouch held there is hidden behind the HUD.
        public static readonly Vector3 HoldKey = new Vector3(.035f, -.075f, .340f);
        /// <summary>Where both hands open it: a little nearer and lower than the hold, so the left hand reaches its top corner.</summary>
        public static readonly Vector3 OpenKey = new Vector3(.008f, -.068f, .290f);
        /// <summary>Between sips: the opened pouch below the face, its mouth turned up and towards the lips.</summary>
        public static readonly Vector3 NearFaceKey = new Vector3(.010f, -.070f, .240f);
        /// <summary>At the lips: the opening at the mouth below the eye, the far end tipped up into the lower middle of the view.</summary>
        public static readonly Vector3 BiteKey = new Vector3(.004f, -.076f, .128f);
        public static readonly Vector3 MouthKey = new Vector3(0f, -.085f, .030f);
        /// <summary>Right shoulder relative to the eye when no body bone is available (the antenna worksite uses the same).</summary>
        public static readonly Vector3 ShoulderKey = new Vector3(.18f, -.28f, -.08f);
        /// <summary>Where the left hand takes the torn strip: down and left, out of the view.</summary>
        public static readonly Vector3 StripAwayKey = new Vector3(-.30f, -.34f, .20f);

        // The opening, as 0..1 of the opening part of MealStage.Open (after the optional repeated-menu label look).
        /// <summary>The left hand starts towards the pouch at this progress of MealStage.Prepare.</summary>
        public const float OffHandApproachFrom = .60f;
        /// <summary>The left thumb and index finger have closed on the top corner.</summary>
        public const float PinchAt = .26f;
        public const float TearStart = .36f;
        public const float TearEnd = .70f;
        /// <summary>Share of the "take the strip away" move that happens before the first bite.</summary>
        public const float AwayShareWhileOpening = .45f;
        /// <summary>Part of MealStage.Open that is the label look when the menu repeats.</summary>
        public const float LabelCheckShare = .40f;

        // Eating, as 0..1 of MealStage.Eat.
        /// <summary>The left hand is out of the view and the arm is given back by this progress.</summary>
        public const float OffHandGoneAt = .12f;
        public const float RaiseEnd = .14f;
        public const int SipCount = 3;
        public const float SipsEnd = .86f;
        /// <summary>Within one sip (0..1): the squeeze, and the swallow after it.</summary>
        public const float SipSqueezeFrom = .28f, SipSqueezeTo = .72f, SipSwallowAt = .84f;
        /// <summary>How far the pouch comes back from the lips between sips (0 = stays, 1 = back below the face).</summary>
        public const float SipRelease = .70f;
        /// <summary>Thickness left when the pouch is empty.</summary>
        public const float EmptyThickness = .58f;
        /// <summary>How far the holding fingers stay closed between squeezes (1 = the full measured clasp).</summary>
        public const float RestingClasp = .80f;
        /// <summary>Head nod in degrees: down to meet the pouch, back while squeezing, a small dip at the swallow.</summary>
        public const float NodDown = 2.0f, NodBack = 1.4f, NodSwallow = .9f;

        // The left hand in the eye frame: forward along the fingers, up out of the palm.
        private static readonly Quaternion PinchHand = Quaternion.LookRotation(new Vector3(.28f, .76f, .59f), new Vector3(.90f, -.08f, -.42f));
        private static readonly Quaternion AwayHand = Quaternion.LookRotation(new Vector3(.30f, .10f, .95f), new Vector3(.80f, .50f, -.33f));

        private readonly MealStations stations;
        private readonly MealPreparation preparation;
        private readonly bool labelCheck;
        private readonly MealPouchShape shape;
        private readonly Quaternion eyeRotation;
        private readonly Pose drawer, drawerOut, hold, open, nearFace, bite, check;
        private readonly Pose? station, stationShown, trash;
        private readonly Vector3 hoverLocal, cornerLocal, away;

        public MealChoreography(MealStations actual, MealPreparation kind, bool repeatedMenu, MealPouchShape? pouchShape = null)
        {
            stations = actual;
            preparation = kind;
            labelCheck = repeatedMenu;
            shape = pouchShape.HasValue && pouchShape.Value.IsValid ? pouchShape.Value : MealPouchShape.Default;
            Quaternion eye = Normalize(actual.EyeRotation);
            eyeRotation = eye;
            Vector3 up = actual.Up.sqrMagnitude > 1e-6f ? actual.Up.normalized : Vector3.up;
            Vector3 drawerForward = Normalize(actual.DrawerRotation) * Vector3.forward;

            // In the drawer: standing upright, label out towards the aisle, leaning back 12 degrees like the stowed pouches.
            Quaternion inDrawer = Quaternion.LookRotation(drawerForward, up) * Quaternion.Euler(-12f, 0f, 0f);
            drawer = new Pose(actual.DrawerPosition - drawerForward * .03f - up * .03f, inDrawer);
            drawerOut = new Pose(actual.DrawerPosition + drawerForward * .09f - up * .02f, inDrawer);

            hold = FacingEye(eye, actual.EyePosition, HoldKey, -9f);
            check = FacingEye(eye, actual.EyePosition, HoldKey + new Vector3(-.025f, .020f, -.035f), 0f);
            open = FacingEye(eye, actual.EyePosition, OpenKey, -10f);
            nearFace = ToMouth(eye, actual.EyePosition, NearFaceKey, .55f);
            // Tipped like a drink: the opening points at the mouth and down, so the far end rises by about 30 degrees and
            // the eye sees the foil face of the pouch, not its edge.
            bite = ToMouth(eye, actual.EyePosition, BiteKey, -.45f);
            float tanY = actual.ViewTanY > 0f ? actual.ViewTanY : Mathf.Tan(34f * Mathf.Deg2Rad);
            float tanX = actual.ViewTanX > 0f ? actual.ViewTanX : tanY * 16f / 9f;

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

            if (station.HasValue)
            {
                // The movement lock freezes the view where E was pressed, and a galley station is usually below it and
                // beyond arm's reach: present the pouch TOWARDS the real station, on its line of sight when it is in
                // view, otherwise at the edge of the view in its direction, so the whole preparation stays visible.
                stationShown = new Pose(TowardsInView(eye, actual.EyePosition, station.Value.position, tanX, tanY), station.Value.rotation);
            }
            if (actual.Trash.HasValue)
                trash = new Pose(actual.Trash.Value, Quaternion.LookRotation(up, into));

            // The left hand: it waits beside the top corner, pinches the strip there and takes it out of the view.
            cornerLocal = MealStripShape.Pinch(shape, 0f, MealStripShape.PeelDirection) + new Vector3(0f, 0f, .002f);
            hoverLocal = cornerLocal + new Vector3(.045f, -.012f, .040f);
            away = actual.EyePosition + eye * StripAwayKey;
        }

        public MealPreparation Preparation => preparation;
        public MealPouchShape Shape => shape;
        public Pose Hold => hold;
        /// <summary>The real station frame (needle or warmer), or null for ready to eat.</summary>
        public Pose? Station => station;
        /// <summary>Where the pouch is presented towards the station inside the frozen view.</summary>
        public Pose? StationShown => stationShown;

        /// <summary>0..1 of the opening part of MealStage.Open for a progress of that stage (below 0 during the label look).</summary>
        public static float OpeningProgress(float openStageProgress, bool repeatedMenu)
        {
            float start = repeatedMenu ? LabelCheckShare : 0f;
            return (Mathf.Clamp01(openStageProgress) - start) / (1f - start);
        }

        /// <summary>The MealStage.Open progress at which the opening part reaches <paramref name="k"/>.</summary>
        public static float OpenStageProgress(float k, bool repeatedMenu)
        {
            float start = repeatedMenu ? LabelCheckShare : 0f;
            return start + Mathf.Clamp01(k) * (1f - start);
        }

        /// <summary>0..1 of the tear for an opening progress: it starts slowly and rips through.</summary>
        public static float TearProgress(float k)
        {
            float t = Mathf.Clamp01((k - TearStart) / (TearEnd - TearStart));
            return t * t * (3f - 2f * t) * (.35f + .65f * t);
        }

        /// <summary>Length of one sip in MealStage.Eat progress.</summary>
        public static float SipLength => (SipsEnd - RaiseEnd) / SipCount;

        /// <summary>The MealStage.Eat progress at which sip <paramref name="index"/> (0-based) reaches <paramref name="within"/> (0..1).</summary>
        public static float SipProgress(int index, float within) => RaiseEnd + (index + Mathf.Clamp01(within)) * SipLength;

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
                {
                    MealPouchFrame frame = SamplePrepare(s, thickness);
                    // The left hand comes up beside the top corner while the pouch returns to the hold.
                    float approach = Smooth((s - OffHandApproachFrom) / (1f - OffHandApproachFrom));
                    return OffHand(frame, hoverLocal, approach * .9f, 0f, 0f);
                }
                case MealStage.Open:
                    return SampleOpen(s);
                case MealStage.Eat:
                    return SampleEat(s);
                case MealStage.Stow:
                {
                    Pose target = trash ?? new Pose(hold.position - stations.EyeRotation * Vector3.up * .25f, hold.rotation);
                    if (s < .70f) return Eaten(Frame(Blend(hold, target, Smooth(s / .70f)), true, EmptyThickness, 1f, 1f));
                    float release = Smooth((s - .70f) / .30f);
                    MealPouchFrame frame = Eaten(Frame(target, true, EmptyThickness, 1f - release, 1f - release));
                    frame.Visible = false;
                    return frame;
                }
                default:
                    return new MealPouchFrame { Position = drawer.position, Rotation = drawer.rotation, Visible = false, Thickness = 1f, OffHandRotation = eyeRotation * PinchHand };
            }
        }

        private MealPouchFrame SampleOpen(float s)
        {
            if (labelCheck && s < LabelCheckShare)
            {
                // Restrained repeat reaction: a short look at the familiar label before opening. No taste claim.
                return OffHand(Frame(Blend(hold, check, Bell(s / LabelCheckShare)), false, 1f, 1f, 1f), hoverLocal, .9f, 0f, 0f);
            }

            float k = OpeningProgress(s, labelCheck);
            float tear = TearProgress(k);
            // Sealed -> Opened in place when the tear starts: the strip still covers the cut where it has not torn yet.
            bool opened = k >= TearStart;
            Pose p = Blend(hold, open, Smooth(Mathf.Min(1f, k / PinchAt)));
            if (opened)
            {
                // The holding hand pulls against the tear, and jerks as the strip comes free.
                float rip = Mathf.Sin(tear * Mathf.PI);
                float snap = Mathf.Sin(Mathf.Clamp01((k - TearEnd) / .14f) * Mathf.PI);
                p = new Pose(p.position + eyeRotation * new Vector3(.007f * rip, -.004f * snap, 0f),
                    p.rotation * Quaternion.Euler(0f, 0f, -5f * rip + 6f * snap));
            }
            MealPouchFrame frame = Frame(p, opened, 1f, 1f, 1f);
            frame.Tear = tear;
            frame.StripVisible = opened;
            if (k < PinchAt)
            {
                // Reach the corner, then close thumb and index finger on it.
                float reach = Smooth(k / PinchAt);
                return OffHand(frame, Vector3.Lerp(hoverLocal, cornerLocal, reach), Mathf.Lerp(.9f, 1f, reach),
                    Smooth((k - PinchAt * .5f) / (PinchAt * .5f)), 0f);
            }
            if (k < TearEnd)
                return OffHand(frame, MealStripShape.Pinch(shape, tear, MealStripShape.PeelDirection), 1f, 1f, 0f);
            return OffHand(frame, MealStripShape.Pinch(shape, 1f, MealStripShape.PeelDirection), 1f, 1f,
                Smooth((k - TearEnd) / (1f - TearEnd)) * AwayShareWhileOpening);
        }

        private MealPouchFrame SampleEat(float s)
        {
            MealPouchFrame frame;
            if (s < RaiseEnd)
            {
                // The first bite is taken (the command ran at s = 0): the opened pouch comes up below the face.
                float raised = Smooth(s / RaiseEnd);
                frame = Frame(Blend(open, nearFace, raised), true, 1f, 1f, Mathf.Lerp(1f, RestingClasp, raised));
            }
            else if (s < SipsEnd)
            {
                float sip = (s - RaiseEnd) / SipLength;
                int index = Mathf.Min(SipCount - 1, (int)sip);
                float u = Mathf.Clamp01(sip - index);
                // To the lips, squeeze, and only part of the way back before the next sip.
                float from = index == 0 ? 0f : 1f - SipRelease;
                float lips = u < SipSqueezeFrom ? Mathf.Lerp(from, 1f, Smooth(u / SipSqueezeFrom))
                    : u < SipSqueezeTo ? 1f
                    : 1f - SipRelease * Smooth((u - SipSqueezeTo) / (1f - SipSqueezeTo));
                float within = Mathf.Clamp01((u - SipSqueezeFrom) / (SipSqueezeTo - SipSqueezeFrom));
                float squeeze = Mathf.Sin(within * Mathf.PI);
                float empty = (index + Smooth(within)) / SipCount;
                Pose p = Blend(nearFace, bite, lips);
                // The far end tips up a little more with each squeeze.
                p = new Pose(p.position, p.rotation * Quaternion.Euler(8f * squeeze, 0f, 0f));
                // The fingers press the pouch on each squeeze and ease between them.
                frame = Frame(p, true, Mathf.Lerp(1f, EmptyThickness, empty) * (1f - .14f * squeeze), 1f, Mathf.Lerp(RestingClasp, 1f, squeeze));
                frame.Squeeze = squeeze;
                frame.Empty = empty;
                float nod = u < SipSqueezeFrom ? Mathf.Lerp(0f, NodDown, Smooth(u / SipSqueezeFrom))
                    : u < SipSqueezeTo ? Mathf.Lerp(NodDown, -NodBack, Smooth(within))
                    : Mathf.Lerp(-NodBack, 0f, Smooth((u - SipSqueezeTo) / (1f - SipSqueezeTo)));
                // The swallow: a small dip of the head after the squeeze.
                nod += NodSwallow * Mathf.Sin(Mathf.Clamp01((u - (SipSwallowAt - .07f)) / .16f) * Mathf.PI);
                frame.NodDegrees = nod;
            }
            else
            {
                Pose between = Blend(nearFace, bite, 1f - SipRelease);
                float lowered = Smooth((s - SipsEnd) / (1f - SipsEnd));
                frame = Eaten(Frame(Blend(between, hold, lowered), true, EmptyThickness, 1f, Mathf.Lerp(RestingClasp, 1f, lowered)));
            }

            // The left hand takes the torn strip out of the view while the pouch comes up, then the arm is given back.
            float awayBlend = Mathf.Lerp(AwayShareWhileOpening, 1f, Smooth(s / OffHandGoneAt));
            frame.Tear = 1f;
            frame.StripVisible = awayBlend < .9f;
            return OffHand(frame, MealStripShape.Pinch(shape, 1f, MealStripShape.PeelDirection),
                1f - Smooth((awayBlend - .55f) / .45f), 1f, awayBlend);
        }

        private MealPouchFrame SamplePrepare(float s, float dryThickness)
        {
            if (!station.HasValue)
            {
                // Ready to eat (or a station frame is missing): a short squeeze and a turn of the label towards the eye.
                float squeeze = 1f - .06f * Bell(s);
                MealPouchFrame ready = Frame(Blend(hold, check, Bell(s)), false, squeeze * (preparation == MealPreparation.Rehydrated ? dryThickness : 1f), 1f, 1f);
                ready.Squeeze = Bell(s);
                return ready;
            }

            Pose at = stationShown ?? station.Value;
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
            MealPouchFrame frame;
            if (preparation == MealPreparation.Rehydrated)
            {
                // Knead the water through the dry food on the way back.
                float knead = Mathf.Sin(back * Mathf.PI * 3f) * 6f * (1f - back);
                p = new Pose(p.position, p.rotation * Quaternion.Euler(knead, 0f, 0f));
                frame = Frame(p, false, thickness, 1f, 1f);
                frame.Squeeze = Mathf.Abs(knead) / 6f;
                return frame;
            }
            return Frame(p, false, thickness, 1f, 1f);
        }

        private MealPouchFrame Frame(Pose pose, bool opened, float thickness, float hand, float clasp, float attach = 1f)
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
                Tear = opened ? 1f : 0f,
                OffHandLocal = hoverLocal,
                OffHandAway = away,
                OffHandRotation = eyeRotation * PinchHand,
            };
        }

        private static MealPouchFrame Eaten(MealPouchFrame frame) { frame.Empty = 1f; return frame; }

        private MealPouchFrame OffHand(MealPouchFrame frame, Vector3 local, float weight, float pinch, float awayBlend)
        {
            frame.OffHandLocal = local;
            frame.OffHandWeight = Mathf.Clamp01(weight);
            frame.OffHandPinch = Mathf.Clamp01(pinch);
            frame.OffHandAwayBlend = Mathf.Clamp01(awayBlend);
            frame.OffHandAway = away;
            // The hand turns palm-in as it takes the strip away.
            frame.OffHandRotation = eyeRotation * Quaternion.Slerp(PinchHand, AwayHand, Smooth(awayBlend));
            return frame;
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

        private static Pose ToMouth(Quaternion eye, Vector3 eyePosition, Vector3 key, float upShare)
        {
            // The torn opening (pouch +y) tilts towards the assumed mouth, and up by upShare so the food shows (or down
            // by a negative share, so the far end rises); the clasped edge (prop -x) stays on the right, towards the
            // right hand, never across the view.
            Vector3 position = eyePosition + eye * key;
            Vector3 toMouth = ((eyePosition + eye * MouthKey) - position).normalized;
            Vector3 y = (toMouth + (eye * Vector3.up) * upShare).normalized;
            Vector3 x = Vector3.Cross(eye * Vector3.up, y);
            if (x.sqrMagnitude < 1e-6f) x = eye * Vector3.left;
            x.Normalize();
            Vector3 z = Vector3.Cross(x, y).normalized;
            return new Pose(position, Quaternion.LookRotation(z, y));
        }

        /// <summary>A point towards <paramref name="target"/> inside the view: on its line of sight at 0.36 m, kept within the frustum.</summary>
        public static Vector3 TowardsInView(Quaternion eye, Vector3 eyePosition, Vector3 target, float tanX, float tanY)
        {
            const float depth = .36f;
            Vector3 local = Quaternion.Inverse(eye) * (target - eyePosition);
            Vector2 slope = local.z > 1e-3f ? new Vector2(local.x / local.z, local.y / local.z) : new Vector2(local.x, local.y) * 1e3f;
            float limitX = tanX * .78f, limitY = tanY * .72f;
            float scale = Mathf.Max(1f, Mathf.Max(Mathf.Abs(slope.x) / limitX, Mathf.Abs(slope.y) / limitY));
            slope /= scale;
            return eyePosition + eye * new Vector3(slope.x * depth, slope.y * depth, depth);
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

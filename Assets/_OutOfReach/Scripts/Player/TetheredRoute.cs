using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Player
{
    /// <summary>
    /// The short, constrained EVA route: waypoints for the player's feet from the outer hatch (EvaStart) to the work
    /// site near AntennaRepair. While on it the player has no gravity and no free flight: the feet stay exactly on
    /// the waypoint line and move along it, never past either end. Vertical stretches are traversed like any other
    /// part of the line. Limits are explicit GAME ASSUMPTIONS (tether length = line length, traverse speed), not a
    /// tether physics model.
    /// <para>
    /// EVA-TETHER-01: the line is travelled hand over hand along the tether rope. Grip stations ("rungs") sit every
    /// <see cref="HandStrokeMetres"/> along the line; the body only moves while a glove holds a rung and pulls, with
    /// a surge per pull, a short grip ramp and a braking glove at rest (tuning below, game assumptions).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetheredRoute : MonoBehaviour
    {
        [Tooltip("Feet positions along the tether, in order. The first is the EVA start by the outer hatch.")]
        [SerializeField] private Transform[] waypoints = new Transform[0];

        [Tooltip("Corridor half-width used by Constrain, metres. The player controller keeps the feet exactly on the line.")]
        [SerializeField] private float lateralFreedomMetres = 0.5f;

        [Tooltip("Average traverse speed along the route, metres per second (time-averaged over whole arm pulls).")]
        [SerializeField] private float speedMetresPerSecond = 1f;

        [Header("Hand-over-hand climb (EVA-TETHER-01, game tuning)")]
        [Tooltip("Body travel per arm pull, metres: the spacing of the grip stations on the rope.")]
        [SerializeField, Range(0.3f, 0.7f)] private float handStrokeMetres = 0.5f;

        [Tooltip("Where a pull ends: the gripping glove's distance ahead of the body, metres. Full reach is this plus the stroke.")]
        [SerializeField, Range(0.05f, 0.3f)] private float nearReachMetres = 0.14f;

        [Tooltip("Speed surge through one pull (0 = constant glide): slowest as the gloves change over, fastest mid-pull.")]
        [SerializeField, Range(0f, 0.5f)] private float pullSurge = 0.3f;

        [Tooltip("Seconds for a fresh grip to bring the body up to speed.")]
        [SerializeField, Range(0.05f, 0.5f)] private float gripRampSeconds = 0.18f;

        [Tooltip("Seconds for the holding glove to brake the body to rest after W/S is released or reversed.")]
        [SerializeField, Range(0.05f, 0.6f)] private float brakeSeconds = 0.24f;

        private readonly List<Vector3> points = new List<Vector3>();

        public float LateralFreedomMetres => lateralFreedomMetres;

        public float SpeedMetresPerSecond => speedMetresPerSecond;

        /// <summary>Body travel per arm pull (grip station spacing on the rope), metres.</summary>
        public float HandStrokeMetres => Mathf.Clamp(handStrokeMetres, 0.3f, 0.7f);

        /// <summary>The gripping glove's distance ahead of the body when its pull ends, metres.</summary>
        public float NearReachMetres => Mathf.Clamp(nearReachMetres, 0.05f, 0.3f);

        /// <summary>Speed surge through one pull, 0 to 0.5.</summary>
        public float PullSurge => Mathf.Clamp(pullSurge, 0f, 0.5f);

        public float GripRampSeconds => Mathf.Clamp(gripRampSeconds, 0.05f, 0.5f);

        public float BrakeSeconds => Mathf.Clamp(brakeSeconds, 0.05f, 0.6f);

        /// <summary>The rung (route distance) the leading glove holds for a body at a distance facing a sense.</summary>
        public float HoldingRung(float distanceMetres, int facingSense)
        {
            return RouteMath.HoldingRung(distanceMetres, facingSense, HandStrokeMetres, NearReachMetres);
        }

        /// <summary>Phase of the current arm pull in [0, 1) for a body at a distance facing a sense.</summary>
        public float StrokePhase(float distanceMetres, int facingSense)
        {
            return RouteMath.HandStrokePhase(distanceMetres, facingSense, HandStrokeMetres, NearReachMetres);
        }

        public int WaypointCount => waypoints != null ? waypoints.Length : 0;

        /// <summary>The waypoint transforms in order (read-only view), e.g. for the tether rope that follows the line.</summary>
        public IReadOnlyList<Transform> Waypoints => waypoints ?? new Transform[0];

        /// <summary>True when at least two waypoints are assigned.</summary>
        public bool IsValid => RefreshPoints().Count >= 2;

        public Vector3 StartPosition => RefreshPoints().Count > 0 ? points[0] : transform.position;

        public Vector3 EndPosition => RefreshPoints().Count > 0 ? points[points.Count - 1] : transform.position;

        /// <summary>Tether length: the length of the waypoint line, metres.</summary>
        public float LengthMetres => RouteMath.Length(RefreshPoints());

        /// <summary>Half-length of the stretch of route used to judge its heading, metres.</summary>
        public const float HeadingWindowMetres = 1.5f;

        /// <summary>The feet position at a distance from the start, along the line (clamped to both ends).</summary>
        public Vector3 PointAt(float distanceMetres)
        {
            List<Vector3> line = RefreshPoints();
            return line.Count > 0 ? RouteMath.PointAtDistance(line, distanceMetres) : transform.position;
        }

        /// <summary>Horizontal unit direction towards the route's end around a distance; zero for an all-vertical route.</summary>
        public Vector3 HeadingAt(float distanceMetres)
        {
            return RouteMath.HorizontalHeading(RefreshPoints(), distanceMetres, HeadingWindowMetres);
        }

        /// <summary>Explicit wiring for code-built scenes; authored scenes use the serialized fields.</summary>
        public void Configure(Transform[] routeWaypoints, float lateralFreedom, float speed)
        {
            waypoints = routeWaypoints ?? new Transform[0];
            lateralFreedomMetres = Mathf.Max(0f, lateralFreedom);
            speedMetresPerSecond = Mathf.Max(0f, speed);
        }

        /// <summary>Hand-over-hand tuning for code-built scenes and tests (values are clamped to the inspector ranges).</summary>
        public void ConfigureHandOverHand(float strokeMetres, float nearMetres, float surge, float rampSeconds, float brakingSeconds)
        {
            handStrokeMetres = Mathf.Clamp(strokeMetres, 0.3f, 0.7f);
            nearReachMetres = Mathf.Clamp(nearMetres, 0.05f, 0.3f);
            pullSurge = Mathf.Clamp(surge, 0f, 0.5f);
            gripRampSeconds = Mathf.Clamp(rampSeconds, 0.05f, 0.5f);
            brakeSeconds = Mathf.Clamp(brakingSeconds, 0.05f, 0.6f);
        }

        /// <summary>The allowed position closest to a desired feet position.</summary>
        public Vector3 Constrain(Vector3 desiredFeetPosition)
        {
            return RouteMath.Constrain(RefreshPoints(), desiredFeetPosition, lateralFreedomMetres);
        }

        /// <summary>Distance of a position from the route start, measured along the line, metres.</summary>
        public float DistanceAlong(Vector3 feetPosition)
        {
            RouteMath.ClosestPoint(RefreshPoints(), feetPosition, out float distance);
            return distance;
        }

        private List<Vector3> RefreshPoints()
        {
            points.Clear();
            if (waypoints != null)
            {
                foreach (Transform waypoint in waypoints)
                {
                    if (waypoint != null)
                    {
                        points.Add(waypoint.position);
                    }
                }
            }

            return points;
        }
    }
}

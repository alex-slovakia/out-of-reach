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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetheredRoute : MonoBehaviour
    {
        [Tooltip("Feet positions along the tether, in order. The first is the EVA start by the outer hatch.")]
        [SerializeField] private Transform[] waypoints = new Transform[0];

        [Tooltip("Corridor half-width used by Constrain, metres. The player controller keeps the feet exactly on the line.")]
        [SerializeField] private float lateralFreedomMetres = 0.5f;

        [Tooltip("Traverse speed along the route, metres per second.")]
        [SerializeField] private float speedMetresPerSecond = 1f;

        private readonly List<Vector3> points = new List<Vector3>();

        public float LateralFreedomMetres => lateralFreedomMetres;

        public float SpeedMetresPerSecond => speedMetresPerSecond;

        public int WaypointCount => waypoints != null ? waypoints.Length : 0;

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

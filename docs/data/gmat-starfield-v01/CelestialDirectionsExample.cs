// Standalone presentation math EXAMPLE, not production integration or a compiled Unity claim.
// Original implementation; astronomical source values are injected by the integrator.
using System;
using UnityEngine;

namespace OutOfReach.VerificationExamples
{
    public static class CelestialDirectionsExample
    {
        // Earth-relative ICRF km -> Unity celestial basis (+Z RA0, +X RA90, +Y north).
        // Subtract in DOUBLE before converting to float. Never place bodies at AU-scale float coordinates.
        public static Vector3 DirectionToBody(
            double bodyXKm, double bodyYKm, double bodyZKm,
            double observerXKm, double observerYKm, double observerZKm)
        {
            double x = bodyXKm - observerXKm;
            double y = bodyYKm - observerYKm;
            double z = bodyZKm - observerZKm;
            double distance = Math.Sqrt(x*x + y*y + z*z);
            if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0)
                throw new ArgumentException("Finite non-zero body displacement required.");
            return new Vector3((float)(y/distance), (float)(z/distance), (float)(x/distance));
        }

        // Quaternion must be normalized: it maps ship-local directions INTO the celestial basis.
        public static Vector3 CelestialToShipLocal(Vector3 celestialDirection, Quaternion shipToCelestial)
            => Quaternion.Inverse(shipToCelestial) * celestialDirection;

        // Use only if the physical ship transform has a separate Unity-world orientation.
        public static Vector3 CelestialToWorld(
            Vector3 celestialDirection, Quaternion shipToCelestial, Quaternion shipToWorld)
            => shipToWorld * CelestialToShipLocal(celestialDirection, shipToCelestial);

        public static double AngularDiameterRadians(double meanRadiusKm, double centreDistanceKm)
        {
            if (double.IsNaN(meanRadiusKm) || double.IsInfinity(meanRadiusKm) || meanRadiusKm <= 0 ||
                double.IsNaN(centreDistanceKm) || double.IsInfinity(centreDistanceKm) || centreDistanceKm <= meanRadiusKm)
                throw new ArgumentException("Observer must be outside a finite spherical body.");
            return 2.0 * Math.Asin(meanRadiusKm / centreDistanceKm);
        }

        public static double RelativeFlux(double sourceVisualMagnitude)
        {
            if (double.IsNaN(sourceVisualMagnitude) || double.IsInfinity(sourceVisualMagnitude))
                throw new ArgumentException("Finite magnitude required.");
            return Math.Pow(10.0, -0.4 * sourceVisualMagnitude);
        }
    }
}

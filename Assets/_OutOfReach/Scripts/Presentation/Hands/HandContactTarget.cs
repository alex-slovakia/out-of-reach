using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    public enum ContactHand { Left, Right }
    public enum HandContactKind { Push, Grip }
    public enum HandContactStage { None, Reaching, Holding, PushContact, Returning }

    /// <summary>Actual surface hit in its own transform space; never a guessed point along a view ray.</summary>
    public readonly struct HandContactTarget
    {
        public readonly Transform Surface;
        public readonly Vector3 LocalPoint, LocalNormal, LocalTangent;
        public readonly float GripRadiusMetres;

        /// <param name="localTangent">For Grip: the verified rail axis. For Push: desired fingers-up direction on the surface.</param>
        /// <param name="gripRadiusMetres">Actual rail radius, 12–17 mm for this authored glove; zero for Push.</param>
        public HandContactTarget(Transform surface, Vector3 localPoint, Vector3 localNormal,
            Vector3 localTangent, float gripRadiusMetres = 0f)
        {
            Surface = surface; LocalPoint = localPoint; LocalNormal = localNormal;
            LocalTangent = localTangent; GripRadiusMetres = gripRadiusMetres;
        }
    }

    /// <summary>Fresh world-space contact submitted to root validation/action, including the selected hand.</summary>
    public readonly struct HandContactSample
    {
        public readonly ContactHand Hand;
        public readonly HandContactKind Kind;
        public readonly Transform Surface;
        public readonly Vector3 Point, Normal, Tangent;
        public readonly Vector3 CurrentWristPoint, WristPoint;
        public readonly float GripRadiusMetres;
        internal HandContactSample(ContactHand hand, HandContactKind kind, HandContactTarget target,
            Vector3 point, Vector3 normal, Vector3 tangent, Vector3 currentWristPoint, Vector3 wristPoint)
        {
            Hand = hand; Kind = kind; Surface = target.Surface; Point = point;
            Normal = normal; Tangent = tangent; GripRadiusMetres = target.GripRadiusMetres;
            CurrentWristPoint = currentWristPoint; WristPoint = wristPoint;
        }
    }
}

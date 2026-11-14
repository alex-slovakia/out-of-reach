using UnityEngine;

namespace OutOfReach.Player
{
    /// <summary>
    /// Device-independent player intent for one frame. The default implementation reads the Input System;
    /// tests inject a scripted source so movement and interaction are deterministic.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Walk intent: x = strafe right, y = forward, each from -1 to 1.</summary>
        Vector2 Move { get; }

        /// <summary>View rotation requested this frame, in degrees: x = turn right, y = look up.</summary>
        Vector2 LookDegrees { get; }

        bool InteractPressedThisFrame { get; }

        bool PausePressedThisFrame { get; }
    }
}

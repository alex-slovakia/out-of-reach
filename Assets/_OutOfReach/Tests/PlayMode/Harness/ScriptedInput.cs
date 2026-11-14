using OutOfReach.Player;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>Deterministic player input for tests. Move is held; look and button presses are consumed once.</summary>
    public sealed class ScriptedInput : IPlayerInputSource
    {
        private Vector2 queuedLook;
        private bool interactQueued;
        private bool pauseQueued;

        public Vector2 Move { get; set; }

        public Vector2 LookDegrees
        {
            get
            {
                Vector2 look = queuedLook;
                queuedLook = Vector2.zero;
                return look;
            }
        }

        public bool InteractPressedThisFrame
        {
            get
            {
                bool pressed = interactQueued;
                interactQueued = false;
                return pressed;
            }
        }

        public bool PausePressedThisFrame
        {
            get
            {
                bool pressed = pauseQueued;
                pauseQueued = false;
                return pressed;
            }
        }

        public void QueueLook(float yawDegrees, float pitchDegrees)
        {
            queuedLook += new Vector2(yawDegrees, pitchDegrees);
        }

        public void PressInteract()
        {
            interactQueued = true;
        }

        public void PressPause()
        {
            pauseQueued = true;
        }
    }
}

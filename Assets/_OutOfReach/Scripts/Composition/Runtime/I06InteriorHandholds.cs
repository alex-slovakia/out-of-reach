using System;
using System.Collections.Generic;
using OutOfReach.Player;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Explicit scene rail registration only. No input, movement, contact animation or suit authority.</summary>
    [DisallowMultipleComponent]
    public sealed class I06InteriorHandholds : MonoBehaviour
    {
        [SerializeField] private CapsuleCollider[] rails = Array.Empty<CapsuleCollider>();
        private readonly List<Collider> owned = new List<Collider>();
        private FirstPersonController player;
        public IReadOnlyList<CapsuleCollider> Rails => rails;
        public int OwnedRegistrationCount => owned.Count;
        public FirstPersonController BoundPlayer => player;

        public void ConfigureRails(CapsuleCollider[] authoredRails)
        {
            Unbind();
            if (authoredRails == null) throw new ArgumentNullException(nameof(authoredRails));
            var unique = new HashSet<Collider>();
            foreach (CapsuleCollider rail in authoredRails)
                if (!rail || !rail.transform.IsChildOf(transform) || rail.isTrigger || !unique.Add(rail)
                    || Mathf.Abs(rail.radius - .015f) > .00001f || rail.height < .15f)
                    throw new ArgumentException("Supply distinct, solid 30mm shaft colliders owned by this authored hierarchy.");
            rails = (CapsuleCollider[])authoredRails.Clone();
        }

        /// <summary>Root calls on bind/re-enable. Already registered rails are borrowed, never claimed or removed.</summary>
        public bool Bind(FirstPersonController actualPlayer)
        {
            Unbind();
            if (!isActiveAndEnabled || !actualPlayer || actualPlayer.gameObject.scene != gameObject.scene) return false;
            foreach (CapsuleCollider rail in rails)
                if (!rail || !rail.enabled || !rail.gameObject.activeInHierarchy || rail.isTrigger
                    || !rail.transform.IsChildOf(transform) || (rail.transform.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                    return false;
            player = actualPlayer;
            foreach (CapsuleCollider rail in rails)
            {
                if (player.IsHandhold(rail)) continue;
                player.RegisterHandhold(rail); owned.Add(rail);
            }
            return true;
        }

        /// <summary>Clear the contact bridge first; unregistering one of our held rails legitimately releases its grip.</summary>
        public void Unbind()
        {
            FirstPersonController previous = player; player = null;
            if (previous)
                foreach (Collider rail in owned) if (rail) previous.UnregisterHandhold(rail);
            owned.Clear();
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}

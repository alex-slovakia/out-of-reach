using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    /// <summary>One root-owned field selects the next explicitly rebuilt body. Does not hot-swap an active lease.</summary>
    public sealed class AvatarPresentationSelection : ScriptableObject
    {
        [SerializeField] private AvatarPresentationProfile profile;
        public AvatarPresentationProfile Profile => profile;
        internal void SetInitialProfile(AvatarPresentationProfile value) { profile = value; }
    }
}

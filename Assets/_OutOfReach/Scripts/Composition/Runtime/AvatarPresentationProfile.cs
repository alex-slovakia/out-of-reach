using System;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Explicit presentation mapping, not a retargeter or a gameplay capability override.</summary>
    [CreateAssetMenu(menuName = "Out of Reach/Avatar presentation profile")]
    public sealed class AvatarPresentationProfile : ScriptableObject
    {
        [Serializable]
        public sealed class SkinRole
        {
            [Tooltip("Exact path relative to the imported model root. Duplicate leaf names are supported.")]
            public string path;
            public Material[] materials = Array.Empty<Material>();
            [Tooltip("WORK arm skin submesh indices measured by palm contact proof. Material names are not identity.")]
            public int[] palmSubmeshes = Array.Empty<int>();
        }

        [Serializable]
        public sealed class OutfitDefinition
        {
            public GameObject model;
            public SkinRole torsoLegs = new SkinRole();
            public SkinRole leftArm = new SkinRole();
            public SkinRole rightArm = new SkinRole();
            public EmbodiedBodyPresenter.SemanticBinding[] bones = Array.Empty<EmbodiedBodyPresenter.SemanticBinding>();
            public AnimationClip standing, floating, seated;
            [Tooltip("Explicitly reviewed Generic translation channels. Paths must exist in this model.")]
            public string[] translationPaths = Array.Empty<string>();
            [Tooltip("Only constant, bind-scale curves are supported; these paths explicitly acknowledge them.")]
            public string[] scalePaths = Array.Empty<string>();
        }

        [SerializeField] private OutfitDefinition work = new OutfitDefinition();
        [SerializeField] private OutfitDefinition eva = new OutfitDefinition();
        [SerializeField] private Vector3 standingOffset = new Vector3(0f, 0f, -.197f);
        [SerializeField] private Vector3 floatingOffset = Vector3.zero;
        [SerializeField] private Vector3 seatedOffset = new Vector3(0f, 0f, -.330f);

        public OutfitDefinition Work => work;
        public OutfitDefinition Eva => eva;
        public Vector3 StandingOffset => standingOffset;
        public Vector3 FloatingOffset => floatingOffset;
        public Vector3 SeatedOffset => seatedOffset;
    }
}

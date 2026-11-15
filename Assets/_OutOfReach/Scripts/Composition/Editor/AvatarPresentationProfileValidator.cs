using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Presentation.Motion;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Structural validation only. Native palm, grip, seat and interpenetration acceptance remains required.</summary>
    public static class AvatarPresentationProfileValidator
    {
        public static void Validate(AvatarPresentationProfile profile)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Validate avatar profiles outside Play mode.");
            AvatarPresentationResolver.ValidateOffsets(profile);
            ValidateOutfit(profile.Work, true); ValidateOutfit(profile.Eva, false);
        }

        private static void ValidateOutfit(AvatarPresentationProfile.OutfitDefinition definition, bool bareHands)
        {
            if (definition == null || !definition.model || !EditorUtility.IsPersistent(definition.model))
                throw new InvalidOperationException("Profile model must reference a persistent prefab or imported model asset.");
            ValidatePassiveSource(definition.model.transform);
            // Preview clones are hidden and destroyed in finally. Source assets/scenes are never posed or edited.
            GameObject preview = Object.Instantiate(definition.model);
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var outfit = AvatarPresentationResolver.Resolve(definition, preview.transform, bareHands);
                foreach (AnimationClip clip in new[] { outfit.standing, outfit.floating, outfit.seated })
                    ValidateClip(clip, preview.transform, definition.translationPaths, definition.scalePaths);
            }
            finally { Object.DestroyImmediate(preview); }
        }

        internal static void ValidatePassiveSource(Transform root)
        {
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || root.GetComponentsInChildren<Camera>(true).Length != 0
                || root.GetComponentsInChildren<AudioListener>(true).Length != 0
                || root.GetComponentsInChildren<Collider>(true).Length != 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || root.GetComponentsInChildren<Animation>(true).Length != 0)
                throw new InvalidOperationException("Profile source must be a passive body model without scripts/cameras/physics/legacy Animation.");
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController || animator.applyRootMotion)
                    throw new InvalidOperationException("Profile source Animator must have no controller or root motion. Authoring disables its instance.");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!(renderer is SkinnedMeshRenderer)) throw new InvalidOperationException("Only the three explicit skinned body roles are supported.");
        }

        public static void ValidateClip(AnimationClip clip, Transform root, string[] translationPaths, string[] scalePaths)
        {
            if (!clip || !root || clip.legacy || clip.humanMotion || clip.length <= 0f)
                throw new InvalidOperationException("Use positive-length non-Humanoid Generic clips authored for this mapped hierarchy.");
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0 || AnimationUtility.GetAnimationEvents(clip).Length != 0)
                throw new InvalidOperationException("Passive avatar clips must not change object references or invoke events: " + clip.name);
            HashSet<string> translations = PathSet(root, translationPaths), scales = PathSet(root, scalePaths);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0) throw new InvalidOperationException("Avatar clip has no transform curves: " + clip.name);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(Transform)) throw new InvalidOperationException("Avatar clip has a non-transform binding: " + clip.name);
                Transform node = EmbodiedBodyPresenter.ResolveModelPath(root, binding.path);
                string property = binding.propertyName;
                bool position = IsAxis(property, "m_LocalPosition", false), scale = IsAxis(property, "m_LocalScale", false);
                bool rotation = IsAxis(property, "m_LocalRotation", true) || IsAxis(property, "localEulerAnglesRaw", false)
                    || IsAxis(property, "localEulerAnglesBaked", false);
                if (!(position || scale || rotation) || (position && !translations.Contains(binding.path))
                    || (scale && !scales.Contains(binding.path)))
                    throw new InvalidOperationException("Unreviewed Generic curve " + binding.path + ":" + property + " in " + clip.name);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0) throw new InvalidOperationException("Missing avatar curve data: " + clip.name);
                float expectedScale = property.EndsWith(".x", StringComparison.Ordinal) ? node.localScale.x
                    : property.EndsWith(".y", StringComparison.Ordinal) ? node.localScale.y : node.localScale.z;
                foreach (Keyframe key in curve.keys)
                {
                    if (!Finite(key.time) || !Finite(key.value) || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                        throw new InvalidOperationException("Nonfinite avatar animation key: " + clip.name);
                    // Constant unit/bind-scale channels are accepted; animated scaling would invalidate measured anatomy.
                    if (scale && (Mathf.Abs(key.value - expectedScale) > .00001f
                        || (key.inTangent != 0f && !float.IsInfinity(key.inTangent))
                        || (key.outTangent != 0f && !float.IsInfinity(key.outTangent))))
                        throw new InvalidOperationException("Avatar scale curve changes bind anatomy: " + binding.path);
                }
            }
        }

        private static bool IsAxis(string property, string prefix, bool quaternion) => property == prefix + ".x"
            || property == prefix + ".y" || property == prefix + ".z" || (quaternion && property == prefix + ".w");
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static HashSet<string> PathSet(Transform root, string[] paths)
        {
            if (paths == null) throw new InvalidOperationException("Use explicit empty arrays for absent avatar curve permissions.");
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                EmbodiedBodyPresenter.ResolveModelPath(root, path);
                if (!result.Add(path)) throw new InvalidOperationException("Duplicate avatar curve permission " + path);
            }
            return result;
        }

        [MenuItem("Out of Reach/Avatar/Validate selected profile")]
        private static void ValidateSelected()
        {
            var profile = Selection.activeObject as AvatarPresentationProfile;
            Validate(profile);
            Debug.Log("Avatar structure PASS: " + profile.name + ". Native contact, grip, camera clearance, seated, gait and EVA acceptance NOT RUN.", profile);
        }
    }
}

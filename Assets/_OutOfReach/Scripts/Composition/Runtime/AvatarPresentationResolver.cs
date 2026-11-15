using System;
using System.Collections.Generic;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Resolves caller-owned instances. Does not instantiate, retarget or change player/camera/physics.</summary>
    public static class AvatarPresentationResolver
    {
        public static EmbodiedBodyPresenter.Outfit Resolve(AvatarPresentationProfile.OutfitDefinition definition,
            Transform instance, bool bareHands)
        {
            if (definition == null || !definition.model || !instance)
                throw new ArgumentException("Supply a profile outfit and its owned model instance.");
            var nodes = EmbodiedBodyPresenter.ResolveSemanticBindings(instance, definition.bones);
            var used = new HashSet<Transform>();
            foreach (string role in RequiredRoles())
            {
                if (!nodes.TryGetValue(role, out Transform node)) throw new InvalidOperationException("Avatar mapping is missing role " + role);
                if (!used.Add(node)) throw new InvalidOperationException("Distinct anatomy roles must not alias one transform: " + role);
            }
            foreach (Transform node in instance.GetComponentsInChildren<Transform>(true))
            {
                if (!Finite(node.localPosition) || !Finite(node.localScale)
                    || (node.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Avatar requires finite, unit world-scale transforms: " + node.name);
            }
            foreach (string side in new[] { "L", "R" })
            {
                Transform upper = nodes["upperarm01." + side], lower = nodes["lowerarm01." + side], wrist = nodes["wrist." + side];
                Chain(nodes["CharacterRoot"], nodes["root"], "body root");
                Chain(nodes["root"], upper, "shoulder " + side);
                Chain(upper, lower, "upper arm " + side); Chain(lower, wrist, "forearm " + side);
                float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, wrist.position);
                if (a < .15f || a > .4f || b < .15f || b > .4f)
                    throw new InvalidOperationException("Avatar arm exceeds the existing .15–.4 m segment envelope: " + side);
                Transform thigh = nodes["upperleg01." + side], shin = nodes["lowerleg01." + side], foot = nodes["foot." + side];
                Chain(nodes["root"], thigh, "hip " + side); Chain(thigh, shin, "thigh " + side); Chain(shin, foot, "shin " + side);
                if (Vector3.Distance(thigh.position, shin.position) < .001f || Vector3.Distance(shin.position, foot.position) < .001f)
                    throw new InvalidOperationException("Avatar leg has a zero-length segment: " + side);
                for (int digit = 1; digit <= 5; digit++)
                {
                    Transform parent = wrist;
                    for (int joint = 1; joint <= 3; joint++)
                    {
                        string role = "finger" + digit + "-" + joint + "." + side;
                        Transform finger = nodes[role]; Chain(parent, finger, role); parent = finger;
                    }
                }
            }
            var outfit = new EmbodiedBodyPresenter.Outfit
            {
                modelRoot = instance, torsoLegs = Skin(definition.torsoLegs, instance),
                leftArm = Skin(definition.leftArm, instance), rightArm = Skin(definition.rightArm, instance),
                standing = definition.standing, floating = definition.floating, seated = definition.seated,
                semanticBindings = (EmbodiedBodyPresenter.SemanticBinding[])definition.bones.Clone(),
                leftPalmSubmeshes = bareHands ? CopySubmeshes(definition.leftArm) : null,
                rightPalmSubmeshes = bareHands ? CopySubmeshes(definition.rightArm) : null
            };
            if (outfit.torsoLegs == outfit.leftArm || outfit.torsoLegs == outfit.rightArm || outfit.leftArm == outfit.rightArm
                || instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 3)
                throw new InvalidOperationException("Profile needs exactly three distinct skin roles, prepared once during mapping.");
            if (bareHands)
            {
                RequireBareSkin(outfit.leftArm, definition.leftArm); RequireBareSkin(outfit.rightArm, definition.rightArm);
            }
            foreach (AnimationClip clip in new[] { outfit.standing, outfit.floating, outfit.seated })
                if (!clip || clip.legacy || clip.humanMotion || clip.length <= 0f)
                    throw new InvalidOperationException("Profile requires three positive-length, non-Humanoid Generic clips.");
            return outfit;
        }

        public static IEnumerable<string> RequiredRoles()
        {
            yield return "CharacterRoot"; yield return "root";
            foreach (string side in new[] { "L", "R" })
            {
                foreach (string part in new[] { "upperarm01.", "lowerarm01.", "wrist.", "upperleg01.", "lowerleg01.", "foot." })
                    yield return part + side;
                for (int digit = 1; digit <= 5; digit++)
                    for (int joint = 1; joint <= 3; joint++) yield return "finger" + digit + "-" + joint + "." + side;
            }
        }

        public static void ValidateOffsets(AvatarPresentationProfile profile)
        {
            if (!profile) throw new ArgumentNullException(nameof(profile));
            foreach (Vector3 offset in new[] { profile.StandingOffset, profile.FloatingOffset, profile.SeatedOffset })
                if (!Finite(offset) || offset.magnitude > 1f) throw new InvalidOperationException("Profile visual offsets must be finite and within one metre.");
        }

        private static SkinnedMeshRenderer Skin(AvatarPresentationProfile.SkinRole role, Transform root)
        {
            if (role == null) throw new InvalidOperationException("Missing avatar skin role.");
            var skin = EmbodiedBodyPresenter.ResolveModelPath(root, role.path).GetComponent<SkinnedMeshRenderer>();
            if (!skin || !skin.sharedMesh || !skin.rootBone || !skin.rootBone.IsChildOf(root)
                || skin.bones.Length == 0 || skin.sharedMesh.bindposes.Length != skin.bones.Length)
                throw new InvalidOperationException("Invalid skin/root/bind poses at " + role.path);
            foreach (Transform bone in skin.bones)
                if (!bone || !bone.IsChildOf(root)) throw new InvalidOperationException("Skin refers outside its model: " + role.path);
            if (role.materials == null || role.materials.Length != skin.sharedMesh.subMeshCount)
                throw new InvalidOperationException("Explicit material count must match submeshes: " + role.path);
            foreach (Material material in role.materials)
                if (!material) throw new InvalidOperationException("Missing explicit material at " + role.path);
            return skin;
        }

        private static void RequireBareSkin(SkinnedMeshRenderer skin, AvatarPresentationProfile.SkinRole role)
        {
            if (!skin.sharedMesh.isReadable) throw new InvalidOperationException("WORK arms must remain CPU-readable for existing skin contact proof.");
            if (role.palmSubmeshes == null || role.palmSubmeshes.Length == 0)
                throw new InvalidOperationException("Profile WORK arms need explicit palm skin submesh membership.");
            var unique = new HashSet<int>();
            foreach (int index in role.palmSubmeshes)
                if (index < 0 || index >= skin.sharedMesh.subMeshCount || !unique.Add(index))
                    throw new InvalidOperationException("Palm submesh indices must be unique and within the actual arm mesh.");
            if (skin.sharedMesh.vertices.Length != skin.sharedMesh.boneWeights.Length
                || skin.sharedMesh.vertices.Length != skin.sharedMesh.normals.Length)
                throw new InvalidOperationException("WORK palm calibration needs matching readable vertices, normals and four-weight skin data.");
        }

        private static int[] CopySubmeshes(AvatarPresentationProfile.SkinRole role) => role.palmSubmeshes == null
            ? null : (int[])role.palmSubmeshes.Clone();

        private static void Chain(Transform parent, Transform child, string label)
        {
            if (parent == child || !child.IsChildOf(parent)) throw new InvalidOperationException("Invalid avatar ancestry: " + label);
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}

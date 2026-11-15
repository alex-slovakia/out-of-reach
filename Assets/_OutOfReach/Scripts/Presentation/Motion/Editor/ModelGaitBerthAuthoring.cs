using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Motion.Editor
{
    /// <summary>
    /// Authors the berth body frame for <see cref="EmbodiedBodyPresenter.ConfigureRest"/> from the habitat's authored
    /// SleepCamera anchor: the real WORK rig's eyes sit at the camera, the body upright (+Y) with its chest (+Z) along the
    /// camera's level forward into the aisle. It adds or updates one sibling transform and never moves the camera,
    /// the source anchors or any collider. Root runs it on its own scene; nothing here edits Bootstrap by itself.
    /// </summary>
    public static class ModelGaitBerthAuthoring
    {
        public const string FrameName = "SleepBodyFrame";
        public const string BodyModelPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Meshes/A7_BodyFit02_WORK.fbx";
        public const string StandingClipPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/StandingIdleV02/Animations/BODY_StandIdle_V02.fbx";

        /// <summary>Midpoint of the WORK rig's eye joints in its held standing pose, in the body frame (origin at the feet).</summary>
        public static Vector3 MeasureEyeLocal()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(StandingClipPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (!model || !clip) throw new InvalidOperationException("Cannot load the WORK body or its standing clip.");
            GameObject instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform root = instance.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.localScale = Vector3.one;
                clip.SampleAnimation(instance, 0f);
                Transform left = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "eye.L");
                Transform right = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "eye.R");
                if (!left || !right) throw new InvalidOperationException("The WORK rig has no eye.L / eye.R joints.");
                return root.InverseTransformPoint((left.position + right.position) * .5f);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>Body frame whose eyes (at <paramref name="eyeLocal"/>) sit at the camera, upright, chest along its level forward.</summary>
        public static Pose BodyFrameForCamera(Vector3 cameraPosition, Vector3 cameraForward, Vector3 up, Vector3 eyeLocal)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, up);
            if (forward.sqrMagnitude < 1e-8f) throw new ArgumentException("The berth camera must look across the berth, not along its up axis.");
            Quaternion rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
            return new Pose(cameraPosition - rotation * eyeLocal, rotation);
        }

        /// <summary>Adds or updates <see cref="FrameName"/> beside the SleepCamera anchor (same parent).</summary>
        public static Transform CreateOrUpdate(Transform sleepCamera)
        {
            if (!sleepCamera) throw new ArgumentNullException(nameof(sleepCamera));
            Transform parent = sleepCamera.parent;
            Pose pose = BodyFrameForCamera(sleepCamera.position, sleepCamera.forward, parent ? parent.up : Vector3.up, MeasureEyeLocal());
            Transform frame = parent ? parent.Find(FrameName) : null;
            if (!frame)
            {
                var node = new GameObject(FrameName);
                Undo.RegisterCreatedObjectUndo(node, "Create berth body frame");
                frame = node.transform;
                frame.SetParent(parent, false);
            }
            else Undo.RecordObject(frame, "Update berth body frame");
            frame.SetPositionAndRotation(pose.position, pose.rotation);
            frame.localScale = Vector3.one;
            EditorUtility.SetDirty(frame);
            return frame;
        }

        [MenuItem("Out of Reach/Avatar/Create berth body frame at SleepCamera")]
        private static void CreateFromMenu()
        {
            Transform[] cameras = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "SleepCamera").ToArray();
            if (cameras.Length != 1)
            {
                Debug.LogError("Expected exactly one SleepCamera anchor in the open scenes, found " + cameras.Length + ".");
                return;
            }
            Transform frame = CreateOrUpdate(cameras[0]);
            Debug.Log("Berth body frame " + frame.name + " at " + frame.position.ToString("F4") + " rotation "
                + frame.rotation.eulerAngles.ToString("F2") + " (eye offset " + MeasureEyeLocal().ToString("F4") + ")", frame);
        }
    }
}

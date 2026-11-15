using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Hands.Editor
{
    /// <summary>
    /// Builds the WORK <see cref="BareRailGripPose"/> from the WORK-BARE-GRIP-01 art in
    /// <c>docs/art-direction/work-bare-grip-v01</c>. The finger samples there are Blender bone-space deltas; they are
    /// converted to absolute Unity local rotations on the real FitV02 WORK rig and accepted only when the converted
    /// closed pose reproduces the art's Unity model-space bone heads. The rail frame comes from the Unity model-space
    /// anchors, expressed in the wrist bone's local frame exactly as <see cref="BareArmContactPresenter"/> reads it
    /// (<c>Wrist.TransformPoint(railPoint)</c>, <c>LookRotation(railAxis, palmNormal)</c>). Nothing else is changed.
    /// </summary>
    public static class WorkGripPoseImporter
    {
        public const string ArtFolder = "docs/art-direction/work-bare-grip-v01";
        public const string BodyModelPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Meshes/A7_BodyFit02_WORK.fbx";
        public const string DefaultOutputPath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/WorkGripV01/WORK_GripRail_V01.asset";
        /// <summary>Largest accepted closed-pose bone-head deviation from the art, metres.</summary>
        public const float HeadTolerance = .0005f;

        /// <summary>Evidence of one import.</summary>
        public sealed class Report
        {
            public string LeftConvention, RightConvention;
            public float LeftHeadError, RightHeadError;            // metres: closed pose on the real rig vs the art heads
            public float LeftRailFrameError, RightRailFrameError;   // metres: rail point vs the art's wrist-frame point
            public float LeftRadius, RightRadius;                  // metres, from the art anchors
            public float WorstAngleError;                          // degrees: closed joint angle vs the art angle_deg
            public bool InputHashesMatch;                          // JSON and FBX bytes vs the art manifest
            public readonly List<string> Notes = new List<string>();

            public override string ToString()
            {
                var b = new StringBuilder();
                b.AppendFormat(CultureInfo.InvariantCulture,
                    "conversion L '{0}' R '{1}'; closed bone heads max error L {2:0.0000} mm R {3:0.0000} mm; " +
                    "rail point vs art wrist frame L {4:0.000} mm R {5:0.000} mm; radius L {6} m R {7} m; " +
                    "worst closed joint angle error {8:0.000} deg; input hashes match manifest: {9}",
                    LeftConvention, RightConvention, LeftHeadError * 1000f, RightHeadError * 1000f,
                    LeftRailFrameError * 1000f, RightRailFrameError * 1000f, LeftRadius, RightRadius, WorstAngleError, InputHashesMatch);
                foreach (string note in Notes) b.Append("\n- ").Append(note);
                return b.ToString();
            }
        }

        private delegate Quaternion Conversion(Quaternion blenderXyzw);

        // Candidate mappings of a Blender bone-space delta into the Unity bone's local frame. The FBX right-to-left
        // handed conversion mirrors one axis; the one that reproduces the art's closed pose on the real rig wins.
        private static readonly string[] ConversionNames = { "mirror X (x,-y,-z,w)", "mirror Z (-x,-y,z,w)", "mirror Y (-x,y,-z,w)", "unchanged (x,y,z,w)" };
        private static readonly Conversion[] Conversions =
        {
            q => new Quaternion(q.x, -q.y, -q.z, q.w),
            q => new Quaternion(-q.x, -q.y, q.z, q.w),
            q => new Quaternion(-q.x, q.y, -q.z, q.w),
            q => q
        };

        [MenuItem("Out of Reach/Avatar/Build WORK rail grip pose (WORK-BARE-GRIP-01)")]
        public static void BuildFromMenu()
        {
            BareRailGripPose pose = Build(ArtFolder, DefaultOutputPath, out Report report);
            Debug.Log("WORK rail grip pose written to " + DefaultOutputPath + "\n" + report, pose);
        }

        /// <summary>
        /// Batch entry: <c>-executeMethod OutOfReach.Presentation.Hands.Editor.WorkGripPoseImporter.BuildBatch</c>
        /// with optional <c>-gripArt &lt;folder&gt;</c> and <c>-gripOutput &lt;Assets/...asset&gt;</c>. Exits 0 on success.
        /// </summary>
        public static void BuildBatch()
        {
            string art = Argument("-gripArt") ?? ArtFolder, output = Argument("-gripOutput") ?? DefaultOutputPath;
            try
            {
                Build(art, output, out Report report);
                Debug.Log("WORK_GRIP_IMPORT_OK " + output + "\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("WORK_GRIP_IMPORT_FAILED " + error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Builds, proves and writes the asset. An existing asset keeps its GUID, so scene references survive.</summary>
        public static BareRailGripPose Build(string artFolder, string outputAssetPath, out Report report)
        {
            if (string.IsNullOrEmpty(outputAssetPath) || !outputAssetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !outputAssetPath.EndsWith(".asset", StringComparison.Ordinal))
                throw new ArgumentException("Write the grip pose to an Assets/... .asset path.");
            BareRailGripPose pose = CreateFromArt(artFolder, out report);
            pose.name = Path.GetFileNameWithoutExtension(outputAssetPath);
            EnsureFolder(Path.GetDirectoryName(outputAssetPath).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<BareRailGripPose>(outputAssetPath);
            if (existing)
            {
                EditorUtility.CopySerialized(pose, existing);
                existing.name = pose.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(pose);
            }
            else AssetDatabase.CreateAsset(pose, outputAssetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<BareRailGripPose>(outputAssetPath);
        }

        /// <summary>In-memory build and proof. The caller owns the returned instance (DestroyImmediate it when done).</summary>
        public static BareRailGripPose CreateFromArt(string artFolder, out Report report)
        {
            report = new Report();
            Dictionary<string, object> samples = ReadJson(artFolder, "grip_pose_samples.json");
            Dictionary<string, object> anchors = ReadJson(artFolder, "grip_anchors.json");
            Dictionary<string, object> manifest = ReadJson(artFolder, "grip_manifest.json");
            Dictionary<string, object> files = Obj(manifest, "files");
            string leftSha = Str(Obj(files, "BODY_WORK_GripRail_L_V01.fbx"), "sha256");
            string rightSha = Str(Obj(files, "BODY_WORK_GripRail_R_V01.fbx"), "sha256");
            string bodySha = Str(Obj(manifest, "input_body"), "lfs_sha256");
            report.InputHashesMatch = FileMatches(Path.Combine(artFolder, "grip_pose_samples.json"), Str(Obj(files, "grip_pose_samples.json"), "sha256"), report)
                & FileMatches(Path.Combine(artFolder, "grip_anchors.json"), Str(Obj(files, "grip_anchors.json"), "sha256"), report)
                & FileMatches(Path.Combine(artFolder, "BODY_WORK_GripRail_L_V01.fbx"), leftSha, report)
                & FileMatches(Path.Combine(artFolder, "BODY_WORK_GripRail_R_V01.fbx"), rightSha, report);
            // The pose was solved against exactly this body; another body would need a new solve.
            string actualBody = Sha256(File.ReadAllBytes(BodyModelPath));
            if (actualBody != bodySha)
                throw new InvalidOperationException("The FitV02 WORK body is not the one the grip was solved for: " + actualBody + " (art: " + bodySha + ").");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath);
            if (!model) throw new InvalidOperationException("Cannot load " + BodyModelPath);
            GameObject instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform root = instance.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.localScale = Vector3.one;
                var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    if (!nodes.ContainsKey(node.name)) nodes.Add(node.name, node);
                    else if (node.name.StartsWith("finger", StringComparison.Ordinal) || node.name.StartsWith("wrist", StringComparison.Ordinal))
                        throw new InvalidOperationException("Ambiguous hand joint " + node.name);
                report.Notes.Add("FitV02 bones set to the skin bind pose (the art's frame): " + PoseAtSkinBind(root, report));

                var pose = ScriptableObject.CreateInstance<BareRailGripPose>();
                pose.left = BuildHand(root, nodes, "L", samples, anchors, report, out report.LeftConvention, out report.LeftHeadError,
                    out report.LeftRailFrameError, out report.LeftRadius);
                pose.right = BuildHand(root, nodes, "R", samples, anchors, report, out report.RightConvention, out report.RightHeadError,
                    out report.RightRailFrameError, out report.RightRadius);
                pose.sourceLeftSha256 = leftSha;
                pose.sourceRightSha256 = rightSha;
                pose.sourceBodySha256 = bodySha;
                if (!pose.IsValid)
                {
                    Object.DestroyImmediate(pose);
                    throw new InvalidOperationException("The built grip pose fails BareRailGripPose.IsValid.\n" + report);
                }
                return pose;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Unity local rotations of one hand's fifteen finger joints at the FitV02 WORK skin bind pose.</summary>
        public static Dictionary<string, Quaternion> BindLocalRotations(string side)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BodyModelPath);
            if (!model) throw new InvalidOperationException("Cannot load " + BodyModelPath);
            GameObject instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PoseAtSkinBind(instance.transform, new Report());
                var result = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("finger", StringComparison.Ordinal) && t.name.EndsWith("." + side, StringComparison.Ordinal))
                        result[t.name] = t.localRotation;
                return result;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // The FitV02 nodes rest in the body's fitted first-person pose; the art was solved on the skin bind pose,
        // which the skins' bind matrices define exactly. Parents are visited before children.
        private static int PoseAtSkinBind(Transform root, Report report)
        {
            var bindWorld = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
                Transform[] bones = skin.bones;
                Matrix4x4 rendererWorld = skin.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (!bones[i]) continue;
                    Matrix4x4 world = rendererWorld * bindposes[i].inverse;
                    if (bindWorld.TryGetValue(bones[i], out Matrix4x4 other))
                    {
                        float difference = 0f;
                        for (int k = 0; k < 16; k++) difference = Mathf.Max(difference, Mathf.Abs(other[k] - world[k]));
                        if (difference > 1e-4f) report.Notes.Add("skins differ on the bind of " + bones[i].name + " by " + difference);
                        continue;
                    }
                    bindWorld[bones[i]] = world;
                }
            }
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (bindWorld.TryGetValue(t, out Matrix4x4 m))
                {
                    t.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    count++;
                }
            if (count == 0) throw new InvalidOperationException("The FitV02 WORK skins carry no bind pose.");
            return count;
        }

        /// <summary>One Blender bone-space sample (Unity component order) as stored in the art, for checks and tests.</summary>
        public static Quaternion ReadBlenderSample(string artFolder, string side, int frame, string bone)
        {
            Dictionary<string, object> take = Obj(Obj(ReadJson(artFolder, "grip_pose_samples.json"), "WORK_GripRail_" + side),
                "local_rotation_wxyz_blender_bone_space");
            return BlenderXyzw(Obj(take, frame.ToString(CultureInfo.InvariantCulture)), bone);
        }

        private static BareRailGripPose.HandPose BuildHand(Transform root, Dictionary<string, Transform> nodes, string side,
            Dictionary<string, object> samples, Dictionary<string, object> anchors, Report report,
            out string convention, out float headError, out float railFrameError, out float radius)
        {
            const int frames = BareRailGripPose.Frames, joints = BareRailGripPose.Joints;
            string suffix = "." + side;
            Transform wrist = Node(nodes, "wrist" + suffix);
            var fingers = new Transform[joints];
            var bind = new Quaternion[joints];
            for (int digit = 0; digit < 5; digit++)
                for (int joint = 0; joint < 3; joint++)
                {
                    int index = digit * 3 + joint;
                    fingers[index] = Node(nodes, "finger" + (digit + 1) + "-" + (joint + 1) + suffix);
                    bind[index] = fingers[index].localRotation;
                }

            Dictionary<string, object> take = Obj(Obj(samples, "WORK_GripRail_" + side), "local_rotation_wxyz_blender_bone_space");
            if (take.Count != frames) throw new InvalidOperationException("The " + side + " take has " + take.Count + " frames, expected " + frames + ".");
            var blender = new Quaternion[frames * joints];
            for (int f = 0; f < frames; f++)
            {
                Dictionary<string, object> frame = Obj(take, (f + 1).ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < joints; i++) blender[f * joints + i] = BlenderXyzw(frame, fingers[i].name);
            }

            Dictionary<string, object> hand = Obj(anchors, side);
            Dictionary<string, object> heads = Obj(hand, "closed_pose_bone_heads_unity_model_m");
            int best = -1;
            headError = float.MaxValue;
            for (int c = 0; c < Conversions.Length; c++)
            {
                for (int i = 0; i < joints; i++) fingers[i].localRotation = bind[i] * Conversions[c](blender[(frames - 1) * joints + i]);
                float error = 0f;
                foreach (KeyValuePair<string, object> head in heads)
                    error = Mathf.Max(error, Vector3.Distance(root.InverseTransformPoint(Node(nodes, head.Key).position), ToVector(head.Value, head.Key)));
                if (error < headError) { headError = error; best = c; }
            }
            for (int i = 0; i < joints; i++) fingers[i].localRotation = bind[i];
            convention = ConversionNames[best];
            if (headError > HeadTolerance)
            {
                var diagnosis = new StringBuilder();
                foreach (KeyValuePair<string, object> head in heads)
                {
                    Transform node = Node(nodes, head.Key);
                    diagnosis.AppendFormat(CultureInfo.InvariantCulture, "\n  {0}: rig bind {1} art closed {2} parent {3}",
                        head.Key, root.InverseTransformPoint(node.position).ToString("F4"), ToVector(head.Value, head.Key).ToString("F4"),
                        node.parent ? node.parent.name : "-");
                }
                for (Transform t = wrist; t && t != root; t = t.parent)
                    diagnosis.AppendFormat(CultureInfo.InvariantCulture, "\n  chain {0} localPos {1} localRot {2} localScale {3}",
                        t.name, t.localPosition.ToString("F4"), t.localRotation.eulerAngles.ToString("F2"), t.localScale.ToString("F4"));
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "No finger-delta mapping reproduces the art's closed {0} hand on the FitV02 WORK rig (best '{1}', {2:0.000} mm).{3}",
                    side, convention, headError * 1000f, diagnosis));
            }

            var result = new BareRailGripPose.HandPose();
            for (int k = 0; k < frames * joints; k++)
            {
                Quaternion q = bind[k % joints] * Conversions[best](blender[k]);
                result.rotations[k] = Normalize(q);
            }

            // Closed angle of each joint against its own bind must match the art's angle_deg (independent of the mapping).
            Dictionary<string, object> closedJoints = Obj(hand, "joints");
            for (int i = 0; i < joints; i++)
            {
                if (!closedJoints.TryGetValue(fingers[i].name, out object entry)) continue;
                float artAngle = Num((Dictionary<string, object>)entry, "angle_deg");
                float angle = Quaternion.Angle(bind[i], result.rotations[(frames - 1) * joints + i]);
                report.WorstAngleError = Mathf.Max(report.WorstAngleError, Mathf.Abs(angle - artAngle));
            }

            // The rail frame in the wrist bone's local frame. The take never moves the wrist, so its bind is the frame.
            Dictionary<string, object> rail = Obj(hand, "rail");
            radius = Num(rail, "radius_m");
            if (Mathf.Abs(radius - BareRailGripPose.RailRadius) > 1e-6f)
                throw new InvalidOperationException("The " + side + " art was solved for a " + radius + " m rail, not " + BareRailGripPose.RailRadius + " m.");
            result.railPoint = wrist.InverseTransformPoint(root.TransformPoint(Vec(rail, "axis_point_unity_model_m")));
            result.railAxis = wrist.InverseTransformDirection(root.TransformDirection(Vec(rail, "axis_dir_unity_model"))).normalized;
            Vector3 palm = wrist.InverseTransformDirection(root.TransformDirection(Vec(hand, "palm_normal_unity_model")));
            float skew = Mathf.Abs(Vector3.Dot(palm.normalized, result.railAxis));
            if (skew > .02f) throw new InvalidOperationException("The " + side + " palm normal is not perpendicular to the rail (" + skew + ").");
            result.palmNormal = Vector3.ProjectOnPlane(palm, result.railAxis).normalized;

            // Cross-check: the art also gives the point in the Blender wrist bone frame; one mirror of it must agree.
            Vector3 artPoint = Vec(rail, "axis_point_in_wrist_bone_bind_frame_m");
            railFrameError = Mathf.Min(
                Mathf.Min(Vector3.Distance(new Vector3(-artPoint.x, artPoint.y, artPoint.z), result.railPoint),
                    Vector3.Distance(new Vector3(artPoint.x, artPoint.y, -artPoint.z), result.railPoint)),
                Mathf.Min(Vector3.Distance(new Vector3(artPoint.x, -artPoint.y, artPoint.z), result.railPoint),
                    Vector3.Distance(artPoint, result.railPoint)));
            if (!result.IsValid) throw new InvalidOperationException("The built " + side + " hand pose is not valid.");
            return result;
        }

        private static Quaternion BlenderXyzw(Dictionary<string, object> frame, string bone)
        {
            if (!frame.TryGetValue(bone, out object value) || !(value is List<object> wxyz) || wxyz.Count != 4)
                throw new InvalidOperationException("Missing four-number sample for " + bone);
            return new Quaternion(ToFloat(wxyz[1]), ToFloat(wxyz[2]), ToFloat(wxyz[3]), ToFloat(wxyz[0]));
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }

        private static Transform Node(Dictionary<string, Transform> nodes, string name) =>
            nodes.TryGetValue(name, out Transform node) ? node : throw new InvalidOperationException("The FitV02 WORK rig has no joint " + name);

        private static Dictionary<string, object> ReadJson(string folder, string file)
        {
            string path = Path.Combine(folder, file);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing grip art file (merge the art folder first).", path);
            return MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>
                ?? throw new InvalidOperationException(path + " is not a JSON object.");
        }

        private static Dictionary<string, object> Obj(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out object value) && value is Dictionary<string, object> result ? result
                : throw new InvalidOperationException("Missing JSON object '" + key + "'.");

        private static string Str(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out object value) && value is string result ? result
                : throw new InvalidOperationException("Missing JSON string '" + key + "'.");

        private static float Num(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out object value) ? ToFloat(value) : throw new InvalidOperationException("Missing JSON number '" + key + "'.");

        private static Vector3 Vec(Dictionary<string, object> parent, string key) =>
            parent.TryGetValue(key, out object value) ? ToVector(value, key) : throw new InvalidOperationException("Missing JSON vector '" + key + "'.");

        private static Vector3 ToVector(object value, string what) =>
            value is List<object> xyz && xyz.Count == 3 ? new Vector3(ToFloat(xyz[0]), ToFloat(xyz[1]), ToFloat(xyz[2]))
                : throw new InvalidOperationException("'" + what + "' is not a three-number vector.");

        private static float ToFloat(object value) =>
            value is double number ? (float)number : throw new InvalidOperationException("Expected a JSON number.");

        private static bool FileMatches(string path, string expected, Report report)
        {
            if (!File.Exists(path)) { report.Notes.Add("not checked (missing): " + path); return false; }
            byte[] bytes = File.ReadAllBytes(path);
            if (Sha256(bytes) == expected) return true;
            if (bytes.Length < 200 && Encoding.ASCII.GetString(bytes).StartsWith("version https://git-lfs", StringComparison.Ordinal))
            { report.Notes.Add("not checked (Git LFS pointer, run git lfs pull): " + path); return false; }
            string normalised = Sha256(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n")));
            if (normalised == expected) { report.Notes.Add("matches after CRLF->LF: " + path); return true; }
            report.Notes.Add("hash differs from the art manifest: " + path);
            return false;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var b = new StringBuilder(64);
                foreach (byte x in sha.ComputeHash(bytes)) b.Append(x.ToString("x2", CultureInfo.InvariantCulture));
                return b.ToString();
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Minimal JSON reader for the art files (objects keyed by bone and frame names, which JsonUtility cannot read).</summary>
        private static class MiniJson
        {
            public static object Parse(string json)
            {
                int i = 0;
                object value = Value(json, ref i);
                Skip(json, ref i);
                if (i != json.Length) throw new FormatException("Trailing JSON content at " + i);
                return value;
            }

            private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            private static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
                char c = s[i];
                if (c == '{')
                {
                    i++;
                    var result = new Dictionary<string, object>(StringComparer.Ordinal);
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; return result; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string key = Text(s, ref i);
                        Skip(s, ref i); Expect(s, ref i, ':');
                        result[key] = Value(s, ref i);
                        Skip(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        Expect(s, ref i, '}');
                        return result;
                    }
                }
                if (c == '[')
                {
                    i++;
                    var result = new List<object>();
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; return result; }
                    while (true)
                    {
                        result.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (i < s.Length && s[i] == ',') { i++; continue; }
                        Expect(s, ref i, ']');
                        return result;
                    }
                }
                if (c == '"') return Text(s, ref i);
                if (Word(s, ref i, "true")) return true;
                if (Word(s, ref i, "false")) return false;
                if (Word(s, ref i, "null")) return null;
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("Unexpected JSON character '" + c + "' at " + i);
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private static bool Word(string s, ref int i, string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
                i += word.Length;
                return true;
            }

            private static string Text(string s, ref int i)
            {
                Expect(s, ref i, '"');
                var b = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length) throw new FormatException("Unterminated JSON string");
                    char c = s[i++];
                    if (c == '"') return b.ToString();
                    if (c != '\\') { b.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': b.Append('\n'); break;
                        case 't': b.Append('\t'); break;
                        case 'r': b.Append('\r'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'u': b.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                        default: b.Append(e); break;
                    }
                }
            }

            private static void Expect(string s, ref int i, char c)
            {
                if (i >= s.Length || s[i] != c) throw new FormatException("Expected '" + c + "' at " + i);
                i++;
            }
        }
    }
}

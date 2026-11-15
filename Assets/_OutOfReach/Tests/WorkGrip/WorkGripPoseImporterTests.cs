using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.Hands.Editor;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Tests.WorkGrip
{
    /// <summary>The WORK rail grip pose built from the WORK-BARE-GRIP-01 art, checked against the art itself.</summary>
    public sealed class WorkGripPoseImporterTests
    {
        private static string Art => WorkGripPoseImporter.ArtFolder;

        private static void RequireArt()
        {
            if (!File.Exists(Path.Combine(Art, "grip_pose_samples.json")))
                Assert.Ignore("The WORK-BARE-GRIP-01 art folder " + Art + " is not in this checkout.");
        }

        private static Dictionary<string, Quaternion> BindFingers(string side) => WorkGripPoseImporter.BindLocalRotations(side);

        [Test]
        public void BuildsAValidPoseThatReproducesTheArtClosedHand()
        {
            RequireArt();
            BareRailGripPose pose = WorkGripPoseImporter.CreateFromArt(Art, out WorkGripPoseImporter.Report report);
            try
            {
                Debug.Log("[WORKGRIP] " + report);
                Assert.IsTrue(pose.IsValid, "BareRailGripPose.IsValid");
                Assert.IsTrue(pose.left.IsValid && pose.right.IsValid);
                Assert.AreEqual(BareRailGripPose.Frames * BareRailGripPose.Joints, pose.left.rotations.Length);
                Assert.AreEqual(BareRailGripPose.Frames * BareRailGripPose.Joints, pose.right.rotations.Length);
                Assert.Less(report.LeftHeadError, WorkGripPoseImporter.HeadTolerance, "left closed hand vs art bone heads");
                Assert.Less(report.RightHeadError, WorkGripPoseImporter.HeadTolerance, "right closed hand vs art bone heads");
                Assert.Less(report.WorstAngleError, .05f, "closed joint angles vs art angle_deg");
                Assert.Less(report.LeftRailFrameError, .001f, "left rail point vs the art wrist-frame point");
                Assert.Less(report.RightRailFrameError, .001f, "right rail point vs the art wrist-frame point");
                Assert.IsTrue(report.InputHashesMatch, "art JSON and FBX bytes vs the art manifest");
            }
            finally { Object.DestroyImmediate(pose); }
        }

        [Test]
        public void RailIs15MillimetresAndSitsInThePalm()
        {
            RequireArt();
            BareRailGripPose pose = WorkGripPoseImporter.CreateFromArt(Art, out WorkGripPoseImporter.Report report);
            try
            {
                Assert.AreEqual(.015f, BareRailGripPose.RailRadius, 1e-7f);
                Assert.AreEqual(.015f, report.LeftRadius, 1e-6f);
                Assert.AreEqual(.015f, report.RightRadius, 1e-6f);
                foreach (BareRailGripPose.HandPose hand in new[] { pose.left, pose.right })
                {
                    // In the wrist bone frame the rail centre lies a few centimetres from the wrist, across the palm.
                    Assert.That(hand.railPoint.magnitude, Is.InRange(.04f, .12f));
                    Assert.Less(Mathf.Abs(Vector3.Dot(hand.railAxis, hand.palmNormal)), .001f);
                }
                // The hands were solved independently, but the rail sits about as far from each wrist.
                Assert.Less(Mathf.Abs(pose.left.railPoint.magnitude - pose.right.railPoint.magnitude), .005f);
            }
            finally { Object.DestroyImmediate(pose); }
        }

        [TestCase("R", 1, "finger1-1"), TestCase("R", 11, "finger2-2"), TestCase("R", 21, "finger2-1"), TestCase("R", 21, "finger5-3"),
         TestCase("L", 1, "finger1-1"), TestCase("L", 15, "finger3-1"), TestCase("L", 21, "finger4-2"), TestCase("L", 21, "finger1-1")]
        public void SampledRotationsMatchTheArtAngles(string side, int frame, string bone)
        {
            RequireArt();
            BareRailGripPose pose = WorkGripPoseImporter.CreateFromArt(Art, out _);
            try
            {
                Dictionary<string, Quaternion> bind = BindFingers(side);
                string name = bone + "." + side;
                int digit = bone[6] - '1', joint = bone[8] - '1';
                int index = (frame - 1) * BareRailGripPose.Joints + digit * 3 + joint;
                Quaternion built = (side == "L" ? pose.left : pose.right).rotations[index];
                Quaternion art = WorkGripPoseImporter.ReadBlenderSample(Art, side, frame, name);
                float artAngle = 2f * Mathf.Acos(Mathf.Clamp01(Mathf.Abs(art.w))) * Mathf.Rad2Deg;
                Assert.AreEqual(artAngle, Quaternion.Angle(bind[name], built), .05f, name + " frame " + frame);
                // Progress sampling at the ends returns exactly the first and the last frame.
                var hand = side == "L" ? pose.left : pose.right;
                Assert.AreEqual(0f, Quaternion.Angle(hand.Sample(digit * 3 + joint, (frame - 1) / 20f), built), .01f);
            }
            finally { Object.DestroyImmediate(pose); }
        }

        [Test]
        public void CommittedAssetEqualsAFreshImport()
        {
            RequireArt();
            var asset = AssetDatabase.LoadAssetAtPath<BareRailGripPose>(WorkGripPoseImporter.DefaultOutputPath);
            if (!asset) Assert.Ignore("No generated asset at " + WorkGripPoseImporter.DefaultOutputPath + " yet.");
            BareRailGripPose fresh = WorkGripPoseImporter.CreateFromArt(Art, out _);
            try
            {
                Assert.IsTrue(asset.IsValid);
                foreach (bool left in new[] { true, false })
                {
                    BareRailGripPose.HandPose a = left ? asset.left : asset.right, b = left ? fresh.left : fresh.right;
                    Assert.Less(Vector3.Distance(a.railPoint, b.railPoint), 1e-6f);
                    Assert.Less(Vector3.Angle(a.railAxis, b.railAxis), .001f);
                    Assert.Less(Vector3.Angle(a.palmNormal, b.palmNormal), .001f);
                    for (int i = 0; i < a.rotations.Length; i++) Assert.Less(Quaternion.Angle(a.rotations[i], b.rotations[i]), .001f);
                }
            }
            finally { Object.DestroyImmediate(fresh); }
        }
    }
}

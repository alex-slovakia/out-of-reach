using NUnit.Framework;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Tests.BodyAnim
{
    /// <summary>
    /// Verify-ground: the on-foot walk presentation never rolls the view (Alex, 15 Nov: no camera roll from A/D).
    /// Strafe lean and gait sway reach the camera only as zero roll; the small turn-lag yaw stays within one degree.
    /// </summary>
    public sealed class WalkCameraNoRollTests
    {
        private static readonly float[] Rolls = { -1.5f, -.71f, -.06f, 0f, .06f, .65f, .71f, 1.5f };
        private static readonly float[] Yaws = { -2f, -.3f, 0f, .3f, 2f };

        [Test]
        public void CameraMotionNeverRollsTheView()
        {
            foreach (float roll in Rolls)
            foreach (float yaw in Yaws)
            {
                Quaternion q = FirstPersonMotionPresenter.CameraMotionRotation(yaw, roll);
                Vector3 up = q * Vector3.up;
                Vector3 right = q * Vector3.right;
                Assert.That(Vector3.Angle(up, Vector3.up), Is.LessThan(1e-3f), $"yaw {yaw}, roll {roll}: view up tilted");
                Assert.That(Mathf.Abs(right.y), Is.LessThan(1e-5f), $"yaw {yaw}, roll {roll}: horizon not level");
            }
        }

        [Test]
        public void TurnLagYawStaysWithinOneDegree()
        {
            Assert.That(Quaternion.Angle(Quaternion.identity, FirstPersonMotionPresenter.CameraMotionRotation(.3f, .65f)),
                Is.EqualTo(.3f).Within(1e-3f));
            Assert.That(Quaternion.Angle(Quaternion.identity, FirstPersonMotionPresenter.CameraMotionRotation(5f, 0f)),
                Is.EqualTo(1f).Within(1e-3f));
            Assert.That(FirstPersonMotionPresenter.CameraMotionRotation(float.NaN, float.NaN), Is.EqualTo(Quaternion.identity));
        }
    }
}

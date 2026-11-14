using NUnit.Framework;
using OutOfReach.Core.Runtime;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>The launch rule behind "always start in full screen" (the switch itself only runs in a Windows player).</summary>
    public sealed class FullscreenOnLaunchTests
    {
        [Test]
        public void APlayerLaunch_IsSwitchedToFullScreen_TheEditorNever()
        {
            Assert.That(FullscreenOnLaunch.ShouldForce(false, new[] { "OutOfReach.exe" }), Is.True);
            Assert.That(FullscreenOnLaunch.ShouldForce(false, null), Is.True);
            Assert.That(FullscreenOnLaunch.ShouldForce(true, new[] { "Unity.exe" }), Is.False, "Play mode and tests keep the Game view");
            Assert.That(Application.isEditor, Is.True, "so this test run itself is never switched");
        }

        [TestCase("-screen-fullscreen")]
        [TestCase("-window-mode")]
        [TestCase("-popupwindow")]
        [TestCase("-WINDOWED")]
        public void AnExplicitWindowRequest_IsRespected(string argument)
        {
            Assert.That(FullscreenOnLaunch.ShouldForce(false, new[] { "OutOfReach.exe", argument, "0" }), Is.False);
        }
    }
}

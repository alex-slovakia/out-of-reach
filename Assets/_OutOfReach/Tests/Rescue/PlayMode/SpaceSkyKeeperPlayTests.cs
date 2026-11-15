using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Presentation.Rescue;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.Rescue.PlayMode
{
    public sealed class SpaceSkyKeeperPlayTests
    {
        private readonly List<Object> owned = new List<Object>();
        private Material before;
        private bool wasEnabled;

        [SetUp]
        public void SetUp()
        {
            before = RenderSettings.skybox;
            wasEnabled = SpaceSkyKeeper.Enabled;
        }

        [TearDown]
        public void TearDown()
        {
            if (SpaceSkyKeeper.Instance) Object.DestroyImmediate(SpaceSkyKeeper.Instance.gameObject);
            SpaceSkyKeeper.Enabled = wasEnabled;
            RenderSettings.skybox = before;
            foreach (Object item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
        }

        private Material MakeSky(string starMapName, float exposure)
        {
            var map = new Texture2D(4, 2) { name = starMapName };
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "NasaDeepSky (test)" };
            sky.SetTexture("_MainTex", map);
            sky.SetFloat("_Exposure", exposure);
            owned.Add(map); owned.Add(sky);
            return sky;
        }

        [Test]
        public void It_does_not_install_itself_outside_the_mission_scene()
        {
            Assert.IsFalse(SpaceSkyKeeper.Instance, "test scenes keep their own sky: the existing rescue tests are unaffected");
        }

        [UnityTest]
        public IEnumerator The_saved_NASA_sky_is_shown_at_the_deep_view_without_editing_it()
        {
            Material sky = MakeSky(RescueEndingSky.NasaStarMapName, .15f);
            RenderSettings.skybox = sky;
            SpaceSkyKeeper keeper = SpaceSkyKeeper.Create();
            yield return null;
            yield return null;

            Assert.IsTrue(keeper.IsHolding);
            Assert.AreNotSame(sky, RenderSettings.skybox, "a runtime copy is shown");
            Assert.AreSame(sky, keeper.SavedSky);
            Assert.AreSame(sky.GetTexture("_MainTex"), RenderSettings.skybox.GetTexture("_MainTex"), "the same NASA star map");
            Assert.AreEqual(RescueEndingSky.DeepViewExposure, RenderSettings.skybox.GetFloat("_Exposure"), 1e-4f, "no ramp");
            Assert.AreEqual(.15f, sky.GetFloat("_Exposure"), 1e-6f, "the saved material is never edited");

            // Kill switch / QA toggle: the saved sky is back on the next frame, and up again when re-enabled.
            SpaceSkyKeeper.Enabled = false;
            yield return null;
            Assert.AreSame(sky, RenderSettings.skybox);
            Assert.IsFalse(keeper.IsHolding);
            SpaceSkyKeeper.Enabled = true;
            yield return null;
            Assert.IsTrue(keeper.IsHolding);
            Assert.AreEqual(RescueEndingSky.DeepViewExposure, RenderSettings.skybox.GetFloat("_Exposure"), 1e-4f);

            // Teardown of the owner puts the saved sky back.
            Object.DestroyImmediate(keeper.gameObject);
            Assert.AreSame(sky, RenderSettings.skybox);
        }

        [UnityTest]
        public IEnumerator Other_skies_are_left_alone_and_the_deep_view_follows_the_saved_sky_back()
        {
            Material sky = MakeSky(RescueEndingSky.NasaStarMapName, .15f);
            Material launchSite = MakeSky("launch site sky", .15f);
            RenderSettings.skybox = launchSite;
            SpaceSkyKeeper keeper = SpaceSkyKeeper.Create();
            yield return null;
            Assert.AreSame(launchSite, RenderSettings.skybox, "the launch site on Earth keeps its own sky");
            Assert.IsFalse(keeper.IsHolding);

            RenderSettings.skybox = null;
            yield return null;
            Assert.IsFalse(RenderSettings.skybox);
            Assert.IsFalse(keeper.IsHolding);

            // The departure assigns the saved sky for the orbit shot: the deep view goes up the same frame.
            RenderSettings.skybox = sky;
            yield return null;
            Assert.IsTrue(keeper.IsHolding);
            Material copy = RenderSettings.skybox;
            Assert.AreEqual(RescueEndingSky.DeepViewExposure, copy.GetFloat("_Exposure"), 1e-4f);

            // An environment restore that hands the copy back (it was captured while up) changes nothing.
            RenderSettings.skybox = copy;
            yield return null;
            Assert.AreSame(copy, RenderSettings.skybox);
            Assert.IsTrue(keeper.IsHolding);

            // Restart: the launch site sky again, untouched; then the saved sky again, deep view again.
            RenderSettings.skybox = launchSite;
            yield return null;
            Assert.AreSame(launchSite, RenderSettings.skybox);
            Assert.IsFalse(keeper.IsHolding);
            RenderSettings.skybox = sky;
            yield return null;
            Assert.IsTrue(keeper.IsHolding);
            Assert.AreEqual(.15f, sky.GetFloat("_Exposure"), 1e-6f);

            // A sky that is already at least as deep is left alone.
            Material deep = MakeSky(RescueEndingSky.NasaStarMapName, .6f);
            RenderSettings.skybox = deep;
            yield return null;
            Assert.AreSame(deep, RenderSettings.skybox);
            Assert.IsFalse(keeper.IsHolding);
        }

        [UnityTest]
        public IEnumerator The_rescue_ending_never_takes_the_sky_while_the_owner_holds_it()
        {
            Material sky = MakeSky(RescueEndingSky.NasaStarMapName, .15f);
            SpaceSkyKeeper keeper = SpaceSkyKeeper.Create();
            yield return null;

            // The saved sky is assigned and the ending starts in the same frame, before the owner's LateUpdate.
            RenderSettings.skybox = sky;
            var ending = new RescueEndingSky();
            SpaceSkyKeeper.HoldNow();   // what RescueEndingPresenter.TryBegin does first
            Material copy = RenderSettings.skybox;
            Assert.IsTrue(keeper.IsHolding);
            Assert.IsFalse(ending.Raise(), "already at the deep view: the ending creates no copy of its own");
            Assert.IsFalse(ending.IsRaised);
            Assert.AreSame(copy, RenderSettings.skybox);

            // Everything the ending does to its own sky afterwards leaves the owner's copy alone.
            ending.SetBlend(0f);
            ending.Keep(true);
            ending.Lower();
            ending.Keep(false);
            yield return null;
            Assert.AreSame(copy, RenderSettings.skybox);
            Assert.AreEqual(RescueEndingSky.DeepViewExposure, copy.GetFloat("_Exposure"), 1e-4f);
            Assert.IsTrue(keeper.IsHolding);
            ending.Dispose();
            Assert.AreSame(copy, RenderSettings.skybox);
        }
    }
}

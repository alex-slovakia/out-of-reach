using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Localization;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>LANG-01 through the real resolver: the focused prompt and an E-press refusal follow the chosen language.</summary>
    public sealed class LocalizationPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const float WalkwayZ = 6.5f;

        private RepairLoopHarness harness;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(preflight: true);
        }

        [TearDown]
        public void TearDown()
        {
            GameText.SetLanguage(GameLanguage.English);
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThePromptOnScreen_SwitchesLanguageAtOnce_AndRefusalsAreTranslated()
        {
            Vector3 console = RepairLoopHarness.ConsolePosition;
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.Console));
            string english = harness.Resolver.CurrentPrompt;
            Assert.That(english, Does.StartWith("Begin mission: "));

            GameText.SetLanguage(GameLanguage.Spanish);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo(GameText.Translate(english, GameLanguage.Spanish)), "refreshed on the change");
            Assert.That(harness.Resolver.CurrentPrompt, Is.Not.EqualTo(english));

            GameText.SetLanguage(GameLanguage.French);
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo(GameText.Translate(english, GameLanguage.French)));

            // A refusal through E: the only spectrometer is already aboard, so its pallet has nothing to pick up.
            string refusal = null;
            harness.Resolver.InteractionAttempted.AddListener((succeeded, explanation) => refusal = succeeded ? null : explanation);
            Vector3 pallet = RepairLoopHarness.PalletPosition(AuroraMission.Spectrometer);
            harness.PlacePlayerFacing(pallet.x, WalkwayZ, pallet);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(harness.CargoSupply[AuroraMission.Spectrometer]));
            string englishRefusal = harness.Session.Cargo.CheckSupply(AuroraMission.Spectrometer).Explanation;
            Assert.That(englishRefusal, Is.EqualTo("The only atmospheric spectrometer is already aboard"));
            harness.Input.PressInteract();
            yield return Frames(3);
            Assert.That(refusal, Is.EqualTo(GameText.Translate(englishRefusal, GameLanguage.French)));
            Assert.That(refusal, Is.Not.EqualTo(englishRefusal), "the refusal is shown in French");

            GameText.SetLanguage(GameLanguage.English);
            harness.PlacePlayerFacing(0f, console.z - 0.15f - 1.5f, console);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentPrompt, Is.EqualTo(english), "English again, unchanged");
        }
    }
}

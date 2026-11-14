using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Localization;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>LANG-01: English stays the source of truth; Spanish and French are shown where text is displayed.</summary>
    public sealed class LocalizationTests
    {
        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{\d+(?:,[^}:]*)?(?::[^}]*)?\}");

        [TearDown]
        public void TearDown()
        {
            GameText.SetLanguage(GameLanguage.English);
        }

        [Test]
        public void English_IsTheDefault_AndShowsTheSourceTextUnchanged()
        {
            Assert.That(GameText.Current, Is.EqualTo(GameLanguage.English));
            const string text = "Begin mission: 546 of 600 kg, 720 of 800 credits";
            Assert.That(GameText.Translate(text), Is.SameAs(text));
            Assert.That(GameText.Languages, Is.EqualTo(new[] { GameLanguage.English, GameLanguage.Spanish, GameLanguage.French }));
            Assert.That(GameText.NativeName(GameLanguage.Spanish), Is.EqualTo("Español"));
            Assert.That(GameText.NativeName(GameLanguage.French), Is.EqualTo("Français"));
            Assert.That(GameText.TryParseCode("FR", out GameLanguage french), Is.True);
            Assert.That(french, Is.EqualTo(GameLanguage.French));
            Assert.That(GameText.TryParseCode("xx", out GameLanguage none), Is.False);
            Assert.That(none, Is.EqualTo(GameLanguage.English));
        }

        [TestCase(GameLanguage.Spanish)]
        [TestCase(GameLanguage.French)]
        public void ExactText_RenderedNumbers_AndEmbeddedNames_AreTranslated(GameLanguage language)
        {
            const string standby = "Console on standby until launch";
            Assert.That(GameText.Translate(standby, language), Is.Not.EqualTo(standby));

            string begin = GameText.Translate("Begin mission: 546 of 600 kg, 720 of 800 credits", language);
            Assert.That(begin, Does.Not.StartWith("Begin mission"));
            foreach (string number in new[] { "546", "600", "720", "800" })
            {
                Assert.That(begin, Does.Contain(number), "numbers are kept as rendered");
            }

            string carrying = GameText.Translate("Carrying oxygen tank.", language);
            Assert.That(carrying, Does.Not.Contain("Carrying"));
            Assert.That(carrying, Does.Not.Contain("oxygen tank"), "the embedded equipment name is translated too");

            Assert.That(GameText.Translate(PlanningDeadline.LoadingClosedMessage, language), Is.Not.EqualTo(PlanningDeadline.LoadingClosedMessage));
        }

        [Test]
        public void UnknownText_FallsBackToEnglish_AndNullOrEmptyIsSafe()
        {
            const string unknown = "Zz42 is not a game sentence";
            Assert.That(GameText.Translate(unknown, GameLanguage.Spanish), Is.EqualTo(unknown));
            Assert.That(GameText.Translate(null, GameLanguage.French), Is.Null);
            Assert.That(GameText.Translate(string.Empty, GameLanguage.French), Is.Empty);
            Assert.That(GameText.Translate("546 / 600", GameLanguage.French), Is.EqualTo("546 / 600"));
        }

        [TestCase(GameLanguage.Spanish)]
        [TestCase(GameLanguage.French)]
        public void EveryCatalogEntry_KeepsItsPlaceholdersAndEdgeSpaces(GameLanguage language)
        {
            IReadOnlyList<KeyValuePair<string, string>> entries = GameText.Entries(language);
            Assert.That(entries.Count, Is.GreaterThan(400));
            foreach (KeyValuePair<string, string> entry in entries)
            {
                string[] source = Placeholder.Matches(entry.Key).Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                string[] target = Placeholder.Matches(entry.Value).Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                Assert.That(target, Is.EqualTo(source), "placeholders of: " + entry.Key);
                Assert.That(entry.Value.Length - entry.Value.TrimStart().Length, Is.EqualTo(entry.Key.Length - entry.Key.TrimStart().Length), "leading space of: " + entry.Key);
                Assert.That(entry.Value.Length - entry.Value.TrimEnd().Length, Is.EqualTo(entry.Key.Length - entry.Key.TrimEnd().Length), "trailing space of: " + entry.Key);
                Assert.That(entry.Value.Trim(), Is.Not.Empty, entry.Key);
            }

            Assert.That(GameText.Entries(GameLanguage.English), Is.Empty);
        }

        [Test]
        public void SetLanguage_NotifiesOncePerChange_AndRejectsUndefinedValues()
        {
            int changes = 0;
            Action handler = () => changes++;
            GameText.Changed += handler;
            try
            {
                GameText.SetLanguage(GameLanguage.French);
                GameText.SetLanguage(GameLanguage.French);
                GameText.SetLanguage(GameLanguage.Spanish);
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(GameText.Current, Is.EqualTo(GameLanguage.Spanish));
                Assert.Throws<ArgumentOutOfRangeException>(() => GameText.SetLanguage((GameLanguage)99));
                Assert.That(GameText.Current, Is.EqualTo(GameLanguage.Spanish));
            }
            finally
            {
                GameText.Changed -= handler;
            }
        }

        [TestCase(GameLanguage.Spanish)]
        [TestCase(GameLanguage.French)]
        public void EveryPromptAlongAMission_IsShownTranslated(GameLanguage language)
        {
            var untranslated = new SortedSet<string>(StringComparer.Ordinal);
            int checkedPrompts = 0;

            void Sample(MissionSession session)
            {
                foreach (IInteractable interactable in session.Interactions.All)
                {
                    string prompt = interactable.Prompt;
                    if (string.IsNullOrEmpty(prompt) || !Regex.IsMatch(prompt, "[A-Za-z]{3}"))
                    {
                        continue;
                    }

                    checkedPrompts++;
                    if (GameText.Translate(prompt, language) == prompt)
                    {
                        untranslated.Add(prompt);
                    }
                }
            }

            using (var session = new MissionSession(AuroraMission.CreateDefinition(), null, null, true, false, true))
            {
                Sample(session);
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Sample(session);
                Fixtures.TriggerImpact(session);
                Sample(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Sample(session);
                Fixtures.SuitUp(session);
                Sample(session);
                Fixtures.OpenOuterHatch(session);
                Sample(session);
                Fixtures.GoOutside(session);
                Sample(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Antenna);
                Fixtures.ReturnAndRepressurize(session);
                Sample(session);
                Fixtures.Transmit(session);
                Sample(session);
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Sample(session);
            }

            Assert.That(checkedPrompts, Is.GreaterThan(60));
            Assert.That(untranslated, Is.Empty, "untranslated prompts:\n" + string.Join("\n", untranslated));
        }

        [Test]
        public void TheReport_IsBuiltInTheChosenLanguage_AndRebuiltWhenItChanges()
        {
            using (MissionSession session = Fixtures.RecoveredSession())
            {
                MissionReport english = session.Report;
                Assert.That(english.Headline, Is.EqualTo("RECOVERY POSSIBLE"));

                GameText.SetLanguage(GameLanguage.Spanish);
                MissionReport spanish = session.Report;
                Assert.That(spanish, Is.Not.SameAs(english), "the cached report follows the language");
                Assert.That(spanish.Headline, Is.Not.EqualTo(english.Headline));
                Assert.That(spanish.Sections.Select(s => s.Title), Is.Not.EqualTo(english.Sections.Select(s => s.Title)));
                Assert.That(spanish.Sections.Select(s => s.Id), Is.EqualTo(english.Sections.Select(s => s.Id)), "section IDs stay stable");
                Assert.That(session.State.OutcomeReason, Is.EqualTo(english.OutcomeReason), "the state keeps the English text");

                GameText.SetLanguage(GameLanguage.English);
                Assert.That(session.Report.Headline, Is.EqualTo("RECOVERY POSSIBLE"));
            }
        }
    }
}

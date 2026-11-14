using System;
using System.Collections.Generic;
using System.Globalization;

namespace OutOfReach.Localization
{
    /// <summary>The languages a player can choose. English is the source language and the default.</summary>
    public enum GameLanguage
    {
        English = 0,
        Spanish = 1,
        French = 2,
    }

    /// <summary>
    /// LANG-01: the one place that turns the game's English text into the chosen language. Pure C#, no Unity
    /// dependency, so Core, Interaction, UI and presentation can all use it.
    /// <para>
    /// The game keeps writing English (every authority, test and log stays English); text is translated where it is
    /// shown. <see cref="Translate(string)"/> accepts an already rendered English string, including numbers: it tries
    /// an exact match, then the format patterns generated from the actual source strings (for example
    /// "Begin mission: {0:0} of {1:0} kg, ..."), translating embedded phrases recursively, then sentence by sentence,
    /// and falls back to the English text. It never throws for unknown text.
    /// </para>
    /// <para>
    /// Choosing and remembering the language is presentation's job (settings menu, PlayerPrefs): call
    /// <see cref="SetLanguage"/>; subscribers of <see cref="Changed"/> refresh their text.
    /// </para>
    /// </summary>
    public static class GameText
    {
        private static readonly GameLanguage[] AllLanguages = { GameLanguage.English, GameLanguage.Spanish, GameLanguage.French };
        private static GameTextTable spanish;
        private static GameTextTable french;

        /// <summary>The language text is shown in now. English by default.</summary>
        public static GameLanguage Current { get; private set; } = GameLanguage.English;

        /// <summary>Raised after <see cref="Current"/> changes.</summary>
        public static event Action Changed;

        /// <summary>Every selectable language, English first.</summary>
        public static IReadOnlyList<GameLanguage> Languages => AllLanguages;

        /// <summary>The language's own name for a picker: English, Español, Français.</summary>
        public static string NativeName(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Spanish: return "Español";
                case GameLanguage.French: return "Français";
                default: return "English";
            }
        }

        /// <summary>Two-letter code for saving the choice: en, es, fr.</summary>
        public static string Code(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Spanish: return "es";
                case GameLanguage.French: return "fr";
                default: return "en";
            }
        }

        /// <summary>Parses a saved two-letter code; false (and English) for anything else.</summary>
        public static bool TryParseCode(string code, out GameLanguage language)
        {
            foreach (GameLanguage candidate in AllLanguages)
            {
                if (string.Equals(Code(candidate), code, StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }

            language = GameLanguage.English;
            return false;
        }

        /// <summary>Switches the language and notifies <see cref="Changed"/>. Undefined values are rejected.</summary>
        public static void SetLanguage(GameLanguage language)
        {
            if (Array.IndexOf(AllLanguages, language) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown game language.");
            }

            if (language == Current)
            {
                return;
            }

            Current = language;
            Changed?.Invoke();
        }

        /// <summary>The rendered English text in the current language (the English text itself when untranslated).</summary>
        public static string Translate(string english)
        {
            return Translate(english, Current);
        }

        /// <summary>The rendered English text in the given language.</summary>
        public static string Translate(string english, GameLanguage language)
        {
            if (language == GameLanguage.English || string.IsNullOrEmpty(english))
            {
                return english;
            }

            GameTextTable table = Table(language);
            return table != null ? table.Translate(english) : english;
        }

        /// <summary>string.Format with the invariant culture over the translated format string (for new code).</summary>
        public static string Format(string englishFormat, params object[] args)
        {
            GameTextTable table = Current == GameLanguage.English ? null : Table(Current);
            string format = table != null ? table.TranslateFormat(englishFormat) : englishFormat;
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        /// <summary>Every catalog pair (English, translation) of a language, exact strings and formats; empty for English.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Entries(GameLanguage language)
        {
            GameTextTable table = language == GameLanguage.English ? null : Table(language);
            return table != null ? table.Entries : Array.Empty<KeyValuePair<string, string>>();
        }

        private static GameTextTable Table(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Spanish:
                    return spanish ?? (spanish = new GameTextTable(GameTextCatalog.SpanishExact, GameTextCatalog.SpanishFormats));
                case GameLanguage.French:
                    return french ?? (french = new GameTextTable(GameTextCatalog.FrenchExact, GameTextCatalog.FrenchFormats));
                default:
                    return null;
            }
        }
    }
}

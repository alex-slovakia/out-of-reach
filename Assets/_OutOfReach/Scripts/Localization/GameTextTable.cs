using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace OutOfReach.Localization
{
    /// <summary>
    /// One language's translations: exact strings, format strings and the patterns built from the formats.
    /// Results are cached; the cache is bounded because rendered text contains changing numbers.
    /// </summary>
    internal sealed class GameTextTable
    {
        private const int MaxDepth = 3;
        private const int CacheLimit = 4096;
        private static readonly Regex SentenceBreak = new Regex(@"(?<=[.!?])\s+(?=[A-Z0-9(])", RegexOptions.CultureInvariant);
        private static readonly Regex ListBreak = new Regex(@"(, |; )", RegexOptions.CultureInvariant);
        private static readonly string[] Articles = { "an ", "a ", "the " };

        private readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> formats = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<GameTextPattern> patterns = new List<GameTextPattern>();
        private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();

        internal GameTextTable(string[] exactPairs, string[] formatPairs)
        {
            for (int i = 0; i + 1 < exactPairs.Length; i += 2)
            {
                exact[exactPairs[i]] = exactPairs[i + 1];
                entries.Add(new KeyValuePair<string, string>(exactPairs[i], exactPairs[i + 1]));
            }

            for (int i = 0; i + 1 < formatPairs.Length; i += 2)
            {
                formats[formatPairs[i]] = formatPairs[i + 1];
                entries.Add(new KeyValuePair<string, string>(formatPairs[i], formatPairs[i + 1]));
                GameTextPattern pattern = GameTextPattern.TryCreate(formatPairs[i], formatPairs[i + 1]);
                if (pattern != null)
                {
                    patterns.Add(pattern);
                }
            }

            // Most specific first: a pattern anchored by leading text ("Repair {0}: {1}.") before one that starts with a
            // placeholder, then the longer literal text before a shorter, more general pattern.
            patterns.Sort((a, b) =>
            {
                int anchored = b.StartsWithLiteral.CompareTo(a.StartsWithLiteral);
                return anchored != 0 ? anchored : b.LiteralLength.CompareTo(a.LiteralLength);
            });
        }

        internal IReadOnlyList<KeyValuePair<string, string>> Entries => entries;

        internal string TranslateFormat(string englishFormat)
        {
            return englishFormat != null && formats.TryGetValue(englishFormat, out string translated) ? translated : englishFormat;
        }

        internal string Translate(string text)
        {
            return Translate(text, 0);
        }

        private string Translate(string text, int depth)
        {
            if (string.IsNullOrEmpty(text) || !HasLetters(text))
            {
                return text;
            }

            if (cache.TryGetValue(text, out string cached))
            {
                return cached;
            }

            string result = TranslateUncached(text, depth);
            if (cache.Count >= CacheLimit)
            {
                cache.Clear();
            }

            cache[text] = result;
            return result;
        }

        private string TranslateUncached(string text, int depth)
        {
            if (exact.TryGetValue(text, out string direct))
            {
                return direct;
            }

            // Surrounding whitespace belongs to the caller's layout, not to the phrase.
            string trimmed = text.Trim();
            if (trimmed.Length > 0 && trimmed.Length != text.Length && exact.TryGetValue(trimmed, out string inner))
            {
                int start = text.IndexOf(trimmed, StringComparison.Ordinal);
                return text.Substring(0, start) + inner + text.Substring(start + trimmed.Length);
            }

            // Names change case with their position ("Carrying oxygen tank.", "Power distribution: repaired"):
            // match the entry with the other first-letter case and give the translation the case of the text.
            if (char.IsLower(text[0]) && exact.TryGetValue(char.ToUpperInvariant(text[0]) + text.Substring(1), out string capitalized)
                && capitalized.Length > 0)
            {
                return char.ToLowerInvariant(capitalized[0]) + capitalized.Substring(1);
            }

            if (char.IsUpper(text[0]) && exact.TryGetValue(char.ToLowerInvariant(text[0]) + text.Substring(1), out string lowered)
                && lowered.Length > 0)
            {
                return char.ToUpperInvariant(lowered[0]) + lowered.Substring(1);
            }

            // Some source strings are appended fragments with a leading space (" The battery ran flat..."): once the
            // text is split into sentences the space is gone, so match the entry with it.
            if (exact.TryGetValue(" " + text, out string spaced))
            {
                return spaced.TrimStart(' ');
            }

            Func<string, string> embedded = null;
            if (depth < MaxDepth)
            {
                embedded = value => Translate(value, depth + 1);
            }

            foreach (GameTextPattern pattern in patterns)
            {
                if (pattern.TryApply(text, embedded, out string translated))
                {
                    return translated;
                }
            }

            // An embedded English article ("carrying a battery module") cannot be mapped to a gendered one:
            // show the translated noun phrase without it.
            if (depth > 0)
            {
                foreach (string article in Articles)
                {
                    if (text.Length > article.Length && text.StartsWith(article, StringComparison.Ordinal))
                    {
                        string rest = text.Substring(article.Length);
                        string translatedRest = Translate(rest, depth + 1);
                        if (!string.Equals(translatedRest, rest, StringComparison.Ordinal))
                        {
                            return translatedRest;
                        }
                    }
                }
            }

            if (depth < MaxDepth)
            {
                string[] sentences = SentenceBreak.Split(text);
                if (sentences.Length > 1)
                {
                    // Longest run of sentences first: a two-sentence source message followed by an appended sentence
                    // is still found as one message plus the extra sentence.
                    bool changed = false;
                    var builder = new StringBuilder(text.Length + 16);
                    int i = 0;
                    while (i < sentences.Length)
                    {
                        int taken = 1;
                        string piece = sentences[i];
                        for (int end = sentences.Length; end > i; end--)
                        {
                            string run = string.Join(" ", sentences, i, end - i);
                            if (end - i == sentences.Length)
                            {
                                continue;
                            }

                            string translatedRun = Translate(run, depth + 1);
                            if (!string.Equals(translatedRun, run, StringComparison.Ordinal))
                            {
                                piece = translatedRun;
                                taken = end - i;
                                changed = true;
                                break;
                            }
                        }

                        if (builder.Length > 0)
                        {
                            builder.Append(' ');
                        }

                        builder.Append(piece);
                        i += taken;
                    }

                    if (changed)
                    {
                        return builder.ToString();
                    }
                }

                // A list built by the game ("2 spare parts, 1 kWh"; "water 2.5 L needed; food ..."): item by item.
                string[] items = ListBreak.Split(text);
                if (items.Length > 1)
                {
                    bool changed = false;
                    var builder = new StringBuilder(text.Length + 16);
                    foreach (string item in items)
                    {
                        bool separator = item == ", " || item == "; ";
                        string piece = separator ? item : Translate(item, depth + 1);
                        changed |= !separator && !string.Equals(piece, item, StringComparison.Ordinal);
                        builder.Append(piece);
                    }

                    if (changed)
                    {
                        return builder.ToString();
                    }
                }
            }

            return text;
        }

        internal static bool HasLetters(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A .NET format string turned into an anchored pattern: "Begin mission: {0:0} of {1:0} kg" matches the rendered
    /// "Begin mission: 546 of 600 kg" and rebuilds the translated format with the captured values. Captured text
    /// that contains words is translated recursively (an embedded phrase); numbers are kept as rendered.
    /// </summary>
    internal sealed class GameTextPattern
    {
        private readonly Regex regex;
        private readonly string prefix;
        private readonly List<Token> output;

        private GameTextPattern(Regex regex, string prefix, List<Token> output, int literalLength)
        {
            this.regex = regex;
            this.prefix = prefix;
            this.output = output;
            LiteralLength = literalLength;
        }

        internal int LiteralLength { get; }

        /// <summary>True when the pattern begins with literal text: it cannot swallow an unrelated leading clause.</summary>
        internal bool StartsWithLiteral => prefix.Length > 0;

        internal static GameTextPattern TryCreate(string englishFormat, string translatedFormat)
        {
            List<Token> source = Parse(englishFormat);
            List<Token> target = Parse(translatedFormat);
            if (source == null || target == null)
            {
                return null;
            }

            var seen = new HashSet<int>();
            var builder = new StringBuilder("^");
            int literalLetters = 0;
            int literalLength = 0;
            string previousLiteral = string.Empty;
            foreach (Token token in source)
            {
                if (token.IsPlaceholder)
                {
                    // A numeric format ({0:0}, {1:#,0.0}) only ever renders a number, so it only captures one: a short
                    // pattern like "{0:0} spare part{1}" can then not swallow a longer sentence. A placeholder glued to a
                    // word ("part{1}", "remain{3}") is an ending such as a plural "s": letters only. Other placeholders
                    // may render anything, including nothing (an optional warning).
                    bool suffix = previousLiteral.Length > 0 && char.IsLetter(previousLiteral[previousLiteral.Length - 1]);
                    string capture = token.IsNumeric ? "[-+]?[0-9][0-9.,]*" : suffix ? "[A-Za-z]*" : ".*?";
                    builder.Append(seen.Add(token.Index) ? "(?<p" + token.Index + ">" + capture + ")" : "\\k<p" + token.Index + ">");
                    previousLiteral = string.Empty;
                }
                else
                {
                    builder.Append(Regex.Escape(token.Text));
                    previousLiteral = token.Text;
                    literalLength += token.Text.Length;
                    foreach (char c in token.Text)
                    {
                        if (char.IsLetter(c))
                        {
                            literalLetters++;
                        }
                    }
                }
            }

            builder.Append('$');
            if (seen.Count == 0 || literalLetters < 3)
            {
                return null;
            }

            foreach (Token token in target)
            {
                if (token.IsPlaceholder && !seen.Contains(token.Index))
                {
                    return null;
                }
            }

            string prefix = !source[0].IsPlaceholder ? source[0].Text : string.Empty;
            var regex = new Regex(builder.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline);
            return new GameTextPattern(regex, prefix, target, literalLength);
        }

        internal bool TryApply(string text, Func<string, string> embedded, out string translated)
        {
            translated = null;
            if (prefix.Length > 0 && !text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            Match match = regex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            var builder = new StringBuilder(text.Length + 16);
            foreach (Token token in output)
            {
                if (!token.IsPlaceholder)
                {
                    builder.Append(token.Text);
                    continue;
                }

                string value = match.Groups["p" + token.Index].Value;
                if (embedded != null && GameTextTable.HasLetters(value))
                {
                    value = embedded(value);
                }

                builder.Append(value);
            }

            translated = builder.ToString();
            return true;
        }

        /// <summary>Splits a .NET format string into literal text and placeholders; null when it is malformed.</summary>
        private static List<Token> Parse(string format)
        {
            var tokens = new List<Token>();
            var literal = new StringBuilder();
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i];
                if (c == '{' && i + 1 < format.Length && format[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }

                if (c == '}')
                {
                    return null;
                }

                if (c != '{')
                {
                    literal.Append(c);
                    i++;
                    continue;
                }

                int close = format.IndexOf('}', i);
                if (close < 0)
                {
                    return null;
                }

                string body = format.Substring(i + 1, close - i - 1);
                int end = 0;
                while (end < body.Length && char.IsDigit(body[end]))
                {
                    end++;
                }

                if (end == 0 || (end < body.Length && body[end] != ':' && body[end] != ','))
                {
                    return null;
                }

                if (literal.Length > 0)
                {
                    tokens.Add(Token.Literal(literal.ToString()));
                    literal.Clear();
                }

                int colon = body.IndexOf(':');
                string spec = colon >= 0 ? body.Substring(colon + 1) : string.Empty;
                tokens.Add(Token.Placeholder(int.Parse(body.Substring(0, end), System.Globalization.CultureInfo.InvariantCulture), IsNumericSpec(spec)));
                i = close + 1;
            }

            if (literal.Length > 0)
            {
                tokens.Add(Token.Literal(literal.ToString()));
            }

            return tokens.Count > 0 ? tokens : null;
        }

        /// <summary>A custom numeric format made only of 0 # . , (for example 0, 0.##, #,0.0, 00) renders digits only.</summary>
        private static bool IsNumericSpec(string spec)
        {
            if (string.IsNullOrEmpty(spec))
            {
                return false;
            }

            foreach (char c in spec)
            {
                if (c != '0' && c != '#' && c != '.' && c != ',')
                {
                    return false;
                }
            }

            return true;
        }

        private struct Token
        {
            internal bool IsPlaceholder;
            internal bool IsNumeric;
            internal int Index;
            internal string Text;

            internal static Token Literal(string text)
            {
                return new Token { IsPlaceholder = false, Text = text };
            }

            internal static Token Placeholder(int index, bool numeric)
            {
                return new Token { IsPlaceholder = true, Index = index, IsNumeric = numeric };
            }
        }
    }
}

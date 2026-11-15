using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OutOfReach.Presentation.Audio
{
    /// <summary>Readable caption chunks distributed over a clip; not word-level alignment.</summary>
    public sealed class SubtitleSegments
    {
        private readonly string[] chunks;
        private readonly float[] ends;
        public int Count => chunks.Length;

        public SubtitleSegments(string transcript)
        {
            string text = Regex.Replace(transcript ?? string.Empty, @"\s+", " ").Trim();
            var result = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '.' && text[i] != '?' && text[i] != '!') continue;
                int end = i + 1;
                while (end < text.Length && (text[end] == '.' || text[end] == '?' || text[end] == '!'
                    || text[end] == '"' || text[end] == '\u201d' || text[end] == '\'')) end++;
                if (end < text.Length && text[end] != ' ') continue;
                if (text[i] == '.' && IsAbbreviation(text, start, i)) continue;
                AddReadable(result, text.Substring(start, end - start));
                start = end;
                while (start < text.Length && text[start] == ' ') start++;
                i = start - 1;
            }
            if (start < text.Length) AddReadable(result, text.Substring(start));
            chunks = result.ToArray();
            ends = new float[chunks.Length];
            float total = 0;
            for (int i = 0; i < chunks.Length; i++)
            {
                total += chunks[i].Length + 12f;
                ends[i] = total;
            }
            if (total > 0) for (int i = 0; i < ends.Length; i++) ends[i] /= total;
        }

        public string At(float normalizedPlayback)
        {
            if (chunks.Length == 0) return string.Empty;
            if (float.IsNaN(normalizedPlayback) || normalizedPlayback <= 0) return chunks[0];
            for (int i = 0; i < ends.Length; i++)
                if (normalizedPlayback < ends[i]) return chunks[i];
            return chunks[chunks.Length - 1];
        }

        private static bool IsAbbreviation(string text, int start, int dot)
        {
            int begin = dot;
            while (begin > start && text[begin - 1] != ' ') begin--;
            string token = text.Substring(begin, dot - begin + 1);
            return token == "Mr." || token == "Mrs." || token == "Ms." || token == "Dr."
                || token == "Prof." || token == "Cmdr." || token == "Lt." || token == "e.g." || token == "i.e."
                || (token.Length > 2 && token.IndexOf('.') < token.Length - 1);
        }

        private static void AddReadable(List<string> result, string sentence)
        {
            sentence = sentence.Trim();
            const int maximumCharacters = 96;
            while (sentence.Length > maximumCharacters)
            {
                int split = sentence.LastIndexOfAny(new[] { ',', ';', ':' }, maximumCharacters - 1);
                if (split < 36) split = sentence.LastIndexOf(' ', maximumCharacters);
                else split++;
                if (split <= 0) break; // Keep an unusually long single word intact.
                result.Add(sentence.Substring(0, split).Trim());
                sentence = sentence.Substring(split).Trim();
            }
            if (sentence.Length > 0) result.Add(sentence);
        }
    }
}

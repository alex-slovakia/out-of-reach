using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.Radio
{
    public enum RadioLineKind { Receive, Transmit }

    /// <summary>One spoken radio line: who speaks, the subtitle and whether the player keys the PTT.</summary>
    public readonly struct RadioLine
    {
        public RadioLine(string speaker, string text, RadioLineKind kind, string cueId = null, float? seconds = null)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            Kind = kind;
            CueId = cueId;
            // Subtitle reading time (about 17 characters per second plus 0.6 s) with a floor for short calls;
            // a recorded voice line uses its clip length instead.
            Seconds = seconds ?? Math.Max(2f, .058f * Text.Length + .6f);
        }

        public string Speaker { get; }
        public string Text { get; }
        public RadioLineKind Kind { get; }
        /// <summary>Voice cue id (Audio/Voices/...), or null for a subtitle-and-radio-sound line.</summary>
        public string CueId { get; }
        public float Seconds { get; }
        public RadioLine WithSeconds(float seconds) => new RadioLine(Speaker, Text, Kind, CueId, seconds);

        /// <summary>Lines up to this length are shown whole.</summary>
        public const int WholeLineCharacters = 90;
        /// <summary>
        /// Longest chunk of a split line. One centred HUD line of this length ends left of the chapter strip in
        /// the lower right corner, which otherwise covers the end of the subtitle.
        /// </summary>
        public const int ChunkCharacters = 52;

        /// <summary>
        /// Subtitle shown at <paramref name="t"/> seconds into the line: long lines are split at sentence ends into
        /// readable chunks (max <see cref="ChunkCharacters"/> characters) timed in proportion to their length.
        /// </summary>
        public string SubtitleAt(float t)
        {
            if (Text.Length <= WholeLineCharacters) return Text;
            var chunks = new List<string>();
            string current = string.Empty;
            foreach (string sentence in SplitSentences(Text))
            {
                if (current.Length > 0 && current.Length + 1 + sentence.Length > ChunkCharacters) { chunks.Add(current); current = sentence; }
                else current = current.Length == 0 ? sentence : current + " " + sentence;
            }
            if (current.Length > 0) chunks.Add(current);
            int total = 0; foreach (string c in chunks) total += c.Length;
            float elapsed = Math.Max(0f, Math.Min(1f, t / Math.Max(.01f, Seconds))) * total;
            int run = 0;
            foreach (string c in chunks) { run += c.Length; if (elapsed < run) return c; }
            return chunks[chunks.Count - 1];
        }

        private static IEnumerable<string> SplitSentences(string text)
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                bool end = (text[i] == '.' || text[i] == '?' || text[i] == '!') && (i + 1 == text.Length || text[i + 1] == ' ');
                if (!end) continue;
                // Keep "Seven..." style ellipses inside the sentence.
                if (text[i] == '.' && i + 1 < text.Length && i > 0 && text[i - 1] == '.') continue;
                yield return text.Substring(start, i + 1 - start).Trim();
                start = i + 1;
            }
            if (start < text.Length && text.Substring(start).Trim().Length > 0) yield return text.Substring(start).Trim();
        }
    }

    /// <summary>A short first-person radio scene: an optional incoming-call chirp, then lines.</summary>
    public sealed class RadioBeat
    {
        public RadioBeat(string id, bool incomingCall, params RadioLine[] lines)
        {
            Id = id; IncomingCall = incomingCall; Lines = lines ?? Array.Empty<RadioLine>();
        }

        public string Id { get; }
        public bool IncomingCall { get; }
        public IReadOnlyList<RadioLine> Lines { get; }

        /// <summary>Copy whose lines with a recorded voice last as long as that recording.</summary>
        public RadioBeat WithVoiceLengths(Func<string, float?> clipSeconds)
        {
            var lines = new RadioLine[Lines.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                RadioLine line = Lines[i];
                float? length = line.CueId != null ? clipSeconds(line.CueId) : null;
                lines[i] = length.HasValue ? line.WithSeconds(length.Value + .35f) : line;
            }
            return new RadioBeat(Id, IncomingCall, lines);
        }
    }

    /// <summary>
    /// STORY-RADIO-01 script (Alex's story, issue #7 6896895693). Mission fiction only: no real programme, crew or
    /// historic callsigns. The Earth reply never carries a time: Core decides when it arrives (light time).
    /// </summary>
    public static class RadioBeats
    {
        /// <summary>Boarding AURORA-7 after the transfer (Alex: "when you board Aurora"; the opening stays untouched).</summary>
        public const string AuroraBoarding = "radio.aurora-boarding";
        /// <summary>Alternative placement: radio check at the transport hatch on the pad, before seat E.</summary>
        public const string PadBoarding = "radio.pad-boarding";
        public const string DistressCall = "radio.distress-call";
        public const string EarthReplyDay14 = "radio.earth-reply-day14";
        public const string Day14Call = "radio.day14-call";
        /// <summary>
        /// Voiced Day-14 announcement at the console. The voice is the day-neutral cut of Codex's approved vo23
        /// (Voices/PROVENANCE.md): the sentence naming the new rendezvous day is removed, so the line never
        /// contradicts Core's schedule.
        /// </summary>
        public const string Day14Announcement = "radio.day14-announcement";
        public const string RescueLostCue = "vo23_mc_rescue_lost_dayneutral";

        public const string PadSpeaker = "PAD CONTROL · RADIO";
        public const string PlayerSpeaker = "YOU · RADIO";
        public const string EarthSpeaker = "MISSION CONTROL · EARTH · RADIO";
        public const string ControlSpeaker = "MISSION CONTROL · RADIO";

        private static readonly Dictionary<string, RadioBeat> Beats = new Dictionary<string, RadioBeat>(StringComparer.Ordinal)
        {
            [AuroraBoarding] = new RadioBeat(AuroraBoarding, true,
                new RadioLine(ControlSpeaker, "Aurora Seven, Mission Control. We show you aboard. Radio check, how do you read?", RadioLineKind.Receive),
                new RadioLine(PlayerSpeaker, "Loud and clear, Control. Aboard and floating. Starting cockpit checks.", RadioLineKind.Transmit)),
            [PadBoarding] = new RadioBeat(PadBoarding, false,
                new RadioLine(PadSpeaker, "Aurora, Pad. Radio check before boarding. How copy?", RadioLineKind.Receive),
                new RadioLine(PlayerSpeaker, "Pad, Aurora. Loud and clear. Boarding now.", RadioLineKind.Transmit),
                new RadioLine(PadSpeaker, "Copy, Aurora. Strap in for the count. Pad out.", RadioLineKind.Receive)),
            [DistressCall] = new RadioBeat(DistressCall, false,
                new RadioLine(PlayerSpeaker, "Mayday, mayday. Earth, this is Aurora. Debris strike. Antenna repaired, crew alive.", RadioLineKind.Transmit),
                new RadioLine(PlayerSpeaker, "Requesting rescue. Aurora standing by for your reply. Over.", RadioLineKind.Transmit)),
            [EarthReplyDay14] = new RadioBeat(EarthReplyDay14, true,
                new RadioLine(EarthSpeaker, "Aurora, Earth. We hear you. Good work on that antenna.", RadioLineKind.Receive),
                new RadioLine(EarthSpeaker, "A rescue rocket is being readied. Expect rescue on day fourteen. Ration carefully. Earth out.", RadioLineKind.Receive)),
            [Day14Call] = new RadioBeat(Day14Call, true,
                new RadioLine(EarthSpeaker, "Aurora, Earth. Priority traffic. Go to the console, Aurora.", RadioLineKind.Receive)),
            // Transcript exactly as spoken by the day-neutral cut (Voices/vo23_mc_rescue_lost_dayneutral.txt): the
            // approved recording without "New rendezvous: mission day thirty-one.".
            [Day14Announcement] = new RadioBeat(Day14Announcement, true,
                new RadioLine(ControlSpeaker, "Aurora Seven... Mission Control. The rescue vehicle has crashed. We lost it on final approach. "
                    + "I'm sorry. A second craft is already in transit. "
                    + "Conserve your supplies. We are not giving up on you.", RadioLineKind.Receive, RescueLostCue)),
        };

        /// <summary>The Day-14 announcement belongs to the day of the message: one simulated day after its receipt.</summary>
        public const double AnnouncementWindowSeconds = 86400d;

        /// <summary>
        /// A boarding radio check that could not start before the mission moved on is stale and is dropped instead
        /// of played late: "we show you aboard" must never follow the impact. Other beats do not go stale here.
        /// </summary>
        public static bool IsStale(string beatId, GamePhase phase)
        {
            if (beatId == AuroraBoarding) return phase != GamePhase.Intro;
            if (beatId == PadBoarding) return phase != GamePhase.Planning;
            return false;
        }

        /// <summary>True while a console use may still play the announcement of a message received at that time.</summary>
        public static bool IsAnnouncementFresh(double receivedAtSimulationSeconds, double nowSimulationSeconds)
            => nowSimulationSeconds - receivedAtSimulationSeconds <= AnnouncementWindowSeconds;

        public static IEnumerable<string> Ids => Beats.Keys;

        public static bool TryGet(string id, out RadioBeat beat)
        {
            beat = null;
            return !string.IsNullOrEmpty(id) && Beats.TryGetValue(id, out beat);
        }
    }
}

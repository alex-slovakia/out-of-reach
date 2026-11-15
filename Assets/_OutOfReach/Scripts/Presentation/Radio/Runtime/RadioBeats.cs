using System;
using System.Collections.Generic;

namespace OutOfReach.Presentation.Radio
{
    public enum RadioLineKind { Receive, Transmit }

    /// <summary>One spoken radio line: who speaks, the subtitle and whether the player keys the PTT.</summary>
    public readonly struct RadioLine
    {
        public RadioLine(string speaker, string text, RadioLineKind kind)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            Kind = kind;
            // Subtitle reading time (about 17 characters per second plus 0.6 s) with a floor for short calls.
            Seconds = Math.Max(2f, .058f * Text.Length + .6f);
        }

        public string Speaker { get; }
        public string Text { get; }
        public RadioLineKind Kind { get; }
        public float Seconds { get; }
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
        };

        public static IEnumerable<string> Ids => Beats.Keys;

        public static bool TryGet(string id, out RadioBeat beat)
        {
            beat = null;
            return !string.IsNullOrEmpty(id) && Beats.TryGetValue(id, out beat);
        }
    }
}

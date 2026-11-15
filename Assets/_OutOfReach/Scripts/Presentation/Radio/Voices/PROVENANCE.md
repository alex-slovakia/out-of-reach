# vo23_mc_rescue_lost_dayneutral.wav: provenance

Derived from the user-approved take. **No new generated audio**: every sample comes from the approved recording.

| | |
|---|---|
| Source | `Assets/_OutOfReach/Audio/Voices/Story02/vo23_mc_rescue_lost.wav` (unchanged, still listed in `approved-voices.json`) |
| Source SHA-256 | `806b2c29fd0eb3bb7b4676fb7183af5b253de6e613f089b3c4384c8d38876303` |
| Source format | 24 kHz, mono, 16-bit PCM, 21.080 s (505 920 samples) |
| Derived SHA-256 | `44a8e04343cdb3b4fee668b182ea55157e7623140098c4a8b3d9bb9843377b7b` |
| Derived format | 24 kHz, mono, 16-bit PCM, 17.980 s (431 520 samples) |
| Tool | ffmpeg 9.0.2 (gyan.dev full build), filters `atrim` + `acrossfade` only |
| Date | 15 November 2026 |

## Why

The approved take says "New rendezvous: mission day thirty-one." while Core has the second rescue on day 30
(`Story02/README.md`, "Unresolved date"). No schedule or Core change is authorized, so this cut removes that ONE
sentence and nothing else. The spoken line then names no date.

## The cut

Kept source ranges: `0.000 - 13.920 s` and `16.980 - 21.080 s`, joined with a 40 ms triangular crossfade
(derived `13.880 - 13.920 s` mixes source `13.880 - 13.920 s` with `16.980 - 17.020 s`). Both cut points sit in the
silence between sentences (room tone only). The removed sentence spans source `14.07 - 16.40 s`.

```
ffmpeg -i vo23_mc_rescue_lost.wav -filter_complex "[0:a]atrim=0:13.92,asetpts=PTS-STARTPTS[a];[0:a]atrim=16.98,asetpts=PTS-STARTPTS[b];[a][b]acrossfade=d=0.04:c1=tri:c2=tri[out]" -map "[out]" -ar 24000 -ac 1 -c:a pcm_s16le -fflags +bitexact -flags:a +bitexact -map_metadata -1 vo23_mc_rescue_lost_dayneutral.wav
```

## Measured (sample-level check against the source)

- Derived `[0, 13.880 s)` is bit-identical to the source; derived from `13.920 s` on is bit-identical to the source from `17.020 s` on.
- The 40 ms crossfade holds room tone only: peak -58.9 dBFS, RMS -67.7 dBFS.
- Largest sample-to-sample step within 10 ms of the seam: 15 LSB (-66.8 dBFS); the untouched source pause has 8 LSB.
- Pause across the cut ("...in transit." to "Conserve..."): 1.00 s. The source's own sentence pauses: 0.85 - 1.47 s.

## Transcript (subtitles use exactly this text)

See `vo23_mc_rescue_lost_dayneutral.txt`.

## Approval

The source take is approved by Alex (15 November 2026). This derived cut is **pending Alex's approval**. If it is
rejected: clear the `voiceClips` entry on "Handheld radio V01" (or delete this WAV and re-run the installer); the
Day-14 announcement then plays as subtitles with the radio sound only.

---

# vo30 - vo40: AI-generated radio takes (15 November 2026)

Alex asked for the radio lines that had no voice, and for a fresh Day-14 crash take. These eleven clips are
**synthetic speech generated with Google Gemini TTS** (`gemini-3.8-flash-lite-tts`); no human recording is used.
`voices.json` lists every clip with transcript, voice, model, style prompt, SHA-256, duration and levels.

- **Casting:** Orus for Mission Control, Earth and Pad Control (the voice of the approved Clean, Story02 and
  Departure masters). Iapetus for the player: the project had no player voice before.
- **Processing:** none. Each WAV is the exact generated master (24 kHz, mono, 16-bit PCM, dry).
  `HandheldRadioPresenter` band-limits the voice at runtime (high-pass 300 Hz, low-pass 3400 Hz, distortion 0.12).
- **Day-14 announcement:** `vo40_mc_rescue_lost_day14` speaks exactly the subtitle text, so it names no rendezvous
  day. It replaces the day-neutral cut above as the bound clip; the cut and the approved vo23 master stay in the
  repository, unbound.
- **Review state:** every transcript is machine-checked word for word (`transcript_audit` in `voices.json`); no clip
  has been listened to by a human (`listening_audited: false`). The Day-14 take is the second one: the first was
  transcribed as "is crashed" and discarded.
  Delete a WAV and re-run the installer to fall back to subtitles with the radio sound only for that line.
- **vo41 - vo45 (preflight briefing, added later the same evening):** five Pad Control lines for the beat
  `radio.preflight-briefing`, same model, voice (Orus) and handling. The spoken limit is Core's
  `AuroraMission.MassLimitKg` (600 kg).

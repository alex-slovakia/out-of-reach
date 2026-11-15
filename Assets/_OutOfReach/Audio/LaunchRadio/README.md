# Launch radio countdown (AUDIO-OPENING)

`launch-radio-countdown.wav` is the radio countdown heard in the departure cutscene. It is cut from NASA's
official public-domain Apollo 11 launch countdown and aligned to the existing departure timeline
(`TransportDepartureTimeline`: ignition 9.0 s, liftoff 10.5 s, climb cut 13.25 s, upper air 17.5 s, orbit 22.0 s).
Nothing in the speech is re-timed, pitched, stretched or synthesized.

## Source and credit

- **Credit: NASA.** Apollo 11 launch commentary, NASA public-domain audio. NASA audio is not protected by copyright
  in the United States (NASA media usage guidelines). Its use does not imply NASA endorsement.
- File: <https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3>,
  downloaded 15 Nov 2026, **400,398 bytes**, SHA-256 `9f14d326e95356e096b181c807136dd460f5a30afae02d55135e84d5e3c0a6f0`,
  MP3 128 kbps 44.1 kHz, **24.759 s**. The download is kept outside the repository; the build script refuses any
  other bytes.
- Transcript: <https://www.nasa.gov/wp-content/uploads/2015/03/apollo-11_day-01-highlights.pdf> (PAO, launch):
  "... 12, 11, 10, 9. Ignition sequence starts. 6, 5, 4, 3, 2, 1, 0. All engines running. Liftoff. We have a
  liftoff, 32 minutes past the hour, liftoff on Apollo 11. Tower cleared."

## What the official file actually contains

The file **starts at "Ignition sequence start"**. Spoken 12, 11, 10 and 9 (and the omitted 8, 7) are **not** in it.
Its order: "Ignition sequence start. 6, 5, 4, 3, 2, 1, 0. All engine(s) running. Liftoff! We have a liftoff,
32 minutes past the hour. Liftoff on Apollo 11." crowd noise, "Tower cleared." and the band-limited spacecraft
call "We got a roll program."

Word identities were cross-checked with the official transcript and a local Whisper large-v3 pass. **Every time
below comes from the waveform**: a 10 ms speech-band envelope (250-3500 Hz), an autocorrelation pitch track and
spectrograms. The Whisper word times were 0.3-0.9 s early and were not used.

## Cut and alignment

| Segment | Source seconds | Content | Departure seconds |
|---|---|---|---|
| A | 0.08-13.80 | "Ignition sequence start ... We have a liftoff." (uncut inside) | 1.82-15.54 |
| omitted | 13.80-21.36 | "32 minutes past the hour. Liftoff on Apollo 11." + crowd (names Apollo 11) | - |
| B | 21.36-24.60 | "Tower cleared. We got a roll program." (uncut inside) | 16.24-19.48 |

Alignment rule: the onset of "zero" (source 8.66 s) is at **10.40 s**, 0.1 s before the vehicle starts to move
(10.5 s). "Tower cleared" starts at 16.30 s, when the vehicle is far above the tower in the climb shot.

| Departure s | Radio | Picture |
|---|---|---|
| 0.00 | (flight control TTS "Secure for departure." keeps playing) | seat |
| 1.87 | Ignition sequence start. | seat |
| 4.36 / 5.29 / 6.31 / 7.20 / 8.29 | Six / Five / Four / Three / Two | hatch, access arm |
| 9.26 | One. | ignition 9.0 (shake starts) |
| **10.40** | **Zero.** | **liftoff 10.5** |
| 11.26 | All engines running. | pad shot |
| **13.19** | **Liftoff!** | **climb cut 13.25** |
| 14.26 | We have a liftoff. | climb |
| 16.30 | Tower cleared. | climb |
| 17.85 | We got a roll program. | upper atmosphere 17.5 |
| 19.48 | radio ends | orbit cut 22.0 |

The flight-control TTS lines that used to play at 3, 6, 9 and 10.5 s would talk over these numbers and are no
longer called. The numeric UI may still show 8 and 7; they are simply not spoken.

## Processing

High-pass 280 Hz and low-pass 3.5 kHz (4-pole each), a narrow notch at the recording's steady **2079.5 Hz** whistle,
+2.5 dB at 1.8 kHz for intelligibility, gentle 3:1 compression, 30-120 ms raised-cosine fades at the two cuts,
normalized to **-18 LUFS** (the departure TTS lines measure -18.7 to -19.2 LUFS), peak -5.7 dBFS. 48 kHz mono PCM16.

In game the score ducks **-5 dB** under the radio (80 ms attack, 650 ms release). With the runtime gains the speech
band stays 17-20 dB above the ducked score.

## Files

- `launch-radio-countdown.wav` - the asset (17.66 s). `launch-radio-cues.json` - every cut, offset and word time.
- `Editor/build_launch_radio.py` - deterministic rebuild:
  `python -I build_launch_radio.py --source <official mp3> --out <dir> --score <opening master wav>`.
- `Previews/opening-departure-mixdown-v01.mp3` - offline listening mix of departure -1.0 to 35.5 s (departure 0 s
  is at 1.0 s in the file): the approved opening score from 49.90 s, this radio, the runtime duck and the 3.5 s end
  fade. It contains no engine/cabin SFX and is never used at runtime.

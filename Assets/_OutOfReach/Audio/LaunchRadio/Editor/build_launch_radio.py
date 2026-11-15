"""Builds the OUT OF REACH launch radio countdown from NASA's official public-domain Apollo 11 countdown file.

Reproducible and offline after the one official download. Requires Python 3 + NumPy and FFmpeg on PATH.

  python -I build_launch_radio.py --source <590320main_ringtone_apollo11_countdown.mp3> --out <dir> [--score <master.wav>]

Writes launch-radio-countdown.wav (48 kHz mono PCM16), launch-radio-cues.json and, with --score, the offline
departure mixdown that applies the same gains, duck and cue times as MissionAudioPresenter's opening program.
Cut points come from the waveform, pitch-track and spectrogram analysis documented in ../README.md. Speech
recognition was used only to name words; no timestamp below is an ASR timestamp.
"""
import argparse
import hashlib
import json
import os
import struct
import subprocess
import sys

import numpy as np

SOURCE_URL = "https://www.nasa.gov/wp-content/uploads/2015/01/590320main_ringtone_apollo11_countdown.mp3"
SOURCE_BYTES = 400398
SOURCE_SHA256 = "9f14d326e95356e096b181c807136dd460f5a30afae02d55135e84d5e3c0a6f0"
TRANSCRIPT_URL = "https://www.nasa.gov/wp-content/uploads/2015/03/apollo-11_day-01-highlights.pdf"
SR = 48000

# Departure playhead (TransportDepartureTimeline): ignition 9.0 s, liftoff 10.5 s, climb cut 13.25 s,
# upper air 17.5 s, orbit 22.0 s. Source times are seconds in the official file (measured onsets).
ZERO_ONSET_SRC = 8.66        # "zero" word onset in the source
ZERO_GAME = 10.40            # 0.10 s before TransportDepartureTimeline.LiftoffSeconds
TOWER_ONSET_SRC = 21.42      # "Tower" onset in the source
TOWER_GAME = 16.30           # the vehicle is above the tower during the climb shot
SEGMENTS = [  # (name, source start, source end, fade in, fade out); no edit inside a segment
    ("A ignition-sequence-start .. we-have-a-liftoff", 0.08, 13.80, 0.03, 0.07),
    ("B tower-cleared .. we-got-a-roll-program", 21.36, 24.60, 0.04, 0.12),
]
OMITTED = [
    (0.00, 0.08, "silence before the first word"),
    (13.80, 21.36, "'32 minutes past the hour. Liftoff on Apollo 11.' and crowd noise (names Apollo 11)"),
    (24.60, 24.76, "fade-out tail"),
]
WORDS = [  # (caption, source onset) measured from the waveform; captions start at these onsets
    ("Ignition sequence start.", 0.13), ("Six.", 2.62), ("Five.", 3.55), ("Four.", 4.57), ("Three.", 5.46),
    ("Two.", 6.55), ("One.", 7.52), ("Zero.", 8.66), ("All engines running.", 9.52), ("Liftoff!", 11.45),
    ("We have a liftoff.", 12.52), ("Tower cleared.", 21.42), ("We got a roll program.", 22.97),
]
# Runtime mix used by MissionAudioPresenter (keep in sync with its serialized defaults).
MUSIC_VOLUME, DEPARTURE_TRIM, RADIO_VOLUME = 0.8, 0.65, 1.0
DUCK_DB, DUCK_ATTACK, DUCK_RELEASE = -5.0, 0.08, 0.65
SCORE_AT_DEPARTURE_ZERO = 49.90   # score seconds at departure 0 s (the 49.91 s downbeat)
DEPARTURE_SECONDS = 34.0
END_FADE_SECONDS = 3.5
RADIO_TARGET_LUFS = -18.0


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def decode(path, channels):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype="<f4").astype(np.float64).reshape(-1, channels)


def ffmpeg_filter(x, chain):
    out = subprocess.run(["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af", chain,
                          "-f", "f32le", "-"], input=x.astype("<f4").tobytes(), check=True, capture_output=True).stdout
    return np.frombuffer(out, dtype="<f4").astype(np.float64)


def write_wav(path, x, channels):
    pcm = np.clip(np.round(np.asarray(x).reshape(-1) * 32767.0), -32768, 32767).astype("<i2").tobytes()
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 36 + len(pcm)) + b"WAVEfmt " +
                struct.pack("<IHHIIHH", 16, 1, channels, SR, SR * channels * 2, channels * 2, 16) +
                b"data" + struct.pack("<I", len(pcm)) + pcm)


def fade(seg, fin, fout):
    n_in, n_out = int(fin * SR), int(fout * SR)
    if n_in:
        seg[:n_in] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, n_in))
    if n_out:
        seg[-n_out:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n_out))
    return seg


def loudness(path):
    err = subprocess.run(["ffmpeg", "-nostats", "-i", path, "-af", "ebur128=peak=true", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    lines = [l.strip() for l in err.splitlines()]
    i = next(float(l.split()[1]) for l in reversed(lines) if l.startswith("I:"))
    p = next(float(l.split()[1]) for l in reversed(lines) if l.startswith("Peak:"))
    return i, p


def build_radio(source):
    size = os.path.getsize(source)
    digest = sha256(source)
    if size != SOURCE_BYTES or digest != SOURCE_SHA256:
        sys.exit(f"Source mismatch: {size} bytes, sha256 {digest}; expected the official NASA file {SOURCE_URL}")
    src = decode(source, 2).mean(axis=1)
    offsets = [ZERO_GAME - ZERO_ONSET_SRC, TOWER_GAME - TOWER_ONSET_SRC]   # departure = source + offset
    start_game = SEGMENTS[0][1] + offsets[0]
    length = SEGMENTS[1][2] + offsets[1] - start_game
    radio = np.zeros(int(round(length * SR)) + 1)
    placed = []
    for (name, s0, s1, fin, fout), off in zip(SEGMENTS, offsets):
        seg = fade(src[int(round(s0 * SR)):int(round(s1 * SR))].copy(), fin, fout)
        at = int(round((s0 + off - start_game) * SR))
        radio[at:at + len(seg)] += seg
        placed.append({"segment": name, "source_seconds": [s0, s1],
                       "radio_clip_seconds": [round(at / SR, 4), round((at + len(seg)) / SR, 4)],
                       "departure_seconds": [round(s0 + off, 4), round(s1 + off, 4)]})
    # Radio band, the recording's steady 2.08 kHz whistle notched, level evened out. No time edits inside a segment.
    chain = ("highpass=f=280:poles=2,highpass=f=280:poles=2,lowpass=f=3500:poles=2,lowpass=f=3500:poles=2,"
             "equalizer=f=2079.5:t=q:w=18:g=-24,equalizer=f=1800:t=q:w=1.2:g=2.5,"
             "acompressor=threshold=0.08:ratio=3:attack=6:release=140:makeup=1.6:knee=4")
    radio = ffmpeg_filter(radio, chain)[:len(radio)]
    return src, radio, offsets, start_game, placed, chain


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--score")
    ap.add_argument("--sfx-root", help="Audio/Original/LaunchEffects: also render the mix with the in-game cabin effects")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    src, radio, offsets, start_game, placed, chain = build_radio(a.source)

    probe = os.path.join(a.out, "_radio_probe.wav")
    write_wav(probe, radio / max(1e-9, np.abs(radio).max()) * 0.5, 1)
    probe_lufs, _ = loudness(probe)
    os.remove(probe)
    radio = radio / max(1e-9, np.abs(radio).max()) * 0.5 * 10 ** ((RADIO_TARGET_LUFS - probe_lufs) / 20)
    if np.abs(radio).max() > 0.708:   # keep about 3 dB of peak headroom
        radio *= 0.708 / np.abs(radio).max()
    radio_path = os.path.join(a.out, "launch-radio-countdown.wav")
    write_wav(radio_path, radio, 1)
    ri, rp = loudness(radio_path)

    words = []
    for text, onset in WORDS:
        off = offsets[0] if onset < SEGMENTS[0][2] else offsets[1]
        words.append({"text": text, "source_onset": onset, "departure_seconds": round(onset + off, 3),
                      "radio_clip_seconds": round(onset + off - start_game, 3)})
    cues = {
        "asset": "Assets/_OutOfReach/Audio/LaunchRadio/launch-radio-countdown.wav",
        "source": {"url": SOURCE_URL, "bytes": SOURCE_BYTES, "sha256": SOURCE_SHA256,
                   "decoded_seconds": round(len(src) / SR, 4), "transcript": TRANSCRIPT_URL, "credit": "NASA",
                   "rights": "NASA audio; not protected by copyright in the United States (NASA media usage guidelines). "
                             "Credit NASA. No NASA endorsement is implied."},
        "starts_at_departure_seconds": round(start_game, 4),
        "duration_seconds": round(len(radio) / SR, 4),
        "alignment": {"zero_onset_source": ZERO_ONSET_SRC, "zero_departure_seconds": ZERO_GAME,
                      "ignition_departure_seconds": 9.0, "liftoff_departure_seconds": 10.5,
                      "climb_cut_departure_seconds": 13.25, "orbit_departure_seconds": 22.0,
                      "tower_onset_source": TOWER_ONSET_SRC, "tower_departure_seconds": TOWER_GAME},
        "segments": placed,
        "omitted": [{"source_seconds": [s, e], "content": c} for s, e, c in OMITTED],
        "words": words,
        "not_in_source": "The official file starts at 'Ignition sequence start'. Spoken 12, 11, 10, 9, 8 and 7 are not in it.",
        "processing": chain, "integrated_lufs": ri, "peak_dbfs": rp, "sample_rate": SR, "channels": 1,
        "sha256": sha256(radio_path),
    }
    with open(os.path.join(a.out, "launch-radio-cues.json"), "w", encoding="utf-8") as f:
        json.dump(cues, f, indent=2)
    print(json.dumps({k: cues[k] for k in ("starts_at_departure_seconds", "duration_seconds", "integrated_lufs",
                                           "peak_dbfs", "sha256")}))
    for w in words:
        print(f"  departure {w['departure_seconds']:6.2f} s  {w['text']}")
    if a.score:
        mixdown(a.score, radio, start_game, a.out)
        if a.sfx_root:
            mixdown(a.score, radio, start_game, a.out, a.sfx_root)


# In-game departure cabin effects (I01MissionComposition.OnDepartureCue): (departure s, clip, cue gain, stopped at s).
# The seatbelt click follows the seat latch (2.84 s in the native run). StopDepartureEffects cuts every one-shot at
# 9.75 s (pad cut) and 17.5 s (upper atmosphere); from the pad roar on the cabin source keeps a -4 dB duck floor.
CABIN_EVENTS = [
    (2.84, "departure-seatbelt-click", .90, 9.75), (3.00, "departure-hatch-latch", .85, 9.75),
    (9.00, "departure-ignition-ramp", 1.0, 9.75), (9.00, "departure-engine-vibration", .90, 9.75),
    (9.75, "departure-pad-roar", .92, 17.5),
]
SFX_VOLUME, ATMOSPHERIC_FLOOR, PAD_ROAR_SECONDS = 0.8, 0.631, 9.75


def mixdown(score_path, radio, radio_start_game, out, sfx_root=None):
    score = decode(score_path, 2)
    pre, post = 1.0, 1.5
    total = int(round((pre + DEPARTURE_SECONDS + post) * SR))
    t = np.arange(total) / SR - pre                          # departure seconds
    pos = SCORE_AT_DEPARTURE_ZERO + t
    idx = np.clip(np.round(pos * SR).astype(int), 0, len(score) - 1)
    music = score[idx] * ((t >= 0) & (pos < len(score) / SR))[:, None]
    entry = np.clip(t / 0.12, 0, 1)                         # runtime entrance at the downbeat
    end = 0.5 - 0.5 * np.cos(np.pi * np.clip((DEPARTURE_SECONDS - t) / END_FADE_SECONDS, 0, 1))
    voice = np.zeros(total)
    r0 = int(round((radio_start_game + pre) * SR))
    n = max(0, min(len(radio), total - r0))
    voice[r0:r0 + n] = radio[:n]
    active = np.zeros(total, dtype=bool)
    active[r0:r0 + n] = True
    duck = np.empty(total)
    g, target = 1.0, 10 ** (DUCK_DB / 20)
    a_att, a_rel = 1 - np.exp(-1 / (DUCK_ATTACK * SR)), 1 - np.exp(-1 / (DUCK_RELEASE * SR))
    for k in range(total):
        g += ((target if active[k] else 1.0) - g) * (a_att if active[k] else a_rel)
        duck[k] = g
    mix = music * (MUSIC_VOLUME * DEPARTURE_TRIM * entry * end * duck)[:, None] + (voice * RADIO_VOLUME)[:, None]
    name = "opening-departure-mixdown"
    if sfx_root:
        cabin = np.zeros((total, 2))
        for start, clip, gain, stop in CABIN_EVENTS:
            data = decode(os.path.join(sfx_root, clip + ".wav"), 2)
            a, b = int(round((start + pre) * SR)), int(round((stop + pre) * SR))
            n = max(0, min(len(data), b - a, total - a))
            cabin[a:a + n] += data[:n] * gain
        floor = np.where(t >= PAD_ROAR_SECONDS, np.maximum(duck, ATMOSPHERIC_FLOOR), duck)
        mix = mix + cabin * (SFX_VOLUME * floor)[:, None]
        name += "-with-cabin-sfx"
    path = os.path.join(out, name + ".wav")
    write_wav(path, mix, 2)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", path, "-c:a", "libmp3lame", "-b:a", "192k",
                    os.path.join(out, name + ".mp3")], check=True)
    mi, mp = loudness(path)
    print(f"mixdown {path}: {mi} LUFS, peak {mp} dBFS; departure 0 s is at {pre:.1f} s in the file")


if __name__ == "__main__":
    main()

"""Original OUT OF REACH instrumental loop candidates, synthesized offline.

All notes, rhythms, patches and arrangements below are authored for this task.
No sampled music, third-party audio, downloads, API calls, or runtime integration.
Run from any directory: python .local/music-staging/build_music.py
All writes stay beside this script. numpy and an installed ffmpeg are required.
"""
from __future__ import annotations

import hashlib
import io
import json
import math
import re
import shutil
import subprocess
import wave
from pathlib import Path

import numpy as np

OUT = Path(__file__).resolve().parent
RATE = 48000
DURATION = 32.0
FRAMES = round(RATE * DURATION)
TIME = np.arange(FRAMES, dtype=np.float64) / RATE
TAU = 2 * math.pi
VERSION = "oor-original-score-candidates-v1"
TARGET_LUFS = -24.0
PEAK_CEILING_DBFS = -8.0


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def db(value: float):
    return round(20 * math.log10(value), 4) if value > 0 else None


def hz(midi_note: float) -> float:
    return 440 * 2 ** ((midi_note - 69) / 12)


def periodic_frequency(frequency: float) -> float:
    """Whole cycles over the full loop; tuning offset remains far below a semitone."""
    return round(frequency * DURATION) / DURATION


def sine(frequency: float, phase: float = 0, depth: float = 0, motion_cycles: int = 1):
    motion = depth * np.sin(TAU * motion_cycles * TIME / DURATION)
    return np.sin(TAU * periodic_frequency(frequency) * TIME + phase + motion)


def pan(signal: np.ndarray, position: float) -> np.ndarray:
    angle = (position + 1) * math.pi / 4
    return np.column_stack((signal * math.cos(angle), signal * math.sin(angle)))


def periodic_noise(seed: int, low: float, high: float) -> np.ndarray:
    rng = np.random.default_rng(seed)
    spectrum = np.fft.rfft(rng.standard_normal(FRAMES))
    freq = np.fft.rfftfreq(FRAMES, 1 / RATE)
    response = 1 / np.sqrt(1 + (low / np.maximum(freq, .001)) ** 8)
    response *= 1 / np.sqrt(1 + (freq / high) ** 8)
    response[0] = 0
    result = np.fft.irfft(spectrum * response, FRAMES)
    return result / max(float(np.sqrt(np.mean(result * result))), 1e-9)


def chord_window(center: float, half_width: float = 8.0) -> np.ndarray:
    distance = (TIME - center + DURATION / 2) % DURATION - DURATION / 2
    window = np.zeros(FRAMES)
    inside = np.abs(distance) < half_width
    window[inside] = np.cos(math.pi * distance[inside] / (2 * half_width)) ** 2
    return window


def soft_event(start: float, duration: float, attack: float = .025, release: float = .08,
               decay: float = 2.5) -> np.ndarray:
    phase = (TIME - start) % DURATION
    envelope = np.zeros(FRAMES)
    active = phase < duration
    t = phase[active]
    attack_shape = np.sin(np.clip(t / attack, 0, 1) * math.pi / 2) ** 2
    release_shape = np.sin(np.clip((duration - t) / release, 0, 1) * math.pi / 2) ** 2
    envelope[active] = attack_shape * release_shape * np.exp(-decay * t / duration)
    return envelope


def pad(notes: list[int], center: float, gain: float, seed: int, width: float = .7,
        half_width: float = 8.0) -> np.ndarray:
    output = np.zeros((FRAMES, 2))
    envelope = chord_window(center, half_width)
    rng = np.random.default_rng(seed)
    for index, note in enumerate(notes):
        frequency = hz(note)
        phase = float(rng.uniform(0, TAU))
        position = (-width if index % 2 == 0 else width) * (.7 + .3 * index / len(notes))
        # Soft odd partials and slow periodic phase motion make an original warm pad patch.
        core = sine(frequency, phase, .07, 2)
        core += .12 * sine(frequency * 3, phase * .6, .035, 1)
        core += .025 * sine(frequency * 5, phase * 1.3)
        detuned = sine(frequency + .065, phase + .11, .05, 3)
        output += pan(core * envelope, position) * (gain / len(notes))
        output += pan(detuned * envelope, -position) * (gain * .22 / len(notes))
    return output


def low_pulse(note: int, envelope: np.ndarray, gain: float, position: float = 0):
    fundamental = sine(hz(note), .2)
    harmonic = .18 * sine(hz(note) * 2, .45)
    gentle_drive = np.tanh((fundamental + harmonic) * 1.15) / math.tanh(1.15)
    return pan(gentle_drive * envelope * gain, position)


def isolation() -> np.ndarray:
    output = np.zeros((FRAMES, 2))
    # Four original open voicings, gently crossfaded; D remains a shared anchor.
    voicings = ([38, 45, 53, 64], [38, 46, 53, 57], [38, 45, 52, 60], [38, 43, 50, 57])
    for index, chord in enumerate(voicings):
        output += pad(list(chord), 4 + index * 8, .48, 110 + index)
    breath = .65 + .35 * np.sin(TAU * TIME / 16 + .4) ** 2
    output += pan(sine(hz(26), .8) * breath, 0) * .035
    # Isolated low-key instrument glints; no lead melody or familiar quotation.
    for index, (start, note) in enumerate(((2, 57), (10.5, 64), (18, 60), (26.5, 57))):
        envelope = soft_event(start, 3.8, .10, .75, 3.5)
        glint = sine(hz(note), index * .4) + .04 * sine(hz(note) * 4, .3)
        output += pan(glint * envelope, -.52 if index % 2 == 0 else .52) * .024
    # A barely audible mechanical pulse every four beats, rather than percussion.
    for start in np.arange(.5, DURATION, 4):
        output += low_pulse(38, soft_event(float(start), .65, .035, .2, 3), .022)
    return output


def tension() -> np.ndarray:
    output = np.zeros((FRAMES, 2))
    for index, chord in enumerate(([38, 45, 52], [38, 46, 53], [38, 44, 52], [38, 45, 51])):
        output += pad(list(chord), 4 + index * 8, .32, 210 + index, .55)
    beat = .8  # 75 BPM, 40 beats = ten complete 4/4 bars.
    notes = [38, 38, 45, 38, 38, 41, 43, 45]
    for step in range(80):
        start = step * beat / 2
        accent = .095 if step % 4 == 0 else .048 if step % 2 == 0 else .021
        # A repeating swell creates anticipation without a discontinuous loop-end crescendo.
        arc = .72 + .28 * math.sin(TAU * start / 16 - math.pi / 2)
        envelope = soft_event(start, .33, .018, .08, 3)
        output += low_pulse(notes[step % len(notes)], envelope, accent * arc,
                            -.12 if step % 2 == 0 else .12)
    brush = periodic_noise(221, 1100, 2800)
    for start in np.arange(.8, DURATION, 1.6):
        output += pan(brush * soft_event(float(start), .10, .012, .045, 2), .28) * .009
    # A quiet upper neighbour introduces tension sparsely, not a continuous speech-band wash.
    for start in (6.4, 14.4, 22.4, 30.4):
        output += pan(sine(hz(51), .7) * soft_event(start, 1.3, .15, .35, 1), -.25) * .018
    return output


def emergency() -> np.ndarray:
    output = np.zeros((FRAMES, 2))
    for index, chord in enumerate(([38, 45, 51], [38, 39, 45], [38, 43, 50], [38, 45, 51])):
        output += pad(list(chord), 4 + index * 8, .22, 310 + index, .45)
    beat = 2 / 3  # 90 BPM, 48 beats = twelve complete 4/4 bars.
    for step in range(96):
        # Syncopated 3+3+2 grouping changes urgency through pattern/density, not just gain.
        phase = step % 8
        if phase not in (0, 2, 3, 5, 6):
            continue
        start = step * beat / 2
        accent = .12 if phase in (0, 3, 6) else .045
        note = 38 if phase != 5 else 39
        envelope = soft_event(start, .24, .012, .065, 2.7)
        output += low_pulse(note, envelope, accent, -.08 if phase % 2 else .08)
    body = periodic_noise(331, 110, 850)
    tick = periodic_noise(332, 1400, 3500)
    for bar in range(12):
        start = bar * 4 * beat
        output += pan(body * soft_event(start, .28, .018, .10, 3), 0) * .024
        for offset in (1.5, 3.5):
            output += pan(tick * soft_event(start + offset * beat, .065, .008, .028, 3),
                          -.25 if offset == 1.5 else .25) * .011
    # Slow sustained strain around D/E-flat, intentionally understated and non-vocal.
    strain = .5 + .5 * np.sin(TAU * TIME / 8 - .6) ** 2
    output += pan(sine(hz(50), .4, .08, 4) * strain, -.3) * .014
    output += pan(sine(hz(51), 1.8, .07, 4) * strain, .3) * .009
    return output


def spectral_space(audio: np.ndarray) -> np.ndarray:
    """Periodic FFT filtering: no filter-reset transient at the loop seam."""
    frequency = np.fft.rfftfreq(FRAMES, 1 / RATE)
    response = 1 / np.sqrt(1 + (29 / np.maximum(frequency, .001)) ** 4)
    response *= 1 / np.sqrt(1 + (frequency / 5200) ** 8)
    # Leave a broad pocket for speech articulation; dialogue ducking is a later integration concern.
    scoop = 1 - .48 * np.exp(-.5 * (np.log2(np.maximum(frequency, 1) / 1100) / .95) ** 2)
    response *= scoop
    response[0] = 0
    return np.column_stack([np.fft.irfft(np.fft.rfft(audio[:, channel]) * response, FRAMES)
                            for channel in range(2)])


def loudness(audio: np.ndarray, executable: str) -> dict:
    result = subprocess.run([executable, "-hide_banner", "-nostats", "-f", "f32le", "-ar", str(RATE),
                             "-ac", "2", "-i", "pipe:0", "-af", "ebur128=peak=true", "-f", "null", "-"],
                            input=audio.astype("<f4").tobytes(), stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, check=True)
    log = result.stderr.decode("utf-8", errors="replace")
    summary = log.rsplit("Summary:", 1)[-1]

    def number(pattern):
        match = re.search(pattern, summary)
        if not match:
            raise RuntimeError("Could not parse ffmpeg loudness summary: " + summary[-1000:])
        return float(match.group(1))

    return {"integrated_lufs": number(r"I:\s+(-?[\d.]+) LUFS"),
            "loudness_range_lu": number(r"LRA:\s+(-?[\d.]+) LU"),
            "true_peak_dbfs": number(r"Peak:\s+(-?[\d.]+) dBFS"),
            "measurement": "Installed ffmpeg ebur128 with true-peak oversampling; not human listening"}


def seam_checks(pcm: np.ndarray) -> dict:
    values = pcm.astype(np.float64) / 32768
    delta = np.diff(values, axis=0)
    wrap_delta = values[0] - values[-1]
    incoming_slope = delta[-1]
    outgoing_slope = delta[0]
    slope_change = np.maximum(np.abs(wrap_delta - incoming_slope), np.abs(outgoing_slope - wrap_delta))
    ordinary_step = np.percentile(np.abs(delta), 99.9, axis=0)
    ordinary_curvature = np.percentile(np.abs(np.diff(delta, axis=0)), 99.9, axis=0)
    step_limit = np.maximum(8 / 32768, ordinary_step * 2)
    curvature_limit = np.maximum(8 / 32768, ordinary_curvature * 2)
    passed = bool(np.all(np.abs(wrap_delta) <= step_limit) and np.all(slope_change <= curvature_limit))
    return {
        "pass": passed,
        "first_sample_l_r": values[0].tolist(), "last_sample_l_r": values[-1].tolist(),
        "wrap_step_l_r": wrap_delta.tolist(), "incoming_slope_l_r": incoming_slope.tolist(),
        "outgoing_slope_l_r": outgoing_slope.tolist(),
        "boundary_slope_change_l_r": slope_change.tolist(),
        "allowed_wrap_step_l_r": step_limit.tolist(), "allowed_slope_change_l_r": curvature_limit.tolist(),
        "method": "Integer-cycle oscillators, periodic modulation/envelopes and FFT noise/filtering; compare PCM wrap step/slope against normal local changes and quantization floor",
        "interpretation": "Numerical seam check only. Adjacent first/last PCM samples need not be equal for a continuous periodic waveform. Auditory loop check is pending.",
    }


def pcm_wav(audio: np.ndarray) -> tuple[bytes, np.ndarray]:
    pcm = np.rint(audio * 32767).astype("<i2")
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    return buffer.getvalue(), pcm


def main():
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("Installed ffmpeg required; this script will not download it")
    pieces = [
        ("mx01_isolation", isolation, 60, 8, "D minor / open suspended voicings",
         "Quiet isolation: slow crossfaded pads, wide restrained glints, sparse low mechanical pulse"),
        ("mx02_tension", tension, 75, 10, "D minor with fleeting chromatic neighbours",
         "Rising tension: repeating low ostinato, cyclical swells and sparse muted ticks; no sudden loop reset"),
        ("mx03_emergency", emergency, 90, 12, "D-centred minor / E-flat tension",
         "Controlled emergency: syncopated low pulses, restrained contact-like percussion and narrow dissonance; no trailer impacts"),
    ]
    files = []
    for name, compose, tempo, bars, key, intention in pieces:
        audio = spectral_space(compose())
        if not np.all(np.isfinite(audio)):
            raise ValueError("Non-finite synthesized samples")
        initial = loudness(audio, ffmpeg)
        gain_db = TARGET_LUFS - initial["integrated_lufs"]
        audio *= 10 ** (gain_db / 20)
        peak = float(np.max(np.abs(audio)))
        ceiling = 10 ** (PEAK_CEILING_DBFS / 20)
        if peak > ceiling:
            audio *= ceiling / peak
        encoded, pcm = pcm_wav(audio)
        reconstructed = pcm.astype(np.float64) / 32768
        measured = loudness(reconstructed, ffmpeg)
        seam = seam_checks(pcm)
        peak_db = db(float(np.max(np.abs(reconstructed))))
        clipped = int(np.sum(np.abs(pcm.astype(np.int32)) >= 32767))
        if clipped or peak_db > -6 or measured["true_peak_dbfs"] > -6 or not seam["pass"]:
            raise ValueError(f"Audio checks failed: {name}: peak={peak_db}, true peak={measured}, seam={seam}")
        path = OUT / (name + ".wav")
        temporary = OUT / (name + ".wav.tmp")
        temporary.write_bytes(encoded)
        temporary.replace(path)
        mono = np.mean(reconstructed, axis=1)
        stereo_rms = float(np.sqrt(np.mean(reconstructed * reconstructed)))
        mono_rms = float(np.sqrt(np.mean(mono * mono)))
        metadata = {
            "id": name, "path": path.name, "sha256": sha(encoded), "byte_count": len(encoded),
            "sample_rate_hz": RATE, "channels": 2, "pcm_bits": 16, "frames": FRAMES,
            "duration_seconds": DURATION, "tempo_bpm": tempo, "meter": "4/4", "bars": bars,
            "tonal_center": key, "intention": intention,
            "loop_start_frame": 0, "loop_end_frame_exclusive": FRAMES,
            "pre_roll_seconds": 0, "post_roll_seconds": 0,
            "sample_peak_dbfs": peak_db, "clipped_samples": clipped, "nonfinite_samples": 0,
            "loudness": measured, "loop_seam_checks": seam,
            "stereo_correlation": round(float(np.corrcoef(reconstructed.T)[0, 1]), 5),
            "mono_fold_down_rms_loss_db": round(20 * math.log10(max(mono_rms, 1e-12) / stereo_rms), 4),
            "authorship": "Original OUT OF REACH composition, arrangement, oscillator patches and procedural noise authored in build_music.py; no third-party recordings, samples, melodies or model/API audio",
            "human_listening_verified": False,
            "integration": "Non-diegetic score candidate only. Runtime gain, dialogue ducking and state transitions belong to later integration; no gameplay authority or rescue/delay implication.",
        }
        files.append(metadata)
        (OUT / (name + ".json")).write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"id": name, "duration": DURATION, "tempo": tempo, "lufs": measured["integrated_lufs"],
                          "peak": peak_db, "true_peak": measured["true_peak_dbfs"], "seam_pass": seam["pass"],
                          "sha256": metadata["sha256"]}), flush=True)
    manifest = {
        "version": VERSION, "script_sha256": sha(Path(__file__).read_bytes()),
        "ffmpeg_version": subprocess.run([ffmpeg, "-version"], capture_output=True, text=True, check=True).stdout.splitlines()[0],
        "scope": "Three original instrumental candidates for user listening; no project import or runtime integration",
        "same_loudness_target_lufs": TARGET_LUFS, "sample_peak_ceiling_dbfs": PEAK_CEILING_DBFS,
        "loop_construction": "Exactly 32 s, native 48 kHz stereo, no terminal fade; periodic synthesis and modulation",
        "existing_voice_or_sfx_files_modified": False, "human_listening_verified": False,
        "files": files,
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("MUSIC_CANDIDATES_COMPLETE " + str(OUT), flush=True)


if __name__ == "__main__":
    main()

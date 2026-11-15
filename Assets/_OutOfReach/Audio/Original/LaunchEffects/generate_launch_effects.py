"""Render original transport cabin cues using only the Python standard library."""

import hashlib
import json
import math
import random
import struct
import wave
from pathlib import Path

RATE = 48_000
TAU = 2 * math.pi
FOLDER = Path(__file__).resolve().parent


def smoothstep(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def window(time, start, end, attack, release):
    return smoothstep((time - start) / attack) * smoothstep((end - time) / release)


def resonant_hit(time, begin, duration, decay, frequencies):
    age = time - begin
    if age < 0.0 or age >= duration:
        return 0.0
    envelope = window(age, 0.0, duration, 0.002, 0.025) * math.exp(-age / decay)
    return envelope * sum(weight * math.sin(TAU * hz * age) for hz, weight in frequencies)


class BandNoise:
    def __init__(self, seed, lower, upper):
        self.random = random.Random(seed)
        self.fast = 0.0
        self.slow = 0.0
        self.fast_step = 1.0 - math.exp(-TAU * upper / RATE)
        self.slow_step = 1.0 - math.exp(-TAU * lower / RATE)

    def next(self):
        sample = self.random.uniform(-1.0, 1.0)
        self.fast += self.fast_step * (sample - self.fast)
        self.slow += self.slow_step * (sample - self.slow)
        return self.fast - self.slow


def seatbelt_click(time, noise):
    buckle = resonant_hit(time, 0.110, 0.155, 0.024, ((305, 0.35), (765, 0.31), (1660, 0.16)))
    latch = resonant_hit(time, 0.149, 0.150, 0.019, ((535, 0.50), (1390, 0.29), (2460, 0.12)))
    contact = window(time, 0.103, 0.205, 0.012, 0.070)
    strap = window(time, 0.225, 0.430, 0.015, 0.140)
    return buckle + latch + noise.next() * (0.52 * contact + 0.17 * strap)


def hatch_latch(time, noise):
    motor = window(time, 0.04, 2.74, 0.30, 0.46)
    phase = TAU * (69.0 * time + 5.9 * time * time - 1.3 * time ** 3)
    drive = 0.11 * math.sin(phase) + 0.08 * math.sin(2.0 * phase) + 0.034 * math.sin(4.0 * phase)
    pawl = resonant_hit(time, 2.66, 0.23, 0.040, ((106, 0.62), (226, 0.31), (874, 0.18)))
    lock = resonant_hit(time, 2.79, 0.16, 0.023, ((256, 0.55), (1140, 0.23), (1779, 0.12)))
    friction = window(time, 2.59, 2.90, 0.02, 0.20)
    return motor * drive + pawl + lock + noise.next() * (0.33 * motor + 0.41 * friction)


def ignition_ramp(time, noise):
    lift = window(time, 0.005, 0.750, 0.105, 0.070)
    intensity = 0.35 + 0.65 * smoothstep((time - 0.055) / 0.48)
    phase = TAU * (41.0 * time + 24.0 * time * time)
    harmonic = 0.29 * math.sin(phase) + 0.19 * math.sin(2.0 * phase) + 0.09 * math.sin(4.0 * phase)
    rattle = math.sin(TAU * 197.0 * time) * (0.055 + 0.046 * math.sin(TAU * 24.0 * time))
    catches = resonant_hit(time, 0.092, 0.31, 0.073, ((54, 0.31), (125, 0.19), (262, 0.17)))
    return lift * (intensity * (harmonic + 1.15 * noise.next() + rattle) + catches)


def engine_vibration(time, noise):
    body = window(time, 0.0, 7.35, 0.27, 0.77)
    dynamics = 0.76 + 0.24 * smoothstep(time / 1.9)
    phase = TAU * (72.0 * time + 0.47 * time * time)
    hull = 0.32 * math.sin(phase) + 0.20 * math.sin(2.0 * phase) + 0.07 * math.sin(4.0 * phase)
    rattle = 0.08 * math.sin(TAU * 177.0 * time) * (0.70 + 0.30 * math.sin(TAU * 18.0 * time))
    airflow = noise.next() * (0.92 + 0.14 * math.sin(TAU * 1.9 * time))
    return body * dynamics * (hull + rattle + airflow)


def pad_roar(time, noise):
    # Turbulent exhaust heard by the fixed pad camera, then receding as the stack climbs.
    envelope = window(time, 0.0, 7.75, .13, 1.7)
    distance_gain = 1.0 - .78 * smoothstep((time - .9) / 6.2)
    boom = .11 * math.sin(TAU * (53.0 * time - .45 * time * time))
    flutter = .78 + .16 * math.sin(TAU * 7.5 * time) + .06 * math.sin(TAU * 23.0 * time)
    return envelope * distance_gain * math.tanh(3.1 * (noise.next() * flutter + boom))


def liftoff_pressure(time, noise):
    front = window(time, 0.0, .28, .009, .15)
    rise = math.exp(-time / .32) * window(time, 0.0, 1.68, .025, .32)
    body = .64 * math.sin(TAU * (66.0 * time - 9.0 * time * time))
    body += .28 * math.sin(TAU * (103.0 * time - 7.0 * time * time))
    return math.tanh((front * .48 + rise * .44) * (noise.next() * 2.7 + body * 1.22))


def amplitude_db(amplitude):
    return round(20.0 * math.log10(max(1e-10, amplitude)), 2)


def render(filename, seconds, target_peak, synthesis, seed, band, description):
    frame_count = round(seconds * RATE)
    noise = BandNoise(seed, *band)
    samples = [synthesis(index / RATE, noise) for index in range(frame_count)]
    mix_gain = 10.0 ** (target_peak / 20.0) / max(map(abs, samples))
    pcm = [round(32767 * sample * mix_gain) for sample in samples]
    destination = FOLDER / filename
    with wave.open(str(destination), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(struct.pack(f"<{frame_count}h", *pcm))
    rms = (sum(sample * sample for sample in pcm) / frame_count) ** 0.5 / 32768
    return {
        "file": filename,
        "description": description,
        "length_seconds": seconds,
        "sample_frames": frame_count,
        "sample_peak_dbfs": amplitude_db(max(map(abs, pcm)) / 32768),
        "whole_file_rms_dbfs": amplitude_db(rms),
        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
    }


def main():
    clips = [
        render("departure-seatbelt-click.wav", 0.48, -11.0, seatbelt_click, 41, (570, 3500),
               "Two buckle contact transients and a short strap scrape."),
        render("departure-hatch-latch.wav", 2.98, -10.0, hatch_latch, 65, (180, 2250),
               "Cabin hatch drive, deceleration, then pawl and latch at the end of the close."),
        render("departure-ignition-ramp.wav", 0.75, -8.0, ignition_ramp, 217, (50, 990),
               "Rapid engine onset with hull resonance, cut-ready at the first exterior frame."),
        render("departure-engine-vibration.wav", 7.35, -10.0, engine_vibration, 379, (60, 1050),
               "Optional sustained structural vibration for an interior ascent shot."),
        render("departure-pad-roar.wav", 7.75, -9.0, pad_roar, 817, (43, 2100),
               "Atmospheric exhaust roar only, cut before the upper-air transition."),
        render("departure-liftoff-shock.wav", 1.68, -6.5, liftoff_pressure, 1103, (34, 2100),
               "Cinematic launch-pad acoustic pressure transient, with a sharp front and falling bass tail."),
    ]
    manifest = {
        "source": "Original mathematical synthesis. No samples, recordings, or external dependencies.",
        "generator": Path(__file__).name,
        "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "format": "48,000 Hz mono 16-bit little-endian PCM WAV",
        "perspective": "Cabin-transmitted contact and vibration, plus a separately gated pad-atmosphere roar.",
        "clips": clips,
    }
    (FOLDER / "audio-provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

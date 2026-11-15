"""Original wake accent; standard library only. Explicit output folder prevents live imports."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import wave

RATE = 48000
DURATION = 3.2
TAU = 2 * math.pi
SOURCE_HASH = "beef0c1715bd4c301af3574ff763562cdbc612594dd5955c1c60535965eafd7b"


def ease(value):
    t = max(0.0, min(1.0, value))
    return t * t * (3 - 2 * t)


def generate(project, output):
    source = project / "Assets/_OutOfReach/Audio/Original/LaunchEffects/departure-seatbelt-click.wav"
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_HASH:
        raise ValueError("Original contact source does not match recorded provenance")
    with wave.open(str(source), "rb") as audio:
        if (audio.getnchannels(), audio.getsampwidth(), audio.getframerate()) != (1, 2, RATE):
            raise ValueError("Expected original mono PCM16 48 kHz contact")
        click = [v / 32768.0 for v in struct.unpack("<" + "h" * audio.getnframes(), audio.readframes(audio.getnframes()))]
    random_source = random.Random(20261115)
    count = round(DURATION * RATE)
    dry = []
    slow = fast = 0.0
    fast_step = 1 - math.exp(-TAU * 1300 / RATE)
    slow_step = 1 - math.exp(-TAU * 160 / RATE)
    for index in range(count):
        t = index / RATE
        white = random_source.uniform(-1.0, 1.0)
        fast += fast_step * (white - fast)
        slow += slow_step * (white - slow)
        # Rounded low impact with no gunshot-like impulse or subsonic sweep.
        low_envelope = ease(t / .045) * math.exp(-t / .36) * ease((1.5 - t) / .25)
        low = low_envelope * (.36 * math.sin(TAU * (73.416 * t + 1.5 * (1 - math.exp(-t * 8))))
            + .09 * math.sin(TAU * 146.832 * t))
        # Soft open fifth and octave, understated rather than an alarm melody.
        age = max(0.0, t - .11)
        bloom = ease(age / .16) * math.exp(-age / .80) * ease((DURATION - t) / .85)
        tone = bloom * (.045 * math.sin(TAU * 293.665 * age)
            + .022 * math.sin(TAU * 440.0 * age + .18)
            + .012 * math.sin(TAU * 587.33 * age + .31))
        air = (fast - slow) * .033 * ease(t / .13) * math.exp(-t / .65)
        contact = click[index] * .32 if index < len(click) else 0.0
        dry.append(low + tone + air + contact)
    left, right = [], []
    for index, value in enumerate(dry):
        l = r = value
        for delay, gain in ((.091, .17), (.173, .09), (.291, .045)):
            dl = index - round(delay * RATE)
            dr = index - round((delay + .013) * RATE)
            if dl >= 0: l += dry[dl] * gain
            if dr >= 0: r += dry[dr] * gain
        release = ease((count - 1 - index) / (RATE * .4))
        left.append(l * release)
        right.append(r * release)
    # Remove residual DC and leave explicit peak headroom; no Unity normalization.
    for channel in (left, right):
        mean = sum(channel) / count
        for i in range(count):
            channel[i] = (channel[i] - mean) * ease(i / (RATE * .012)) * ease((count - 1 - i) / (RATE * .04))
    peak = max(max(map(abs, left)), max(map(abs, right)))
    gain = 10 ** (-13 / 20) / peak
    pcm = []
    for l, r in zip(left, right):
        pcm.extend((round(l * gain * 32767), round(r * gain * 32767)))
    if max(map(abs, pcm)) >= 32767:
        raise ValueError("Clipped generated cue")
    output.mkdir(parents=True, exist_ok=True)
    target = output / "rest-wake-cinematic.wav"
    with wave.open(str(target), "wb") as audio:
        audio.setnchannels(2); audio.setsampwidth(2); audio.setframerate(RATE)
        audio.writeframes(struct.pack("<" + "h" * len(pcm), *pcm))
    rms = math.sqrt(sum((v / 32768.0) ** 2 for v in pcm) / len(pcm))
    manifest = {
        "asset": target.name,
        "source": "Original mathematical synthesis plus this project's original contact cue; no external samples, service generation or new dependencies.",
        "perspective": "Non-diegetic wake accent with a softened cabin/contact-like transient. Not evidence of an animated belt or a sound propagating through vacuum.",
        "reuse": {"path": str(source.relative_to(project)).replace('\\', '/'), "sha256": SOURCE_HASH, "gain_before_final_mastering": .32},
        "generator": "docs/art-direction/rest-wake-audio-20261115/generate_rest_wake.py",
        "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "format": "48000 Hz stereo PCM16 WAV",
        "duration_seconds": DURATION,
        "frames": count,
        "bytes": target.stat().st_size,
        "sample_peak_dbfs": round(20 * math.log10(max(map(abs, pcm)) / 32768.0), 3),
        "whole_clip_rms_dbfs": round(20 * math.log10(rms), 3),
        "clipped_samples": 0,
        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
        "recommended_PlaySfx_volume": .7,
        "verification_limit": "Offline PCM verification only. Native mixer/listener acceptance and subjective listening remain unverified."
    }
    (output / "audio-provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    generate(args.project.resolve(), args.output.resolve())

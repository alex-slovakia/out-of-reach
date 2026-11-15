"""Build the approved Cobalt underscore from its verified local official MP3.
No network, dependencies installed, scene edits or metadata mutation.
Run from the Unity project root. Requires existing FFmpeg and NumPy.
"""
from pathlib import Path
import hashlib, json, subprocess, wave
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = HERE / "cobalt-source.mp3"
OUTPUT = ROOT / "Assets/_OutOfReach/Audio/Music/mx01_isolation.wav"
EXPECTED_SOURCE = "5834a502f3a35407969b81e7e0291a03c808b4464f5b747ca7fb3dc2c0f8c8b4"
if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != EXPECTED_SOURCE:
    raise RuntimeError("Cobalt source hash mismatch")
meta = OUTPUT.with_suffix(OUTPUT.suffix + ".meta")
meta_before = meta.read_bytes()
rate, duration, crossfade = 48000, 128, 8
filters = "loudnorm=I=-18:TP=-3:LRA=9"
command = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-ss", "45", "-i", str(SOURCE),
           "-t", str(duration), "-af", filters, "-ar", str(rate), "-ac", "2", "-f", "f32le", "-"]
raw = subprocess.run(command, check=True, capture_output=True).stdout
samples = np.frombuffer(raw, dtype="<f4").reshape(-1, 2).astype(np.float64)
assert samples.shape == (duration * rate, 2), samples.shape
count = crossfade * rate
phase = np.arange(count, dtype=np.float64) / count
weight = .5 - .5 * np.cos(np.pi * phase)
# First8s blends source165..173 into45..53. Then continue53..165.
# End-to-start wrap lies between consecutive samples at source165s.
blend = samples[-count:] * (1 - weight[:, None]) + samples[:count] * weight[:, None]
loop = np.concatenate((blend, samples[count:-count]), axis=0)
peak = float(np.max(np.abs(loop)))
# Global headroom guard only. It preserves all seam relationships.
gain = min(1.0, 10 ** (-2.0 / 20.0) / max(peak, 1e-12))
quantized = np.rint(loop * gain * 32767).clip(-32768, 32767).astype("<i2")
reference = np.rint(samples * gain * 32767).clip(-32768, 32767).astype("<i2")
with wave.open(str(OUTPUT), "wb") as stream:
    stream.setnchannels(2); stream.setsampwidth(2); stream.setframerate(rate)
    stream.writeframes(quantized.tobytes())
assert meta.read_bytes() == meta_before, "Existing .meta changed"
cut = samples.shape[0] - count
boundary_exact = bool(np.array_equal(quantized[-1], reference[cut - 1]) and np.array_equal(quantized[0], reference[cut]))
assert boundary_exact, "Wrap samples must be consecutive original samples"
seam_step = (quantized[0].astype(np.int64) - quantized[-1].astype(np.int64)) / 32768.0
crossfade_join = (quantized[count].astype(np.int64) - quantized[count-1].astype(np.int64)) / 32768.0
assert np.isfinite(loop).all()
assert np.max(np.abs(quantized.astype(np.int32))) < 32767
assert len(quantized) == 120 * rate
measurement = subprocess.run(["ffmpeg", "-hide_banner", "-i", str(OUTPUT), "-af", "ebur128=peak=true", "-f", "null", "-"], capture_output=True, text=True, check=True).stderr
levels = measurement[measurement.rfind("Summary:"):]
(HERE / "cobalt-loop-levels.txt").write_text(levels, encoding="utf-8")
report = {
    "source_sha256": EXPECTED_SOURCE,
    "output_sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    "meta_sha256_unchanged": hashlib.sha256(meta_before).hexdigest(),
    "sample_rate_hz": rate, "channels": 2, "bits": 16,
    "frames": len(quantized), "duration_seconds": len(quantized) / rate,
    "source_excerpt_seconds": [45,173], "crossfade_seconds": crossfade,
    "crossfade_curve": "raised-cosine equal-gain, tail165..173 to head45..53; then53..165",
    "normalization_filter": filters, "additional_scalar_gain": gain,
    "wrap_is_exact_consecutive_source_samples": boundary_exact,
    "wrap_step_full_scale_per_channel": seam_step.tolist(),
    "crossfade_join_step_full_scale_per_channel": crossfade_join.tolist(),
    "maximum_abs_pcm": int(np.max(np.abs(quantized.astype(np.int32)))),
    "clipped_samples": int(np.sum(np.abs(quantized.astype(np.int32)) >= 32767)),
    "rms": float(np.sqrt(np.mean(loop ** 2))),
    "mean_dc_per_channel": np.mean(loop, axis=0).tolist(),
    "verification_limit": "Numerical seam/encoding checks only. Full-loop human listening and Unity import/playback are pending."
}
(HERE / "cobalt-loop-verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
print(levels)

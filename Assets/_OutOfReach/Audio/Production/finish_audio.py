"""Offline OUT OF REACH voice finishing and original functional SFX.

No API calls, downloads, source mutations, or project imports. Requires installed
ffmpeg and numpy. All output writes stay beneath the ignored .local/audio-rebuild directory.
Run: python Assets/_OutOfReach/Audio/Production/finish_audio.py --cue vo01_mc_briefing --skip-sfx
Run again without arguments to pick up every completed source cue and make SFX.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import shutil
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

PRODUCTION = Path(__file__).resolve().parent
ROOT = PRODUCTION.parents[3]
OUT = ROOT / ".local" / "audio-rebuild"
SOURCE = PRODUCTION / "Editor" / "SourceTakes"
SCRIPT = PRODUCTION / "voice-cues.en.json"
RATE = 24000
PEAK_DBFS = -2.0
PEAK = 10 ** (PEAK_DBFS / 20)
VERSION = "oor-offline-finish-v1"
MANIFEST = OUT / "manifest.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load_pcm(data: bytes) -> np.ndarray:
    with wave.open(io.BytesIO(data), "rb") as wav:
        if (wav.getframerate(), wav.getnchannels(), wav.getsampwidth(), wav.getcomptype()) != (RATE, 1, 2, "NONE"):
            raise ValueError("Voice source must already be native 24000 Hz mono PCM16; no implicit resampling.")
        pcm = wav.readframes(wav.getnframes())
        if len(pcm) != wav.getnframes() * 2:
            raise ValueError("Incomplete PCM payload")
    return np.frombuffer(pcm, dtype="<i2").astype(np.float64) / 32768.0


def db(value: float) -> float | None:
    return round(20 * math.log10(value), 3) if value > 0 else None


def stats(data: bytes) -> dict:
    x = load_pcm(data)
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    return {
        "sha256": sha(data), "sample_rate_hz": RATE, "channels": 1,
        "bits_per_sample": 16, "frames": len(x),
        "duration_seconds": round(len(x) / RATE, 6),
        "peak_dbfs": db(peak), "rms_dbfs": db(float(np.sqrt(np.mean(x * x)))),
        "clipped_samples": int(np.sum(np.abs(x) >= 32767 / 32768)),
        "nonfinite_samples": int(np.sum(~np.isfinite(x))),
    }


def normalize(x: np.ndarray, peak: float = PEAK) -> np.ndarray:
    if not len(x) or not np.all(np.isfinite(x)):
        raise ValueError("Empty or non-finite audio")
    maximum = float(np.max(np.abs(x)))
    if maximum < 1e-8:
        return x.copy()
    return x * (peak / maximum)


def fade(x: np.ndarray, attack: float = .008, release: float = .015) -> np.ndarray:
    x = x.copy()
    a, r = min(len(x), int(attack * RATE)), min(len(x), int(release * RATE))
    if a:
        x[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    if r:
        x[-r:] *= np.cos(np.linspace(0, math.pi / 2, r)) ** 2
    return x


def noise(seconds: float, seed: int, low: float, high: float) -> np.ndarray:
    n = max(1, round(seconds * RATE))
    rng = np.random.default_rng(seed)
    spectrum = np.fft.rfft(rng.standard_normal(n))
    freq = np.fft.rfftfreq(n, 1 / RATE)
    highpass = 1 / np.sqrt(1 + (low / np.maximum(freq, .0001)) ** 8)
    lowpass = 1 / np.sqrt(1 + (freq / high) ** 8)
    y = np.fft.irfft(spectrum * highpass * lowpass, n)
    return y / max(float(np.sqrt(np.mean(y * y))), 1e-9)


def save_audio(relative: str, x: np.ndarray, details: dict, entries: dict) -> Path:
    path = OUT / relative
    if OUT not in path.resolve().parents:
        raise ValueError("Output escaped assigned directory")
    path.parent.mkdir(parents=True, exist_ok=True)
    if not np.all(np.isfinite(x)):
        raise ValueError("Non-finite output")
    if np.max(np.abs(x)) > PEAK + 1e-7:
        x = normalize(x)
    pcm = np.rint(x * 32767).astype("<i2")
    temporary = path.with_name(path.name + ".tmp")
    with wave.open(str(temporary), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    temporary.replace(path)
    evidence = stats(path.read_bytes())
    if evidence["clipped_samples"] or evidence["nonfinite_samples"] or evidence["peak_dbfs"] > -1.5:
        raise ValueError(f"Output safety check failed: {relative}")
    entries[relative] = {"path": relative, **details, **evidence,
                         "human_listening_verified": False,
                         "automated_checks": "PCM format, finite samples, duration, sample peak, no digital clipping, SHA256"}
    return path


def ffmpeg_filter(x: np.ndarray, filters: str, executable: str) -> np.ndarray:
    command = [executable, "-hide_banner", "-loglevel", "error", "-f", "f32le", "-ar", str(RATE),
               "-ac", "1", "-i", "pipe:0", "-af", filters, "-f", "f32le", "-ar", str(RATE),
               "-ac", "1", "pipe:1"]
    result = subprocess.run(command, input=x.astype("<f4").tobytes(), stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, check=False)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", errors="replace"))
    output = np.frombuffer(result.stdout, dtype="<f4").astype(np.float64)
    if len(output) != len(x):
        raise ValueError(f"Filter changed frame count: {len(x)} -> {len(output)}")
    return output


def finish_voice(cue: dict, entries: dict, ffmpeg: str) -> str:
    cue_id = cue["id"]
    path = SOURCE / (cue_id + ".wav")
    metadata_path = SOURCE / (cue_id + ".json")
    if not path.exists() or not metadata_path.exists():
        return "waiting_for_completed_source"
    raw = path.read_bytes()
    metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    if metadata.get("sha256") != sha(raw):
        return "source_hash_not_yet_matching_metadata"
    x = load_pcm(raw)
    if len(x) < RATE // 2:
        raise ValueError(f"Unexpectedly short voice: {cue_id}")
    x -= float(np.mean(x))
    clean = normalize(x)
    source_info = {
        "cue": cue_id, "speaker": cue["speaker"], "transcript": cue["text"],
        "gate": cue["gate"], "context_label": cue.get("context_label"),
        "source": str(path.relative_to(ROOT)).replace("\\", "/"),
        "source_sha256": sha(raw), "source_duration_seconds": round(len(x) / RATE, 6),
        "source_audio_stats": stats(raw),
        "voice_source": metadata.get("source", "Generated voice; see source metadata"),
        "voice_model": metadata.get("model"), "voice_name": metadata.get("voice"),
        "authorship": "Source generated voice with supplied original transcript; offline finishing by OUT OF REACH",
        "processor_version": VERSION,
        "resampled": False, "upsampling_quality_claim": False,
    }
    master = np.concatenate((np.zeros(round(.04 * RATE)), clean, np.zeros(round(.50 * RATE))))
    save_audio(f"masters/{cue_id}.wav", master,
               {**source_info, "variant": "clean_master", "settings": {
                   "dc_offset_removed": True, "sample_peak_target_dbfs": PEAK_DBFS,
                   "head_padding_seconds": .04, "end_padding_seconds": .50,
                   "source_trimmed": False, "reverb": False}}, entries)
    strong = cue["speaker"] == "mission_control"
    low, high, ratio, drive = (220, 3500, 2.4, 1.28) if strong else (150, 4800, 1.65, 1.10)
    filters = (f"highpass=f={low}:p=2,lowpass=f={high}:p=2,"
               f"acompressor=threshold=0.10:ratio={ratio}:attack=6:release=110:makeup=1.15:knee=2.5")
    filtered = ffmpeg_filter(clean, filters, ffmpeg)
    filtered = np.tanh(filtered * drive) / math.tanh(drive)
    filtered = normalize(filtered)
    preroll, tail = round(.13 * RATE), round(.50 * RATE)
    radio = np.zeros(preroll + len(filtered) + tail)
    radio[preroll:preroll + len(filtered)] = filtered
    seed = int(hashlib.sha256(cue_id.encode()).hexdigest()[:8], 16)
    carrier_db = -55 if strong else -62
    carrier = fade(noise(len(filtered) / RATE, seed, 700, high), .03, .08) * 10 ** (carrier_db / 20)
    radio[preroll:preroll + len(filtered)] += carrier
    squelch_db = -37 if strong else -44
    opening = fade(noise(.045, seed + 1, 500, high), .006, .018) * 10 ** (squelch_db / 20)
    start = round(.015 * RATE)
    radio[start:start + len(opening)] += opening
    closing = fade(noise(.035, seed + 2, 650, high), .005, .014) * 10 ** ((squelch_db - 4) / 20)
    end = preroll + len(filtered) + round(.20 * RATE)
    radio[end:end + len(closing)] += closing
    radio = normalize(radio)
    variant = "mission_control_radio" if strong else "ship_cabin_radio"
    save_audio(f"radio/{cue_id}.wav", radio,
               {**source_info, "variant": variant, "settings": {
                   "ffmpeg_filter": filters, "soft_saturation_drive": drive,
                   "carrier_rms_dbfs_before_final_normalization": carrier_db,
                   "squelch_rms_dbfs_before_final_normalization": squelch_db,
                   "voice_start_seconds": .13, "end_safety_margin_seconds": .50,
                   "opening_squelch_seconds": [.015, .060],
                   "closing_squelch_seconds_after_source_end": [.20, .235],
                   "source_trimmed": False, "sample_peak_target_dbfs": PEAK_DBFS,
                   "reverb": False, "music": False}}, entries)
    for directory in ("masters", "radio"):
        (OUT / directory / (cue_id + ".txt")).write_text(cue["text"] + "\n", encoding="utf-8")
    return "processed"


def tone(seconds: float, frequency: float, release: float = .03) -> np.ndarray:
    t = np.arange(round(seconds * RATE)) / RATE
    return fade(np.sin(2 * math.pi * frequency * t) + .08 * np.sin(4 * math.pi * frequency * t), .008, release)


def make_sfx(entries: dict) -> None:
    t = np.arange(round(.075 * RATE)) / RATE
    select = fade((np.sin(2 * math.pi * 760 * t) * np.exp(-48 * t)) +
                  .10 * noise(.075, 11, 500, 2600) * np.exp(-85 * t), .003, .02)
    confirm = np.concatenate((tone(.085, 440), np.zeros(round(.035 * RATE)), tone(.12, 660)))
    t = np.arange(round(.055 * RATE)) / RATE
    tick = fade((np.sin(2 * math.pi * 920 * t) + .25 * np.sin(2 * math.pi * 180 * t)) * np.exp(-75 * t), .002, .015)
    urgent = np.concatenate((tone(.065, 620, .02), np.zeros(round(.07 * RATE)), tone(.065, 620, .02)))
    t = np.arange(round(.48 * RATE)) / RATE
    loss = fade((.65 * noise(.48, 23, 350, 3200) + .25 * np.sin(2 * math.pi * 850 * t)) * np.exp(-5 * t), .012, .12)
    t = np.arange(round(.85 * RATE)) / RATE
    # A descending contact-transmitted equipment vibration, not a vacuum sound.
    phase = 2 * math.pi * (115 * t - (80 / (.85 * 2)) * t * t)
    power = fade((np.sin(phase) + .25 * np.sin(phase * 2) + .13 * noise(.85, 31, 80, 800)) * (1 - t / .85), .025, .13)
    warning = np.concatenate((tone(.14, 480, .05), np.zeros(round(.10 * RATE)), tone(.14, 480, .05)))
    cues = {
        "ui_select": (select, -18, "Restrained tactile selection tick; interface only"),
        "ui_confirm": (confirm, -16, "Soft two-tone confirmation; successful command only"),
        "planning_tick": (tick, -23, "Quiet planning countdown tick; no simulation authority"),
        "planning_urgent": (urgent, -19, "Restrained paired planning prompt; does not set a deadline"),
        "comms_loss": (loss, -19, "Filtered receiver dropout; radio source only"),
        "power_down": (power, -17, "Descending equipment/contact vibration; cabin or suit contact only"),
        "warning_pulse": (warning, -19, "Soft repeated warning; informational presentation only"),
    }
    for name, (audio, peak_db, note) in cues.items():
        audio = normalize(audio, 10 ** (peak_db / 20))
        audio = np.concatenate((audio, np.zeros(round(.06 * RATE))))
        save_audio(f"sfx/{name}.wav", audio,
                   {"cue": name, "variant": "original_procedural_sfx", "processor_version": VERSION,
                    "authorship": "Original OUT OF REACH procedural synthesis authored in process.py; no sampled third-party audio",
                    "integration_note": note, "exterior_constraint": "Exterior playback must be suit, radio or contact transmitted; never airborne vacuum sound",
                    "settings": {"sample_peak_target_dbfs": peak_db, "deterministic": True,
                                 "tail_padding_seconds": .06, "reverb": False, "music": False}}, entries)
        (OUT / "sfx" / (name + ".txt")).write_text("[" + note + "]\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cue", action="append", help="Only this cue ID; repeatable")
    parser.add_argument("--skip-sfx", action="store_true")
    parser.add_argument("--sfx-only", action="store_true")
    args = parser.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("Installed ffmpeg was not found on PATH")
    entries = {}
    if MANIFEST.exists():
        previous = json.loads(MANIFEST.read_text(encoding="utf-8"))
        entries.update({item["path"]: item for item in previous.get("files", [])})
    script = json.loads(SCRIPT.read_text(encoding="utf-8-sig"))
    cues = script["cues"]
    wanted = set(args.cue or [])
    if wanted - {cue["id"] for cue in cues}:
        raise ValueError("Unknown cue IDs: " + ", ".join(sorted(wanted - {cue["id"] for cue in cues})))
    statuses = {}
    if not args.sfx_only:
        for cue in cues:
            if not wanted or cue["id"] in wanted:
                statuses[cue["id"]] = finish_voice(cue, entries, ffmpeg)
                print(cue["id"] + ": " + statuses[cue["id"]], flush=True)
    if not args.skip_sfx:
        make_sfx(entries)
    manifest = {
        "schema_version": 1, "processor_version": VERSION,
        "script_sha256": sha(Path(__file__).read_bytes()),
        "ffmpeg_version": subprocess.run([ffmpeg, "-version"], capture_output=True, text=True,
                                          check=True).stdout.splitlines()[0],
        "script_transcript_sha256": sha(SCRIPT.read_bytes()),
        "format": "Native voice 24000 Hz mono signed PCM16; SFX synthesized directly at 24000 Hz",
        "scope": "Offline production artifacts only; no runtime integration or gameplay authority",
        "source_files_preserved": True, "human_listening_verified": False,
        "transcript_note": "Sidecar subtitles reproduce supplied production script; processor does not verify spoken transcript",
        "verification_limits": "Sample peak/no clipping checks are automated; no true-peak/loudness compliance or listening claim",
        "latest_run_status": statuses,
        "files": [entries[key] for key in sorted(entries)],
    }
    OUT.mkdir(parents=True, exist_ok=True)
    temporary = MANIFEST.with_suffix(".json.tmp")
    temporary.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(MANIFEST)
    print(f"Manifest: {MANIFEST}; {len(entries)} audio files recorded", flush=True)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(1)

"""Original, deterministic contact sounds for the galley meal (R35); Python standard library only.

Foil in the hand, the tear of the top strip, the squeeze of the pouch and a swallow. No recording, sample library,
voice model or other external asset is used: every sample is computed here from seeded noise, sines and envelopes.
Run it from anywhere: it rewrites Resources/MealFoley/*.wav and audio-provenance.json beside this file.
"""
from pathlib import Path
import array
import hashlib
import json
import math
import random
import sys
import wave

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'Resources' / 'MealFoley'
RATE = 48000
PEAK = 10 ** (-10 / 20)
SEED = 351116


def bandpass(samples, f0, q):
    """RBJ band-pass biquad with 0 dB peak gain."""
    w = 2 * math.pi * f0 / RATE
    alpha = math.sin(w) / (2 * q)
    b0, b2 = alpha, -alpha
    a0, a1, a2 = 1 + alpha, -2 * math.cos(w), 1 - alpha
    x1 = x2 = y1 = y2 = 0.0
    out = []
    for x in samples:
        y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def lowpass(samples, cutoff):
    k = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    y = 0.0
    out = []
    for x in samples:
        y += k * (x - y)
        out.append(y)
    return out


def highpass(samples, cutoff):
    low = lowpass(samples, cutoff)
    return [x - l for x, l in zip(samples, low)]


def bell(x):
    return math.sin(math.pi * min(1.0, max(0.0, x)))


def add(out, start_seconds, samples, gain=1.0):
    first = int(round(start_seconds * RATE))
    for i, value in enumerate(samples):
        j = first + i
        if 0 <= j < len(out):
            out[j] += gain * value


def crackle(rng, tau, f0, q):
    """One foil crackle: a decaying noise burst through a resonance."""
    n = int(tau * 9 * RATE) + 8
    burst = [rng.uniform(-1, 1) * math.exp(-i / (tau * RATE)) for i in range(n)]
    return bandpass(burst, f0, q)


def grab(rng):
    """Foil crinkling as the fingers close on the pouch: two short clusters of crackles over a faint rustle."""
    out = [0.0] * round(.30 * RATE)
    for cluster, (t0, t1, count, level) in enumerate(((.012, .110, 16, 1.0), (.130, .245, 11, .65))):
        for _ in range(count):
            add(out, rng.uniform(t0, t1), crackle(rng, rng.uniform(.0012, .0050), rng.uniform(2800, 7500), rng.uniform(1.1, 2.2)),
                level * rng.uniform(.25, 1.0))
    rustle = highpass([rng.uniform(-1, 1) for _ in out], 1800)
    for i in range(len(out)):
        t = i / RATE
        out[i] += .24 * rustle[i] * (bell(t / .12) + .6 * bell((t - .12) / .15))
    return out


def tear(rng):
    """The top strip ripping off: crackles that come faster and brighter, a rising hiss, and a snap as it comes free."""
    length, rip = .40, .33
    out = [0.0] * round(length * RATE)
    t = .008
    while t < rip:
        x = t / rip
        add(out, t, crackle(rng, rng.uniform(.0005, .0016), (2200 + 4200 * x) * rng.uniform(.8, 1.25), 1.3),
            (.45 + .55 * x) * rng.uniform(.6, 1.0))
        t += rng.expovariate(220 + 900 * x ** 1.5)
    hiss = [rng.uniform(-1, 1) for _ in out]
    low, high = bandpass(hiss, 2600, .9), bandpass(hiss, 6200, .9)
    for i in range(len(out)):
        x = min(1.0, (i / RATE) / rip)
        fade = 1.0 if i / RATE < rip else math.exp(-((i / RATE) - rip) / .012)
        out[i] += .60 * x ** .7 * fade * ((1 - x) * low[i] + x * high[i])
    add(out, rip + .004, crackle(rng, .0060, 3200, .9), 1.0)
    thump = [math.exp(-i / (.018 * RATE)) * math.sin(2 * math.pi * 170 * i / RATE) for i in range(round(.06 * RATE))]
    add(out, rip + .004, thump, .32)
    return out


def squeeze(rng):
    """A soft pouch squeezed: muffled crinkles, a wet low squelch and a few small bubbles."""
    length = .36
    out = [0.0] * round(length * RATE)
    for _ in range(11):
        t = rng.uniform(.02, .30)
        add(out, t, crackle(rng, rng.uniform(.0030, .0090), rng.uniform(1400, 3800), rng.uniform(1.0, 1.6)),
            rng.uniform(.15, .55) * (.35 + .65 * bell(t / length)))
    wet = lowpass(lowpass([rng.uniform(-1, 1) for _ in out], 1000), 1400)
    for i in range(len(out)):
        t = i / RATE
        wobble = .5 + .5 * math.sin(2 * math.pi * (26 + 30 * t / length) * t)
        out[i] += 1.9 * wet[i] * wobble * bell(t / length) ** 1.2
    for _ in range(4):
        t0, f0 = rng.uniform(.08, .28), rng.uniform(420, 620)
        n = round(.030 * RATE)
        phase = 0.0
        blip = []
        for i in range(n):
            phase += 2 * math.pi * f0 * (1 + .6 * min(1.0, i / (.016 * RATE))) / RATE
            blip.append(math.exp(-i / (.007 * RATE)) * math.sin(phase))
        add(out, t0, blip, .22)
    return out


def swallow(rng):
    """A swallow heard from inside: a soft tick, a falling low glide, a rising release and a faint closing tick."""
    length = .28
    out = [0.0] * round(length * RATE)
    add(out, .030, lowpass([rng.uniform(-1, 1) * math.exp(-i / (.0015 * RATE)) for i in range(round(.012 * RATE))], 1200), .9)

    def glide(f_from, f_to, seconds, envelope, partials):
        n = round(seconds * RATE)
        phase = 0.0
        tone = []
        for i in range(n):
            x = i / n
            phase += 2 * math.pi * f_from * (f_to / f_from) ** x / RATE
            tone.append(envelope(i / RATE, x) * sum(level * math.sin(multiple * phase) for multiple, level in partials))
        return tone

    add(out, .040, glide(160, 92, .16, lambda t, x: min(1.0, t / .008) * math.exp(-t / .050),
                         ((1, 1.0), (2.02, .28), (3.01, .10))), .9)
    add(out, .170, glide(105, 250, .075, lambda t, x: bell(x), ((1, 1.0), (2.0, .20))), .5)
    add(out, .236, lowpass([rng.uniform(-1, 1) * math.exp(-i / (.0025 * RATE)) for i in range(round(.016 * RATE))], 1500), .55)
    breath = lowpass(highpass([rng.uniform(-1, 1) for _ in out], 500), 2500)
    for i in range(len(out)):
        out[i] += .035 * breath[i] * bell((i / RATE) / length)
    return lowpass(out, 1400)


def centroid(samples):
    """Spectral centroid in Hz from zero crossings of the signal and of its difference (a cheap, exact-enough check)."""
    energy = sum(x * x for x in samples)
    diff = sum((b - a) ** 2 for a, b in zip(samples, samples[1:]))
    return 0.0 if energy <= 0 else RATE / (2 * math.pi) * math.sqrt(diff / energy)


def write(name, samples):
    samples = list(samples)
    mean = sum(samples) / len(samples)
    samples = [x - mean for x in samples]
    for i in range(len(samples)):
        fade = min(1.0, i / (RATE * .006), (len(samples) - 1 - i) / (RATE * .014))
        samples[i] *= max(0.0, fade)
    maximum = max(abs(x) for x in samples)
    pcm = array.array('h', (round(x / maximum * PEAK * 32767) for x in samples))
    if sys.byteorder != 'little':
        pcm.byteswap()
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    with wave.open(str(path), 'rb') as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 2, RATE)
        values = array.array('h', source.readframes(source.getnframes()))
        if sys.byteorder != 'little':
            values.byteswap()
    peak = max(abs(x) for x in values) / 32768
    assert peak <= 10 ** (-8 / 20) and len(values) / RATE <= .6
    assert values[0] == values[-1] == 0
    return {'filename': str(path.relative_to(ROOT)).replace('\\', '/'), 'duration_seconds': len(values) / RATE,
            'channels': 1, 'sample_rate_hz': RATE, 'pcm_bits': 16, 'peak_dbfs': 20 * math.log10(peak),
            'rms_dbfs': 20 * math.log10(math.sqrt(sum((x / 32768) ** 2 for x in values) / len(values))),
            'spectral_centroid_hz': round(centroid([x / 32768 for x in values])),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    files = []
    for index, (name, make) in enumerate((('meal-pouch-grab.wav', grab), ('meal-pouch-tear.wav', tear),
                                          ('meal-pouch-squeeze.wav', squeeze), ('meal-swallow.wav', swallow))):
        files.append(write(name, make(random.Random(SEED + index))))
    report = {
        'result': 'PASS',
        'creator': 'OUT OF REACH, original mathematical synthesis, 2026-11-16 (R35 meal foley)',
        'source': 'No recordings, voice model, sample library or external assets used.',
        'synthesis': 'Seeded noise bursts through resonances (foil crackles), filtered noise, sine glides and smooth '
                     'envelopes; seeds %d..%d; normalized to -10 dBFS peak.' % (SEED, SEED + 3),
        'use_limit': 'Cabin contact sound heard by the crew member inside the pressurised habitat (the pouch in the '
                     'hands, the own swallow). Never exterior airborne space sound.',
        'listening_and_Unity_playback': 'NOT VERIFIED BY EAR in the authoring session; lengths, levels and spectra '
                                        'checked numerically (see files).',
        'files': files,
    }
    (ROOT / 'audio-provenance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    for entry in files:
        print('%-34s %.3f s  peak %.2f dBFS  rms %.1f dBFS  centroid %d Hz' % (
            entry['filename'], entry['duration_seconds'], entry['peak_dbfs'], entry['rms_dbfs'], entry['spectral_centroid_hz']))


if __name__ == '__main__':
    main()

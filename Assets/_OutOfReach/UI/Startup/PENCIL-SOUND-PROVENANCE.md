# Pencil scratch sound: provenance

**Origin: original, procedurally synthesised at runtime. No recordings, samples, libraries or downloads were used.**

Source of truth: `Runtime/PencilScratchSynth.cs` (author: Claude, session `claude-max-startup`, 2026-11-15). The clip is
rendered in memory when the startup cards begin (a few milliseconds), played by `Runtime/StartupPencilAudio.cs`
and destroyed with the card. Nothing audio is stored in the repository, so there is no third-party licence to attribute.

## How it sounds, and why it is in sync

The synth is driven by the same schedule as the picture: for every audio sample it asks `VectorArt` / `StartupSequence`
where the pen is (eased sketch progress, which shape is being traced, pen speed). A pen lift between shapes is silent;
each shape is a scratch with its own pressure envelope. After the last stroke a quieter, wider rub follows the colour fill.

Signal path (44.1 kHz, mono, deterministic seed `20261114`):

1. White noise (`System.Random`).
2. State-variable band-pass, Q 1.8, centre 2.2 kHz rising to about 5.3 kHz with pen speed and pressure.
3. Amplitude = pressure envelope (8 % attack, 15 % release of each stroke, 3 ms smoothing) x slow 7 Hz hand variation
   x per-stroke level x pen speed x paper grain. Grain is `0.3 + 0.7 * |low-passed noise (120 Hz)|`, giving the scratchy
   bursts of pencil on paper.
4. A short, decaying noise tick when each stroke lands.
5. Fill rub: band-pass at 1.8 kHz, bell envelope over the fill time, three slow swipes, grain from a second noise.
6. One-pole high-pass (about 600 Hz), `tanh` soft clip, peak normalise to 0.9, 20 ms end fade.

Playback: 2D, volume 0.55 x skip fade, non-diegetic. It starts 50 ms before the first stroke and re-seats the playhead if it
ever drifts more than 100 ms. Reduced effects: no sketch, so no scratch sound.

## Reproduce

`PencilScratchSynth.Render(art, timing)` returns the float samples; the same art and timing always give identical output.

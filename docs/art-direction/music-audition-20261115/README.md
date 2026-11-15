# Music audition — OUT OF REACH, 15 November 2026

Three private 25-second previews for Alex to approve. These are **composed by Scott Buckley**, not AI-generated. No runtime audio, scene or mix setting has been replaced. Full downloads are local audition sources excluded by this directory's `.gitignore`; commit the previews and evidence only.

| Option | Track / intended use | Preview excerpt | Direction |
|---|---|---|---|
| A | [Cobalt](https://www.scottbuckley.com.au/library/cobalt/) — quiet exploration and rest | 00:45–01:10 | Ambient piano, slowly changing synth texture and strings. Recommended foundation for the lonely interior; keep breathing and interaction sounds above it. |
| B | [Resonance, alternate mix](https://www.scottbuckley.com.au/library/resonance/) — danger/EVA tension | 00:35–01:00 | A darker electronic pulse with industrial tension. Use sparingly for danger rather than playing it continuously. |
| C | [Titan](https://www.scottbuckley.com.au/library/titan/) — cinematic departure/recovery | 00:10.9–00:35.9 | Orchestral exploration theme; this excerpt reprises the beginning of the currently licensed departure cue. Useful as the emotional comparison, not a newly composed replacement. |

These intended uses are editorial recommendations based on the composer's descriptions and the selected excerpts; human listening approval remains open. This does not claim that listening inside the game was tested.

## Attribution and license

- 'Cobalt' by Scott Buckley — released under CC BY 4.0. www.scottbuckley.com.au
- 'Resonance' by Scott Buckley — released under CC BY 4.0. www.scottbuckley.com.au
- 'Titan' by Scott Buckley — released under CC BY 4.0. www.scottbuckley.com.au

[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). [Composer's use policy](https://www.scottbuckley.com.au/library/using-this-music/), reviewed 15 November 2026. Each official track page explicitly identifies this license and permits attributed commercial project use. The library is presented for filmmakers, game designers and media artists. Credit must ship with any selected track. No composer endorsement is implied. These are private game-development auditions; do not sell or distribute them as a standalone music release or register them with fingerprinting services.

Edits: 25-second excerpts, normalized with FFmpeg loudnorm target -18 LUFS / -2 dBTP / LRA9, then 0.7-second fade-in and 1.5-second fade-out, encoded at192kbps MP3,44.1kHz stereo. The final encoded preview duration is25.051429 seconds including MP3 padding. All source URLs, excerpt boundaries, source/preview SHA-256 and codec probes are recorded in [manifest.json](manifest.json). Each `*-levels.txt` is FFmpeg's post-encode EBU R128 measurement. Audible taste and final game balance require Alex's approval, not just a level measurement.

Titan uses the previously downloaded original MP3 in `.local/music-staging/sb_titan.mp3`; its SHA-256 matches the existing shipped Titan notice. The Cobalt and Resonance originals were downloaded from their exact official MP3 links (9.26MB and11.89MB respectively), under a25MB-per-file limit. No new dependency or paid service was used.

## Suggested approval

Approve or reject each direction separately: A for exploration/rest, B for danger/EVA, C for cinematic moments. Once approved, produce seamless transitions and dialogue ducking in the actual game, then capture a short in-game audio pass before shipping. Do not simply loop these faded audition clips.


## Approved directions and actual loop —15 November2026

Alex explicitly approved **A(Cobalt)** and **C(Titan)** and rejected **B(Resonance)**. Cobalt is now prepared in `Assets/_OutOfReach/Audio/Music/mx01_isolation.wav`; Titan remains unchanged. Resonance is audition evidence only and must not be used in runtime. Root retains runtime mood routing/mixer ownership.

The Cobalt candidate is a120-second48kHz stereo16-bit PCM musical loop(23,040,044bytes), replacing the prior48-second procedural cue without changing the clip GUID. It uses original00:45–02:53 and an8-second raised-cosine equal-gain tail/head crossfade. Playback order is blended02:45–02:53/00:45–00:53, then00:53–02:45. This puts the actual loop wrap between consecutive original samples at02:45 instead of cutting two different phrases directly together. No start/end silence is introduced. `build_cobalt_loop.py` reproduces it from the locally ignored verified original using existing FFmpeg/NumPy; no new dependencies were installed.

Verification: exactly5,760,000stereo frames; both wrap samples equal the corresponding consecutive original PCM samples; zero clipped samples; peak sample23200/32768; post-edit loudness -17.2LUFS, true peak -3.0dBFS, LRA6.3LU. Full numerical measurements and hashes are in `cobalt-loop-verification.json` and `cobalt-loop-levels.txt`. `Cobalt-loop-wrap-preview.mp3` plays the last8seconds followed by the first8seconds, with the actual wrap at8seconds. This is an engineering audition, not a separately approved new track.

Root explicitly authorized correcting the old misleading `.meta` userData attribution. The original GUID and all importer settings are preserved. Complete credit/CC BY4.0 license text ships under `Assets/StreamingAssets/ThirdParty/Cobalt/` with new valid metadata. Full-loop human listening and actual Unity import/playback remain unverified by the audio worker. The user's approval establishes musical direction, not a claim that this exact edit has been auditioned in-game.

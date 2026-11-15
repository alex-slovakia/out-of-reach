# Approved hand push-off direction — 15 November 2026

Alex explicitly approved this board: **“Áno, prenes to do animácie”**, then **“presne takto to ma vyzerat”** and requested that it be shared privately with Claude.

![Approved animation concept](approved-concept.png)

This is an AI-generated animation direction reference, not a Unity screenshot or proof of working animation. Generated with the built-in imagegen tool; the exact prompt is in [prompt.txt](prompt.txt). It is an original project concept, not a third-party game asset.

## Required actual behavior

- Arms enter naturally from below or the corresponding side. Fingers point naturally upward in the ordinary wall-push view; neutral wrists and plausible shoulder/elbow movement replace the rejected inverted overhead approach.
- Left click uses the left hand, right click the right, and both buttons use both hands.
- Both palms have separate contact zones. Fingers, thumbs, wrists and sleeves must not overlap or pass through each other during approach, contact or return.
- Hands are opaque. Keep actual skin/material depth and contact shadows; do not mask penetration with transparency.
- Palm contact is established on the real solid surface before the existing canonical Player command pushes the body away. Preserve actual reach, joint, surface, collision and authority checks.
- The normal wall-push should match the approved board. Overhead contact needs its own anatomically natural lateral reach and visual review; the board does not justify moving the camera or faking a ceiling contact.
- Present genuine Unity sequences for left, right and paired pushes, with isolated front/side views showing the complete arms and contact. Physics assertions alone do not establish visual acceptance.

## Ownership and current status

Codex root owns integration. One Codex writer stages the hand-animation change under the ignored `.local/hand-natural-push-v02-draft/` directory. Claude should use this board as the shared visual target and coordinate before editing the same hand/bridge files. No parallel writer is assigned those runtime files.

At approval, the saved-scene neutral L/R/pair tests each passed 23 assertions and neutral Space passed 18 on the existing runtime. Those checks establish command routing and a physical impulse at one ceiling pose; Alex explicitly rejected the appearance of that pose. The complete route still fails on a later storage approach. No new build or completed art is claimed here.

Runtime baseline: `80568152425acf3e75e9282a5772c5b8bbf3bf82`, private `codex/i01-integration`; original user editor remains on `74116d0` and untouched.

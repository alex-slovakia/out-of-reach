# I04 physical-loading checkpoint

Runtime commit: `375c5353ae227159b634f50dfa75c3a17e68e24d`, Unity6000.3.28f1, Windows, RTX3070. Source includes Claude C05 through9044a00 and continuous EVA fix3bde63b. This is an internal checkpoint, not an organizer submission.

## Verification actually run

- Full saved Bootstrap: **273 scripted scene checks passed**, complete success and objective-loss missions, actual physical-cargo commands, cockpit E launch, real-input exterior round trip, repairs, light-time reply, report and restart. Actual captures included here. Setup uses documented interior teleports and precisely aimed route views; it is not a human walkthrough.
- **25/25 PlayMode tests passed**, zero failed/skipped, including independent free-look traversal regressions against this combined source. XML included.
- Earlier physical cargo scenario:144 checks passed including actual60 active seconds, pause,601kg rejection and last-valid fallback. This preceded the final cargo-label resizing and hand/panel additions, so it is supporting evidence rather than an exact375c535 whole-tree test.
- Repository/NASA offline checks passed; Unity static checker:0errors,3temporary editor harness class-name warnings. These harnesses are not shipped game source.
- No game runtime errors in the full mission run. One known UnityEditor.Search startup exception is separately recorded, not hidden or counted as gameplay success.

## Included / limits

Physical supply/bay stations are in existing Storage, not a separate launch-site map. Default valid546kg cargo starts aboard and can be changed. Carrying uses Core's movement multiplier. C05 field crafting is still unbound. Opening drift, pause blur, lower nominal practical lights, hand Press/Grip acknowledgements and the airlock readout are present. The hand gestures do not yet perform spatial tool contact; the timer's screen was mounted correctly but the actionable target was still across the room at this revision (fixed in the following patch). No sprint/stamina, full body, new dramatic voice recordings, playable post-contact survival or launch-to-orbit sequence is claimed. No Windows build or second-machine sync was verified for this revision.

The approved HUD reference and later bars/grade/tension/cargo-grip iteration are later work. Do not describe these captures as that finished image.

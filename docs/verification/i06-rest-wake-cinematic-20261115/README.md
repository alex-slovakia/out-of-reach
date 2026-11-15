# Rest / wake cinematic increment

Isolated Unity6000.3.28f1 native saved-Bootstrap run `TestResults/rest-wake-cinematic-02.log` exited0. **92 assertions passed**. Actual domain rest/Wake, captured calendar day, large heading, curved blink, wake title motion/fade, pause, reduced-effects zero offset, teardown/rebind and water guidance were exercised. `checks.json`, captured state files, source hashes and fixture are included.

The first run failed a fixture assertion that incorrectly compared flex-centered resolved top to an animation offset. The corrected test checks authored offset and rendered centering; production code did not change between those two runs. No failing production reduced-motion behavior was dismissed.

The original3.2s wake cue is bound through existing MissionAudioPresenter.PlaySfx and its existing mixer controls. Offline PCM integrity/peak checked; subjective in-game mix approval remains open. No body/seatbelt animation is supplied by this UI increment. Day is actual mission day: an early wake does not invent a new day. The setup uses real commands to reach survival, not a manual start-to-finish playthrough.

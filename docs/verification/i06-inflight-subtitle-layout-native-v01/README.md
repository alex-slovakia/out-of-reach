# In-flight subtitle layout — bounded native acceptance

Root-owned Unity6000.3.28f1 GPU run `20261115T014429621Z`: **696 checks PASS, process exit0**, with exact log/exitcode/result and source hashes archived. Both genuine component screenshots were reviewed at original size.

At1280×720 and960×540 the exact previously observed long B24 caption is complete, readable and **fits on one line**; no forced two-line wrapping or clipping is observed. Native text-measurement checks also confirm sufficient full-text height. The speaker remains above the caption. Both are visibly separated from reserve bars and the Tab control. Minimum measured horizontal clearance for vertically adjacent painted HUD/subtitle leaves across all six normal phase inputs is117 panel/rendered pixels at1280, and117.333 panel units =88.000 rendered pixels at960. Caption leaf heights are27 and20 rendered pixels respectively. See clearance-summary.json and native bounds JSON for all cases.

Native component inputs covered Intro/Emergency/Repair/Eva/Transmission/Survival; normal three reserve rows and Survival's four rows remained visible. Rendered departure centering18%/18%/9%, terminal+departure15%/15%/1%, removal/restoration ordering, menu hide/restore, and owned-view disposal passed. The actual shipped FlightBoardView/USS/fonts and cloned saved PanelSettings were used, with no production-file changes; exact pre-run source pins and all Assets snapshot retained.

**Acceptance limits:** unsaved component fixture on neutral background, illustrative reserve values and direct presentation SetPhase calls. This is not a live mission transition, Core/airlock proof, actual ship-background contrast review, sealed suit-service binding, SurvivalChapter needs overlay, every subtitle length or every aspect ratio. No claim that B24 was fixed: this addresses the separate observed caption/HUD overlap only. Native photos here are actual GPU UI screenshots, not the previous airlock scene captures. The separately retained UnityEditor.Search startup exception is an environment issue, not silently omitted.

Archive all Evidence files, source-pins.json, clearance-summary.json and manifest.json. Representative PNGs: `Evidence/eva-component-1280.png` and `Evidence/eva-component-960.png`.

Repository archive excludes full Editor startup/licensing logs. Their hashes and private originals remain available. No external browser navigation was performed.

# Actual saved-scene menu lifetime verification

Unity6000.3.28f1 GPU run2026-11-14T23:41UTC. See result/scope/checks and real screenshots. Uses the actual Bootstrap UIDocument/session controls and real restart/reload. Desktop Quit is replaced ONLY in this QA run by a counting callback; actual process exit still requires the standalone acceptance run.

Planning/pause/final report/plain outcome menus, hidden toggle NavigationSubmit, focus release, expiry, context changes, real scene restart and disable/re-enable are exercised. Real loss is reached through the disclosed public-domain/resource-time setup, not by assigning an outcome.

Fail-before run20261114T233926699Z found one real defect: opening Mission record retained the armed end-card Quit confirmation. Root fixed the view-specific context so hiding that action column disarms it; this unchanged behavioral fixture then passed. An earlier fixture run incorrectly waited on a destroyed old composition after scene reload; its wait was corrected to reacquire the actual new scene component. Neither earlier failure is counted as a passing result.

The source changes under test include Claude fb324e0's hidden-menu focus/toggle fix and root's report-detail context fix. Recorded Unity SearchDatabase startup errors remain disclosed as environment errors.

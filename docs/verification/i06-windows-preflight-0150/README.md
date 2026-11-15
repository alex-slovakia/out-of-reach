# Windows player preflight — 15 November 2026, 01:50 CET

Unity6000.3.28f1 built the saved Bootstrap into a **non-development Windows x64 player**,578,541,059 reported bytes. Exit0. The manifest hashes the complete local build directory, not just the generic Unity player executable. This was dirty integration f6b571e with the Earth/sleeve/land source changes; it predates the directed-push correction.

The executable was launched using the desktop app tools and displayed the real planning menu. A Windows network-permission dialog blocked further interaction. No permission was clicked and no security setting was changed. Root stopped only the process whose exact executable path matched this test build. **Manual gameplay and actual Quit-to-desktop remain unverified.** The build itself is not evidence that the known microgravity route failure is solved.

The original open Unity project was untouched. No executable was published or submitted. This preflight is not the final candidate build; final changed source still needs its own build and playable-flow acceptance.

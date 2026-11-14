# OUT OF REACH

A cinematic first-person survival game about designing a mission, losing contact with Earth, and making recovery possible through engineering decisions.

**Team:** Polish Alex Goats · **Challenge:** [Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/) · **Event:** [Universal Event](https://www.spaceappschallenge.org/2026/local-events/universal-event/)

## Start here

Use **Unity 6000.3.28f1**, Universal Render Pipeline, C#, Windows x64. This exact editor is installed and running in the original workspace. It supersedes the earlier preparation document's `6000.3.26f1`. Do not upgrade or downgrade independently.

This repository is the shared development foundation. A finished playable build is not yet claimed. Current work and verification are recorded in [TASKS.md](TASKS.md).

```powershell
git lfs install
git clone https://github.com/alex-slovakia/out-of-reach.git
cd out-of-reach
git lfs pull
```

In Unity Hub, install/select **6000.3.28f1** with Windows Build Support, then **Add project from disk** and select this folder. Unity will restore packages and rebuild its local Library. Keep `Packages/packages-lock.json` unchanged unless the integrator coordinates a dependency change.

Apoliak: after accepting the repository invitation, follow [the teammate handoff](docs/handoff/APOLIAK-START-HERE.md). Use [the prompt selector](docs/prompts/README.md) to choose the correct task and model.

## Development map

- [PRODUCT.md](PRODUCT.md): experience, story, limits, definition of done.
- [ARCHITECTURE.md](ARCHITECTURE.md): Unity composition and file ownership.
- [AGENTS.md](AGENTS.md): mandatory collaboration rules.
- [INTEGRATION.md](INTEGRATION.md): contracts, events, checkpoint and merge protocol.
- [NASA-DATA.md](NASA-DATA.md): dataset provenance and scientific assumptions.
- [ASSETS.md](ASSETS.md): visual direction, asset licenses and performance budget.
- [TESTING.md](TESTING.md): acceptance tests and release verification.
- [TASKS.md](TASKS.md): execution order, owners and status.

The target is one 15–20 minute mission with one accident, three repairable systems, one constrained EVA, two outcomes, and a science-informed mission report. Story days are visibly compressed. This is an educational simulation, not NASA-certified flight software.

Universal Event submissions close **November 15, 2026 at 23:59 in the team creator's local timezone**. If the creator is in Europe/Bratislava, that is 22:59 UTC. Confirm the creator's timezone before treating that conversion as the team's deadline. Reserve at least two hours for packaging and submission.

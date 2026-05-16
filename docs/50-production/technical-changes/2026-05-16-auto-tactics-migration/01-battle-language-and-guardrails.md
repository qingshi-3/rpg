# Battle Language And Guardrails

## Purpose

This document keeps future agents aligned on what "auto tactics" means for this project.

The battle execution layer can feel closer to 云顶之弈 / TFT than to turn-based tactics: after deployment, units fight automatically in real time. The gameplay identity is still a strategy RPG with `WorldSite` operation, officer/social management, and strategic-map consequences.

## Player-Facing Promise

Use this framing:

```text
The player prepares the site, appoints people, commits hero/corps builds, deploys units, then watches an automated battle validate the preparation.
```

The player should understand:

- why the fight started;
- which site, entrances, terrain, facilities, and deployments mattered;
- what each hero or corps contributed;
- why the battle was won, lost, withdrawn, or became a disaster;
- how the result changed the world, site, people, resources, facilities, or threat state.

## TFT-Like Elements We Can Borrow

- Automatic combat after a preparation phase.
- Readable unit target acquisition, movement, attacks, and skill timing.
- Composition and placement changing battle outcome.
- Speed controls and skip once the player trusts the simulation.
- End-of-battle contribution and failure diagnosis.

## Elements We Must Not Copy As Core Identity

- Shop rolls as the primary progression loop.
- Fair-board economy rounds.
- Synergy drafting replacing officer, corps, facility, and WorldSite systems.
- Disposable match-only boards detached from strategic consequences.
- Battle-time micro commands that recreate the legacy manual action menu.

## Required Terms

- Use `WorldSite` / 场域 for persistent operable locations.
- Use "automated tactical battle validation" or "auto tactical battle" for the target battle loop.
- Use "hero/corps build" for battle composition decisions.
- Use "deployment" for pre-battle unit placement stored in `WorldSiteState.UnitPlacements`.
- Use "battle playback" and "battle report" for readable feedback.

## Avoided Terms

Avoid these unless discussing legacy or non-goals:

- "turn-based tactics" as the main pitch;
- "manual battle loop" as future identity;
- "AP action menu" as a growth area;
- "TFT clone";
- "city panel" for `WorldSite`;
- "battle scene" when the concept is a persistent `WorldSite`.

## Product And Gameplay Docs To Keep Aligned

When this language changes, update these documents in the same change set:

- `docs/10-product/positioning.md`
- `docs/20-game-design/gameplay-direction.md`
- `docs/20-game-design/core-loop.md`
- `docs/20-game-design/tactical-battle/README.md`
- `docs/30-technical-design/architecture/global-system-decomposition.md`
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`
- `docs/50-production/roadmap/development-priority.md`

## Acceptance

- A main agent can describe the target without mentioning player-operated turns.
- A worker can tell the difference between "TFT-like automatic execution" and "TFT-like economy".
- No new doc treats legacy AP, `TurnSystem`, or action menu growth as the main combat future.

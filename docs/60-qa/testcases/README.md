# Test Case Index

This directory tracks what should be manually verified during prototype development.

It is not an automated test suite. Its purpose is to keep verification points out of chat history and make regressions easier to notice.

## Usage Rules

- Update the relevant test case document when code, scene, data, or asset behavior changes.
- Record focused checks for the current change.
- Record regression checks for older behavior that can be affected.
- Record unverified checks when local validation is not possible.
- Keep test cases concise and tied to player-visible or system-visible behavior.

## Current Documents

- `strategic-world-v1.md`: Strategic world V1 logic, battle writeback, raid, UI, and persistence checks.
- `strategic-world-site-grid-exploration.md`: Grid-based WorldSite exploration checks.
- `world-site-state-deployment.md`: WorldSite state driven battle deployment checks.
- `auto-tactics-migration.md`: Historical migration guardrails for keeping the retired AP/manual battle runtime deleted.
- `smoke-check-template.md`: Reusable template for focused manual smoke checks.

# Auto Tactics Migration Child Notes

This directory is historical record only.

These files document the first cleanup that retired the legacy manual/AP battle runtime and created backend auto-resolve/report infrastructure. They are not current gameplay or architecture authority.

Current authority:

- gameplay direction: `../../../../gameplay-design/content-systems-long-term-design.md`
- active architecture proposal: `../../../../design-proposals/active/2026-05-17-hero-led-light-rts-system-architecture/expected/system-design/hero-led-light-rts-system-architecture.md`
- alignment rules: `../../../../gameplay-alignment/authority-map.md`
- current gaps: `../../../../gameplay-alignment/gap-register.md`

Use these notes only to investigate:

- why legacy manual/AP runtime was removed;
- how the first auto-resolve/report backend was built;
- what code or tests were added during that cleanup;
- compatibility constraints around `BattleStartRequest`, `BattleSessionHandoff`, and `BattleResult`.

Do not use these notes to define future battle identity. Future battle work must follow hero-led light RTS with battle-time hero, corps, and combined command channels.


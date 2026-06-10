# Acceptance

Status: Accepted

The user accepted the expected architecture on 2026-06-10.

Accepted scope:

- Distinguish current execution command, player command, and self-calculated command.
- Keep player command as highest priority, created only by player input or an accepted player battle plan.
- Clear player command only when its objective is completed.
- Allow self-calculated temporary targets only when no player command is active and autonomous fallback is allowed.
- Clear self-calculated targets when combat starts, when the target is completed, or when a player command overrides it.
- Let combat-local execution commands replace display and execution while engaged without erasing stored player command truth.

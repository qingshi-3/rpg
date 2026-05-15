# Collaboration Index

This directory stores long-lived collaboration rules and working agreements.

## Routes

- AI design collaboration rules: `ai-collaboration.md`
- User/AI working agreement: `user-ai-working-agreement.md`
- Multi-agent workflow state machine: `multi-agent-workflow.md`
- Persistent agent role specs: `agents/`
- Codex Godot UI guidance: `codex-godot-ui-guidance.md`
- Game-studio quality gates: `game-studio-quality-gates.md`
- Godot C# review checklist: `godot-csharp-review-checklist.md`
- Local Godot 4.5 knowledge base: `local-knowledge/godot/`

## Godot Post-change Knowledge Check

After Godot UI, input, scene, resource, animation, runtime, or performance
changes, use `godot-csharp-review-checklist.md` to run a targeted local
knowledge-base check with `rg` before reporting completion. Search only the
relevant keywords in `local-knowledge/godot/` and, when needed,
`.codex/external/godot-docs-4.5/`; do not scan the full docs. Keep large
external documentation clones outside `res://` so Godot does not import them.

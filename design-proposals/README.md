# Design Proposals

This directory stores local proposal copies for product, gameplay, content-system, and architecture changes.

It exists because this project does not rely on frequent Git commits for every design step. A proposal is the local equivalent of a reviewed branch:

```text
authority document
+ current copy
+ expected copy
+ user acceptance
+ implementation
+ final acceptance
+ merge expected copy back to authority
+ archive proposal
```

## Scope

Use this process for changes that affect:

- product or gameplay direction;
- content-system rules;
- system architecture;
- persistent state;
- runtime ownership;
- cross-system contracts;
- resource, scene, or authoring taxonomy;
- any old-document cleanup that changes future agent behavior.

Small typo fixes and local wording corrections do not need a proposal unless they change meaning.

## Directory Layout

```text
design-proposals/
  active/
    YYYY-MM-DD-short-slug/
      proposal.md
      current/
        gameplay-design/...
        system-design/...
      expected/
        gameplay-design/...
        system-design/...
      implementation-notes.md
      acceptance.md
  archived/
    README.md
    AGENTS.md
    .aiignore
    YYYY-MM-DD-short-slug/
      proposal.md
      current/
      expected/
      implementation-notes.md
      acceptance.md
```

`current/` and `expected/` must preserve repository-relative paths. Merge means:

```text
expected/<relative-path> -> <relative-path>
```

## Status

Every `proposal.md` must use one status:

```text
Draft
Accepted
Implementing
Implemented
Merged
Archived
```

Status meanings:

| Status | Meaning |
|---|---|
| Draft | Current and expected design are still being discussed. |
| Accepted | User approved expected design; implementation may start. |
| Implementing | Code, resources, or old docs are being changed against expected design. |
| Implemented | Implementation is done and waiting for final acceptance. |
| Merged | Expected copies have replaced authority documents. |
| Archived | Proposal was moved to `archived/` and is no longer an active reference. |

## Fixed Flow

1. Identify affected authority documents.
2. Read current authority documents.
3. Present current design or architecture to the user.
4. Present expected design or architecture to the user.
5. Wait for user acceptance.
6. Create an active proposal with `current/` and `expected/` copies.
7. Implement against `expected/`, not against unstated conversation memory.
8. If expected design changes during implementation, pause and request acceptance again.
9. After implementation acceptance, merge `expected/` into authority documents.
10. Move the proposal to `archived/` and update `archived/README.md`.

## Hard Rules

- Do not replace authority documents with unaccepted expected copies.
- Do not directly edit authority design or architecture documents for proposal-scoped changes.
- Do not treat archived proposal bodies as active design input.
- Do not use archived proposal bodies for context unless the user explicitly requests a specific archive read.
- Active proposals are valid working references. Archived summaries are valid orientation references.
- After merge, authority documents are the source of truth, not the proposal.

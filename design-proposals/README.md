# Design Proposals

This directory stores local proposal copies for product, gameplay, content-system, and architecture changes.

It exists because this project does not rely on frequent Git commits for every design step. A proposal is the local equivalent of a reviewed branch:

```text
authority document
+ current copy
+ expected copy
+ user acceptance
+ merge expected copy back to authority
+ archive proposal
+ focused implementation proposal under gameplay-alignment/implementation-proposals/
+ implementation
+ implementation acceptance record
```

Design proposals change accepted documents. They do not authorize code, scene, resource, or data implementation directly.

Implementation proposals live under `gameplay-alignment/implementation-proposals/` after the originating design proposal has been merged into authority documents and archived.

## Relationship Metadata

Every default AI-readable proposal entry must expose:

```text
Requirement Id
Parent Proposal
Supersedes
Superseded By
Amends
Amended By
Affected Authority Documents
Related Implementation Proposals
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
      acceptance.md
  archived/
    README.md
    AGENTS.md
    .aiignore
    YYYY-MM-DD-short-slug/
      proposal.md
      current/
      expected/
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
Merged
Archived
```

Status meanings:

| Status | Meaning |
|---|---|
| Draft | Current and expected design are still being discussed. |
| Accepted | User approved expected design; authority merge may proceed. |
| Merged | Expected copies have replaced authority documents; proposal may be archived. |
| Archived | Proposal was moved to `archived/` and is no longer an active reference. |

## Fixed Flow

1. Identify affected authority documents.
2. Read current authority documents.
3. Present current design or architecture to the user.
4. Present expected design or architecture to the user.
5. Wait for user acceptance.
6. Create an active proposal with `current/` and `expected/` copies.
7. Merge the accepted `expected/` copies into authority documents.
8. Move the proposal to `archived/` and update `archived/README.md`.
9. Start implementation work only through a focused proposal under `gameplay-alignment/implementation-proposals/`.

If implementation later proves the accepted design wrong, create a new amendment or supersession proposal. Do not edit archived proposal bodies. Update only index or relationship metadata needed for future agents to follow the chain.

## Hard Rules

- Do not replace authority documents with unaccepted expected copies.
- Do not directly edit authority design or architecture documents for proposal-scoped changes.
- Do not use active design proposals as implementation authority.
- Do not treat archived proposal bodies as active design input.
- Do not use archived proposal bodies for context unless the user explicitly requests a specific archive read.
- Active proposals are valid working references. Archived summaries are valid orientation references.
- After merge, authority documents are the source of truth, not the proposal.

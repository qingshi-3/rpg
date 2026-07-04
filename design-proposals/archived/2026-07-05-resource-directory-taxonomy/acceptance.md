# Acceptance

Status: Accepted by user direction.

## User Direction

The user identified the current directory taxonomy as wrong:

```text
assets is for material/source assets, not scenes or authored resources.
Create a resource directory at the same level as src.
Inventory resource-like files and migrate them in batches under resource.
```

The user also accepted a temporary exception:

```text
frames.tres stays beside its source assets for now to preserve preview convenience.
It may be migrated later after the pipeline matures.
```

The user requested a careful execution plan before migration:

```text
Path moves can easily break scene references. Do detailed inventory and staged execution first.
Do not migrate everything at once.
```

## Merge State

- Expected authority copy prepared: Yes
- Merged to authority documents: Yes
- Archived: Yes
- Follow-up implementation proposal created: Pending in this execution

## Review Notes

This proposal changes repository taxonomy only. It does not authorize direct resource migration. The follow-up migration proposal must preserve path stability through static audits, focused batches, and verification after each batch.

# Strategic Management Core Foundation Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-battle-bridge-contract/proposal.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

The accepted architecture says Strategic Management is a clean rebuild with five layers: content definitions, strategic state, rules, commands, and presentation/external boundaries. Strategic state mutates only through Strategic Management commands. Legacy world/site/action/garrison structures are not long-term authority for this work.

## Goal

Build the first independent Strategic Management kernel: definitions, serializable state, read-only rules, and command mutation for the smallest city/resource/facility/corps/hero loop.

The slice proves that a city can derive muster templates from city identity, source locations, facilities, and resources; create persistent corps instances; assign a corps to a hero; and reject invalid operations without partial state mutation.

## Scope

- Add new Strategic Management namespaces instead of extending legacy `World` authority:
  - `Rpg.Definitions.StrategicManagement`
  - `Rpg.Domain.StrategicManagement`
  - `Rpg.Application.StrategicManagement`
- Add focused first-slice content definitions:
  - resource types: food, money, building materials, beast materials;
  - strategic locations: one core plains human city, one resource site, one beast minor site;
  - city identity for plains human city;
  - facility definitions for a basic training facility and beast pen;
  - corps definitions for shield, archer, cavalry, wolf pack assault, and great beast charge;
  - hero aptitude tags for ordinary commander and beast-capable commander.
- Add durable strategic state records:
  - faction resource store;
  - strategic location control state;
  - city state with limited facility slots;
  - facility instances;
  - corps instances with strength, level, equipment level, state, home city, and assignment;
  - hero strategic state and corps assignment.
- Add read-only rules:
  - resource affordability;
  - location control/source permission;
  - facility slot and facility build eligibility;
  - city-supported muster template availability and disabled reasons;
  - corps creation eligibility;
  - hero-corps assignment aptitude result.
- Add commands as the only state mutation entry point:
  - add or spend resources;
  - occupy or lose a strategic location;
  - build a city facility;
  - create a corps instance from a city-supported muster template;
  - assign or unassign a corps to a hero.
- Add command result contracts with success, failure reason, changed facts, and strategic events.
- Add regression tests for rules, commands, and no-partial-mutation behavior.
- Add low-noise diagnostics around accepted commands and rejected commands.

## Non-Goals

- Do not replace the current strategic world UI in this slice.
- Do not wire Strategic Management to scene transition or battle Runtime in this slice.
- Do not implement the Strategic Battle Bridge session in this slice.
- Do not delete old `StrategicWorldState`, `WorldSiteState`, `WorldArmyState`, `WorldActionResolver`, or current presentation roots in this slice.
- Do not add save/load integration in this slice.
- Do not add a generic config loader, scripting language, expression tree, technology framework, diplomacy, logistics, or regional transport loss.
- Do not implement resource-site UI, city panel UI, corps roster UI, or hero assignment UI in this slice.
- Do not model individual soldier stockpiles.
- Do not make beast corps use random control failure.

## Touched Systems

- Create `src/Definitions/StrategicManagement/` for first-slice definitions and content IDs.
- Create `src/Domain/StrategicManagement/` for strategic state records and enums.
- Create `src/Application/StrategicManagement/` for rules, commands, command results, and diagnostics.
- Create `tests/StrategicManagementRegression/` for focused .NET regression coverage.
- Update `gameplay-alignment/implementation-proposals/README.md` index.

## Implementation Slices

### Slice 1: Contracts And State

- Define content IDs and first-slice definition objects.
- Define strategic state objects with no Godot node references.
- Define command result and strategic event contracts.
- Add tests proving the state can be created without legacy `World` state or Godot scene nodes.

### Slice 2: Rules

- Implement affordability, facility, source-permission, muster-template, and assignment-aptitude queries.
- Add tests proving:
  - a plains human city exposes common corps templates from identity;
  - beast corps templates require both controlled beast minor site and beast pen;
  - losing the beast site disables new beast corps creation without deleting existing corps instances;
  - disabled reasons are deterministic and player-readable through rule result codes.

### Slice 3: Commands

- Implement resource, location-control, facility-build, corps-create, and hero-corps assignment commands.
- Add tests proving:
  - facility build consumes resources and a slot;
  - facility build rejects insufficient resources or full slots without partial mutation;
  - corps creation consumes resources and creates a persistent corps instance at full strength;
  - invalid corps creation rejects without partial resource or corps-state mutation;
  - hero-corps assignment records assignment and aptitude without random failure.

### Slice 4: Architecture Guards

- Add regression guards proving the new Strategic Management Application layer does not reference legacy `Rpg.Domain.World` state types.
- Add regression guards proving command tests mutate state only through command services.
- Add focused diagnostics assertions only where logs represent important state transitions or rejection reasons.

## Tests

Primary verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
```

Expected result:

```text
PASS strategic management state initializes without legacy world state
PASS common city identity derives common muster templates
PASS beast muster requires controlled beast source and beast pen
PASS losing beast source keeps existing corps but blocks new beast creation
PASS build facility consumes resources and facility slot
PASS build facility failure leaves resources and slots unchanged
PASS create corps consumes resources and creates persistent corps instance
PASS create corps failure leaves resources and corps list unchanged
PASS assign corps to hero records aptitude without random failure
PASS strategic management application has no legacy world state dependency
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected result: build succeeds after the full slice is implemented.

## Diagnostics

- Accepted commands log command kind, primary target ID, resource deltas, and changed strategic fact IDs.
- Rejected commands log command kind, primary target ID, and structured failure reason.
- Logs must stay low-noise and must not run per frame.

## Manual QA

No Godot editor launch is required for this foundation slice because it has no UI or scene integration.

Manual QA begins in a later presentation slice after city/resource/corps view models are connected to the large-map presentation.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed before implementation because `Rpg.Application.StrategicManagement`, `Rpg.Definitions.StrategicManagement`, and `Rpg.Domain.StrategicManagement` did not exist.
- 2026-06-14: Implemented the first independent Strategic Management kernel under `src/Definitions/StrategicManagement`, `src/Domain/StrategicManagement`, and `src/Application/StrategicManagement`. The slice includes first-slice definitions, serializable state, read-only rule queries, command mutation entry points, structured failure reasons, strategic events, and low-noise command diagnostics.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed. The suite covers state initialization without legacy world state, common and beast muster rules, source-loss behavior, facility build mutation/no-partial-mutation, corps creation mutation/no-partial-mutation, hero-corps assignment aptitude, and a guard against Strategic Management Application references to legacy `Rpg.Domain.World` state.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.

# Acceptance

Accepted on 2026-06-09.

The user requested Sanguo Qunying-style multi-unit expedition behavior:

- multiple units can travel together in one expedition and fight one battle;
- battle preparation allows optional company deployment;
- not every carried company must be deployed;
- at least one company must be deployed before battle start.

Implementation scope accepted for the first slice:

- undeployed companies are reserve-only;
- undeployed companies do not enter Runtime;
- undeployed companies do not suffer battle casualties;
- no mid-battle reinforcement flow is required.

# ADR-0001: AAEmu 1.2 is the sole primary target

Status: Accepted
Date: 2026-08-31

## Decision

Develop PlayerBots features and run milestone acceptance against AAEmu 1.2 r208022 at host commit `62e3eb1d87da01194802ac886cd500134facad28` until the one-zone Population Director is accepted.

AAEmu 3.0.4.2 r336598 remains a frozen compatibility target. Its host base, alpha-v4 compatibility patch, client/assets, database provenance, module tests, and accepted evidence are retained. It receives clean install/build/adapter regression only at major milestone or release boundaries.

## Evidence

- AAEmu 1.2 has physical 100, 500, and 1,000 bot movement evidence.
- Its 99-bot fight recorded 5,192 successful casts out of 5,193 attempts.
- It has broader manager, party, governance, trial, crime, battleground, and ship-system foundations.
- The module manifest classifies 1.2 as `supported` and 3.0 as `server-start-validated`.
- The measured empty 3.0 Game runtime used approximately 6.35 GB working set, while its scale and recovery acceptance remains open.

## Consequences

- No new 3.0 feature work, quest expansion, role promotion, or live scale campaign.
- Shared behavior remains version-neutral and host calls remain behind adapters.
- 1.2 receives focused writer tests, integration suites, and physical milestone gates.
- Porting to 3.0 or another AAEmu line starts after the population foundation is accepted.

# T-126 contract: delivery audit and operating-model reset

Use Git history, the authoritative ops ledger, registered workspace state, and
the checked-in module wiring to distinguish code volume from working product.
Classify recent changes as live-proven, deployable but unproven, useful support,
or overhead. Record contradictions between documentation, task status, source,
installed runtime, and player-visible behavior without reinterpreting failed or
incomplete receipts.

Use the official `mod-playerbots/mod-playerbots` repository as a capability and
runtime-operating reference. Copy neither World of Warcraft content nor its
implementation mechanically; retain ArcheAge-native combat, quests, transport,
trade, housing, naval play, faction conflict, and world persistence as the
product domain.

Install a successor model with a short executable-goal ledger, one active
vertical slice, explicit deployment states, and client-visible acceptance. A
source or test PASS is not product completion. Completion requires the exact
source and host adapters to be installed, the server to run against a named
test-data version, native server state to show the expected outcome, and an
active client to witness the player-facing behavior. Headless tests remain
required for deterministic regression and scale, but cannot substitute for the
live gate.

Preserve the old task ledger and every retained failure as evidence. Do not
touch the leased T-120 runtime, database, client, process state, or lease in this
task. Do not edit feature source. End with a concise handoff identifying the
first executable goal and the exact runtime transition that a separately
authorized task must perform.

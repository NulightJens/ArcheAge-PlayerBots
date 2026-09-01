# T-103 contract: integrate and install exact bot transform telemetry

Act only as the Integrator for T-102 candidate
`70b577861a5f8fef8df4be40c61af3ba66298ed3`, sole parent
`9a94b21fd4f9855346109cfe7b5b941df13e6b4b`, tree
`2476ff1be7040f028882d54ef44b5371e4ed9f14`, and stable patch ID
`ed539b217262a89a3a7d20b32810582d7a9d5e9c`. Start only after PB-000
commits the exact T-103 binding and build-only lease. Require the candidate to
change exactly the four T-102 paths and independently verify their blobs:

- `bb771ced28f10665a4ca453d1fb06738f47cd680` command source;
- `d59389ba77980db37b387858a2de863308c89e42` command tests;
- `d25bcfb8ebc73759fd2bfcbd12220eaea7096f07` source receipt;
- `f7cd35cfca6ed7f1f2cebdd18eda31000ecef939` source handoff.

Replay those blobs byte-identically onto the committed saved lineage. Do not
merge the writer branch, rewrite candidate content, or add source changes.

Before any registered-host write, require the pinned reference clean and
detached at `62e3eb1d87da01194802ac886cd500134facad28`, the expected retained
30-entry integration-host overlay, the clean installed module at source/tree
`e0af81eb43d244d69114f3e69fbf5efbdfdc9d96` /
`7afaac89eb0d5ef53475064134e69b7e7527eb58`, and compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
Require strict reference apply-check and complete integration-host reverse
check. Stop without mutation on any identity, cleanliness, overlay, or patch
mismatch.

Install the exact replay commit into the registered AAEmu 1.2 integration
module without changing host seams beyond the already accepted patch. Prove
installed commit/tree equality and a clean installed module. Run the complete
46-case `BotCommandsTests` selection and require 46 pass, zero fail, zero skip,
including the exact invariant transform line, default world ID `0`, bit-exact
single-precision yaw round trip, retained diagnostics, and no state mutation.
Perform one no-incremental AAEmu 1.2 solution build with zero errors; retain
the exact warnings and installed Game assembly SHA-256.

Consume exactly one complete AAEmu 1.2 full-suite invocation for this
integration wave. The expected delta from T-101 is the single new T-102 test:
1,864 passed, four intentional legacy-golden skips, zero failed, 1,868 total.
Investigate and stop on any count or skip-identity mismatch. Never repeat the
full suite. Never start Login/Game or observers, edit deployed runtime config,
access/control database or client, or touch external runtime evidence.

After every replay/install/focused/build/full-suite gate passes, commit only
the T-103 integration receipt and handoff after the replay commit, then
fast-forward saved branch `integration/aaemu12-world` through exactly those
two commits without rewriting history. Report replay commit/tree, final saved
head/tree, installed source/tree, stable patch identity, compatibility patch
and assembly hashes, focused/build/full-suite proof, retained warnings and
anomalies, clean stopped runtime state, and the exact next action. PB-000 may
then release the build lease and bind a fresh versioned runtime successor;
live transform telemetry, predicted one-shot fixture placement, both lifecycle
waves, restart, and dwell remain unproven until that successor passes.

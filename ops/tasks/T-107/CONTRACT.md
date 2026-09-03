# T-107 contract: integrate and install bounded relative-yaw placement

Act only as the Integrator for T-106 candidate
`8e091a7c45ea3ad4403dc0926d071b4156c8949a`, sole parent
`8b7a10101910c704b5f2d08129d7468cf36b8796`, tree
`5fd50e919afab1caeac909fbaf88637ca943cab6`, and stable patch ID
`1658582af94e18e4376ec0e8ffde12926d32fa0b`. Start only after PB-000
commits the exact T-107 task binding and build-only lease. Require the candidate
to change exactly the four T-106 paths and independently verify their blobs:

- `893f7d639477bd8cbc4fc9911f63cb642733a8cf` command source;
- `664e1b595e00d9659f3cc13c40e1c8ba5350ac40` command tests;
- `a08fd0c7c643e5cb4baabe30c506befc9c9305a2` source receipt;
- `cddda2f69d70c01869d50c4a2146ea804326872b` source handoff.

Replay those blobs byte-identically onto the committed saved lineage. Do not
merge the writer worktree, rewrite candidate content, or add source changes.
The grammar must remain exactly
`spawnpassive <npcTemplateId> [distance] [anchorBotId] [yawOffsetDegrees]`;
the invalid-angle error must remain exactly
`Yaw offset degrees must be a finite invariant number from -180 through 180.`

Before any registered-host write, require the pinned reference clean and
detached at `62e3eb1d87da01194802ac886cd500134facad28`, the integration host overlay
to match its current accepted T-103 receipt exactly, the clean installed module
at source/tree `bf0ae36fc65eea4f341b936c9f7a961e9d474580` /
`7f33545311c972d7a0ed31fa7f75a016df3571f7`, and compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
Require strict reference apply-check and complete integration-host reverse
check. Stop without mutation on any identity, cleanliness, overlay, lease, or
patch mismatch.

Install the exact replay commit into the registered AAEmu 1.2 integration
module without changing host seams beyond the accepted compatibility patch.
Prove installed commit/tree equality and a clean installed module. Run the
complete 46-case `SpawnPassiveNpcCommandTests` selection and require 46 pass,
zero fail, zero skip, including invariant/range parsing, bit-exact zero and
signed-angle geometry, default world `0`, retained anchor guards, and no source
state mutation. Perform one no-incremental AAEmu 1.2 solution build with zero
errors; retain the exact warnings and installed Game assembly SHA-256.

Consume exactly one complete AAEmu 1.2 full-suite invocation for this
integration wave. The expected delta from T-103 is six passing T-106 cases:
1,870 passed, four intentional legacy-golden skips, zero failed, 1,874 total.
Investigate and stop on any count or skip-identity mismatch. Never repeat the
full suite. Never start Login/Game or observers, edit deployed runtime config,
access/control database or client, or touch external runtime evidence.

After every replay/install/focused/build/full-suite gate passes, commit only
the T-107 integration receipt and handoff after the replay commit, then
fast-forward saved branch `integration/aaemu12-world` through exactly those
two commits without rewriting history. Report replay commit/tree, final saved
head/tree, installed source/tree, stable patch identity, compatibility patch
and assembly hashes, focused/build/full-suite proof, retained warnings and
anomalies, clean stopped runtime state, and the exact next action. PB-000 may
then release the build lease and bind fresh T-108 v7 runtime evidence; physical
angular placement, both lifecycle/refill waves, restart, and dwell remain
unproven until T-108 passes and is independently integrated.

# T-115 contract: integrate and install per-runtime recovery observability

Act only as the Integrator for T-114 candidate
`3b9824e5250caf90a6347cb286e31deda993a0cf`, sole parent
`d335ce053c58eaebbfda384412ef9be5c9d839da`, tree
`e342e61030ae78ab411fa7cb6a109839199ca610`, and stable patch ID
`f2172268436a17d0642b837ed5e0696b81c316d6`. Start host writes only after
PB-000 commits the exact T-115 task/thread/worktree build-only lease. Require
the candidate to change exactly these seven paths and independently verify the
listed blobs:

- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`:
  `3dcb8c803fad04657132c16ee37c0fec64911c9f`;
- `scripts/autonomy/AutonomyObserver.psm1`:
  `173a2de8f8844f16be463ab0779670f0fe264198`;
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`:
  `38f199f79d9e8f5116678625b2f912bd752407fe`;
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`:
  `e2158cd316bf1e809653f722de7764719c70911a`;
- `scripts/autonomy/tests/Test-AutonomyObserver.ps1`:
  `587be6192a9138cb9c3c94ec9c612e16db56bcec`;
- `ops/evidence/aaemu12-runtime-recovery-observability-v1.yaml`:
  `0ae6b043427c0e288e1dcd104073d3d964412b2b`;
- `ops/tasks/T-114/HANDOFF.md`:
  `26ea78391a6ccc95cdd77e5c8f0dd4ea697af154`.

Replay those blobs byte-identically onto committed saved head
`d147bebce60ee79588e0e430f47969e9ba8662b9`. Do not merge the writer
worktree, rewrite candidate content, or add source changes. Independently
confirm the production diff is limited to the invariant per-runtime botdebug
line and observation-only schema-v2 parser, with no lifecycle, scheduler,
mover, brain, combat, recovery, logout, or host-metric semantic change.

Before any registered-host write, require the pinned reference clean and
detached at `62e3eb1d87da01194802ac886cd500134facad28`, the integration host overlay
to match the current accepted T-107 receipt exactly, the clean installed module
at source/tree `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
`a1b9302625a65e10dfa9b7e11393a67134f914e8`, and compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
Require strict reference apply-check and complete integration-host reverse
check. Stop without mutation on any identity, cleanliness, overlay, lease, or
patch mismatch.

Install the exact replay commit into the registered AAEmu 1.2 integration
module without changing host seams beyond the accepted compatibility patch.
Prove installed commit/tree equality and a clean installed module. Run the
complete deterministic observer qualification and require exactly 91 green
assertions with the fixed route/command/parameter/AST allowlists. Run the
complete `BotCommandsTests` selection and require 46 pass, zero fail, zero
skip. Run the complete `BotHostBehaviorTests` selection and require 34 pass,
zero fail, zero skip, including suspended runtime deltas `0/0`, peer and host
deltas `+3/+3`, and exactly one logout after full-resource restoration.

Perform one no-incremental AAEmu 1.2 solution build with zero errors; retain
the exact warnings and installed Game assembly SHA-256. Consume exactly one
complete AAEmu 1.2 full-suite invocation for this integration wave. The
expected delta from T-107 is two passing T-114 cases: 1,872 passed, four
intentional legacy-golden skips, zero failed, 1,876 total. Validate the exact
four skip identities and stop on any mismatch. Never repeat the full suite.

Never start Login/Game or live observers, edit deployed runtime config,
access/control database or client, or touch external runtime evidence. Do not
modify the clean reference, global ledgers, lease, workspace registry, AAEmu
3.0, scale, soak, or packaging.

After every replay/install/focused/build/full-suite gate passes, commit only
the T-115 integration receipt and handoff after the replay commit, then
fast-forward saved branch `integration/aaemu12-world` through exactly those
two commits without history rewrite. Report replay commit/tree, final saved
head/tree, installed source/tree, stable patch identity, compatibility patch
and assembly hashes, observer/focused/build/full-suite proof, warnings,
retained anomalies, and clean stopped runtime state. PB-000 may then release
the build-only lease and bind a fresh v10 runtime successor. Physical sampling,
two-wave recovery, shutdown-tail closure, restart, and dwell remain unproven
until that fresh successor passes and is independently integrated.

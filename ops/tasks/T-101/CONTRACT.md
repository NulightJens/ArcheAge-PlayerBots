# T-101 contract: integrate and install default-world anchor correction

Act only as the Integrator for T-100 candidate
`043bf7aff4619808a050538c6763f0c6ab2e2ee8`, parent
`96b8b5004a89a0dec6522753f64d03fd1d732c5b`, tree
`5e5a1795466f8d24f2adef4640a47f1f01e75880`, and the exact committed T-101
binding/build-only lease. Require the candidate to change exactly the four
T-100 paths. Replay it byte-identically onto the committed saved lineage; do
not rewrite candidate content or add source changes.

Before writing the registered integration module, require the pinned clean
reference at `62e3eb1d87da01194802ac886cd500134facad28`, the expected retained
30-entry integration-host overlay, the clean currently installed module source
`761ffa1e0bd76d06532688f34b45e192a493b239` and tree
`c36b1255cccb3a782e44e87a56bc9e867a946048`, and compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
Require the patch to apply strictly to the reference and reverse-check against
the installed host. Stop without mutation on any mismatch.

Install the exact replay source into the registered AAEmu 1.2 integration
module without modifying host seams beyond the already accepted patch. Prove
the installed source and tree equal the replay. Run the complete 40-case
`SpawnPassiveNpcCommandTests` selection and require 40 pass, zero fail, zero
skip. Perform one clean AAEmu 1.2 build and require zero errors; retain exact
warning count and installed Game assembly SHA-256.

Consume exactly one complete AAEmu 1.2 full-suite invocation for this
integration wave. The expected result is 1,863 passed, four intentional skips,
zero failures, 1,867 total; investigate and stop on any count or identity
mismatch. Do not repeat the full suite. Never start Login/Game or observers,
edit deployed runtime configuration, access/control database or client, or
touch external runtime evidence.

After every source/install/build/test gate passes, fast-forward saved branch
`integration/aaemu12-world` only through the replay and the commit containing
`ops/evidence/aaemu12-passive-anchor-default-world-zero-integration-v1.yaml`
and `ops/tasks/T-101/HANDOFF.md`. Report replay commit/tree, final integration
head/tree, installed source/tree, patch and assembly hashes, focused/build/full
suite proof, retained warnings/anomalies, runtime state, and the exact next
action: PB-000 may release the build lease and prepare a fresh versioned runtime
successor; physical fixture placement remains unproven until then.

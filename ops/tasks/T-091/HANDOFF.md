# T-091 handoff

Verdict: `PASS-independent-review-byte-identical-replay-dual-shell`. The exact
T-090 candidate was independently reviewed, replayed byte-for-byte onto the
committed T-091 binding lineage, and qualified once under each required shell
without a runtime lease.

## Source identity

- Preparation base: `020c5cc1b4bfabb002a94af004ab78dfa2fb3922`.
- Exact binding commit: `2452e1d3dd8d5476ce1de9a999c1e7f54734c0c5`.
- Candidate/parent/tree: `e03ca62e623350cfd88e9f1abe09e6b810b8626a` /
  `13522612e1af23c7066bd7836da412d02f018fad` /
  `801f144671b77466de9f1dc14e31f7efd1db6a4d`.
- Byte-identical replay commit/tree: `bf386e981649ee609fb9a38b6ffb7a2218dc2f67` /
  `be58ceaa4ea71fdea1c8145da85a9df4989b452a`.
- Candidate and replay stable patch ID:
  `557ca150a6baf7935de723a59c679a03e439e20c`.

## Changed files

The replay adds exactly:

- `scripts/autonomy/Observe-ClientProcessAbsence.ps1`
- `scripts/autonomy/tests/Test-ClientProcessObserver.ps1`
- `ops/tasks/T-090/HANDOFF.md`

The integrator adds exactly:

- `ops/evidence/aaemu12-client-process-observer-integration-v1.yaml`
- `ops/tasks/T-091/HANDOFF.md`

All three replay blob IDs match the candidate. There are no unexpected paths.

## Independent review and proof

The observer is compatible with Windows PowerShell 5.1 and uses framework
SHA-256 over exact bytes without a `Get-FileHash` command dependency. Its raw,
ledger, ready, summary, and error artifacts use create-new/atomic publication;
raw sample and hash-chain row zero are durable before readiness. Sample numbers
are monotonic, process names/PIDs/counts are exact, sentinel shutdown is the
only success path, summary counts/gap/terminal chain are complete, and query,
write, hash, and refusal failures close without observed-process mutation. No
product or contract defect was found.

- Windows PowerShell 5.1: exactly one complete run, `105` assertions, `4`
  chained zero-match samples, `2` current-shell samples, exit `0`.
- `pwsh` 7.6.4: exactly one complete run, `105` assertions, `4` chained
  zero-match samples, `2` current-shell samples, exit `0`.
- Each run used a distinct fresh retained root under
  `scripts/autonomy/.test-runs`; no prior artifacts were overwritten or
  deleted. Raw test output is not committed.
- Sanitized receipt:
  `ops/evidence/aaemu12-client-process-observer-integration-v1.yaml`.

## Retained failures, runtime state, and boundaries

There are no retained product or gate failures. No registered host, module,
config, runtime/evidence root, Login, Game, MySQL/database, or client was
accessed or changed. No runtime was started, stopped, or controlled; the
runtime lease remained absent. Global ledgers/lease, product source, patches,
the full suite, scale, soak, packaging, and AAEmu 3.0 were untouched.

This PASS proves repository integration and isolated deterministic observer
behavior only. It does not prove client absence in a live run, Director
bootstrap, admission, fixture placement, autonomous waves, progression,
recovery, logout/refill, restart persistence, scale, soak, packaging, or
AAEmu 3.0. T-088 remains INCOMPLETE and T-037 remains blocked until a fresh
runtime successor passes and is independently integrated.

## Exact integration action

Fast-forward saved branch `integration/aaemu12-world` from binding commit
`2452e1d3dd8d5476ce1de9a999c1e7f54734c0c5` through replay commit
`bf386e981649ee609fb9a38b6ffb7a2218dc2f67` and the scoped commit containing
this receipt and handoff, without a merge commit or history rewrite. PB-000 may
then prepare a fresh runtime successor with a new exact task/thread/worktree
binding, runtime lease, and versioned evidence root.

# T-090 handoff: portable continuous client-process observer

## Source identity

- Preparation base: `5308fe24ae64de7b9c080a4094dde6290e379e35`
- Exact PB-000 binding commit: `13522612e1af23c7066bd7836da412d02f018fad`
- Bound task/thread/worktree: T-090 / `01a05dfc-60e5-71a2-945f-8b938195c8ee` / `C:\Users\jensh\.codex\worktrees\99a2\PB-W00-control`
- Candidate: the single scoped commit containing this handoff; its immutable hash is returned to PB-000 in the completion message.

## Changed files

- `scripts/autonomy/Observe-ClientProcessAbsence.ps1`
- `scripts/autonomy/tests/Test-ClientProcessObserver.ps1`
- `ops/tasks/T-090/HANDOFF.md`

The observer accepts a fresh output directory, an external cooperative stop sentinel, a 10-60000 ms interval, and exact client process base names. It uses framework SHA-256 over exact bytes, atomic create-new raw/marker/summary/error files, a durably flushed JSONL hash chain, and publishes readiness only after raw sample 0 and row 0 are durable. Sentinel shutdown writes the required gap/count/error/terminal-chain summary and exits zero. Refusals and unexpected query/hash/write failures do not write a success summary; failures after output creation retain `error.json` and exit nonzero.

## Proof

- Retained T-088 diagnosis: its Windows PowerShell child retained raw sequence 0, then failed before the first derived row because `Get-FileHash` was unavailable. The repository observer has no dependency on that command.
- `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/autonomy/tests/Test-ClientProcessObserver.ps1`: PASS, 105 assertions, 4 chained zero-match samples, 2 current-shell samples, exit 0. Retained proof root: `scripts/autonomy/.test-runs/client-observer-20260901T1737109161739Z-119420-4b7fc45054de49b78fe20c94b08b33ab`.
- `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/autonomy/tests/Test-ClientProcessObserver.ps1`: PASS, 105 assertions, 4 chained zero-match samples, 2 current-shell samples, exit 0. Retained proof root: `scripts/autonomy/.test-runs/client-observer-20260901T1737108819625Z-119832-0f2a7df6135d4686bf3b0e9eea7ddf97`.
- The harness independently recomputed every raw SHA-256 and ledger-row hash, verified previous/current chain linkage, observed ready only after raw/row 0, recomputed the maximum adjacent gap, detected its own live shell PID, exercised cooperative stop, injected a deterministic query failure, and proved existing-output, existing-sentinel, missing-parent, invalid-interval, and invalid-name refusals.
- `git diff --check`: PASS.
- Earlier harness-development attempts remain retained under `scripts/autonomy/.test-runs/client-observer-*`; none is claimed as final proof and none was deleted or cleaned.

## Runtime state and boundaries

- Runtime lease: none. AAEmu, ArcheAge, Login, Game, MySQL, databases, clients, registered host/module/config/evidence roots, global ledgers/lease, product source, compatibility patches, full suite, scale, soak, packaging, and AAEmu 3.0 were untouched.
- Test artifacts are fresh, isolated, ignored paths. No prior artifact was overwritten or deleted, and every spawned observer exited through cooperative sentinel or fail-closed refusal/error handling.
- Physical runtime acceptance remains unproven and out of scope.

## Exact integration action

PB-000 should dispatch an independent no-runtime integration task against the immutable candidate hash from the completion message. That integrator should verify the candidate parent/binding lineage, confirm the three-file scope, rerun the complete test script under both `powershell.exe -NoProfile` and `pwsh -NoProfile`, and integrate only if both remain green. A fresh separately bound runtime proof may be dispatched only after that integration.

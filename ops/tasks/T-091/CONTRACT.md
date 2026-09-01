# T-091 contract: integrate portable client-process observer

Verify candidate `e03ca62e623350cfd88e9f1abe09e6b810b8626a`, exact
parent/binding `13522612e1af23c7066bd7836da412d02f018fad`, clean writer
worktree, and exactly the observer, test harness, and T-090 handoff. Review the
implementation independently: Windows PowerShell 5.1 compatibility; no
`Get-FileHash` command dependency in the observer; exact-byte framework
SHA-256; create-new/atomic artifacts; monotonically numbered raw samples and
durably flushed hash-chain rows; readiness only after raw/row zero; exact
process identities/counts; cooperative sentinel-only success; summary counts,
maximum gap and terminal chain; and fail-closed error/refusal behavior without
observed-process mutation. Reject rather than repair any defect.

Replay all three candidate blobs byte-identically onto the current saved
integration lineage. Run the complete deterministic harness once under
`powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass` and once under
`pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass`, each using a distinct fresh
artifact directory and retaining prior attempts. Require exit zero and exactly
105 assertions from each host, including chain/hash recomputation, ready
ordering, current-shell detection, cooperative stop, injected query failure,
and refusal cases. Do not delete or clean artifacts and do not run a full suite.

If green, add one sanitized integration receipt and T-091 handoff, then
fast-forward `integration/aaemu12-world` without merge commit or history
rewrite. Commit only the five declared paths and leave worktrees clean. This
authorizes PB-000 to prepare a fresh runtime successor, but proves no live
runtime behavior itself. Never use the registered host, runtime lease, config,
Login/Game/MySQL/database/client, retained evidence, ledgers/lease, scale, soak,
packaging, or AAEmu 3.0.
